using AAXClean;
using AaxDecrypter;
using AudibleApi.Common;
using DataLayer;
using LibationFileManager;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace FileLiberator.Tests;

[TestClass]
[DoNotParallelize]
public class ChapterDurationTests
{
	private string directory = string.Empty;
	private string? previousDirectory;

	[TestInitialize]
	public void Initialize()
	{
		directory = Path.Combine(Path.GetTempPath(), $"libation-chapters-{Guid.NewGuid():N}");
		Directory.CreateDirectory(directory);
		previousDirectory = Environment.GetEnvironmentVariable(LibationFiles.LIBATION_FILES_DIR);
		Environment.SetEnvironmentVariable(LibationFiles.LIBATION_FILES_DIR, directory);
		Configuration.CreateMockInstance();
		Configuration.Instance.MergeOpeningAndEndCredits = false;
		Configuration.Instance.MinimumFileDuration = 0;
		AudibleUtilities.AudibleApiStorage.EnsureAccountsSettingsFileExists();
	}

	[TestCleanup]
	public void Cleanup()
	{
		Configuration.RestoreSingletonInstance();
		Environment.SetEnvironmentVariable(LibationFiles.LIBATION_FILES_DIR, previousDirectory);
		Directory.Delete(directory, true);
	}

	[TestMethod]
	public void MissingTailExtendsFinalChapterToExactFractionalDuration()
	{
		using var options = Options(0, 0, Chapter("Only", 0, 1000));
		var original = options.ChapterInfo;
		var duration = TimeSpan.FromMilliseconds(1234) + TimeSpan.FromTicks(5678);
		options.ReconcileChapterDuration(duration);
		Assert.AreEqual(duration, options.ChapterInfo.EndOffset);
		Assert.AreEqual(TimeSpan.FromSeconds(1), original.EndOffset);
		Assert.AreNotSame(original, options.ChapterInfo);
	}

	[TestMethod]
	public void ShortMediaDropsUnavailableTailWithoutNegativeDurations()
	{
		using var options = Options(0, 0, Chapter("First", 0, 1000), Chapter("Second", 1000, 1000));
		var original = options.ChapterInfo;
		options.ReconcileChapterDuration(TimeSpan.FromMilliseconds(500));
		Assert.AreEqual(1, options.ChapterInfo.Count);
		Assert.AreEqual(TimeSpan.FromMilliseconds(500), options.ChapterInfo.Single().Duration);
		Assert.AreEqual(2, original.Count);
		Assert.IsTrue(original.All(c => c.Duration > TimeSpan.Zero));
	}

	[TestMethod]
	public void ReconciliationRetainsOriginalDeclarationForRetry()
	{
		using var options = Options(0, 0, Chapter("First", 0, 1000), Chapter("Second", 1000, 1000));
		options.ReconcileChapterDuration(TimeSpan.FromMilliseconds(500));
		options.ReconcileChapterDuration(TimeSpan.FromMilliseconds(2500));
		Assert.AreEqual(2, options.ChapterInfo.Count);
		Assert.AreEqual("Second", options.ChapterInfo.Chapters[1].Title);
		Assert.AreEqual(TimeSpan.FromMilliseconds(1500), options.ChapterInfo.Chapters[1].Duration);
		var before = options.ChapterInfo.ToArray();
		options.ReconcileChapterDuration(TimeSpan.FromMilliseconds(2500));
		CollectionAssert.AreEqual(before, options.ChapterInfo.ToArray());
	}

	[TestMethod]
	[DataRow(0)]
	[DataRow(-1)]
	public void InvalidMediaDurationLeavesPreparedTimelineUntouched(int milliseconds)
	{
		using var options = Options(0, 0, Chapter("Only", 0, 1000));
		var original = options.ChapterInfo;
		Assert.ThrowsExactly<InvalidDataException>(() => options.ReconcileChapterDuration(TimeSpan.FromMilliseconds(milliseconds)));
		Assert.AreSame(original, options.ChapterInfo);
	}

	[TestMethod]
	public void AmbiguousOutroTrimIsRejectedAtomically()
	{
		Configuration.Instance.StripAudibleBrandAudio = true;
		using var options = Options(0, 100, Chapter("Only", 0, 2000));
		var original = options.ChapterInfo;
		Assert.ThrowsExactly<InvalidDataException>(() => options.ReconcileChapterDuration(TimeSpan.FromMilliseconds(2500)));
		Assert.AreSame(original, options.ChapterInfo);
	}

