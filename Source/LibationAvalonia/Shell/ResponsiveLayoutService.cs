using LibationAvalonia.DesignSystem;
using LibationAvalonia.ViewModels;
using LibationFileManager;
using ReactiveUI;
using System;

namespace LibationAvalonia.Shell;

public enum DesktopLayoutClass
{
	Wide,
	Standard,
	Compact,
	Narrow,
	BelowSupportedMinimum,
}

public enum NavigationRailState
{
	Expanded,
	Compact,
	Overlay,
}

public enum ContextualPaneState
{
	Persistent,
	Collapsed,
	Drawer,
}

public enum QueueSurfaceState
{
	Dock,
	Card,
	CompactBar,
	Drawer,
}

public sealed record ResponsiveLayout(
	DesktopLayoutClass LayoutClass,
	NavigationRailState NavigationRail,
	ContextualPaneState ContextualPane,
	QueueSurfaceState QueueSurface,
	bool ShowDecorativeHero,
	bool IsBelowSupportedMinimum);

/// <summary>
/// Deterministic projection of the plan's authoritative 1360/1080/840/720
/// breakpoints. It owns no view models and therefore never recreates domain state.
/// </summary>
public sealed class ResponsiveLayoutService : ViewModelBase
{
	private ResponsiveLayout current = Resolve(
		1360,
		768,
		DashboardLayoutKind.TastingRoom,
		DecorationLevel.Full,
		NavigationRailPreference.Automatic);

	public ResponsiveLayout Current
	{
		get => current;
		private set => this.RaiseAndSetIfChanged(ref current, value);
	}

	public void Update(
		double effectiveWidth,
		double effectiveHeight,
		ExperienceProfile profile,
		DecorationLevel decoration,
		NavigationRailPreference navigationPreference)
	{
		var next = Resolve(effectiveWidth, effectiveHeight, profile.DashboardLayout, decoration, navigationPreference);
		if (next != Current)
			Current = next;
	}

	public static ResponsiveLayout Resolve(
		double effectiveWidth,
		double effectiveHeight,
		DashboardLayoutKind dashboard,
		DecorationLevel decoration,
		NavigationRailPreference navigationPreference = NavigationRailPreference.Automatic)
	{
		if (!double.IsFinite(effectiveWidth) || !double.IsFinite(effectiveHeight))
			throw new ArgumentOutOfRangeException(nameof(effectiveWidth), "Window dimensions must be finite.");

		var layoutClass = effectiveWidth switch
		{
			>= 1360 => DesktopLayoutClass.Wide,
			>= 1080 => DesktopLayoutClass.Standard,
			>= 840 => DesktopLayoutClass.Compact,
			>= 720 => DesktopLayoutClass.Narrow,
			_ => DesktopLayoutClass.BelowSupportedMinimum,
		};

		var automaticRail = layoutClass switch
		{
			DesktopLayoutClass.Wide or DesktopLayoutClass.Standard => NavigationRailState.Expanded,
			DesktopLayoutClass.Compact => NavigationRailState.Compact,
			_ => NavigationRailState.Overlay,
		};
		// Narrow layouts always overlay so an explicit preference cannot squeeze the
		// primary text below the plan's supported width. At wider classes the saved
		// preference is authoritative.
		var rail = automaticRail == NavigationRailState.Overlay
			? NavigationRailState.Overlay
			: navigationPreference switch
			{
				NavigationRailPreference.Expanded => NavigationRailState.Expanded,
				NavigationRailPreference.Compact => NavigationRailState.Compact,
				_ => automaticRail,
			};
		var pane = layoutClass switch
		{
			DesktopLayoutClass.Wide => ContextualPaneState.Persistent,
			DesktopLayoutClass.Standard => ContextualPaneState.Collapsed,
			_ => ContextualPaneState.Drawer,
		};
		var queue = (dashboard, layoutClass) switch
		{
			(_, DesktopLayoutClass.BelowSupportedMinimum) => QueueSurfaceState.Drawer,
			(_, DesktopLayoutClass.Narrow or DesktopLayoutClass.Compact) => QueueSurfaceState.CompactBar,
			(DashboardLayoutKind.TastingRoom, _) => QueueSurfaceState.Card,
			_ => QueueSurfaceState.Dock,
		};
		bool showHero = decoration != DecorationLevel.Off
			&& dashboard == DashboardLayoutKind.TastingRoom
			&& layoutClass is DesktopLayoutClass.Wide or DesktopLayoutClass.Standard;

		return new(
			layoutClass,
			rail,
			pane,
			queue,
			showHero,
			layoutClass == DesktopLayoutClass.BelowSupportedMinimum || effectiveHeight < 560);
	}
}
