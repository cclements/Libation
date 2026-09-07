using LibationAvalonia.Diagnostics;
using LibationAvalonia.Shell;
using LibationFileManager;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LibationAvalonia.Tests;

[TestClass]
public class CapturePlanTests
{
	[TestMethod]
	public void Parse_ReadsEntriesAndDefaults()
	{
		var plan = CapturePlan.Parse("""
			{"entries":[{"profile":"TastingRoom","route":"Library","width":960,"height":720}]}
			""");

		Assert.AreEqual(800, plan.SettleMs);
		Assert.AreEqual(1, plan.Entries.Count);
		var entry = plan.Entries[0];
		Assert.AreEqual(ExperienceStyle.TastingRoom, entry.Profile);
		Assert.AreEqual(CaptureSurface.Route, entry.Surface);
		Assert.AreEqual(AppRouteId.Library, entry.Route);
		Assert.AreEqual(DensityMode.Comfortable, entry.Density);
		Assert.AreEqual(DecorationLevel.Full, entry.Decoration);
		Assert.IsNull(entry.LibraryView);
		Assert.AreEqual("tastingroom-library-960x720.png", entry.FileName);
	}

	[TestMethod]
	public void Parse_HonorsExplicitFileSettleDensityAndDecoration()
	{
		var plan = CapturePlan.Parse("""
			{"settleMs":1500,"entries":[{"profile":"Cellar","route":"Processing","width":1456,"height":1060,"density":"Compact","decoration":"Off","libraryView":"Details","file":"custom.png"}]}
			""");

		Assert.AreEqual(1500, plan.SettleMs);
		Assert.AreEqual("custom.png", plan.Entries[0].FileName);
		Assert.AreEqual(DensityMode.Compact, plan.Entries[0].Density);
		Assert.AreEqual(DecorationLevel.Off, plan.Entries[0].Decoration);
		Assert.AreEqual(LibraryViewMode.Details, plan.Entries[0].LibraryView);
	}

	[TestMethod]
	public void Parse_HonorsMotionAndLogicalScale()
	{
		var plan = CapturePlan.Parse("""
			{"entries":[{"profile":"Cellar","route":"Library","width":1456,"height":1060,"motion":"Reduce","logicalScale":2}]}
			""");

		Assert.AreEqual(ReducedMotionPreference.Reduce, plan.Entries[0].Motion);
		Assert.AreEqual(2d, plan.Entries[0].LogicalScale);
	}

	[TestMethod]
	public void Parse_RejectsInvalidLogicalScale()
	{
		Assert.ThrowsExactly<CapturePlanException>(() => CapturePlan.Parse("""
			{"entries":[{"profile":"Cellar","route":"Library","width":1456,"height":1060,"logicalScale":0}]}
			"""));
		Assert.ThrowsExactly<CapturePlanException>(() => CapturePlan.Parse("""
			{"entries":[{"profile":"Cellar","route":"Library","width":1456,"height":1060,"logicalScale":-1}]}
			"""));
	}

	[TestMethod]
	public void Parse_ComponentGalleryDefaultsRouteAndFileName()
	{
		var plan = CapturePlan.Parse("""
			{"entries":[{"profile":"TastingRoom","surface":"ComponentGallery","width":960,"height":720}]}
			""");

		var entry = plan.Entries[0];
		Assert.AreEqual(CaptureSurface.ComponentGallery, entry.Surface);
		Assert.AreEqual(AppRouteId.Overview, entry.Route);
		Assert.AreEqual("tastingroom-componentgallery-960x720.png", entry.FileName);
	}

	[TestMethod]
	public void Parse_RejectsUnknownRoute()
	{
		Assert.ThrowsExactly<CapturePlanException>(() => CapturePlan.Parse("""
			{"entries":[{"profile":"Cellar","route":"Kitchen","width":10,"height":10}]}
			"""));
	}

	[TestMethod]
	public void Parse_RejectsUnknownSurface()
	{
		Assert.ThrowsExactly<CapturePlanException>(() => CapturePlan.Parse("""
			{"entries":[{"profile":"Cellar","surface":"Board","route":"Overview","width":720,"height":560}]}
			"""));
	}

	[TestMethod]
	public void Parse_RejectsEmptyPlan()
	{
		Assert.ThrowsExactly<CapturePlanException>(() => CapturePlan.Parse("""{"entries":[]}"""));
	}
	[TestMethod]
	public void Parse_RejectsAboutBeforeAnUnsatisfiableRenderedWait()
	{
		var error = Assert.ThrowsExactly<CapturePlanException>(() => CapturePlan.Parse("""
			{"entries":[{"profile":"Cellar","route":"About","width":720,"height":560,"file":"about.png"}]}
			"""));
		StringAssert.Contains(error.Message, "Entry 0 (about.png)");
		StringAssert.Contains(error.Message, "utility dialog");
	}

	[TestMethod]
	public void Parse_RejectsInfiniteWaitAndAmbiguousOutputContracts()
	{
		var entry = """{"profile":"Cellar","route":"Library","width":720,"height":560}""";
		foreach (var settings in new[] { "\"settleMs\":-1", "\"settleMs\":10001", "\"stageTimeoutMs\":0", "\"stageTimeoutMs\":120001" })
			Assert.ThrowsExactly<CapturePlanException>(() => CapturePlan.Parse("{" + settings + ",\"entries\":[" + entry + "]}"));
		Assert.ThrowsExactly<CapturePlanException>(() => CapturePlan.Parse("{\"entries\":[" + entry + "," + entry + "]}"));
		Assert.ThrowsExactly<CapturePlanException>(() => CapturePlan.Parse("""
			{"entries":[{"profile":"Cellar","route":"Library","width":720,"height":560,"file":"../outside.png"}]}
			"""));
		Assert.ThrowsExactly<CapturePlanException>(() => CapturePlan.Parse("""
			{"entries":[{"profile":"FollowSystem","route":"Library","width":720,"height":560}]}
			"""));
	}

	[TestMethod]
	public void Parse_ReservesFailureDiagnosticsOutsidePlannedFrames()
	{
		Assert.ThrowsExactly<CapturePlanException>(() => CapturePlan.Parse("""
			{"entries":[{"profile":"Cellar","route":"Library","width":720,"height":560,"file":"Capture-Diagnostics/window-0000.png"}]}
			"""));
		var plan = CapturePlan.Parse("""
			{"entries":[{"profile":"Cellar","route":"Library","width":720,"height":560,"file":"window-0000.png"}]}
			""");
		Assert.AreEqual("window-0000.png", plan.Entries[0].FileName);
	}

	[TestMethod]
	public void CanonicalPlans_OnlyContainSupportedUniqueCaptures()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "Scripts", "capture-plans")))
			directory = directory.Parent;
		Assert.IsNotNull(directory);
		var plans = Directory.GetFiles(Path.Combine(directory.FullName, "Scripts", "capture-plans"), "*.json");
		Assert.IsGreaterThan(0, plans.Length);
		foreach (var path in plans)
		{
			var plan = CapturePlan.Load(path);
			Assert.IsTrue(plan.Entries.All(entry => entry.Surface != CaptureSurface.Route || entry.Route != AppRouteId.About), path);
			if (Path.GetFileName(path) == "all-routes.json")
				Assert.AreEqual(54, plan.Entries.Count, "Nine rendered routes, two profiles, three sizes; the About utility dialog remains attended coverage.");
		}
	}

}
