using FileManager;
using LibationFileManager;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace LibationAvalonia.Tests;

[TestClass]
[DoNotParallelize]
public class S9ReleaseHardeningContractTests
{
	[TestCleanup]
	public void Cleanup() => Configuration.RestoreSingletonInstance();

	[TestMethod]
	public void InvalidContemporarySetting_RepairsTheValueAndDisablesTheShell()
	{
		var configuration = Configuration.CreateMockInstance();
		configuration.SetString("preserve me", "S9UnrelatedSetting");
		configuration.SetNonString("FutureProfile", nameof(Configuration.ExperienceStyle));
		configuration.UseContemporaryShell = true;

		Assert.AreEqual(ExperienceStyle.FollowSystem, configuration.ExperienceStyle);
		Assert.IsFalse(configuration.UseContemporaryShell);
		Assert.AreEqual("preserve me", configuration.GetString(propertyName: "S9UnrelatedSetting"));
		Assert.AreEqual(
			ExperienceStyle.FollowSystem,
			configuration.CreateEphemeralCopy().ExperienceStyle,
			"The repaired safe profile must be observable after a settings reload.");
	}

	[TestMethod]
	public void LegacyWriter_PreservesUnknownContemporarySettingsAcrossDowngradeRoundTrip()
	{
		var directory = Directory.CreateTempSubdirectory("LibationS9Downgrade_");
		var settingsPath = Path.Combine(directory.FullName, "Settings.json");
		try
		{
			File.WriteAllText(settingsPath, """
				{
				  "AutoScan": false,
				  "ExperienceStyle": "TastingRoom",
				  "DensityMode": "Compact",
				  "UseContemporaryShell": true,
				  "ContemporaryFlightProductIds": ["B001", "B002"]
				}
				""");

			// PersistentDictionary is the same key-preserving writer used by versions that
			// predate the contemporary settings. Simulate a downgraded app changing a
			// setting it knows, then verify a later version can still read the newer keys.
			var legacyWriter = new PersistentDictionary(settingsPath);
			legacyWriter.SetNonString(nameof(Configuration.AutoScan), true);

			var persisted = JObject.Parse(File.ReadAllText(settingsPath));
			Assert.IsTrue(persisted.Value<bool>(nameof(Configuration.AutoScan)));
			Assert.AreEqual("TastingRoom", persisted.Value<string>(nameof(Configuration.ExperienceStyle)));
			Assert.AreEqual("Compact", persisted.Value<string>(nameof(Configuration.DensityMode)));
			Assert.IsTrue(persisted.Value<bool>(nameof(Configuration.UseContemporaryShell)));
			CollectionAssert.AreEqual(
				new[] { "B001", "B002" },
				persisted[nameof(Configuration.ContemporaryFlightProductIds)]!.Values<string>().ToArray());
		}
		finally
		{
			directory.Delete(recursive: true);
		}
	}
}
