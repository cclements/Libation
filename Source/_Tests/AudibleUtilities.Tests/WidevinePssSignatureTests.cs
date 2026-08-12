using AudibleUtilities.Widevine;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace AudibleUtilities.Tests;

[TestClass]
public class WidevinePssSignatureTests
{
	private const string PrivateKey = """
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

	[TestMethod]
	public void SignMessage_left_zero_pads_pss_signature_to_exact_rsa_modulus_width()
	{
		using var fixtureKey = RSA.Create();
		fixtureKey.ImportFromPem(PrivateKey);

		var device = new Device(CreateDeviceData(fixtureKey.ExportRSAPrivateKey()));
		using var deviceKey = device.CdmKey;
		byte[] message = [0x9E, 0x02, 0x00, 0x00];

		var signature = device.SignMessage(message);

		Assert.HasCount((deviceKey.KeySize + 7) / 8, signature);
		Assert.AreEqual((byte)0, signature[0], "The fixed witness must require one leading zero byte.");
		Assert.IsTrue(device.VerifyMessage(message, signature));
	}

	private static byte[] CreateDeviceData(byte[] privateKey)
	{
		byte[] clientIdentification = [0x12, 0x01, 0x00];
		var deviceData = new byte[11 + privateKey.Length + clientIdentification.Length];

		"WVD"u8.CopyTo(deviceData);
		deviceData[3] = 2;
		deviceData[4] = (byte)DeviceTypes.Android;
		deviceData[5] = 3;
		BinaryPrimitives.WriteUInt16BigEndian(deviceData.AsSpan(7, 2), checked((ushort)privateKey.Length));
		privateKey.CopyTo(deviceData.AsSpan(9));

		var clientIdLengthOffset = 9 + privateKey.Length;
		BinaryPrimitives.WriteUInt16BigEndian(
			deviceData.AsSpan(clientIdLengthOffset, 2),
			checked((ushort)clientIdentification.Length));
		clientIdentification.CopyTo(deviceData.AsSpan(clientIdLengthOffset + 2));

		return deviceData;
	}
}
