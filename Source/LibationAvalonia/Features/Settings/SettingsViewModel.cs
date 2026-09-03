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
			"open settings",
			"Libation could not open Settings. No settings were changed.");
		OpenImportantSettingsCommand = CreateSectionCommand(commands, SettingsDialogSection.Important, "Important Settings");
		OpenImportLibraryCommand = CreateSectionCommand(commands, SettingsDialogSection.ImportLibrary, "Import Library");
		OpenDownloadDecryptCommand = CreateSectionCommand(commands, SettingsDialogSection.DownloadDecrypt, "Download/Decrypt");
		OpenAudioFilesCommand = CreateSectionCommand(commands, SettingsDialogSection.AudioFiles, "Audio File Settings");
		OpenAudiobookshelfCommand = CreateSectionCommand(commands, SettingsDialogSection.Audiobookshelf, "Audiobookshelf");
		OpenAccountsCommand = CreateOwnerCommand(
			commands.ShowAccountsAsync,
			"open account settings",
			"Libation could not open account management. No account data was changed.");
		OpenAboutCommand = CreateOwnerCommand(
			commands.ShowAboutAsync,
			"open About and update status",
			"Libation could not open About. Try the native application menu instead.");
		ApplyAppearanceCommand = CreateOwnerCommand(
			() => { Appearance.Apply(); return Task.CompletedTask; },
			"apply contemporary appearance",
			"Libation could not apply the appearance draft. The saved appearance was not partially changed.");
		ResetAppearanceCommand = CreateOwnerCommand(
			() => { Appearance.ResetAndApply(); return Task.CompletedTask; },
			"reset contemporary appearance",
			"Libation could not reset appearance. The saved appearance was not partially changed.");
		RequestOnboardingCommand = Track(ReactiveCommand.Create(() => OnboardingRequested?.Invoke(this, EventArgs.Empty)));

		allCategories =
		[
			new(
				"Important Settings",
				"Books location, startup updates, authentication-token storage, logging, display scaling, and Classic theme access.",
				"general books folder startup updates privacy credentials tokens logging display classic chardonnay",
				OpenImportantSettingsCommand,
				"Open Important Settings"),
			new(
				"Import Library",
				"Automatic scans, imported-title summaries, podcasts, episodes, and Plus titles.",
				"import library automatic auto scan podcasts episodes plus",
				OpenImportLibraryCommand,
				"Open Import Library"),
			new(
				"Download / Decrypt",
				"Daily download limits, unavailable-title behavior, naming templates, temporary files, and metadata sidecars.",
				"download decrypt limit unavailable naming folders templates temporary metadata",
				OpenDownloadDecryptCommand,
				"Open Download / Decrypt"),
			new(
				"Audio File Settings",
				"Audio quality, codecs, output format, chapters, cover art, and processing fix-ups.",
				"audio quality codec widevine xhe aac spatial mp3 m4b chapters cover processing",
				OpenAudioFilesCommand,
				"Open Audio File Settings"),
			new(
				"Audiobookshelf",
				"Server connection, API token, remote library, and destination folder.",
				"audiobookshelf server api token integration remote library folder advanced",
				OpenAudiobookshelfCommand,
				"Open Audiobookshelf"),
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
		? "Five Settings tabs"
		: VisibleCategories.Count == 1
			? "1 Settings tab matches the current search"
			: $"{VisibleCategories.Count} Settings tabs match the current search";
	public string AppearanceProfileText => !configuration.UseContemporaryShell
		? "Current Libation interface"
		: configuration.ExperienceStyle switch
		{
			ExperienceStyle.FollowSystem => "Follow System",
			ExperienceStyle.TastingRoom => "Tasting Room",
			ExperienceStyle.CurrentAvalonia => "Current Libation interface",
			ExperienceStyle.HighContrast => "High Contrast",
			_ => "Cellar",
		};
	public string DensityText => configuration.DensityMode == DensityMode.Compact ? "Compact" : "Comfortable";
	public string DecorationText => configuration.DecorationLevel.ToString();
	public string MotionText => configuration.ReducedMotionPreference switch
	{
		ReducedMotionPreference.Reduce => "Reduced motion on",
		ReducedMotionPreference.Full => "Reduced motion off",
		_ => "Follow system motion",
	};
	public string AppearanceSummary => $"{AppearanceProfileText} · {DensityText} density · {DecorationText} decoration · {MotionText}";
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
	public string RouteEyebrow => "Preferences";
	public string RouteTitle => "Settings";
	public string RouteSubtitle => "Adjust contemporary appearance here or open one exact Settings tab.";
	public RouteCommandPresentation RoutePrimaryCommand => new("Apply appearance", ApplyAppearanceCommand);
	public IReadOnlyList<RouteCommandPresentation> RouteSecondaryCommands =>
	[
		new("Open Settings", OpenSettingsCommand),
		new("Manage accounts", OpenAccountsCommand),
	];
	public RouteStatusPresentation RouteStatusBadge => new(AppearanceProfileText, LibationStatusKind.Completed);

	private ICommand CreateSectionCommand(ILibationCommandAdapter commands, SettingsDialogSection section, string sectionName)
		=> CreateOwnerCommand(
			() => commands.ShowSettingsAsync(section),
			$"open the {sectionName} tab",
			$"Libation could not open {sectionName}. No settings were changed.");

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
