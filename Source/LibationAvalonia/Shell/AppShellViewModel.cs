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
	private TransientSurface activeTransientSurface;
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
		ToggleFlightCommand = ReactiveCommand.Create(ToggleFlight);
		Library = new LibraryViewModel(
			main.ProductsDisplay,
			Flight,
			configuration,
			CommandAdapter,
			Responsive,
			CurrentFlight.ProcessCommand,
			ToggleFlightCommand);
		CurrentFlight.CoverCache = Library.CoverCache;
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
			Library,
			Processing,
			new ShellDashboardNavigation(Navigation, Library),
			DashboardSupplement);
		Dashboard.SetProfile(Profile);
		NavigateCommand = ReactiveCommand.Create<AppRouteId>(Navigation.Navigate);
		ToggleNavigationOverlayCommand = ReactiveCommand.Create(ToggleNavigationOverlay);
		CloseNavigationOverlayCommand = ReactiveCommand.Create(CloseNavigationOverlay);
		ToggleDecanterDrawerCommand = ReactiveCommand.Create(ToggleDecanterDrawer);
		CloseDecanterDrawerCommand = ReactiveCommand.Create(CloseDecanterDrawer);

		Navigation.RouteChanged += Navigation_RouteChanged;
		Responsive.PropertyChanged += Responsive_PropertyChanged;
		experienceManager.ProfileChanged += ExperienceManager_ProfileChanged;
		configuration.PropertyChanged += Configuration_PropertyChanged;
		main.PropertyChanged += Main_PropertyChanged;
		LibraryCommands.LibrarySizeChanged += LibraryCommands_LibrarySizeChanged;
		Flight.SelectionChanged += Flight_SelectionChanged;
		AboutPresentation = new StaticRoutePresentation(
			"Application information",
			Properties.Resources.ShellAboutPageTitle,
			Properties.Resources.ShellAboutPageDescription,
			new("Open About and updates", Settings.OpenAboutCommand),
			[],
			null);
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
	public IRoutePresentation AboutPresentation { get; }
	public ReactiveCommand<AppRouteId, Unit> NavigateCommand { get; }
	public ReactiveCommand<Unit, Unit> ToggleFlightCommand { get; }
	public ReactiveCommand<Unit, Unit> ToggleNavigationOverlayCommand { get; }
	public ReactiveCommand<Unit, Unit> CloseNavigationOverlayCommand { get; }
	public ReactiveCommand<Unit, Unit> ToggleDecanterDrawerCommand { get; }
	public ReactiveCommand<Unit, Unit> CloseDecanterDrawerCommand { get; }
	public AppRoute CurrentRoute => Navigation.CurrentRoute;
	public IRoutePresentation CurrentRoutePresentation => CurrentRoute.Id switch
	{
		AppRouteId.Overview => Dashboard,
		AppRouteId.Library => Library,
		AppRouteId.Downloads => Downloads,
		AppRouteId.Processing => Processing,
		AppRouteId.History => History,
		AppRouteId.Accounts => Accounts,
		AppRouteId.Settings => Settings,
		AppRouteId.Tools => Tools,
		AppRouteId.Trash => Trash,
		_ => AboutPresentation,
	};
	public bool HasRouteStatusBadge => CurrentRoutePresentation.RouteStatusBadge is not null;
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
	public bool ShowRouteHeader => !IsLibraryRoute;
	public bool HasUpdateAvailable => Main.ApplicationUpdateState.Contains("available", StringComparison.OrdinalIgnoreCase);
	public TransientSurface ActiveTransientSurface
	{
		get => activeTransientSurface;
		private set
		{
			this.RaiseAndSetIfChanged(ref activeTransientSurface, value);
			this.RaisePropertyChanged(nameof(Layout));
		}
	}
	public ShellLayout Layout => BuildLayout();
	public ShellNavigationItemViewModel? SelectedNavigationItem
	{
		get => Navigation.PrimaryItems.Concat(Navigation.UtilityItems).FirstOrDefault(item => item.IsSelected);
		set
		{
			if (value is not null)
				Navigation.Navigate(value.Id);
		}
	}

	private ShellLayout BuildLayout()
	{
		var responsive = Responsive.Current;
		bool navigationOverlay = responsive.NavigationRail == NavigationRailState.Overlay;
		bool hostFlightInOverview = IsOverviewRoute
			&& IsCellarComposition
			&& responsive.ContextualPane == ContextualPaneState.Persistent;
		bool hostDecanterInOverview = IsOverviewRoute && IsTastingRoomComposition;
		bool showPersistentFlight = !hostFlightInOverview
			&& IsCellarComposition
			&& responsive.ContextualPane == ContextualPaneState.Persistent;
		bool showFlightOverlay = !hostFlightInOverview
			&& !showPersistentFlight
			&& ActiveTransientSurface == TransientSurface.Flight;
		bool showQueueDock = !hostDecanterInOverview
			&& configuration.ShowDecanterDock
			&& responsive.QueueSurface == QueueSurfaceState.Dock
			&& !IsProcessingRoute;
		return new(
			responsive.LayoutClass,
			responsive.NavigationRail switch
			{
				NavigationRailState.Expanded => SplitViewDisplayMode.Inline,
				NavigationRailState.Compact => SplitViewDisplayMode.CompactInline,
				_ => SplitViewDisplayMode.Overlay,
			},
			responsive.NavigationRail == NavigationRailState.Expanded
				|| navigationOverlay && ActiveTransientSurface == TransientSurface.Navigation,
			responsive.NavigationRail != NavigationRailState.Compact,
			navigationOverlay,
			showPersistentFlight,
			showFlightOverlay,
			!hostFlightInOverview && !showPersistentFlight,
			showQueueDock,
			!showQueueDock && !hostDecanterInOverview && ActiveTransientSurface == TransientSurface.Decanter,
			responsive.QueueSurface is QueueSurfaceState.CompactBar or QueueSurfaceState.Drawer,
			hostFlightInOverview,
			hostDecanterInOverview,
			responsive.IsBelowSupportedMinimum);
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
		this.RaisePropertyChanged(nameof(CurrentRoutePresentation));
		this.RaisePropertyChanged(nameof(HasRouteStatusBadge));
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
		this.RaisePropertyChanged(nameof(ShowRouteHeader));
		this.RaisePropertyChanged(nameof(SelectedNavigationItem));
		ActiveTransientSurface = TransientSurface.None;
		this.RaisePropertyChanged(nameof(Layout));
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
		if (Responsive.Current.NavigationRail != NavigationRailState.Overlay
			&& ActiveTransientSurface == TransientSurface.Navigation)
			ActiveTransientSurface = TransientSurface.None;
		this.RaisePropertyChanged(nameof(Layout));
	}

	private void ExperienceManager_ProfileChanged(object? sender, ExperienceProfileChangedEventArgs e)
	{
		if (!Dispatcher.UIThread.CheckAccess())
		{
			Dispatcher.UIThread.Post(() => ExperienceManager_ProfileChanged(sender, e));
			return;
		}

		Profile = e.Current;
		Dashboard.SetProfile(Profile);
		this.RaisePropertyChanged(nameof(IsCellarComposition));
		this.RaisePropertyChanged(nameof(IsTastingRoomComposition));
		this.RaisePropertyChanged(nameof(IsAccessibleComposition));
		this.RaisePropertyChanged(nameof(CurrentRoutePresentation));
		this.RaisePropertyChanged(nameof(Layout));
		UpdateLayout(lastEffectiveSize);
	}

	private void ToggleFlight()
	{
		ActiveTransientSurface = ActiveTransientSurface == TransientSurface.Flight
			? TransientSurface.None
			: TransientSurface.Flight;
	}
	private void ToggleNavigationOverlay()
	{
		if (Responsive.Current.NavigationRail == NavigationRailState.Overlay)
			ActiveTransientSurface = ActiveTransientSurface == TransientSurface.Navigation
				? TransientSurface.None
				: TransientSurface.Navigation;
	}
	private void CloseNavigationOverlay()
	{
		if (ActiveTransientSurface == TransientSurface.Navigation)
			ActiveTransientSurface = TransientSurface.None;
	}
	private void ToggleDecanterDrawer()
	{
		if (Layout.ShowQueueDock || Layout.HostDecanterInOverview)
			Navigation.Navigate(AppRouteId.Processing);
		else
			ActiveTransientSurface = ActiveTransientSurface == TransientSurface.Decanter
				? TransientSurface.None
				: TransientSurface.Decanter;
	}
	private void CloseDecanterDrawer()
	{
		if (ActiveTransientSurface == TransientSurface.Decanter)
			ActiveTransientSurface = TransientSurface.None;
	}

	public bool CloseTransientSurface()
	{
		if (ActiveTransientSurface == TransientSurface.None)
			return false;
		ActiveTransientSurface = TransientSurface.None;
		return true;
	}

	private void Flight_SelectionChanged(object? sender, FlightChangedEventArgs e)
	{
		this.RaisePropertyChanged(nameof(Layout));
		if (Flight.Count == 0
			&& e.Kind is FlightChangeKind.Clear or FlightChangeKind.Remove
			&& Responsive.Current.ContextualPane != ContextualPaneState.Persistent
			&& ActiveTransientSurface == TransientSurface.Flight)
			ActiveTransientSurface = TransientSurface.None;
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

	private void Main_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		if (e.PropertyName is nameof(MainVM.ApplicationUpdateState) or nameof(MainVM.DownloadProgress))
			this.RaisePropertyChanged(nameof(HasUpdateAvailable));
	}

	private void RefreshConfigurationProjection()
	{
		SetActiveDestinations(Navigation.CurrentRoute.Id);
		UpdateLayout(lastEffectiveSize);
		this.RaisePropertyChanged(nameof(Layout));
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
		Main.PropertyChanged -= Main_PropertyChanged;
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
