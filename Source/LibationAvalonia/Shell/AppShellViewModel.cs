using ApplicationServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using LibationAvalonia.DesignSystem;
using LibationAvalonia.Features.Accounts;
using LibationAvalonia.Features.Downloads;
using LibationAvalonia.Features.Flight;
using LibationAvalonia.Features.History;
using LibationAvalonia.Features.Library;
using LibationAvalonia.Features.Overview;
using LibationAvalonia.Features.Processing;
using LibationAvalonia.Features.Settings;
using LibationAvalonia.Features.Tools;
using LibationAvalonia.Features.Trash;
using LibationAvalonia.ViewModels;
using LibationFileManager;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;

namespace LibationAvalonia.Shell;

/// <summary>
/// Presentation-only shell state. The existing MainVM remains the owner of
/// library commands, ProductsDisplayViewModel, and ProcessQueueViewModel.
/// </summary>
public sealed class AppShellViewModel : ViewModelBase, IDisposable
{
	private readonly Configuration configuration;
	private readonly ExperienceManager experienceManager;
	private bool disposed;
	private bool isDecanterDrawerOpen;
	private bool isFlightOpen;
	private bool isNavigationOverlayOpen;
	private ExperienceProfile profile;
	private Size lastEffectiveSize = new(1360, 768);

	public AppShellViewModel(
		MainVM main,
		Configuration configuration,
		ExperienceManager experienceManager)
	{
		this.configuration = configuration;
		this.experienceManager = experienceManager;
		CommandAdapter = new LibationCommandAdapter(main);
		Flight = new FlightService(configuration);
		FlightProcessor = new FlightProcessAdapter(main, configuration);
		FlightActions = new FlightActionAdapter(main);
		CurrentFlight = new CurrentFlightViewModel(Flight, configuration, main.ProcessQueue, FlightProcessor, FlightActions);
		Processing = new ProcessingViewModel(
			main.ProcessQueue,
			(book, effectiveConfiguration) => main.QueueBooksAsync([book], effectiveConfiguration));
		profile = experienceManager.CurrentProfile;
		Navigation = new NavigationService(configuration, Profile);
		Responsive = new ResponsiveLayoutService();
		Library = new LibraryViewModel(
			main.ProductsDisplay,
			Flight,
			configuration,
			CommandAdapter,
			Responsive,
			CurrentFlight.ProcessCommand);
		Downloads = new DownloadsViewModel(CommandAdapter);
		History = new HistoryViewModel(main);
		Accounts = new AccountsViewModel(CommandAdapter);
		Settings = new SettingsViewModel(CommandAdapter, configuration);
		Tools = new ToolsViewModel(CommandAdapter);
		Trash = new TrashViewModel(CommandAdapter);
		DashboardSupplement = new MainDashboardSupplementSource(main);
		Dashboard = new DashboardViewModel(
			CommandAdapter,
			Flight,
			CurrentFlight,
			new ShellDashboardNavigation(Navigation, Library),
			DashboardSupplement);
		NavigateCommand = ReactiveCommand.Create<AppRouteId>(Navigation.Navigate);
		ToggleFlightCommand = ReactiveCommand.Create(ToggleFlight);
		ToggleNavigationOverlayCommand = ReactiveCommand.Create(ToggleNavigationOverlay);
		CloseNavigationOverlayCommand = ReactiveCommand.Create(CloseNavigationOverlay);
		ToggleDecanterDrawerCommand = ReactiveCommand.Create(ToggleDecanterDrawer);
		CloseDecanterDrawerCommand = ReactiveCommand.Create(CloseDecanterDrawer);

		Navigation.RouteChanged += Navigation_RouteChanged;
		Responsive.PropertyChanged += Responsive_PropertyChanged;
		experienceManager.ProfileChanged += ExperienceManager_ProfileChanged;
		configuration.PropertyChanged += Configuration_PropertyChanged;
		LibraryCommands.LibrarySizeChanged += LibraryCommands_LibrarySizeChanged;
		Flight.SelectionChanged += Flight_SelectionChanged;
		SetActiveDestinations(Navigation.CurrentRoute.Id);
	}