	[TestMethod]
	public void MillisecondMetadataRoundingPreservesFractionalTail()
	{
		Configuration.Instance.StripAudibleBrandAudio = true;
		using var options = Options(100, 100, Chapter("Only", 0, 2000));
		var duration = TimeSpan.FromMilliseconds(2000) + TimeSpan.FromTicks(5000);
		options.ReconcileChapterDuration(duration);
		Assert.AreEqual(TimeSpan.FromMilliseconds(100), options.ChapterInfo.StartOffset);
		Assert.AreEqual(duration - TimeSpan.FromMilliseconds(100), options.ChapterInfo.EndOffset);
	}

	[TestMethod]
	public void IntroBeyondActualMediaIsRejectedWithoutReplacingTimeline()
	{
		Configuration.Instance.StripAudibleBrandAudio = true;
		using var options = Options(1500, 0, Chapter("Only", 0, 2000));
		var original = options.ChapterInfo;
		Assert.ThrowsExactly<InvalidDataException>(() => options.ReconcileChapterDuration(TimeSpan.FromSeconds(1)));
		Assert.AreSame(original, options.ChapterInfo);
	}

	[TestMethod]
	[DataRow(true, false, false)]
	[DataRow(true, false, true)]
	[DataRow(false, true, false)]
	[DataRow(false, false, false)]
	public void RealMetadataConsumerValidatesEveryTimelineUserAndHonorsTagFixup(bool fixup, bool split, bool mp3)
	{
		var config = Configuration.Instance;
		config.AllowLibationFixup = fixup;
		config.SplitFilesByChapter = split;
		config.DecryptToLossy = mp3;
		using var options = Options(0, 0, Chapter("First", 0, 10), Chapter("Second", 10, 10));
		// This is a metadata-only source, not a compressed-audio playback fixture.
		using var source = new Mp4File(new MemoryStream(OutputAudioMetadataTests.CreateAc4File(48000, 2, 96), false));
		var process = DownloadDecryptBook.Create(config);
		var converter = process.CreateConverter(directory, directory, options);
		typeof(AaxcDownloadConvertBase).GetProperty(nameof(AaxcDownloadConvertBase.AaxFile))!.SetValue(converter, source);
		var registered = typeof(AaxcDownloadConvertBase).GetField(nameof(AaxcDownloadConvertBase.RetrievedMetadata), BindingFlags.NonPublic | BindingFlags.Instance)!;
		var handler = registered.GetValue(converter) as EventHandler<Mpeg4Lib.MetadataItems>;
		Assert.IsNotNull(handler, "The production construction path must subscribe even when tag fixup is disabled.");
		var original = options.ChapterInfo;
		handler(converter, source.MetadataItems);
		if (fixup || split || mp3)
		{
			Assert.AreEqual(source.Duration, options.ChapterInfo.EndOffset);
			Assert.IsTrue(options.ChapterInfo.All(c => c.Duration > TimeSpan.Zero));
		}
		else
			Assert.AreSame(original, options.ChapterInfo, "Full remux without fixup does not consume the requested timeline.");
		Assert.AreEqual(fixup ? "Chapter fixture" : null, source.MetadataItems.Title);
	}

	[TestMethod]
	[DataRow(true, false, false)]
	[DataRow(false, true, false)]
	[DataRow(true, false, true)]
	[DataRow(false, false, false)]
	public void MetadataConsumerUsesPresentedEditWindow(bool fixup, bool split, bool mp3)
	{
		var config = Configuration.Instance;
		config.AllowLibationFixup = fixup;
		config.SplitFilesByChapter = split;
		config.DecryptToLossy = mp3;
		using var options = Options(0, 0, Chapter("First", 0, 1000), Chapter("Second", 1000, 1000));
		using var source = EditedSource(presentedSamples: 96024);
		Assert.AreEqual(TimeSpan.FromSeconds(4), source.Duration);
		Assert.AreEqual(4800L, source.PresentationStartSample);
		Assert.AreEqual(TimeSpan.FromTicks(20005000), source.PresentedDuration);
		var original = options.ChapterInfo;
		ApplyMetadata(directory, options, source);
		if (fixup || split || mp3)
		{
			Assert.AreEqual(source.PresentedDuration, options.ChapterInfo.EndOffset);
			Assert.AreEqual(TimeSpan.Zero, options.ChapterInfo.StartOffset,
				"The media edit offset must not be added to presentation chapter coordinates.");
		}
		else Assert.AreSame(original, options.ChapterInfo);
	}

	[TestMethod]
	public void PresentedWindowPreservesFractionalBrandingEndpoint()
	{
		Configuration.Instance.SplitFilesByChapter = true;
		Configuration.Instance.AllowLibationFixup = false;
		Configuration.Instance.StripAudibleBrandAudio = true;
		using var options = Options(100, 100, Chapter("First", 0, 1000), Chapter("Second", 1000, 1000));
		using var source = EditedSource(presentedSamples: 96024);
		ApplyMetadata(directory, options, source);
		Assert.AreEqual(TimeSpan.FromMilliseconds(100), options.ChapterInfo.StartOffset);
		Assert.AreEqual(TimeSpan.FromTicks(19005000), options.ChapterInfo.EndOffset);
	}

