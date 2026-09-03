using Avalonia.Threading;
using Avalonia.Input.Platform;
using DataLayer;
using LibationAvalonia.DesignSystem;
using LibationAvalonia.DesignSystem.Components;
using LibationAvalonia.Features.Flight;
using LibationAvalonia.Features.Library;
using LibationAvalonia.Features.Processing;
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
public sealed class DashboardViewModel : ViewModelBase, IDisposable, IRoutePresentation
{
	private static readonly TimeSpan RefreshDebounce = TimeSpan.FromMilliseconds(300);
	private static readonly string RecentAdditionsScope = global::LibationAvalonia.Properties.Resources.DashboardViewModelRecentAdditions;
	private static readonly string RecentCompletionsScope = global::LibationAvalonia.Properties.Resources.DashboardViewModelRecentlyCompleted;

	private static readonly string[] SnapshotPropertyNames =
	[
		nameof(Snapshot), nameof(HasDashboardData), nameof(IsLoading), nameof(ShowInitialError),
		nameof(TotalTitlesText), nameof(VisibleTitlesText), nameof(DownloadPendingText),
		nameof(CompletedText), nameof(ProcessingText), nameof(HasLocalStorage), nameof(LocalStorageText),
		nameof(TotalSizeText), nameof(AddedThisWeekDeltaText), nameof(DownloadsDeltaText), nameof(ProcessingDeltaText), nameof(CompletedDeltaText),
		nameof(StorageStatusText), nameof(AccountHealthText), nameof(AccountStatus), nameof(ScanStateText),
		nameof(ScanStatus), nameof(IsScanning), nameof(ShowNoAccount), nameof(ShowNeedsScan),
		nameof(ShowScanningEmpty), nameof(HasLibrary),
		nameof(IsOffline), nameof(IsScanStale), nameof(HasFailedJobs), nameof(FailedJobsText),
		nameof(CurrentFlightItems), nameof(FlightCountText),
		nameof(HasFlight), nameof(HasNoFlight), nameof(ShowAccountScanStrip), nameof(RecentAdditions), nameof(RecentCompletions), nameof(TastingLibraryItems),
		nameof(VisibleLibrarySummary), nameof(ErrorMessage), nameof(HasError),
		nameof(RouteStatusBadge),
	];

	private readonly IDashboardDataSource source;
	private readonly ILibationCommandAdapter commands;
	private readonly LibraryViewModel library;
	private readonly ProcessingViewModel processing;
	private readonly IDashboardNavigation navigation;
	private readonly CancellationTokenSource lifetime = new();
	private readonly SemaphoreSlim actionGate = new(1, 1);
	private readonly List<IDisposable> commandDisposables = [];
	private CancellationTokenSource? refreshDebounceCancellation;
	private DashboardSnapshot snapshot = DashboardSnapshot.Loading;
	private UserFacingError? refreshError;
	private UserFacingError? actionError;
	private bool refreshRunning;
	private bool refreshAgain;
	private bool isActive;
	private bool refreshPending = true;
	private bool disposed;
	private DashboardLayoutKind dashboardLayout = DashboardLayoutKind.Cellar;
	internal bool IsActive => isActive;

