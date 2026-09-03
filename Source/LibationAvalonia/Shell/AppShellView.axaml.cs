using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DataLayer;
using LibationAvalonia.DesignSystem.Components;
using LibationAvalonia.Features.Overview;
using LibationAvalonia.Properties;
using LibationAvalonia.ViewModels;
using LibationFileManager;
using LibationUiBase.GridView;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;

namespace LibationAvalonia.Shell;

public partial class AppShellView : UserControl
{
	private AppShellViewModel? ViewModel => DataContext as AppShellViewModel;
	private AppShellViewModel? subscribedViewModel;
	private Control? navigationReturnFocus;
	private Control? flightReturnFocus;
	private Control? decanterReturnFocus;
	private CellarOverviewView? cellarOverview;
	private TastingRoomOverviewView? tastingRoomOverview;
	private readonly FlightSurfaceHost flightSurfaceHost;
	private readonly DecanterSurfaceHost decanterSurfaceHost;

	public AppShellView()
	{
		InitializeComponent();
		flightSurfaceHost = new(SharedFlightSurface);
		decanterSurfaceHost = new(SharedDecanterSurface);
#if DEBUG
		ConfigureDebugMenu();
#endif
		SizeChanged += (_, e) => ViewModel?.UpdateLayout(e.NewSize);
		DataContextChanged += (_, _) => OnShellDataContextChanged();
	}

#if DEBUG
	private void ConfigureDebugMenu()
	{
		var galleryItem = new MenuItem { Header = LibationAvalonia.Properties.Resources.MenuComponentGalleryHeader };
		galleryItem.Click += (_, _) => ComponentGallery.ShowWindow(TopLevel.GetTopLevel(this) as Window);

		var insertIndex = ShellSettingsMenu.Items.Count;
		for (var index = 0; index < ShellSettingsMenu.Items.Count; index++)
		{
			if (ShellSettingsMenu.Items[index] is MenuItem menuItem
				&& menuItem.Header?.ToString()?.Contains("Setup", StringComparison.OrdinalIgnoreCase) == true)
			{
				insertIndex = index;
				break;
			}
		}
		ShellSettingsMenu.Items.Insert(insertIndex, galleryItem);
	}
#endif

	private void OnShellDataContextChanged()
	{
		if (subscribedViewModel is not null)
		{
			subscribedViewModel.PropertyChanged -= ViewModel_PropertyChanged;
			subscribedViewModel.Processing.PropertyChanged -= Processing_PropertyChanged;
		}
		if (!ReferenceEquals(subscribedViewModel, ViewModel))
		{
			cellarOverview = null;
			tastingRoomOverview = null;
			OverviewHost.Content = null;
		}
		subscribedViewModel = ViewModel;
		if (subscribedViewModel is not null)
		{
			subscribedViewModel.PropertyChanged += ViewModel_PropertyChanged;
			subscribedViewModel.Processing.PropertyChanged += Processing_PropertyChanged;
			SharedFlightSurface.DataContext = subscribedViewModel.CurrentFlight;
			UpdateDecanterSurface(subscribedViewModel);
		}
		ViewModel?.UpdateLayout(Bounds.Size);
		UpdateOverviewHost();
		MoveContextualSurfaces();
	}

