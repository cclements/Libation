using ApplicationServices;
using DataLayer;
using Dinah.Core;
using LibationAvalonia.DesignSystem.Components;
using LibationAvalonia.ViewModels;
using LibationFileManager;
using LibationUiBase.ProcessQueue;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Windows.Input;

namespace LibationAvalonia.Features.Downloads;

public enum DownloadsSectionKind
{
	DownloadPending,
	Downloading,
	Downloaded,
	Unavailable,
}

internal readonly record struct DownloadBookKey(string Account, string ProductId)
{
	public static DownloadBookKey From(LibraryBook book)
		=> new(book.Account ?? string.Empty, book.Book.AudibleProductId);
}

/// <summary>
/// One row in the Downloads route's single virtualized surface. Section rows and
/// book rows share a carrier so the four groups never become nested item controls.
/// </summary>
public sealed class DownloadsListRowViewModel : ViewModelBase
{
	private DownloadsListRowViewModel(string? sectionTitle, DownloadBookItemViewModel? book)
	{
		SectionTitle = sectionTitle;
		Book = book;
	}

	public string? SectionTitle { get; }
	public int SectionCount { get => field; private set => this.RaiseAndSetIfChanged(ref field, value); }
	public DownloadBookItemViewModel? Book { get; }
	public bool IsSectionHeader => Book is null;
	public bool IsBookRow => Book is not null;
	public string SectionText => $"{SectionTitle} ({SectionCount:N0})";

	internal void UpdateSectionCount(int count)
	{
		if (SectionCount == count)
			return;
		SectionCount = count;
		this.RaisePropertyChanged(nameof(SectionText));
	}

	public static DownloadsListRowViewModel Section(string title) => new(title, null);
	public static DownloadsListRowViewModel BookRow(DownloadBookItemViewModel book) => new(null, book);
}

/// <summary>
/// Presentation-only projection of one library title and, when present, its
/// existing queue item. It never owns download or processing state.
/// </summary>
public sealed class DownloadBookItemViewModel : ReactiveObject, IDisposable
{
	private readonly DownloadsViewModel owner;
	private readonly ReactiveCommand<Unit, Unit> primaryCommand;
	private readonly ReactiveCommand<Unit, Unit> locateCommand;
	private LibraryBook book;
	private ProcessBookViewModel? queueItem;
	private LiberatedStatus libraryStatus;
	private long? knownSizeBytes;
	private bool disposed;

	internal DownloadBookItemViewModel(
		DownloadsViewModel owner,
		LibraryBook book,
		LiberatedStatus libraryStatus,
		long? knownSizeBytes)
	{
		this.owner = owner;
		this.book = book;
		this.libraryStatus = libraryStatus;
		this.knownSizeBytes = knownSizeBytes;
		primaryCommand = ReactiveCommand.CreateFromTask(RunPrimaryAsync);
		locateCommand = ReactiveCommand.CreateFromTask(() => owner.LocateBookAsync(Book));
	}

	internal event EventHandler? MembershipChanged;
	internal DownloadBookKey Key => DownloadBookKey.From(Book);
	internal ProcessBookViewModel? QueueItem => queueItem;

	public LibraryBook Book => book;
	public string Title => string.IsNullOrWhiteSpace(Book.Book.TitleWithSubtitle) ? "Untitled audiobook" : Book.Book.TitleWithSubtitle;
	public string SupportingText => string.IsNullOrWhiteSpace(Book.Book.AuthorNames) ? "Unknown author" : Book.Book.AuthorNames;
	public string MaskedAccount => string.IsNullOrWhiteSpace(Book.Account) ? "Account unavailable" : $"Account {Book.Account.ToMask()}";
	public string Marketplace => string.IsNullOrWhiteSpace(Book.Book.Locale) ? "Marketplace unavailable" : $"Marketplace {Book.Book.Locale}";
	public string? QualityText => Book.Book.UserDefinedItem.LastDownloadedFormat is { IsDefault: false } format
		? format.BitRate > 0 ? $"{format.CodecString} {format.BitRate:N0} kbps" : format.CodecString
		: null;
	public string? SizeText => knownSizeBytes is long bytes ? DiskSpaceHelper.FormatBytes(bytes) : null;
	public string Metadata => string.Join(" · ", new[] { MaskedAccount, Marketplace, QualityText, SizeText }
		.Where(value => !string.IsNullOrWhiteSpace(value)));