	public DashboardViewModel(
		ILibationCommandAdapter commands,
		IFlightService flight,
		LibraryViewModel library,
		ProcessingViewModel processing,
		IDashboardNavigation navigation,
		IDashboardSupplementSource? supplementSource = null)
	{
		this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
		ArgumentNullException.ThrowIfNull(flight);
		this.library = library ?? throw new ArgumentNullException(nameof(library));
		this.processing = processing ?? throw new ArgumentNullException(nameof(processing));
		this.navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
		source = new MainDashboardDataSource(commands.Main, flight, processing, supplementSource);
		source.Invalidated += Source_Invalidated;

		RefreshCommand = Track(ReactiveCommand.CreateFromTask(RefreshAsync));
		CopyTechnicalDetailsCommand = Track(ReactiveCommand.CreateFromTask(CopyTechnicalDetailsAsync));
		AddAccountCommand = Track(ReactiveCommand.CreateFromTask(() => RunActionAsync(
			global::LibationAvalonia.Properties.Resources.DashboardViewModelAddAnAccount,
			commands.AddAccountAsync,
			global::LibationAvalonia.Properties.Resources.DashboardViewModelLibationCouldNotOpenAccountSetupTry)));
		ManageAccountsCommand = Track(ReactiveCommand.CreateFromTask(() => RunActionAsync(
			global::LibationAvalonia.Properties.Resources.DashboardViewModelManageAccounts,
			commands.ShowAccountsAsync,
			global::LibationAvalonia.Properties.Resources.DashboardViewModelLibationCouldNotOpenAccountManagementTry)));
		ScanLibraryCommand = Track(ReactiveCommand.CreateFromTask(() => RunActionAsync(
			global::LibationAvalonia.Properties.Resources.DashboardViewModelScanTheLibrary,
			commands.ScanLibraryAsync,
			global::LibationAvalonia.Properties.Resources.DashboardViewModelLibationCouldNotStartTheLibraryScan)));
		LocateAudiobooksCommand = Track(ReactiveCommand.CreateFromTask(() => RunActionAsync(
			global::LibationAvalonia.Properties.Resources.DashboardViewModelLocateAudiobooks,
			commands.LocateAudiobooksAsync,
			global::LibationAvalonia.Properties.Resources.DownloadsViewModelLibationCouldNotOpenTheAudiobookLocator)));
		DownloadPendingCommand = Track(ReactiveCommand.CreateFromTask(() => RunActionAsync(
			global::LibationAvalonia.Properties.Resources.DashboardViewModelDownloadPendingTitles,
			commands.DownloadPendingBooksAsync,
			global::LibationAvalonia.Properties.Resources.DashboardViewModelLibationCouldNotQueueThePendingTitles)));
		DropAudiobooksCommand = Track(ReactiveCommand.CreateFromTask<IReadOnlyList<string>>(paths => RunActionAsync(
			global::LibationAvalonia.Properties.Resources.DashboardViewModelLocateDroppedAudiobooks,
			() => commands.LocateAudiobooksFromDropAsync(paths),
			global::LibationAvalonia.Properties.Resources.DashboardViewModelLibationCouldNotInspectTheDroppedLocation)));
		ApplySearchCommand = Track(ReactiveCommand.CreateFromTask(() => RunActionAsync(
			global::LibationAvalonia.Properties.Resources.DashboardViewModelSearchTheLibrary,
			() => commands.ApplyFilterAsync(SearchText),
			global::LibationAvalonia.Properties.Resources.DashboardViewModelLibationCouldNotApplyThatLibrarySearch)));
		OpenFilteredLibraryCommand = Track(ReactiveCommand.CreateFromTask(() => RunActionAsync(
			global::LibationAvalonia.Properties.Resources.DashboardViewModelSearchTheLibrary,
			async () =>
			{
				await commands.ApplyFilterAsync(SearchText);
				await navigation.OpenLibraryAsync();
			},
			global::LibationAvalonia.Properties.Resources.DashboardViewModelLibationCouldNotOpenTheFilteredLibrary)));
		OpenLibraryCommand = Track(ReactiveCommand.CreateFromTask(() => RunActionAsync(
			global::LibationAvalonia.Properties.Resources.DashboardViewModelOpenTheLibrary,
			navigation.OpenLibraryAsync,
			global::LibationAvalonia.Properties.Resources.DashboardViewModelLibationCouldNotOpenTheLibraryView)));
		OpenProcessingCommand = Track(ReactiveCommand.CreateFromTask(() => RunActionAsync(
			global::LibationAvalonia.Properties.Resources.DashboardViewModelOpenProcessing,
			navigation.OpenProcessingAsync,
			global::LibationAvalonia.Properties.Resources.DashboardViewModelLibationCouldNotOpenProcessingTryProcessing)));
		OpenBookCommand = Track(ReactiveCommand.CreateFromTask<DashboardBookItem>(OpenBookAsync));
		library.PropertyChanged += Library_PropertyChanged;
		processing.PropertyChanged += Processing_PropertyChanged;
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
		get => library.SearchText;
		set
		{
			value ??= string.Empty;
			if (string.Equals(library.SearchText, value, StringComparison.Ordinal))
				return;
			library.SearchText = value;
			this.RaisePropertyChanged();
		}
	}

