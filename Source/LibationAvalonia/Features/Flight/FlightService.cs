using DataLayer;
using Avalonia.Threading;
using LibationAvalonia.ViewModels;
using LibationFileManager;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace LibationAvalonia.Features.Flight;

public readonly record struct FlightItemId(string ProductId)
{
	public static FlightItemId From(LibraryBook book)
	{
		ArgumentNullException.ThrowIfNull(book);
		if (string.IsNullOrWhiteSpace(book.Book?.AudibleProductId))
			throw new ArgumentException("A Current Flight title must have a stable product identifier.", nameof(book));
		return new(book.Book.AudibleProductId);
	}

	public override string ToString() => ProductId;
}

public sealed class FlightItemViewModel(LibraryBook libraryBook) : ViewModelBase
{
	private LibraryBook book = libraryBook;

	public FlightItemId Id => FlightItemId.From(book);
	public LibraryBook LibraryBook
	{
		get => book;
		internal set
		{
			if (FlightItemId.From(value) != Id)
				throw new InvalidOperationException("A Flight item's stable identity cannot change.");
			this.RaiseAndSetIfChanged(ref book, value);
			this.RaisePropertyChanged(nameof(Title));
			this.RaisePropertyChanged(nameof(Author));
			this.RaisePropertyChanged(nameof(DurationMinutes));
			this.RaisePropertyChanged(nameof(IsAvailable));
		}
	}
	public string Title => book.Book.TitleWithSubtitle;
	public string Author => book.Book.AuthorNames;
	public int DurationMinutes => Math.Max(0, book.Book.LengthInMinutes);
	public bool IsAvailable => !book.AbsentFromLastScan;
}

public sealed record FlightUndoEntry(FlightItemViewModel Item, int Index);

public sealed class FlightUndoToken
{
	internal FlightUndoToken(IReadOnlyList<FlightUndoEntry> entries) => Entries = entries;
	internal IReadOnlyList<FlightUndoEntry> Entries { get; }
	public bool CanRestore => Entries.Count > 0;
}

public enum FlightChangeKind
{
	Add,
	Replace,
	Move,
	Remove,
	Clear,
	Restore,
	Reconcile,
}

public sealed class FlightChangedEventArgs(string announcement, FlightChangeKind kind = FlightChangeKind.Replace) : EventArgs
{
	public string Announcement { get; } = announcement;
	public FlightChangeKind Kind { get; } = kind;
}

public interface IFlightService
{
	ReadOnlyObservableCollection<FlightItemViewModel> Items { get; }
	int Count { get; }
	int HiddenCount { get; }
	int TotalDurationMinutes { get; }
	long EstimatedBytes { get; }
	event EventHandler<FlightChangedEventArgs>? SelectionChanged;
	bool Add(LibraryBook book);
	int AddRange(IEnumerable<LibraryBook> books);
	bool Replace(IReadOnlyCollection<FlightItemId> ids);
	bool Move(FlightItemId id, int destinationIndex);
	FlightUndoToken Remove(FlightItemId id);
	FlightUndoToken Clear();
	bool Restore(FlightUndoToken token);
	bool Toggle(LibraryBook book);
	void SetVisibleItems(IEnumerable<LibraryBook> visibleBooks);
	void ReconcileLibrary(IEnumerable<LibraryBook> library);
}

/// <summary>
/// The one ordered, stable-ID selection shared by Details, Gallery, routes,
/// and profiles. It never owns a second library or processing queue.
/// </summary>
public sealed class FlightService : ViewModelBase, IFlightService, IDisposable
{
	private readonly Configuration configuration;
	private readonly ObservableCollection<FlightItemViewModel> items = new();
	private readonly Dictionary<FlightItemId, FlightItemViewModel> byId = new();
	private readonly Dictionary<FlightItemId, LibraryBook> availableById = new();
	private HashSet<FlightItemId>? visibleIds;
	private int hiddenCount;
	private string[]? pendingPersistentIds;
	private bool persistenceWriteQueued;
	private bool disposed;

	public FlightService(Configuration configuration)
	{
		this.configuration = configuration;
		Items = new(items);
		configuration.PropertyChanged += Configuration_PropertyChanged;
	}

