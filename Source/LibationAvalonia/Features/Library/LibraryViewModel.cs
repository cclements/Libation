using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using LibationAvalonia.Features.Flight;
using LibationAvalonia.Shell;
using LibationAvalonia.ViewModels;
using LibationAvalonia.Views;
using LibationFileManager;
using LibationUiBase.Forms;
using LibationUiBase.GridView;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace LibationAvalonia.Features.Library;

/// <summary>
/// One presentation model over the established ProductsDisplay/filter state and the
/// shell-scoped Flight selection. It never owns a second library or domain command path.
/// </summary>
public sealed class LibraryViewModel : ViewModelBase, IDisposable
{
	private static readonly TimeSpan SearchDebounce = TimeSpan.FromMilliseconds(200);
	private const double GalleryCardWidth = 220;
	private const double GalleryCardGap = 12;
	private const double SmallCoverLogicalWidth = 180;
	private const double MediumCoverLogicalWidth = 300;

	private readonly ProductsDisplayViewModel products;
	private readonly IFlightService flight;
	private readonly Configuration configuration;
	private readonly ILibationCommandAdapter commands;
	private readonly ResponsiveLayoutService responsive;
	private readonly Dictionary<FlightItemId, LibraryBookItemViewModel> itemMap = new();
	private CancellationTokenSource? searchCancellation;
	private IReadOnlyList<LibraryBookItemViewModel> visibleItems = Array.Empty<LibraryBookItemViewModel>();
	private IReadOnlyList<GalleryRowViewModel> galleryRows = Array.Empty<GalleryRowViewModel>();
	private LibraryBookItemViewModel? focusedItem;
	private LibraryBookItemViewModel? transientFocusedItem;
	private FlightItemId? selectionAnchor;
	private LibraryViewMode viewMode;
	private LibrarySortOption selectedSort;
	private string searchText;
	private bool isDetailsPaneOpen;
	private int galleryColumnCount = 1;
	private int smallCoverDecodePixelWidth = (int)SmallCoverLogicalWidth;
	private int mediumCoverDecodePixelWidth = (int)MediumCoverLogicalWidth;
	private int realizedSmallCoverCount;
	private int realizedMediumCoverCount;
	private bool disposed;

	public LibraryViewModel(
		ProductsDisplayViewModel products,
		IFlightService flight,
		Configuration configuration,
		ILibationCommandAdapter commands,
		ResponsiveLayoutService responsive,
		ICommand? processSelectionCommand = null)
	{
		this.products = products ?? throw new ArgumentNullException(nameof(products));
		this.flight = flight ?? throw new ArgumentNullException(nameof(flight));
		this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
		this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
		this.responsive = responsive ?? throw new ArgumentNullException(nameof(responsive));
		ProcessSelectionCommand = processSelectionCommand;
		CoverCache = new CoverImageCache();
		SortOptions =
		[
			new("Current Details order", null, ListSortDirection.Ascending),
			new("Recently added", nameof(GridEntry.DateAdded), ListSortDirection.Descending),
			new("Title A–Z", nameof(GridEntry.Title), ListSortDirection.Ascending),
			new("Author A–Z", nameof(GridEntry.Authors), ListSortDirection.Ascending),
		];
		selectedSort = SortOptions[0];
		viewMode = configuration.LibraryViewMode;
		searchText = products.FilterString ?? commands.Main.SelectedNamedFilter?.Filter ?? string.Empty;

		ShowDetailsCommand = ReactiveCommand.Create(() => { ViewMode = LibraryViewMode.Details; });
		ShowGalleryCommand = ReactiveCommand.Create(() => { ViewMode = LibraryViewMode.Gallery; });
		ClearSearchCommand = ReactiveCommand.Create(() => { SearchText = string.Empty; });
		OpenFilterHelpCommand = ReactiveCommand.Create(commands.OpenFilterHelp);
		AddAccountCommand = ReactiveCommand.CreateFromTask(commands.AddAccountAsync);
		ScanLibraryCommand = ReactiveCommand.CreateFromTask(commands.ScanLibraryAsync);
		OpenTrashCommand = ReactiveCommand.CreateFromTask(commands.ShowTrashAsync);
		AddVisibleToFlightCommand = ReactiveCommand.CreateFromTask(AddVisibleToFlightAsync);
		EmptyPrimaryCommand = ReactiveCommand.CreateFromTask(InvokeEmptyPrimaryAsync);
		CloseDetailsCommand = ReactiveCommand.Create(() => { IsDetailsPaneOpen = false; });

		products.VisibleLibraryEntriesChanged += Products_VisibleLibraryEntriesChanged;
		flight.SelectionChanged += Flight_SelectionChanged;
		commands.Main.PropertyChanged += Main_PropertyChanged;
		responsive.PropertyChanged += Responsive_PropertyChanged;
		configuration.PropertyChanged += Configuration_PropertyChanged;
		RefreshProjection(products.GetVisibleLibraryEntrySnapshot(), products.IsInitialLoadComplete, products.FilterString);
		RefreshResponsiveState();
	}

