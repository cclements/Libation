using Avalonia.Controls;
using Avalonia.Styling;

namespace LibationAvalonia.DesignSystem;

/// <summary>
/// A local resource scope that lets separate preview controls render different
/// profiles without changing application resources or domain state.
/// </summary>
public sealed record ExperiencePreviewScope(
	ExperienceProfile Profile,
	IResourceDictionary Resources,
	ThemeVariant RequestedThemeVariant);
