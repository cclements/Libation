using AudibleUtilities.Widevine;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mpeg4Lib.Boxes;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace AudibleUtilities.Tests;

[TestClass]
public class WidevineProvisioningSecurityTests
{
	private const string ApprovedEndpoint
		= "https://ollj0gz40d.execute-api.us-west-2.amazonaws.com/default/AudibleCdm";

	[TestMethod]
	public void ParseCdmUris_accepts_the_release_anchored_https_endpoint()
	{
		var uris = Cdm.ParseCdmUris(Json(ApprovedEndpoint));

		Assert.HasCount(1, uris);
		Assert.AreEqual(new Uri(ApprovedEndpoint), uris[0]);
	}

	[TestMethod]
	[DataRow("http://ollj0gz40d.execute-api.us-west-2.amazonaws.com/default/AudibleCdm")]
	[DataRow("https://attacker.example/collect")]
	[DataRow("https://ollj0gz40d.execute-api.us-west-2.amazonaws.com/attacker")]
	public void ParseCdmUris_rejects_endpoints_not_anchored_in_the_release(string endpoint)
		=> Assert.Throws<InvalidDataException>(() => Cdm.ParseCdmUris(Json(endpoint)));

	[TestMethod]
	public void Pss_signature_is_serialized_to_the_exact_rsa_modulus_width()
	{
		const string privateKey = """
			-----BEGIN RSA PRIVATE KEY-----
			MIICXQIBAAKBgQC//W2XNdaLALRh5yTL0Vz9uklzT+j74Xzr//Ntzenfq+5BeeP6
			NgWoRSumpP7UgE3x7L3R0eETIz4zhI+WNoAjjjzaEKxTLieg/Aqquv0wYWBW3zJx
			2Xd3+1q5AqXhbgo75Wrzj+GhjYrxx5xSoVv53fdglr3rkvA57xf5DavMfwIDAQAB
			AoGAPtDJwwAT9n3gBupMIT2aec+yCX77QTI5H7QqLuKA4zRLK3QYkbyMJE6hZhA0
			6k0ic4WcY6KSTCMrTkrQefrR+H7ud6fQgry37uP5JH0/unmx3ORvuKssXLbho4IF
			+ewGoQjSEngvRve0/O+Ik7E2zHjco7BlWNCHjE+phwg5Ys0CQQDoM5R/m070DwUe
			Q1G/+J4ROCTer+rEXYlkPju5DVrMVYGAt7Owxp7/PPD5XjU/ma+ElkeT8RKl1X4Z
			xmIOJmcNAkEA06rKy6eYaO+3gAKYSCuiCZ4vvfFx38y37NKMD1iX/aWZzP3BullA
			bGkh8qHclHm8R7t06o1FKnE0Af77cvceuwJBALI5FNu02y7ccHM//Hk6XCifTT1X
			DPzXRmMYmUJ6C50WbCXd2h/u8464ucTNGFXOojdEGYBl4ohCi11BNXXi5+kCQQCm
			e7B0TIbxCpM/KUtTgJY7kGMmt/CEQcXsjJJDQ8CQbZ8x/+lPRAILAwoDiFIxqipw
			FT5ZefIL9uwcIczu2PYfAkB1HYba3SdlzL5icp8w2ezBFdEFX1Obgafe4ja82Jjt
			llXBZXj+MUUN03DDs7DFm57MIUD1KvNYo7wgLp0MuOi0
			-----END RSA PRIVATE KEY-----
			""";

		using var rsa = RSA.Create();
		rsa.ImportFromPem(privateKey);
		var hash = SHA1.HashData(BitConverter.GetBytes(670));

		var signature = Device.PssSha1Signer.SignHash(rsa, hash);

		Assert.HasCount(rsa.KeySize / 8, signature);
		Assert.IsTrue(rsa.VerifyHash(hash, signature, HashAlgorithmName.SHA1, RSASignaturePadding.Pss));
	}

	[TestMethod]
	public void DecodeKeyId_accepts_exact_raw_and_exact_base64_ids()
	{
		byte[] rawId = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];
		var encodedId = Encoding.ASCII.GetBytes(Convert.ToBase64String(rawId));

