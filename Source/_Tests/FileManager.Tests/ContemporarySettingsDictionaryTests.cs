using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace FileManager.Tests;

[TestClass]
public class ContemporarySettingsDictionaryTests
{
	private static string createSettingsFile(string contents = "{}")
	{
		var directory = Directory.CreateDirectory(
			Path.Combine(Path.GetTempPath(), "LibationContemporarySettings_" + Guid.NewGuid().ToString("N")));
		var filepath = Path.Combine(directory.FullName, "Settings.json");
		File.WriteAllText(filepath, contents);
		return filepath;
	}

	private static void deleteSettingsFile(string filepath)
	{
		try { Directory.Delete(Path.GetDirectoryName(filepath)!, recursive: true); } catch { /* best effort */ }
	}

	[TestMethod]
	public void SetNonStrings_PersistsCompleteBatchAndPreservesExistingSettings()
	{
		var filepath = createSettingsFile("""
			{
			  "UnrelatedSetting": "preserve me",
			  "UseContemporaryShell": false
			}
			""");
		try
		{
			var dictionary = new PersistentDictionary(filepath);
			KeyValuePair<string, object?>[] settings =
			[
				new("ExperienceStyle", "Cellar"),
				new("ContemporaryFlightProductIds", new[] { "book-1", "book-2" }),
				new("UseContemporaryShell", true),
			];

			dictionary.SetNonStrings(settings);

			var persisted = JObject.Parse(File.ReadAllText(filepath));
			Assert.AreEqual("preserve me", persisted.Value<string>("UnrelatedSetting"));
			Assert.AreEqual("Cellar", persisted.Value<string>("ExperienceStyle"));
			CollectionAssert.AreEqual(
				new[] { "book-1", "book-2" },
				persisted["ContemporaryFlightProductIds"]!.Values<string>().ToArray());
			Assert.IsTrue(persisted.Value<bool>("UseContemporaryShell"));
		}
		finally
		{
			deleteSettingsFile(filepath);
		}
	}

	[TestMethod]
	public void SetNonStrings_ReplacesPreviouslyCachedDefaultsAfterCommit()
	{
		var filepath = createSettingsFile();
		try
		{
			var dictionary = new PersistentDictionary(filepath);
			Assert.IsFalse(dictionary.GetNonString("UseContemporaryShell", defaultValue: false));
			Assert.AreEqual("CurrentAvalonia", dictionary.GetNonString("ExperienceStyle", "CurrentAvalonia"));

			dictionary.SetNonStrings(
			[
				new("ExperienceStyle", "TastingRoom"),
				new("UseContemporaryShell", true),
			]);

			Assert.AreEqual("TastingRoom", dictionary.GetNonString<string>("ExperienceStyle"));
			Assert.IsTrue(dictionary.GetNonString<bool>("UseContemporaryShell"));
			var reread = new PersistentDictionary(filepath);
			Assert.AreEqual("TastingRoom", reread.GetNonString<string>("ExperienceStyle"));
			Assert.IsTrue(reread.GetNonString<bool>("UseContemporaryShell"));
		}
		finally
		{
			deleteSettingsFile(filepath);
		}
	}

	[TestMethod]
	public void SetNonStrings_ReadOnlyDictionaryLeavesFileUnchanged()
	{
		const string original = """
			{
			  "UseContemporaryShell": false
			}
			""";
		var filepath = createSettingsFile(original);
		try
		{
			var dictionary = new PersistentDictionary(filepath, isReadOnly: true);

			dictionary.SetNonStrings(
			[
				new("ExperienceStyle", "Cellar"),
				new("UseContemporaryShell", true),
			]);

			Assert.AreEqual(original, File.ReadAllText(filepath));
			Assert.IsFalse(dictionary.GetNonString("UseContemporaryShell", defaultValue: false));
			Assert.IsFalse(dictionary.Exists("ExperienceStyle"));
		}
		finally
		{
			deleteSettingsFile(filepath);
		}
	}
}
