using AaxDecrypter;
using DataLayer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace FileLiberator.Tests;

[TestClass]
public class Mp3AudioMetadataTests
{
	private string path = string.Empty;

	[TestInitialize]
	public void Initialize() => path = Path.Combine(Path.GetTempPath(), $"libation-mp3-metadata-{Guid.NewGuid():N}.mp3");

	[TestCleanup]
	public void Cleanup() => File.Delete(path);

	[TestMethod]
	[DataRow(0xfffb90c0u, 417, 128, 44_100, 1)]
	[DataRow(0xfffa90c0u, 417, 128, 44_100, 1)] // CRC protected
	[DataRow(0xfffbe400u, 960, 320, 48_000, 2)]
	[DataRow(0xfffba2c0u, 523, 160, 44_100, 1)] // padding byte
	[DataRow(0xfff380c0u, 208, 64, 22_050, 1)]
	[DataRow(0xffe340c0u, 208, 32, 11_025, 1)]
	[DataRow(0xffe348c0u, 288, 32, 8_000, 1)]
	public void Complete_cbr_frames_report_known_header_fields(uint header, int frameLength, int bitrate, int rate, int channels)
		=> AssertFormat(Read(Pair(Frame(header, frameLength))), bitrate, rate, channels);

	[TestMethod]
	[DataRow(0xffeb90c0u)] // reserved version
	[DataRow(0xfff990c0u)] // reserved layer
	[DataRow(0xfffd90c0u)] // Layer II is outside this MP3 metadata contract
	[DataRow(0xffff90c0u)] // Layer I
	[DataRow(0xfffb9cc0u)] // reserved sample-rate index
	[DataRow(0xfffbf0c0u)] // reserved bitrate index
	[DataRow(0xfffb00c0u)] // free-format needs a separate frame-size inference contract
	[DataRow(0xfffb90c2u)] // reserved emphasis
	public void Reserved_or_unsupported_headers_return_unknown_without_throwing(uint header)
		=> Assert.IsTrue(Read(Pair(Frame(header, 417))).IsDefault);

	[TestMethod]
	[DataRow(0)]
	[DataRow(1)]
	[DataRow(2)]
	[DataRow(3)]
	[DataRow(4)]
	[DataRow(63)]
	[DataRow(132)]
	[DataRow(416)]
	public void A_truncated_header_or_declared_frame_returns_unknown(int length)
		=> Assert.IsTrue(Read(Frame(0xfffb90c0u, 417)[..length]).IsDefault);

	[TestMethod]
	public void One_complete_frame_at_the_end_of_audio_is_sufficient()
		=> AssertFormat(Read(Frame(0xfffb90c0u, 417)), 128, 44_100, 1);

	[TestMethod]
	public void A_truncated_second_frame_does_not_confirm_the_first_candidate()
		=> Assert.IsTrue(Read([.. Frame(0xfffb90c0u, 417), .. Frame(0xfffb90c0u, 417)[..416]]).IsDefault);

	[TestMethod]
	public void Header_confirmation_allows_bitrate_padding_and_channel_mode_changes()
		=> AssertFormat(Read([.. Frame(0xfffb90c0u, 417), .. Frame(0xfffba200u, 523)]), 128, 44_100, 1);

	[TestMethod]
	public void A_false_sync_is_skipped_before_the_valid_frame_sequence()
		=> AssertFormat(Read([0xff, 0xff, 0, 0, .. Pair(Frame(0xfffb90c0u, 417))]), 128, 44_100, 1);

	[TestMethod]
	public void A_plausible_header_without_a_frame_at_its_declared_boundary_is_skipped()
	{
		// This header says 160 kbit/s, 48 kHz stereo (480-byte frames), but the real stream starts at 100.
		var falseCandidate = Frame(0xfffba400u, 100);
		AssertFormat(Read([.. falseCandidate, .. Pair(Frame(0xfffb90c0u, 417))]), 128, 44_100, 1);
	}

	[TestMethod]
	[DataRow(4095, true)]
	[DataRow(4096, false)]
	public void Candidate_search_has_an_explicit_4096_byte_limit(int leadingBytes, bool expected)
	{
		var actual = Read([.. new byte[leadingBytes], .. Pair(Frame(0xfffb90c0u, 417))]);
		Assert.AreEqual(expected, !actual.IsDefault);
	}

	[TestMethod]
	[DataRow(2)]
	[DataRow(3)]
	[DataRow(4)]
	public void A_bounded_id3_tag_is_skipped_before_frame_search(int version)
	{
		// The opaque tag body deliberately contains a false sync; the audio begins after its declared size.
		var tag = Id3(version, [0xff, 0xff, 0xff, 0xff, .. new byte[13]]);
		AssertFormat(Read([.. tag, .. Pair(Frame(0xfffb90c0u, 417))]), 128, 44_100, 1);
	}

	[TestMethod]
	public void A_matching_id3v24_footer_is_skipped()
		=> AssertFormat(Read([.. Id3(4, new byte[17], footer: true), .. Pair(Frame(0xfffb90c0u, 417))]), 128, 44_100, 1);