	public event EventHandler<LibrarySelectionProjectionChangedEventArgs>? SelectionProjectionChanged;
	public ProductsDisplayViewModel Products => products;
	public CoverImageCache CoverCache { get; }
	public IReadOnlyList<LibrarySortOption> SortOptions { get; }
	public IReadOnlyList<LibraryBookItemViewModel> VisibleItems => visibleItems;
	public IReadOnlyList<GalleryRowViewModel> GalleryRows => galleryRows;
	public ICommand? ProcessSelectionCommand { get; }
	public ReactiveCommand<Unit, Unit> ShowDetailsCommand { get; }
	public ReactiveCommand<Unit, Unit> ShowGalleryCommand { get; }
	public ReactiveCommand<Unit, Unit> ClearSearchCommand { get; }
	public ReactiveCommand<Unit, Unit> OpenFilterHelpCommand { get; }
	public ReactiveCommand<Unit, Unit> AddAccountCommand { get; }
	public ReactiveCommand<Unit, Unit> ScanLibraryCommand { get; }
	public ReactiveCommand<Unit, Unit> OpenTrashCommand { get; }
	public ReactiveCommand<Unit, Unit> AddVisibleToFlightCommand { get; }
	public ReactiveCommand<Unit, Unit> EmptyPrimaryCommand { get; }
	public ReactiveCommand<Unit, Unit> CloseDetailsCommand { get; }

	public LibraryViewMode ViewMode
	{
		get => viewMode;
		set
		{
			if (viewMode == value)
				return;
			this.RaiseAndSetIfChanged(ref viewMode, value);
			configuration.LibraryViewMode = value;
			this.RaisePropertyChanged(nameof(IsDetailsMode));
			this.RaisePropertyChanged(nameof(IsGalleryMode));
		}
	}
	public bool IsDetailsMode => ViewMode == LibraryViewMode.Details;
	public bool IsGalleryMode => ViewMode == LibraryViewMode.Gallery;

	public LibrarySortOption SelectedSort
	{
		get => selectedSort;
		set
		{
			if (value is null || Equals(selectedSort, value))
				return;
			this.RaiseAndSetIfChanged(ref selectedSort, value);
			products.ApplyPresentationSort(value.MemberName, value.Direction);
		}
	}

	public string SearchText
	{
		get => searchText;
		set
		{
			value ??= string.Empty;
			if (string.Equals(searchText, value, StringComparison.Ordinal))
				return;
			this.RaiseAndSetIfChanged(ref searchText, value);
			_ = ApplySearchAfterDebounceAsync(value);
		}
	}

	public LibraryBookItemViewModel? FocusedItem
	{
		get => focusedItem;
		private set
		{
			if (ReferenceEquals(focusedItem, value))
				return;
			if (focusedItem is not null)
				focusedItem.IsFocused = false;
			this.RaiseAndSetIfChanged(ref focusedItem, value);
			if (focusedItem is not null)
				focusedItem.IsFocused = true;
			if (transientFocusedItem is not null && !ReferenceEquals(focusedItem, transientFocusedItem))
			{
				transientFocusedItem.Dispose();
				transientFocusedItem = null;
			}
			this.RaisePropertyChanged(nameof(HasFocusedItem));
			PublishSelectionProjection();
		}
	}
	public bool HasFocusedItem => FocusedItem is not null;

	public bool IsDetailsPaneOpen
	{
		get => isDetailsPaneOpen;
		set
		{
			if (isDetailsPaneOpen == value)
				return;
			this.RaiseAndSetIfChanged(ref isDetailsPaneOpen, value);
			this.RaisePropertyChanged(nameof(ShowDetailsPane));
			UpdateCoverBudget();
		}
	}
	public bool ShowDetailsPane => IsDetailsPaneOpen && HasFocusedItem;
	public SplitViewDisplayMode DetailsPaneDisplayMode { get; private set; } = SplitViewDisplayMode.Overlay;
	public bool IsDetailsPanePersistent => DetailsPaneDisplayMode == SplitViewDisplayMode.Inline;

