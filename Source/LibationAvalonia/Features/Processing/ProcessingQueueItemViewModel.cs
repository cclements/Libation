using Avalonia.Input.Platform;
using LibationAvalonia.DesignSystem.Components;
using LibationUiBase.Diagnostics;
using LibationUiBase.ProcessQueue;
using ReactiveUI;
using Avalonia.Media;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

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
	private readonly ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> retryCommand;
	private bool disposed;

	internal ProcessingQueueItemViewModel(ProcessBookViewModel source, ProcessingViewModel owner)
	{
		Source = source;
		this.owner = owner;
		cancelCommand = ReactiveCommand.CreateFromTask(CancelAsync);
		copyTechnicalDetailsCommand = ReactiveCommand.CreateFromTask(CopyTechnicalDetailsAsync);
		retryCommand = ReactiveCommand.CreateFromTask(() => owner.RetryAsync(Source));
		Source.PropertyChanged += Source_PropertyChanged;
	}

	public ProcessBookViewModel Source { get; }
	public string Title => string.IsNullOrWhiteSpace(Source.Title) ? "Untitled audiobook" : Source.Title;
	public string Stage => Source.StatusText;
	public string Message
	{
		get
		{
			var details = string.Join(" · ", new[] { Source.Author, Source.Narrator, OutputProfileText }
				.Where(value => !string.IsNullOrWhiteSpace(value)));
			return string.IsNullOrWhiteSpace(Source.ETA)
				? details
				: string.IsNullOrWhiteSpace(details) ? Source.ETA! : $"{details} · {Source.ETA}";
		}
	}
	public string? OutputProfileText => !Source.IncludesBookDownload
		? null
		: Source.Configuration.SplitFilesByChapter
			? Source.Configuration.DecryptToLossy ? "MP3 split by chapter" : "M4B split by chapter"
			: Source.Configuration.DecryptToLossy ? "MP3 output" : "M4B output";
	public IImage? Cover => Source.Cover as IImage;
	public double Progress => Source.Progress;
	public bool ShowProgress => Source.IsDownloading;
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
	public string? ErrorDetails => Source.Status switch
	{
		ProcessBookStatus.Failed => BuildFailureSummary(),
		ProcessBookStatus.Cancelled => $"Processing was cancelled. Partial output may remain; inspect the output folder before starting again. Reference: {CorrelationId}.",
		_ => null,
	};
	public ICommand? CancelCommand => Source.IsFinished ? null : cancelCommand;
	public ICommand? RetryCommand => owner.CanRetry(Source) ? retryCommand : null;
	public ICommand? OpenLogCommand => Source.Status is ProcessBookStatus.Failed or ProcessBookStatus.Cancelled
		? owner.OpenLogCommand
		: null;
	public ICommand? CopyTechnicalDetailsCommand => Source.Status is ProcessBookStatus.Failed or ProcessBookStatus.Cancelled
		? copyTechnicalDetailsCommand
		: null;

	private string BuildFailureSummary() => Source.Result switch
	{
		ProcessBookResult.DiskFull => $"Processing stopped because the output storage is full. Partial files may remain. Free space or change Books / In progress in Settings before trying again. Reference: {CorrelationId}.",
		ProcessBookResult.LicenseDenied or ProcessBookResult.LicenseDeniedPossibleOutage => $"Audible did not grant access to this title. Confirm the title is available to the connected account, then try again. Reference: {CorrelationId}.",
		ProcessBookResult.WidevineRecommended => $"The current authorization method could not license this title. Follow the Widevine guidance in the queue log before trying again. Reference: {CorrelationId}.",
		ProcessBookResult.ValidationFail => $"The source or requested output did not pass validation. Review the title and output settings before trying again. Reference: {CorrelationId}.",
		_ => $"{Source.StatusText}. Partial output may remain. Open Queue controls & log for the matching reference before trying again. Reference: {CorrelationId}.",
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
			$"Result: {Source.Result}",
			$"Correlation ID: {CorrelationId}",
			$"Summary: {ErrorDetails}"));
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

	private void Source_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName is nameof(ProcessBookViewModel.Status) or nameof(ProcessBookViewModel.Result))
			owner.ScheduleMembershipRefresh();

		this.RaisePropertyChanged(nameof(Title));
		this.RaisePropertyChanged(nameof(Stage));
		this.RaisePropertyChanged(nameof(Message));
		this.RaisePropertyChanged(nameof(OutputProfileText));
		this.RaisePropertyChanged(nameof(Cover));
		this.RaisePropertyChanged(nameof(Progress));
		this.RaisePropertyChanged(nameof(ShowProgress));
		this.RaisePropertyChanged(nameof(Status));
		this.RaisePropertyChanged(nameof(StatusText));
		this.RaisePropertyChanged(nameof(ErrorDetails));
		this.RaisePropertyChanged(nameof(CancelCommand));
		this.RaisePropertyChanged(nameof(RetryCommand));
		this.RaisePropertyChanged(nameof(OpenLogCommand));
		this.RaisePropertyChanged(nameof(CopyTechnicalDetailsCommand));
	}

	public void Dispose()
	{
		if (disposed)
			return;
		disposed = true;
		Source.PropertyChanged -= Source_PropertyChanged;
		cancelCommand.Dispose();
		copyTechnicalDetailsCommand.Dispose();
		retryCommand.Dispose();
	}
}