	[TestMethod]
	[DataRow(3)]
	[DataRow(9)]
	public void A_truncated_id3_header_returns_unknown(int length)
		=> Assert.IsTrue(Read(Id3(4, [0])[..length]).IsDefault);

	[TestMethod]
	[DataRow("size-high-bit")]
	[DataRow("size-beyond-file")]
	[DataRow("reserved-flags")]
	[DataRow("unsupported-version")]
	[DataRow("invalid-revision")]
	[DataRow("missing-footer")]
	[DataRow("mismatched-footer")]
	public void Malformed_id3_does_not_fall_through_into_embedded_sync(string defect)
	{
		var tag = Id3(4, [0], footer: defect.Contains("footer", StringComparison.Ordinal));
		switch (defect)
		{
			case "size-high-bit": tag[6] = 0x80; break;
			case "size-beyond-file": tag[8] = 0x7f; tag[9] = 0x7f; break;
			case "reserved-flags": tag[5] |= 1; break;
			case "unsupported-version": tag[3] = 5; break;
			case "invalid-revision": tag[4] = 255; break;
			case "missing-footer": tag = tag[..^10]; break;
			case "mismatched-footer": tag[^7] = 3; break;
		}
		Assert.IsTrue(Read([.. tag, .. Pair(Frame(0xfffb90c0u, 417))]).IsDefault);
	}

	[TestMethod]
	[DataRow(0xfffb9000u, 417, 36, 124, 44_100, 2)]
	[DataRow(0xfffa9000u, 417, 36, 124, 44_100, 2)] // LAME keeps the Xing offset unchanged with CRC
	[DataRow(0xfffb90c0u, 417, 21, 124, 44_100, 1)]
	[DataRow(0xfffa90c0u, 417, 21, 124, 44_100, 1)]
	[DataRow(0xfff38000u, 208, 21, 62, 22_050, 2)]
	[DataRow(0xfff28000u, 208, 21, 62, 22_050, 2)]
	[DataRow(0xfff380c0u, 208, 13, 62, 22_050, 1)]
	[DataRow(0xfff280c0u, 208, 13, 62, 22_050, 1)]
	[DataRow(0xffe340c0u, 208, 13, 31, 11_025, 1)]
	[DataRow(0xffe240c0u, 208, 13, 31, 11_025, 1)]
	public void Xing_metadata_uses_version_channel_offsets_and_retains_existing_bitrate_units(
		uint header, int frameLength, int xingOffset, int expectedBitrate, int rate, int channels)
	{
		var first = Frame(header, frameLength);
		WriteXing(first, xingOffset, "Xing", 3, 2, (uint)(frameLength * 2));
		AssertFormat(Read([.. first, .. Frame(header, frameLength)]), expectedBitrate, rate, channels);
	}

	[TestMethod]
	public void Info_metadata_retains_the_existing_average_bitrate_contract()
	{
		var first = Frame(0xfffb90c0u, 417);
		WriteXing(first, 21, "Info", 3, 2, 834);
		AssertFormat(Read([.. first, .. Frame(0xfffb90c0u, 417)]), 124, 44_100, 1);
	}

	[TestMethod]
	public void Xing_without_a_byte_count_excludes_trailing_id3v1_from_the_audio_extent()
	{
		var first = Frame(0xfffb90c0u, 417);
		WriteXing(first, 21, "Xing", 1, 2, 0);
		AssertFormat(Read([.. first, .. Frame(0xfffb90c0u, 417), .. Id3v1()]), 124, 44_100, 1);
	}

	[TestMethod]
	[DataRow(3u, 0u, 834u)]
	[DataRow(3u, 2u, 0u)]
	[DataRow(3u, 2u, 835u)]
	[DataRow(3u, 2u, 1u)]
	[DataRow(3u, uint.MaxValue, 834u)]
	[DataRow(3u, 1u, uint.MaxValue)]
	[DataRow(19u, 2u, 834u)] // undefined flag
	[DataRow(0u, 0u, 0u)] // no frame count or independent VBRI witness
	public void Invalid_xing_counts_or_flags_return_unknown(uint flags, uint frames, uint bytes)
	{
		var first = Frame(0xfffb90c0u, 417);
		WriteXing(first, 21, "Xing", flags, frames, bytes);
		Assert.IsTrue(Read([.. first, .. Frame(0xfffb90c0u, 417)]).IsDefault);
	}

	[TestMethod]
	public void Xing_optional_fields_must_fit_inside_the_first_frame()
	{
		var first = Frame(0xffe314c0u, 48); // MPEG-2.5, 8 kbit/s, 12 kHz mono
		WriteXing(first, 13, "Xing", 7, 2, 96); // TOC flag requires another 100 bytes
		Assert.IsTrue(Read([.. first, .. Frame(0xffe314c0u, 48)]).IsDefault);
	}