	public bool IsLoading => !products.IsInitialLoadComplete;
	public bool HasResults => products.IsInitialLoadComplete && VisibleItems.Count > 0;
	public bool ShowEmptyLibrary => commands.Main.GettingStartedVisible;
	public bool ShowNoResults => commands.Main.NoMatchesVisible;
	public string EmptyTitle => commands.Main.GettingStartedHeadline;
	public string EmptyExplanation => commands.Main.GettingStartedDetail;
	public string EmptyPrimaryText => commands.Main.GettingStartedAddAccountVisible ? "Add Audible account" : "Scan library";
	public bool ShowEmptyTrashAction => commands.Main.GettingStartedTrashVisible;
	public string? EmptySecondaryText => ShowEmptyTrashAction ? "Open Trash" : null;
	public string NoResultsText => commands.Main.NoMatchesText;
	public bool ShowNoResultsTrashHint => commands.Main.NoMatchesTrashHintVisible;
	public string NoResultsTrashHint => commands.Main.NoMatchesTrashHintText;
	public int VisibleCount => VisibleItems.Count;
	public int SelectedCount => flight.Count;
	public int HiddenSelectedCount => flight.HiddenCount;
	public string ResultStateText => VisibleCount == 1 ? "1 title shown" : $"{VisibleCount} titles shown";
	public string SelectionStateText => flight.Count switch
	{
		0 => "No titles selected",
		1 when flight.HiddenCount == 0 => "1 title selected",
		_ when flight.HiddenCount == 0 => $"{flight.Count} titles selected",
		_ => $"{flight.Count} titles selected, {flight.HiddenCount} hidden by filter",
	};
	public bool CanAddVisibleToFlight => HasResults && VisibleItems.Any(item => !item.IsSelected);
	public int GalleryColumnCount => galleryColumnCount;
	public int SmallCoverDecodePixelWidth => smallCoverDecodePixelWidth;
	public int MediumCoverDecodePixelWidth => mediumCoverDecodePixelWidth;

	public void UpdateGalleryViewport(Size viewport, double renderScaling)
	{
		if (disposed)
			return;
		if (!double.IsFinite(viewport.Width) || viewport.Width <= 0)
			return;
		renderScaling = double.IsFinite(renderScaling) && renderScaling > 0 ? renderScaling : 1;
		int nextColumns = Math.Max(1, (int)Math.Floor((viewport.Width + GalleryCardGap) / (GalleryCardWidth + GalleryCardGap)));
		int nextSmallWidth = Math.Max(1, (int)Math.Ceiling(SmallCoverLogicalWidth * renderScaling));
		int nextMediumWidth = Math.Max(1, (int)Math.Ceiling(MediumCoverLogicalWidth * renderScaling));
		bool rowsChanged = galleryColumnCount != nextColumns;
		galleryColumnCount = nextColumns;
		smallCoverDecodePixelWidth = nextSmallWidth;
		mediumCoverDecodePixelWidth = nextMediumWidth;
		this.RaisePropertyChanged(nameof(GalleryColumnCount));
		this.RaisePropertyChanged(nameof(SmallCoverDecodePixelWidth));
		this.RaisePropertyChanged(nameof(MediumCoverDecodePixelWidth));
		if (rowsChanged)
			RebuildGalleryRows();
		UpdateCoverBudget();
	}

	internal void RegisterCoverConsumer(CoverVariant variant, bool realized)
	{
		if (disposed)
			return;
		if (variant == CoverVariant.Small)
			realizedSmallCoverCount = Math.Max(0, realizedSmallCoverCount + (realized ? 1 : -1));
		else
			realizedMediumCoverCount = Math.Max(0, realizedMediumCoverCount + (realized ? 1 : -1));
		UpdateCoverBudget();
	}

