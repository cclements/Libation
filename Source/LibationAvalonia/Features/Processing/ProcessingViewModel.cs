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
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;

namespace LibationAvalonia.Features.Processing;

/// <summary>
/// Groups and summarizes the existing queue for the contemporary presentation.
/// <see cref="Source"/> remains the only queue and execution owner.
/// </summary>
public sealed class ProcessingViewModel : ReactiveObject, IDisposable, IRoutePresentation
{
	private readonly Dictionary<ProcessBookViewModel, ProcessingQueueItemViewModel> items = new();
	private readonly Func<LibraryBook, Configuration, Task<bool>>? retryDownload;
	private bool refreshPending;
	private bool disposed;

	public ProcessingViewModel(
		ProcessQueueViewModel source,
		Func<LibraryBook, Configuration, Task<bool>>? retryDownload = null)
	{
		Source = source;
		this.retryDownload = retryDownload;
		CancelAllCommand = ReactiveCommand.CreateFromTask(() => Source.CancelAllAsync());
		ClearFinishedCommand = ReactiveCommand.Create(ClearFinished);
		OpenLogCommand = ReactiveCommand.Create(() =>
		{
			QueueDetailTabIndex = 1;
			SelectedTabIndex = 4;
		});
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
	public ReactiveCommand<Unit, Unit> CancelAllCommand { get; }
	public ReactiveCommand<Unit, Unit> ClearFinishedCommand { get; }
	public ReactiveCommand<Unit, Unit> OpenLogCommand { get; }
	public int SelectedTabIndex { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
	public int QueueDetailTabIndex { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }

	public bool HasWork => Active.Count + Waiting.Count + Completed.Count + Failed.Count > 0;
	public bool HasActive => Active.Count > 0;
	public bool HasWaiting => Waiting.Count > 0;
	public bool HasCompleted => Completed.Count > 0;
	public bool HasFailed => Failed.Count > 0;
	public bool HasFinished => HasCompleted || HasFailed;
	public bool CanCancel => HasActive || HasWaiting;
	public bool ShowProgress => CanCancel;
	public double Progress => Source.Queue.Count == 0 ? 0 : Source.Progress;
	public string ActiveText => Active.FirstOrDefault()?.Title ?? "No active processing";
	public string SummaryText => $"{Active.Count} active · {Waiting.Count} waiting · {Completed.Count} completed · {Failed.Count} failed or cancelled";
	public string LogSummary => Source.LogEntries.Count == 1 ? "1 queue log entry" : $"{Source.LogEntries.Count} queue log entries";
	public string RouteEyebrow => "Processing workspace";
	public string RouteTitle => "The Decanter";
	public string RouteSubtitle => "Follow the existing processing queue from waiting through completion.";
	public RouteCommandPresentation? RoutePrimaryCommand => CanCancel ? new("Cancel all", CancelAllCommand) : null;
	public IReadOnlyList<RouteCommandPresentation> RouteSecondaryCommands =>
	[
		new("Clear finished", ClearFinishedCommand),
		new("Open queue log", OpenLogCommand),
	];
	public RouteStatusPresentation RouteStatusBadge => new(SummaryText, HasFailed
		? LibationStatusKind.NeedsAttention
		: HasActive || HasWaiting ? LibationStatusKind.Processing : LibationStatusKind.Completed);

	internal bool CanRetry(ProcessBookViewModel item)
		=> retryDownload is not null
			&& item.Status == ProcessBookStatus.Failed
			&& item.IncludesBookDownload;

	internal async Task RetryAsync(ProcessBookViewModel item)
	{
		if (!CanRetry(item) || retryDownload is null)
			return;
		if (!await retryDownload(item.LibraryBook, item.Configuration))
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

	private void Queue_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => ScheduleMembershipRefresh();
	private void Source_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName is nameof(ProcessQueueViewModel.Progress)
			or nameof(ProcessQueueViewModel.ProgressBarVisible)
			or nameof(ProcessQueueViewModel.RunningTime))
		{
			this.RaisePropertyChanged(nameof(Progress));
			this.RaisePropertyChanged(nameof(ShowProgress));
		}
	}
	private void LogEntries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
		=> this.RaisePropertyChanged(nameof(LogSummary));

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

		Replace(Active, sourceItems.Where(item => item.Status == ProcessBookStatus.Working).Select(item => items[item]));
		Replace(Waiting, sourceItems.Where(item => item.Status == ProcessBookStatus.Queued).Select(item => items[item]));
		Replace(Completed, sourceItems.Where(item => item.Status == ProcessBookStatus.Completed).Select(item => items[item]));
		Replace(Failed, sourceItems.Where(item => item.Status is ProcessBookStatus.Failed or ProcessBookStatus.Cancelled).Select(item => items[item]));

		this.RaisePropertyChanged(nameof(HasWork));
		this.RaisePropertyChanged(nameof(HasActive));
		this.RaisePropertyChanged(nameof(HasWaiting));
		this.RaisePropertyChanged(nameof(HasCompleted));
		this.RaisePropertyChanged(nameof(HasFailed));
		this.RaisePropertyChanged(nameof(HasFinished));
		this.RaisePropertyChanged(nameof(CanCancel));
		this.RaisePropertyChanged(nameof(ShowProgress));
		this.RaisePropertyChanged(nameof(Progress));
		this.RaisePropertyChanged(nameof(ActiveText));
		this.RaisePropertyChanged(nameof(SummaryText));
		this.RaisePropertyChanged(nameof(RoutePrimaryCommand));
		this.RaisePropertyChanged(nameof(RouteStatusBadge));
	}

	private static void Replace(
		ObservableCollection<ProcessingQueueItemViewModel> target,
		IEnumerable<ProcessingQueueItemViewModel> source)
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
		OpenLogCommand.Dispose();
	}
}
