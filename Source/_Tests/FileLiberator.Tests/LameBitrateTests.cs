using AAXClean;
using AaxDecrypter;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NAudio.Lame;
using System.IO;

namespace FileLiberator.Tests;

[TestClass]
public class LameBitrateTests
{
    [TestMethod]
    [DataRow(false, false, 2, 32000, 128)]
    [DataRow(true, false, 2, 32000, 128)]
    [DataRow(false, true, 2, 32000, 64)]
    [DataRow(true, true, 2, 32000, 64)]
    [DataRow(false, true, 2, 16000, 32)]
    [DataRow(true, false, 2, 16000, 64)]
    [DataRow(false, false, 1, 48000, 128)]
    [DataRow(true, false, 1, 16000, 64)]
    public void MatchSourceBitrateUsesDecimalKbpsWithExistingRateAndChannelScaling(
        bool abr, bool downsample, int channels, int requestedRate, int expectedKbps)
    {
        using var source = Source(channels);
        Assert.AreEqual(128000, source.AverageBitrate);
        var options = new LameConfig { OutputSampleRate = requestedRate, Mode = MPEGMode.Mono,
            VBR = abr ? VBRMode.ABR : null };
        MpegUtil.ConfigureLameOptions(source, options, downsample, true, null);
        Assert.AreEqual(expectedKbps, abr ? options.ABRRateKbps : options.BitRate);
        Assert.AreEqual(System.Math.Min(32000, requestedRate), options.OutputSampleRate);
        Assert.AreEqual(channels == 2 && !downsample ? MPEGMode.Stereo : MPEGMode.Mono, options.Mode);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ExplicitBitrateIsPreservedWhenSourceMatchingIsOff(bool abr)
    {
        using var source = Source(2);
        var options = new LameConfig { OutputSampleRate = 32000, VBR = abr ? VBRMode.ABR : null,
            BitRate = 96, ABRRateKbps = 96 };
        MpegUtil.ConfigureLameOptions(source, options, false, false, null);
        Assert.AreEqual(96, abr ? options.ABRRateKbps : options.BitRate);
    }

    private static Mp4File Source(int channels)
        // Two 512-byte samples at 32000/1024 frames per second: exactly 128000 bit/s.
        // Payload is metadata-only synthetic AC-4, never decoded.
        => new(new MemoryStream(OutputAudioMetadataTests.CreateAc4File(32000, (ushort)channels, 512)));
}
