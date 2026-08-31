using Avalonia.Threading;
using Avalonia.Input.Platform;
using DataLayer;
using LibationAvalonia.DesignSystem;
using LibationAvalonia.DesignSystem.Components;
using LibationAvalonia.Features.Flight;
using LibationAvalonia.Shell;
using LibationAvalonia.ViewModels;
using LibationFileManager;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace LibationAvalonia.Features.Overview;

/// <summary>
/// One stateful presentation model shared by the Cellar and Tasting Room compositions.
/// Construct it once for the shell lifetime so profile switching preserves search text,
/// expanded regions, live queue state, and Current Flight.
/// </summary>
public sealed class DashboardViewModel : ViewModelBase, IDisposable
{
	private static readonly string[] SnapshotPropertyNames =
	[
		nameof(Snapshot), nameof(HasDashboardData), nameof(IsLoading), nameof(ShowInitialError),
		nameof(TotalTitlesText), nameof(VisibleTitlesText), nameof(DownloadPendingText), nameof(DownloadedText),
		nameof(CompletedText), nameof(ProcessingText), nameof(LocalStorageText), nameof(StorageSavedText),
		nameof(StorageStatusText), nameof(AccountHealthText), nameof(AccountStatus), nameof(ScanStateText),
		nameof(ScanStatus), nameof(IsScanning), nameof(ShowNoAccount), nameof(ShowNeedsScan),
		nameof(ShowScanningEmpty), nameof(HasLibrary), nameof(HasCatalogWithoutLocalCopies),
		nameof(IsOffline), nameof(IsScanStale), nameof(HasFailedJobs), nameof(FailedJobsText),
		nameof(HasActiveWork), nameof(NoActiveWork), nameof(QueueSummaryText), nameof(QueueActiveText),
		nameof(QueueProgress), nameof(CurrentFlightItems), nameof(FlightCountText), nameof(FlightDurationText), nameof(FlightWarningText),
		nameof(ProcessFlightActionText), nameof(FlightUndoActionText),
		nameof(HasFlight), nameof(VisibleLibraryItems), nameof(RecentAdditions), nameof(RecentCompletions),
		nameof(ActiveQueueItems), nameof(FailedJobs), nameof(HasUpdateState), nameof(UpdateStateText),
		nameof(VisibleLibrarySummary), nameof(ErrorMessage), nameof(HasError), nameof(HasAttention),
	];

	private readonly IDashboardDataSource source;
	private readonly ILibationCommandAdapter commands;
	private readonly IFlightService flight;
	private readonly CurrentFlightViewModel currentFlight;
	private readonly IDashboardNavigation navigation;
	private readonly CancellationTokenSource lifetime = new();
	private readonly SemaphoreSlim actionGate = new(1, 1);
	private readonly List<IDisposable> commandDisposables = [];
	private DashboardSnapshot snapshot = DashboardSnapshot.Loading;
	private UserFacingError? refreshError;
	private UserFacingError? actionError;
	private bool refreshRunning;
	private bool refreshAgain;
	private bool hasLoadedSearchText;
	private bool disposed;