	public void SelectGalleryItem(LibraryBookItemViewModel item, KeyModifiers modifiers)
	{
		ArgumentNullException.ThrowIfNull(item);
		var commandModifier = global::LibationAvalonia.KeyGestureHelper.CommandModifier;
		bool extend = modifiers.HasFlag(KeyModifiers.Shift) && selectionAnchor is not null;
		bool toggle = modifiers.HasFlag(commandModifier);
		if (extend)
		{
			int anchorIndex = IndexOf(selectionAnchor!.Value);
			int itemIndex = IndexOf(item.Id);
			if (anchorIndex >= 0 && itemIndex >= 0)
			{
				int first = Math.Min(anchorIndex, itemIndex);
				int last = Math.Max(anchorIndex, itemIndex);
				var desired = toggle
					? flight.Items.Select(selected => selected.Id).ToHashSet()
					: new HashSet<FlightItemId>();
				for (int index = first; index <= last; index++)
					desired.Add(VisibleItems[index].Id);
				ReplaceVisibleSelection(desired);
			}
		}
		else if (toggle)
		{
			flight.Toggle(item.LibraryBook);
			selectionAnchor = item.Id;
		}
		else
		{
			ReplaceVisibleSelection(new HashSet<FlightItemId> { item.Id });
			selectionAnchor = item.Id;
		}

		FocusedItem = item;
	}

	public void SynchronizeDetailsSelection(ProductsDisplaySelectionChangedEventArgs selection)
	{
		ArgumentNullException.ThrowIfNull(selection);
		ReplaceVisibleSelection(selection.SelectedEntries.Select(entry => FlightItemId.From(entry.LibraryBook)).ToHashSet());
		if (selection.FocusedEntry is not null && itemMap.TryGetValue(FlightItemId.From(selection.FocusedEntry.LibraryBook), out var focused))
		{
			FocusedItem = focused;
			selectionAnchor = focused.Id;
		}
	}

	public void OpenItem(LibraryBookItemViewModel item)
	{
		ArgumentNullException.ThrowIfNull(item);
		FocusedItem = item;
		IsDetailsPaneOpen = true;
	}

	/// <summary>
	/// Opens a title without clearing or rewriting the authoritative current filter.
	/// A hidden Flight title gets one details-only wrapper so cross-route navigation
	/// remains truthful while the visible projection and selection stay unchanged.
	/// </summary>
	public bool TryOpenBook(DataLayer.LibraryBook? book)
	{
		if (book?.Book is null || string.IsNullOrWhiteSpace(book.Book.AudibleProductId))
			return false;
		if (!itemMap.TryGetValue(new FlightItemId(book.Book.AudibleProductId), out var item))
		{
			transientFocusedItem?.Dispose();
			transientFocusedItem = new LibraryBookItemViewModel(new LibraryBookEntry(book), this);
			item = transientFocusedItem;
		}
		OpenItem(item);
		return true;
	}

	public void FocusGalleryItem(LibraryBookItemViewModel item)
	{
		ArgumentNullException.ThrowIfNull(item);
		FocusedItem = item;
	}

	public IReadOnlyList<LibraryBookEntry> GetContextSelection(LibraryBookItemViewModel clicked)
	{
		if (!clicked.IsSelected)
			SelectGalleryItem(clicked, KeyModifiers.None);
		var selected = flight.Items.Select(item => item.Id).ToHashSet();
		return VisibleItems.Where(item => selected.Contains(item.Id)).Select(item => item.Entry).ToArray();
	}

	private void ReplaceVisibleSelection(IReadOnlySet<FlightItemId> desiredVisibleIds)
	{
		var visibleIds = VisibleItems.Select(item => item.Id).ToHashSet();
		foreach (var selected in flight.Items.Where(item => visibleIds.Contains(item.Id) && !desiredVisibleIds.Contains(item.Id)).ToArray())
			flight.Remove(selected.Id);
		foreach (var item in VisibleItems.Where(item => desiredVisibleIds.Contains(item.Id)))
			flight.Add(item.LibraryBook);
		RefreshSelectionState();
	}

	private async Task AddVisibleToFlightAsync()
	{
		var selectedIds = flight.Items.Select(item => item.Id).ToHashSet();
		var candidates = VisibleItems
			.Where(item => !selectedIds.Contains(item.Id))
			.Select(item => item.LibraryBook)
			.ToArray();
		if (candidates.Length == 0)
			return;

		string countText = candidates.Length == 1 ? "1 shown title" : $"{candidates.Length} shown titles";
		var result = await global::LibationAvalonia.MessageBox.Show(
			global::LibationAvalonia.App.MainWindow,
			$"Add {countText} to Current Flight?\n\nThis changes the explicit Flight selection only. It does not download, process, or remove any title.",
			"Add shown titles to Current Flight",
			MessageBoxButtons.OKCancel,
			MessageBoxIcon.Question,
			MessageBoxDefaultButton.Button1);
		if (result is DialogResult.OK)
			flight.AddRange(candidates);
	}

