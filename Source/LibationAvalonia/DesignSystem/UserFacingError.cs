using AudibleUtilities;
using LibationFileManager;
using LibationUiBase.Diagnostics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security;
using System.Security.Authentication;
using System.Text;

namespace LibationAvalonia.DesignSystem;

public enum UserFacingErrorCategory
{
	Authentication,
	Network,
	MarketplaceTitleAvailability,
	DiskSpace,
	FilePermissions,
	OutputConflict,
	InvalidSource,
	Conversion,
	Metadata,
	Cancelled,
	Unknown,
}

public enum ErrorSeverity
{
	Information,
	Warning,
	Error,
}

/// <summary>
/// Stable presentation contract for a recoverable application failure. Technical
/// details are scrubbed before they enter this object; the primary message never
/// includes a stack trace.
/// </summary>
public sealed record UserFacingError(
	string Title,
	string Summary,
	string? RecommendedAction,
	ErrorSeverity Severity,
	bool CanRetry,
	bool CanOpenSettings,
	bool CanRevealPath,
	string? TechnicalDetails,
	string CorrelationId)
{
	public UserFacingErrorCategory Category { get; init; } = UserFacingErrorCategory.Unknown;

	public string PrimaryMessage
	{
		get
		{
			var recovery = string.IsNullOrWhiteSpace(RecommendedAction) ? string.Empty : $" {RecommendedAction}";
			return UserFacingErrorFactory.Scrub($"{Summary}{recovery} Reference: {CorrelationId}");
		}
	}

	public string ToDiagnosticText()
	{
		var details = new StringBuilder()
			.AppendLine(Title)
			.Append("Category: ").AppendLine(Category.ToDisplayName())
			.Append("Severity: ").AppendLine(Severity.ToString())
			.Append("Summary: ").AppendLine(Summary);

		if (!string.IsNullOrWhiteSpace(RecommendedAction))
			details.Append("Recommended action: ").AppendLine(RecommendedAction);

		details
			.Append("Retry available: ").AppendLine(CanRetry ? "Yes" : "No")
			.Append("Settings available: ").AppendLine(CanOpenSettings ? "Yes" : "No")
			.Append("Reveal path available: ").AppendLine(CanRevealPath ? "Yes" : "No")
			.Append("Correlation ID: ").AppendLine(CorrelationId);

		if (!string.IsNullOrWhiteSpace(TechnicalDetails))
			details.AppendLine().AppendLine("Technical details:").Append(TechnicalDetails);

		return UserFacingErrorFactory.Scrub(details.ToString());
	}
}

public static class UserFacingErrorCategoryExtensions
{
	public static string ToDisplayName(this UserFacingErrorCategory category)
		=> category == UserFacingErrorCategory.MarketplaceTitleAvailability
			? "Marketplace/title availability"
			: category.ToString();
}

/// <summary>
/// Maps owner-layer exceptions into the presentation taxonomy and removes secrets
/// and profile roots before diagnostics reach user-facing presentation surfaces.
/// </summary>
public static class UserFacingErrorFactory
{
	public static UserFacingError FromMessage(
		UserFacingErrorCategory category,
		string title,
		string summary,
		string? recommendedAction,
		ErrorSeverity severity,
		bool canRetry,
		bool canOpenSettings,
		bool canRevealPath,
		string? technicalDetails = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(title);
		ArgumentException.ThrowIfNullOrWhiteSpace(summary);
		return new(
			Scrub(title),
			Scrub(summary),
			string.IsNullOrWhiteSpace(recommendedAction) ? null : Scrub(recommendedAction),
			severity,
			canRetry,
			canOpenSettings,
			canRevealPath,
			string.IsNullOrWhiteSpace(technicalDetails) ? null : Scrub(technicalDetails),
			Guid.NewGuid().ToString("D"))
		{
			Category = category,
		};
	}

	public static UserFacingError FromException(Exception exception, string operation, string summary)
	{
		ArgumentNullException.ThrowIfNull(exception);
		ArgumentException.ThrowIfNullOrWhiteSpace(operation);
		ArgumentException.ThrowIfNullOrWhiteSpace(summary);

		var category = Classify(exception, operation);
		var descriptor = Describe(category);
		var technicalDetails = Scrub($"Operation: {operation}{Environment.NewLine}{exception}");

		return new UserFacingError(
			descriptor.Title,
			summary,
			descriptor.RecommendedAction,
			descriptor.Severity,
			descriptor.CanRetry,
			descriptor.CanOpenSettings,
			descriptor.CanRevealPath,
			technicalDetails,
			Guid.NewGuid().ToString("D"))
		{
			Category = category,
		};
	}

	public static string Scrub(string? value)
		=> DiagnosticTextScrubber.Scrub(value) ?? string.Empty;