	public DashboardViewModel(
		ILibationCommandAdapter commands,
		IFlightService flight,
		CurrentFlightViewModel currentFlight,
		IDashboardNavigation navigation,
		IDashboardSupplementSource? supplementSource = null)
	{
		this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
		this.flight = flight ?? throw new ArgumentNullException(nameof(flight));
		this.currentFlight = currentFlight ?? throw new ArgumentNullException(nameof(currentFlight));
		this.navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
		source = new MainDashboardDataSource(commands.Main, flight, supplementSource);
		source.Invalidated += Source_Invalidated;

		RefreshCommand = Track(ReactiveCommand.CreateFromTask(RefreshAsync));
		CopyTechnicalDetailsCommand = Track(ReactiveCommand.CreateFromTask(CopyTechnicalDetailsAsync));
		AddAccountCommand = Track(ReactiveCommand.CreateFromTask(() => RunActionAsync(
			"add an account",
			commands.AddAccountAsync,
			"Libation could not open account setup. Try Accounts from the navigation.")));
		ManageAccountsCommand = Track(ReactiveCommand.CreateFromTask(() => RunActionAsync(
			"manage accounts",
			commands.ShowAccountsAsync,
			"Libation could not open account management. Try Accounts from the navigation.")));
		ScanLibraryCommand = Track(ReactiveCommand.CreateFromTask(() => RunActionAsync(
			"scan the library",
			commands.ScanLibraryAsync,
			"Libation could not start the library scan. Check the account connection and try again.")));
		LocateAudiobooksCommand = Track(ReactiveCommand.CreateFromTask(() => RunActionAsync(
			"locate audiobooks",
			commands.LocateAudiobooksAsync,
			"Libation could not open the audiobook locator. Check the configured Books location and try again.")));
		DropAudiobooksCommand = Track(ReactiveCommand.CreateFromTask<IReadOnlyList<string>>(paths => RunActionAsync(
			"locate dropped audiobooks",
			() => commands.LocateAudiobooksFromDropAsync(paths),
			"Libation could not inspect the dropped location. Use Browse and choose a local folder instead.")));
		ApplySearchCommand = Track(ReactiveCommand.CreateFromTask(() => RunActionAsync(
			"search the library",
			() => commands.ApplyFilterAsync(SearchText),
			"Libation could not apply that library search. Clear the search and try again.")));
		OpenLibraryCommand = Track(ReactiveCommand.CreateFromTask(() => RunActionAsync(
			"open the library",
			navigation.OpenLibraryAsync,
			"Libation could not open the Library view. Try Library from the navigation.")));
		OpenProcessingCommand = Track(ReactiveCommand.CreateFromTask(() => RunActionAsync(
			"open processing",
			navigation.OpenProcessingAsync,
			"Libation could not open Processing. Try Processing from the navigation.")));
		OpenBookCommand = Track(ReactiveCommand.CreateFromTask<DashboardBookItem>(OpenBookAsync));
		ProcessFlightCommand = currentFlight.ProcessCommand;
		ClearFlightCommand = currentFlight.ClearCommand;
		UndoFlightCommand = currentFlight.UndoCommand;
		CancelAllProcessingCommand = Track(ReactiveCommand.CreateFromTask(() => RunActionAsync(
			"cancel active processing",
			() => commands.Main.ProcessQueue.CancelAllAsync(),
			"Libation could not cancel all active processing. Open Processing to review the remaining work.")));
		ToggleFlightExpandedCommand = Track(ReactiveCommand.Create(() => IsFlightExpanded = !IsFlightExpanded));
		currentFlight.PropertyChanged += CurrentFlight_PropertyChanged;

		_ = RefreshAsync();
	}

	public DashboardSnapshot Snapshot
	{
		get => snapshot;
		private set
		{
			snapshot = value;
			foreach (var propertyName in SnapshotPropertyNames)
				this.RaisePropertyChanged(propertyName);
		}
	}

	public string SearchText
	{
		get => field;
		set => this.RaiseAndSetIfChanged(ref field, value ?? string.Empty);
	} = string.Empty;

