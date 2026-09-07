using AudibleUtilities.Widevine;
using Google.Protobuf;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mpeg4Lib.Boxes;
using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace AudibleUtilities.Tests;

[TestClass]
public class WidevineRepresentationBindingTests
{
	private const string RequestedCodec = "mp4a.40.42";
	private static readonly Uri ManifestUri = new("https://cdn.example/books/manifest.mpd");

	[TestMethod]
	[DataRow(false)]
	[DataRow(true)]
	public void Requested_audio_representation_supplies_its_own_codec_url_and_pssh(bool reverseOrder)
	{
		string videoPssh = EncodedPssh([0x11]);
		string lcPssh = EncodedPssh([0x02]);
		string usacPssh = EncodedPssh([0x42]);
		using var mpd = Utf8($$"""
			<MPD xmlns="urn:mpeg:dash:schema:mpd:2011" xmlns:cenc="urn:mpeg:cenc:2013">
			  <Period>
			    <AdaptationSet contentType="video">
			      <ContentProtection schemeIdUri="urn:uuid:{{Cdm.WidevineContentProtection}}"><cenc:pssh>{{videoPssh}}</cenc:pssh></ContentProtection>
			      <Representation codecs="avc1"><BaseURL>video.mp4</BaseURL></Representation>
			    </AdaptationSet>
			    <AdaptationSet contentType="audio">
			      <Representation codecs="mp4a.40.2">
			        <ContentProtection schemeIdUri="urn:uuid:{{Cdm.WidevineContentProtection}}"><cenc:pssh>{{lcPssh}}</cenc:pssh></ContentProtection>
			        <BaseURL>lc.mp4</BaseURL>
			      </Representation>
			      <Representation codecs="mp4a.40.42">
			        <ContentProtection schemeIdUri="urn:uuid:{{Cdm.WidevineContentProtection}}"><cenc:pssh>{{usacPssh}}</cenc:pssh></ContentProtection>
			        <BaseURL>usac.mp4</BaseURL>
			      </Representation>
			    </AdaptationSet>
			  </Period>
			</MPD>
			""");

		var xml = XElement.Load(mpd);
		if (reverseOrder)
		{
			XNamespace ns = xml.Name.Namespace;
			foreach (var parent in xml.Descendants(ns + "Period").Concat(xml.Descendants(ns + "AdaptationSet")))
			{
				var alternatives = parent.Elements().Where(e => e.Name.LocalName is "AdaptationSet" or "Representation").Reverse().ToArray();
				foreach (var alternative in alternatives)
					alternative.Remove();
				parent.Add(alternatives);
			}
		}
		using var orderedMpd = Utf8(xml.ToString());
		var dash = new MpegDash(orderedMpd);

		Assert.IsTrue(dash.TryGetContent(
			ManifestUri,
			Cdm.WidevineContentProtection,
			RequestedCodec,
			out var uri,
			out var pssh));
		using (pssh)
		{
			Assert.AreEqual(new Uri("https://cdn.example/books/usac.mp4"), uri);
			CollectionAssert.AreEqual(new byte[] { 0x42 }, pssh.InitData);
		}
	}

	[TestMethod]
	public void Inherited_base_urls_and_adaptation_pssh_remain_bound_to_the_representation()
	{
		string pssh = EncodedPssh([0x42]);
		using var mpd = Utf8($$"""
			<MPD xmlns="urn:mpeg:dash:schema:mpd:2011" xmlns:cenc="urn:mpeg:cenc:2013">
			  <BaseURL>cdn/</BaseURL>
			  <Period><BaseURL>period/</BaseURL>
			    <AdaptationSet contentType="audio"><BaseURL>adaptation/</BaseURL>
			      <ContentProtection schemeIdUri="urn:uuid:{{Cdm.WidevineContentProtection}}"><cenc:pssh>{{pssh}}</cenc:pssh></ContentProtection>
			      <Representation codecs="{{RequestedCodec}}"><BaseURL>audio.mp4</BaseURL></Representation>
			    </AdaptationSet>
			  </Period>
			</MPD>
			""");

		var dash = new MpegDash(mpd);

		Assert.IsTrue(dash.TryGetContent(
			ManifestUri,
			Cdm.WidevineContentProtection,
			RequestedCodec,
			out var uri,
			out var selectedPssh));
		using (selectedPssh)
		{
			Assert.AreEqual(new Uri("https://cdn.example/books/cdn/period/adaptation/audio.mp4"), uri);
			CollectionAssert.AreEqual(new byte[] { 0x42 }, selectedPssh.InitData);
		}
	}

