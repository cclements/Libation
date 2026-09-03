using LibationAvalonia.DesignSystem.Components;
using LibationAvalonia.Features.Tools;
using LibationAvalonia.Shell;
using LibationAvalonia.ViewModels;
using Avalonia.Threading;
using DataLayer;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace LibationAvalonia.Features.Trash;

/// <summary>
/// Lazy inline projection over the existing trash query and mutation owners.
/// Podcast parent rows are retained only to explain their removed episodes.
/// </summary>
public sealed class TrashViewModel : SecondaryDestinationViewModel, IRoutePresentation
{
	private sealed record TrashRowSource(LibraryBook LibraryBook, string? ParentTitle = null, string? RelatedSearchText = null)
	{
		internal TrashItemKey Key => TrashItemKey.From(LibraryBook);
	}

	private readonly MainVM main;
	private readonly ILibationCommandAdapter commands;
	private readonly CancellationTokenSource lifetime = new();
	private IReadOnlyList<TrashItemViewModel> allItems = [];
	private bool isActive;
	private bool refreshPending = true;
	private bool hasLoaded;
	private bool disposed;

	public TrashViewModel(ILibationCommandAdapter commands)
	{
		ArgumentNullException.ThrowIfNull(commands);
		this.commands = commands;
		main = commands.Main;
		RefreshCommand = Track(ReactiveCommand.CreateFromTask(RefreshAsync));
		RestoreSelectedCommand = CreateOwnerCommand(
			RestoreSelectedAsync,
			"restore selected Trash titles",
			"Libation could not restore the selected titles. Their existing Trash state remains authoritative.");
		PermanentlyDeleteSelectedCommand = CreateOwnerCommand(
			PermanentlyDeleteSelectedAsync,
			"permanently delete selected Trash records",
			"Libation could not permanently delete the selected records. Review Trash before trying again.");
		main.PropertyChanged += Main_PropertyChanged;
	}

	public int Count => hasLoaded ? allItems.Count(item => item.CanSelect) : main.BooksInTrash;
	public bool HasItems => Count > 0;
	public string CountText => Count == 1 ? "1 title in Trash" : $"{Count.ToString("N0", CultureInfo.CurrentCulture)} titles in Trash";
	public LibationStatusKind Status => HasItems ? LibationStatusKind.NeedsAttention : LibationStatusKind.Completed;
	public string SearchText
	{
		get => field;
		set
		{
			this.RaiseAndSetIfChanged(ref field, value ?? string.Empty);
			ApplyFilter();
		}
	} = string.Empty;
	public ObservableCollection<TrashItemViewModel> VisibleItems { get; } = new();
	public bool IsLoading { get => field; private set { this.RaiseAndSetIfChanged(ref field, value); RaiseResultState(); } } = true;
	public bool HasVisibleItems => VisibleItems.Count > 0;
	public bool ShowEmpty => !IsLoading && !HasItems;
	public bool ShowNoMatches => !IsLoading && HasItems && !HasVisibleItems;
	public int SelectedCount => allItems.Count(item => item.CanSelect && item.IsSelected);
	public bool HasSelection => SelectedCount > 0;
	public string SelectedCountText => SelectedCount == 1
		? "1 removed title selected"
		: $"{SelectedCount.ToString("N0", CultureInfo.CurrentCulture)} removed titles selected";
	public string RestoreActionAccessibleName => SelectedCount == 1 ? "Restore 1 selected title" : $"Restore {SelectedCount} selected titles";
	public string DeleteActionAccessibleName => SelectedCount == 1
		? "Permanently delete 1 selected Libation record"
		: $"Permanently delete {SelectedCount} selected Libation records";
	public string ResultSummary => string.IsNullOrWhiteSpace(SearchText)
		? $"{CountText}; {SelectedCountText}"
		: $"{VisibleItems.Count.ToString("N0", CultureInfo.CurrentCulture)} rows match the current search; {SelectedCountText}";
	public string? LastActionText { get => field; private set => this.RaiseAndSetIfChanged(ref field, value); }
	public ICommand RefreshCommand { get; }
	public ICommand RestoreSelectedCommand { get; }
	public ICommand PermanentlyDeleteSelectedCommand { get; }
	public string RouteEyebrow => "Removed library records";
	public string RouteTitle => "Trash";
	public string RouteSubtitle => "Review titles hidden from the Library before restoring or permanently deleting records.";
	public RouteCommandPresentation RoutePrimaryCommand => new("Refresh Trash", RefreshCommand);
	public IReadOnlyList<RouteCommandPresentation> RouteSecondaryCommands => [];
	public RouteStatusPresentation RouteStatusBadge => new(CountText, Status);

