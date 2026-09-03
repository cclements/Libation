using ApplicationServices;
using Avalonia.Threading;
using DataLayer;
using LibationAvalonia.DesignSystem;
using LibationAvalonia.DesignSystem.Components;
using LibationAvalonia.Features.Tools;
using LibationAvalonia.Shell;
using LibationAvalonia.ViewModels;
using LibationUiBase.ProcessQueue;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace LibationAvalonia.Features.History;

/// <summary>
/// An honest projection of timestamps Libation already owns. This is not an audit
/// log: library-added and last-downloaded dates can be shown, while queue log rows
/// cover only the current retained processing session.
/// </summary>
public sealed class HistoryViewModel : SecondaryDestinationViewModel, IRoutePresentation
{
	public static readonly string AllActions = global::LibationAvalonia.Properties.Resources.HistoryViewModelAllActions;
	public static readonly string AllResults = global::LibationAvalonia.Properties.Resources.HistoryViewModelAllResults;

	private readonly MainVM main;
	private readonly CancellationTokenSource lifetime = new();
	private IReadOnlyList<HistoryItem> allItems = [];
	private bool refreshRunning;
	private bool refreshAgain;
	private bool isActive;
	private bool refreshPending = true;
	private bool changingFilters;
	private bool disposed;
	internal bool IsActive => isActive;