	public DownloadsSectionKind Section => DetermineSection();
	public LibationStatusKind Status => DetermineStatus();
	public string StatusText => DetermineStatusText();
	public string RowAccessibleName => $"{Title}, {StatusText}, {Metadata}";
	public string StatusAccessibleName => $"{Title}: {StatusText}";
	public bool ShowProgress => QueueItem?.Status is ProcessBookStatus.Working;
	public double Progress => QueueItem?.Progress ?? 0;
	public string ProgressAccessibleName => $"{Title} progress {Progress:0} percent";

	public bool CanRetry => QueueItem is { Status: ProcessBookStatus.Failed } item
		&& item.LastPresentationStage switch
		{
			ProcessBookPresentationStage.Downloading => item.IncludesPdfDownload && item.LibraryBook.NeedsPdfDownload,
			ProcessBookPresentationStage.Decrypting => item.IncludesBookDownload && item.LibraryBook.NeedsBookDownload,
			_ => false,
		};

	public bool CanStart => QueueItem?.Status is not (ProcessBookStatus.Queued or ProcessBookStatus.Working)
		&& Book.NeedsBookDownload
		&& Section is DownloadsSectionKind.DownloadPending or DownloadsSectionKind.Downloaded;
	public string? PrimaryActionText => CanRetry ? "Retry" : CanStart
		? Section == DownloadsSectionKind.Downloaded ? "Process" : "Download"
		: null;
	public ICommand? PrimaryCommand => PrimaryActionText is null ? null : primaryCommand;
	public string PrimaryAccessibleName => $"{PrimaryActionText ?? "Process"} {Title}";
	public ICommand LocateCommand => locateCommand;
	public string LocateAccessibleName => $"Locate an existing file for {Title}";

	internal void UpdateLibrary(LibraryBook replacement, LiberatedStatus status, long? sizeBytes)
	{
		book = replacement;
		libraryStatus = status;
		knownSizeBytes = sizeBytes;
		RaiseAll();
	}

	internal void UpdateQueue(ProcessBookViewModel? replacement)
	{
		if (ReferenceEquals(queueItem, replacement))
			return;
		if (queueItem is not null)
			queueItem.PropertyChanged -= QueueItem_PropertyChanged;
		queueItem = replacement;
		if (queueItem is not null)
			queueItem.PropertyChanged += QueueItem_PropertyChanged;
		RaiseQueueState();
	}

	private DownloadsSectionKind DetermineSection()
	{
		if (QueueItem?.Status is ProcessBookStatus.Failed
			|| libraryStatus is LiberatedStatus.Error
			|| Book.AbsentFromLastScan && libraryStatus is LiberatedStatus.NotLiberated or LiberatedStatus.PartialDownload)
			return DownloadsSectionKind.Unavailable;
		if (QueueItem is
			{
				IncludesBookDownload: true,
				Status: ProcessBookStatus.Queued or ProcessBookStatus.Working,
				PresentationStage: not ProcessBookPresentationStage.Converting,
			})
			return DownloadsSectionKind.Downloading;
		if (QueueItem is { Status: ProcessBookStatus.Working, PresentationStage: ProcessBookPresentationStage.Converting })
			return DownloadsSectionKind.Downloaded;
		return libraryStatus is LiberatedStatus.PartialDownload or LiberatedStatus.Liberated
			? DownloadsSectionKind.Downloaded
			: DownloadsSectionKind.DownloadPending;
	}