	[TestMethod]
	public void Missing_codec_identity_is_not_treated_as_the_requested_codec()
	{
		var dash = DashWithRepresentation(EncodedPssh([0x42]), representationAttributes: null);

		Assert.IsFalse(dash.TryGetContent(
			ManifestUri,
			Cdm.WidevineContentProtection,
			RequestedCodec,
			out _,
			out _));
	}

	[TestMethod]
	public void A_direct_representation_base_url_is_required()
	{
		string pssh = EncodedPssh([0x42]);
		using var mpd = Utf8($$"""
			<MPD xmlns="urn:mpeg:dash:schema:mpd:2011" xmlns:cenc="urn:mpeg:cenc:2013">
			  <Period><AdaptationSet contentType="audio">
			    <ContentProtection schemeIdUri="urn:uuid:{{Cdm.WidevineContentProtection}}"><cenc:pssh>{{pssh}}</cenc:pssh></ContentProtection>
			    <Representation codecs="{{RequestedCodec}}" />
			  </AdaptationSet></Period>
			</MPD>
			""");
		var dash = new MpegDash(mpd);

		Assert.IsFalse(dash.TryGetContent(
			ManifestUri,
			Cdm.WidevineContentProtection,
			RequestedCodec,
			out _,
			out _));
	}

	[TestMethod]
	[DataRow("SegmentTemplate", "MPD")]
	[DataRow("SegmentTemplate", "Period")]
	[DataRow("SegmentTemplate", "AdaptationSet")]
	[DataRow("SegmentTemplate", "Representation")]
	[DataRow("SegmentList", "MPD")]
	[DataRow("SegmentList", "Period")]
	[DataRow("SegmentList", "AdaptationSet")]
	[DataRow("SegmentList", "Representation")]
	public void Segmented_addressing_is_rejected_at_each_inheritance_level(string segmentElement, string level)
	{
		string pssh = EncodedPssh([0x42]);
		using var mpd = Utf8($$"""
			<MPD xmlns="urn:mpeg:dash:schema:mpd:2011" xmlns:cenc="urn:mpeg:cenc:2013">
			  <Period><AdaptationSet contentType="audio">
			    <ContentProtection schemeIdUri="urn:uuid:{{Cdm.WidevineContentProtection}}"><cenc:pssh>{{pssh}}</cenc:pssh></ContentProtection>
			    <Representation codecs="{{RequestedCodec}}"><BaseURL>audio/</BaseURL></Representation>
			  </AdaptationSet></Period>
			</MPD>
			""");
		var xml = XElement.Load(mpd);
		var owner = xml.DescendantsAndSelf().Single(element => element.Name.LocalName == level);
		owner.Add(new XElement(xml.Name.Namespace + segmentElement));
		using var addressedMpd = Utf8(xml.ToString());
		var dash = new MpegDash(addressedMpd);

		Assert.IsFalse(dash.TryGetContent(
			ManifestUri,
			Cdm.WidevineContentProtection,
			RequestedCodec,
			out _,
			out _));
	}

	[TestMethod]
	[DataRow("dynamic", 1)]
	[DataRow("static", 2)]
	[DataRow("static", 0)]
	[DataRow("unknown", 1)]
	public void Only_one_static_period_can_describe_a_complete_download(string type, int periodCount)
	{
		string pssh = EncodedPssh([0x42]);
		string period = $$"""
			<Period><AdaptationSet contentType="audio">
			  <Representation codecs="{{RequestedCodec}}">
			    <ContentProtection schemeIdUri="urn:uuid:{{Cdm.WidevineContentProtection}}"><cenc:pssh>{{pssh}}</cenc:pssh></ContentProtection>
			    <BaseURL>audio.mp4</BaseURL>
			  </Representation>
			</AdaptationSet></Period>
			""";
		using var mpd = Utf8($$"""
			<MPD xmlns="urn:mpeg:dash:schema:mpd:2011" xmlns:cenc="urn:mpeg:cenc:2013" type="{{type}}">
			{{string.Concat(Enumerable.Repeat(period, periodCount))}}
			</MPD>
			""");
		var dash = new MpegDash(mpd);

		Assert.IsFalse(dash.TryGetContent(ManifestUri, Cdm.WidevineContentProtection, RequestedCodec, out var uri, out var selectedPssh));
		Assert.IsNull(uri);
		Assert.IsNull(selectedPssh);
	}

