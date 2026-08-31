using Avalonia.Styling;
using LibationFileManager;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace LibationAvalonia.DesignSystem;

public static class ExperienceCatalog
{
	private static readonly FrozenDictionary<ExperienceStyle, ExperienceProfile> Profiles
		= new Dictionary<ExperienceStyle, ExperienceProfile>
		{
			[ExperienceStyle.Cellar] = new(
				ExperienceStyle.Cellar,
				"Cellar",
				ThemeVariant.Dark,
				new Uri("avares://Libation/DesignSystem/Palettes/CellarPalette.axaml"),
				DashboardLayoutKind.Cellar,
				QueuePresentationKind.CellarDock,
				true),
			[ExperienceStyle.TastingRoom] = new(
				ExperienceStyle.TastingRoom,
				"Tasting Room",
				ThemeVariant.Light,
				new Uri("avares://Libation/DesignSystem/Palettes/TastingRoomPalette.axaml"),
				DashboardLayoutKind.TastingRoom,
				QueuePresentationKind.TastingRoomCard,
				true),
			[ExperienceStyle.CurrentAvalonia] = new(
				ExperienceStyle.CurrentAvalonia,
				"Current Libation interface",
				ThemeVariant.Default,
				null,
				DashboardLayoutKind.Current,
				QueuePresentationKind.CurrentPane,
				false),
			[ExperienceStyle.HighContrast] = new(
				ExperienceStyle.HighContrast,
				"High Contrast",
				ThemeVariant.Dark,
				new Uri("avares://Libation/DesignSystem/Palettes/HighContrastPalette.axaml"),
				DashboardLayoutKind.Accessible,
				QueuePresentationKind.AccessibleDock,
				true),
		}.ToFrozenDictionary();

	public static IReadOnlyDictionary<ExperienceStyle, ExperienceProfile> All => Profiles;

	public static ExperienceProfile Resolve(ExperienceStyle requested, ThemeVariant actualSystemTheme)
	{
		if (requested == ExperienceStyle.FollowSystem)
			requested = actualSystemTheme == ThemeVariant.Dark
				? ExperienceStyle.Cellar
				: ExperienceStyle.TastingRoom;

		return Profiles.TryGetValue(requested, out var profile)
			? profile
			: Profiles[ExperienceStyle.CurrentAvalonia];
	}
}
