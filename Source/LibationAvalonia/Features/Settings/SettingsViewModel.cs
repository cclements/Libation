using LibationAvalonia.Features.Tools;
using LibationAvalonia.Properties;
using LibationAvalonia.Shell;
using LibationFileManager;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;

namespace LibationAvalonia.Features.Settings;

public sealed record SettingsCategoryItem(
	string Name,
	string Description,
	string SearchTerms,
	ICommand OpenCommand,
	string ActionLabel = "Open settings");

/// <summary>
/// Searchable category index and live appearance summary over the existing settings
/// dialog and Configuration owner. It does not duplicate the established editor's
/// validation, preview/cancel, or legacy Chardonnay override behavior.
/// </summary>
public sealed class SettingsViewModel : SecondaryDestinationViewModel
{
	private readonly Configuration configuration;
	private readonly IReadOnlyList<SettingsCategoryItem> allCategories;

	public SettingsViewModel(ILibationCommandAdapter commands, Configuration? configuration = null)
	{
		ArgumentNullException.ThrowIfNull(commands);
		this.configuration = configuration ?? Configuration.Instance;
		OpenSettingsCommand = CreateOwnerCommand(
			commands.ShowSettingsAsync,
			"open settings",
			"Libation could not open Settings. No settings were changed.");
		OpenAccountsCommand = CreateOwnerCommand(
			commands.ShowAccountsAsync,
			"open account settings",
			"Libation could not open account management. No account data was changed.");
		OpenAboutCommand = CreateOwnerCommand(
			commands.Main.ShowAboutAsync,
			"open About and update status",
			"Libation could not open About. Try the native application menu instead.");
		RequestOnboardingCommand = Track(ReactiveCommand.Create(() => OnboardingRequested?.Invoke(this, EventArgs.Empty)));

		allCategories =
		[
			new("General", "Startup, notifications, and core application behavior.", "startup notifications basic", OpenSettingsCommand),
			new("Appearance", "Experience profile, density, decoration, motion, typography, library view, and legacy palette overrides.", "cellar tasting room current interface high contrast theme chardonnay density motion typography", OpenSettingsCommand),
			new("Accounts", "Audible accounts, marketplaces, sign-in, and authorization.", "audible login authentication marketplace", OpenAccountsCommand, "Manage accounts"),
			new("Download", "Acquisition format, quality, and download behavior.", "download audible format quality", OpenSettingsCommand),
			new("Processing", "Output format, chapter handling, concurrency, and conversion.", "decrypt mp3 m4b queue concurrency chapter", OpenSettingsCommand),
			new("Naming and folders", "Books location, temporary files, templates, and file naming.", "path folder books location template filename", OpenSettingsCommand),
			new("Metadata", "Tags, cover art, metadata files, and file metadata behavior.", "tag cover metadata", OpenSettingsCommand),
			new("Automation", "Automatic scans and post-scan processing behavior.", "auto scan automatic schedule", OpenSettingsCommand),
			new("Updates", Resources.SettingsUpdatesDescription, "upgrade version release", OpenSettingsCommand),
			new("Privacy", "Logging, account storage, and integrations that may send library information elsewhere.", "credential token log telemetry service", OpenSettingsCommand),
			new("Advanced", "Legacy palette editor, integrations, and expert settings.", "advanced chardonnay audiobook shelf legacy", OpenSettingsCommand),
		];
		VisibleCategories = allCategories;
		this.configuration.PropertyChanged += Configuration_PropertyChanged;
	}

	public event EventHandler? OnboardingRequested;

	public string SearchText
	{
		get => field;
		set
		{
			this.RaiseAndSetIfChanged(ref field, value ?? string.Empty);
			ApplySearch();
		}
	} = string.Empty;

	public IReadOnlyList<SettingsCategoryItem> VisibleCategories { get => field; private set => this.RaiseAndSetIfChanged(ref field, value); }
	public bool HasCategories => VisibleCategories.Count > 0;
	public string CategorySummary => string.IsNullOrWhiteSpace(SearchText)
		? "All stable settings categories"
		: $"{VisibleCategories.Count} categories match the current search";
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
	public string TypographyText => configuration.UseSystemTypography ? "System typography" : "Profile typography";
	public string LibraryViewText => configuration.LibraryViewMode.ToString();
	public string NavigationText => configuration.NavigationRailPreference.ToString();
	public string AppearanceSummary => $"{AppearanceProfileText} · {DensityText} density · {DecorationText} decoration · {MotionText}";
	public string LegacyThemeSummary => Resources.SettingsClassicColorSummary;

	public ICommand OpenSettingsCommand { get; }
	public ICommand OpenAccountsCommand { get; }
	public ICommand OpenAboutCommand { get; }
	public ICommand RequestOnboardingCommand { get; }

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
				and not nameof(Configuration.NavigationRailPreference))
			return;

		foreach (var property in new[]
		{
			nameof(AppearanceProfileText), nameof(DensityText), nameof(DecorationText), nameof(MotionText),
			nameof(TypographyText), nameof(LibraryViewText), nameof(NavigationText), nameof(AppearanceSummary),
		})
			this.RaisePropertyChanged(property);
	}

	protected override void DisposeCore() => configuration.PropertyChanged -= Configuration_PropertyChanged;
}
