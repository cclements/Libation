using AudibleUtilities.Widevine;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Text;

namespace AudibleUtilities.Tests;

[TestClass]
public class WidevineSessionCompatibilityTests
{
	[TestMethod]
	public void Legacy_session_implementer_requires_only_the_original_interface_members()
	{
		using var mpd = new MemoryStream(Encoding.UTF8.GetBytes(
			"<MPD xmlns=\"urn:mpeg:dash:schema:mpd:2011\" />"));
		var dash = new MpegDash(mpd);
		var legacy = new LegacySession();

		using (ISession session = legacy)
		{
			Assert.AreEqual("legacy-challenge", session.GetLicenseChallenge(dash));
			Assert.HasCount(0, session.ParseLicense("legacy-license"));
		}

		Assert.IsTrue(legacy.Disposed);
	}

	private sealed class LegacySession : ISession
	{
		public bool Disposed { get; private set; }

		public string GetLicenseChallenge(MpegDash dash) => "legacy-challenge";

		public WidevineKey[] ParseLicense(string licenseMessage) => [];

		public void Dispose() => Disposed = true;
	}
}