	public bool IsRefreshing { get => field; private set => this.RaiseAndSetIfChanged(ref field, value); } = true;
	public bool IsActionBusy { get => field; private set => this.RaiseAndSetIfChanged(ref field, value); }
	public bool IsQueueExpanded { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
	public bool IsFlightExpanded { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
	public CurrentFlightViewModel CurrentFlight => currentFlight;

	public bool HasDashboardData => Snapshot.IsDataReady;
	public bool IsLoading => !HasDashboardData && !HasError;
	public bool ShowInitialError => !HasDashboardData && HasError;
	public bool HasLibrary => HasDashboardData && Snapshot.TotalTitles > 0;
	public bool ShowNoAccount => HasDashboardData && !Snapshot.IsScanning && Snapshot.AccountCount == 0 && Snapshot.TotalTitles == 0;
	public bool ShowNeedsScan => HasDashboardData && !Snapshot.IsScanning && Snapshot.AccountCount > 0 && Snapshot.TotalTitles == 0;
	public bool ShowScanningEmpty => HasDashboardData && Snapshot.IsScanning && Snapshot.TotalTitles == 0;
	public bool HasCatalogWithoutLocalCopies => HasLibrary && Snapshot.CompletedCount == 0;
	public bool IsScanning => Snapshot.IsScanning;
	public bool IsOffline => Snapshot.Supplement.Connectivity == DashboardConnectivityState.Offline;
	public bool IsScanStale => Snapshot.Supplement.ScanFreshness == DashboardScanFreshness.Stale;
	public bool HasFailedJobs => Snapshot.FailedJobCount > 0;
	public bool HasActiveWork => Snapshot.ActiveProcessingCount + Snapshot.QueuedProcessingCount > 0;
	public bool NoActiveWork => !HasActiveWork;
	public bool HasFlight => Snapshot.CurrentFlight.Count > 0;
	public bool HasUpdateState => !string.IsNullOrWhiteSpace(Snapshot.Supplement.ApplicationUpdateState);
	public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
	public bool HasAttention => HasError || IsOffline || IsScanStale || HasFailedJobs || HasCatalogWithoutLocalCopies;

	public string TotalTitlesText => FormatCount(Snapshot.TotalTitles);
	public string VisibleTitlesText => FormatCount(Snapshot.VisibleTitles);
	public string DownloadPendingText => FormatCount(Snapshot.DownloadPendingCount);
	public string DownloadedText => FormatCount(Snapshot.DownloadedCount);
	public string CompletedText => FormatCount(Snapshot.CompletedCount);
	public string ProcessingText => FormatCount(Snapshot.ActiveProcessingCount + Snapshot.QueuedProcessingCount);
	public string LocalStorageText => Snapshot.Supplement.TotalLocalStorageBytes is long bytes ? DiskSpaceHelper.FormatBytes(bytes) : "Not available";
	public string StorageSavedText => Snapshot.Supplement.StorageSavedBytes is long bytes ? DiskSpaceHelper.FormatBytes(bytes) : "Not available";
	public string StorageStatusText => Snapshot.Supplement.TotalLocalStorageBytes.HasValue ? "Local audiobook storage" : "Storage has not been measured";
	public string AccountHealthText => Snapshot.AccountCount switch
	{
		0 => "No Audible account connected",
		1 => "1 Audible account connected",
		_ => $"{Snapshot.AccountCount.ToString("N0", CultureInfo.CurrentCulture)} Audible accounts connected",
	};
	public LibationStatusKind AccountStatus => Snapshot.AccountCount > 0 ? LibationStatusKind.Connected : LibationStatusKind.NeedsAttention;
	public string ScanStateText => Snapshot.IsScanning
		? Snapshot.ScanProgressText
		: Snapshot.Supplement.ScanFreshness == DashboardScanFreshness.Stale
			? "The last successful scan is stale"
			: Snapshot.Supplement.LastSuccessfulScan is DateTimeOffset scanned
				? $"Last successful scan: {scanned.ToLocalTime():g}"
				: Snapshot.AccountCount > 0 && Snapshot.TotalTitles == 0
					? "Ready to scan"
					: "Last scan time is not available";
	public LibationStatusKind ScanStatus => Snapshot.IsScanning
		? LibationStatusKind.Processing
		: IsScanStale || Snapshot.AccountCount == 0
			? LibationStatusKind.NeedsAttention
			: LibationStatusKind.Completed;
	public string FailedJobsText => Snapshot.FailedJobCount == 1
		? "1 processing job failed. Open Processing for details."
		: $"{Snapshot.FailedJobCount.ToString("N0", CultureInfo.CurrentCulture)} processing jobs failed. Open Processing for details.";
	public string QueueSummaryText => HasActiveWork
		? $"{Snapshot.ActiveProcessingCount.ToString("N0", CultureInfo.CurrentCulture)} active, {Snapshot.QueuedProcessingCount.ToString("N0", CultureInfo.CurrentCulture)} queued"
		: "No active processing work";
	public string QueueActiveText => Snapshot.ActiveQueue.FirstOrDefault()?.Title ?? "Queue is idle";
	public double QueueProgress => Snapshot.QueueProgress;
	public IReadOnlyList<DashboardBookItem> CurrentFlightItems => Snapshot.CurrentFlight;
	public IReadOnlyList<DashboardBookItem> VisibleLibraryItems => Snapshot.VisibleLibrary;
	public IReadOnlyList<DashboardBookItem> RecentAdditions => Snapshot.RecentAdditions;
	public IReadOnlyList<DashboardBookItem> RecentCompletions => Snapshot.RecentCompletions;
	public IReadOnlyList<DashboardQueueItem> ActiveQueueItems => Snapshot.ActiveQueue;
	public IReadOnlyList<DashboardQueueItem> FailedJobs => Snapshot.FailedJobs;
	public string FlightCountText => Snapshot.CurrentFlight.Count == 1
		? "1 title"
		: $"{Snapshot.CurrentFlight.Count.ToString("N0", CultureInfo.CurrentCulture)} titles";
	public string FlightDurationText => currentFlight.DurationText;
	public bool FocusFlightWarning => currentFlight.FocusWarning;
	public string? FlightWarningText => !string.IsNullOrWhiteSpace(currentFlight.WarningText)
		? currentFlight.WarningText
		: Snapshot.CurrentFlight.Count == 0
			? "No titles selected. Add titles from the Library."
			: Snapshot.HiddenFlightCount > 0
				? $"{Snapshot.HiddenFlightCount.ToString("N0", CultureInfo.CurrentCulture)} selected title(s) are hidden by the current library filter."
				: null;
	public string FlightOutputProfileText => currentFlight.OutputProfileText;
	public string ProcessFlightActionText => currentFlight.ProcessActionText;
	public string? FlightUndoActionText => currentFlight.UndoActionText;
	public string UpdateStateText => Snapshot.Supplement.ApplicationUpdateState ?? string.Empty;
	public string VisibleLibrarySummary => string.IsNullOrWhiteSpace(Snapshot.SearchText)
		? $"{VisibleTitlesText} titles visible"
		: $"{VisibleTitlesText} titles match the current library search";
	public UserFacingError? CurrentError => actionError ?? refreshError;
	public string? ErrorMessage => CurrentError?.PrimaryMessage ?? Snapshot.Supplement.ErrorMessage;
	public bool CanCopyTechnicalDetails => CurrentError is not null && App.MainWindow?.Clipboard is not null;

	public ICommand RefreshCommand { get; }
	public ICommand CopyTechnicalDetailsCommand { get; }
	public ICommand AddAccountCommand { get; }
	public ICommand ManageAccountsCommand { get; }
	public ICommand ScanLibraryCommand { get; }
	public ICommand LocateAudiobooksCommand { get; }
	public ICommand DropAudiobooksCommand { get; }
	public ICommand ApplySearchCommand { get; }
	public ICommand OpenLibraryCommand { get; }
	public ICommand OpenProcessingCommand { get; }
	public ICommand OpenBookCommand { get; }
	public ICommand ProcessFlightCommand { get; }
	public ICommand ClearFlightCommand { get; }
	public ICommand UndoFlightCommand { get; }
	public ICommand CancelAllProcessingCommand { get; }
	public ICommand ToggleFlightExpandedCommand { get; }

	public async Task RefreshAsync()
	{
		if (disposed)
			return;
		if (!Dispatcher.UIThread.CheckAccess())
		{
			Dispatcher.UIThread.Post(() => _ = RefreshAsync());
			return;
		}
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
				IsRefreshing = true;
				try
				{
					var next = await source.LoadAsync(lifetime.Token);
					refreshError = null;
					Snapshot = AttachPresentationCommands(next);
					if (!hasLoadedSearchText)
					{
						SearchText = next.SearchText;
						hasLoadedSearchText = true;
					}
					RaiseErrorProperties();
				}
				catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
				{
					return;
				}
				catch (Exception ex)
				{
					refreshError = UserFacingErrorFactory.FromException(
						ex,
						"refresh the contemporary overview",
						"Libation could not refresh the overview. Your library data was not changed.");
					Serilog.Log.Logger.Error(
						ex,
						"Failed to refresh the contemporary overview. Correlation ID: {CorrelationId}. Category: {ErrorCategory}",
						refreshError.CorrelationId,
						refreshError.Category.ToDisplayName());
					RaiseErrorProperties();
				}
				finally
				{
					IsRefreshing = false;
				}
			}
			while (refreshAgain && !disposed);
		}
		finally
		{
			refreshRunning = false;
		}
	}

