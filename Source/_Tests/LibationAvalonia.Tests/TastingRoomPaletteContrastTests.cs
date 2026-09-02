using Avalonia.Media;
using Avalonia.Styling;
using LibationFileManager;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;

namespace LibationAvalonia.Tests;

[TestClass]
[DoNotParallelize]
public class TastingRoomPaletteContrastTests
{
	[TestMethod]
	public async Task SemanticTokenPairs_MeetTextAndNonTextContrast()
	{
		await HeadlessTestHost.Reset(ExperienceStyle.TastingRoom);
		await HeadlessTestHost.Dispatch(() =>
		{
			var textPairs = new[]
			{
				("Libation.Color.TextTertiary", "Libation.Color.Surface"),
				("Libation.Color.AccentSecondary", "Libation.Color.Surface"),
				("Libation.Color.TextOnAccent", "Libation.Color.Selection"),
				("Libation.Color.TextOnAccent", "Libation.Color.Success"),
				("Libation.Color.TextOnAccent", "Libation.Color.Warning"),
			};
			var nonTextPairs = new[]
			{
				("Libation.Color.AccentSecondary", "Libation.Color.Surface"),
				("Libation.Color.Selection", "Libation.Color.Surface"),
				("Libation.Color.Success", "Libation.Color.Surface"),
				("Libation.Color.Warning", "Libation.Color.Surface"),
			};

			foreach (var pair in textPairs)
				AssertContrast(pair.Item1, pair.Item2, minimum: 4.5);
			foreach (var pair in nonTextPairs)
				AssertContrast(pair.Item1, pair.Item2, minimum: 3.0);
		});
	}

	private static void AssertContrast(string foregroundKey, string backgroundKey, double minimum)
	{
		var foreground = ResolveColor(foregroundKey);
		var background = ResolveColor(backgroundKey);
		var ratio = ContrastRatio(foreground, background);
		Assert.IsTrue(
			ratio >= minimum,
			$"{foregroundKey} on {backgroundKey} is {ratio:F2}:1; expected at least {minimum:F1}:1.");
	}

	private static Color ResolveColor(string key)
	{
		Assert.IsTrue(App.Current.TryGetResource(key, ThemeVariant.Light, out var value), $"Missing palette token {key}.");
		return value is Color color
			? color
			: throw new AssertFailedException($"Palette token {key} is not a Color.");
	}

	private static double ContrastRatio(Color first, Color second)
	{
		var firstLuminance = RelativeLuminance(first);
		var secondLuminance = RelativeLuminance(second);
		return (Math.Max(firstLuminance, secondLuminance) + 0.05)
			/ (Math.Min(firstLuminance, secondLuminance) + 0.05);
	}

	private static double RelativeLuminance(Color color)
		=> 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);

	private static double Linear(byte channel)
	{
		var value = channel / 255d;
		return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
	}
}
