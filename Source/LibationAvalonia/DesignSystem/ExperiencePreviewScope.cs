using Avalonia.Controls;

namespace LibationAvalonia.DesignSystem;

/// <summary>
/// A local resource scope that lets separate preview controls render different
/// profiles without changing application resources or domain state.
/// </summary>
public sealed record ExperiencePreviewScope(ExperienceProfile Profile, ThemeVariantScope Host)
{
	public IResourceDictionary Resources => Host.Resources;
}