	private async Task OpenBookAsync(DashboardBookItem item)
	{
		if (item is null)
			return;
		await RunActionAsync(
			"open a book",
			() => navigation.OpenBookAsync(item.LibraryBook),
			"Libation could not open that book. Open it from the Library instead.");
	}

	private DashboardSnapshot AttachPresentationCommands(DashboardSnapshot next)
	{
		DashboardBookItem Attach(DashboardBookItem item) => item with { OpenCommand = OpenBookCommand };
		return next with
		{
			VisibleLibrary = next.VisibleLibrary.Select(Attach).ToArray(),
			RecentAdditions = next.RecentAdditions.Select(Attach).ToArray(),
			RecentCompletions = next.RecentCompletions.Select(Attach).ToArray(),
			CurrentFlight = next.CurrentFlight.Select(Attach).ToArray(),
		};
	}

	private void CurrentFlight_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		if (string.IsNullOrEmpty(e.PropertyName)
			|| e.PropertyName is nameof(CurrentFlightViewModel.WarningText)
				or nameof(CurrentFlightViewModel.ProcessActionText)
				or nameof(CurrentFlightViewModel.OutputProfileText)
				or nameof(CurrentFlightViewModel.UndoActionText)
				or nameof(CurrentFlightViewModel.DurationText)
				or nameof(CurrentFlightViewModel.FocusWarning))
		{
			this.RaisePropertyChanged(nameof(FlightWarningText));
			this.RaisePropertyChanged(nameof(ProcessFlightActionText));
			this.RaisePropertyChanged(nameof(FlightOutputProfileText));
			this.RaisePropertyChanged(nameof(FlightUndoActionText));
			this.RaisePropertyChanged(nameof(FlightDurationText));
			this.RaisePropertyChanged(nameof(FocusFlightWarning));
		}
	}

	private async Task RunActionAsync(string actionName, Func<Task> action, string userError)
	{
		if (disposed || !await actionGate.WaitAsync(0, lifetime.Token))
			return;
		try
		{
			IsActionBusy = true;
			actionError = null;
			RaiseErrorProperties();
			await action();
			await RefreshAsync();
		}
		catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
		{
		}
		catch (Exception ex)
		{
			actionError = UserFacingErrorFactory.FromException(ex, actionName, userError);
			Serilog.Log.Logger.Error(
				ex,
				"Dashboard action failed: {DashboardAction}. Correlation ID: {CorrelationId}. Category: {ErrorCategory}",
				actionName,
				actionError.CorrelationId,
				actionError.Category.ToDisplayName());
			RaiseErrorProperties();
		}
		finally
		{
			IsActionBusy = false;
			actionGate.Release();
		}
	}

	private void Source_Invalidated(object? sender, EventArgs e)
		=> Dispatcher.UIThread.Post(() => _ = RefreshAsync());

	private async Task CopyTechnicalDetailsAsync()
	{
		var error = CurrentError;
		if (error is null || App.MainWindow?.Clipboard is not { } clipboard)
			return;
		try
		{
			await clipboard.SetTextAsync(error.ToDiagnosticText());
		}
		catch (Exception ex)
		{
			Serilog.Log.Logger.Warning(
				"Unable to copy overview diagnostics. Correlation ID: {CorrelationId}. {TechnicalDetails}",
				error.CorrelationId,
				UserFacingErrorFactory.Scrub(ex.ToString()));
		}
	}

	private void RaiseErrorProperties()
	{
		this.RaisePropertyChanged(nameof(ErrorMessage));
		this.RaisePropertyChanged(nameof(CurrentError));
		this.RaisePropertyChanged(nameof(CanCopyTechnicalDetails));
		this.RaisePropertyChanged(nameof(HasError));
		this.RaisePropertyChanged(nameof(IsLoading));
		this.RaisePropertyChanged(nameof(ShowInitialError));
		this.RaisePropertyChanged(nameof(HasAttention));
	}

	private T Track<T>(T command) where T : class, ICommand
	{
		if (command is IDisposable disposable)
			commandDisposables.Add(disposable);
		return command;
	}

	private static string FormatCount(int value) => value.ToString("N0", CultureInfo.CurrentCulture);

	public void Dispose()
	{
		if (disposed)
			return;
		disposed = true;
		source.Invalidated -= Source_Invalidated;
		currentFlight.PropertyChanged -= CurrentFlight_PropertyChanged;
		lifetime.Cancel();
		source.Dispose();
		foreach (var command in commandDisposables)
			command.Dispose();
		commandDisposables.Clear();
		// A routed owner action can outlive the shell teardown. Do not dispose its
		// semaphore while the action's finally block can still release it.
		lifetime.Dispose();
	}
}
