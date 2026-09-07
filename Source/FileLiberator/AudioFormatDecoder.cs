using AAXClean;
using DataLayer;
using FileManager;
using Mpeg4Lib.Boxes;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;

namespace AaxDecrypter;

/// <summary> Read audio codec, bitrate, sample rate, and channel count from MP4 and MP3 audio files. </summary>
public static class AudioFormatDecoder
{
	public static AudioFormat FromMpeg4(string filename)
	{
		using var fileStream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
		return FromMpeg4(new Mp4File(fileStream));
	}

	public static AudioFormat FromMpeg4(Mp4File mp4File)
	{
		Codec codec;
		if (mp4File.AudioSampleEntry.Dac4 is not null)
		{
			codec = Codec.AC_4;
		}
		else if (mp4File.AudioSampleEntry.Dec3 is not null)
		{
			codec = Codec.EC_3;
		}
		else if (mp4File.AudioSampleEntry.Esds is EsdsBox esds)
		{
			var objectType = esds.ES_Descriptor.DecoderConfig.AudioSpecificConfig.AudioObjectType;
			codec
				= objectType == 2 ? Codec.AAC_LC
				: objectType == 42 ? Codec.xHE_AAC
				: Codec.Unknown;
		}
		else
			return AudioFormat.Default;

		var bitrate = (int)Math.Round(mp4File.AverageBitrate / 1024d);

		return new AudioFormat(codec, bitrate, mp4File.TimeScale, mp4File.AudioChannels);
	}

	public static AudioFormat FromMpeg3(LongPath mp3Filename)
	{
		using var mp3File = File.Open(mp3Filename, FileMode.Open, FileAccess.Read, FileShare.Read);
		long audioEnd = mp3File.Length;
		Span<byte> tagMarker = stackalloc byte[3];
		if (TryReadAt(mp3File, audioEnd - 128, tagMarker) && tagMarker.SequenceEqual("TAG"u8))
			audioEnd -= 128;

		if (!TrySkipId3(mp3File, audioEnd, out long audioStart)
			|| !TryFindMp3Frame(mp3File, audioStart, audioEnd, out long frameStart, out var header))
			return AudioFormat.Default;

		var frame = new byte[header.FrameLength];
		if (!TryReadAt(mp3File, frameStart, frame))
			return AudioFormat.Default;

		long audioBytes = audioEnd - frameStart;
		if (TryReadXingBitrate(frame, header, audioBytes, out int bitrate, out bool hasXing)
			|| TryReadVbriBitrate(frame, header, audioBytes, out bitrate, out _))
			return new AudioFormat(Codec.Mp3, bitrate, header.SampleRate, header.Channels);

		// An identified but unusable VBR tag does not justify reporting its first frame's CBR table value.
		if (hasXing || HasMarker(frame, 36, "VBRI"u8))
			return AudioFormat.Default;

		return new AudioFormat(Codec.Mp3, header.BitRate, header.SampleRate, header.Channels);
	}

	#region MP3 metadata helpers
	private static bool TrySkipId3(Stream file, long audioEnd, out long audioStart)
	{
		audioStart = 0;
		Span<byte> header = stackalloc byte[10];
		if (!TryReadAt(file, 0, header[..3]) || !header[..3].SequenceEqual("ID3"u8))
			return true;
		if (!TryReadAt(file, 0, header) || header[3] is < 2 or > 4 || header[4] == 255)
			return false;

		int allowedFlags = header[3] switch { 2 => 0xc0, 3 => 0xe0, _ => 0xf0 };
		if ((header[5] & ~allowedFlags) != 0 || ((header[6] | header[7] | header[8] | header[9]) & 0x80) != 0)
			return false;

		int tagSize = (header[6] << 21) | (header[7] << 14) | (header[8] << 7) | header[9];
		bool hasFooter = header[3] == 4 && (header[5] & 0x10) != 0;
		audioStart = 10L + tagSize + (hasFooter ? 10 : 0);
		if (audioStart > audioEnd)
			return false;
		if (hasFooter)
		{
			Span<byte> footer = stackalloc byte[10];
			if (!TryReadAt(file, audioStart - 10, footer) || !footer[..3].SequenceEqual("3DI"u8)
				|| !footer[3..].SequenceEqual(header[3..]))
				return false;
		}
		return true;
	}

