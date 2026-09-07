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
using System.Text;

namespace FileLiberator.Tests;

[TestClass]
[DoNotParallelize]
public class OutputAudioMetadataTests
{
	private string tempDirectory = string.Empty;
	private string? previousLibationFiles;

	[TestInitialize]
	public void Initialize()
	{
		tempDirectory = Path.Combine(Path.GetTempPath(), $"libation-output-metadata-{Guid.NewGuid():N}");
		Directory.CreateDirectory(tempDirectory);
		previousLibationFiles = Environment.GetEnvironmentVariable(LibationFiles.LIBATION_FILES_DIR);
		Environment.SetEnvironmentVariable(LibationFiles.LIBATION_FILES_DIR, tempDirectory);
		Configuration.CreateMockInstance();
		AudibleUtilities.AudibleApiStorage.EnsureAccountsSettingsFileExists();
	}

	[TestCleanup]
	public void Cleanup()
	{
		Configuration.RestoreSingletonInstance();
		Environment.SetEnvironmentVariable(LibationFiles.LIBATION_FILES_DIR, previousLibationFiles);
		Directory.Delete(tempDirectory, recursive: true);
	}

	[TestMethod]
	[DataRow(".m4b")]
	[DataRow(".m4a")]
	[DataRow(".mp4")]
	[DataRow(".M4B")]
	public void Completed_mpeg4_metadata_comes_from_the_output_even_with_an_open_input(string extension)
	{
		using var options = CreateOptions();
		using var source = OpenSource();
		var process = CreateProcessWithInput(options, source);
		var outputPath = WriteOutput("output" + extension, 44_100, 1, 32);
		var sourceFormat = AudioFormatDecoder.FromMpeg4(source);
		var outputFormat = AudioFormatDecoder.FromMpeg4(outputPath);

		Assert.AreEqual(44_100, outputFormat.SampleRate);
		Assert.AreEqual(1, outputFormat.ChannelCount);
		Assert.AreNotEqual(sourceFormat.SampleRate, outputFormat.SampleRate);
		Assert.AreNotEqual(sourceFormat.ChannelCount, outputFormat.ChannelCount);
		Assert.AreNotEqual(sourceFormat.BitRate, outputFormat.BitRate);

		var actual = process.GetFileFormatInfo(options, new TempFile(outputPath));

		Assert.AreEqual(outputFormat.Serialize(), actual.Serialize());
	}

	[TestMethod]
	public void Each_completed_part_is_inspected_instead_of_reusing_source_or_previous_part_metadata()
	{
		using var options = CreateOptions();
		using var source = OpenSource();
		var process = CreateProcessWithInput(options, source);
		var firstPath = WriteOutput("part-1.m4b", 44_100, 1, 32);
		var secondPath = WriteOutput("part-2.m4b", 44_100, 1, 64);
		var firstExpected = AudioFormatDecoder.FromMpeg4(firstPath);
		var secondExpected = AudioFormatDecoder.FromMpeg4(secondPath);
		Assert.AreNotEqual(firstExpected.BitRate, secondExpected.BitRate);

		var first = process.GetFileFormatInfo(options, new TempFile(firstPath));
		var second = process.GetFileFormatInfo(options, new TempFile(secondPath));

		Assert.AreEqual(firstExpected.Serialize(), first.Serialize());
		Assert.AreEqual(secondExpected.Serialize(), second.Serialize());
	}

	[TestMethod]
	[DataRow(false)]
	[DataRow(true)]
	public void Unreadable_output_returns_unknown_instead_of_input_metadata(bool malformed)
	{
		using var options = CreateOptions();
		using var source = OpenSource();
		var process = CreateProcessWithInput(options, source);
		var outputPath = Path.Combine(tempDirectory, "unreadable.m4b");
		if (malformed)
			File.WriteAllBytes(outputPath, [0, 1, 2]);
		Assert.IsFalse(AudioFormatDecoder.FromMpeg4(source).IsDefault);

		var actual = process.GetFileFormatInfo(options, new TempFile(outputPath));

		Assert.IsTrue(actual.IsDefault,
			"Failure to inspect a completed output remains nonfatal and must not substitute input facts.");
	}

	[TestMethod]
	public void Mp3_output_still_reads_its_own_frame_header_with_an_open_mpeg4_input()
	{
		using var options = CreateOptions();
		using var source = OpenSource();
		var process = CreateProcessWithInput(options, source);
		var outputPath = Path.Combine(tempDirectory, "output.mp3");
		// MPEG-1 layer III, 128 kbit/s, 44.1 kHz, mono. Payload is synthetic; no audio decode is performed.
		File.WriteAllBytes(outputPath, [0xff, 0xfb, 0x90, 0xc0, .. new byte[128]]);

		var actual = process.GetFileFormatInfo(options, new TempFile(outputPath));

		Assert.AreEqual(Codec.Mp3, actual.Codec);
		Assert.AreEqual(128, actual.BitRate);
		Assert.AreEqual(44_100, actual.SampleRate);
		Assert.AreEqual(1, actual.ChannelCount);
	}

	[TestMethod]
	public void Unknown_extension_still_returns_unknown_with_an_open_input()
	{
		using var options = CreateOptions();
		using var source = OpenSource();
		var process = CreateProcessWithInput(options, source);

		var actual = process.GetFileFormatInfo(options, new TempFile(Path.Combine(tempDirectory, "output.bin")));

		Assert.IsTrue(actual.IsDefault);
	}

