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
}
