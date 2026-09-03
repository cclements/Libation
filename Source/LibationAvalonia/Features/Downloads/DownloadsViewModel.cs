using ApplicationServices;
using Avalonia.Threading;
using DataLayer;
using LibationAvalonia.DesignSystem.Components;
using LibationAvalonia.Features.Tools;
using LibationAvalonia.Properties;
using LibationAvalonia.Shell;
using LibationAvalonia.ViewModels;
using LibationFileManager;
using LibationUiBase.ProcessQueue;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace LibationAvalonia.Features.Downloads;

/// <summary>
/// Sectioned presentation over the existing filesystem-aware LibraryStats and
/// ProcessQueue. It never creates a downloader, queue, or persisted status.
/// </summary>
public sealed class DownloadsViewModel : SecondaryDestinationViewModel, IRoutePresentation
{
	private static readonly DownloadsSectionKind[] SectionOrder =
	[
		DownloadsSectionKind.DownloadPending,
		DownloadsSectionKind.Downloading,
		DownloadsSectionKind.Downloaded,
		DownloadsSectionKind.Unavailable,
	];

	private readonly ILibationCommandAdapter commands;
	private readonly MainVM main;
	private readonly Dictionary<DownloadBookKey, DownloadBookItemViewModel> books = new();
	private readonly Dictionary<DownloadBookKey, DownloadsListRowViewModel> bookRows = new();
	private readonly IReadOnlyDictionary<DownloadsSectionKind, DownloadsListRowViewModel> sectionRows;
	private CancellationTokenSource projectionCancellation = new();
	private bool projectionRunning;
	private bool queueRefreshPending;
	private bool disposed;

	public DownloadsViewModel(ILibationCommandAdapter commands)
	{
		ArgumentNullException.ThrowIfNull(commands);
		this.commands = commands;
		main = commands.Main;
		sectionRows = SectionOrder.ToDictionary(
			section => section,
			section => DownloadsListRowViewModel.Section(SectionTitle(section)));
		DownloadPendingCommand = CreateOwnerCommand(
			commands.DownloadPendingBooksAsync,
			global::LibationAvalonia.Properties.Resources.DownloadsViewModelDownloadAndProcessPendingAudiobooks,
			global::LibationAvalonia.Properties.Resources.DownloadsViewModelLibationCouldNotStartThePendingAudiobook);
		DownloadPdfsCommand = CreateOwnerCommand(
			commands.DownloadPendingPdfsAsync,
			global::LibationAvalonia.Properties.Resources.DownloadsViewModelDownloadPendingPDFSupplements,
			global::LibationAvalonia.Properties.Resources.DownloadsViewModelLibationCouldNotStartPendingPDFDownloads);
		LocateFilesCommand = CreateOwnerCommand(
			commands.LocateAudiobooksAsync,
			global::LibationAvalonia.Properties.Resources.DownloadsViewModelLocateExistingAudiobookFiles,
			global::LibationAvalonia.Properties.Resources.DownloadsViewModelLibationCouldNotOpenTheAudiobookLocator);
		ConvertAllToMp3Command = CreateOwnerCommand(
			commands.ConvertLibraryToMp3Async,
			global::LibationAvalonia.Properties.Resources.DownloadsViewModelConvertTheLibraryToMP3,
			global::LibationAvalonia.Properties.Resources.DownloadsViewModelLibationCouldNotStartTheMP3Conversion);
		RefreshCommand = CreateOwnerCommand(
			async () =>
			{
				await main.SetBackupCountsAsync(null);
				RequestLibraryProjection();
			},
			global::LibationAvalonia.Properties.Resources.DownloadsViewModelRefreshDownloadState,
			Resources.DownloadsRefreshUnchangedError);
		OpenAccountsCommand = CreateOwnerCommand(
			commands.ShowAccountsAsync,
			global::LibationAvalonia.Properties.Resources.DownloadsViewModelOpenAccountManagementFromDownloads,
			global::LibationAvalonia.Properties.Resources.AccountsViewModelLibationCouldNotOpenAccountManagementNo);
		ScanLibraryCommand = CreateOwnerCommand(
			commands.ScanLibraryAsync,
			global::LibationAvalonia.Properties.Resources.DownloadsViewModelScanAccountsFromDownloads,
			global::LibationAvalonia.Properties.Resources.DownloadsViewModelLibationCouldNotStartTheLibraryScan);

		main.PropertyChanged += Main_PropertyChanged;
		main.ProcessQueue.Queue.CollectionChanged += Queue_CollectionChanged;
		RequestLibraryProjection();
	}