	[TestMethod]
	public void ShortPresentedWindowDropsOnlyUnavailableChapters()
	{
		Configuration.Instance.SplitFilesByChapter = true;
		using var options = Options(0, 0, Chapter("First", 0, 1000), Chapter("Second", 1000, 1000));
		using var source = EditedSource(presentedSamples: 24024);
		ApplyMetadata(directory, options, source);
		Assert.AreEqual(1, options.ChapterInfo.Count);
		Assert.AreEqual(24024L, source.PresentedDurationSamples);
		Assert.AreEqual(source.PresentedDuration, options.ChapterInfo.Single().Duration);
	}

	[TestMethod]
	[TestCategory("NativeAudioIntegration")]
	[DataRow("single-16001-16000-1", 500, 1000, 0, 0, true)]
	[DataRow("single-16001-16000-1", 500, 1000, 0, 0, false)]
	[DataRow("single-16001-16000-1", 500, 1000, 100, 100, true)]
	[DataRow("single-16001-16000-1", 500, 1000, 100, 100, false)]
	[DataRow("single-16001-44100-2", 183, 363, 17, 31, true)]
	[DataRow("single-16001-44100-2", 183, 363, 17, 31, false)]
	public async System.Threading.Tasks.Task GeneratedAacOutputsFollowThePresentedChapterTimeline(
		string fixture, int boundaryMs, int totalMs, int introMs, int outroMs, bool split)
	{
		string? inputs = Environment.GetEnvironmentVariable("LIBATION_SYNTHETIC_AUDIO_FIXTURES");
		string? outputs = Environment.GetEnvironmentVariable("LIBATION_CHAPTER_OUTPUT");
		if (inputs is null || outputs is null)
			Assert.Inconclusive("Opt-in generated AAC integration requires fixture and durable output directories; see audio-candidate.md.");
		string input = Path.Combine(inputs, fixture + ".m4a");
		using var manifest = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(inputs, fixture + ".json")));
		Assert.AreEqual(manifest.RootElement.GetProperty("mp4_sha256").GetString(),
			Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(input))).ToLowerInvariant());
		using var source = new Mp4File(File.OpenRead(input));
		Assert.AreEqual(manifest.RootElement.GetProperty("samples").GetInt64(), source.PresentedDurationSamples);
		Assert.IsTrue(source.PresentationStartSample > 0 && source.Duration > source.PresentedDuration);
		Configuration.Instance.SplitFilesByChapter = true;
		Configuration.Instance.AllowLibationFixup = false;
		Configuration.Instance.StripAudibleBrandAudio = introMs > 0 || outroMs > 0;
		using var options = Options(introMs, outroMs, Chapter("First", 0, boundaryMs), Chapter("Second", boundaryMs, totalMs - boundaryMs));
		ApplyMetadata(directory, options, source);
		Assert.AreEqual(source.PresentedDuration - TimeSpan.FromMilliseconds(outroMs), options.ChapterInfo.EndOffset);
		var streams = new System.Collections.Generic.List<MemoryStream>();
		if (split)
			await source.ConvertToMultiMp4aAsync(options.ChapterInfo, callback =>
			{
				var output = new MemoryStream();
				streams.Add(output);
				callback.OutputFile = output;
			});
		else
		{
			var output = new MemoryStream(); streams.Add(output);
			await source.ConvertToMp4aAsync(output, options.ChapterInfo);
		}
		Assert.AreEqual(split ? options.ChapterInfo.Count : 1, streams.Count);
		string result = Path.Combine(outputs, $"{fixture}-{introMs}-{outroMs}-{(split ? "split" : "single")}");
		Directory.CreateDirectory(result);
		var parts = new System.Collections.Generic.List<object>();
		long SampleAt(TimeSpan time) => checked((time.Ticks * source.Moov.AudioTrack.Mdia.Mdhd.Timescale + TimeSpan.TicksPerSecond / 2) / TimeSpan.TicksPerSecond);
		for (int i = 0; i < streams.Count; i++)
		{
			var chapter = options.ChapterInfo.Chapters[i];
			byte[] bytes = streams[i].ToArray();
			File.WriteAllBytes(Path.Combine(result, $"part-{i}.m4a"), bytes);
			using var output = new Mp4File(new MemoryStream(bytes));
			long start = SampleAt(split ? chapter.StartOffset : options.ChapterInfo.StartOffset);
			long end = SampleAt(split ? chapter.EndOffset : options.ChapterInfo.EndOffset);
			Assert.AreEqual(end - start, output.PresentedDurationSamples);
			parts.Add(new { file = $"part-{i}.m4a", start_sample = start, end_sample = end,
				sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant() });
			streams[i].Dispose();
		}
		File.WriteAllText(Path.Combine(result, "manifest.json"), System.Text.Json.JsonSerializer.Serialize(new
		{
			fixture, rate = source.Moov.AudioTrack.Mdia.Mdhd.Timescale, channels = source.AudioChannels,
			input_sha256 = manifest.RootElement.GetProperty("mp4_sha256").GetString(), parts
		}));
	}

	internal static void ApplyMetadata(string directory, DownloadOptions options, Mp4File source)
	{
		var process = DownloadDecryptBook.Create(Configuration.Instance);
		var converter = process.CreateConverter(directory, directory, options);
		typeof(AaxcDownloadConvertBase).GetProperty(nameof(AaxcDownloadConvertBase.AaxFile))!.SetValue(converter, source);
		var registered = typeof(AaxcDownloadConvertBase).GetField(nameof(AaxcDownloadConvertBase.RetrievedMetadata), BindingFlags.NonPublic | BindingFlags.Instance)!;
		var handler = registered.GetValue(converter) as EventHandler<Mpeg4Lib.MetadataItems>;
		Assert.IsNotNull(handler);
		handler(converter, source.MetadataItems);
	}

	private static Mp4File EditedSource(long presentedSamples)
	{
		using var seed = new Mp4File(new MemoryStream(OutputAudioMetadataTests.CreateAc4File(48000, 2, 96)));
		var output = new MemoryStream();
		using (var writer = new AAXClean.FrameFilters.Audio.Mp4aWriter(output, seed.Ftyp, seed.Moov))
		{
			// Structural duration fixture only; these bytes are not an AC-4 bitstream.
			writer.AddFrame(new byte[96], true, 192000);
			writer.SetEditList(4800, presentedSamples);
			writer.Close();
		}
		output.Position = 0;
		return new Mp4File(output);
	}

	[TestMethod]
	public void MaximumMinimumFileDurationDoesNotOverflowMilliseconds()
	{
		Configuration.Instance.SplitFilesByChapter = true;
		Configuration.Instance.MinimumFileDuration = int.MaxValue;
		using var options = Options(0, 0, Chapter("First", 0, 1000), Chapter("Second", 1000, 1000));
		Assert.AreEqual(1, options.ChapterInfo.Count);
		Assert.AreEqual(TimeSpan.FromSeconds(2), options.ChapterInfo.Single().Duration);
	}

	[TestMethod]
	public void FactoryUsesAnOwnedGapPreservingProjectionOnEveryCall()
	{
		Chapter[] provider = [Chapter("First", 1000, 1000), Chapter("Second", 3000, 1000)];
		using var first = Options(0, 0, provider);
		using var second = Options(0, 0, provider);
		Assert.AreEqual(1000L, provider[0].StartOffsetMs);
		Assert.AreEqual(1000L, provider[0].LengthMs);
		Assert.AreEqual(TimeSpan.Zero, first.ChapterInfo.StartOffset);
		Assert.AreEqual(TimeSpan.FromSeconds(3), first.ChapterInfo.Chapters[1].StartOffset);
		Assert.AreEqual(TimeSpan.FromSeconds(4), first.ChapterInfo.EndOffset);
		CollectionAssert.AreEqual(first.ChapterInfo.ToArray(), second.ChapterInfo.ToArray());
	}

	internal static Chapter Chapter(string title, long start, long length)
		=> new() { Title = title, StartOffsetMs = start, LengthMs = length, StartOffsetSec = start / 1000 };

	internal static DownloadOptions Options(long intro, long outro, params Chapter[] chapters)
	{
		var book = new Book(new AudibleProductId("B0CHAPTER1"), "Chapter fixture", "", "", 1,
			DataLayer.ContentType.Product, [new Contributor("Test Author")], [new Contributor("Test Narrator")], "us");
		var license = DownloadOptions.LicenseInfo.Create(new ContentLicense
		{
			DrmType = DrmType.Widevine,
			ContentMetadata = new()
			{
				ContentReference = new() { Acr = "fixture", Asin = "B0CHAPTER1", Codec = "mp4a.40.2", ContentFormat = "MPEG4", Marketplace = "us", Sku = "fixture", Tempo = "1", Version = "1" },
				ContentUrl = new() { OfflineUrl = "https://chapter-fixture.invalid/book.mp4" },
				ChapterInfo = new() { Chapters = chapters, RuntimeLengthMs = chapters.Max(c => c.StartOffsetMs + c.LengthMs), BrandIntroDurationMs = intro, BrandOutroDurationMs = outro }
			}
		});
		return DownloadOptions.BuildDownloadOptions(new LibraryBook(book, new DateTime(2026, 1, 1), "fixture"), Configuration.Instance, license);
	}
}