		CollectionAssert.AreEqual(rawId, Cdm.Session.DecodeKeyId(rawId));
		CollectionAssert.AreEqual(rawId, Cdm.Session.DecodeKeyId(encodedId));
	}

	[TestMethod]
	[DataRow(15)]
	[DataRow(17)]
	public void DecodeKeyId_rejects_nonstandard_raw_lengths(int length)
		=> Assert.Throws<InvalidDataException>(() => Cdm.Session.DecodeKeyId(new byte[length]));

	[TestMethod]
	public void MpegDash_rejects_pssh_whose_embedded_system_id_disagrees_with_the_mpd()
	{
		var pssh = Convert.ToBase64String(PsshBoxBytes(new Guid("9a04f079-9840-4286-ab92-e65be0885f95"), [1, 2, 3]));
		using var mpd = new MemoryStream(Encoding.UTF8.GetBytes($$"""
			<MPD xmlns="urn:mpeg:dash:schema:mpd:2011" xmlns:cenc="urn:mpeg:cenc:2013">
			  <Period><AdaptationSet>
			    <ContentProtection schemeIdUri="urn:uuid:{{Cdm.WidevineContentProtection}}">
			      <cenc:pssh>{{pssh}}</cenc:pssh>
			    </ContentProtection>
			  </AdaptationSet></Period>
			</MPD>
			"""));

		var dash = new MpegDash(mpd);

		Assert.IsFalse(dash.TryGetPssh(Cdm.WidevineContentProtection, out _));
	}

	[TestMethod]
	public void MpegDash_rejects_pssh_with_bytes_after_the_declared_box()
	{
		var box = PsshBoxBytes(Cdm.WidevineContentProtection, [1, 2, 3]);
		var pssh = Convert.ToBase64String([.. box, 0xAA]);
		using var mpd = new MemoryStream(Encoding.UTF8.GetBytes($$"""
			<MPD xmlns="urn:mpeg:dash:schema:mpd:2011" xmlns:cenc="urn:mpeg:cenc:2013">
			  <Period><AdaptationSet>
			    <ContentProtection schemeIdUri="urn:uuid:{{Cdm.WidevineContentProtection}}">
			      <cenc:pssh>{{pssh}}</cenc:pssh>
			    </ContentProtection>
			  </AdaptationSet></Period>
			</MPD>
			"""));

		var dash = new MpegDash(mpd);

		Assert.IsFalse(dash.TryGetPssh(Cdm.WidevineContentProtection, out _));
	}

	[TestMethod]
	public void MpegDash_rejects_truncated_and_oversized_pssh_without_leaving_the_try_contract()
	{
		byte[] valid = PsshBoxBytes(Cdm.WidevineContentProtection, [1, 2, 3]);
		byte[] truncated = valid[..^1];
		byte[] oversized = (byte[])valid.Clone();
		WriteUInt32BE(oversized.AsSpan(28, 4), 1_000_000);

		foreach (var (name, bytes) in new[] { ("truncated", truncated), ("oversized", oversized) })
		{
			var dash = DashWithPssh(bytes);
			Assert.IsFalse(dash.TryGetPssh(Cdm.WidevineContentProtection, out _), name);
		}
	}

	[TestMethod]
	public void MpegDash_rejects_version1_pssh_until_its_kids_can_be_preserved_in_the_license_request()
	{
		byte[] version1 = PsshBoxBytesV1(
			Cdm.WidevineContentProtection,
			[Guid.Parse("00112233-4455-6677-8899-aabbccddeeff")],
			[1, 2, 3]);
		var dash = DashWithPssh(version1);

		Assert.IsFalse(dash.TryGetPssh(Cdm.WidevineContentProtection, out _));
	}

	[TestMethod]
	public void Cdm_accepts_version0_and_rejects_a_direct_version1_pssh_before_license_generation()
	{
		using var version0Stream = new MemoryStream(
			PsshBoxBytes(Cdm.WidevineContentProtection, [1, 2, 3]),
			writable: false);
		using var version0 = (PsshBox)BoxFactory.CreateBox(version0Stream, null);
		Cdm.ValidateLicensePssh(version0);

		byte[] version1 = PsshBoxBytesV1(
			Cdm.WidevineContentProtection,
			[Guid.Parse("00112233-4455-6677-8899-aabbccddeeff")],
			[1, 2, 3]);
		using var stream = new MemoryStream(version1, writable: false);
		using var pssh = (PsshBox)BoxFactory.CreateBox(stream, null);

		var exception = Assert.Throws<InvalidDataException>(() => Cdm.ValidateLicensePssh(pssh));
		StringAssert.Contains(exception.Message, "version-0");
	}

	[TestMethod]
	public void MpegDash_binds_the_audio_url_and_pssh_to_the_same_representation()
	{
		var videoPssh = Convert.ToBase64String(PsshBoxBytes(Cdm.WidevineContentProtection, [0x11]));
		var audioPssh = Convert.ToBase64String(PsshBoxBytes(Cdm.WidevineContentProtection, [0x22]));
		using var mpd = new MemoryStream(Encoding.UTF8.GetBytes($$"""
			<MPD xmlns="urn:mpeg:dash:schema:mpd:2011" xmlns:cenc="urn:mpeg:cenc:2013">
			  <Period>
			    <AdaptationSet contentType="video">
			      <ContentProtection schemeIdUri="urn:uuid:{{Cdm.WidevineContentProtection}}"><cenc:pssh>{{videoPssh}}</cenc:pssh></ContentProtection>
			      <Representation><BaseURL>video.mp4</BaseURL></Representation>
			    </AdaptationSet>
			    <AdaptationSet contentType="audio">
			      <Representation>
			        <ContentProtection schemeIdUri="urn:uuid:{{Cdm.WidevineContentProtection}}"><cenc:pssh>{{audioPssh}}</cenc:pssh></ContentProtection>
			        <BaseURL>audio.mp4</BaseURL>
			      </Representation>
			    </AdaptationSet>
			  </Period>
			</MPD>
			"""));

		var dash = new MpegDash(mpd);

		Assert.IsTrue(dash.TryGetContent(
			new Uri("https://cdn.example/books/manifest.mpd"),
			Cdm.WidevineContentProtection,
			out var uri,
			out var pssh));
		Assert.AreEqual(new Uri("https://cdn.example/books/audio.mp4"), uri);
		CollectionAssert.AreEqual(new byte[] { 0x22 }, pssh.InitData);
	}

	[TestMethod]
	public void MpegDash_selects_the_requested_codec_within_audio_representations()
	{
		var lcPssh = Convert.ToBase64String(PsshBoxBytes(Cdm.WidevineContentProtection, [0x02]));
		var usacPssh = Convert.ToBase64String(PsshBoxBytes(Cdm.WidevineContentProtection, [0x42]));
		using var mpd = new MemoryStream(Encoding.UTF8.GetBytes($$"""
			<MPD xmlns="urn:mpeg:dash:schema:mpd:2011" xmlns:cenc="urn:mpeg:cenc:2013">
			  <Period><AdaptationSet contentType="audio">
			    <Representation codecs="mp4a.40.2">
			      <ContentProtection schemeIdUri="urn:uuid:{{Cdm.WidevineContentProtection}}"><cenc:pssh>{{lcPssh}}</cenc:pssh></ContentProtection>
			      <BaseURL>lc.mp4</BaseURL>
			    </Representation>
			    <Representation codecs="mp4a.40.42">
			      <ContentProtection schemeIdUri="urn:uuid:{{Cdm.WidevineContentProtection}}"><cenc:pssh>{{usacPssh}}</cenc:pssh></ContentProtection>
			      <BaseURL>usac.mp4</BaseURL>
			    </Representation>
			  </AdaptationSet></Period>
			</MPD>
			"""));

		var dash = new MpegDash(mpd);

		Assert.IsTrue(dash.TryGetContent(
			new Uri("https://cdn.example/manifest.mpd"),
			Cdm.WidevineContentProtection,
			"mp4a.40.42",
			out var uri,
			out var pssh));
		Assert.AreEqual(new Uri("https://cdn.example/usac.mp4"), uri);
		CollectionAssert.AreEqual(new byte[] { 0x42 }, pssh.InitData);
	}

	[TestMethod]
	public void MpegDash_does_not_treat_missing_codec_identity_as_the_requested_codec()
	{
		var unknownPssh = Convert.ToBase64String(PsshBoxBytes(Cdm.WidevineContentProtection, [0x00]));
		var usacPssh = Convert.ToBase64String(PsshBoxBytes(Cdm.WidevineContentProtection, [0x42]));
		using var mpd = new MemoryStream(Encoding.UTF8.GetBytes($$"""
			<MPD xmlns="urn:mpeg:dash:schema:mpd:2011" xmlns:cenc="urn:mpeg:cenc:2013">
			  <Period><AdaptationSet contentType="audio">
			    <Representation>
			      <ContentProtection schemeIdUri="urn:uuid:{{Cdm.WidevineContentProtection}}"><cenc:pssh>{{unknownPssh}}</cenc:pssh></ContentProtection>
			      <BaseURL>unknown.mp4</BaseURL>
			    </Representation>
			    <Representation codecs="mp4a.40.42">
			      <ContentProtection schemeIdUri="urn:uuid:{{Cdm.WidevineContentProtection}}"><cenc:pssh>{{usacPssh}}</cenc:pssh></ContentProtection>
			      <BaseURL>usac.mp4</BaseURL>
			    </Representation>
			  </AdaptationSet></Period>
			</MPD>
			"""));

		var dash = new MpegDash(mpd);

		Assert.IsTrue(dash.TryGetContent(
			new Uri("https://cdn.example/manifest.mpd"),
			Cdm.WidevineContentProtection,
			"mp4a.40.42",
			out var uri,
			out var pssh));
		Assert.AreEqual(new Uri("https://cdn.example/usac.mp4"), uri);
		CollectionAssert.AreEqual(new byte[] { 0x42 }, pssh.InitData);
	}

	[TestMethod]
	public void Content_key_selection_excludes_noncontent_keys_and_rejects_duplicate_kids()
	{
		var signing = MakeKey(Guid.NewGuid(), KeyType.Signing, 0x01);
		var contentKid = Guid.NewGuid();
		var content = MakeKey(contentKid, KeyType.Content, 0x02);
		var oemContent = MakeKey(Guid.NewGuid(), KeyType.OemContent, 0x03);

		var selected = Cdm.Session.SelectContentKeys([signing, content, oemContent]);

		Assert.HasCount(2, selected);
		Assert.IsTrue(selected.All(key => key.Type is KeyType.Content or KeyType.OemContent));
		Assert.Throws<InvalidDataException>(() => Cdm.Session.SelectContentKeys([signing]));
		Assert.Throws<InvalidDataException>(() =>
			Cdm.Session.SelectContentKeys([content, MakeKey(contentKid, KeyType.OemContent, 0x04)]));
	}

	[TestMethod]
	public void MpegDash_resolves_inherited_base_urls_and_adaptation_protection_together()
	{
		var encodedPssh = Convert.ToBase64String(PsshBoxBytes(Cdm.WidevineContentProtection, [0x42]));
		using var mpd = new MemoryStream(Encoding.UTF8.GetBytes($$"""
			<MPD xmlns="urn:mpeg:dash:schema:mpd:2011" xmlns:cenc="urn:mpeg:cenc:2013">
			  <BaseURL>cdn/</BaseURL>
			  <Period><BaseURL>period/</BaseURL>
			    <AdaptationSet contentType="audio"><BaseURL>adaptation/</BaseURL>
			      <ContentProtection schemeIdUri="urn:uuid:{{Cdm.WidevineContentProtection}}"><cenc:pssh>{{encodedPssh}}</cenc:pssh></ContentProtection>
			      <Representation codecs="mp4a.40.42"><BaseURL>audio.mp4</BaseURL></Representation>
			    </AdaptationSet>
			  </Period>
			</MPD>
			"""));

		var dash = new MpegDash(mpd);

		Assert.IsTrue(dash.TryGetContent(
			new Uri("https://cdn.example/books/manifest.mpd"),
			Cdm.WidevineContentProtection,
			"mp4a.40.42",
			out var uri,
			out var pssh));
		Assert.AreEqual(new Uri("https://cdn.example/books/cdn/period/adaptation/audio.mp4"), uri);
		CollectionAssert.AreEqual(new byte[] { 0x42 }, pssh.InitData);
	}

	[TestMethod]
	public void MpegDash_requires_a_direct_representation_base_url()
	{
		var encodedPssh = Convert.ToBase64String(PsshBoxBytes(Cdm.WidevineContentProtection, [0x42]));
		using var mpd = new MemoryStream(Encoding.UTF8.GetBytes($$"""
			<MPD xmlns="urn:mpeg:dash:schema:mpd:2011" xmlns:cenc="urn:mpeg:cenc:2013">
			  <Period><AdaptationSet contentType="audio">
			    <ContentProtection schemeIdUri="urn:uuid:{{Cdm.WidevineContentProtection}}"><cenc:pssh>{{encodedPssh}}</cenc:pssh></ContentProtection>
			    <Representation codecs="mp4a.40.42" />
			  </AdaptationSet></Period>
			</MPD>
			"""));

		var dash = new MpegDash(mpd);

		Assert.IsFalse(dash.TryGetContent(
			new Uri("https://cdn.example/manifest.mpd"),
			Cdm.WidevineContentProtection,
			"mp4a.40.42",
			out _,
			out _));
	}

	[TestMethod]
	public void MpegDash_rejects_unsupported_segment_template_addressing()
	{
		var encodedPssh = Convert.ToBase64String(PsshBoxBytes(Cdm.WidevineContentProtection, [0x42]));
		using var mpd = new MemoryStream(Encoding.UTF8.GetBytes($$"""
			<MPD xmlns="urn:mpeg:dash:schema:mpd:2011" xmlns:cenc="urn:mpeg:cenc:2013">
			  <Period><AdaptationSet contentType="audio">
			    <ContentProtection schemeIdUri="urn:uuid:{{Cdm.WidevineContentProtection}}"><cenc:pssh>{{encodedPssh}}</cenc:pssh></ContentProtection>
			    <SegmentTemplate media="segment-$Number$.m4s" />
			    <Representation codecs="mp4a.40.42"><BaseURL>audio/</BaseURL></Representation>
			  </AdaptationSet></Period>
			</MPD>
			"""));

		var dash = new MpegDash(mpd);

		Assert.IsFalse(dash.TryGetContent(
			new Uri("https://cdn.example/manifest.mpd"),
			Cdm.WidevineContentProtection,
			"mp4a.40.42",
			out _,
			out _));
	}

	private static string Json(string endpoint)
		=> $$"""
			{
			  "CdmUrls": ["{{endpoint}}"]
			}
			""";

	private static WidevineKey MakeKey(Guid kid, KeyType type, byte fill)
		=> new(kid, (License.Types.KeyContainer.Types.KeyType)type, Enumerable.Repeat(fill, 16).ToArray());

	private static MpegDash DashWithPssh(byte[] psshBytes)
	{
		var pssh = Convert.ToBase64String(psshBytes);
		return new MpegDash(new MemoryStream(Encoding.UTF8.GetBytes($$"""
			<MPD xmlns="urn:mpeg:dash:schema:mpd:2011" xmlns:cenc="urn:mpeg:cenc:2013">
			  <Period><AdaptationSet>
			    <ContentProtection schemeIdUri="urn:uuid:{{Cdm.WidevineContentProtection}}">
			      <cenc:pssh>{{pssh}}</cenc:pssh>
			    </ContentProtection>
			  </AdaptationSet></Period>
			</MPD>
			""")));
	}

	private static byte[] PsshBoxBytes(Guid systemId, byte[] initData)
	{
		using var stream = new MemoryStream();
		using var writer = new BinaryWriter(stream);
		writer.Write(System.Net.IPAddress.HostToNetworkOrder(32 + initData.Length));
		writer.Write(Encoding.ASCII.GetBytes("pssh"));
		writer.Write(0);
		writer.Write(systemId.ToByteArray(bigEndian: true));
		writer.Write(System.Net.IPAddress.HostToNetworkOrder(initData.Length));
		writer.Write(initData);
		return stream.ToArray();
	}

	private static byte[] PsshBoxBytesV1(Guid systemId, Guid[] kids, byte[] initData)
	{
		using var stream = new MemoryStream();
		using var writer = new BinaryWriter(stream);
		writer.Write(System.Net.IPAddress.HostToNetworkOrder(36 + kids.Length * 16 + initData.Length));
		writer.Write(Encoding.ASCII.GetBytes("pssh"));
		writer.Write(System.Net.IPAddress.HostToNetworkOrder(1 << 24));
		writer.Write(systemId.ToByteArray(bigEndian: true));
		writer.Write(System.Net.IPAddress.HostToNetworkOrder(kids.Length));
		foreach (var kid in kids)
			writer.Write(kid.ToByteArray(bigEndian: true));
		writer.Write(System.Net.IPAddress.HostToNetworkOrder(initData.Length));
		writer.Write(initData);
		return stream.ToArray();
	}

	private static void WriteUInt32BE(Span<byte> destination, uint value)
	{
		destination[0] = (byte)(value >> 24);
		destination[1] = (byte)(value >> 16);
		destination[2] = (byte)(value >> 8);
		destination[3] = (byte)value;
	}
}