	public bool IsRefreshing { get => field; private set => this.RaiseAndSetIfChanged(ref field, value); } = true;
	public bool IsActionBusy { get => field; private set => this.RaiseAndSetIfChanged(ref field, value); }
	public string SelectedLibraryScope
	{
		get => field;
		set
		{
			value = value == RecentCompletionsScope ? RecentCompletionsScope : RecentAdditionsScope;
			if (string.Equals(field, value, StringComparison.Ordinal))
				return;
			this.RaiseAndSetIfChanged(ref field, value);
			this.RaisePropertyChanged(nameof(TastingLibraryItems));
		}
	} = RecentAdditionsScope;
	public IReadOnlyList<string> LibraryScopes { get; } = [RecentAdditionsScope, RecentCompletionsScope];
	public bool HasDashboardData => Snapshot.IsDataReady;
	public bool IsLoading => !HasDashboardData && !HasError;
	public bool ShowInitialError => !HasDashboardData && HasError;
	public bool HasLibrary => HasDashboardData && Snapshot.TotalTitles > 0;
	public bool ShowNoAccount => HasDashboardData && !Snapshot.IsScanning && Snapshot.AccountCount == 0 && Snapshot.TotalTitles == 0;
	public bool ShowNeedsScan => HasDashboardData && !Snapshot.IsScanning && Snapshot.AccountCount > 0 && Snapshot.TotalTitles == 0;
	public bool ShowScanningEmpty => HasDashboardData && Snapshot.IsScanning && Snapshot.TotalTitles == 0;
	public bool IsScanning => Snapshot.IsScanning;
	public bool IsOffline => Snapshot.Supplement.Connectivity == DashboardConnectivityState.Offline;
	public bool IsScanStale => Snapshot.Supplement.ScanFreshness == DashboardScanFreshness.Stale;
	public bool HasFailedJobs => Snapshot.FailedJobCount > 0;
	public bool HasFlight => Snapshot.CurrentFlight.Count > 0;
	public bool HasNoFlight => !HasFlight;
	public bool ShowAccountScanStrip => Snapshot.AccountCount == 0 || Snapshot.IsScanning || IsScanStale;
	public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

