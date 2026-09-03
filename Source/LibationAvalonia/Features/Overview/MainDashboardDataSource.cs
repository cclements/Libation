using ApplicationServices;
using Avalonia.Threading;
using DataLayer;
using LibationAvalonia.DesignSystem.Components;
using LibationAvalonia.Features.Flight;
using LibationAvalonia.Features.Processing;
using LibationAvalonia.ViewModels;
using LibationUiBase.ProcessQueue;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LibationAvalonia.Features.Overview;

/// <summary>
/// Adapts the established MainVM, ProductsDisplay, ProcessQueue, library-statistics, and
/// Current Flight owners into one immutable overview snapshot. It never opens a second
/// database context and never mutates domain state.
/// </summary>
public sealed class MainDashboardDataSource : IDashboardDataSource
{
	private readonly MainVM main;
	private readonly IFlightService flight;
	private readonly ProcessingViewModel processing;
	private readonly IDashboardSupplementSource? supplementSource;
	private readonly object observedQueueSync = new();
	private readonly HashSet<ProcessBookViewModel> observedQueueItems = [];
	private readonly object aggregateCacheSync = new();
	private LibraryCommands.LibraryStats? cachedStats;
	private LibraryAggregate? cachedLibrary;
	private bool disposed;

	public MainDashboardDataSource(
		MainVM main,
		IFlightService flight,
		ProcessingViewModel processing,
		IDashboardSupplementSource? supplementSource = null)
	{
		this.main = main ?? throw new ArgumentNullException(nameof(main));
		this.flight = flight ?? throw new ArgumentNullException(nameof(flight));
		this.processing = processing ?? throw new ArgumentNullException(nameof(processing));
		this.supplementSource = supplementSource;

		main.PropertyChanged += Main_PropertyChanged;
		main.ProductsDisplay.VisibleCountChanged += ProductsDisplay_VisibleCountChanged;
		processing.Source.PropertyChanged += ProcessQueue_PropertyChanged;
		processing.Source.Queue.CollectionChanged += Queue_CollectionChanged;
		processing.Source.ProcessStart += ProcessQueue_ProcessChanged;
		processing.Source.ProcessEnd += ProcessQueue_ProcessChanged;
		flight.SelectionChanged += Flight_Changed;
		if (supplementSource is not null)
			supplementSource.Invalidated += SupplementSource_Invalidated;

		SynchronizeQueueItemSubscriptions();
	}

	public event EventHandler? Invalidated;

	public async Task<DashboardSnapshot> LoadAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(disposed, this);
		cancellationToken.ThrowIfCancellationRequested();

		var supplementTask = supplementSource?.LoadAsync(cancellationToken)
			?? Task.FromResult(DashboardSupplement.Unknown);
		var raw = await CaptureAsync();
		cancellationToken.ThrowIfCancellationRequested();