	public ReadOnlyObservableCollection<FlightItemViewModel> Items { get; }
	public int Count => items.Count;
	public int HiddenCount
	{
		get => hiddenCount;
		private set => this.RaiseAndSetIfChanged(ref hiddenCount, value);
	}
	public int TotalDurationMinutes => items.Sum(item => item.DurationMinutes);
	public long EstimatedBytes => (long)items.Count * DiskSpaceHelper.EstimatedBytesPerAudiobookBackup;
	public event EventHandler<FlightChangedEventArgs>? SelectionChanged;

	public bool Add(LibraryBook book)
	{
		var id = FlightItemId.From(book);
		availableById[id] = book;
		if (byId.ContainsKey(id))
			return false;

		var item = new FlightItemViewModel(book);
		byId.Add(id, item);
		items.Add(item);
		OnChanged($"Added {item.Title} to Current Flight. {Count} selected.", FlightChangeKind.Add);
		return true;
	}

	public int AddRange(IEnumerable<LibraryBook> books)
	{
		ArgumentNullException.ThrowIfNull(books);
		int added = 0;
		foreach (var book in books)
		{
			var id = FlightItemId.From(book);
			availableById[id] = book;
			if (byId.ContainsKey(id))
				continue;
			var item = new FlightItemViewModel(book);
			byId.Add(id, item);
			items.Add(item);
			added++;
		}
		if (added > 0)
			OnChanged($"Added {added} titles to Current Flight. {Count} selected.", FlightChangeKind.Add);
		return added;
	}

	/// <summary>
	/// Replaces the explicit selection as one user gesture. Stable item instances are
	/// moved or retained where possible, and unavailable identifiers are ignored.
	/// </summary>
	public bool Replace(IReadOnlyCollection<FlightItemId> ids)
	{
		ArgumentNullException.ThrowIfNull(ids);
		var desiredIds = ids.Distinct().Where(id => byId.ContainsKey(id) || availableById.ContainsKey(id)).ToArray();
		if (items.Select(item => item.Id).SequenceEqual(desiredIds))
			return false;

		var desiredItems = desiredIds.Select(id => byId.TryGetValue(id, out var existing)
			? existing
			: new FlightItemViewModel(availableById[id])).ToArray();
		var desiredSet = desiredIds.ToHashSet();
		for (int index = items.Count - 1; index >= 0; index--)
			if (!desiredSet.Contains(items[index].Id))
				items.RemoveAt(index);

		for (int index = 0; index < desiredItems.Length; index++)
		{
			var desired = desiredItems[index];
			if (index < items.Count && ReferenceEquals(items[index], desired))
				continue;
			int currentIndex = items.IndexOf(desired);
			if (currentIndex >= 0)
				items.Move(currentIndex, index);
			else
				items.Insert(index, desired);
		}

		byId.Clear();
		foreach (var item in items)
			byId.Add(item.Id, item);
		OnChanged($"Current Flight now contains {Count} title(s).", FlightChangeKind.Replace);
		return true;
	}

	public bool Move(FlightItemId id, int destinationIndex)
	{
		if (!byId.TryGetValue(id, out var item) || items.Count < 2)
			return false;
		int currentIndex = items.IndexOf(item);
		int targetIndex = Math.Clamp(destinationIndex, 0, items.Count - 1);
		if (currentIndex == targetIndex)
			return false;
		items.Move(currentIndex, targetIndex);
		OnChanged($"Moved {item.Title} to position {targetIndex + 1} in Current Flight.", FlightChangeKind.Move);
		return true;
	}

	public bool Toggle(LibraryBook book)
	{
		var id = FlightItemId.From(book);
		if (byId.ContainsKey(id))
		{
			Remove(id);
			return false;
		}
		Add(book);
		return true;
	}

	public FlightUndoToken Remove(FlightItemId id)
	{
		if (!byId.Remove(id, out var item))
			return new([]);
		int index = items.IndexOf(item);
		items.RemoveAt(index);
		OnChanged($"Removed {item.Title} from Current Flight. {Count} selected.", FlightChangeKind.Remove);
		return new([new(item, index)]);
	}

	public FlightUndoToken Clear()
	{
		if (items.Count == 0)
			return new([]);
		var entries = items.Select((item, index) => new FlightUndoEntry(item, index)).ToArray();
		items.Clear();
		byId.Clear();
		OnChanged("Cleared Current Flight.", FlightChangeKind.Clear);
		return new(entries);
	}