	private LibationStatusKind DetermineStatus()
	{
		if (QueueItem is { } item)
		{
			if (item.Status is ProcessBookStatus.Failed)
				return LibationStatusKind.Failed;
			if (item.Status is ProcessBookStatus.Cancelled)
				return LibationStatusKind.Cancelled;
			if (item.Status is ProcessBookStatus.Queued)
				return LibationStatusKind.DownloadPending;
			if (item.Status is ProcessBookStatus.Working)
				return item.PresentationStage == ProcessBookPresentationStage.Downloading
					? LibationStatusKind.Downloading
					: LibationStatusKind.Processing;
		}

		if (Book.AbsentFromLastScan && libraryStatus is LiberatedStatus.NotLiberated or LiberatedStatus.PartialDownload)
			return LibationStatusKind.Unavailable;
		return libraryStatus switch
		{
			LiberatedStatus.PartialDownload => LibationStatusKind.Downloaded,
			LiberatedStatus.Liberated => LibationStatusKind.Completed,
			LiberatedStatus.Error => LibationStatusKind.NeedsAttention,
			_ => LibationStatusKind.DownloadPending,
		};
	}

	private string DetermineStatusText()
	{
		if (QueueItem is { } item)
		{
			if (item.Status is ProcessBookStatus.Queued)
				return "Queued";
			if (item.Status is ProcessBookStatus.Working)
				return item.PresentationStage switch
				{
					ProcessBookPresentationStage.Downloading => "Downloading",
					ProcessBookPresentationStage.Decrypting => "Decrypting",
					ProcessBookPresentationStage.Converting => "Converting",
					_ => item.StatusText,
				};
			if (item.Status is ProcessBookStatus.Failed or ProcessBookStatus.Cancelled)
				return item.StatusText;
		}

		if (Book.AbsentFromLastScan)
			return "Unavailable";
		return libraryStatus switch
		{
			LiberatedStatus.PartialDownload => "Audible file downloaded",
			LiberatedStatus.Liberated => "Processed open copy",
			LiberatedStatus.Error => "Needs attention",
			_ => "Download pending",
		};
	}

	private async System.Threading.Tasks.Task RunPrimaryAsync()
	{
		if (CanRetry && QueueItem is { } item)
			await owner.RetryBookAsync(item);
		else if (CanStart)
			await owner.QueueBookAsync(Book);
	}

	private void QueueItem_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		RaiseQueueState();
		if (e.PropertyName is null
			or nameof(ProcessBookViewModel.Status)
			or nameof(ProcessBookViewModel.Result)
			or nameof(ProcessBookViewModel.PresentationStage)
			or nameof(ProcessBookViewModel.LastPresentationStage))
			MembershipChanged?.Invoke(this, EventArgs.Empty);
	}

	private void RaiseQueueState()
	{
		foreach (var property in new[]
		{
			nameof(Section), nameof(Status), nameof(StatusText), nameof(RowAccessibleName), nameof(StatusAccessibleName),
			nameof(ShowProgress), nameof(Progress), nameof(ProgressAccessibleName), nameof(CanRetry), nameof(CanStart),
			nameof(PrimaryActionText), nameof(PrimaryCommand), nameof(PrimaryAccessibleName),
		})
			this.RaisePropertyChanged(property);
	}

	private void RaiseAll()
	{
		foreach (var property in new[]
		{
			nameof(Book), nameof(Title), nameof(SupportingText), nameof(MaskedAccount), nameof(Marketplace),
			nameof(QualityText), nameof(SizeText), nameof(Metadata), nameof(LocateAccessibleName),
		})
			this.RaisePropertyChanged(property);
		RaiseQueueState();
	}

	public void Dispose()
	{
		if (disposed)
			return;
		disposed = true;
		if (queueItem is not null)
			queueItem.PropertyChanged -= QueueItem_PropertyChanged;
		primaryCommand.Dispose();
		locateCommand.Dispose();
	}
}
