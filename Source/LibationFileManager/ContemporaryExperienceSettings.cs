using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.ComponentModel;

namespace LibationFileManager;

/// <summary>
/// Persisted experience choices. These types deliberately contain no Avalonia
/// references so the CLI, WinForms application, and configuration layer can
/// continue to read Settings.json without loading the Avalonia UI.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ExperienceStyle
{
	[Description("Follow system appearance")]
	FollowSystem = 0,
	[Description("Cellar")]
	Cellar = 1,
	[Description("Tasting Room")]
	TastingRoom = 2,
	[Description("Current Libation interface")]
	CurrentAvalonia = 3,
	[Description("High Contrast")]
	HighContrast = 4,
}

[JsonConverter(typeof(StringEnumConverter))]
public enum DensityMode
{
	[Description("Comfortable")]
	Comfortable = 0,
	[Description("Compact")]
	Compact = 1,
}

[JsonConverter(typeof(StringEnumConverter))]
public enum DecorationLevel
{
	[Description("Full")]
	Full = 0,
	[Description("Reduced")]
	Reduced = 1,
	[Description("Off")]
	Off = 2,
}

[JsonConverter(typeof(StringEnumConverter))]
public enum ReducedMotionPreference
{
	[Description("Follow system")]
	FollowSystem = 0,
	[Description("On")]
	Reduce = 1,
	[Description("Off")]
	Full = 2,
}

[JsonConverter(typeof(StringEnumConverter))]
public enum LibraryViewMode
{
	Details = 0,
	Gallery = 1,
}

[JsonConverter(typeof(StringEnumConverter))]
public enum NavigationRailPreference
{
	Automatic = 0,
	Expanded = 1,
	Compact = 2,
}

/// <summary>
/// One persisted contemporary-experience transaction. Shell activation is kept
/// last so configuration notifications cannot expose a partially applied profile.
/// </summary>
public sealed record ContemporaryExperienceSettings(
	ExperienceStyle ExperienceStyle,
	DensityMode DensityMode,
	DecorationLevel DecorationLevel,
	ReducedMotionPreference ReducedMotionPreference,
	bool UseSystemTypography,
	LibraryViewMode LibraryViewMode,
	NavigationRailPreference NavigationRailPreference,
	bool ShowDecanterDock,
	bool PersistFlightBetweenSessions,
	bool UseContemporaryShell);
