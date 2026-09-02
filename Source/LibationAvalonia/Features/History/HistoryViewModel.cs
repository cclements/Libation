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
	private readonly MainVM main;
	private readonly CancellationTokenSource lifetime = new();
	private IReadOnlyList<HistoryItem> allItems = [];
	private bool refreshRunning;
	private bool refreshAgain;
	private bool isActive;
	private bool refreshPending = true;
	private bool disposed;
	internal bool IsActive => isActive;

	public HistoryViewModel(MainVM main)
	{
		this.main = main ?? throw new ArgumentNullException(nameof(main));
		RefreshCommand = Track(ReactiveCommand.CreateFromTask(RefreshAsync));
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
			ApplyFilter();
		}
	} = string.Empty;

	public IReadOnlyList<HistoryItem> VisibleItems { get => field; private set => this.RaiseAndSetIfChanged(ref field, value); } = [];
	public bool IsLoading { get => field; private set => this.RaiseAndSetIfChanged(ref field, value); } = true;
	public bool HasItems => VisibleItems.Count > 0;
	public bool ShowEmpty => !IsLoading && !HasItems;
	public string ResultSummary => string.IsNullOrWhiteSpace(SearchText)
		? $"{VisibleItems.Count.ToString("N0", CultureInfo.CurrentCulture)} available timestamped outcomes"
		: $"{VisibleItems.Count.ToString("N0", CultureInfo.CurrentCulture)} outcomes match the current search";
	public string? LoadError => CurrentError?.PrimaryMessage;
	public bool HasLoadError => CurrentError is not null;
	public ICommand RefreshCommand { get; }
	public string RouteEyebrow => LibationAvalonia.Properties.Resources.HistoryEyebrow;
	public string RouteTitle => "History";
	public string RouteSubtitle => LibationAvalonia.Properties.Resources.HistorySupportingText;
	public RouteCommandPresentation RoutePrimaryCommand => new("Refresh", RefreshCommand);
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
					CurrentError = LibationAvalonia.DesignSystem.UserFacingErrorFactory.FromException(
						ex,
						"refresh the available History projection",
						"Libation could not refresh the available history timestamps. No library or queue data was changed.");
					Serilog.Log.Logger.Error(
						ex,
						"Failed to build the available History projection. Correlation ID: {CorrelationId}. Category: {ErrorCategory}",
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
				items.Add(ToItem(book.DateAdded, "Catalogued", book.Title, "Added to Libation’s library.", "Recorded", LibationStatusKind.DownloadPending));
			if (book.LastDownloaded is DateTime downloaded)
				items.Add(ToItem(downloaded, "Downloaded", book.Title, "Last completed download timestamp recorded for this title.", "Completed", LibationStatusKind.Completed));
		}
		foreach (var log in logs)
		{
			cancellationToken.ThrowIfCancellationRequested();
			items.Add(ToItem(log.Timestamp, "Processing session", "Queue activity", log.Message, "Recorded", LibationStatusKind.Processing));
		}
		return items.OrderByDescending(item => item.Timestamp).ToArray();
	}

	private static HistoryItem ToItem(DateTime timestamp, string action, string title, string detail, string result, LibationStatusKind status)
		=> new(timestamp, timestamp.ToString("g", CultureInfo.CurrentCulture), action, title, detail, result, status);

	private void ApplyFilter()
	{
		string query = SearchText.Trim();
		VisibleItems = string.IsNullOrWhiteSpace(query)
			? allItems
			: allItems.Where(item => new[] { item.DateText, item.Action, item.Title, item.Detail, item.Result }
				.Any(value => value.Contains(query, StringComparison.CurrentCultureIgnoreCase)))
				.ToArray();
		RaiseResultState();
	}

	private void RaiseResultState()
	{
		this.RaisePropertyChanged(nameof(HasItems));
		this.RaisePropertyChanged(nameof(ShowEmpty));
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
