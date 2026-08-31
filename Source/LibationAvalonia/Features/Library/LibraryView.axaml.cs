using Avalonia.Controls;
using Avalonia.Input;
using DataLayer;
using LibationAvalonia.ViewModels;
using LibationAvalonia.Views;
using LibationFileManager;
using LibationUiBase.Forms;
using LibationUiBase.GridView;
using System;
using System.Linq;

namespace LibationAvalonia.Features.Library;

public partial class LibraryView : UserControl
{
	private LibraryViewModel? subscribedViewModel;

	public LibraryView()
	{
		InitializeComponent();
		DetailsDisplay.LibrarySelectionChanged += DetailsDisplay_LibrarySelectionChanged;
		GalleryDisplay.ContextMenuRequested += GalleryDisplay_ContextMenuRequested;
		DataContextChanged += (_, _) => SubscribeViewModel(DataContext as LibraryViewModel);
	}

	public ProductsDisplay DetailsProductsDisplay => DetailsDisplay;
	public string SearchText
	{
		get => SearchBox.Text ?? string.Empty;
		set => SearchBox.Text = value ?? string.Empty;
	}

	public event LiberateClickedHandler? LiberateClicked
	{
		add => DetailsDisplay.LiberateClicked += value;
		remove => DetailsDisplay.LiberateClicked -= value;
	}
	public event EventHandler<SeriesEntry>? LiberateSeriesClicked
	{
		add => DetailsDisplay.LiberateSeriesClicked += value;
		remove => DetailsDisplay.LiberateSeriesClicked -= value;
	}
	public event EventHandler<LibraryBook[]>? ConvertToMp3Clicked
	{
		add => DetailsDisplay.ConvertToMp3Clicked += value;
		remove => DetailsDisplay.ConvertToMp3Clicked -= value;
	}
	public event EventHandler<LibraryBook>? TagsButtonClicked
	{
		add => DetailsDisplay.TagsButtonClicked += value;
		remove => DetailsDisplay.TagsButtonClicked -= value;
	}

	public void SelectAndFocusSearch()
	{
		SearchBox.Focus();
		SearchBox.SelectAll();
	}

	public void InsertSearchTag(string tag)
	{
		if (string.IsNullOrEmpty(tag))
			return;
		int caret = Math.Clamp(SearchBox.CaretIndex, 0, SearchText.Length);
		SearchText = SearchText.Insert(caret, tag);
		SearchBox.CaretIndex = caret + tag.Length;
		SearchBox.Focus();
	}

	public void SetFilterHelpEnabled(bool enabled) => FilterHelpButton.IsEnabled = enabled;
	public void CloseImageDisplay() => DetailsDisplay.CloseImageDisplay();

	private void SubscribeViewModel(LibraryViewModel? viewModel)
	{
		if (ReferenceEquals(subscribedViewModel, viewModel))
			return;
		if (subscribedViewModel is not null)
			subscribedViewModel.SelectionProjectionChanged -= ViewModel_SelectionProjectionChanged;
		subscribedViewModel = viewModel;
		if (subscribedViewModel is not null)
		{
			subscribedViewModel.SelectionProjectionChanged += ViewModel_SelectionProjectionChanged;
			ViewModel_SelectionProjectionChanged(
				subscribedViewModel,
				new(
					subscribedViewModel.VisibleItems.Where(item => item.IsSelected).Select(item => item.ProductId).ToHashSet(StringComparer.Ordinal),
					subscribedViewModel.FocusedItem?.ProductId));
		}
	}

	private void ViewModel_SelectionProjectionChanged(object? sender, LibrarySelectionProjectionChangedEventArgs e)
		=> DetailsDisplay.ApplySharedSelection(e.SelectedProductIds, e.FocusedProductId);

	private void DetailsDisplay_LibrarySelectionChanged(object? sender, ProductsDisplaySelectionChangedEventArgs e)
		=> subscribedViewModel?.SynchronizeDetailsSelection(e);

	private void GalleryDisplay_ContextMenuRequested(object? sender, LibraryGalleryContextMenuRequestedEventArgs e)
	{
		var menu = DetailsDisplay.CreateLibraryContextMenu(e.Entries);
		menu.Open(e.PlacementTarget);
	}

	private void ColumnChooser_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
	{
		if (sender is Control control)
			DetailsDisplay.OpenColumnChooser(control);
	}
}
