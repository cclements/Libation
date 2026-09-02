using ApplicationServices;
using LibationAvalonia.DesignSystem.Components;
using LibationAvalonia.Features.Tools;
using LibationAvalonia.Properties;
using LibationAvalonia.Shell;
using LibationAvalonia.ViewModels;
using LibationUiBase.ProcessQueue;
using ReactiveUI;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace LibationAvalonia.Features.Downloads;

/// <summary>
/// Explains acquisition state using the existing filesystem-aware LibraryStats and
/// queue. It deliberately does not create a second downloader or infer unavailable
/// account/marketplace/quality facts.
/// </summary>
public sealed class DownloadsViewModel : SecondaryDestinationViewModel, IRoutePresentation
{
	private readonly MainVM main;

	public DownloadsViewModel(ILibationCommandAdapter commands)
	{
		ArgumentNullException.ThrowIfNull(commands);
		main = commands.Main;
		DownloadPendingCommand = CreateOwnerCommand(
			commands.DownloadPendingBooksAsync,
			"download and process pending audiobooks",
			"Libation could not start the pending audiobook workflow. Review account access and the Books location, then try again.");
		DownloadPdfsCommand = CreateOwnerCommand(
			commands.DownloadPendingPdfsAsync,
			"download pending PDF supplements",
			"Libation could not start pending PDF downloads. Review account access and try again.");
		LocateFilesCommand = CreateOwnerCommand(
			commands.LocateAudiobooksAsync,
			"locate existing audiobook files",
			"Libation could not open the audiobook locator. Check the configured Books location and try again.");
		ConvertAllToMp3Command = CreateOwnerCommand(
			commands.ConvertLibraryToMp3Async,
			"convert the library to MP3",
			"Libation could not start the MP3 conversion workflow. Review Processing and the application log.");
		RefreshCommand = CreateOwnerCommand(
			() => main.SetBackupCountsAsync(null),
			"refresh download counts",
			Resources.DownloadsRefreshUnchangedError);
		OpenAccountsCommand = CreateOwnerCommand(
			commands.ShowAccountsAsync,
			"open account management from Downloads",
			"Libation could not open account management. No account data was changed.");
		ScanLibraryCommand = CreateOwnerCommand(
			commands.ScanLibraryAsync,
			"scan accounts from Downloads",
			"Libation could not start the library scan. Review account access and try again.");

		main.PropertyChanged += Main_PropertyChanged;
		main.ProcessQueue.PropertyChanged += ProcessQueue_PropertyChanged;
		main.ProcessQueue.Queue.CollectionChanged += Queue_CollectionChanged;
	}

	private LibraryCommands.LibraryStats? Stats => main.LibraryStats;
	public bool IsLoading => Stats is null;
	public bool IsReady => Stats is not null;
	public bool HasLibraryTitles => Stats?.HasBookResults == true;
	public bool ShowEmptyLibrary => IsReady && !HasLibraryTitles;
	public string EmptyLibraryExplanation => main.AnyAccounts
		? main.ActivelyScanning
			? Resources.DownloadsEmptyScanningExplanation
			: "Scan the connected accounts to catalogue titles before starting a download workflow."
		: "Add an Audible account, then scan it to catalogue titles before starting a download workflow.";
	public string EmptyLibraryActionText => main.AnyAccounts && !main.ActivelyScanning ? "Scan accounts" : "Manage accounts";
	public ICommand EmptyLibraryCommand => main.AnyAccounts && !main.ActivelyScanning ? ScanLibraryCommand : OpenAccountsCommand;
	public bool HasAttention => AttentionCount > 0;
	public string TotalText => Format(TotalCount);
	public string PendingText => Format(Stats?.PendingBooks ?? 0);
	public string DownloadedText => Format(Stats?.booksDownloadedOnly ?? 0);
	public string CompletedText => Format(Stats?.booksFullyBackedUp ?? 0);
	public string AttentionText => Format(AttentionCount);
	public string PendingExplanation => string.Format(CultureInfo.CurrentCulture, Resources.DownloadsPendingExplanationFormat, PendingText, DownloadedText);
	public int TotalCount => Stats is null ? 0 : Stats.booksFullyBackedUp + Stats.booksDownloadedOnly + Stats.booksNoProgress + Stats.booksError + Stats.booksUnavailable;
	public int AttentionCount => Stats is null ? 0 : Stats.booksError + Stats.booksUnavailable;
	public int ActiveCount => main.ProcessQueue.Queue.GetAllItems().Count(item => item.Status == ProcessBookStatus.Working);
	public int QueuedCount => main.ProcessQueue.Queue.GetAllItems().Count(item => item.Status == ProcessBookStatus.Queued);
	public bool HasActivePipeline => ActiveCount + QueuedCount > 0;
	public string PipelineText => HasActivePipeline
		? string.Format(CultureInfo.CurrentCulture, Resources.DownloadsPipelineActiveFormat, Format(ActiveCount), Format(QueuedCount))
		: Resources.DownloadsPipelineIdle;
	public string PipelineBadgeText => HasActivePipeline ? "Active" : "Idle";
	public LibationStatusKind PipelineStatus => HasActivePipeline ? LibationStatusKind.Processing : LibationStatusKind.Completed;
	public string AttentionExplanation => AttentionCount == 1
		? "1 title is unavailable or has a recorded download error. Open the Library or Processing view for title-level detail."
		: $"{Format(AttentionCount)} titles are unavailable or have recorded download errors. Open the Library or Processing view for title-level detail.";