		var snapshotTask = Task.Run(() => Aggregate(raw, cancellationToken), cancellationToken);
		await Task.WhenAll(snapshotTask, supplementTask);
		return (await snapshotTask) with { Supplement = await supplementTask };
	}

	private async Task<RawState> CaptureAsync()
	{
		if (Dispatcher.UIThread.CheckAccess())
			return Capture();
		return await Dispatcher.UIThread.InvokeAsync(Capture);
	}

	private RawState Capture()
	{
		var stats = main.LibraryStats;
		var library = stats?.LibraryBooks.ToArray() ?? [];
		var visibleLibrary = main.ProductsDisplay.GetVisibleBookEntries().ToArray();
		var queue = processing.Source.Queue.GetAllItems().Select(QueueRaw.From).ToArray();
		var currentFlight = flight.Items.Select(FlightRaw.From).ToArray();

		return new(
			stats,
			library,
			visibleLibrary,
			queue,
			currentFlight,
			processing.Source.ErrorCount,
			main.AccountsCount,
			main.ActivelyScanning,
			main.ScanningText,
			main.ProductsDisplay.FilterString ?? string.Empty);
	}

	private DashboardSnapshot Aggregate(RawState raw, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var library = raw.Stats is null
			? LibraryAggregate.Empty
			: GetLibraryAggregate(raw.Stats, raw.Library, cancellationToken);

		var visibleTitleCount = raw.VisibleLibrary.WithoutParents().Count();
		var queueByProductId = raw.Queue
			.Where(item => !string.IsNullOrWhiteSpace(item.ProductId))
			.GroupBy(item => item.ProductId, StringComparer.Ordinal)
			.ToDictionary(
				group => group.Key,
				group => group.OrderBy(item => item.Status switch
				{
					ProcessBookStatus.Working => 0,
					ProcessBookStatus.Queued => 1,
					_ => 2,
				}).First(),
				StringComparer.Ordinal);
		var currentFlight = raw.Flight
			.Select(item => ToFlightItem(item, queueByProductId.GetValueOrDefault(item.ProductId)))
			.ToArray();

		var stats = raw.Stats;
		return new()
		{
			IsDataReady = stats is not null,
			TotalTitles = stats is null ? 0 : stats.booksFullyBackedUp + stats.booksDownloadedOnly + stats.booksNoProgress + stats.booksError + stats.booksUnavailable,
			VisibleTitles = visibleTitleCount,
			DownloadPendingCount = stats?.PendingBooks ?? 0,
			CompletedCount = stats?.booksFullyBackedUp ?? 0,
			FailedJobCount = Math.Max(raw.FailedJobCount, raw.Queue.Count(item => item.Status == ProcessBookStatus.Failed)),
			AddedThisWeekCount = library.AddedThisWeekCount,
			ActiveDownloadCount = raw.Queue.Count(item => item.Status == ProcessBookStatus.Working && item.IncludesBookDownload),
			AccountCount = raw.AccountCount,
			IsScanning = raw.IsScanning,
			ScanProgressText = raw.ScanProgressText,
			SearchText = raw.SearchText,
			RecentAdditions = library.RecentAdditions,
			RecentCompletions = library.RecentCompletions,
			CurrentFlight = currentFlight,
		};
	}

	private LibraryAggregate GetLibraryAggregate(
		LibraryCommands.LibraryStats stats,
		IReadOnlyList<LibraryBook> library,
		CancellationToken cancellationToken)
	{
		lock (aggregateCacheSync)
		{
			if (ReferenceEquals(cachedStats, stats) && cachedLibrary is not null)
				return cachedLibrary;
		}

		var built = LibraryAggregate.Build(library, cancellationToken);
		lock (aggregateCacheSync)
		{
			cachedStats = stats;
			cachedLibrary = built;
			return built;
		}
	}

	private static DashboardBookItem ToFlightItem(FlightRaw item, QueueRaw? queue)
		=> new(
			item.LibraryBook,
			item.ProductId,
			item.Title,
			JoinSupportingText(item.Author, item.Narrator),
			item.Author,
			item.Narrator,
			FormatDuration(item.DurationMinutes),
			FormatAddedDate(item.LibraryBook.DateAdded),
			FormatDuration(item.DurationMinutes),
			item.IsAvailable ? LibationStatusKind.DownloadPending : LibationStatusKind.Unavailable,
			item.IsAvailable ? "Ready to process" : "Unavailable after the latest scan")
		{
			ProcessingProgress = queue is null ? 0 : Math.Clamp(queue.Progress, 0, 100),
			ShowProcessingProgress = queue?.Status == ProcessBookStatus.Working,
			ProcessingStatusText = queue?.StatusText,
		};

	private static string FormatDuration(int minutes)
	{
		if (minutes <= 0)
			return string.Empty;
		int hours = minutes / 60;
		int remainingMinutes = minutes % 60;
		return hours == 0 ? $"{remainingMinutes}m" : $"{hours}h {remainingMinutes}m";
	}

	private static string FormatAddedDate(DateTime added)
		=> added == default ? string.Empty : added.ToString("d", CultureInfo.CurrentCulture);

	private static string JoinSupportingText(string author, string narrator)
	{
		if (string.IsNullOrWhiteSpace(narrator))
			return author;
		if (string.IsNullOrWhiteSpace(author))
			return $"Narrated by {narrator}";
		return $"{author} · Narrated by {narrator}";
	}

	private void Main_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (string.IsNullOrEmpty(e.PropertyName)
			|| e.PropertyName is nameof(MainVM.LibraryStats)
				or nameof(MainVM.AccountsCount)
				or nameof(MainVM.AnyAccounts)
				or nameof(MainVM.ActivelyScanning)
				or nameof(MainVM.ScanningText))
			OnInvalidated();
	}

	private void ProductsDisplay_VisibleCountChanged(object? sender, int e) => OnInvalidated();
	private void ProcessQueue_PropertyChanged(object? sender, PropertyChangedEventArgs e) => OnInvalidated();
	private void Flight_Changed(object? sender, FlightChangedEventArgs e) => OnInvalidated();
	private void SupplementSource_Invalidated(object? sender, EventArgs e) => OnInvalidated();
	private void ProcessQueue_ProcessChanged(object? sender, ProcessBookViewModel e) => OnInvalidated();

	private void Queue_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		SynchronizeQueueItemSubscriptions();
		OnInvalidated();
	}

	private void QueueItem_PropertyChanged(object? sender, PropertyChangedEventArgs e) => OnInvalidated();

	private void SynchronizeQueueItemSubscriptions()
	{
		var current = processing.Source.Queue.GetAllItems().ToHashSet();
		lock (observedQueueSync)
		{
			foreach (var removed in observedQueueItems.Except(current).ToArray())
			{
				removed.PropertyChanged -= QueueItem_PropertyChanged;
				observedQueueItems.Remove(removed);
			}
			foreach (var added in current.Except(observedQueueItems))
			{
				added.PropertyChanged += QueueItem_PropertyChanged;
				observedQueueItems.Add(added);
			}
		}
	}

	private void OnInvalidated()
	{
		if (!disposed)
			Invalidated?.Invoke(this, EventArgs.Empty);
	}

	public void Dispose()
	{
		if (disposed)
			return;
		disposed = true;

		main.PropertyChanged -= Main_PropertyChanged;
		main.ProductsDisplay.VisibleCountChanged -= ProductsDisplay_VisibleCountChanged;
		processing.Source.PropertyChanged -= ProcessQueue_PropertyChanged;
		processing.Source.Queue.CollectionChanged -= Queue_CollectionChanged;
		processing.Source.ProcessStart -= ProcessQueue_ProcessChanged;
		processing.Source.ProcessEnd -= ProcessQueue_ProcessChanged;
		flight.SelectionChanged -= Flight_Changed;
		if (supplementSource is not null)
			supplementSource.Invalidated -= SupplementSource_Invalidated;
		if (supplementSource is IDisposable disposableSupplement)
			disposableSupplement.Dispose();

		lock (observedQueueSync)
		{
			foreach (var item in observedQueueItems)
				item.PropertyChanged -= QueueItem_PropertyChanged;
			observedQueueItems.Clear();
		}
	}

	private sealed record RawState(
		LibraryCommands.LibraryStats? Stats,
		IReadOnlyList<LibraryBook> Library,
		IReadOnlyList<LibraryBook> VisibleLibrary,
		QueueRaw[] Queue,
		FlightRaw[] Flight,
		int FailedJobCount,
		int AccountCount,
		bool IsScanning,
		string ScanProgressText,
		string SearchText);

	private sealed record QueueRaw(
		string ProductId,
		string Title,
		string Author,
		string Narrator,
		int Progress,
		ProcessBookStatus Status,
		string StatusText,
		bool IncludesBookDownload)
	{
		public static QueueRaw From(ProcessBookViewModel item)
			=> new(
				item.LibraryBook.Book.AudibleProductId,
				item.Title ?? item.LibraryBook.Book.TitleWithSubtitle,
				item.Author ?? string.Empty,
				item.Narrator ?? string.Empty,
				item.Progress,
				item.Status,
				item.StatusText,
				item.IncludesBookDownload);
	}

	private sealed record FlightRaw(
		LibraryBook LibraryBook,
		string ProductId,
		string Title,
		string Author,
		string Narrator,
		int DurationMinutes,
		bool IsAvailable)
	{
		public static FlightRaw From(FlightItemViewModel item)
			=> new(
				item.LibraryBook,
				item.LibraryBook.Book.AudibleProductId,
				item.Title,
				item.Author,
				item.LibraryBook.Book.NarratorNames,
				item.DurationMinutes,
				item.IsAvailable);
	}

	private enum BookMetadata
	{
		Added,
		Completed,
	}

	private sealed class LibraryAggregate
	{
		public static LibraryAggregate Empty { get; } = new([], [], [], 0);

		private readonly Dictionary<LibraryBook, BookCore> byBook;

		private LibraryAggregate(
			Dictionary<LibraryBook, BookCore> byBook,
			IReadOnlyList<DashboardBookItem> recentAdditions,
			IReadOnlyList<DashboardBookItem> recentCompletions,
			int addedThisWeekCount)
		{
			this.byBook = byBook;
			RecentAdditions = recentAdditions;
			RecentCompletions = recentCompletions;
			AddedThisWeekCount = addedThisWeekCount;
		}

		public IReadOnlyList<DashboardBookItem> RecentAdditions { get; }
		public IReadOnlyList<DashboardBookItem> RecentCompletions { get; }
		public int AddedThisWeekCount { get; }

		public static LibraryAggregate Build(IReadOnlyList<LibraryBook> library, CancellationToken cancellationToken)
		{
			var books = library.WithoutParents().ToArray();
			var byBook = new Dictionary<LibraryBook, BookCore>();
			foreach (var book in books)
			{
				cancellationToken.ThrowIfCancellationRequested();
				byBook[book] = BookCore.From(book);
			}

			var aggregate = new LibraryAggregate(byBook, [], [], 0);
			var recentAdditions = books
				.OrderByDescending(book => book.DateAdded)
				.Take(10)
				.Select(book => aggregate.ToBookItem(book, BookMetadata.Added))
				.ToArray();
			var recentCompletions = books
				.Where(book => book.Book.UserDefinedItem.LastDownloaded.HasValue)
				.OrderByDescending(book => book.Book.UserDefinedItem.LastDownloaded)
				.Take(10)
				.Select(book => aggregate.ToBookItem(book, BookMetadata.Completed))
				.ToArray();
			var today = DateTime.Today;
			var firstDayOfWeek = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
			int daysSinceWeekStart = (7 + (int)today.DayOfWeek - (int)firstDayOfWeek) % 7;
			var weekStart = today.AddDays(-daysSinceWeekStart);
			int addedThisWeekCount = books.Count(book => book.DateAdded >= weekStart);
			return new(byBook, recentAdditions, recentCompletions, addedThisWeekCount);
		}

		public DashboardBookItem ToBookItem(LibraryBook book, BookMetadata metadata)
		{
			if (!byBook.TryGetValue(book, out var core))
				core = BookCore.From(book);
			var eventDate = metadata == BookMetadata.Completed
				? book.Book.UserDefinedItem.LastDownloaded
				: book.DateAdded;
			string eventLabel = metadata == BookMetadata.Completed ? "Completed" : "Added";
			string eventText = eventDate is null
				? string.Empty
				: $"{eventLabel} {eventDate.Value.ToString("d", CultureInfo.CurrentCulture)}";
			string metadataText = string.Join(" · ", new[] { core.Duration, eventText }.Where(text => !string.IsNullOrWhiteSpace(text)));
			return new(
				book,
				book.Book.AudibleProductId,
				core.Title,
				core.SupportingText,
				core.Author,
				core.Narrator,
				core.Duration,
				FormatAddedDate(book.DateAdded),
				metadataText,
				metadata == BookMetadata.Completed ? LibationStatusKind.Completed : core.Status,
				metadata == BookMetadata.Completed ? "Completed" : core.StatusText);
		}
	}

	private sealed record BookCore(
		string Title,
		string SupportingText,
		string Author,
		string Narrator,
		string Duration,
		LibationStatusKind Status,
		string StatusText)
	{
		public static BookCore From(LibraryBook book)
		{
			var liberation = LibraryCommands.Liberated_Status(book.Book);
			var status = book.AbsentFromLastScan && liberation is not LiberatedStatus.Liberated
				? LibationStatusKind.Unavailable
				: liberation switch
				{
					LiberatedStatus.Liberated => LibationStatusKind.Completed,
					LiberatedStatus.PartialDownload => LibationStatusKind.Downloaded,
					LiberatedStatus.Error => LibationStatusKind.Failed,
					_ => LibationStatusKind.DownloadPending,
				};
			string statusText = status switch
			{
				LibationStatusKind.Unavailable => "Unavailable after the latest scan",
				LibationStatusKind.Completed => "Completed",
				LibationStatusKind.Downloaded => "Downloaded; processing pending",
				LibationStatusKind.Failed => "Needs attention",
				_ => "Download pending",
			};
			string author = book.Book.AuthorNames;
			string narrator = book.Book.NarratorNames;
			return new(
				book.Book.TitleWithSubtitle,
				JoinSupportingText(author, narrator),
				author,
				narrator,
				FormatDuration(book.Book.LengthInMinutes),
				status,
				statusText);
		}
	}
}
