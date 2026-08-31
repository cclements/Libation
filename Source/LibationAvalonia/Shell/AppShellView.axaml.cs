using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DataLayer;
using LibationAvalonia.ViewModels;
using LibationFileManager;
using LibationUiBase.GridView;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace LibationAvalonia.Shell;

public partial class AppShellView : UserControl
{
	private AppShellViewModel? ViewModel => DataContext as AppShellViewModel;
	private AppShellViewModel? subscribedViewModel;
	private Control? navigationReturnFocus;
	private Control? flightReturnFocus;
	private Control? decanterReturnFocus;

	public AppShellView()
	{
		InitializeComponent();
		SizeChanged += (_, e) => ViewModel?.UpdateLayout(e.NewSize);
		DataContextChanged += (_, _) => OnShellDataContextChanged();
	}

	private void OnShellDataContextChanged()
	{
		if (subscribedViewModel is not null)
			subscribedViewModel.PropertyChanged -= ViewModel_PropertyChanged;
		subscribedViewModel = ViewModel;
		if (subscribedViewModel is not null)
			subscribedViewModel.PropertyChanged += ViewModel_PropertyChanged;
		ViewModel?.UpdateLayout(Bounds.Size);
	}

	private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (ViewModel is not { } viewModel)
			return;

		switch (e.PropertyName)
		{
			case nameof(AppShellViewModel.IsNavigationOverlayOpen):
				if (viewModel.IsNavigationOverlayOpen)
					EnterTransientSurface(NavigationRail, ref navigationReturnFocus);
				else
					RestoreTransientFocus(ref navigationReturnFocus);
				break;
			case nameof(AppShellViewModel.ShowFlightOverlay):
				if (viewModel.ShowFlightOverlay)
					EnterTransientSurface(FlightOverlay, ref flightReturnFocus);
				else
					RestoreTransientFocus(ref flightReturnFocus);
				break;
			case nameof(AppShellViewModel.ShowDecanterDrawer):
				if (viewModel.ShowDecanterDrawer)
					EnterTransientSurface(DecanterDrawer, ref decanterReturnFocus);
				else
					RestoreTransientFocus(ref decanterReturnFocus);
				break;
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
		Control[] candidates =
		[
			NavigationRail,
			HeaderRegion,
			ContentRegion,
			PersistentFlightPane,
			FlightOverlay,
			QueueDock,
			ShellStatusBar,
			DecanterDrawer,
		];
		var regions = candidates.Where(region => region.IsEffectivelyVisible).ToList();
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