	public ICommand DownloadPendingCommand { get; }
	public ICommand DownloadPdfsCommand { get; }
	public ICommand LocateFilesCommand { get; }
	public ICommand ConvertAllToMp3Command { get; }
	public ICommand RefreshCommand { get; }
	public ICommand OpenAccountsCommand { get; }
	public ICommand ScanLibraryCommand { get; }
	public string RouteEyebrow => "Acquisition";
	public string RouteTitle => "Downloads";
	public string RouteSubtitle => "See which titles are pending, downloaded, processed, or need attention.";
	public RouteCommandPresentation RoutePrimaryCommand => new("Download pending titles", DownloadPendingCommand);
	public System.Collections.Generic.IReadOnlyList<RouteCommandPresentation> RouteSecondaryCommands =>
	[
		new("Download PDFs", DownloadPdfsCommand),
		new("Locate files", LocateFilesCommand),
		new("Refresh", RefreshCommand),
	];
	public RouteStatusPresentation RouteStatusBadge => new(HasAttention ? AttentionExplanation : PipelineText,
		HasAttention ? LibationStatusKind.NeedsAttention : PipelineStatus);

	private void Main_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (string.IsNullOrEmpty(e.PropertyName)
			|| e.PropertyName is nameof(MainVM.LibraryStats) or nameof(MainVM.AnyAccounts) or nameof(MainVM.ActivelyScanning))
			RaiseState();
	}

	private void ProcessQueue_PropertyChanged(object? sender, PropertyChangedEventArgs e) => RaisePipeline();
	private void Queue_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => RaisePipeline();

	private void RaiseState()
	{
		foreach (var property in new[]
		{
			nameof(IsLoading), nameof(IsReady), nameof(HasLibraryTitles), nameof(ShowEmptyLibrary),
			nameof(EmptyLibraryExplanation), nameof(EmptyLibraryActionText), nameof(EmptyLibraryCommand),
			nameof(HasAttention), nameof(TotalText), nameof(PendingText), nameof(PendingExplanation),
			nameof(DownloadedText), nameof(CompletedText), nameof(AttentionText), nameof(TotalCount),
			nameof(AttentionCount), nameof(AttentionExplanation), nameof(RouteStatusBadge),
		})
			this.RaisePropertyChanged(property);
	}

	private void RaisePipeline()
	{
		this.RaisePropertyChanged(nameof(ActiveCount));
		this.RaisePropertyChanged(nameof(QueuedCount));
		this.RaisePropertyChanged(nameof(HasActivePipeline));
		this.RaisePropertyChanged(nameof(PipelineText));
		this.RaisePropertyChanged(nameof(PipelineBadgeText));
		this.RaisePropertyChanged(nameof(PipelineStatus));
		this.RaisePropertyChanged(nameof(RouteStatusBadge));
	}

	private static string Format(int value) => value.ToString("N0", CultureInfo.CurrentCulture);

	protected override void DisposeCore()
	{
		main.PropertyChanged -= Main_PropertyChanged;
		main.ProcessQueue.PropertyChanged -= ProcessQueue_PropertyChanged;
		main.ProcessQueue.Queue.CollectionChanged -= Queue_CollectionChanged;
	}
}