	public string TotalTitlesText => FormatCount(Snapshot.TotalTitles);
	public string VisibleTitlesText => FormatCount(Snapshot.VisibleTitles);
	public string DownloadPendingText => FormatCount(Snapshot.DownloadPendingCount);
	public string CompletedText => FormatCount(Snapshot.CompletedCount);
	public string ProcessingText => FormatCount(processing.Active.Count + processing.Waiting.Count);
	public bool HasLocalStorage => Snapshot.Supplement.TotalLocalStorageBytes.HasValue;
	public string LocalStorageText => Snapshot.Supplement.TotalLocalStorageBytes is long bytes ? DiskSpaceHelper.FormatBytes(bytes) : string.Empty;
	public string TotalSizeText => HasLocalStorage ? LocalStorageText : global::LibationAvalonia.Properties.Resources.DashboardViewModelNotMeasured;
	public string AddedThisWeekDeltaText => Snapshot.AddedThisWeekCount == 0
		? global::LibationAvalonia.Properties.Resources.DashboardViewModelNoAdditionsThisWeek
		: Snapshot.AddedThisWeekCount == 1
			? global::LibationAvalonia.Properties.Resources.DashboardViewModel1ThisWeek
			: string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.DashboardViewModel0ThisWeek, FormatCount(Snapshot.AddedThisWeekCount));
	public string DownloadsDeltaText => Snapshot.ActiveDownloadCount == 0
		? global::LibationAvalonia.Properties.Resources.DashboardViewModelNoneRunning
		: Snapshot.ActiveDownloadCount == 1 ? global::LibationAvalonia.Properties.Resources.DashboardViewModel1Running : string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.DashboardViewModel0Running, FormatCount(Snapshot.ActiveDownloadCount));
	public string ProcessingDeltaText => string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.DashboardViewModel0Running1Queued, FormatCount(processing.Active.Count), FormatCount(processing.Waiting.Count));
	public string CompletedDeltaText => Snapshot.FailedJobCount == 0
		? global::LibationAvalonia.Properties.Resources.DashboardViewModelAllLooksGood
		: Snapshot.FailedJobCount == 1 ? global::LibationAvalonia.Properties.Resources.DashboardViewModel1Failed : string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.DashboardViewModel0Failed, FormatCount(Snapshot.FailedJobCount));
	public string StorageStatusText => Snapshot.Supplement.TotalLocalStorageBytes.HasValue ? global::LibationAvalonia.Properties.Resources.DashboardViewModelLocalAudiobookStorage : global::LibationAvalonia.Properties.Resources.DashboardViewModelStorageHasNotBeenMeasured;
	public string AccountHealthText => Snapshot.AccountCount switch
	{
		0 => global::LibationAvalonia.Properties.Resources.OnboardingViewModelNoAudibleAccountConnected,
		1 => global::LibationAvalonia.Properties.Resources.DashboardViewModel1AudibleAccountConnected,
		_ => string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.DashboardViewModel0AudibleAccountsConnected, Snapshot.AccountCount.ToString("N0", CultureInfo.CurrentCulture)),
	};
	public LibationStatusKind AccountStatus => Snapshot.AccountCount > 0 ? LibationStatusKind.Connected : LibationStatusKind.NeedsAttention;
	public string ScanStateText => Snapshot.IsScanning
		? Snapshot.ScanProgressText
		: Snapshot.Supplement.ScanFreshness == DashboardScanFreshness.Stale
			? global::LibationAvalonia.Properties.Resources.DashboardViewModelTheLastSuccessfulScanIsStale
			: Snapshot.Supplement.LastSuccessfulScan is DateTimeOffset scanned
				? string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.DashboardViewModelLastSuccessfulScan0G, scanned.ToLocalTime())
				: Snapshot.AccountCount > 0 && Snapshot.TotalTitles == 0
					? global::LibationAvalonia.Properties.Resources.DashboardViewModelReadyToScan
					: string.Empty;
	public LibationStatusKind ScanStatus => Snapshot.IsScanning
		? LibationStatusKind.Processing
		: IsScanStale || Snapshot.AccountCount == 0
			? LibationStatusKind.NeedsAttention
			: LibationStatusKind.Completed;
	public string FailedJobsText => Snapshot.FailedJobCount == 1
		? global::LibationAvalonia.Properties.Resources.DashboardViewModel1ProcessingJobFailedOpenProcessingFor
		: string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.DashboardViewModel0ProcessingJobsFailedOpenProcessingFor, Snapshot.FailedJobCount.ToString("N0", CultureInfo.CurrentCulture));
	public IReadOnlyList<DashboardBookItem> CurrentFlightItems => Snapshot.CurrentFlight;
	public IReadOnlyList<DashboardBookItem> RecentAdditions => Snapshot.RecentAdditions;
	public IReadOnlyList<DashboardBookItem> RecentCompletions => Snapshot.RecentCompletions;
	public IReadOnlyList<DashboardBookItem> TastingLibraryItems => SelectedLibraryScope == RecentCompletionsScope
		? RecentCompletions
		: RecentAdditions;
	public string FlightCountText => Snapshot.CurrentFlight.Count == 1
		? global::LibationAvalonia.Properties.Resources.CurrentFlightViewModel1Title
		: string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.CurrentFlightViewModel0Titles, Snapshot.CurrentFlight.Count.ToString("N0", CultureInfo.CurrentCulture));
	public string VisibleLibrarySummary => string.IsNullOrWhiteSpace(Snapshot.SearchText)
		? string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.DashboardViewModel0TitlesVisible, VisibleTitlesText)
		: string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.DashboardViewModel0TitlesMatchTheCurrentLibrarySearch, VisibleTitlesText);
	public UserFacingError? CurrentError => actionError ?? refreshError;
	public string? ErrorMessage => CurrentError?.PrimaryMessage ?? Snapshot.Supplement.ErrorMessage;
	public bool CanCopyTechnicalDetails => CurrentError is not null && App.MainWindow?.Clipboard is not null;

	public ICommand RefreshCommand { get; }
	public ICommand CopyTechnicalDetailsCommand { get; }
	public ICommand AddAccountCommand { get; }
	public ICommand ManageAccountsCommand { get; }
	public ICommand ScanLibraryCommand { get; }
	public ICommand LocateAudiobooksCommand { get; }
	public ICommand DownloadPendingCommand { get; }
	public ICommand DropAudiobooksCommand { get; }
	public ICommand ApplySearchCommand { get; }
	public ICommand OpenFilteredLibraryCommand { get; }
	public ICommand OpenLibraryCommand { get; }
	public ICommand OpenProcessingCommand { get; }
	public ICommand OpenBookCommand { get; }
	public string RouteEyebrow => dashboardLayout == DashboardLayoutKind.TastingRoom
		? global::LibationAvalonia.Properties.Resources.DashboardViewModelEditorialLibraryWorkspace
		: global::LibationAvalonia.Properties.Resources.LibraryViewModelLibraryWorkspace;
	public string RouteTitle => dashboardLayout == DashboardLayoutKind.TastingRoom ? global::LibationAvalonia.Properties.Resources.DashboardViewModelTodaySSelection : global::LibationAvalonia.Properties.Resources.DashboardViewModelTheCellar;
	public string RouteSubtitle => dashboardLayout == DashboardLayoutKind.TastingRoom
		? global::LibationAvalonia.Properties.Resources.DashboardViewModelWelcomeBackHereSWhatSHappening
		: global::LibationAvalonia.Properties.Resources.DashboardViewModelYourCuratedCollectionOfStoriesNeatlyAged;
	public RouteCommandPresentation? RoutePrimaryCommand => null;
	public IReadOnlyList<RouteCommandPresentation> RouteSecondaryCommands => [];
	public RouteStatusPresentation RouteStatusBadge => new(AccountHealthText, AccountStatus);

	public void SetProfile(ExperienceProfile profile)
	{
		var next = profile.DashboardLayout;
		if (next == dashboardLayout)
			return;
		dashboardLayout = next;
		this.RaisePropertyChanged(nameof(RouteEyebrow));
		this.RaisePropertyChanged(nameof(RouteTitle));
		this.RaisePropertyChanged(nameof(RouteSubtitle));
	}

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
				IsRefreshing = true;
				try
				{
					var next = await source.LoadAsync(lifetime.Token);
					refreshError = null;
					Snapshot = AttachPresentationCommands(next);
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
						global::LibationAvalonia.Properties.Resources.DashboardViewModelRefreshTheContemporaryOverview,
						global::LibationAvalonia.Properties.Resources.DashboardViewModelLibationCouldNotRefreshTheOverviewYour);
					Serilog.Log.Logger.Error(
						ex,
						global::LibationAvalonia.Properties.Resources.DashboardViewModelFailedToRefreshTheContemporaryOverviewCorrelation,
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
			global::LibationAvalonia.Properties.Resources.DashboardViewModelOpenABook,
			() => navigation.OpenBookAsync(item.LibraryBook),
			global::LibationAvalonia.Properties.Resources.DashboardViewModelLibationCouldNotOpenThatBookOpen);
	}

	private DashboardSnapshot AttachPresentationCommands(DashboardSnapshot next)
	{
		DashboardBookItem Attach(DashboardBookItem item) => item with { OpenCommand = OpenBookCommand };
		return next with
		{
				RecentAdditions = next.RecentAdditions.Select(Attach).ToArray(),
			RecentCompletions = next.RecentCompletions.Select(Attach).ToArray(),
			CurrentFlight = next.CurrentFlight.Select(Attach).ToArray(),
		};
	}

	private void Library_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(LibraryViewModel.SearchText))
			this.RaisePropertyChanged(nameof(SearchText));
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
				global::LibationAvalonia.Properties.Resources.DashboardViewModelDashboardActionFailedDashboardActionCorrelationIDCorrelationId,
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
	{
		if (disposed)
			return;
		if (!Dispatcher.UIThread.CheckAccess())
		{
			Dispatcher.UIThread.Post(() => Source_Invalidated(sender, e));
			return;
		}
		if (!isActive)
		{
			refreshPending = true;
			return;
		}
		ScheduleRefresh();
	}

	private void ScheduleRefresh()
	{
		refreshDebounceCancellation?.Cancel();
		var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
		refreshDebounceCancellation = cancellation;
		_ = RefreshAfterDebounceAsync(cancellation);
	}

	private async Task RefreshAfterDebounceAsync(CancellationTokenSource cancellation)
	{
		try
		{
			await Task.Delay(RefreshDebounce, cancellation.Token);
			if (!ReferenceEquals(refreshDebounceCancellation, cancellation))
				return;
			refreshDebounceCancellation = null;
			await RefreshAsync();
		}
		catch (OperationCanceledException)
		{
		}
		finally
		{
			cancellation.Dispose();
		}
	}

	private void Processing_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		if (string.IsNullOrEmpty(e.PropertyName)
			|| e.PropertyName is nameof(ProcessingViewModel.HasActive)
				or nameof(ProcessingViewModel.HasWaiting)
				or nameof(ProcessingViewModel.ActiveText)
				or nameof(ProcessingViewModel.SummaryText)
				or nameof(ProcessingViewModel.Progress))
		{
			this.RaisePropertyChanged(nameof(ProcessingText));
			this.RaisePropertyChanged(nameof(ProcessingDeltaText));
		}
	}

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
				global::LibationAvalonia.Properties.Resources.DashboardViewModelUnableToCopyOverviewDiagnosticsCorrelationID,
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
		library.PropertyChanged -= Library_PropertyChanged;
		processing.PropertyChanged -= Processing_PropertyChanged;
		refreshDebounceCancellation?.Cancel();
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