	public bool Restore(FlightUndoToken token)
	{
		ArgumentNullException.ThrowIfNull(token);
		bool restored = false;
		foreach (var entry in token.Entries.OrderBy(entry => entry.Index))
		{
			if (byId.ContainsKey(entry.Item.Id))
				continue;
			int index = Math.Clamp(entry.Index, 0, items.Count);
			items.Insert(index, entry.Item);
			byId.Add(entry.Item.Id, entry.Item);
			restored = true;
		}
		if (restored)
			OnChanged($"Restored Current Flight. {Count} selected.", FlightChangeKind.Restore);
		return restored;
	}

	public void SetVisibleItems(IEnumerable<LibraryBook> visibleBooks)
	{
		ArgumentNullException.ThrowIfNull(visibleBooks);
		var visible = visibleBooks.ToArray();
		foreach (var book in visible)
			availableById[FlightItemId.From(book)] = book;
		visibleIds = visible.Select(FlightItemId.From).ToHashSet();
		UpdateHiddenCount();
	}

	public void ReconcileLibrary(IEnumerable<LibraryBook> library)
	{
		ArgumentNullException.ThrowIfNull(library);
		var available = library
			.Where(book => book.Book is not null && !string.IsNullOrWhiteSpace(book.Book.AudibleProductId))
			.GroupBy(FlightItemId.From)
			.ToDictionary(group => group.Key, group => group.First());
		availableById.Clear();
		foreach (var pair in available)
			availableById.Add(pair.Key, pair.Value);

		if (items.Count == 0 && configuration.PersistFlightBetweenSessions)
		{
			foreach (var productId in configuration.ContemporaryFlightProductIds)
			{
				var id = new FlightItemId(productId);
				if (available.TryGetValue(id, out var persistedBook) && !byId.ContainsKey(id))
				{
					var persistedItem = new FlightItemViewModel(persistedBook);
					byId.Add(id, persistedItem);
					items.Add(persistedItem);
				}
			}
		}

		int removed = 0;
		foreach (var item in items.ToArray())
		{
			if (available.TryGetValue(item.Id, out var current))
				item.LibraryBook = current;
			else
			{
				items.Remove(item);
				byId.Remove(item.Id);
				removed++;
			}
		}

		// Reconciliation can replace the live LibraryBook instances without changing IDs.
		// Publish once so aggregate duration/size and row metadata refresh together.
		OnChanged(removed > 0
			? $"Removed {removed} unavailable titles from Current Flight. {Count} selected."
			: $"Current Flight contains {Count} titles.", FlightChangeKind.Reconcile);
	}

	private void OnChanged(string announcement, FlightChangeKind kind)
	{
		this.RaisePropertyChanged(nameof(Count));
		this.RaisePropertyChanged(nameof(TotalDurationMinutes));
		this.RaisePropertyChanged(nameof(EstimatedBytes));
		UpdateHiddenCount();
		QueuePersistenceWrite();
		SelectionChanged?.Invoke(this, new(announcement, kind));
	}

	private void QueuePersistenceWrite()
	{
		if (!configuration.PersistFlightBetweenSessions)
			return;
		pendingPersistentIds = items.Select(item => item.Id.ProductId).ToArray();
		// Ephemeral configuration is the synchronous contract used by headless owners;
		// persistent profiles coalesce a burst of selection gestures at dispatcher idle.
		if (configuration.IsEphemeralInstance)
		{
			FlushPersistenceWrite();
			return;
		}
		if (persistenceWriteQueued)
			return;
		persistenceWriteQueued = true;
		Dispatcher.UIThread.Post(FlushPersistenceWrite, DispatcherPriority.Background);
	}

	private void FlushPersistenceWrite()
	{
		persistenceWriteQueued = false;
		var ids = pendingPersistentIds;
		pendingPersistentIds = null;
		if (ids is not null && configuration.PersistFlightBetweenSessions)
			configuration.ContemporaryFlightProductIds = ids;
	}

	private void UpdateHiddenCount()
		=> HiddenCount = visibleIds is null ? 0 : items.Count(item => !visibleIds.Contains(item.Id));

	private void Configuration_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(Configuration.PersistFlightBetweenSessions))
			return;
		if (configuration.PersistFlightBetweenSessions)
			QueuePersistenceWrite();
		else
		{
			pendingPersistentIds = null;
			configuration.ContemporaryFlightProductIds = [];
		}
	}

	public void Dispose()
	{
		if (disposed)
			return;
		disposed = true;
		FlushPersistenceWrite();
		configuration.PropertyChanged -= Configuration_PropertyChanged;
	}
}
