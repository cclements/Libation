using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DataLayer;
using LibationAvalonia.DesignSystem.Components;
using LibationAvalonia.Features.Overview;
using LibationAvalonia.Features.Processing;
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
		if (!Dispatcher.UIThread.CheckAccess())
		{
			Dispatcher.UIThread.Post(
				() => Processing_PropertyChanged(sender, e),
				DispatcherPriority.Background);
			return;
		}

		if (subscribedViewModel is not { } viewModel)
			return;

		var processing = viewModel.Processing;
		var current = processing.CurrentItem;
		switch (e.PropertyName)
		{
			case nameof(ProcessingViewModel.SummaryText):
				SharedDecanterSurface.SummaryText = processing.SummaryText;
				break;
			case nameof(ProcessingViewModel.CanCancel):
				SharedDecanterSurface.HasWork = processing.CanCancel;
				SharedDecanterSurface.IsIdle = !processing.CanCancel;
				break;
			case nameof(ProcessingViewModel.InQueueText):
				SharedDecanterSurface.InQueueText = processing.InQueueText;
				break;
			case nameof(ProcessingViewModel.ConvertingText):
				SharedDecanterSurface.ConvertingText = processing.ConvertingText;
				break;
			case nameof(ProcessingViewModel.RunningTimeText):
				SharedDecanterSurface.RunningTimeText = processing.RunningTimeText;
				break;
			case nameof(ProcessingViewModel.CurrentItem):
				UpdateDecanterCurrentItem(processing);
				break;
			case nameof(ProcessingViewModel.CurrentTitle):
				UpdateDecanterCurrentItem(processing);
				break;
			case nameof(ProcessingViewModel.CurrentStage):
			case nameof(ProcessingViewModel.CurrentStageAnnouncement):
				SharedDecanterSurface.CurrentStageText = processing.CurrentStage;
				SharedDecanterSurface.CurrentStageAccessibleName = processing.CurrentStageAnnouncement;
				break;
			case nameof(ProcessingViewModel.CurrentProgress):
				SharedDecanterSurface.Progress = processing.CurrentProgress;
				SharedDecanterSurface.ProgressText = current?.ProgressText;
				SharedDecanterSurface.ProgressAccessibleName = current?.ProgressAccessibleName;
				break;
			case nameof(ProcessingViewModel.ShowCurrentProgress):
				SharedDecanterSurface.ShowProgress = processing.ShowCurrentProgress;
				break;
			case nameof(ProcessingViewModel.CurrentCancellable):
			case nameof(ProcessingViewModel.CurrentCancelCommand):
				SharedDecanterSurface.CancelCommand = processing.CurrentCancelCommand;
				SharedDecanterSurface.CanCancel = processing.CurrentCancellable;
				SharedDecanterSurface.CancelAccessibleName = current?.CancelAccessibleName;
				break;
		}
	}

	private void UpdateDecanterSurface(AppShellViewModel viewModel)
	{
		var processing = viewModel.Processing;
		SharedDecanterSurface.IsCellar = viewModel.IsCellarComposition || viewModel.IsAccessibleComposition;
		SharedDecanterSurface.IsTastingRoom = viewModel.IsTastingRoomComposition;
		SharedDecanterSurface.SummaryText = processing.SummaryText;
		SharedDecanterSurface.HasWork = processing.CanCancel;
		SharedDecanterSurface.IsIdle = !processing.CanCancel;
		SharedDecanterSurface.ActiveItems = processing.DecanterActiveItems;
		SharedDecanterSurface.InQueueText = processing.InQueueText;
		SharedDecanterSurface.ConvertingText = processing.ConvertingText;
		SharedDecanterSurface.RunningTimeText = processing.RunningTimeText;
		UpdateDecanterCurrentItem(processing);
		SharedDecanterSurface.OpenProcessingCommand = viewModel.NavigateCommand;
		SharedDecanterSurface.OpenProcessingCommandParameter = AppRouteId.Processing;
	}

	private void UpdateDecanterCurrentItem(ProcessingViewModel processing)
	{
		var current = processing.CurrentItem;
		SharedDecanterSurface.CurrentTitle = processing.CurrentTitle;
		SharedDecanterSurface.CurrentStageText = processing.CurrentStage;
		SharedDecanterSurface.CurrentStageAccessibleName = processing.CurrentStageAnnouncement;
		SharedDecanterSurface.CurrentOutputText = current?.OutputProfileText;
		SharedDecanterSurface.Progress = processing.CurrentProgress;
		SharedDecanterSurface.ProgressText = current?.ProgressText;
		SharedDecanterSurface.ProgressAccessibleName = current?.ProgressAccessibleName;
		SharedDecanterSurface.ShowProgress = processing.ShowCurrentProgress;
		SharedDecanterSurface.CancelCommand = processing.CurrentCancelCommand;
		SharedDecanterSurface.CanCancel = processing.CurrentCancellable;
		SharedDecanterSurface.CancelAccessibleName = current?.CancelAccessibleName;
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
			case nameof(AppShellViewModel.IsAccessibleComposition):
				UpdateDecanterSurface(viewModel);
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
		// The shared Decanter is physically re-parented. Refresh every local input before
		// attachment so its presentation never depends on an inherited data context.
		UpdateDecanterSurface(viewModel);

		ContentControl flightTarget = viewModel.Layout.HostFlightInOverview && cellarOverview is not null
			? cellarOverview.FlightHost
			: viewModel.Layout.ShowPersistentFlight
				? PersistentFlightHost
				: viewModel.Layout.ShowFlightOverlay
					? FlightOverlayHost
					: FlightParkingHost;
		// Keep the shell-owned Flight bound to its owner when it moves beneath an
		// Overview host whose inherited data context is the dashboard projection.
		SharedFlightSurface.DataContext = viewModel.CurrentFlight;
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
		UpdateDecanterSurface(viewModel);
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
		ContentRegion.Opacity = 1;
		if (ViewModel?.IsReducedMotionEnabled == true)
		{
			SetWithoutTransitions(
				ContentRegion,
				() => ContentRegion.RenderTransform = TransformOperations.Parse("translateY(0px)"));
			return;
		}

		SetWithoutTransitions(
			ContentRegion,
			() => ContentRegion.RenderTransform = TransformOperations.Parse("translateY(10px)"));
		Dispatcher.UIThread.Post(() =>
			ContentRegion.RenderTransform = TransformOperations.Parse("translateY(0px)"),
			DispatcherPriority.Render);
	}

	internal bool IsRoutePresented(AppRouteId route)
		=> route switch
		{
			AppRouteId.Overview => OverviewHost.IsEffectivelyVisible && OverviewHost.Content is not null,
			AppRouteId.Library => LibraryRouteHost.IsEffectivelyVisible,
			AppRouteId.Downloads => DownloadsRouteHost.IsEffectivelyVisible,
			AppRouteId.Processing => ProcessingRouteHost.IsEffectivelyVisible,
			AppRouteId.History => HistoryRouteHost.IsEffectivelyVisible,
			AppRouteId.Accounts => AccountsRouteHost.IsEffectivelyVisible,
			AppRouteId.Settings => SettingsRouteHost.IsEffectivelyVisible,
			AppRouteId.Tools => ToolsRouteHost.IsEffectivelyVisible,
			AppRouteId.Trash => TrashRouteHost.IsEffectivelyVisible,
			_ => false,
		};

	private void PlaySurfaceEntrance(Control surface, string initialTransform)
	{
		if (ViewModel?.IsReducedMotionEnabled == true)
		{
			SetWithoutTransitions(
				surface,
				() => surface.RenderTransform = TransformOperations.Parse("translateX(0px)"));
			return;
		}

		SetWithoutTransitions(
			surface,
			() => surface.RenderTransform = TransformOperations.Parse(initialTransform));
		Dispatcher.UIThread.Post(
			() => surface.RenderTransform = TransformOperations.Parse("translateX(0px)"),
			DispatcherPriority.Render);
	}

	private static void SetWithoutTransitions(Control control, Action update)
	{
		var transitions = control.Transitions;
		control.Transitions = null;
		try
		{
			update();
		}
		finally
		{
			control.Transitions = transitions;
		}
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
