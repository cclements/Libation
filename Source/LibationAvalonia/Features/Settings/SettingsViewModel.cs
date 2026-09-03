using LibationAvalonia.DesignSystem.Components;
using LibationAvalonia.Dialogs;
using LibationAvalonia.Features.Tools;
using LibationAvalonia.Properties;
using LibationAvalonia.Shell;
using LibationFileManager;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace LibationAvalonia.Features.Settings;

public sealed record SettingsCategoryItem(
	string Name,
	string Description,
	string SearchTerms,
	ICommand OpenCommand,
	string ActionLabel);

/// <summary>
/// Native contemporary appearance editor plus a truthful index of the five tabs
/// still owned by SettingsDialog.
/// </summary>
public sealed class SettingsViewModel : SecondaryDestinationViewModel, IRoutePresentation
{
	private readonly Configuration configuration;
	private readonly IReadOnlyList<SettingsCategoryItem> allCategories;

	public SettingsViewModel(ILibationCommandAdapter commands, Configuration? configuration = null)
	{
		ArgumentNullException.ThrowIfNull(commands);
		this.configuration = configuration ?? Configuration.Instance;
		Appearance = new(this.configuration);
		OpenSettingsCommand = CreateOwnerCommand(
			commands.ShowSettingsAsync,
			global::LibationAvalonia.Properties.Resources.SettingsViewModelOpenSettings,
			global::LibationAvalonia.Properties.Resources.SettingsViewModelLibationCouldNotOpenSettingsNoSettings);
		OpenImportantSettingsCommand = CreateSectionCommand(commands, SettingsDialogSection.Important, global::LibationAvalonia.Properties.Resources.SettingsViewModelImportantSettings);
		OpenImportLibraryCommand = CreateSectionCommand(commands, SettingsDialogSection.ImportLibrary, global::LibationAvalonia.Properties.Resources.SettingsViewModelImportLibrary);
		OpenDownloadDecryptCommand = CreateSectionCommand(commands, SettingsDialogSection.DownloadDecrypt, global::LibationAvalonia.Properties.Resources.SettingsViewModelDownloadDecrypt);
		OpenAudioFilesCommand = CreateSectionCommand(commands, SettingsDialogSection.AudioFiles, global::LibationAvalonia.Properties.Resources.SettingsViewModelAudioFileSettings);
		OpenAudiobookshelfCommand = CreateSectionCommand(commands, SettingsDialogSection.Audiobookshelf, global::LibationAvalonia.Properties.Resources.SettingsViewModelAudiobookshelf);
		OpenAccountsCommand = CreateOwnerCommand(
			commands.ShowAccountsAsync,
			global::LibationAvalonia.Properties.Resources.SettingsViewModelOpenAccountSettings,
			global::LibationAvalonia.Properties.Resources.AccountsViewModelLibationCouldNotOpenAccountManagementNo);
		OpenAboutCommand = CreateOwnerCommand(
			commands.ShowAboutAsync,
			global::LibationAvalonia.Properties.Resources.SettingsViewModelOpenAboutAndUpdateStatus,
			global::LibationAvalonia.Properties.Resources.SettingsViewModelLibationCouldNotOpenAboutTryThe);
		ApplyAppearanceCommand = CreateOwnerCommand(
			() => { Appearance.Apply(); return Task.CompletedTask; },
			global::LibationAvalonia.Properties.Resources.SettingsViewModelApplyContemporaryAppearance,
			global::LibationAvalonia.Properties.Resources.SettingsViewModelLibationCouldNotApplyTheAppearanceDraft);
		ResetAppearanceCommand = CreateOwnerCommand(
			() => { Appearance.ResetAndApply(); return Task.CompletedTask; },
			global::LibationAvalonia.Properties.Resources.SettingsViewModelResetContemporaryAppearance,
			global::LibationAvalonia.Properties.Resources.SettingsViewModelLibationCouldNotResetAppearanceTheSaved);
		RequestOnboardingCommand = Track(ReactiveCommand.Create(() => OnboardingRequested?.Invoke(this, EventArgs.Empty)));

		allCategories =
		[
			new(
				global::LibationAvalonia.Properties.Resources.SettingsViewModelImportantSettings,
				global::LibationAvalonia.Properties.Resources.SettingsViewModelBooksLocationStartupUpdatesAuthenticationTokenStorage,
				global::LibationAvalonia.Properties.Resources.SettingsViewModelGeneralBooksFolderStartupUpdatesPrivacyCredentials,
				OpenImportantSettingsCommand,
				global::LibationAvalonia.Properties.Resources.SettingsViewModelOpenImportantSettings),
			new(
				global::LibationAvalonia.Properties.Resources.SettingsViewModelImportLibrary,
				global::LibationAvalonia.Properties.Resources.SettingsViewModelAutomaticScansImportedTitleSummariesPodcastsEpisodes,
				global::LibationAvalonia.Properties.Resources.SettingsViewModelImportLibraryAutomaticAutoScanPodcastsEpisodes,
				OpenImportLibraryCommand,
				global::LibationAvalonia.Properties.Resources.SettingsViewModelOpenImportLibrary),
			new(
				global::LibationAvalonia.Properties.Resources.SettingsViewModelDownloadDecrypt2,
				global::LibationAvalonia.Properties.Resources.SettingsViewModelDailyDownloadLimitsUnavailableTitleBehaviorNaming,
				global::LibationAvalonia.Properties.Resources.SettingsViewModelDownloadDecryptLimitUnavailableNamingFoldersTemplates,
				OpenDownloadDecryptCommand,
				global::LibationAvalonia.Properties.Resources.SettingsViewModelOpenDownloadDecrypt),
			new(
				global::LibationAvalonia.Properties.Resources.SettingsViewModelAudioFileSettings,
				global::LibationAvalonia.Properties.Resources.SettingsViewModelAudioQualityCodecsOutputFormatChaptersCover,
				global::LibationAvalonia.Properties.Resources.SettingsViewModelAudioQualityCodecWidevineXheAacSpatial,
				OpenAudioFilesCommand,
				global::LibationAvalonia.Properties.Resources.SettingsViewModelOpenAudioFileSettings),
			new(
				global::LibationAvalonia.Properties.Resources.SettingsViewModelAudiobookshelf,
				global::LibationAvalonia.Properties.Resources.SettingsViewModelServerConnectionAPITokenRemoteLibraryAnd,
				global::LibationAvalonia.Properties.Resources.SettingsViewModelAudiobookshelfServerApiTokenIntegrationRemoteLibrary,
				OpenAudiobookshelfCommand,
				global::LibationAvalonia.Properties.Resources.SettingsViewModelOpenAudiobookshelf),
		];
		VisibleCategories = allCategories;
		this.configuration.PropertyChanged += Configuration_PropertyChanged;
	}