	[TestMethod]
	[DataRow("Period")]
	[DataRow("AdaptationSet")]
	public void External_hierarchy_references_cannot_be_replaced_by_local_fallback_content(string level)
	{
		var xml = XElement.Parse(SegmentBaseMpd("<Initialization range=\"0-100\" />"));
		var owner = xml.Descendants().Single(element => element.Name.LocalName == level);
		owner.SetAttributeValue(XNamespace.Get("http://www.w3.org/1999/xlink") + "href", "external.xml");
		using var mpd = Utf8(xml.ToString());
		var dash = new MpegDash(mpd);

		Assert.IsFalse(dash.TryGetContent(ManifestUri, Cdm.WidevineContentProtection, RequestedCodec, out _, out _));
	}

	[TestMethod]
	[DataRow("Initialization")]
	[DataRow("RepresentationIndex")]
	public void SegmentBase_cannot_require_a_separate_resource(string childName)
	{
		using var mpd = Utf8(SegmentBaseMpd($"<{childName} sourceURL=\"separate.mp4\" />"));
		var dash = new MpegDash(mpd);

		Assert.IsFalse(dash.TryGetContent(ManifestUri, Cdm.WidevineContentProtection, RequestedCodec, out _, out _));
	}

	[TestMethod]
	public void SegmentBase_ranges_inside_the_single_media_resource_are_supported()
	{
		using var mpd = Utf8(SegmentBaseMpd("<Initialization range=\"0-100\" />"));
		var dash = new MpegDash(mpd);

		Assert.IsTrue(dash.TryGetContent(ManifestUri, Cdm.WidevineContentProtection, RequestedCodec, out var uri, out var selectedPssh));
		using (selectedPssh)
		{
			Assert.AreEqual(new Uri(ManifestUri, "audio.mp4"), uri);
			CollectionAssert.AreEqual(new byte[] { 0x42 }, selectedPssh.InitData);
		}
	}

	[TestMethod]
	[DataRow("file:///tmp/audio.mp4")]
	[DataRow("ftp://cdn.example/audio.mp4")]
	public void Selected_media_requires_an_http_resource(string mediaUri)
	{
		using var mpd = Utf8(SegmentBaseMpd("<Initialization range=\"0-100\" />").Replace("audio.mp4", mediaUri));
		var dash = new MpegDash(mpd);

		Assert.IsFalse(dash.TryGetContent(ManifestUri, Cdm.WidevineContentProtection, RequestedCodec, out _, out _));
	}

	[TestMethod]
	public void Selected_pssh_is_the_signed_challenge_payload_and_license_signature_is_still_required()
	{
		var dash = DashWithRepresentation(EncodedPssh([1, 2, 3]), $"codecs=\"{RequestedCodec}\"");
		Assert.IsTrue(dash.TryGetContent(ManifestUri, Cdm.WidevineContentProtection, RequestedCodec, out _, out var pssh));
		using (pssh)
		{
			var device = WidevinePssSignatureTests.CreateFixtureDevice();
			using var deviceKey = device.CdmKey;
			var cdm = new Cdm(device);
			using var session = cdm.OpenSession(pssh, out var challenge);
			var signedRequest = SignedMessage.Parser.ParseFrom(Convert.FromBase64String(challenge));
			var request = LicenseRequest.Parser.ParseFrom(signedRequest.Msg);
			Assert.HasCount(1, request.ContentId.WidevinePsshData.PsshData);
			CollectionAssert.AreEqual(pssh.InitData, request.ContentId.WidevinePsshData.PsshData[0].ToByteArray());
			Assert.IsTrue(device.VerifyMessage(signedRequest.Msg.ToByteArray(), signedRequest.Signature.ToByteArray()));

			var invalidLicense = new SignedMessage
			{
				Type = SignedMessage.Types.MessageType.License,
				SessionKey = ByteString.CopyFrom(deviceKey.Encrypt(new byte[16], RSAEncryptionPadding.OaepSHA1)),
				Msg = ByteString.CopyFrom(new License().ToByteArray()),
				Signature = ByteString.Empty,
			};
			var error = Assert.Throws<InvalidDataException>(() => session.ParseLicense(Convert.ToBase64String(invalidLicense.ToByteArray())));
			StringAssert.Contains(error.Message, "Message signature is invalid");
		}
	}

