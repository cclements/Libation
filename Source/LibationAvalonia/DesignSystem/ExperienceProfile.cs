using Avalonia.Styling;
using LibationFileManager;
using System;

namespace LibationAvalonia.DesignSystem;

public enum DashboardLayoutKind
{
	Current,
	Cellar,
	TastingRoom,
	Accessible,
}

public enum QueuePresentationKind
{
	CurrentPane,
	CellarDock,
	TastingRoomCard,
	AccessibleDock,
}

/// <summary>A resolved, renderable experience. FollowSystem resolves to Cellar or Tasting Room before this is consumed.</summary>
public sealed record ExperienceProfile(
	ExperienceStyle Style,
	string DisplayName,
	ThemeVariant ThemeVariant,
	Uri? PaletteResource,
	DashboardLayoutKind DashboardLayout,
	QueuePresentationKind QueuePresentation,
	bool UsesContemporaryShell);

public sealed class ExperienceProfileChangedEventArgs(ExperienceProfile previous, ExperienceProfile current) : EventArgs
{
	public ExperienceProfile Previous { get; } = previous;
	public ExperienceProfile Current { get; } = current;
}