	public ILibationCommandAdapter CommandAdapter { get; }
	public FlightService Flight { get; }
	public IFlightProcessAdapter FlightProcessor { get; }
	public IFlightActionAdapter FlightActions { get; }
	public CurrentFlightViewModel CurrentFlight { get; }
	public ProcessingViewModel Processing { get; }
	public MainVM Main => CommandAdapter.Main;
	public NavigationService Navigation { get; }
	public ResponsiveLayoutService Responsive { get; }
	public LibraryViewModel Library { get; }
	public DownloadsViewModel Downloads { get; }
	public HistoryViewModel History { get; }
	public AccountsViewModel Accounts { get; }
	public SettingsViewModel Settings { get; }
	public ToolsViewModel Tools { get; }
	public TrashViewModel Trash { get; }
	public DashboardViewModel Dashboard { get; }
	public IDashboardSupplementSource DashboardSupplement { get; }
	public ReactiveCommand<AppRouteId, Unit> NavigateCommand { get; }
	public ReactiveCommand<Unit, Unit> ToggleFlightCommand { get; }
	public ReactiveCommand<Unit, Unit> ToggleNavigationOverlayCommand { get; }
	public ReactiveCommand<Unit, Unit> CloseNavigationOverlayCommand { get; }
	public ReactiveCommand<Unit, Unit> ToggleDecanterDrawerCommand { get; }
	public ReactiveCommand<Unit, Unit> CloseDecanterDrawerCommand { get; }
	public AppRoute CurrentRoute => Navigation.CurrentRoute;
	public ExperienceProfile Profile
	{
		get => profile;
		private set => this.RaiseAndSetIfChanged(ref profile, value);
	}
	public bool IsCellarComposition => Profile.DashboardLayout == DashboardLayoutKind.Cellar;
	public bool IsTastingRoomComposition => Profile.DashboardLayout == DashboardLayoutKind.TastingRoom;
	public bool IsAccessibleComposition => Profile.DashboardLayout == DashboardLayoutKind.Accessible;
	public bool IsOverviewRoute => CurrentRoute.Id == AppRouteId.Overview;
	public bool IsLibraryRoute => CurrentRoute.Id == AppRouteId.Library;
	public bool IsDownloadsRoute => CurrentRoute.Id == AppRouteId.Downloads;
	public bool IsProcessingRoute => CurrentRoute.Id == AppRouteId.Processing;
	public bool IsHistoryRoute => CurrentRoute.Id == AppRouteId.History;
	public bool IsAccountsRoute => CurrentRoute.Id == AppRouteId.Accounts;
	public bool IsSettingsRoute => CurrentRoute.Id == AppRouteId.Settings;
	public bool IsToolsRoute => CurrentRoute.Id == AppRouteId.Tools;
	public bool IsTrashRoute => CurrentRoute.Id == AppRouteId.Trash;
	public bool IsAboutRoute => CurrentRoute.Id == AppRouteId.About;
	public bool UsesOverviewFlightSurface => IsOverviewRoute && IsCellarComposition;
	public bool IsRailExpanded => Responsive.Current.NavigationRail == NavigationRailState.Expanded;
	public bool IsRailOverlay => Responsive.Current.NavigationRail == NavigationRailState.Overlay;
	public bool IsNavigationRailExpanded => IsRailExpanded || IsRailOverlay;
	public bool IsNavigationRailVisible => !IsRailOverlay || IsNavigationOverlayOpen;
	public bool ShowNavigationScrim => IsRailOverlay && IsNavigationOverlayOpen;
	public GridLength NavigationColumnWidth => IsRailOverlay ? new GridLength(0) : GridLength.Auto;
	public bool IsContextPanePersistent => Responsive.Current.ContextualPane == ContextualPaneState.Persistent;
	public bool IsQueueCompact => Responsive.Current.QueueSurface is QueueSurfaceState.CompactBar or QueueSurfaceState.Drawer;
	public bool ShowQueueDock => configuration.ShowDecanterDock
		&& Responsive.Current.QueueSurface == QueueSurfaceState.Dock
		&& !IsProcessingRoute
		&& !IsOverviewRoute;
	public bool ShowDecanterDrawer => IsDecanterDrawerOpen && !ShowQueueDock;
	public bool IsNavigationOverlayOpen
	{
		get => isNavigationOverlayOpen;
		private set
		{
			this.RaiseAndSetIfChanged(ref isNavigationOverlayOpen, value);
			this.RaisePropertyChanged(nameof(IsNavigationRailVisible));
			this.RaisePropertyChanged(nameof(ShowNavigationScrim));
		}
	}
	public bool IsDecanterDrawerOpen
	{
		get => isDecanterDrawerOpen;
		private set
		{
			this.RaiseAndSetIfChanged(ref isDecanterDrawerOpen, value);
			this.RaisePropertyChanged(nameof(ShowDecanterDrawer));
		}
	}
	public bool IsFlightOpen
	{
		get => isFlightOpen;
		private set
		{
			this.RaiseAndSetIfChanged(ref isFlightOpen, value);
			this.RaisePropertyChanged(nameof(ShowFlightPane));
			this.RaisePropertyChanged(nameof(ShowFlightOverlay));
		}
	}
	public bool ShowPersistentFlightPane => !UsesOverviewFlightSurface && IsCellarComposition && IsContextPanePersistent;
	public bool ShowFlightOverlay => !UsesOverviewFlightSurface && IsFlightOpen && !ShowPersistentFlightPane;
	public bool ShowFlightPane => ShowPersistentFlightPane || ShowFlightOverlay;
	public bool ShowFlightToggle => !UsesOverviewFlightSurface && !ShowPersistentFlightPane;
	public ShellNavigationItemViewModel? SelectedPrimaryItem
	{
		get => Navigation.PrimaryItems.FirstOrDefault(item => item.IsSelected);
		set
		{
			if (value is not null)
				Navigation.Navigate(value.Id);
		}
	}

