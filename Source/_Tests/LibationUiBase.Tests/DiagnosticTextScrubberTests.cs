using LibationUiBase.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LibationUiBase.Tests;

[TestClass]
public class DiagnosticTextScrubberTests
{
	[TestMethod]
	public void null_and_empty_diagnostics_are_unchanged()
	{
		Assert.IsNull(DiagnosticTextScrubber.Scrub(null));
		Assert.AreEqual(string.Empty, DiagnosticTextScrubber.Scrub(string.Empty));
	}

	[TestMethod]
	[DataRow("access_token=access-secret", "access-secret")]
	[DataRow("refreshToken: 'refresh-secret'", "refresh-secret")]
	[DataRow("{\"id_token\":\"id-secret\"}", "id-secret")]
	[DataRow("{\"accountId\":\"account-123\"}", "account-123")]
	[DataRow("""{"clientSecret":"value-with-\"quoted\"-part"}""", "value-with")]
	public void keyed_secrets_are_redacted_from_plain_text_and_json(string diagnostic, string secretFragment)
	{
		var scrubbed = DiagnosticTextScrubber.Scrub(diagnostic);

		Assert.IsNotNull(scrubbed);
		StringAssert.Contains(scrubbed, "[redacted]");
		Assert.IsFalse(scrubbed.Contains(secretFragment, StringComparison.Ordinal));
	}

	[TestMethod]
	public void bearer_authorization_is_redacted_as_one_credential()
	{
		const string bearerToken = "bearer-secret-value";

		var scrubbed = DiagnosticTextScrubber.Scrub($"Authorization: Bearer {bearerToken}");

		Assert.IsNotNull(scrubbed);
		StringAssert.Contains(scrubbed, "[redacted]");
		Assert.IsFalse(scrubbed.Contains(bearerToken, StringComparison.Ordinal));
	}

	[TestMethod]
	[DataRow("account-name:password-value")]
	[DataRow("authorization:authorization-value")]
	public void uri_user_information_is_redacted_without_removing_the_destination(string userInformation)
	{
		var scrubbed = DiagnosticTextScrubber.Scrub($"Request failed for https://{userInformation}@example.test/library");

		Assert.AreEqual("Request failed for https://[redacted]@example.test/library", scrubbed);
	}

	[TestMethod]
	public void private_key_blocks_are_redacted_in_full()
	{
		const string keyMaterial = "private-key-material";
		var diagnostic = $"Before\n-----BEGIN PRIVATE KEY-----\n{keyMaterial}\n-----END PRIVATE KEY-----\nAfter";

		var scrubbed = DiagnosticTextScrubber.Scrub(diagnostic);

		Assert.IsNotNull(scrubbed);
		StringAssert.Contains(scrubbed, "[redacted private key]");
		Assert.IsFalse(scrubbed.Contains(keyMaterial, StringComparison.Ordinal));
	}

	[TestMethod]
	public void account_email_and_user_profile_paths_are_redacted()
	{
		const string diagnostic = "Account reader@example.test read /Users/reader/Library/a.log, /home/reader/.config/b.log, and C:\\Users\\reader\\AppData\\c.log";

		var scrubbed = DiagnosticTextScrubber.Scrub(diagnostic);

		Assert.IsNotNull(scrubbed);
		StringAssert.Contains(scrubbed, "[redacted account]");
		StringAssert.Contains(scrubbed, "[redacted user path]");
		Assert.IsFalse(scrubbed.Contains("reader@example.test", StringComparison.Ordinal));
		Assert.IsFalse(scrubbed.Contains("/Users/reader", StringComparison.Ordinal));
		Assert.IsFalse(scrubbed.Contains("/home/reader", StringComparison.Ordinal));
		Assert.IsFalse(scrubbed.Contains(@"C:\Users\reader", StringComparison.Ordinal));
	}

	[TestMethod]
	public void ordinary_diagnostic_context_is_preserved()
	{
		const string diagnostic = "Download failed at chapter 12 with status 503.";

		Assert.AreEqual(diagnostic, DiagnosticTextScrubber.Scrub(diagnostic));
	}
}
