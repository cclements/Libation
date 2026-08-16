using AudibleUtilities.Widevine;
using Google.Protobuf;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace AudibleUtilities.Tests;

[TestClass]
public class WidevineContentKeyContractTests
{
	private static readonly byte[] KeyEncryptionKey = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();

	[TestMethod]
	public void DecryptKeys_accepts_exact_raw_and_exact_base64_kids()
	{
		byte[] rawKid = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];
		byte[] encodedKidBytes = [15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0];
		byte[] encodedKid = Encoding.ASCII.GetBytes(Convert.ToBase64String(encodedKidBytes));

		var keys = Cdm.Session.DecryptKeys(KeyEncryptionKey,
		[
			EncryptedKey(rawKid, KeyType.Content, 0x21),
			EncryptedKey(encodedKid, KeyType.OemContent, 0x43),
		]);

		Assert.HasCount(2, keys);
		Assert.AreEqual(new Guid(rawKid, bigEndian: true), keys[0].Kid);
		Assert.AreEqual(new Guid(encodedKidBytes, bigEndian: true), keys[1].Kid);
		CollectionAssert.AreEqual(Enumerable.Repeat((byte)0x21, 16).ToArray(), keys[0].Key);
		CollectionAssert.AreEqual(Enumerable.Repeat((byte)0x43, 16).ToArray(), keys[1].Key);
	}

	[TestMethod]
	public void DecryptKeys_rejects_non_sixteen_byte_raw_and_decoded_kids()
	{
		foreach (var (name, id) in new[]
		{
			("short raw", new byte[15]),
			("long raw", new byte[17]),
			("short base64", Encoding.ASCII.GetBytes(Convert.ToBase64String(new byte[15]))),
			("long base64", Encoding.ASCII.GetBytes(Convert.ToBase64String(new byte[17]))),
			("invalid base64", Encoding.ASCII.GetBytes("not-a-valid-base64-key!")),
		})
		{
			Assert.Throws<InvalidDataException>(
				() => Cdm.Session.DecryptKeys(KeyEncryptionKey, [EncryptedKey(id, KeyType.Content, 0x21)]),
				name);
		}
	}

	[TestMethod]
	public void DecryptKeys_skips_noncontent_containers_before_decryption()
	{
		byte[] contentKid = Guid.NewGuid().ToByteArray(bigEndian: true);
		byte[] oemContentKid = Guid.NewGuid().ToByteArray(bigEndian: true);
		var licenseKeys = new List<License.Types.KeyContainer>
		{
			MalformedKey(KeyType.Signing),
			MalformedKey(KeyType.KeyControl),
			MalformedKey(KeyType.OperatorSession),
			MalformedKey(KeyType.Entitlement),
			MalformedKey((KeyType)99),
			EncryptedKey(contentKid, KeyType.Content, 0x21),
			EncryptedKey(oemContentKid, KeyType.OemContent, 0x43),
		};

		var keys = Cdm.Session.DecryptKeys(KeyEncryptionKey, licenseKeys);

		Assert.HasCount(2, keys);
		Assert.IsTrue(keys.All(key => key.Type is KeyType.Content or KeyType.OemContent));
		CollectionAssert.AreEqual(
			new[] { new Guid(contentKid, bigEndian: true), new Guid(oemContentKid, bigEndian: true) },
			keys.Select(key => key.Kid).ToArray());
	}

	[TestMethod]
	public void DecryptKeys_fails_closed_without_a_content_key()
	{
		var signing = EncryptedKey(Guid.NewGuid().ToByteArray(bigEndian: true), KeyType.Signing, 0x01);

		Assert.Throws<InvalidDataException>(() => Cdm.Session.DecryptKeys(KeyEncryptionKey, [signing]));
	}

	[TestMethod]
	public void DecryptKeys_preserves_duplicate_kids_with_identical_key_material()
	{
		byte[] kid = Guid.NewGuid().ToByteArray(bigEndian: true);

		var keys = Cdm.Session.DecryptKeys(KeyEncryptionKey,
		[
			EncryptedKey(kid, KeyType.Content, 0x21),
			EncryptedKey(kid, KeyType.OemContent, 0x21),
		]);

		Assert.HasCount(2, keys);
		Assert.AreEqual(new Guid(kid, bigEndian: true), keys[0].Kid);
		Assert.AreEqual(new Guid(kid, bigEndian: true), keys[1].Kid);
		Assert.AreEqual(KeyType.Content, keys[0].Type);
		Assert.AreEqual(KeyType.OemContent, keys[1].Type);
		CollectionAssert.AreEqual(Enumerable.Repeat((byte)0x21, 16).ToArray(), keys[0].Key);
		CollectionAssert.AreEqual(Enumerable.Repeat((byte)0x21, 16).ToArray(), keys[1].Key);
	}

	[TestMethod]
	public void DecryptKeys_fails_closed_on_duplicate_kids_with_conflicting_key_material()
	{
		byte[] kid = Guid.NewGuid().ToByteArray(bigEndian: true);

		Assert.Throws<InvalidDataException>(() => Cdm.Session.DecryptKeys(KeyEncryptionKey,
		[
			EncryptedKey(kid, KeyType.Content, 0x21),
			EncryptedKey(kid, KeyType.OemContent, 0x43),
		]));
	}

	private static License.Types.KeyContainer EncryptedKey(byte[] id, KeyType type, byte fill)
	{
		byte[] iv = Enumerable.Repeat((byte)0xA5, 16).ToArray();
		byte[] key = Enumerable.Repeat(fill, 16).ToArray();
		using var aes = Aes.Create();
		aes.Key = KeyEncryptionKey;
		return new()
		{
			Id = ByteString.CopyFrom(id),
			Iv = ByteString.CopyFrom(iv),
			Key = ByteString.CopyFrom(aes.EncryptCbc(key, iv, PaddingMode.PKCS7)),
			Type = (License.Types.KeyContainer.Types.KeyType)type,
		};
	}

	private static License.Types.KeyContainer MalformedKey(KeyType type)
		=> new()
		{
			Id = ByteString.Empty,
			Iv = ByteString.Empty,
			Key = ByteString.Empty,
			Type = (License.Types.KeyContainer.Types.KeyType)type,
		};
}
