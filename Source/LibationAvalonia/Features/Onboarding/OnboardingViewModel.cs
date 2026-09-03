using Avalonia.Threading;
using LibationAvalonia.Features.Tools;
using LibationAvalonia.DesignSystem.Components;
using LibationAvalonia.Shell;
using LibationAvalonia.ViewModels;
using LibationFileManager;
using ReactiveUI;
using System;
using System.ComponentModel;
using System.Windows.Input;

namespace LibationAvalonia.Features.Onboarding;

public enum OnboardingProfileChoice
{
	FollowSystem,
	Cellar,
	TastingRoom,
	HighContrast,
	CurrentInterface,
}

public sealed record OnboardingCompletionRequest(int NewestEligibleTitleCount, AppRouteId Destination);
public sealed record OnboardingExitEventArgs(
	bool Completed,
	bool Skipped,
	OnboardingProfileChoice SelectedProfile,
	OnboardingCompletionRequest? CompletionRequest = null);

/// <summary>
/// Five-step, local-draft onboarding coordinator. Construction and preview never
/// write Configuration. Only explicit Finish commits appearance; Skip changes only
/// the established FirstLaunch marker.
/// </summary>
public sealed class OnboardingViewModel : SecondaryDestinationViewModel
{
	private const int LastStepIndex = 4;
	private const int FirstFlightTitleCount = 3;
	private readonly MainVM main;
	private readonly Configuration configuration;
	private OnboardingProfileChoice selectedProfile;
	private bool hasExplicitProfileChoice;
	private bool captureScanActive;
	private bool isCaptureProjection;
	private bool exitRequested;
	private bool disposed;

	public OnboardingViewModel(
		ILibationCommandAdapter commands,
		bool isManualReentry = false,
		Configuration? configuration = null)
	{
		ArgumentNullException.ThrowIfNull(commands);
		main = commands.Main;
		this.configuration = configuration ?? Configuration.Instance;
		IsManualReentry = isManualReentry;
		selectedProfile = isManualReentry ? ReadPersistedChoice(this.configuration) : OnboardingProfileChoice.FollowSystem;
		hasExplicitProfileChoice = isManualReentry;

		BackCommand = Track(ReactiveCommand.Create(Back));
		NextCommand = Track(ReactiveCommand.Create(Next));
		SkipCommand = Track(ReactiveCommand.Create(Skip));
		SelectFollowSystemCommand = Track(ReactiveCommand.Create(() => SelectProfile(OnboardingProfileChoice.FollowSystem)));
		SelectCellarCommand = Track(ReactiveCommand.Create(() => SelectProfile(OnboardingProfileChoice.Cellar)));
		SelectTastingRoomCommand = Track(ReactiveCommand.Create(() => SelectProfile(OnboardingProfileChoice.TastingRoom)));
		SelectHighContrastCommand = Track(ReactiveCommand.Create(() => SelectProfile(OnboardingProfileChoice.HighContrast)));
		SelectCurrentInterfaceCommand = Track(ReactiveCommand.Create(() => SelectProfile(OnboardingProfileChoice.CurrentInterface)));
		AddAccountCommand = CreateOwnerCommand(commands.AddAccountAsync, global::LibationAvalonia.Properties.Resources.OnboardingViewModelAddAnAccountDuringOnboarding, global::LibationAvalonia.Properties.Resources.OnboardingViewModelLibationCouldNotOpenAccountSetupYou);
		ManageAccountsCommand = CreateOwnerCommand(commands.ShowAccountsAsync, global::LibationAvalonia.Properties.Resources.OnboardingViewModelManageAccountsDuringOnboarding, global::LibationAvalonia.Properties.Resources.OnboardingViewModelLibationCouldNotOpenAccountManagementYou);
		OpenSettingsCommand = CreateOwnerCommand(commands.ShowSettingsAsync, global::LibationAvalonia.Properties.Resources.OnboardingViewModelChooseFoldersDuringOnboarding, global::LibationAvalonia.Properties.Resources.OnboardingViewModelLibationCouldNotOpenSettingsYouCan);
		LocateFilesCommand = CreateOwnerCommand(commands.LocateAudiobooksAsync, global::LibationAvalonia.Properties.Resources.OnboardingViewModelLocateExistingFilesDuringOnboarding, global::LibationAvalonia.Properties.Resources.OnboardingViewModelLibationCouldNotOpenTheFileLocator);
		ScanCommand = CreateOwnerCommand(commands.ScanLibraryAsync, global::LibationAvalonia.Properties.Resources.OnboardingViewModelScanAccountsDuringOnboarding, global::LibationAvalonia.Properties.Resources.OnboardingViewModelLibationCouldNotStartTheLibraryScan);
		AddNewestToFlightCommand = Track(ReactiveCommand.Create(FinishWithFirstFlight));

		main.PropertyChanged += Main_PropertyChanged;
		this.configuration.PropertyChanged += Configuration_PropertyChanged;
	}