	public void UpdateLayout(Size effectiveSize)
	{
		lastEffectiveSize = effectiveSize;
		Responsive.Update(
			effectiveSize.Width,
			effectiveSize.Height,
			Profile,
			configuration.DecorationLevel,
			configuration.NavigationRailPreference);
	}

	private void Navigation_RouteChanged(object? sender, AppRouteChangedEventArgs e)
	{
		SetActiveDestinations(e.Current.Id);
		this.RaisePropertyChanged(nameof(CurrentRoute));
		this.RaisePropertyChanged(nameof(IsOverviewRoute));
		this.RaisePropertyChanged(nameof(IsLibraryRoute));
		this.RaisePropertyChanged(nameof(IsDownloadsRoute));
		this.RaisePropertyChanged(nameof(IsProcessingRoute));
		this.RaisePropertyChanged(nameof(IsHistoryRoute));
		this.RaisePropertyChanged(nameof(IsAccountsRoute));
		this.RaisePropertyChanged(nameof(IsSettingsRoute));
		this.RaisePropertyChanged(nameof(IsToolsRoute));
		this.RaisePropertyChanged(nameof(IsTrashRoute));
		this.RaisePropertyChanged(nameof(IsAboutRoute));
		this.RaisePropertyChanged(nameof(UsesOverviewFlightSurface));
		this.RaisePropertyChanged(nameof(SelectedPrimaryItem));
		this.RaisePropertyChanged(nameof(ShowQueueDock));
		this.RaisePropertyChanged(nameof(ShowFlightPane));
		this.RaisePropertyChanged(nameof(ShowPersistentFlightPane));
		this.RaisePropertyChanged(nameof(ShowFlightOverlay));
		this.RaisePropertyChanged(nameof(ShowFlightToggle));
		IsNavigationOverlayOpen = false;
		IsDecanterDrawerOpen = false;
	}

	private void SetActiveDestinations(AppRouteId route)
	{
		bool shellActive = configuration.UseContemporaryShell;
		Dashboard.SetActive(shellActive && route == AppRouteId.Overview);
		History.SetActive(shellActive && route == AppRouteId.History);
	}

