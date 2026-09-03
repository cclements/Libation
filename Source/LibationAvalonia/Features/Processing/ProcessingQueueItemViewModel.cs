using Avalonia.Input.Platform;
using Avalonia.Media;
using DataLayer;
using Dinah.Core;
using LibationAvalonia.DesignSystem.Components;
using LibationFileManager;
using LibationUiBase.Diagnostics;
using LibationUiBase.ProcessQueue;
using ReactiveUI;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using QueuePosition = LibationUiBase.QueuePosition;

namespace LibationAvalonia.Features.Processing;

/// <summary>
/// Presentation projection for one item in the existing process queue. It never
/// owns or recreates processing state.
/// </summary>
public sealed class ProcessingQueueItemViewModel : ReactiveObject, IDisposable
{
	private readonly ProcessingViewModel owner;
	private readonly ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> cancelCommand;
	private readonly ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> copyTechnicalDetailsCommand;
	private readonly ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> moveDownCommand;
	private readonly ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> moveUpCommand;
	private readonly ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> retryCommand;
	private readonly ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> revealCommand;
	private bool disposed;

	internal ProcessingQueueItemViewModel(ProcessBookViewModel source, ProcessingViewModel owner)
	{
		Source = source;
		this.owner = owner;
		IsExpanded = Source.Status is ProcessBookStatus.Failed;
		cancelCommand = ReactiveCommand.CreateFromTask(CancelAsync);
		copyTechnicalDetailsCommand = ReactiveCommand.CreateFromTask(CopyTechnicalDetailsAsync);
		moveDownCommand = ReactiveCommand.Create(() => owner.Move(Source, QueuePosition.OneDown));
		moveUpCommand = ReactiveCommand.Create(() => owner.Move(Source, QueuePosition.OneUp));
		retryCommand = ReactiveCommand.CreateFromTask(() => owner.RetryAsync(Source));
		revealCommand = ReactiveCommand.Create(RevealOutput);
		Source.PropertyChanged += Source_PropertyChanged;
	}

