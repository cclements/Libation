using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using Avalonia.Styling;
using LibationFileManager;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace LibationAvalonia.DesignSystem;

public sealed class ThemeResourceValidator
{
	public static readonly FrozenSet<string> RequiredColorKeys = new HashSet<string>
	{
		"Libation.Color.Canvas",
		"Libation.Color.Sidebar",
		"Libation.Color.Surface",
		"Libation.Color.SurfaceRaised",
		"Libation.Color.SurfaceSunken",
		"Libation.Color.SurfaceInteractive",
		"Libation.Color.TextPrimary",
		"Libation.Color.TextSecondary",
		"Libation.Color.TextTertiary",
		"Libation.Color.TextOnAccent",
		"Libation.Color.BorderSubtle",
		"Libation.Color.BorderStrong",
		"Libation.Color.AccentPrimary",
		"Libation.Color.AccentSecondary",
		"Libation.Color.AccentHover",
		"Libation.Color.AccentPressed",
		"Libation.Color.Focus",
		"Libation.Color.Selection",
		"Libation.Color.Success",
		"Libation.Color.Warning",
		"Libation.Color.Danger",
		"Libation.Color.Info",
		"Libation.Color.ProgressTrack",
		"Libation.Color.ProgressFill",
		"Libation.Color.CoverPlaceholder",
	}.ToFrozenSet(StringComparer.Ordinal);

	public static readonly FrozenSet<string> RequiredBrushKeys = new HashSet<string>
	{
		"Libation.Brush.Canvas",
		"Libation.Brush.Sidebar",
		"Libation.Brush.Surface",
		"Libation.Brush.SurfaceRaised",
		"Libation.Brush.SurfaceSunken",
		"Libation.Brush.SurfaceInteractive",
		"Libation.Brush.TextPrimary",
		"Libation.Brush.TextSecondary",
		"Libation.Brush.TextTertiary",
		"Libation.Brush.TextOnAccent",
		"Libation.Brush.BorderSubtle",
		"Libation.Brush.BorderStrong",
		"Libation.Brush.AccentPrimary",
		"Libation.Brush.AccentSecondary",
		"Libation.Brush.AccentHover",
		"Libation.Brush.AccentPressed",
		"Libation.Brush.Focus",
		"Libation.Brush.Selection",
		"Libation.Brush.Success",
		"Libation.Brush.Warning",
		"Libation.Brush.Danger",
		"Libation.Brush.Info",
		"Libation.Brush.ProgressTrack",
		"Libation.Brush.ProgressFill",
		"Libation.Brush.CoverPlaceholder",
	}.ToFrozenSet(StringComparer.Ordinal);

	public static readonly FrozenDictionary<string, Type> RequiredSharedTokens = new Dictionary<string, Type>
	{
		["Libation.Space.1"] = typeof(double),
		["Libation.Space.2"] = typeof(double),
		["Libation.Space.3"] = typeof(double),
		["Libation.Space.4"] = typeof(double),
		["Libation.Space.5"] = typeof(double),
		["Libation.Space.6"] = typeof(double),
		["Libation.Space.7"] = typeof(double),
		["Libation.Space.8"] = typeof(double),
		["Libation.Thickness.3"] = typeof(Thickness),
		["Libation.Thickness.4"] = typeof(Thickness),
		["Libation.Thickness.5"] = typeof(Thickness),
		["Libation.Radius.Small"] = typeof(CornerRadius),
		["Libation.Radius.Medium"] = typeof(CornerRadius),
		["Libation.Radius.Large"] = typeof(CornerRadius),
		["Libation.ControlHeight.Compact"] = typeof(double),
		["Libation.ControlHeight.Default"] = typeof(double),
		["Libation.ControlHeight.Prominent"] = typeof(double),
		["Libation.Navigation.WidthExpanded"] = typeof(double),
		["Libation.Navigation.WidthCompact"] = typeof(double),
		["Libation.Content.MaxReadingWidth"] = typeof(double),
		["Libation.Shadow.Low"] = typeof(BoxShadows),
		["Libation.Shadow.Medium"] = typeof(BoxShadows),
		["Libation.Font.UI"] = typeof(FontFamily),
		["Libation.Font.Display"] = typeof(FontFamily),
		["Libation.FontSize.Caption"] = typeof(double),
		["Libation.FontSize.BodySmall"] = typeof(double),
		["Libation.FontSize.Body"] = typeof(double),
		["Libation.FontSize.BodyStrong"] = typeof(double),
		["Libation.FontSize.Section"] = typeof(double),
		["Libation.FontSize.Page"] = typeof(double),
		["Libation.FontSize.Hero"] = typeof(double),
		["Libation.Motion.Duration.Fast"] = typeof(double),
		["Libation.Motion.Duration.Default"] = typeof(double),
		["Libation.Motion.Duration.Deliberate"] = typeof(double),
		["Libation.Motion.Scale"] = typeof(double),
		["Libation.Motion.EffectiveDuration.Fast"] = typeof(TimeSpan),
		["Libation.Motion.EffectiveDuration.Default"] = typeof(TimeSpan),
		["Libation.Motion.EffectiveDuration.Deliberate"] = typeof(TimeSpan),
		["Libation.Density.RowHeight"] = typeof(double),
		["Libation.Density.CardPadding"] = typeof(Thickness),
		["Libation.Density.ToolbarGap"] = typeof(double),
		["Libation.Density.QueueItemHeight"] = typeof(double),
		["Libation.Density.MetadataOpacity"] = typeof(double),
		["Libation.Decoration.Opacity"] = typeof(double),
		["Libation.Decoration.Visible"] = typeof(bool),
		["illustration.library.empty"] = typeof(IControlTemplate),
		["illustration.decanter.empty"] = typeof(IControlTemplate),
	}.ToFrozenDictionary(StringComparer.Ordinal);

	public void ValidatePalette(IResourceProvider palette, ThemeVariant theme, ExperienceStyle style)
	{
		foreach (var key in RequiredColorKeys)
		{
			if (!palette.TryGetResource(key, theme, out var value))
				throw new InvalidOperationException($"The {style} palette is missing required resource '{key}'.");
			if (value is not Color)
				throw new InvalidOperationException($"The {style} palette resource '{key}' must be a Color, but is {value?.GetType().Name ?? "null"}.");
		}
	}

	public void ValidateActiveResources(Application application, ThemeVariant theme, ExperienceStyle style)
		=> ValidateResources(application, theme, style);

	public void ValidateResources(IResourceNode resources, ThemeVariant theme, ExperienceStyle style)
	{
		foreach (var key in RequiredColorKeys)
			RequireType(resources, theme, style, key, typeof(Color));
		foreach (var key in RequiredBrushKeys)
			RequireType(resources, theme, style, key, typeof(IBrush));
		foreach (var (key, expectedType) in RequiredSharedTokens)
			RequireType(resources, theme, style, key, expectedType);
	}

	private static void RequireType(IResourceNode resources, ThemeVariant theme, ExperienceStyle style, string key, Type expectedType)
	{
		if (!resources.TryGetResource(key, theme, out var value))
			throw new InvalidOperationException($"The {style} experience is missing required resource '{key}'.");
		if (value is null || !expectedType.IsAssignableFrom(value.GetType()))
			throw new InvalidOperationException($"The {style} resource '{key}' must be {expectedType.Name}, but is {value?.GetType().Name ?? "null"}.");
	}
}