	public event EventHandler<OnboardingExitEventArgs>? ExitRequested;
	public bool IsManualReentry { get; }
	public bool ShouldOfferAutomatically => !IsManualReentry && configuration.FirstLaunch;
	public bool HasExplicitProfileChoice
	{
		get => hasExplicitProfileChoice;
		private set
		{
			this.RaiseAndSetIfChanged(ref hasExplicitProfileChoice, value);
			RaiseProfileState();
		}
	}
	public bool NeedsExplicitProfileChoice { get => field; private set => this.RaiseAndSetIfChanged(ref field, value); }

	public int StepIndex { get => field; private set { this.RaiseAndSetIfChanged(ref field, value); RaiseStepState(); } }
	public int StepNumber => StepIndex + 1;
	public double Progress => StepNumber * 20d;
	public bool IsWelcomeStep => StepIndex == 0;
	public bool IsAccountStep => StepIndex == 1;
	public bool IsLocationStep => StepIndex == 2;
	public bool IsScanStep => StepIndex == 3;
	public bool IsFirstFlightStep => StepIndex == 4;
	public bool CanGoBack => StepIndex > 0;
	public string NextText => IsFirstFlightStep ? global::LibationAvalonia.Properties.Resources.OnboardingViewModelFinishWithoutAddingTitles : global::LibationAvalonia.Properties.Resources.OnboardingViewModelContinue;
	public string SkipText => IsManualReentry ? global::LibationAvalonia.Properties.Resources.OnboardingViewModelCloseWithoutChanges : global::LibationAvalonia.Properties.Resources.OnboardingViewModelSkipForNow;
	public string StepTitle => StepIndex switch
	{
		0 => global::LibationAvalonia.Properties.Resources.OnboardingViewModelChooseHowLibationShouldFeel,
		1 => global::LibationAvalonia.Properties.Resources.CellarOverviewViewConnectAnAudibleAccount,
		2 => global::LibationAvalonia.Properties.Resources.OnboardingViewModelChooseWhereLocalFilesBelong,
		3 => global::LibationAvalonia.Properties.Resources.OnboardingViewModelScanWithoutBlockingYourWork,
		_ => global::LibationAvalonia.Properties.Resources.OnboardingViewModelCreateYourFirstCurrentFlight,
	};
	public string StepSummary => string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.OnboardingViewModelStep0Of5, StepNumber);

	public OnboardingProfileChoice SelectedProfile
	{
		get => selectedProfile;
		private set
		{
			this.RaiseAndSetIfChanged(ref selectedProfile, value);
			RaiseProfileState();
		}
	}

	public bool IsFollowSystemSelected => HasExplicitProfileChoice && SelectedProfile == OnboardingProfileChoice.FollowSystem;
	public bool IsCellarSelected => HasExplicitProfileChoice && SelectedProfile == OnboardingProfileChoice.Cellar;
	public bool IsTastingRoomSelected => HasExplicitProfileChoice && SelectedProfile == OnboardingProfileChoice.TastingRoom;
	public bool IsHighContrastSelected => HasExplicitProfileChoice && SelectedProfile == OnboardingProfileChoice.HighContrast;
	public bool IsCurrentInterfaceSelected => HasExplicitProfileChoice && SelectedProfile == OnboardingProfileChoice.CurrentInterface;
	public string SelectedProfileText => SelectedProfile switch
	{
		OnboardingProfileChoice.FollowSystem => global::LibationAvalonia.Properties.Resources.OnboardingViewFollowSystem,
		OnboardingProfileChoice.TastingRoom => global::LibationAvalonia.Properties.Resources.OnboardingViewTastingRoom,
		OnboardingProfileChoice.HighContrast => global::LibationAvalonia.Properties.Resources.OnboardingViewHighContrast,
		OnboardingProfileChoice.CurrentInterface => global::LibationAvalonia.Properties.Resources.OnboardingViewCurrentLibationInterface,
		_ => global::LibationAvalonia.Properties.Resources.OnboardingViewCellar,
	};
	public string ProfileChoiceHelpText => IsManualReentry
		? string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.OnboardingViewModel0ReflectsTheCurrentSavedChoiceSelect, SelectedProfileText)
		: HasExplicitProfileChoice
			? string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.OnboardingViewModel0WillBeAppliedOnlyWhenYou, SelectedProfileText)
			: global::LibationAvalonia.Properties.Resources.OnboardingViewModelFollowSystemIsTheStartingPreviewChoose;

	public bool HasAccounts => main.AnyAccounts;
	public bool IsScanning => captureScanActive || main.ActivelyScanning;
	public bool CanStartScan => HasAccounts && !IsScanning && !isCaptureProjection;
	public string AccountStateText => HasAccounts ? main.AccountsCount == 1 ? global::LibationAvalonia.Properties.Resources.OnboardingViewModel1AccountConnected : string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.OnboardingViewModel0AccountsConnected, main.AccountsCount) : global::LibationAvalonia.Properties.Resources.OnboardingViewModelNoAudibleAccountConnected;
	public string ScanStateText => captureScanActive
		? global::LibationAvalonia.Properties.Resources.OnboardingViewModelReadingAccountCataloguesAndUpdatingTheLocal
		: IsScanning
			? main.ScanningText
			: main.LastSuccessfulScan is DateTimeOffset completed
				? string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.OnboardingViewModelLastScanCompleted0, completed.ToString("g"))
				: HasAccounts ? global::LibationAvalonia.Properties.Resources.OnboardingViewModelReadyToScanWhenYouChoose : global::LibationAvalonia.Properties.Resources.OnboardingViewModelConnectAnAccountBeforeScanning;
	public string ScanStageText => IsScanning
		? global::LibationAvalonia.Properties.Resources.OnboardingViewModelScanInProgress
		: main.LastSuccessfulScan is not null ? global::LibationAvalonia.Properties.Resources.OnboardingViewModelLastScanCompleted : HasAccounts ? global::LibationAvalonia.Properties.Resources.OnboardingViewModelReady : global::LibationAvalonia.Properties.Resources.OnboardingViewModelAccountRequired;
	public LibationStatusKind ScanStatus => IsScanning
		? LibationStatusKind.Processing
		: main.LastSuccessfulScan is not null ? LibationStatusKind.Completed : HasAccounts ? LibationStatusKind.DownloadPending : LibationStatusKind.Unavailable;
	public string ScanProgressAccessibleName => IsScanning ? string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.OnboardingViewModelLibraryScanInProgress0, ScanStateText) : ScanStateText;
	public string FirstFlightStateText => SelectedProfile == OnboardingProfileChoice.CurrentInterface
		? global::LibationAvalonia.Properties.Resources.OnboardingViewModelTheCurrentLibationInterfaceDoesNotHost
		: IsScanning
			? global::LibationAvalonia.Properties.Resources.OnboardingViewModelWaitForTheCurrentLibraryScanTo
		: global::LibationAvalonia.Properties.Resources.OnboardingViewModelLibationWillRequestUpToTheThree;
	public string FirstFlightActionText => global::LibationAvalonia.Properties.Resources.OnboardingViewModelAddTheThreeNewestTitlesToYour;
	public string FirstFlightActionHelpText => SelectedProfile == OnboardingProfileChoice.CurrentInterface
		? global::LibationAvalonia.Properties.Resources.OnboardingViewModelUnavailableForTheCurrentLibationInterfaceBecause
		: IsScanning
			? global::LibationAvalonia.Properties.Resources.OnboardingViewModelUnavailableUntilTheCurrentLibraryScanFinishes
		: global::LibationAvalonia.Properties.Resources.OnboardingViewModelAddsUpToThreeAvailableNonSeries;
	public bool CanRequestFirstFlight => !isCaptureProjection
		&& !IsScanning
		&& SelectedProfile != OnboardingProfileChoice.CurrentInterface;
	public bool CanShowFirstFlightAction => !IsScanning
		&& SelectedProfile != OnboardingProfileChoice.CurrentInterface;
	public string LocationStateText
	{
		get
		{
			var path = configuration.Books?.PathWithoutPrefix;
			return string.IsNullOrWhiteSpace(path)
				? global::LibationAvalonia.Properties.Resources.OnboardingViewModelAValidBooksLocationIsStillRequired
				: string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.OnboardingViewModelOpenCopiesWillUseTheConfiguredBooks, path);
		}
	}

	public ICommand BackCommand { get; }
	public ICommand NextCommand { get; }
	public ICommand SkipCommand { get; }
	public ICommand SelectFollowSystemCommand { get; }
	public ICommand SelectCellarCommand { get; }
	public ICommand SelectTastingRoomCommand { get; }
	public ICommand SelectHighContrastCommand { get; }
	public ICommand SelectCurrentInterfaceCommand { get; }
	public ICommand AddAccountCommand { get; }
	public ICommand ManageAccountsCommand { get; }
	public ICommand OpenSettingsCommand { get; }
	public ICommand LocateFilesCommand { get; }
	public ICommand ScanCommand { get; }
	public ICommand AddNewestToFlightCommand { get; }

	/// <summary>
	/// Capture-only projection. It changes no configuration, starts no scan, and
	/// emits no completion or Flight request.
	/// </summary>
	internal void PrepareCaptureState(int stepNumber, bool scanActive)
	{
		if (stepNumber is < 1 or > LastStepIndex + 1)
			throw new ArgumentOutOfRangeException(nameof(stepNumber), stepNumber, global::LibationAvalonia.Properties.Resources.OnboardingViewModelOnboardingCaptureStepMustBeFrom1);
		isCaptureProjection = true;
		captureScanActive = scanActive;
		StepIndex = stepNumber - 1;
		RaiseScanState();
		this.RaisePropertyChanged(nameof(CanRequestFirstFlight));
		this.RaisePropertyChanged(nameof(CanShowFirstFlightAction));
	}

	private void Back()
	{
		if (StepIndex > 0)
			StepIndex--;
	}

	private void Next()
	{
		if (isCaptureProjection || exitRequested)
			return;
		if (StepIndex == 0 && !HasExplicitProfileChoice)
		{
			NeedsExplicitProfileChoice = true;
			return;
		}
		if (StepIndex < LastStepIndex)
		{
			StepIndex++;
			return;
		}
		CommitSelection();
		exitRequested = true;
		ExitRequested?.Invoke(this, new(true, false, SelectedProfile));
	}

	private void FinishWithFirstFlight()
	{
		if (exitRequested || !IsFirstFlightStep || !CanRequestFirstFlight)
			return;
		CommitSelection();
		exitRequested = true;
		ExitRequested?.Invoke(
			this,
			new(
				true,
				false,
				SelectedProfile,
				new(FirstFlightTitleCount, AppRouteId.Library)));
	}

	private void Skip()
	{
		if (isCaptureProjection || exitRequested)
			return;
		configuration.FirstLaunch = false;
		exitRequested = true;
		ExitRequested?.Invoke(this, new(false, true, SelectedProfile));
	}

	private void CommitSelection()
	{
		var settings = configuration.GetContemporaryExperienceSettings();
		if (SelectedProfile == OnboardingProfileChoice.CurrentInterface)
		{
			configuration.SaveContemporaryExperienceSettings(
				settings with { UseContemporaryShell = false },
				completeFirstLaunch: true);
			return;
		}

		var style = SelectedProfile switch
		{
			OnboardingProfileChoice.Cellar => ExperienceStyle.Cellar,
			OnboardingProfileChoice.TastingRoom => ExperienceStyle.TastingRoom,
			OnboardingProfileChoice.HighContrast => ExperienceStyle.HighContrast,
			_ => ExperienceStyle.FollowSystem,
		};
		configuration.SaveContemporaryExperienceSettings(
			settings with { ExperienceStyle = style, UseContemporaryShell = true },
			completeFirstLaunch: true);
	}

	private static OnboardingProfileChoice ReadPersistedChoice(Configuration configuration)
	{
		if (!configuration.UseContemporaryShell)
			return OnboardingProfileChoice.CurrentInterface;
		return configuration.ExperienceStyle switch
		{
			ExperienceStyle.Cellar => OnboardingProfileChoice.Cellar,
			ExperienceStyle.TastingRoom => OnboardingProfileChoice.TastingRoom,
			ExperienceStyle.HighContrast => OnboardingProfileChoice.HighContrast,
			ExperienceStyle.CurrentAvalonia => OnboardingProfileChoice.CurrentInterface,
			_ => OnboardingProfileChoice.FollowSystem,
		};
	}

	private void SelectProfile(OnboardingProfileChoice profile)
	{
		if (isCaptureProjection)
			return;
		SelectedProfile = profile;
		HasExplicitProfileChoice = true;
		NeedsExplicitProfileChoice = false;
	}

	private void RaiseProfileState()
	{
		foreach (var property in new[]
		{
			nameof(IsFollowSystemSelected), nameof(IsCellarSelected), nameof(IsTastingRoomSelected),
			nameof(IsHighContrastSelected), nameof(IsCurrentInterfaceSelected),
			nameof(SelectedProfileText), nameof(ProfileChoiceHelpText), nameof(FirstFlightStateText),
			nameof(FirstFlightActionHelpText), nameof(CanRequestFirstFlight), nameof(CanShowFirstFlightAction),
		})
			this.RaisePropertyChanged(property);
	}

	private void RaiseStepState()
	{
		foreach (var property in new[]
		{
			nameof(StepNumber), nameof(Progress), nameof(IsWelcomeStep), nameof(IsAccountStep),
			nameof(IsLocationStep), nameof(IsScanStep), nameof(IsFirstFlightStep), nameof(CanGoBack),
			nameof(NextText), nameof(StepTitle), nameof(StepSummary),
		})
			this.RaisePropertyChanged(property);
	}

	private void Main_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (disposed)
			return;
		if (!string.IsNullOrEmpty(e.PropertyName)
			&& e.PropertyName is not nameof(MainVM.AccountsCount)
				and not nameof(MainVM.AnyAccounts)
				and not nameof(MainVM.ActivelyScanning)
				and not nameof(MainVM.ScanningText)
				and not nameof(MainVM.LastSuccessfulScan))
			return;
		if (!Dispatcher.UIThread.CheckAccess())
		{
			Dispatcher.UIThread.Post(() => Main_PropertyChanged(sender, e));
			return;
		}
		this.RaisePropertyChanged(nameof(HasAccounts));
		this.RaisePropertyChanged(nameof(AccountStateText));
		RaiseScanState();
	}

	private void RaiseScanState()
	{
		this.RaisePropertyChanged(nameof(IsScanning));
		this.RaisePropertyChanged(nameof(CanStartScan));
		this.RaisePropertyChanged(nameof(ScanStateText));
		this.RaisePropertyChanged(nameof(ScanStageText));
		this.RaisePropertyChanged(nameof(ScanStatus));
		this.RaisePropertyChanged(nameof(ScanProgressAccessibleName));
		this.RaisePropertyChanged(nameof(FirstFlightStateText));
		this.RaisePropertyChanged(nameof(FirstFlightActionHelpText));
		this.RaisePropertyChanged(nameof(CanRequestFirstFlight));
		this.RaisePropertyChanged(nameof(CanShowFirstFlightAction));
	}

	private void Configuration_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(Configuration.Books))
			this.RaisePropertyChanged(nameof(LocationStateText));
	}

	protected override void DisposeCore()
	{
		disposed = true;
		main.PropertyChanged -= Main_PropertyChanged;
		configuration.PropertyChanged -= Configuration_PropertyChanged;
	}
}