	[TestMethod]
	[DataRow(0xfffb90c0u)]
	[DataRow(0xfffa90c0u)]
	public void Vbri_is_read_at_an_absolute_offset_from_the_audio_frame(uint header)
	{
		var first = Frame(header, 417);
		WriteVbri(first, 1, 2, 834);
		AssertFormat(Read([.. Id3(3, new byte[19]), .. first, .. Frame(header, 417)]), 124, 44_100, 1);
	}

	[TestMethod]
	public void A_xing_probe_without_counts_does_not_move_the_vbri_probe()
	{
		var first = Frame(0xfffb90c0u, 417);
		WriteXing(first, 21, "Xing", 0, 0, 0);
		WriteVbri(first, 1, 2, 834);
		AssertFormat(Read([.. first, .. Frame(0xfffb90c0u, 417)]), 124, 44_100, 1);
	}

	[TestMethod]
	[DataRow(1, 0u, 834u)]
	[DataRow(1, 2u, 0u)]
	[DataRow(1, 2u, 835u)]
	[DataRow(1, 2u, 1u)]
	[DataRow(1, uint.MaxValue, 834u)]
	[DataRow(1, 1u, uint.MaxValue)]
	[DataRow(0, 2u, 834u)]
	[DataRow(2, 2u, 834u)]
	public void Invalid_vbri_counts_or_version_return_unknown(int version, uint frames, uint bytes)
	{
		var first = Frame(0xfffb90c0u, 417);
		WriteVbri(first, (ushort)version, frames, bytes);
		Assert.IsTrue(Read([.. first, .. Frame(0xfffb90c0u, 417)]).IsDefault);
	}

	[TestMethod]
	public void Vbri_fields_cannot_be_read_from_the_next_frame()
	{
		var stream = Pair(Frame(0xffe314c0u, 48));
		WriteVbri(stream, 1, 2, 96);
		// Restore the second frame header after placing metadata across that boundary.
		BinaryPrimitives.WriteUInt32BigEndian(stream.AsSpan(48), 0xffe314c0u);
		Assert.IsTrue(Read(stream).IsDefault);
	}

	[TestMethod]
	[DataRow("Xing")]
	[DataRow("VBRI")]
	public void Implausible_counts_cannot_overflow_the_persisted_bitrate_field(string marker)
	{
		var stream = new byte[417 * 100];
		for (int offset = 0; offset < stream.Length; offset += 417)
			BinaryPrimitives.WriteUInt32BigEndian(stream.AsSpan(offset), 0xfffb90c0u);
		// A one-frame duration for these 100 frames would spill beyond the 12-bit stored bitrate.
		if (marker == "Xing") WriteXing(stream, 21, marker, 3, 1, (uint)stream.Length);
		else WriteVbri(stream, 1, 1, (uint)stream.Length);
		Assert.IsTrue(Read(stream).IsDefault);
	}

	[TestMethod]
	public void A_complete_single_frame_before_id3v1_is_supported()
		=> AssertFormat(Read([.. Frame(0xfffb90c0u, 417), .. Id3v1()]), 128, 44_100, 1);

	private AudioFormat Read(byte[] bytes)
	{
		File.WriteAllBytes(path, bytes);
		return AudioFormatDecoder.FromMpeg3(path);
	}

	private static void AssertFormat(AudioFormat actual, int bitrate, int rate, int channels)
	{
		Assert.AreEqual(Codec.Mp3, actual.Codec);
		Assert.AreEqual(bitrate, actual.BitRate);
		Assert.AreEqual(rate, actual.SampleRate);
		Assert.AreEqual(channels, actual.ChannelCount);
	}

	// Frame lengths, header words and expected properties are explicit independent witnesses, rather
	// than calculated with the production parser. Payloads are synthetic and are never audio-decoded.
	private static byte[] Frame(uint header, int length)
	{
		var bytes = new byte[length];
		BinaryPrimitives.WriteUInt32BigEndian(bytes, header);
		return bytes;
	}

	private static byte[] Pair(byte[] frame) => [.. frame, .. frame];

	private static void WriteXing(byte[] frame, int offset, string marker, uint flags, uint frames, uint bytes)
	{
		Encoding.ASCII.GetBytes(marker).CopyTo(frame, offset);
		BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(offset + 4), flags);
		int cursor = offset + 8;
		if ((flags & 1) != 0)
		{
			BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(cursor), frames);
			cursor += 4;
		}
		if ((flags & 2) != 0)
			BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(cursor), bytes);
	}

	private static void WriteVbri(byte[] frame, ushort version, uint frames, uint bytes)
	{
		"VBRI"u8.CopyTo(frame.AsSpan(36));
		BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(40), version);
		BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(46), bytes);
		BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(50), frames);
	}

	private static byte[] Id3(int version, byte[] body, bool footer = false)
	{
		var header = new byte[] { (byte)'I', (byte)'D', (byte)'3', (byte)version, 0, (byte)(footer ? 0x10 : 0), 0, 0, 0, (byte)body.Length };
		if (!footer) return [.. header, .. body];
		return [.. header, .. body, (byte)'3', (byte)'D', (byte)'I', .. header[3..]];
	}

	private static byte[] Id3v1() => [(byte)'T', (byte)'A', (byte)'G', .. new byte[125]];
}
