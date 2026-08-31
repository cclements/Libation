using System;
using System.Text.RegularExpressions;

namespace LibationUiBase.Diagnostics;

/// <summary>
/// Removes common credentials, account identifiers, and user-profile roots from
/// diagnostic text before it is displayed or copied. Full exceptions remain in
/// the established structured log; this class protects presentation surfaces.
/// </summary>
public static partial class DiagnosticTextScrubber
{
	public static string? Scrub(string? text)
	{
		if (string.IsNullOrEmpty(text))
			return text;

		var scrubbed = UriUserInfoRegex().Replace(text, "$1[redacted]@");
		scrubbed = AuthorizationValueRegex().Replace(scrubbed, "$1[redacted]");
		scrubbed = SecretValueRegex().Replace(scrubbed, "$1[redacted]");
		scrubbed = PemBlockRegex().Replace(scrubbed, "[redacted private key]");
		scrubbed = EmailRegex().Replace(scrubbed, "[redacted account]");

		var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		if (!string.IsNullOrWhiteSpace(profile))
			scrubbed = scrubbed.Replace(profile, "[redacted user path]", StringComparison.OrdinalIgnoreCase);

		return UserProfilePathRegex().Replace(scrubbed, "[redacted user path]");
	}

	[GeneratedRegex("""(?i)(["']?\b(?:access[_-]?token|refresh[_-]?token|id[_-]?token|adp[_-]?token|device[_-]?token|password|passwd|client[_-]?secret|private[_-]?key|cookie|account[_-]?(?:id|name|email)|customer[_-]?id|username)\b["']?\s*[:=]\s*)(?:"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'|[^\s,;}\]]+)""")]
	private static partial Regex SecretValueRegex();

	[GeneratedRegex("""(?i)(["']?\bauthorization\b["']?\s*[:=]\s*)(?:"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'|(?:(?:bearer|basic)\s+)?[^\s,;}\]]+)""")]
	private static partial Regex AuthorizationValueRegex();

	[GeneratedRegex(@"(?i)(\b[a-z][a-z0-9+.-]*://)[^/\s:@]+(?::[^/\s@]*)?@")]
	private static partial Regex UriUserInfoRegex();

	[GeneratedRegex(@"(?is)-----BEGIN [^-\r\n]+-----.*?-----END [^-\r\n]+-----")]
	private static partial Regex PemBlockRegex();

	[GeneratedRegex(@"(?i)\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b")]
	private static partial Regex EmailRegex();

	[GeneratedRegex("""(?i)(?:[A-Z]:\\+Users\\+[^\\\s"']+|/(?:Users|home|var/home)/[^/\s"']+)""")]
	private static partial Regex UserProfilePathRegex();
}