	private static bool TryFindMp3Frame(Stream file, long start, long end, out long frameStart, out Mp3FrameHeader header)
	{
		frameStart = 0;
		header = default;
		const int MaxSeekBytes = 4096;
		if (end - start < 4)
			return false;
		var candidates = new byte[(int)Math.Min(end - start, MaxSeekBytes + 3)];
		if (!TryReadAt(file, start, candidates))
			return false;
		Span<byte> nextBytes = stackalloc byte[4];
		for (int offset = 0; offset < MaxSeekBytes && offset <= candidates.Length - 4; offset++)
		{
			if (!TryReadMp3Header(candidates.AsSpan(offset, 4), out var candidate)
				|| candidate.FrameLength > end - (start + offset))
				continue;

			long nextStart = start + offset + candidate.FrameLength;
			// Confirm sync with a complete compatible next frame, or one complete frame ending at EOF.
			// Bitrate, padding and channel mode may change between Layer III frames.
			if (nextStart != end && (!TryReadAt(file, nextStart, nextBytes)
				|| !TryReadMp3Header(nextBytes, out var next)
				|| next.Version != candidate.Version || next.SampleRate != candidate.SampleRate
				|| next.FrameLength > end - nextStart))
				continue;

			frameStart = start + offset;
			header = candidate;
			return true;
		}
		return false;
	}

	private static bool TryReadMp3Header(ReadOnlySpan<byte> bytes, out Mp3FrameHeader header)
	{
		header = default;
		uint bits = BinaryPrimitives.ReadUInt32BigEndian(bytes);
		var version = (Version)((bits >> 19) & 3);
		int layer = (int)((bits >> 17) & 3);
		int bitrateIndex = (int)((bits >> 12) & 15);
		int rateIndex = (int)((bits >> 10) & 3);
		// MPEG-1/2 and the existing MPEG-2.5 extension are supported for Layer III. Free-format has
		// no table-derived frame length and is intentionally unsupported by this metadata inspector.
		if ((bits & 0xffe00000) != 0xffe00000 || version == Version.Reserved || layer != 1
			|| bitrateIndex is 0 or 15 || rateIndex == 3 || (bits & 3) == 2)
			return false;

		int rate = Mp3SampleRateIndex[version][rateIndex];
		int bitrate = Mp3BitrateIndex[version][bitrateIndex];
		int channels = ((bits >> 6) & 3) == 3 ? 1 : 2;
		int frameLength = (version == Version.Version_1 ? 144000 : 72000) * bitrate / rate + (int)((bits >> 9) & 1);
		header = new(version, bitrate, rate, channels, frameLength);
		return true;
	}

	private static bool TryReadXingBitrate(ReadOnlySpan<byte> frame, Mp3FrameHeader header, long audioBytes,
		out int bitrate, out bool present)
	{
		bitrate = 0;
		// LAME writes Xing/Info at these offsets even with CRC protection; CRC does not add two here.
		int offset = 4 + GetSideInfo(header.Channels == 2, header.Version);
		present = HasMarker(frame, offset, "Xing"u8) || HasMarker(frame, offset, "Info"u8);
		if (!present || frame.Length - offset < 8)
			return false;

		uint flags = BinaryPrimitives.ReadUInt32BigEndian(frame[(offset + 4)..]);
		int required = 8 + ((flags & 1) != 0 ? 4 : 0) + ((flags & 2) != 0 ? 4 : 0)
			+ ((flags & 4) != 0 ? 100 : 0) + ((flags & 8) != 0 ? 4 : 0);
		if ((flags & ~15u) != 0 || (flags & 1) == 0 || required > frame.Length - offset)
			return false;

		uint frames = BinaryPrimitives.ReadUInt32BigEndian(frame[(offset + 8)..]);
		long bytes = (flags & 2) != 0 ? BinaryPrimitives.ReadUInt32BigEndian(frame[(offset + 12)..]) : audioBytes;
		return TryGetAverageBitrate(header, audioBytes, frames, bytes, out bitrate);
	}