	private static DownloadOptions CreateOptions()
	{
		var book = new Book(new AudibleProductId("B0OUTMETA1"), "Output metadata fixture", "", "", 1,
			DataLayer.ContentType.Product, [new Contributor("Test Author")], [new Contributor("Test Narrator")], "us");
		var libraryBook = new LibraryBook(book, new DateTime(2026, 1, 1), "test-account");
		var license = DownloadOptions.LicenseInfo.Create(new ContentLicense
		{
			DrmType = DrmType.Widevine,
			ContentMetadata = new()
			{
				ContentReference = new()
				{
					Acr = "fixture", Asin = "B0OUTMETA1", Codec = "ac-4", ContentFormat = "MPEG4",
					Marketplace = "us", Sku = "fixture", Tempo = "1", Version = "1"
				},
				ContentUrl = new() { OfflineUrl = "https://output-metadata.invalid/source.mp4" },
				ChapterInfo = new()
				{
					RuntimeLengthMs = 1000,
					Chapters = [new Chapter { Title = "Chapter 1", StartOffsetMs = 0, LengthMs = 1000 }]
				}
			}
		});
		return DownloadOptions.BuildDownloadOptions(libraryBook, Configuration.Instance, license);
	}

	private DownloadDecryptBook CreateProcessWithInput(DownloadOptions options, Mp4File source)
	{
		// Construct the real converter without running its network/conversion steps. Only its open input
		// and the process's downloader field need fixture injection; neither object is uninitialized.
		var converter = new AaxcDownloadSingleConverter(tempDirectory, tempDirectory, options);
		var inputProperty = typeof(AaxcDownloadConvertBase).GetProperty(nameof(AaxcDownloadConvertBase.AaxFile));
		Assert.IsNotNull(inputProperty);
		inputProperty.SetValue(converter, source);
		Assert.AreSame(source, converter.AaxFile, "The regression must exercise the real input-handle branch.");

		var process = DownloadDecryptBook.Create(Configuration.Instance);
		var downloaderField = typeof(DownloadDecryptBook).GetField("abDownloader", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.IsNotNull(downloaderField);
		downloaderField.SetValue(process, converter);
		Assert.AreSame(converter, downloaderField.GetValue(process));
		return process;
	}

	private static Mp4File OpenSource()
		=> new(new MemoryStream(CreateAc4File(48_000, 2, 96), writable: false));

	private string WriteOutput(string name, ushort sampleRate, ushort channels, uint frameSize)
	{
		var path = Path.Combine(tempDirectory, name);
		File.WriteAllBytes(path, CreateAc4File(sampleRate, channels, frameSize));
		return path;
	}

	// Tiny metadata-only MP4 fixtures. Zero-filled compressed samples do not prove AC-4 decoding,
	// conversion quality, or playback; they deliberately vary the metadata read by the completion step.
	private static byte[] CreateAc4File(ushort sampleRate, ushort channels, uint frameSize)
	{
		const uint sampleCount = 2;
		const uint frameDelta = 1024;
		uint mediaDuration = sampleCount * frameDelta;
		byte[] ftyp = Box("ftyp", Encoding.ASCII.GetBytes("M4A "), UInt32s(0));
		byte[] mdat = Box("mdat", new byte[checked(sampleCount * frameSize)]);
		byte[] mvhd = Box("mvhd",
			UInt32s(0, 0, 0, sampleRate, mediaDuration, 0x0001_0000),
			UInt16s(0x0100, 0), new byte[8], new byte[36], new byte[24], UInt32s(2));
		byte[] tkhd = Box("tkhd",
			UInt32s(0, 0, 0, 1, 0, mediaDuration), new byte[8],
			UInt16s(0, 0, 0x0100, 0), new byte[36], UInt32s(0, 0));
		byte[] mdhd = Box("mdhd", UInt32s(0, 0, 0, sampleRate, mediaDuration, 0));
		byte[] hdlr = Box("hdlr", UInt32s(0, 0), Encoding.ASCII.GetBytes("soun"), new byte[12]);
		byte[] sampleEntry = Box("ac-4",
			new byte[6], UInt16s(1), new byte[8],
			UInt16s(channels, 16, 0, 0, sampleRate, 0), Box("dac4", [0]));
		byte[] stsd = Box("stsd", UInt32s(0, 1), sampleEntry);
		byte[] stts = Box("stts", UInt32s(0, 1, sampleCount, frameDelta));
		byte[] stsc = Box("stsc", UInt32s(0, 1, 1, sampleCount, 1));
		byte[] stsz = Box("stsz", UInt32s(0, 0, sampleCount), UInt32s(frameSize, frameSize));
		byte[] stco = Box("stco", UInt32s(0, 1, (uint)(ftyp.Length + 8)));
		byte[] stbl = Box("stbl", stsd, stts, stsc, stsz, stco);
		byte[] moov = Box("moov", mvhd, Box("trak", tkhd, Box("mdia", mdhd, hdlr, Box("minf", stbl))));
		return [.. ftyp, .. mdat, .. moov];
	}

	private static byte[] Box(string type, params byte[][] payloads)
	{
		using var box = new MemoryStream();
		WriteUInt32BE(box, checked((uint)(8 + payloads.Sum(payload => payload.Length))));
		box.Write(Encoding.ASCII.GetBytes(type));
		foreach (byte[] payload in payloads)
			box.Write(payload);
		return box.ToArray();
	}

	private static byte[] UInt32s(params uint[] values)
	{
		using var bytes = new MemoryStream();
		foreach (uint value in values)
			WriteUInt32BE(bytes, value);
		return bytes.ToArray();
	}

	private static byte[] UInt16s(params ushort[] values)
	{
		using var bytes = new MemoryStream();
		foreach (ushort value in values)
			bytes.Write([(byte)(value >> 8), (byte)value]);
		return bytes.ToArray();
	}

	private static void WriteUInt32BE(Stream stream, uint value)
		=> stream.Write([(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value]);
}