	public void SetActive(bool active)
	{
		if (disposed)
			return;
		if (!Dispatcher.UIThread.CheckAccess())
		{
			Dispatcher.UIThread.Post(() => SetActive(active));
			return;
		}

		isActive = active;
		if (isActive && refreshPending)
			_ = RefreshAsync();
	}

	public Task RefreshAsync() => RunOwnerActionAsync(
		RefreshCoreAsync,
		"refresh Trash",
		"Libation could not load Trash. No library records were changed.");

	private async Task RefreshCoreAsync()
	{
		if (disposed)
			return;
		IsLoading = true;
		try
		{
			var cancellationToken = lifetime.Token;
			var books = await commands.GetTrashItemsAsync(cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();
			var rows = await Task.Run(() => BuildRows(books), cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();
			ReconcileItems(rows);
			hasLoaded = true;
			refreshPending = false;
			ApplyFilter();
		}
		catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
		{
			return;
		}
		finally
		{
			IsLoading = false;
			RaiseResultState();
		}
	}

	private async Task RestoreSelectedAsync()
	{
		var selected = SelectedBooks();
		if (selected.Length == 0)
			return;
		int changed = await commands.RestoreTrashBooksAsync(selected);
		LastActionText = changed == 1 ? "Restored 1 title to the Library." : changed > 1 ? $"Restored {changed} titles to the Library." : "No Trash records changed.";
		if (changed > 0)
			await RefreshCoreAsync();
	}

	private async Task PermanentlyDeleteSelectedAsync()
	{
		var selected = SelectedBooks();
		if (selected.Length == 0)
			return;
		int changed = await commands.PermanentlyDeleteTrashBooksConfirmedAsync(selected);
		LastActionText = changed == 1
			? "Permanently deleted 1 record from Libation. Audiobook files were left untouched."
			: changed > 1
				? $"Permanently deleted {changed} records from Libation. Audiobook files were left untouched."
				: "No Trash records changed.";
		if (changed > 0)
			await RefreshCoreAsync();
	}

	private LibraryBook[] SelectedBooks() => allItems
		.Where(item => item.CanSelect && item.IsSelected)
		.Select(item => item.LibraryBook)
		.ToArray();

	private static IReadOnlyList<TrashRowSource> BuildRows(IReadOnlyList<LibraryBook> source)
	{
		var parents = source
			.Where(book => book.Book.ContentType == ContentType.Parent)
			.GroupBy(book => book.Book.AudibleProductId, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
		var deleted = source
			.Where(book => book.IsDeleted && book.Book.ContentType != ContentType.Parent)
			.ToArray();
		var grouped = new HashSet<LibraryBook>();
		var groups = new Dictionary<LibraryBook, List<LibraryBook>>();

		foreach (var child in deleted.Where(book => book.Book.ContentType == ContentType.Episode))
		{
			var parent = child.Book.SeriesLink
				.Select(link => link.Series.AudibleSeriesId)
				.Where(id => !string.IsNullOrWhiteSpace(id))
				.Select(id => parents.GetValueOrDefault(id))
				.FirstOrDefault(candidate => candidate is not null);
			if (parent is null)
				continue;
			if (!groups.TryGetValue(parent, out var children))
				groups.Add(parent, children = []);
			children.Add(child);
			grouped.Add(child);
		}

		var rows = new List<TrashRowSource>(deleted.Length + groups.Count);
		foreach (var group in groups.OrderByDescending(group => group.Value.Max(child => child.DateAdded)))
		{
			string childSearch = string.Join(" ", group.Value.Select(child => child.Book.TitleWithSubtitle));
			rows.Add(new(group.Key, RelatedSearchText: childSearch));
			rows.AddRange(group.Value
				.OrderByDescending(child => child.DateAdded)
				.Select(child => new TrashRowSource(child, group.Key.Book.TitleWithSubtitle)));
		}
		rows.AddRange(deleted
			.Where(book => !grouped.Contains(book))
			.OrderByDescending(book => book.DateAdded)
			.Select(book => new TrashRowSource(book)));
		return rows;
	}

	private void ReconcileItems(IReadOnlyList<TrashRowSource> rows)
	{
		var existing = allItems.ToDictionary(item => item.Key);
		var next = new List<TrashItemViewModel>(rows.Count);
		foreach (var row in rows)
		{
			if (existing.Remove(row.Key, out var item))
				item.Update(row.LibraryBook, row.ParentTitle, row.RelatedSearchText);
			else
			{
				item = new(row.LibraryBook, row.ParentTitle, row.RelatedSearchText);
				item.SelectionChanged += Item_SelectionChanged;
			}
			next.Add(item);
		}
		foreach (var removed in existing.Values)
			removed.SelectionChanged -= Item_SelectionChanged;
		allItems = next;
	}

	private void ApplyFilter()
	{
		string query = SearchText.Trim();
		var desired = string.IsNullOrWhiteSpace(query)
			? allItems
			: allItems.Where(item => item.Matches(query)).ToArray();
		var desiredSet = desired.ToHashSet();
		for (int index = VisibleItems.Count - 1; index >= 0; index--)
			if (!desiredSet.Contains(VisibleItems[index]))
				VisibleItems.RemoveAt(index);
		for (int index = 0; index < desired.Count; index++)
		{
			var item = desired[index];
			if (index < VisibleItems.Count && ReferenceEquals(VisibleItems[index], item))
				continue;
			int currentIndex = VisibleItems.IndexOf(item);
			if (currentIndex >= 0)
				VisibleItems.Move(currentIndex, index);
			else
				VisibleItems.Insert(index, item);
		}
		RaiseResultState();
	}

	private void Item_SelectionChanged(object? sender, EventArgs e) => RaiseSelectionState();

	private void RaiseSelectionState()
	{
		this.RaisePropertyChanged(nameof(SelectedCount));
		this.RaisePropertyChanged(nameof(HasSelection));
		this.RaisePropertyChanged(nameof(SelectedCountText));
		this.RaisePropertyChanged(nameof(RestoreActionAccessibleName));
		this.RaisePropertyChanged(nameof(DeleteActionAccessibleName));
		this.RaisePropertyChanged(nameof(ResultSummary));
	}

	private void RaiseResultState()
	{
		this.RaisePropertyChanged(nameof(Count));
		this.RaisePropertyChanged(nameof(HasItems));
		this.RaisePropertyChanged(nameof(CountText));
		this.RaisePropertyChanged(nameof(Status));
		this.RaisePropertyChanged(nameof(HasVisibleItems));
		this.RaisePropertyChanged(nameof(ShowEmpty));
		this.RaisePropertyChanged(nameof(ShowNoMatches));
		this.RaisePropertyChanged(nameof(ResultSummary));
		this.RaisePropertyChanged(nameof(RouteStatusBadge));
	}

	private void Main_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (disposed)
			return;
		if (!string.IsNullOrEmpty(e.PropertyName) && e.PropertyName != nameof(MainVM.BooksInTrash))
			return;
		if (!Dispatcher.UIThread.CheckAccess())
		{
			Dispatcher.UIThread.Post(() => Main_PropertyChanged(sender, e));
			return;
		}
		refreshPending = true;
		RaiseResultState();
		if (isActive && !IsActionRunning)
			_ = RefreshAsync();
	}

	protected override void DisposeCore()
	{
		disposed = true;
		main.PropertyChanged -= Main_PropertyChanged;
		foreach (var item in allItems)
			item.SelectionChanged -= Item_SelectionChanged;
		lifetime.Cancel();
		lifetime.Dispose();
	}
}