	private LibraryCommands.LibraryStats? Stats => main.LibraryStats;
	public ObservableCollection<DownloadsListRowViewModel> Rows { get; } = new();
	public bool IsLoading => Stats is null || projectionRunning && Rows.Count == 0;
	public bool IsReady => Stats is not null && !projectionRunning;
	public bool HasLibraryTitles => books.Count > 0;
	public bool ShowEmptyLibrary => IsReady && !HasLibraryTitles;
	public string EmptyLibraryExplanation => main.AnyAccounts
		? main.ActivelyScanning
			? Resources.DownloadsEmptyScanningExplanation
			: global::LibationAvalonia.Properties.Resources.DownloadsViewModelScanTheConnectedAccountsToCatalogueTitles
		: global::LibationAvalonia.Properties.Resources.DownloadsViewModelAddAnAudibleAccountThenScanIt;
	public string EmptyLibraryActionText => main.AnyAccounts && !main.ActivelyScanning ? global::LibationAvalonia.Properties.Resources.DownloadsViewModelScanAccounts : global::LibationAvalonia.Properties.Resources.OnboardingViewManageAccounts;
	public ICommand EmptyLibraryCommand => main.AnyAccounts && !main.ActivelyScanning ? ScanLibraryCommand : OpenAccountsCommand;