	private int IndexOf(FlightItemId id)
	{
		for (int index = 0; index < VisibleItems.Count; index++)
			if (VisibleItems[index].Id == id)
				return index;
		return -1;
	}

	private async Task ApplySearchAfterDebounceAsync(string requestedText)
	{
		var next = new CancellationTokenSource();
		var previous = Interlocked.Exchange(ref searchCancellation, next);
		previous?.Cancel();
		previous?.Dispose();
		try
		{
			await Task.Delay(SearchDebounce, next.Token);
			next.Token.ThrowIfCancellationRequested();
			if (Dispatcher.UIThread.CheckAccess())
				await commands.ApplyFilterAsync(requestedText);
			else
				Dispatcher.UIThread.Post(() => _ = commands.ApplyFilterAsync(requestedText));
		}
		catch (OperationCanceledException) when (next.IsCancellationRequested)
		{
		}
		catch (Exception ex)
		{
			Serilog.Log.Logger.Error(ex, "Unable to apply the Library filter.");
		}
	}

	private void Products_VisibleLibraryEntriesChanged(object? sender, VisibleLibraryEntriesChangedEventArgs e)
	{
		if (!Dispatcher.UIThread.CheckAccess())
		{
			Dispatcher.UIThread.Post(() => Products_VisibleLibraryEntriesChanged(sender, e));
			return;
		}
		if (!string.Equals(searchText, e.FilterText, StringComparison.Ordinal))
		{
			searchText = e.FilterText;
			this.RaisePropertyChanged(nameof(SearchText));
		}
		RefreshProjection(e.Entries, e.IsInitialLoadComplete, e.FilterText);
	}

	private void RefreshProjection(IReadOnlyList<LibraryBookEntry> entries, bool isLoaded, string? filterText)
	{
		var liveIds = entries.Select(entry => FlightItemId.From(entry.LibraryBook)).ToHashSet();
		foreach (var removed in itemMap.Keys.Where(id => !liveIds.Contains(id)).ToArray())
		{
			itemMap[removed].Dispose();
			itemMap.Remove(removed);
		}

		foreach (var entry in entries)
		{
			var id = FlightItemId.From(entry.LibraryBook);
			if (itemMap.TryGetValue(id, out var existing))
				existing.ReplaceEntry(entry);
			else
				itemMap.Add(id, new LibraryBookItemViewModel(entry, this));
		}

		visibleItems = entries.Select(entry => itemMap[FlightItemId.From(entry.LibraryBook)]).ToArray();
		flight.SetVisibleItems(entries.Select(entry => entry.LibraryBook));
		if (FocusedItem is not null && !liveIds.Contains(FocusedItem.Id))
			FocusedItem = null;
		RebuildGalleryRows();
		RefreshSelectionState();
		RaiseResultStateChanged();
	}

	private void RebuildGalleryRows()
	{
		galleryRows = VisibleItems
			.Chunk(Math.Max(1, GalleryColumnCount))
			.Select((items, index) => new GalleryRowViewModel(index, items))
			.ToArray();
		this.RaisePropertyChanged(nameof(GalleryRows));
	}

	private void Flight_SelectionChanged(object? sender, FlightChangedEventArgs e)
	{
		if (Dispatcher.UIThread.CheckAccess())
			RefreshSelectionState();
		else
			Dispatcher.UIThread.Post(RefreshSelectionState);
	}

	private void RefreshSelectionState()
	{
		var selected = flight.Items.Select(item => item.Id).ToHashSet();
		foreach (var item in VisibleItems)
			item.IsSelected = selected.Contains(item.Id);
		this.RaisePropertyChanged(nameof(SelectedCount));
		this.RaisePropertyChanged(nameof(HiddenSelectedCount));
		this.RaisePropertyChanged(nameof(SelectionStateText));
		this.RaisePropertyChanged(nameof(CanAddVisibleToFlight));
		PublishSelectionProjection();
	}