	public event EventHandler? OnboardingRequested;
	public AppearanceSettingsViewModel Appearance { get; }

	public string SearchText
	{
		get => field;
		set
		{
			this.RaiseAndSetIfChanged(ref field, value ?? string.Empty);
			ApplySearch();
		}
	} = string.Empty;

	public IReadOnlyList<SettingsCategoryItem> VisibleCategories { get => field; private set => this.RaiseAndSetIfChanged(ref field, value); } = [];
	public bool HasCategories => VisibleCategories.Count > 0;
	public string CategorySummary => string.IsNullOrWhiteSpace(SearchText)
		? global::LibationAvalonia.Properties.Resources.SettingsViewModelFiveSettingsTabs
		: VisibleCategories.Count == 1
			? global::LibationAvalonia.Properties.Resources.SettingsViewModel1SettingsTabMatchesTheCurrentSearch
			: string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.SettingsViewModel0SettingsTabsMatchTheCurrentSearch, VisibleCategories.Count);
	public string AppearanceProfileText => !configuration.UseContemporaryShell
		? global::LibationAvalonia.Properties.Resources.OnboardingViewCurrentLibationInterface
		: configuration.ExperienceStyle switch
		{
			ExperienceStyle.FollowSystem => global::LibationAvalonia.Properties.Resources.OnboardingViewFollowSystem,
			ExperienceStyle.TastingRoom => global::LibationAvalonia.Properties.Resources.OnboardingViewTastingRoom,
			ExperienceStyle.CurrentAvalonia => global::LibationAvalonia.Properties.Resources.OnboardingViewCurrentLibationInterface,
			ExperienceStyle.HighContrast => global::LibationAvalonia.Properties.Resources.OnboardingViewHighContrast,
			_ => global::LibationAvalonia.Properties.Resources.OnboardingViewCellar,
		};
	public string DensityText => configuration.DensityMode == DensityMode.Compact ? global::LibationAvalonia.Properties.Resources.SettingsViewModelCompact : global::LibationAvalonia.Properties.Resources.SettingsViewModelComfortable;
	public string DecorationText => configuration.DecorationLevel.ToString();
	public string MotionText => configuration.ReducedMotionPreference switch
	{
		ReducedMotionPreference.Reduce => global::LibationAvalonia.Properties.Resources.SettingsViewModelReducedMotionOn,
		ReducedMotionPreference.Full => global::LibationAvalonia.Properties.Resources.SettingsViewModelReducedMotionOff,
		_ => global::LibationAvalonia.Properties.Resources.SettingsViewModelFollowSystemMotion,
	};
	public string AppearanceSummary => string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.SettingsViewModel01Density2Decoration3, AppearanceProfileText, DensityText, DecorationText, MotionText);
	public string LegacyThemeSummary => Resources.SettingsClassicColorSummary;

	public ICommand OpenSettingsCommand { get; }
	public ICommand OpenImportantSettingsCommand { get; }
	public ICommand OpenImportLibraryCommand { get; }
	public ICommand OpenDownloadDecryptCommand { get; }
	public ICommand OpenAudioFilesCommand { get; }
	public ICommand OpenAudiobookshelfCommand { get; }
	public ICommand OpenAccountsCommand { get; }
	public ICommand OpenAboutCommand { get; }
	public ICommand ApplyAppearanceCommand { get; }
	public ICommand ResetAppearanceCommand { get; }
	public ICommand RequestOnboardingCommand { get; }
	public string RouteEyebrow => global::LibationAvalonia.Properties.Resources.SettingsViewModelPreferences;
	public string RouteTitle => global::LibationAvalonia.Properties.Resources.RouteSettingsLabel;
	public string RouteSubtitle => global::LibationAvalonia.Properties.Resources.SettingsViewModelAdjustContemporaryAppearanceHereOrOpenOne;
	public RouteCommandPresentation RoutePrimaryCommand => new(global::LibationAvalonia.Properties.Resources.SettingsViewApplyAppearance, ApplyAppearanceCommand);
	public IReadOnlyList<RouteCommandPresentation> RouteSecondaryCommands =>
	[
		new(global::LibationAvalonia.Properties.Resources.OnboardingViewOpenSettings, OpenSettingsCommand),
		new(global::LibationAvalonia.Properties.Resources.OnboardingViewManageAccounts, OpenAccountsCommand),
	];
	public RouteStatusPresentation RouteStatusBadge => new(AppearanceProfileText, LibationStatusKind.Completed);

	private ICommand CreateSectionCommand(ILibationCommandAdapter commands, SettingsDialogSection section, string sectionName)
		=> CreateOwnerCommand(
			() => commands.ShowSettingsAsync(section),
			string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.SettingsViewModelOpenThe0Tab, sectionName),
			string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.SettingsViewModelLibationCouldNotOpen0NoSettings, sectionName));

	private void ApplySearch()
	{
		string query = SearchText.Trim();
		VisibleCategories = string.IsNullOrWhiteSpace(query)
			? allCategories
			: allCategories.Where(category => new[] { category.Name, category.Description, category.SearchTerms }
				.Any(text => text.Contains(query, StringComparison.CurrentCultureIgnoreCase)))
				.ToArray();
		this.RaisePropertyChanged(nameof(HasCategories));
		this.RaisePropertyChanged(nameof(CategorySummary));
	}

	private void Configuration_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (!string.IsNullOrEmpty(e.PropertyName)
			&& e.PropertyName is not nameof(Configuration.UseContemporaryShell)
				and not nameof(Configuration.ExperienceStyle)
				and not nameof(Configuration.DensityMode)
				and not nameof(Configuration.DecorationLevel)
				and not nameof(Configuration.ReducedMotionPreference)
				and not nameof(Configuration.UseSystemTypography)
				and not nameof(Configuration.LibraryViewMode)
				and not nameof(Configuration.NavigationRailPreference)
				and not nameof(Configuration.ShowDecanterDock)
				and not nameof(Configuration.PersistFlightBetweenSessions))
			return;

		Appearance.SynchronizeFromConfiguration();
		foreach (var property in new[]
		{
			nameof(AppearanceProfileText), nameof(DensityText), nameof(DecorationText),
			nameof(MotionText), nameof(AppearanceSummary), nameof(RouteStatusBadge),
		})
			this.RaisePropertyChanged(property);
	}

	protected override void DisposeCore()
	{
		configuration.PropertyChanged -= Configuration_PropertyChanged;
		Appearance.Dispose();
	}
}