	private void Responsive_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(ResponsiveLayoutService.Current))
			return;
		this.RaisePropertyChanged(nameof(IsRailExpanded));
		this.RaisePropertyChanged(nameof(IsRailOverlay));
		this.RaisePropertyChanged(nameof(IsNavigationRailExpanded));
		this.RaisePropertyChanged(nameof(IsNavigationRailVisible));
		this.RaisePropertyChanged(nameof(ShowNavigationScrim));
		this.RaisePropertyChanged(nameof(NavigationColumnWidth));
		this.RaisePropertyChanged(nameof(IsContextPanePersistent));
		this.RaisePropertyChanged(nameof(IsQueueCompact));
		this.RaisePropertyChanged(nameof(ShowQueueDock));
		this.RaisePropertyChanged(nameof(ShowDecanterDrawer));
		this.RaisePropertyChanged(nameof(ShowFlightPane));
		this.RaisePropertyChanged(nameof(ShowPersistentFlightPane));
		this.RaisePropertyChanged(nameof(ShowFlightOverlay));
		this.RaisePropertyChanged(nameof(ShowFlightToggle));
		if (!IsRailOverlay)
			IsNavigationOverlayOpen = false;
	}

	private void ExperienceManager_ProfileChanged(object? sender, ExperienceProfileChangedEventArgs e)
	{
		if (!Dispatcher.UIThread.CheckAccess())
		{
			Dispatcher.UIThread.Post(() => ExperienceManager_ProfileChanged(sender, e));
			return;
		}

		Profile = e.Current;
		this.RaisePropertyChanged(nameof(IsCellarComposition));
		this.RaisePropertyChanged(nameof(IsTastingRoomComposition));
		this.RaisePropertyChanged(nameof(IsAccessibleComposition));
		this.RaisePropertyChanged(nameof(ShowFlightPane));
		this.RaisePropertyChanged(nameof(ShowPersistentFlightPane));
		this.RaisePropertyChanged(nameof(ShowFlightOverlay));
		this.RaisePropertyChanged(nameof(ShowFlightToggle));
		UpdateLayout(lastEffectiveSize);
	}

	private void ToggleFlight()
	{
		IsNavigationOverlayOpen = false;
		IsDecanterDrawerOpen = false;
		IsFlightOpen = !IsFlightOpen;
	}
	private void ToggleNavigationOverlay()
	{
		if (IsRailOverlay)
		{
			IsDecanterDrawerOpen = false;
			IsFlightOpen = false;
			IsNavigationOverlayOpen = !IsNavigationOverlayOpen;
		}
	}
	private void CloseNavigationOverlay() => IsNavigationOverlayOpen = false;
	private void ToggleDecanterDrawer()
	{
		if (ShowQueueDock)
			Navigation.Navigate(AppRouteId.Processing);
		else
		{
			IsNavigationOverlayOpen = false;
			IsFlightOpen = false;
			IsDecanterDrawerOpen = !IsDecanterDrawerOpen;
		}
	}
	private void CloseDecanterDrawer() => IsDecanterDrawerOpen = false;

	public bool CloseTransientSurface()
	{
		if (IsNavigationOverlayOpen)
		{
			IsNavigationOverlayOpen = false;
			return true;
		}
		if (IsDecanterDrawerOpen)
		{
			IsDecanterDrawerOpen = false;
			return true;
		}
		if (IsFlightOpen)
		{
			IsFlightOpen = false;
			return true;
		}
		return false;
	}

	private void Flight_SelectionChanged(object? sender, FlightChangedEventArgs e)
	{
		this.RaisePropertyChanged(nameof(ShowFlightPane));
		if (Flight.Count == 0 && !IsContextPanePersistent)
			IsFlightOpen = false;
	}

	private void Configuration_PropertyChanged(object sender, Dinah.Core.PropertyChangedEventArgsEx e)
	{
		if (e.PropertyName is not nameof(Configuration.DecorationLevel)
			and not nameof(Configuration.NavigationRailPreference)
			and not nameof(Configuration.ShowDecanterDock)
			and not nameof(Configuration.UseContemporaryShell))
			return;
		if (!Dispatcher.UIThread.CheckAccess())
		{
			Dispatcher.UIThread.Post(RefreshConfigurationProjection);
			return;
		}
		RefreshConfigurationProjection();
	}

	private void RefreshConfigurationProjection()
	{
		SetActiveDestinations(Navigation.CurrentRoute.Id);
		UpdateLayout(lastEffectiveSize);
		this.RaisePropertyChanged(nameof(ShowQueueDock));
		this.RaisePropertyChanged(nameof(ShowDecanterDrawer));
	}

	private void LibraryCommands_LibrarySizeChanged(object? sender, List<DataLayer.LibraryBook> library)
	{
		if (Dispatcher.UIThread.CheckAccess())
			Flight.ReconcileLibrary(library);
		else
			Dispatcher.UIThread.Post(() => Flight.ReconcileLibrary(library));
	}

	public void Dispose()
	{
		if (disposed)
			return;
		disposed = true;
		Navigation.RouteChanged -= Navigation_RouteChanged;
		Responsive.PropertyChanged -= Responsive_PropertyChanged;
		experienceManager.ProfileChanged -= ExperienceManager_ProfileChanged;
		configuration.PropertyChanged -= Configuration_PropertyChanged;
		LibraryCommands.LibrarySizeChanged -= LibraryCommands_LibrarySizeChanged;
		Flight.SelectionChanged -= Flight_SelectionChanged;
		Library.Dispose();
		Downloads.Dispose();
		History.Dispose();
		Accounts.Dispose();
		Settings.Dispose();
		Tools.Dispose();
		Trash.Dispose();
		CurrentFlight.Dispose();
		Processing.Dispose();
		Dashboard.Dispose();
		Flight.Dispose();
		NavigateCommand.Dispose();
		ToggleFlightCommand.Dispose();
		ToggleNavigationOverlayCommand.Dispose();
		CloseNavigationOverlayCommand.Dispose();
		ToggleDecanterDrawerCommand.Dispose();
		CloseDecanterDrawerCommand.Dispose();
	}
}