	public ProcessBookViewModel Source { get; }
	public string Title => string.IsNullOrWhiteSpace(Source.Title) ? "Untitled audiobook" : Source.Title;
	public string Author => string.IsNullOrWhiteSpace(Source.Author) ? "Unknown author" : Source.Author;
	public string? Narrator => Source.Narrator;
	public string Stage => Source.PresentationStage switch
	{
		ProcessBookPresentationStage.Downloading => "Downloading",
		ProcessBookPresentationStage.Decrypting => "Decrypting",
		ProcessBookPresentationStage.Converting => "Converting",
		ProcessBookPresentationStage.Completed => "Completed",
		_ => Source.StatusText,
	};
	public string StageAnnouncement => $"{Title}: {Stage}";
	public string RowAccessibleName => $"{Title}, {StatusText}, {Stage}";
	public string Message => string.Join(" · ", new[] { Author, Narrator, OutputProfileText }
		.Where(value => !string.IsNullOrWhiteSpace(value)));
	public string? OutputProfileText => Source.IncludesMp3Conversion
		? "MP3 output"
		: Source.IncludesBookDownload
			? Source.Configuration.SplitFilesByChapter
				? Source.Configuration.DecryptToLossy ? "MP3 split by chapter" : "M4B split by chapter"
				: Source.Configuration.DecryptToLossy ? "MP3 output" : "M4B output"
			: Source.IncludesPdfDownload ? "PDF supplement" : null;
	public string? OutputPath => AudibleFileStorage.Audio.GetPath(Source.LibraryBook.Book.AudibleProductId)?.ShortPathName;
	public IImage? Cover => Source.Cover as IImage;
	public double Progress => Source.Progress;
	public string? ProgressText => ShowProgress ? $"{Progress:0}%" : null;
	public string ProgressAccessibleName => $"{Title} progress {Progress:0} percent";
	public bool ShowProgress => Source.Status is ProcessBookStatus.Working;
	public string? EtaText => ShowProgress && Source.TimeRemaining > TimeSpan.Zero ? Source.ETA : null;
	public LibationStatusKind Status => Source.Status switch
	{
		ProcessBookStatus.Queued => LibationStatusKind.DownloadPending,
		ProcessBookStatus.Working => LibationStatusKind.Processing,
		ProcessBookStatus.Completed => LibationStatusKind.Completed,
		ProcessBookStatus.Cancelled => LibationStatusKind.Cancelled,
		_ => LibationStatusKind.Failed,
	};
	public string StatusText => Source.StatusText;
	public string CorrelationId => Source.CorrelationId;
	public string? FailureSummary => Source.Status is ProcessBookStatus.Failed or ProcessBookStatus.Cancelled
		? Source.StatusText
		: null;
	public string? RecommendedAction => Source.Status switch
	{
		ProcessBookStatus.Failed => BuildRecommendedAction(),
		ProcessBookStatus.Cancelled => "Inspect the output folder before starting this title again; partial output may remain.",
		_ => null,
	};
	public string? ReferenceText => Source.Status is ProcessBookStatus.Failed or ProcessBookStatus.Cancelled
		? $"Reference: {CorrelationId}"
		: null;
	public string? ErrorDetails => FailureSummary;
	public bool IsExpanded { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
	public ICommand? CancelCommand => Source.IsFinished ? null : cancelCommand;
	public ICommand? RetryCommand => owner.CanRetry(Source) ? retryCommand : null;
	public ICommand? MoveUpCommand => owner.CanMoveUp(Source) ? moveUpCommand : null;
	public ICommand? MoveDownCommand => owner.CanMoveDown(Source) ? moveDownCommand : null;
	public ICommand? OpenLogCommand => Source.Status is ProcessBookStatus.Failed or ProcessBookStatus.Cancelled
		? owner.OpenLogCommand
		: null;
	public ICommand? CopyTechnicalDetailsCommand => Source.Status is ProcessBookStatus.Failed or ProcessBookStatus.Cancelled
		? copyTechnicalDetailsCommand
		: null;
	public ICommand? RevealCommand => CanReveal ? revealCommand : null;
	public bool CanReveal => Source.Status is ProcessBookStatus.Completed
		&& Source.LibraryBook.Book.AudioExists
		&& !string.IsNullOrWhiteSpace(OutputPath);

	public string CancelAccessibleName => $"Cancel {Title}";
	public string RetryAccessibleName => $"Retry {Title}";
	public string MoveUpAccessibleName => $"Move {Title} up one position";
	public string MoveDownAccessibleName => $"Move {Title} down one position";
	public string OpenLogAccessibleName => $"Open the queue log for {Title}";
	public string CopyDetailsAccessibleName => $"Copy technical details for {Title}";
	public string RevealAccessibleName => $"Reveal the output for {Title}";

	private string BuildRecommendedAction() => Source.Result switch
	{
		ProcessBookResult.DiskFull => "Free space or change Books / In progress in Settings before trying again.",
		ProcessBookResult.LicenseDenied or ProcessBookResult.LicenseDeniedPossibleOutage => "Confirm the title is available to the connected Audible account, then try again.",
		ProcessBookResult.WidevineRecommended => "Open the queue log and follow its Widevine guidance before trying again.",
		ProcessBookResult.ValidationFail => "Review the source title and output settings before trying again.",
		_ when owner.CanRetry(Source) => "Retry this title, or open the queue log for the matching reference.",
		_ => "Open the queue log for the matching reference, then requeue the title from the Library if appropriate.",
	};

	private async Task CancelAsync()
	{
		await Source.CancelAsync();
		if (Source.Queued)
			owner.Source.Queue.RemoveQueued(Source);
	}

	private async Task CopyTechnicalDetailsAsync()
	{
		var details = DiagnosticTextScrubber.Scrub(string.Join(Environment.NewLine,
			$"Title: {Title}",
			$"Status: {StatusText}",
			$"Stage: {Stage}",
			$"Result: {Source.Result}",
			$"Output: {OutputProfileText}",
			$"Correlation ID: {CorrelationId}",
			$"Summary: {FailureSummary}",
			$"Recommended action: {RecommendedAction}"));
		if (App.MainWindow?.Clipboard is not { } clipboard || details is null)
			return;

		try
		{
			await clipboard.SetTextAsync(details);
		}
		catch (Exception ex)
		{
			Serilog.Log.Logger.Warning(
				"Unable to copy queue-item diagnostics. Correlation ID: {CorrelationId}. {TechnicalDetails}",
				CorrelationId,
				DiagnosticTextScrubber.Scrub(ex.ToString()));
		}
	}

	private void RevealOutput()
	{
		if (OutputPath is not { Length: > 0 } path || Go.To.File(path))
			return;

		Serilog.Log.Logger.Warning(
			"Unable to reveal completed queue output. Correlation ID: {CorrelationId}",
			CorrelationId);
	}

	private void Source_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName is nameof(ProcessBookViewModel.Status) or nameof(ProcessBookViewModel.Result))
		{
			if (Source.Status is ProcessBookStatus.Failed)
				IsExpanded = true;
			owner.ScheduleMembershipRefresh();
		}