	private static bool TryReadVbriBitrate(ReadOnlySpan<byte> frame, Mp3FrameHeader header, long audioBytes,
		out int bitrate, out bool present)
	{
		bitrate = 0;
		const int offset = 4 + 32;
		present = HasMarker(frame, offset, "VBRI"u8);
		if (!present || frame.Length - offset < 18 || BinaryPrimitives.ReadUInt16BigEndian(frame[(offset + 4)..]) != 1)
			return false;

		uint bytes = BinaryPrimitives.ReadUInt32BigEndian(frame[(offset + 10)..]);
		uint frames = BinaryPrimitives.ReadUInt32BigEndian(frame[(offset + 14)..]);
		return TryGetAverageBitrate(header, audioBytes, frames, bytes, out bitrate);
	}

	private static bool TryGetAverageBitrate(Mp3FrameHeader header, long audioBytes, uint frames, long bytes, out int bitrate)
	{
		bitrate = 0;
		if (frames == 0 || bytes < header.FrameLength || bytes > audioBytes)
			return false;

		// Retain the existing VBR 1024 divisor and truncation. A decimal-unit migration is separate.
		double average = bytes * 8d * header.SampleRate / (header.SamplesPerFrame * (double)frames * 1024d);
		if (!double.IsFinite(average) || average < 1 || average >= 4096)
			return false; // AudioFormat persists bitrate in 12 bits; never wrap or spill into its codec field.
		bitrate = (int)average;
		return true;
	}

	private static bool HasMarker(ReadOnlySpan<byte> frame, int offset, ReadOnlySpan<byte> marker)
		=> offset >= 0 && frame.Length - offset >= marker.Length && frame.Slice(offset, marker.Length).SequenceEqual(marker);

	private static bool TryReadAt(Stream file, long offset, Span<byte> bytes)
	{
		if (offset < 0 || offset > file.Length || bytes.Length > file.Length - offset)
			return false;
		file.Position = offset;
		try { file.ReadExactly(bytes); return true; }
		catch (EndOfStreamException) { return false; }
	}

	private readonly record struct Mp3FrameHeader(Version Version, int BitRate, int SampleRate, int Channels, int FrameLength)
	{
		public int SamplesPerFrame => Version == Version.Version_1 ? 1152 : 576;
	}

	private enum Version
	{
		Version_2_5,
		Reserved,
		Version_2,
		Version_1
	}

	private static byte GetSideInfo(bool stereo, Version version) => (stereo, version) switch
	{
		(true, Version.Version_1) => 32,
		(true, Version.Version_2 or Version.Version_2_5) => 17,
		(false, Version.Version_1) => 17,
		(false, Version.Version_2 or Version.Version_2_5) => 9,
		_ => 0,
	};

	private static readonly Dictionary<Version, ushort[]> Mp3SampleRateIndex = new()
	{
		{ Version.Version_2_5, [11025, 12000,  8000] },
		{ Version.Version_2,   [22050, 24000, 16000] },
		{ Version.Version_1,   [44100, 48000, 32000] },
	};

	private static readonly Dictionary<Version, short[]> Mp3BitrateIndex = new()
	{
		{ Version.Version_2_5, [-1, 8,16,24,32,40,48,56, 64, 80, 96,112,128,144,160,-1]},
		{ Version.Version_2,   [-1, 8,16,24,32,40,48,56, 64, 80, 96,112,128,144,160,-1]},
		{ Version.Version_1,   [-1,32,40,48,56,64,80,96,112,128,160,192,224,256,320,-1]}
	};
	#endregion
}