	public HistoryViewModel(MainVM main)
	{
		this.main = main ?? throw new ArgumentNullException(nameof(main));
		RefreshCommand = Track(ReactiveCommand.CreateFromTask(RefreshAsync));
		ClearFiltersCommand = Track(ReactiveCommand.Create(ClearFilters));
		main.PropertyChanged += Main_PropertyChanged;
		main.ProcessQueue.LogEntries.CollectionChanged += LogEntries_CollectionChanged;
	}

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
		{
			refreshPending = false;
			_ = RefreshAsync();
		}
	}

	public string SearchText
	{
		get => field;
		set
		{
			this.RaiseAndSetIfChanged(ref field, value ?? string.Empty);
			if (!changingFilters)
				ApplyFilter();
		}
	} = string.Empty;

	public DateTimeOffset? FromDate
	{
		get => field;
		set
		{
			this.RaiseAndSetIfChanged(ref field, value);
			if (!changingFilters)
				ApplyFilter();
		}
	}

	public DateTimeOffset? ToDate
	{
		get => field;
		set
		{
			this.RaiseAndSetIfChanged(ref field, value);
			if (!changingFilters)
				ApplyFilter();
		}
	}

	public IReadOnlyList<string> ActionOptions { get => field; private set => this.RaiseAndSetIfChanged(ref field, value); } = [AllActions];
	public string SelectedAction
	{
		get => field;
		set
		{
			this.RaiseAndSetIfChanged(ref field, string.IsNullOrWhiteSpace(value) ? AllActions : value);
			if (!changingFilters)
				ApplyFilter();
		}
	} = AllActions;

	public IReadOnlyList<string> ResultOptions { get => field; private set => this.RaiseAndSetIfChanged(ref field, value); } = [AllResults];
	public string SelectedResult
	{
		get => field;
		set
		{
			this.RaiseAndSetIfChanged(ref field, string.IsNullOrWhiteSpace(value) ? AllResults : value);
			if (!changingFilters)
				ApplyFilter();
		}
	} = AllResults;

	public ObservableCollection<HistoryItem> VisibleItems { get; } = new();
	public bool IsLoading { get => field; private set => this.RaiseAndSetIfChanged(ref field, value); } = true;
	public bool HasItems => VisibleItems.Count > 0;
	public bool ShowEmpty => !IsLoading && !HasItems;
	public bool HasActiveFilters => FromDate is not null || ToDate is not null
		|| SelectedAction != AllActions || SelectedResult != AllResults || !string.IsNullOrWhiteSpace(SearchText);
	public string ResultSummary => HasActiveFilters
		? string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.HistoryViewModel0OutcomesMatchTheCurrentFilters, VisibleItems.Count.ToString("N0", CultureInfo.CurrentCulture))
		: string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.HistoryViewModel0AvailableTimestampedOutcomes, VisibleItems.Count.ToString("N0", CultureInfo.CurrentCulture));
	public string? LoadError => CurrentError?.PrimaryMessage;
	public bool HasLoadError => CurrentError is not null;
	public ICommand RefreshCommand { get; }
	public ICommand ClearFiltersCommand { get; }
	public string RouteEyebrow => LibationAvalonia.Properties.Resources.HistoryEyebrow;
	public string RouteTitle => global::LibationAvalonia.Properties.Resources.RouteHistoryLabel;
	public string RouteSubtitle => LibationAvalonia.Properties.Resources.HistorySupportingText;
	public RouteCommandPresentation RoutePrimaryCommand => new(global::LibationAvalonia.Properties.Resources.DownloadsViewRefresh, RefreshCommand);
	public IReadOnlyList<RouteCommandPresentation> RouteSecondaryCommands => [];
	public RouteStatusPresentation RouteStatusBadge => new(ResultSummary,
		HasLoadError ? LibationStatusKind.NeedsAttention : LibationStatusKind.Completed);

	public async Task RefreshAsync()
	{
		if (disposed)
			return;
		if (!Dispatcher.UIThread.CheckAccess())
		{
			Dispatcher.UIThread.Post(() => _ = RefreshAsync());
			return;
		}
		refreshPending = false;
		if (refreshRunning)
		{
			refreshAgain = true;
			return;
		}

		refreshRunning = true;
		try
		{
			do
			{
				refreshAgain = false;
				IsLoading = true;
				try
				{
					var books = main.LibraryStats?.LibraryBooks
						.WithoutParents()
						.Select(book => new HistoryBookRaw(
							book.Book.TitleWithSubtitle,
							book.DateAdded,
							book.Book.UserDefinedItem.LastDownloaded))
						.ToArray() ?? [];
					var logs = main.ProcessQueue.LogEntries
						.Select(entry => new HistoryLogRaw(entry.LogDate, entry.LogMessage))
						.ToArray();
					allItems = await Task.Run(() => BuildItems(books, logs, lifetime.Token), lifetime.Token);
					RefreshFilterOptions();
					CurrentError = null;
					RaiseLoadErrorState();
					ApplyFilter();
				}
				catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
				{
					return;
				}
				catch (Exception ex)
				{
					CurrentError = UserFacingErrorFactory.FromException(
						ex,
						global::LibationAvalonia.Properties.Resources.HistoryViewModelRefreshTheAvailableHistoryProjection,
						global::LibationAvalonia.Properties.Resources.HistoryViewModelLibationCouldNotRefreshTheAvailableHistory);
					Serilog.Log.Logger.Error(
						ex,
						global::LibationAvalonia.Properties.Resources.HistoryViewModelFailedToBuildTheAvailableHistoryProjection,
						CurrentError.CorrelationId,
						CurrentError.Category.ToDisplayName());
					RaiseLoadErrorState();
				}
				finally
				{
					IsLoading = false;
					RaiseResultState();
				}
			}
			while (refreshAgain && !lifetime.IsCancellationRequested);
		}
		finally
		{
			refreshRunning = false;
		}
	}

	private static IReadOnlyList<HistoryItem> BuildItems(
		IReadOnlyList<HistoryBookRaw> books,
		IReadOnlyList<HistoryLogRaw> logs,
		CancellationToken cancellationToken)
	{
		var items = new List<HistoryItem>(books.Count * 2 + logs.Count);
		foreach (var book in books)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (book.DateAdded != default)
				items.Add(ToItem(book.DateAdded, null, global::LibationAvalonia.Properties.Resources.HistoryViewModelCatalogued, book.Title, global::LibationAvalonia.Properties.Resources.HistoryViewModelAddedToLibationSLibrary, global::LibationAvalonia.Properties.Resources.HistoryViewModelRecorded, LibationStatusKind.Completed));
			if (book.LastDownloaded is DateTime downloaded)
				items.Add(ToItem(downloaded, null, global::LibationAvalonia.Properties.Resources.DownloadsViewModelDownloaded, book.Title, global::LibationAvalonia.Properties.Resources.HistoryViewModelLastCompletedDownloadTimestampRecordedForThis, global::LibationAvalonia.Properties.Resources.CellarOverviewViewCompleted, LibationStatusKind.Completed));
		}
		foreach (var log in logs)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var (correlationId, detail) = ExtractCorrelation(log.Message);
			items.Add(ToItem(log.Timestamp, correlationId, global::LibationAvalonia.Properties.Resources.HistoryViewModelProcessingSession, global::LibationAvalonia.Properties.Resources.HistoryViewModelQueueActivity, detail, global::LibationAvalonia.Properties.Resources.HistoryViewModelRecorded, LibationStatusKind.Processing));
		}
		return items.OrderByDescending(item => item.Timestamp).ToArray();
	}

	private static HistoryItem ToItem(
		DateTime timestamp,
		Guid? correlationId,
		string action,
		string title,
		string detail,
		string result,
		LibationStatusKind status)
		=> new(timestamp, correlationId, timestamp.ToString("g", CultureInfo.CurrentCulture), action, title, detail, result, status);

	private static (Guid? CorrelationId, string Detail) ExtractCorrelation(string message)
	{
		const int idLength = 32;
		if (message.Length >= idLength + 2
			&& message[0] == '['
			&& message[idLength + 1] == ']'
			&& Guid.TryParseExact(message.Substring(1, idLength), "N", out var correlationId))
			return (correlationId, message[(idLength + 2)..].TrimStart());
		return (null, message);
	}

	private void RefreshFilterOptions()
	{
		changingFilters = true;
		try
		{
			ActionOptions = [AllActions, .. allItems.Select(item => item.Action).Distinct().OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase)];
			ResultOptions = [AllResults, .. allItems.Select(item => item.Result).Distinct().OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase)];
			if (!ActionOptions.Contains(SelectedAction))
				SelectedAction = AllActions;
			if (!ResultOptions.Contains(SelectedResult))
				SelectedResult = AllResults;
		}
		finally
		{
			changingFilters = false;
		}
	}

	private void ApplyFilter()
	{
		string query = SearchText.Trim();
		var fromDate = FromDate?.Date;
		var toDate = ToDate?.Date;
		var desired = allItems.Where(item =>
			(fromDate is null || item.Timestamp.Date >= fromDate.Value)
			&& (toDate is null || item.Timestamp.Date <= toDate.Value)
			&& (SelectedAction == AllActions || item.Action == SelectedAction)
			&& (SelectedResult == AllResults || item.Result == SelectedResult)
			&& (string.IsNullOrWhiteSpace(query)
				|| new[] { item.DateText, item.Action, item.Title, item.Detail, item.Result }
					.Any(value => value.Contains(query, StringComparison.CurrentCultureIgnoreCase))))
			.ToArray();
		ReconcileVisibleItems(desired);
		RaiseResultState();
	}

	private void ReconcileVisibleItems(IReadOnlyList<HistoryItem> desired)
	{
		for (int index = 0; index < desired.Count; index++)
		{
			if (index < VisibleItems.Count && VisibleItems[index] == desired[index])
				continue;

			int currentIndex = -1;
			for (int candidate = index + 1; candidate < VisibleItems.Count; candidate++)
			{
				if (VisibleItems[candidate] != desired[index])
					continue;
				currentIndex = candidate;
				break;
			}
			if (currentIndex >= 0)
				VisibleItems.Move(currentIndex, index);
			else
				VisibleItems.Insert(index, desired[index]);
		}
		while (VisibleItems.Count > desired.Count)
			VisibleItems.RemoveAt(VisibleItems.Count - 1);
	}

	private void ClearFilters()
	{
		changingFilters = true;
		try
		{
			SearchText = string.Empty;
			FromDate = null;
			ToDate = null;
			SelectedAction = AllActions;
			SelectedResult = AllResults;
		}
		finally
		{
			changingFilters = false;
		}
		ApplyFilter();
	}

	private void RaiseResultState()
	{
		this.RaisePropertyChanged(nameof(HasItems));
		this.RaisePropertyChanged(nameof(ShowEmpty));
		this.RaisePropertyChanged(nameof(HasActiveFilters));
		this.RaisePropertyChanged(nameof(ResultSummary));
		this.RaisePropertyChanged(nameof(RouteStatusBadge));
	}

	private void RaiseLoadErrorState()
	{
		this.RaisePropertyChanged(nameof(LoadError));
		this.RaisePropertyChanged(nameof(HasLoadError));
		this.RaisePropertyChanged(nameof(RouteStatusBadge));
	}

	private void Main_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(MainVM.LibraryStats))
			RequestRefresh();
	}

	private void LogEntries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RequestRefresh();

	private void RequestRefresh()
	{
		if (disposed)
			return;
		if (!Dispatcher.UIThread.CheckAccess())
		{
			Dispatcher.UIThread.Post(RequestRefresh);
			return;
		}
		if (!isActive)
		{
			refreshPending = true;
			return;
		}
		_ = RefreshAsync();
	}

	protected override void DisposeCore()
	{
		disposed = true;
		main.PropertyChanged -= Main_PropertyChanged;
		main.ProcessQueue.LogEntries.CollectionChanged -= LogEntries_CollectionChanged;
		lifetime.Cancel();
		lifetime.Dispose();
	}
}
