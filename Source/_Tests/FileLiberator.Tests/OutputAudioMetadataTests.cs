using AAXClean;
using AaxDecrypter;
using FileLiberator;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace FileLiberator.Tests;

[TestClass]
public class OutputAudioMetadataTests
{
	[TestMethod]
	public void Completed_mpeg4_metadata_comes_from_the_written_output_file()
	{
		byte[] sourceBytes = CreateAc4File(sampleRate: 48_000, channels: 2, frameSize: 96);
		byte[] outputBytes = CreateAc4File(sampleRate: 44_100, channels: 1, frameSize: 32);
		string outputPath = Path.Combine(Path.GetTempPath(), $"libation-output-metadata-{Guid.NewGuid():N}.m4b");
		File.WriteAllBytes(outputPath, outputBytes);

		try
		{
			using var source = new Mp4File(new MemoryStream(sourceBytes, writable: false));
			var converter = (AaxcDownloadSingleConverter)RuntimeHelpers.GetUninitializedObject(
				typeof(AaxcDownloadSingleConverter));
			SetField(typeof(AaxcDownloadConvertBase), converter, "<AaxFile>k__BackingField", source);

			var process = (DownloadDecryptBook)RuntimeHelpers.GetUninitializedObject(typeof(DownloadDecryptBook));
			SetField(typeof(DownloadDecryptBook), process, "abDownloader", converter);

			var sourceFormat = AudioFormatDecoder.FromMpeg4(source);
			var expectedOutputFormat = AudioFormatDecoder.FromMpeg4(outputPath);
			var actual = process.GetFileFormatInfo(null!, new TempFile(outputPath));

			Assert.AreNotEqual(sourceFormat.Serialize(), expectedOutputFormat.Serialize(),
				"The witness must distinguish encrypted-input metadata from written-output metadata.");
			Assert.AreEqual(expectedOutputFormat.Serialize(), actual.Serialize());
		}
		finally
		{
			File.Delete(outputPath);
		}
	}

	private static void SetField(Type declaringType, object target, string fieldName, object value)
		=> (declaringType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new MissingFieldException(declaringType.FullName, fieldName))
			.SetValue(target, value);

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
