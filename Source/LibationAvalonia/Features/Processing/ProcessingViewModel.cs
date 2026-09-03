using Avalonia.Input.Platform;
using Avalonia.Threading;
using DataLayer;
using LibationAvalonia.DesignSystem.Components;
using LibationAvalonia.Shell;
using LibationFileManager;
using LibationUiBase.ProcessQueue;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using System.Windows.Input;
using QueuePosition = LibationUiBase.QueuePosition;

namespace LibationAvalonia.Features.Processing;

/// <summary>
/// One row in the Processing route's single virtualized surface. Section rows and
/// queue-item rows share this carrier so grouping never creates nested item controls.
/// </summary>
public sealed class ProcessingQueueRowViewModel
{
	private ProcessingQueueRowViewModel(
		string? sectionTitle,
		int sectionCount,
		ProcessingQueueItemViewModel? item)
	{
		SectionTitle = sectionTitle;
		SectionCount = sectionCount;
		Item = item;
	}

	public string? SectionTitle { get; }
	public int SectionCount { get; }
	public ProcessingQueueItemViewModel? Item { get; }
	public bool IsSectionHeader => Item is null;
	public bool IsQueueItem => Item is not null;
	public string SectionText => $"{SectionTitle} ({SectionCount.ToString("N0", CultureInfo.CurrentCulture)})";

	public static ProcessingQueueRowViewModel Section(string title, int count) => new(title, count, null);
	public static ProcessingQueueRowViewModel QueueItem(ProcessingQueueItemViewModel item) => new(null, 0, item);
}

/// <summary>
/// Groups and summarizes the existing queue for the contemporary presentation.
/// <see cref="Source"/> remains the only queue and execution owner.
/// </summary>
public sealed class ProcessingViewModel : ReactiveObject, IDisposable, IRoutePresentation
{
	private readonly Dictionary<ProcessBookViewModel, ProcessingQueueItemViewModel> items = new();
	private readonly Func<ProcessBookViewModel, Task<bool>>? retryProcess;
	private bool refreshPending;
	private bool disposed;

	public ProcessingViewModel(
		ProcessQueueViewModel source,
		Func<ProcessBookViewModel, Task<bool>>? retryProcess = null)
	{
		Source = source;
		this.retryProcess = retryProcess;
		CancelAllCommand = ReactiveCommand.CreateFromTask(() => Source.CancelAllAsync());
		ClearFinishedCommand = ReactiveCommand.Create(ClearFinished);
		PerformanceCommand = ReactiveCommand.Create(() => { IsPerformanceExpanded = !IsPerformanceExpanded; });
		OpenLogCommand = ReactiveCommand.Create(() => { IsLogOpen = true; });
		CloseLogCommand = ReactiveCommand.Create(() => { IsLogOpen = false; });
		CopyLogCommand = ReactiveCommand.CreateFromTask(CopyLogAsync);
		ClearLogCommand = ReactiveCommand.Create(() => Source.LogEntries.Clear());
		Source.Queue.CollectionChanged += Queue_CollectionChanged;
		Source.PropertyChanged += Source_PropertyChanged;
		Source.LogEntries.CollectionChanged += LogEntries_CollectionChanged;
		RefreshMembership();
	}

	public ProcessQueueViewModel Source { get; }
	public ObservableCollection<ProcessingQueueItemViewModel> Active { get; } = new();
	public ObservableCollection<ProcessingQueueItemViewModel> Waiting { get; } = new();
	public ObservableCollection<ProcessingQueueItemViewModel> Completed { get; } = new();
	public ObservableCollection<ProcessingQueueItemViewModel> Failed { get; } = new();
	public ObservableCollection<ProcessingQueueItemViewModel> Cancelled { get; } = new();
	public ObservableCollection<ProcessingQueueItemViewModel> DecanterActiveItems { get; } = new();
	public ObservableCollection<ProcessingQueueRowViewModel> QueueRows { get; } = new();

	public ReactiveCommand<Unit, Unit> CancelAllCommand { get; }
	public ReactiveCommand<Unit, Unit> ClearFinishedCommand { get; }
	public ReactiveCommand<Unit, Unit> PerformanceCommand { get; }
	public ReactiveCommand<Unit, Unit> OpenLogCommand { get; }
	public ReactiveCommand<Unit, Unit> CloseLogCommand { get; }
	public ReactiveCommand<Unit, Unit> CopyLogCommand { get; }
	public ReactiveCommand<Unit, Unit> ClearLogCommand { get; }