		if (e.PropertyName is null or nameof(ProcessBookViewModel.Title))
		{
			this.RaisePropertyChanged(nameof(Title));
			this.RaisePropertyChanged(nameof(StageAnnouncement));
			RaiseAccessibleNamesChanged();
		}
		if (e.PropertyName is null or nameof(ProcessBookViewModel.Author) or nameof(ProcessBookViewModel.Narrator))
		{
			this.RaisePropertyChanged(nameof(Author));
			this.RaisePropertyChanged(nameof(Narrator));
			this.RaisePropertyChanged(nameof(Message));
		}
		if (e.PropertyName is null or nameof(ProcessBookViewModel.Cover))
			this.RaisePropertyChanged(nameof(Cover));
		if (e.PropertyName is null or nameof(ProcessBookViewModel.Progress))
		{
			this.RaisePropertyChanged(nameof(Progress));
			this.RaisePropertyChanged(nameof(ProgressText));
			this.RaisePropertyChanged(nameof(ProgressAccessibleName));
		}
		if (e.PropertyName is null or nameof(ProcessBookViewModel.TimeRemaining) or nameof(ProcessBookViewModel.ETA))
			this.RaisePropertyChanged(nameof(EtaText));
		if (e.PropertyName is null
			or nameof(ProcessBookViewModel.Status)
			or nameof(ProcessBookViewModel.Result)
			or nameof(ProcessBookViewModel.PresentationStage)
			or nameof(ProcessBookViewModel.LastPresentationStage)
			or nameof(ProcessBookViewModel.StatusOverride))
		{
			this.RaisePropertyChanged(nameof(Stage));
			this.RaisePropertyChanged(nameof(StageAnnouncement));
			this.RaisePropertyChanged(nameof(ShowProgress));
			this.RaisePropertyChanged(nameof(ProgressText));
			this.RaisePropertyChanged(nameof(EtaText));
			this.RaisePropertyChanged(nameof(Status));
			this.RaisePropertyChanged(nameof(StatusText));
			this.RaisePropertyChanged(nameof(FailureSummary));
			this.RaisePropertyChanged(nameof(RecommendedAction));
			this.RaisePropertyChanged(nameof(ReferenceText));
			this.RaisePropertyChanged(nameof(ErrorDetails));
			this.RaisePropertyChanged(nameof(CancelCommand));
			this.RaisePropertyChanged(nameof(RetryCommand));
			this.RaisePropertyChanged(nameof(OpenLogCommand));
			this.RaisePropertyChanged(nameof(CopyTechnicalDetailsCommand));
			this.RaisePropertyChanged(nameof(CanReveal));
			this.RaisePropertyChanged(nameof(RevealCommand));
			RaiseAccessibleNamesChanged();
		}

		owner.NotifyItemChanged(this, e.PropertyName);
	}

	internal void NotifyQueuePositionChanged()
	{
		this.RaisePropertyChanged(nameof(MoveUpCommand));
		this.RaisePropertyChanged(nameof(MoveDownCommand));
	}

	private void RaiseAccessibleNamesChanged()
	{
		this.RaisePropertyChanged(nameof(RowAccessibleName));
		this.RaisePropertyChanged(nameof(CancelAccessibleName));
		this.RaisePropertyChanged(nameof(RetryAccessibleName));
		this.RaisePropertyChanged(nameof(MoveUpAccessibleName));
		this.RaisePropertyChanged(nameof(MoveDownAccessibleName));
		this.RaisePropertyChanged(nameof(OpenLogAccessibleName));
		this.RaisePropertyChanged(nameof(CopyDetailsAccessibleName));
		this.RaisePropertyChanged(nameof(RevealAccessibleName));
	}

	public void Dispose()
	{
		if (disposed)
			return;
		disposed = true;
		Source.PropertyChanged -= Source_PropertyChanged;
		cancelCommand.Dispose();
		copyTechnicalDetailsCommand.Dispose();
		moveDownCommand.Dispose();
		moveUpCommand.Dispose();
		retryCommand.Dispose();
		revealCommand.Dispose();
	}
}
