using Avalonia.Styling;
using LibationFileManager;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;

namespace LibationAvalonia.Tests;

[TestClass]
[DoNotParallelize]
public class MotionPreferenceTests
{
	[TestMethod]
	public async Task ReduceMotion_ResolvesEveryEffectiveDurationToZero()
	{
		await HeadlessTestHost.Reset(ExperienceStyle.Cellar);
		await HeadlessTestHost.Dispatch(() => HeadlessTestHost.Configuration.ReducedMotionPreference = ReducedMotionPreference.Reduce);
		await HeadlessTestHost.Dispatch(() =>
		{
			foreach (var key in new[]
			{
				"Libation.Motion.EffectiveDuration.Fast",
				"Libation.Motion.EffectiveDuration.Default",
				"Libation.Motion.EffectiveDuration.Deliberate",
			})
			{
				Assert.IsTrue(App.Current.TryGetResource(key, ThemeVariant.Dark, out var value), $"Missing motion token {key}.");
				Assert.AreEqual(TimeSpan.Zero, value, $"{key} must be zero when reduced motion is enabled.");
			}
		});
	}
}
