using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LibationUiBase.GridView;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LibationAvalonia.Features.Library;

public sealed class LibraryGalleryContextMenuRequestedEventArgs(
	Control placementTarget,
	IReadOnlyList<LibraryBookEntry> entries) : EventArgs
{
	public Control PlacementTarget { get; } = placementTarget;
	public IReadOnlyList<LibraryBookEntry> Entries { get; } = entries;
}

public partial class LibraryGalleryView : UserControl
{
	private bool clearingHostSelection;

	public LibraryGalleryView()
	{
		InitializeComponent();
		GalleryRowsControl.SelectionChanged += (_, _) =>
		{
			if (clearingHostSelection || GalleryRowsControl.SelectedItems is not { Count: > 0 } selectedItems)
				return;
			clearingHostSelection = true;
			selectedItems.Clear();
			clearingHostSelection = false;
		};
		SizeChanged += (_, _) => UpdateViewport();
		DataContextChanged += (_, _) => UpdateViewport();
	}

	public event EventHandler<LibraryGalleryContextMenuRequestedEventArgs>? ContextMenuRequested;
	private LibraryViewModel? ViewModel => DataContext as LibraryViewModel;

	private void UpdateViewport()
	{
		if (ViewModel is not { } viewModel)
			return;
		double renderScaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
		viewModel.UpdateGalleryViewport(Bounds.Size, renderScaling);
	}

	private void GalleryCard_SelectionRequested(object? sender, GalleryCardInteractionEventArgs e)
		=> ViewModel?.SelectGalleryItem(e.Item, e.Modifiers);

	private void GalleryCard_ToggleRequested(object? sender, GalleryCardInteractionEventArgs e)
		=> ViewModel?.ToggleGalleryItem(e.Item);

	private void GalleryCard_SelectAllRequested(object? sender, EventArgs e)
		=> ViewModel?.SelectAllVisible();

	private void GalleryCard_FocusRequested(object? sender, GalleryCardInteractionEventArgs e)
		=> ViewModel?.FocusGalleryItem(e.Item);

	private void GalleryCard_OpenRequested(object? sender, GalleryCardInteractionEventArgs e)
		=> ViewModel?.OpenItem(e.Item);

	private void GalleryCard_ContextMenuRequested(object? sender, GalleryCardInteractionEventArgs e)
	{
		if (ViewModel is not { } viewModel)
			return;
		var entries = viewModel.GetContextSelection(e.Item);
		if (entries.Count > 0)
			ContextMenuRequested?.Invoke(this, new(e.PlacementTarget, entries));
	}

	private void GalleryCard_NavigationRequested(object? sender, GalleryNavigationRequestedEventArgs e)
	{
		if (ViewModel is not { } viewModel)
			return;
		int current = IndexOf(viewModel.VisibleItems, e.Item);
		if (current < 0 || viewModel.VisibleItems.Count == 0)
			return;
		int target = Math.Clamp(current + e.ItemOffset, 0, viewModel.VisibleItems.Count - 1);
		if (target == current)
			return;

		int rowIndex = target / Math.Max(1, viewModel.GalleryColumnCount);
		if (rowIndex >= 0 && rowIndex < viewModel.GalleryRows.Count)
			GalleryRowsControl.ScrollIntoView(viewModel.GalleryRows[rowIndex]);
		var targetItem = viewModel.VisibleItems[target];
		Dispatcher.UIThread.Post(() =>
		{
			var card = GalleryRowsControl
				.GetVisualDescendants()
				.OfType<GalleryBookCard>()
				.FirstOrDefault(candidate => ReferenceEquals(candidate.DataContext, targetItem));
			card?.Focus();
		}, DispatcherPriority.Input);
	}

	private static int IndexOf(IReadOnlyList<LibraryBookItemViewModel> items, LibraryBookItemViewModel item)
	{
		for (int index = 0; index < items.Count; index++)
			if (ReferenceEquals(items[index], item))
				return index;
		return -1;
	}
}
