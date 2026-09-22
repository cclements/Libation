using AudibleApi.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;

namespace FileLiberator.Tests;

[TestClass]
public class ChapterPresentationTests
{
	[TestMethod]
	public void Flatten_RepeatedNormalizationLeavesProviderTreeUnchanged()
	{
		Chapter[] provider = [Chapter("部", 0, 2000, Chapter("第一章", 2000, 8000))];
		var original = JsonConvert.SerializeObject(provider);
		var first = DownloadOptions.flattenChapters(provider);
		var second = DownloadOptions.flattenChapters(provider);

		Assert.AreEqual(original, JsonConvert.SerializeObject(provider), "Normalization must not modify provider titles, starts, lengths, or descendants.");
		Assert.AreEqual(JsonConvert.SerializeObject(first), JsonConvert.SerializeObject(second));
		Assert.AreEqual("部: 第一章", first.Single().Title);
		Assert.AreEqual(10000L, first.Single().LengthMs);
	}

	[TestMethod]
	public void Flatten_EmptyChildCollectionIsALeaf()
	{
		var provider = Chapter("Empty children", 0, 1000);
		provider.Chapters = [];
		var result = DownloadOptions.flattenChapters([provider]);
		Assert.AreEqual(1, result.Count);
		Assert.AreEqual("Empty children", result[0].Title);
		Assert.AreEqual(1000L, result[0].LengthMs);
		Assert.AreNotSame(provider, result[0]);
	}

	[TestMethod]
	public void Branding_RejectsInconsistentEdgesWithoutPartialMutation()
	{
		var chapters = new[] { Chapter("One", 0, 1000), Chapter("Two", 1000, 1000) }.ToList();
		var original = JsonConvert.SerializeObject(chapters);
		Assert.ThrowsExactly<InvalidDataException>(() => DownloadOptions.stripBranding(chapters, 1500, 100));
		Assert.AreEqual(original, JsonConvert.SerializeObject(chapters));
	}

	[TestMethod]
	public void Flatten_WithoutTitleCombinationStillOwnsEveryNode()
	{
		var provider = Chapter("Parent", 0, 1000, Chapter("Child", 1000, 2000));
		var before = JsonConvert.SerializeObject(provider);
		var result = DownloadOptions.flattenChapters([provider], null);
		result[0].Title = "changed";
		result[1].LengthMs = 1;
		Assert.AreEqual(before, JsonConvert.SerializeObject(provider));
		Assert.AreEqual(2, result.Count);
	}

	[TestMethod]
	public void Timeline_PreservesLeadingAndInteriorGapsAndSortsOwnedCopies()
	{
		Chapter[] provider = [Chapter("Second", 4000, 1000), Chapter("First", 1000, 1000)];
		var before = JsonConvert.SerializeObject(provider);
		var result = DownloadOptions.PrepareChapterTimeline(provider, ": ");
		Assert.AreEqual(before, JsonConvert.SerializeObject(provider));
		Assert.AreEqual(0L, result[0].StartOffsetMs);
		Assert.AreEqual(4000L, result[0].LengthMs);
		Assert.AreEqual(4000L, result[1].StartOffsetMs);
		Assert.AreEqual(1000L, result[1].LengthMs);
	}

	[TestMethod]
	public void Flatten_ShortParentPreservesGapBeforeChild()
	{
		var result = DownloadOptions.flattenChapters([Chapter("Part", 0, 1000, Chapter("Child", 2000, 1000))]);
		Assert.AreEqual(3000L, result.Single().LengthMs);
		Assert.AreEqual(0L, result.Single().StartOffsetMs);
	}

	[TestMethod]
	public void Flatten_OverlapsAreRejectedWithoutChangingProvider()
	{
		Chapter[] provider = [Chapter("First", 0, 2000), Chapter("Second", 1000, 2000)];
		var before = JsonConvert.SerializeObject(provider);
		Assert.ThrowsExactly<InvalidDataException>(() => DownloadOptions.flattenChapters(provider));
		Assert.AreEqual(before, JsonConvert.SerializeObject(provider));
	}

	[TestMethod]
	public void Flatten_ParentOverlappingItsChildIsRejected()
		=> Assert.ThrowsExactly<InvalidDataException>(() => DownloadOptions.flattenChapters([Chapter("Part", 0, 2000, Chapter("Child", 1000, 1000))]));

	[TestMethod]
	[DataRow(-1L, 1000L)]
	[DataRow(0L, -1L)]
	[DataRow(long.MaxValue, 1L)]
	[DataRow(1L, long.MaxValue)]
	public void Flatten_InvalidCoordinatesAreRejected(long start, long length)
		=> Assert.ThrowsExactly<InvalidDataException>(() => DownloadOptions.flattenChapters([Chapter("Invalid", start, length)]));

	[TestMethod]
	public void Timeline_EmptyAndZeroOnlyMetadataFailExplicitly()
	{
		Assert.ThrowsExactly<InvalidDataException>(() => DownloadOptions.PrepareChapterTimeline(null, ": "));
		Assert.ThrowsExactly<InvalidDataException>(() => DownloadOptions.PrepareChapterTimeline([], ": "));
		Assert.ThrowsExactly<InvalidDataException>(() => DownloadOptions.PrepareChapterTimeline([Chapter("Empty", 0, 0)], ": "));
	}

	[TestMethod]
	[DataRow(600L, 500L)]
	[DataRow(-1L, 0L)]
	[DataRow(0L, -1L)]
	[DataRow(1000L, 0L)]
	public void Branding_InvalidSingleChapterTrimIsAtomic(long intro, long outro)
	{
		var chapters = new[] { Chapter("Only", 0, 1000) }.ToList();
		var before = JsonConvert.SerializeObject(chapters);
		Assert.ThrowsExactly<InvalidDataException>(() => DownloadOptions.stripBranding(chapters, intro, outro));
		Assert.AreEqual(before, JsonConvert.SerializeObject(chapters));
	}

	[TestMethod]
	public void Branding_UpdatesBothCoordinateUnits()
	{
		var chapters = new[] { Chapter("Only", 0, 5000) }.ToList();
		DownloadOptions.stripBranding(chapters, 1500, 500);
		Assert.AreEqual(1500L, chapters[0].StartOffsetMs);
		Assert.AreEqual(1L, chapters[0].StartOffsetSec);
		Assert.AreEqual(3000L, chapters[0].LengthMs);
	}

	[TestMethod]
	public void Timeline_ZeroLengthMarkerInsideAudioDoesNotRemoveContent()
	{
		var result = DownloadOptions.PrepareChapterTimeline([Chapter("Audio", 0, 2000), Chapter("Marker", 1000, 0)], ": ");
		Assert.AreEqual(1, result.Count);
		Assert.AreEqual(2000L, result[0].LengthMs);
	}

	[TestMethod]
	public void Flatten_ParentWithOnlyZeroLengthChildrenRetainsItsOwnAudio()
	{
		var result = DownloadOptions.flattenChapters([Chapter("Part", 0, 2000, Chapter("Empty", 1000, 0))]);
		Assert.AreEqual(1, result.Count);
		Assert.AreEqual("Part", result[0].Title);
		Assert.AreEqual(2000L, result[0].LengthMs);
	}

	private static Chapter Chapter(string title, long start, long length, params Chapter[] children)
		=> new() { Title = title, StartOffsetMs = start, StartOffsetSec = start / 1000, LengthMs = length, Chapters = children.Length == 0 ? null : children };
}