	private void Processing_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (subscribedViewModel is { } viewModel)
			UpdateDecanterSurface(viewModel);
	}

	private void UpdateDecanterSurface(AppShellViewModel viewModel)
	{
		var processing = viewModel.Processing;
		SharedDecanterSurface.SummaryText = processing.SummaryText;
		SharedDecanterSurface.ActiveText = processing.ActiveText;
		SharedDecanterSurface.Progress = processing.Progress;
		SharedDecanterSurface.ShowProgress = processing.ShowProgress;
		SharedDecanterSurface.CancelCommand = processing.CancelAllCommand;
		SharedDecanterSurface.CanCancel = processing.CanCancel;
	}

	private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (ViewModel is not { } viewModel)
			return;

		switch (e.PropertyName)
		{
			case nameof(AppShellViewModel.ActiveTransientSurface):
				MoveContextualSurfaces();
				UpdateTransientFocus(viewModel);
				break;
			case nameof(AppShellViewModel.Layout):
				ApplyOverviewLayout(viewModel.Layout.LayoutClass);
				MoveContextualSurfaces();
				if (viewModel.Layout.ShowPersistentFlight)
					PlaySurfaceEntrance(PersistentFlightPane, "translateX(20px)");
				break;
			case nameof(AppShellViewModel.CurrentRoute):
				PlayRouteEntrance();
				UpdateOverviewHost();
				MoveContextualSurfaces();
				break;
			case nameof(AppShellViewModel.IsCellarComposition):
			case nameof(AppShellViewModel.IsTastingRoomComposition):
				UpdateOverviewHost();
				MoveContextualSurfaces();
				break;
		}
	}

	private void UpdateOverviewHost()
	{
		if (ViewModel is not { } viewModel)
		{
			OverviewHost.Content = null;
			return;
		}

		if (viewModel.IsCellarComposition)
		{
			cellarOverview ??= new CellarOverviewView
			{
				Library = viewModel.Library,
				DataContext = viewModel.Dashboard,
			};
			OverviewHost.Content = cellarOverview;
		}
		else
		{
			tastingRoomOverview ??= new TastingRoomOverviewView
			{
				Library = viewModel.Library,
				DataContext = viewModel.Dashboard,
			};
			OverviewHost.Content = tastingRoomOverview;
		}
		ApplyOverviewLayout(viewModel.Layout.LayoutClass);
	}

	private void ApplyOverviewLayout(DesktopLayoutClass layoutClass)
	{
		cellarOverview?.ApplyLayout(layoutClass);
		tastingRoomOverview?.ApplyLayout(layoutClass);
	}

	private void MoveContextualSurfaces()
	{
		if (ViewModel is not { } viewModel)
			return;

		ContentControl flightTarget = viewModel.Layout.HostFlightInOverview && cellarOverview is not null
			? cellarOverview.FlightHost
			: viewModel.Layout.ShowPersistentFlight
				? PersistentFlightHost
				: viewModel.Layout.ShowFlightOverlay
					? FlightOverlayHost
					: FlightParkingHost;
		flightSurfaceHost.AttachTo(flightTarget);
		if (cellarOverview is not null)
			cellarOverview.FlightHost.IsVisible = ReferenceEquals(flightTarget, cellarOverview.FlightHost);

		ContentControl decanterTarget = viewModel.Layout.HostDecanterInOverview && tastingRoomOverview is not null
			? tastingRoomOverview.DecanterHost
			: viewModel.Layout.ShowQueueDock
				? QueueDock
				: viewModel.Layout.ShowDecanterDrawer
					? DecanterDrawerHost
					: DecanterParkingHost;
		decanterSurfaceHost.AttachTo(decanterTarget);
		if (tastingRoomOverview is not null)
			tastingRoomOverview.DecanterHost.IsVisible = ReferenceEquals(decanterTarget, tastingRoomOverview.DecanterHost);
	}

	private void UpdateTransientFocus(AppShellViewModel viewModel)
	{
		if (viewModel.ActiveTransientSurface == TransientSurface.Navigation)
		{
			PlaySurfaceEntrance(NavigationRail, "translateX(-20px)");
			EnterTransientSurface(NavigationRail, ref navigationReturnFocus);
		}
		else
			RestoreTransientFocus(ref navigationReturnFocus);

		if (viewModel.ActiveTransientSurface == TransientSurface.Flight && viewModel.Layout.ShowFlightOverlay)
		{
			PlaySurfaceEntrance(FlightOverlay, "translateX(20px)");
			EnterTransientSurface(FlightOverlay, ref flightReturnFocus);
		}
		else
			RestoreTransientFocus(ref flightReturnFocus);

		if (viewModel.ActiveTransientSurface == TransientSurface.Decanter && viewModel.Layout.ShowDecanterDrawer)
		{
			PlaySurfaceEntrance(DecanterDrawer, "translateX(20px)");
			EnterTransientSurface(DecanterDrawer, ref decanterReturnFocus);
		}
		else
			RestoreTransientFocus(ref decanterReturnFocus);
	}

	private void PlayRouteEntrance()
	{
		ContentRegion.Opacity = 0;
		ContentRegion.RenderTransform = TransformOperations.Parse("translateY(10px)");
		Dispatcher.UIThread.Post(() =>
		{
			ContentRegion.Opacity = 1;
			ContentRegion.RenderTransform = TransformOperations.Parse("translateY(0px)");
		}, DispatcherPriority.Render);
	}

	private static void PlaySurfaceEntrance(Control surface, string initialTransform)
	{
		surface.RenderTransform = TransformOperations.Parse(initialTransform);
		Dispatcher.UIThread.Post(
			() => surface.RenderTransform = TransformOperations.Parse("translateX(0px)"),
			DispatcherPriority.Render);
	}

	private void EnterTransientSurface(Control surface, ref Control? returnFocus)
	{
		var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
		if (focused is not null && !surface.IsVisualAncestorOf(focused))
			returnFocus = focused;
		Dispatcher.UIThread.Post(() => FocusFirst(surface), DispatcherPriority.Input);
	}

	private void RestoreTransientFocus(ref Control? returnFocus)
	{
		var target = returnFocus;
		returnFocus = null;
		if (target is null)
			return;
		Dispatcher.UIThread.Post(() =>
		{
			if (!target.IsEffectivelyVisible || !target.IsEffectivelyEnabled || !target.Focus())
				FocusFirst(HeaderRegion);
		}, DispatcherPriority.Input);
	}

	public bool CycleFocusRegion()
	{
		if (ViewModel is not { } viewModel)
			return false;

		var layout = viewModel.Layout;
		(Control Region, bool IsPresented)[] candidates =
		[
			(NavigationRail, !layout.ShowNavigationCommand || layout.IsNavigationPaneOpen),
			(HeaderRegion, true),
			(ContentRegion, true),
			(PersistentFlightPane, layout.ShowPersistentFlight),
			(FlightOverlay, layout.ShowFlightOverlay),
			(QueueDock, layout.ShowQueueDock),
			(ShellStatusBar, true),
			(DecanterDrawer, layout.ShowDecanterDrawer),
		];
		var regions = candidates
			.Where(candidate => candidate.IsPresented && candidate.Region.IsEffectivelyVisible)
			.Select(candidate => candidate.Region)
			.ToList();
		if (regions.Count == 0)
			return false;

		var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
		int currentIndex = focused is null
			? -1
			: regions.FindLastIndex(region => ReferenceEquals(region, focused) || region.IsVisualAncestorOf(focused));
		for (int offset = 1; offset <= regions.Count; offset++)
		{
			var candidate = regions[(currentIndex + offset) % regions.Count];
			if (FocusFirst(candidate))
				return true;
		}
		return false;
	}

	private static bool FocusFirst(Control region)
	{
		if (region.Focusable && region.IsEffectivelyEnabled && region.Focus())
			return true;
		var target = region.GetVisualDescendants()
			.OfType<Control>()
			.FirstOrDefault(control => control.Focusable && control.IsEffectivelyVisible && control.IsEffectivelyEnabled);
		return target?.Focus() == true;
	}

	public void SelectAndFocusSearch()
	{
		ViewModel?.Navigation.Navigate(AppRouteId.Library);
		Dispatcher.UIThread.Post(LibraryDisplay.SelectAndFocusSearch, DispatcherPriority.Input);
	}

	private void SearchShortcut_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
		=> SelectAndFocusSearch();

	private void ShellSplitView_PaneClosing(object? sender, Avalonia.Interactivity.CancelRoutedEventArgs e)
	{
		if (ViewModel?.ActiveTransientSurface != TransientSurface.Navigation)
			return;
		var command = (ICommand)ViewModel.CloseNavigationOverlayCommand;
		if (command.CanExecute(null))
			command.Execute(null);
	}

	public string SearchText
	{
		get => LibraryDisplay.SearchText;
		set => LibraryDisplay.SearchText = value;
	}

	public void SetFilterHelpEnabled(bool enabled) => LibraryDisplay.SetFilterHelpEnabled(enabled);
	public void InsertSearchTag(string tag)
	{
		ViewModel?.Navigation.Navigate(AppRouteId.Library);
		Dispatcher.UIThread.Post(() => LibraryDisplay.InsertSearchTag(tag), DispatcherPriority.Input);
	}
	public void CloseImageDisplay() => LibraryDisplay.CloseImageDisplay();

	private void ProductsDisplay_LiberateClicked(object? sender, IList<LibraryBook> books, Configuration config)
		=> App.MainWindow?.ProductsDisplay_LiberateClicked(sender!, books, config);
	private void ProductsDisplay_LiberateSeriesClicked(object? sender, SeriesEntry series)
		=> App.MainWindow?.ProductsDisplay_LiberateSeriesClicked(sender!, series);
	private void ProductsDisplay_ConvertToMp3Clicked(object? sender, LibraryBook[] books)
		=> App.MainWindow?.ProductsDisplay_ConvertToMp3Clicked(sender!, books);
	private void ProductsDisplay_TagsButtonClicked(object? sender, LibraryBook book)
		=> App.MainWindow?.ProductsDisplay_TagsButtonClicked(sender!, book);
}