	public bool IsPerformanceExpanded { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
	public bool IsLogOpen { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }

	public int ActiveCount => Active.Count;
	public int WaitingCount => Waiting.Count;
	public int CompletedCount => Completed.Count;
	public int FailedCount => Failed.Count;
	public int CancelledCount => Cancelled.Count;
	public int FinishedCount => CompletedCount + FailedCount + CancelledCount;
	public int InQueueCount => ActiveCount + WaitingCount;
	public int QueueItemCount => InQueueCount + FinishedCount;
	public bool HasWork => QueueItemCount > 0;
	public bool HasActive => ActiveCount > 0;
	public bool HasWaiting => WaitingCount > 0;
	public bool HasCompleted => CompletedCount > 0;
	public bool HasFailed => FailedCount > 0;
	public bool HasCancelled => CancelledCount > 0;
	public bool HasFinished => FinishedCount > 0;
	public bool CanCancel => InQueueCount > 0;
	public bool ShowProgress => HasWork;
	public double Progress => QueueItemCount == 0
		? 0
		: (100d * FinishedCount + Active.Sum(item => item.Progress)) / QueueItemCount;
	public string OverallProgressText => $"{Progress:0}%";
	public string ActiveText => Active.FirstOrDefault()?.Title ?? "No active processing";
	public string SummaryText
		=> $"{ActiveCount} active · {WaitingCount} waiting · {CompletedCount} completed · {FailedCount} failed"
			+ (CancelledCount > 0 ? $" · {CancelledCount} cancelled" : string.Empty);
	public string InQueueText => InQueueCount.ToString("N0", CultureInfo.CurrentCulture);
	public string ConvertingText => Active.Count(item => item.Source.PresentationStage is ProcessBookPresentationStage.Converting)
		.ToString("N0", CultureInfo.CurrentCulture);
	public string RunningTimeText => string.IsNullOrWhiteSpace(Source.RunningTime) ? "—" : Source.RunningTime;

	public ProcessingQueueItemViewModel? CurrentItem => Active.FirstOrDefault() ?? Waiting.FirstOrDefault();
	public string? CurrentTitle => CurrentItem?.Title;
	public string? CurrentStage => CurrentItem?.Stage;
	public string? CurrentStageAnnouncement => CurrentItem?.StageAnnouncement;
	public double CurrentProgress => CurrentItem?.Progress ?? 0;
	public bool ShowCurrentProgress => CurrentItem?.ShowProgress == true;
	public bool CurrentCancellable => CurrentCancelCommand is not null;
	public ICommand? CurrentCancelCommand => CurrentItem?.CancelCommand;

	public bool HasLogEntries => Source.LogEntries.Count > 0;
	public string LogSummary => Source.LogEntries.Count == 1 ? "1 queue log entry" : $"{Source.LogEntries.Count} queue log entries";
	public string RouteEyebrow => "Processing";
	public string RouteTitle => "The Decanter";
	public string RouteSubtitle => "Follow the existing processing queue from waiting through completion.";
	public RouteCommandPresentation? RoutePrimaryCommand => CanCancel ? new("Cancel all", CancelAllCommand) : null;
	public IReadOnlyList<RouteCommandPresentation> RouteSecondaryCommands =>
	[
		new("Clear finished", ClearFinishedCommand),
		new("Performance", PerformanceCommand),
	];
	public RouteStatusPresentation RouteStatusBadge => new(SummaryText, HasFailed
		? LibationStatusKind.NeedsAttention
		: CanCancel ? LibationStatusKind.Processing : LibationStatusKind.Completed);

	internal bool CanRetry(ProcessBookViewModel item)
		=> retryProcess is not null
			&& item.Status == ProcessBookStatus.Failed
			&& item.LastPresentationStage switch
				{
					ProcessBookPresentationStage.Downloading
						=> item.IncludesPdfDownload && item.LibraryBook.NeedsPdfDownload,
					ProcessBookPresentationStage.Decrypting
						=> item.IncludesBookDownload && item.LibraryBook.NeedsBookDownload,
					_ => false,
				};

	internal bool CanMoveUp(ProcessBookViewModel item)
		=> item.Status is ProcessBookStatus.Queued
			&& items.TryGetValue(item, out var wrapper)
			&& Waiting.IndexOf(wrapper) > 0;

	internal bool CanMoveDown(ProcessBookViewModel item)
		=> item.Status is ProcessBookStatus.Queued
			&& items.TryGetValue(item, out var wrapper)
			&& Waiting.IndexOf(wrapper) is var index
			&& index >= 0
			&& index < Waiting.Count - 1;

	internal void Move(ProcessBookViewModel item, QueuePosition position)
	{
		if (position is QueuePosition.OneUp && !CanMoveUp(item)
			|| position is QueuePosition.OneDown && !CanMoveDown(item))
			return;

		Source.Queue.MoveQueuePosition(item, position);
	}

	internal async Task RetryAsync(ProcessBookViewModel item)
	{
		if (!CanRetry(item) || retryProcess is null)
			return;
		if (!await retryProcess(item))
		{
			Serilog.Log.Logger.Warning(
				"A failed download queue item was not accepted for retry. Correlation ID: {CorrelationId}",
				item.CorrelationId);
		}
	}

	internal void ScheduleMembershipRefresh()
	{
		if (disposed || refreshPending)
			return;
		refreshPending = true;
		// Coalesce queue/status bursts into the next UI-dispatch pass. This uses the
		// platform render scheduler instead of inventing a timer or polling rate.
		Dispatcher.UIThread.Post(RefreshMembership, DispatcherPriority.Background);
	}

	internal void NotifyItemChanged(ProcessingQueueItemViewModel item, string? propertyName)
	{
		if (disposed)
			return;

		bool isCurrent = ReferenceEquals(item, CurrentItem);
		if (propertyName is null or nameof(ProcessBookViewModel.Progress))
		{
			this.RaisePropertyChanged(nameof(Progress));
			this.RaisePropertyChanged(nameof(OverallProgressText));
			if (isCurrent)
				this.RaisePropertyChanged(nameof(CurrentProgress));
		}
		if (propertyName is null or nameof(ProcessBookViewModel.Title))
		{
			if (isCurrent)
			{
				this.RaisePropertyChanged(nameof(CurrentTitle));
			}
		}
		if (propertyName is null
			or nameof(ProcessBookViewModel.Status)
			or nameof(ProcessBookViewModel.PresentationStage)
			or nameof(ProcessBookViewModel.StatusOverride))
		{
			this.RaisePropertyChanged(nameof(ConvertingText));
			if (isCurrent)
			{
				this.RaisePropertyChanged(nameof(CurrentStage));
				this.RaisePropertyChanged(nameof(ShowCurrentProgress));
				this.RaisePropertyChanged(nameof(CurrentCancellable));
				this.RaisePropertyChanged(nameof(CurrentCancelCommand));
			}
		}
		if (ReferenceEquals(item, Active.FirstOrDefault())
			&& propertyName is (null or nameof(ProcessBookViewModel.Title)))
			this.RaisePropertyChanged(nameof(ActiveText));
	}

	private void Queue_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => ScheduleMembershipRefresh();

	private void Source_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName is nameof(ProcessQueueViewModel.QueuedCount))
			ScheduleMembershipRefresh();
		if (e.PropertyName is nameof(ProcessQueueViewModel.Progress)
			or nameof(ProcessQueueViewModel.ProgressBarVisible))
		{
			this.RaisePropertyChanged(nameof(Progress));
			this.RaisePropertyChanged(nameof(OverallProgressText));
			this.RaisePropertyChanged(nameof(ShowProgress));
		}
		if (e.PropertyName is nameof(ProcessQueueViewModel.RunningTime))
			this.RaisePropertyChanged(nameof(RunningTimeText));
	}

	private void LogEntries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		this.RaisePropertyChanged(nameof(HasLogEntries));
		this.RaisePropertyChanged(nameof(LogSummary));
	}

	private void RefreshMembership()
	{
		if (disposed)
			return;
		refreshPending = false;
		var sourceItems = Source.Queue.GetAllItems().ToArray();
		var live = sourceItems.ToHashSet();

		foreach (var removed in items.Keys.Where(item => !live.Contains(item)).ToArray())
		{
			items[removed].Dispose();
			items.Remove(removed);
		}

		foreach (var source in sourceItems)
		{
			if (!items.ContainsKey(source))
				items.Add(source, new ProcessingQueueItemViewModel(source, this));
		}

		var activeSources = Source.Queue.GetActive().ToHashSet();
		Replace(Active, sourceItems
			.Where(item => activeSources.Contains(item)
				&& item.Status is ProcessBookStatus.Queued or ProcessBookStatus.Working)
			.Select(item => items[item]));
		Replace(Waiting, sourceItems
			.Where(item => !activeSources.Contains(item) && item.Status == ProcessBookStatus.Queued)
			.Select(item => items[item]));
		Replace(Completed, sourceItems.Where(item => item.Status == ProcessBookStatus.Completed).Select(item => items[item]));
		Replace(Failed, sourceItems.Where(item => item.Status == ProcessBookStatus.Failed).Select(item => items[item]));
		Replace(Cancelled, sourceItems.Where(item => item.Status == ProcessBookStatus.Cancelled).Select(item => items[item]));
		Replace(DecanterActiveItems, Active.Take(3));

		foreach (var item in Waiting)
			item.NotifyQueuePositionChanged();

		RebuildRows();
		RaiseAggregateProperties();
	}

	private void RebuildRows()
	{
		var rows = new List<ProcessingQueueRowViewModel>();
		AddSection(rows, "Active", Active);
		AddSection(rows, "Waiting", Waiting);
		AddSection(rows, "Completed", Completed);
		AddSection(rows, "Failed", Failed);
		if (Cancelled.Count > 0)
			AddSection(rows, "Cancelled", Cancelled);
		Replace(QueueRows, rows);

		static void AddSection(
			ICollection<ProcessingQueueRowViewModel> target,
			string title,
			IReadOnlyCollection<ProcessingQueueItemViewModel> sectionItems)
		{
			target.Add(ProcessingQueueRowViewModel.Section(title, sectionItems.Count));
			foreach (var item in sectionItems)
				target.Add(ProcessingQueueRowViewModel.QueueItem(item));
		}
	}

	private void RaiseAggregateProperties()
	{
		this.RaisePropertyChanged(nameof(ActiveCount));
		this.RaisePropertyChanged(nameof(WaitingCount));
		this.RaisePropertyChanged(nameof(CompletedCount));
		this.RaisePropertyChanged(nameof(FailedCount));
		this.RaisePropertyChanged(nameof(CancelledCount));
		this.RaisePropertyChanged(nameof(FinishedCount));
		this.RaisePropertyChanged(nameof(InQueueCount));
		this.RaisePropertyChanged(nameof(QueueItemCount));
		this.RaisePropertyChanged(nameof(HasWork));
		this.RaisePropertyChanged(nameof(HasActive));
		this.RaisePropertyChanged(nameof(HasWaiting));
		this.RaisePropertyChanged(nameof(HasCompleted));
		this.RaisePropertyChanged(nameof(HasFailed));
		this.RaisePropertyChanged(nameof(HasCancelled));
		this.RaisePropertyChanged(nameof(HasFinished));
		this.RaisePropertyChanged(nameof(CanCancel));
		this.RaisePropertyChanged(nameof(ShowProgress));
		this.RaisePropertyChanged(nameof(Progress));
		this.RaisePropertyChanged(nameof(OverallProgressText));
		this.RaisePropertyChanged(nameof(ActiveText));
		this.RaisePropertyChanged(nameof(SummaryText));
		this.RaisePropertyChanged(nameof(InQueueText));
		this.RaisePropertyChanged(nameof(ConvertingText));
		this.RaisePropertyChanged(nameof(CurrentItem));
		this.RaisePropertyChanged(nameof(CurrentTitle));
		this.RaisePropertyChanged(nameof(CurrentStage));
		this.RaisePropertyChanged(nameof(CurrentProgress));
		this.RaisePropertyChanged(nameof(ShowCurrentProgress));
		this.RaisePropertyChanged(nameof(CurrentCancellable));
		this.RaisePropertyChanged(nameof(CurrentCancelCommand));
		this.RaisePropertyChanged(nameof(RoutePrimaryCommand));
		this.RaisePropertyChanged(nameof(RouteStatusBadge));
	}

	private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> source)
	{
		var snapshot = source.ToArray();
		if (target.SequenceEqual(snapshot))
			return;
		target.Clear();
		foreach (var item in snapshot)
			target.Add(item);
	}

	private void ClearFinished()
	{
		Source.Queue.ClearCompleted();
		if (!Source.Running)
			Source.RunningTime = string.Empty;
	}

	private async Task CopyLogAsync()
	{
		if (App.MainWindow?.Clipboard is not { } clipboard)
			return;

		var logText = string.Join(Environment.NewLine,
			Source.LogEntries.Select(entry => $"{entry.LogDate.ToShortDateString()} {entry.LogDate.ToShortTimeString()}\t{entry.LogMessage}"));
		try
		{
			await clipboard.SetTextAsync(logText);
		}
		catch (Exception ex)
		{
			Serilog.Log.Logger.Warning(ex, "Unable to copy the scrubbed processing queue log.");
		}
	}

	public void Dispose()
	{
		if (disposed)
			return;
		disposed = true;
		Source.Queue.CollectionChanged -= Queue_CollectionChanged;
		Source.PropertyChanged -= Source_PropertyChanged;
		Source.LogEntries.CollectionChanged -= LogEntries_CollectionChanged;
		foreach (var item in items.Values)
			item.Dispose();
		items.Clear();
		CancelAllCommand.Dispose();
		ClearFinishedCommand.Dispose();
		PerformanceCommand.Dispose();
		OpenLogCommand.Dispose();
		CloseLogCommand.Dispose();
		CopyLogCommand.Dispose();
		ClearLogCommand.Dispose();
	}
}