	private static UserFacingErrorCategory Classify(Exception exception, string operation)
	{
		var exceptions = EnumerateExceptions(exception).ToArray();
		var leafExceptions = exceptions.Where(item => item is not AggregateException).ToArray();
		var context = string.Join(' ', exceptions.Select(item => $"{item.GetType().Name} {item.Message}"));

		if (leafExceptions.Any(item => item is OperationCanceledException)
			&& leafExceptions.All(item => item is OperationCanceledException))
			return UserFacingErrorCategory.Cancelled;
		if (AuthenticationExceptionHelper.IsAuthenticationFailure(exception)
			|| exceptions.Any(item => item is AuthenticationException or InvalidCredentialException)
			|| ContainsAny(context, "authentication required", "not authenticated", "unauthorized", "sign in again", "login required"))
			return UserFacingErrorCategory.Authentication;
		if (exceptions.Any(DiskSpaceHelper.IsDiskFullException))
			return UserFacingErrorCategory.DiskSpace;
		if (exceptions.Any(item => item is UnauthorizedAccessException or SecurityException)
			|| ContainsAny(context, "permission denied", "access denied", "read-only file system", "not writable"))
			return UserFacingErrorCategory.FilePermissions;
		if (exceptions.Any(item => item is HttpRequestException or WebException or SocketException or TimeoutException)
			|| ContainsAny(context, "network is unreachable", "connection refused", "connection reset", "name resolution", "dns failure", "tls handshake"))
			return UserFacingErrorCategory.Network;
		if (ContainsAny(context, "already exists", "output conflict", "file is in use", "being used by another process", "write.lock", "cannot overwrite"))
			return UserFacingErrorCategory.OutputConflict;
		if (ContainsAny(context, "marketplace", "title is unavailable", "title unavailable", "not available in this catalog", "not available in this marketplace"))
			return UserFacingErrorCategory.MarketplaceTitleAvailability;
		if (ContainsAny(operation, "convert", "conversion", "decrypt", "encode", "liberate", "process pending"))
			return UserFacingErrorCategory.Conversion;
		if (ContainsAny(operation, "metadata", "tag", "pdf status")
			|| ContainsAny(context, "metadata", "tag write", "id3"))
			return UserFacingErrorCategory.Metadata;
		if (exceptions.Any(item => item is FileNotFoundException or DirectoryNotFoundException or InvalidDataException or FormatException or ArgumentException)
			|| ContainsAny(operation, "locate", "import")
			|| ContainsAny(context, "invalid source", "unsupported source", "source file"))
			return UserFacingErrorCategory.InvalidSource;

		return UserFacingErrorCategory.Unknown;
	}

	private static IEnumerable<Exception> EnumerateExceptions(Exception root)
	{
		var pending = new Stack<Exception>();
		var seen = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
		pending.Push(root);

		while (pending.TryPop(out var current))
		{
			if (!seen.Add(current))
				continue;

			yield return current;
			if (current.InnerException is not null)
				pending.Push(current.InnerException);
			if (current is AggregateException aggregate)
			{
				foreach (var inner in aggregate.InnerExceptions)
					pending.Push(inner);
			}
		}
	}

	private static bool ContainsAny(string value, params string[] fragments)
		=> fragments.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase));

	private static ErrorDescriptor Describe(UserFacingErrorCategory category)
		=> category switch
		{
			UserFacingErrorCategory.Authentication => new(
				"Sign-in required", "Open Accounts, sign in again, then retry.", ErrorSeverity.Error, true, true, false),
			UserFacingErrorCategory.Network => new(
				"Connection unavailable", "Check the connection, then retry.", ErrorSeverity.Error, true, false, false),
			UserFacingErrorCategory.MarketplaceTitleAvailability => new(
				"Title unavailable", "Confirm that the title belongs to the selected account and marketplace, then scan again.", ErrorSeverity.Warning, true, true, false),
			UserFacingErrorCategory.DiskSpace => new(
				"Not enough storage", "Free storage or choose another Books or In progress folder, then retry.", ErrorSeverity.Error, true, true, true),
			UserFacingErrorCategory.FilePermissions => new(
				"File access blocked", "Choose a writable folder or update its permissions, then retry.", ErrorSeverity.Error, true, true, true),
			UserFacingErrorCategory.OutputConflict => new(
				"Output conflict", "Review the existing output before choosing whether to replace it or use another location.", ErrorSeverity.Warning, true, true, true),
			UserFacingErrorCategory.InvalidSource => new(
				"Source could not be used", "Choose a valid local audiobook file or folder, then retry.", ErrorSeverity.Error, true, true, true),
			UserFacingErrorCategory.Conversion => new(
				"Conversion failed", "Review Processing and the application log, then retry the affected title.", ErrorSeverity.Error, true, false, true),
			UserFacingErrorCategory.Metadata => new(
				"Metadata update failed", "Retry the affected action; if it continues, copy the technical details.", ErrorSeverity.Error, true, false, true),
			UserFacingErrorCategory.Cancelled => new(
				"Action cancelled", "Start the action again when you are ready.", ErrorSeverity.Information, true, false, false),
			_ => new(
				"Action unavailable", "Retry the action; if it continues, copy the technical details and review the application log.", ErrorSeverity.Error, true, false, false),
		};

	private sealed record ErrorDescriptor(
		string Title,
		string RecommendedAction,
		ErrorSeverity Severity,
		bool CanRetry,
		bool CanOpenSettings,
		bool CanRevealPath);
}
