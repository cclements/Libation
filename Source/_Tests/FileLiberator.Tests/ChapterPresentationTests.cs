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

	private static Chapter Chapter(string title, long start, long length, params Chapter[] children)
		=> new() { Title = title, StartOffsetMs = start, StartOffsetSec = start / 1000, LengthMs = length, Chapters = children.Length == 0 ? null : children };
}