	private static string SegmentBaseMpd(string child)
		=> $$"""
			<MPD xmlns="urn:mpeg:dash:schema:mpd:2011" xmlns:cenc="urn:mpeg:cenc:2013" type="static">
			  <Period><AdaptationSet contentType="audio">
			    <ContentProtection schemeIdUri="urn:uuid:{{Cdm.WidevineContentProtection}}"><cenc:pssh>{{EncodedPssh([0x42])}}</cenc:pssh></ContentProtection>
			    <SegmentBase indexRange="101-200">{{child}}</SegmentBase>
			    <Representation codecs="{{RequestedCodec}}"><BaseURL>audio.mp4</BaseURL></Representation>
			  </AdaptationSet></Period>
			</MPD>
			""";

	[TestMethod]
	public void Mpd_scheme_and_embedded_pssh_system_id_must_agree()
	{
		var otherSystem = new Guid("9a04f079-9840-4286-ab92-e65be0885f95");
		var dash = DashWithRepresentation(
			Convert.ToBase64String(PsshBoxBytes(otherSystem, [0x42])),
			$"codecs=\"{RequestedCodec}\"");

		Assert.IsFalse(dash.TryGetContent(
			ManifestUri,
			Cdm.WidevineContentProtection,
			RequestedCodec,
			out _,
			out _));
	}

	[TestMethod]
	public void Malformed_pssh_sizes_and_trailing_bytes_stay_inside_the_try_contract()
	{
		byte[] valid = PsshBoxBytes(Cdm.WidevineContentProtection, [1, 2, 3]);
		byte[] truncated = valid[..^1];
		byte[] trailing = [.. valid, 0xAA];
		byte[] oversizedData = (byte[])valid.Clone();
		BinaryPrimitives.WriteUInt32BigEndian(oversizedData.AsSpan(28, 4), 1_000_000);

		foreach (var (name, bytes) in new[]
		{
			("truncated", truncated),
			("trailing", trailing),
			("oversized-data", oversizedData),
		})
		{
			var dash = DashWithRepresentation(
				Convert.ToBase64String(bytes),
				$"codecs=\"{RequestedCodec}\"");
			Assert.IsFalse(dash.TryGetContent(
				ManifestUri,
				Cdm.WidevineContentProtection,
				RequestedCodec,
				out _,
				out _), name);
		}
	}

	[TestMethod]
	public void Version1_pssh_is_rejected_until_the_published_parser_can_preserve_kids()
	{
		byte[] version1 = PsshBoxBytesV1(
			Cdm.WidevineContentProtection,
			[Guid.Parse("00112233-4455-6677-8899-aabbccddeeff")],
			[1, 2, 3]);
		var dash = DashWithRepresentation(
			Convert.ToBase64String(version1),
			$"codecs=\"{RequestedCodec}\"");

		Assert.IsFalse(dash.TryGetContent(
			ManifestUri,
			Cdm.WidevineContentProtection,
			RequestedCodec,
			out _,
			out _));
	}

	[TestMethod]
	public void Malformed_representation_pssh_does_not_fall_back_to_adaptation_pssh()
	{
		string inheritedPssh = EncodedPssh([0x11]);
		using var mpd = Utf8($$"""
			<MPD xmlns="urn:mpeg:dash:schema:mpd:2011" xmlns:cenc="urn:mpeg:cenc:2013">
			  <Period><AdaptationSet contentType="audio">
			    <ContentProtection schemeIdUri="urn:uuid:{{Cdm.WidevineContentProtection}}"><cenc:pssh>{{inheritedPssh}}</cenc:pssh></ContentProtection>
			    <Representation codecs="{{RequestedCodec}}">
			      <ContentProtection schemeIdUri="urn:uuid:{{Cdm.WidevineContentProtection}}"><cenc:pssh>not-base64</cenc:pssh></ContentProtection>
			      <BaseURL>audio.mp4</BaseURL>
			    </Representation>
			  </AdaptationSet></Period>
			</MPD>
			""");
		var dash = new MpegDash(mpd);

		Assert.IsFalse(dash.TryGetContent(
			ManifestUri,
			Cdm.WidevineContentProtection,
			RequestedCodec,
			out _,
			out _));
	}