	private void PublishSelectionProjection()
		=> SelectionProjectionChanged?.Invoke(
			this,
			new LibrarySelectionProjectionChangedEventArgs(
				flight.Items.Select(item => item.Id.ProductId).ToHashSet(StringComparer.Ordinal),
				FocusedItem?.ProductId));

	private void Main_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (!Dispatcher.UIThread.CheckAccess())
		{
			Dispatcher.UIThread.Post(() => Main_PropertyChanged(sender, e));
			return;
		}
		RaiseResultStateChanged();
	}

	private void RaiseResultStateChanged()
	{
		this.RaisePropertyChanged(nameof(IsLoading));
		this.RaisePropertyChanged(nameof(HasResults));
		this.RaisePropertyChanged(nameof(ShowEmptyLibrary));
		this.RaisePropertyChanged(nameof(ShowNoResults));
		this.RaisePropertyChanged(nameof(EmptyTitle));
		this.RaisePropertyChanged(nameof(EmptyExplanation));
		this.RaisePropertyChanged(nameof(EmptyPrimaryText));
		this.RaisePropertyChanged(nameof(ShowEmptyTrashAction));
		this.RaisePropertyChanged(nameof(EmptySecondaryText));
		this.RaisePropertyChanged(nameof(NoResultsText));
		this.RaisePropertyChanged(nameof(ShowNoResultsTrashHint));
		this.RaisePropertyChanged(nameof(NoResultsTrashHint));
		this.RaisePropertyChanged(nameof(VisibleCount));
		this.RaisePropertyChanged(nameof(ResultStateText));
		this.RaisePropertyChanged(nameof(CanAddVisibleToFlight));
	}

	private void Responsive_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(ResponsiveLayoutService.Current))
			RefreshResponsiveState();
	}

	private void RefreshResponsiveState()
	{
		DetailsPaneDisplayMode = responsive.Current.ContextualPane == ContextualPaneState.Persistent
			? SplitViewDisplayMode.Inline
			: SplitViewDisplayMode.Overlay;
		this.RaisePropertyChanged(nameof(DetailsPaneDisplayMode));
		this.RaisePropertyChanged(nameof(IsDetailsPanePersistent));
		UpdateCoverBudget();
	}

	private void Configuration_PropertyChanged(object sender, Dinah.Core.PropertyChangedEventArgsEx e)
	{
		if (e.PropertyName == nameof(Configuration.LibraryViewMode) && configuration.LibraryViewMode != ViewMode)
		{
			viewMode = configuration.LibraryViewMode;
			this.RaisePropertyChanged(nameof(ViewMode));
			this.RaisePropertyChanged(nameof(IsDetailsMode));
			this.RaisePropertyChanged(nameof(IsGalleryMode));
		}
	}

	private Task InvokeEmptyPrimaryAsync()
		=> commands.Main.GettingStartedAddAccountVisible ? commands.AddAccountAsync() : commands.ScanLibraryAsync();

	private void UpdateCoverBudget()
	{
		if (disposed)
			return;
		CoverCache.ConfigureViewportBudget(
			realizedSmallCoverCount,
			SmallCoverDecodePixelWidth,
			realizedMediumCoverCount > 0 && ShowDetailsPane,
			MediumCoverDecodePixelWidth);
	}

	public void Dispose()
	{
		if (disposed)
			return;
		disposed = true;
		searchCancellation?.Cancel();
		searchCancellation?.Dispose();
		products.VisibleLibraryEntriesChanged -= Products_VisibleLibraryEntriesChanged;
		flight.SelectionChanged -= Flight_SelectionChanged;
		commands.Main.PropertyChanged -= Main_PropertyChanged;
		responsive.PropertyChanged -= Responsive_PropertyChanged;
		configuration.PropertyChanged -= Configuration_PropertyChanged;
		foreach (var item in itemMap.Values)
			item.Dispose();
		itemMap.Clear();
		transientFocusedItem?.Dispose();
		transientFocusedItem = null;
		CoverCache.Dispose();
		ShowDetailsCommand.Dispose();
		ShowGalleryCommand.Dispose();
		ClearSearchCommand.Dispose();
		OpenFilterHelpCommand.Dispose();
		AddAccountCommand.Dispose();
		ScanLibraryCommand.Dispose();
		OpenTrashCommand.Dispose();
		AddVisibleToFlightCommand.Dispose();
		EmptyPrimaryCommand.Dispose();
		CloseDetailsCommand.Dispose();
	}
}