	public int PendingCount => books.Values.Count(book => book.Section == DownloadsSectionKind.DownloadPending);
	public int DownloadingCount => books.Values.Count(book => book.Section == DownloadsSectionKind.Downloading);
	public int DownloadedCount => books.Values.Count(book => book.Section == DownloadsSectionKind.Downloaded);
	public int UnavailableCount => books.Values.Count(book => book.Section == DownloadsSectionKind.Unavailable);
	public string ResultSummary
		=> string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.DownloadsViewModel0N0Pending1N0Downloading2, PendingCount, DownloadingCount, DownloadedCount, UnavailableCount);

	public ICommand DownloadPendingCommand { get; }
	public ICommand DownloadPdfsCommand { get; }
	public ICommand LocateFilesCommand { get; }
	public ICommand ConvertAllToMp3Command { get; }
	public ICommand RefreshCommand { get; }
	public ICommand OpenAccountsCommand { get; }
	public ICommand ScanLibraryCommand { get; }
	public string RouteEyebrow => global::LibationAvalonia.Properties.Resources.DownloadsViewModelAcquisition;
	public string RouteTitle => global::LibationAvalonia.Properties.Resources.RouteDownloadsLabel;
	public string RouteSubtitle => global::LibationAvalonia.Properties.Resources.DownloadsViewModelSeeWhatIsPendingDownloadingDownloadedOr;
	public RouteCommandPresentation RoutePrimaryCommand => new(global::LibationAvalonia.Properties.Resources.CellarOverviewViewDownloadPendingTitles, DownloadPendingCommand);
	public IReadOnlyList<RouteCommandPresentation> RouteSecondaryCommands =>
	[
		new(global::LibationAvalonia.Properties.Resources.DownloadsViewModelDownloadPDFs, DownloadPdfsCommand),
		new(global::LibationAvalonia.Properties.Resources.DownloadsViewModelLocateFiles, LocateFilesCommand),
		new(global::LibationAvalonia.Properties.Resources.DownloadsViewModelConvertToMP3, ConvertAllToMp3Command),
		new(global::LibationAvalonia.Properties.Resources.DownloadsViewRefresh, RefreshCommand),
	];
	public RouteStatusPresentation RouteStatusBadge => new(ResultSummary,
		UnavailableCount > 0 ? LibationStatusKind.NeedsAttention
		: DownloadingCount > 0 ? LibationStatusKind.Downloading
		: PendingCount > 0 ? LibationStatusKind.DownloadPending
		: LibationStatusKind.Completed);

	internal Task QueueBookAsync(LibraryBook book)
		=> RunOwnerActionAsync(
			async () => await main.QueueBooksAsync([book], Configuration.Instance),
			string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.DownloadsViewModelStartTheDownloadWorkflowFor0, book.Book.AudibleProductId),
			global::LibationAvalonia.Properties.Resources.DownloadsViewModelLibationCouldNotStartThisTitleReview);

	internal Task RetryBookAsync(ProcessBookViewModel item)
		=> RunOwnerActionAsync(
			async () => await main.QueueBooksAsync([item.LibraryBook], item.Configuration),
			string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.DownloadsViewModelRetryTheDownloadWorkflowFor0, item.LibraryBook.Book.AudibleProductId),
			global::LibationAvalonia.Properties.Resources.DownloadsViewModelLibationCouldNotRetryThisTitleReview);

	internal Task LocateBookAsync(LibraryBook book)
		=> RunOwnerActionAsync(
			() => commands.LocateAudiobookAsync(book),
			string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.DownloadsViewModelLocateAnExistingFileFor0, book.Book.AudibleProductId),
			global::LibationAvalonia.Properties.Resources.DownloadsViewModelLibationCouldNotOpenTheFilePicker);

	private void Main_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(MainVM.LibraryStats))
			RequestLibraryProjection();
		if (string.IsNullOrEmpty(e.PropertyName)
			|| e.PropertyName is nameof(MainVM.LibraryStats) or nameof(MainVM.AnyAccounts) or nameof(MainVM.ActivelyScanning))
			RaiseState();
	}

	private void Queue_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RequestQueueProjection();
	private void Book_MembershipChanged(object? sender, EventArgs e) => RequestQueueProjection();

	private void RequestLibraryProjection()
	{
		if (disposed)
			return;
		if (!Dispatcher.UIThread.CheckAccess())
		{
			Dispatcher.UIThread.Post(RequestLibraryProjection);
			return;
		}

		var stats = Stats;
		projectionCancellation.Cancel();
		projectionCancellation.Dispose();
		projectionCancellation = new CancellationTokenSource();
		if (stats is null)
		{
			projectionRunning = false;
			ClearBooks();
			RaiseState();
			return;
		}

		var library = stats.LibraryBooks.WithoutParents().ToArray();
		projectionRunning = true;
		RaiseState();
		_ = ProjectLibraryAsync(stats, library, projectionCancellation.Token);
	}

	private async Task ProjectLibraryAsync(
		LibraryCommands.LibraryStats stats,
		IReadOnlyList<LibraryBook> library,
		CancellationToken cancellationToken)
	{
		try
		{
			var snapshots = await Task.Run(() => library.Select(book => ProjectBook(book, cancellationToken)).ToArray(), cancellationToken);
			await Dispatcher.UIThread.InvokeAsync(() => ApplyLibraryProjection(stats, snapshots, cancellationToken));
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
		catch (Exception ex)
		{
			await Dispatcher.UIThread.InvokeAsync(() =>
			{
				if (cancellationToken.IsCancellationRequested || disposed)
					return;
				projectionRunning = false;
				CurrentError = LibationAvalonia.DesignSystem.UserFacingErrorFactory.FromException(
					ex,
					global::LibationAvalonia.Properties.Resources.DownloadsViewModelBuildTheDownloadsTitleList,
					global::LibationAvalonia.Properties.Resources.DownloadsViewModelLibationCouldNotReadTheCurrentPer);
				RaiseState();
			});
		}
	}

	private static DownloadBookSnapshot ProjectBook(LibraryBook book, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var status = LibraryCommands.Liberated_Status(book.Book);
		var path = status switch
		{
			LiberatedStatus.PartialDownload => FilePathCache.GetFirstPath(book.Book.AudibleProductId, FileType.AAXC)?.Path,
			LiberatedStatus.Liberated => AudibleFileStorage.Audio.GetPath(book.Book.AudibleProductId)?.Path,
			_ => null,
		};
		var file = path is null ? null : new FileInfo(path);
		long? bytes = file?.Exists == true
			? file.Length
			: null;
		return new(book, status, bytes);
	}

	private void ApplyLibraryProjection(
		LibraryCommands.LibraryStats stats,
		IReadOnlyList<DownloadBookSnapshot> snapshots,
		CancellationToken cancellationToken)
	{
		if (disposed || cancellationToken.IsCancellationRequested || !ReferenceEquals(Stats, stats))
			return;

		var retained = new HashSet<DownloadBookKey>();
		foreach (var snapshot in snapshots)
		{
			var key = DownloadBookKey.From(snapshot.Book);
			retained.Add(key);
			if (!books.TryGetValue(key, out var item))
			{
				item = new DownloadBookItemViewModel(this, snapshot.Book, snapshot.Status, snapshot.KnownSizeBytes);
				item.MembershipChanged += Book_MembershipChanged;
				books.Add(key, item);
				bookRows.Add(key, DownloadsListRowViewModel.BookRow(item));
			}
			else
				item.UpdateLibrary(snapshot.Book, snapshot.Status, snapshot.KnownSizeBytes);
		}

		foreach (var key in books.Keys.Where(key => !retained.Contains(key)).ToArray())
		{
			var item = books[key];
			item.MembershipChanged -= Book_MembershipChanged;
			item.Dispose();
			books.Remove(key);
			bookRows.Remove(key);
		}

		projectionRunning = false;
		CurrentError = null;
		ApplyQueueProjection();
		RaiseState();
	}

	private void RequestQueueProjection()
	{
		if (disposed || queueRefreshPending)
			return;
		queueRefreshPending = true;
		Dispatcher.UIThread.Post(() =>
		{
			queueRefreshPending = false;
			if (!disposed)
				ApplyQueueProjection();
		}, DispatcherPriority.Background);
	}

	private void ApplyQueueProjection()
	{
		if (!Dispatcher.UIThread.CheckAccess())
		{
			RequestQueueProjection();
			return;
		}

		var queue = main.ProcessQueue.Queue.GetAllItems()
			.GroupBy(item => DownloadBookKey.From(item.LibraryBook))
			.ToDictionary(group => group.Key, group => group.OrderBy(QueuePriority).First());
		foreach (var (key, book) in books)
			book.UpdateQueue(queue.GetValueOrDefault(key));
		RebuildRows();
		RaiseState();
	}

	private void RebuildRows()
	{
		var desired = new List<DownloadsListRowViewModel>(books.Count + SectionOrder.Length);
		foreach (var section in SectionOrder)
		{
			var sectionBooks = books.Values
				.Where(book => book.Section == section)
				.OrderBy(book => book.Title, StringComparer.CurrentCultureIgnoreCase)
				.ToArray();
			var sectionRow = sectionRows[section];
			sectionRow.UpdateSectionCount(sectionBooks.Length);
			desired.Add(sectionRow);
			foreach (var book in sectionBooks)
				desired.Add(bookRows[book.Key]);
		}

		var desiredSet = desired.ToHashSet();
		for (int index = Rows.Count - 1; index >= 0; index--)
			if (!desiredSet.Contains(Rows[index]))
				Rows.RemoveAt(index);
		for (int index = 0; index < desired.Count; index++)
		{
			var row = desired[index];
			if (index < Rows.Count && ReferenceEquals(Rows[index], row))
				continue;
			int currentIndex = Rows.IndexOf(row);
			if (currentIndex >= 0)
				Rows.Move(currentIndex, index);
			else
				Rows.Insert(index, row);
		}
	}

	private static int QueuePriority(ProcessBookViewModel item) => item.Status switch
	{
		ProcessBookStatus.Working => 0,
		ProcessBookStatus.Queued => 1,
		ProcessBookStatus.Failed => 2,
		ProcessBookStatus.Completed => 3,
		_ => 4,
	};

	private static string SectionTitle(DownloadsSectionKind section) => section switch
	{
		DownloadsSectionKind.DownloadPending => global::LibationAvalonia.Properties.Resources.CellarOverviewViewDownloadPending,
		DownloadsSectionKind.Downloading => global::LibationAvalonia.Properties.Resources.DownloadsModelsDownloading,
		DownloadsSectionKind.Downloaded => global::LibationAvalonia.Properties.Resources.DownloadsViewModelDownloaded,
		_ => global::LibationAvalonia.Properties.Resources.DownloadsModelsUnavailable,
	};

	private void RaiseState()
	{
		foreach (var property in new[]
		{
			nameof(IsLoading), nameof(IsReady), nameof(HasLibraryTitles), nameof(ShowEmptyLibrary),
			nameof(EmptyLibraryExplanation), nameof(EmptyLibraryActionText), nameof(EmptyLibraryCommand),
			nameof(PendingCount), nameof(DownloadingCount), nameof(DownloadedCount), nameof(UnavailableCount),
			nameof(ResultSummary), nameof(RouteStatusBadge),
		})
			this.RaisePropertyChanged(property);
	}

	private void ClearBooks()
	{
		foreach (var book in books.Values)
		{
			book.MembershipChanged -= Book_MembershipChanged;
			book.Dispose();
		}
		books.Clear();
		bookRows.Clear();
		Rows.Clear();
	}

	protected override void DisposeCore()
	{
		disposed = true;
		main.PropertyChanged -= Main_PropertyChanged;
		main.ProcessQueue.Queue.CollectionChanged -= Queue_CollectionChanged;
		projectionCancellation.Cancel();
		projectionCancellation.Dispose();
		ClearBooks();
	}

	private sealed record DownloadBookSnapshot(LibraryBook Book, LiberatedStatus Status, long? KnownSizeBytes);
}