	[TestMethod]
	public void Cdm_accepts_selected_version0_and_rejects_direct_version1_pssh()
	{
		using var version0Stream = new MemoryStream(
			PsshBoxBytes(Cdm.WidevineContentProtection, [1, 2, 3]),
			writable: false);
		using var version0 = (PsshBox)BoxFactory.CreateBox(version0Stream, null);
		Cdm.ValidateLicensePssh(version0);

		using var version1Stream = new MemoryStream(
			PsshBoxBytesV1(
				Cdm.WidevineContentProtection,
				[Guid.Parse("00112233-4455-6677-8899-aabbccddeeff")],
				[1, 2, 3]),
			writable: false);
		using var version1 = (PsshBox)BoxFactory.CreateBox(version1Stream, null);

		var exception = Assert.Throws<InvalidDataException>(() => Cdm.ValidateLicensePssh(version1));
		StringAssert.Contains(exception.Message, "version-0");
	}

	private static MpegDash DashWithRepresentation(string encodedPssh, string? representationAttributes)
	{
		string attributes = string.IsNullOrWhiteSpace(representationAttributes)
			? string.Empty
			: " " + representationAttributes;
		return new MpegDash(Utf8($$"""
			<MPD xmlns="urn:mpeg:dash:schema:mpd:2011" xmlns:cenc="urn:mpeg:cenc:2013">
			  <Period><AdaptationSet contentType="audio">
			    <Representation{{attributes}}>
			      <ContentProtection schemeIdUri="urn:uuid:{{Cdm.WidevineContentProtection}}"><cenc:pssh>{{encodedPssh}}</cenc:pssh></ContentProtection>
			      <BaseURL>audio.mp4</BaseURL>
			    </Representation>
			  </AdaptationSet></Period>
			</MPD>
			"""));
	}

	private static MemoryStream Utf8(string value) => new(Encoding.UTF8.GetBytes(value));

	private static string EncodedPssh(byte[] initData)
		=> Convert.ToBase64String(PsshBoxBytes(Cdm.WidevineContentProtection, initData));

	private static byte[] PsshBoxBytes(Guid systemId, byte[] initData)
	{
		byte[] box = new byte[checked(32 + initData.Length)];
		BinaryPrimitives.WriteUInt32BigEndian(box, checked((uint)box.Length));
		"pssh"u8.CopyTo(box.AsSpan(4));
		systemId.TryWriteBytes(box.AsSpan(12, 16), bigEndian: true, out _);
		BinaryPrimitives.WriteUInt32BigEndian(box.AsSpan(28, 4), checked((uint)initData.Length));
		initData.CopyTo(box.AsSpan(32));
		return box;
	}

	private static byte[] PsshBoxBytesV1(Guid systemId, Guid[] kids, byte[] initData)
	{
		byte[] box = new byte[checked(36 + kids.Length * 16 + initData.Length)];
		BinaryPrimitives.WriteUInt32BigEndian(box, checked((uint)box.Length));
		"pssh"u8.CopyTo(box.AsSpan(4));
		box[8] = 1;
		systemId.TryWriteBytes(box.AsSpan(12, 16), bigEndian: true, out _);
		BinaryPrimitives.WriteUInt32BigEndian(box.AsSpan(28, 4), checked((uint)kids.Length));
		int offset = 32;
		foreach (Guid kid in kids)
		{
			kid.TryWriteBytes(box.AsSpan(offset, 16), bigEndian: true, out _);
			offset += 16;
		}
		BinaryPrimitives.WriteUInt32BigEndian(box.AsSpan(offset, 4), checked((uint)initData.Length));
		initData.CopyTo(box.AsSpan(offset + 4));
		return box;
	}
}
