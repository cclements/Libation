using DataLayer;
using AudibleUtilities;
using LibationAvalonia.ViewModels;
using LibationAvalonia.Properties;
using LibationFileManager;
using LibationUiBase.ProcessQueue;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LibationAvalonia.Features.Flight;

public enum FlightOutputProfile
{
	CurrentSettings,
	M4b,
	Mp3,
	SplitByChapter,
}

public enum FlightPreflightSeverity
{
	Warning,
	Blocking,
}

public sealed record FlightPreflightIssue(FlightPreflightSeverity Severity, string Message);

public sealed record FlightPreflightResult(
	IReadOnlyList<LibraryBook> Books,
	IReadOnlyList<FlightPreflightIssue> Issues)
{
	public bool CanProceed => Books.Count > 0 && Issues.All(issue => issue.Severity != FlightPreflightSeverity.Blocking);
}

public interface IFlightProcessAdapter
{
	Task<FlightProcessResult> ProcessAsync(IReadOnlyList<LibraryBook> books, FlightOutputProfile outputProfile);
}

public sealed record FlightProcessResult(bool Queued, string Message);

public interface IFlightActionAdapter
{
	Task<UserActionResult> ExportMetadataAsync(IReadOnlyList<LibraryBook> books);
	Task<UserActionResult> AddTagsAsync(IReadOnlyList<LibraryBook> books);
	Task<UserActionResult> ReplaceTagsAsync(IReadOnlyList<LibraryBook> books);
}

public sealed class FlightActionAdapter(MainVM main) : IFlightActionAdapter
{
	public Task<UserActionResult> ExportMetadataAsync(IReadOnlyList<LibraryBook> books)
		=> main.ExportSelectedBooksAsync(books);

	public Task<UserActionResult> AddTagsAsync(IReadOnlyList<LibraryBook> books)
		=> main.AddTagsToBooksAsync(books);

	public Task<UserActionResult> ReplaceTagsAsync(IReadOnlyList<LibraryBook> books)
		=> main.ReplaceTagsForBooksAsync(books);
}

public sealed class FlightProcessAdapter(MainVM main, Configuration configuration) : IFlightProcessAdapter
{
	public async Task<FlightProcessResult> ProcessAsync(IReadOnlyList<LibraryBook> books, FlightOutputProfile outputProfile)
	{
		ArgumentNullException.ThrowIfNull(books);
		var effective = configuration.CreateEphemeralCopy();
		switch (outputProfile)
		{
			case FlightOutputProfile.M4b:
				effective.DecryptToLossy = false;
				effective.SplitFilesByChapter = false;
				break;
			case FlightOutputProfile.Mp3:
				effective.DecryptToLossy = true;
				effective.SplitFilesByChapter = false;
				break;
			case FlightOutputProfile.SplitByChapter:
				effective.SplitFilesByChapter = true;
				effective.AllowLibationFixup = true;
				break;
		}
		bool queued = await main.QueueBooksAsync(books.ToArray(), effective);
		return queued
			? new(true, Resources.FlightQueueAccepted)
			: new(false, Resources.FlightQueueRejected);
	}
}

public static class FlightPreflight
{
	public static FlightPreflightResult Evaluate(
		IEnumerable<FlightItemViewModel> selection,
		Configuration configuration,
		ProcessQueueViewModel queue,
		FlightOutputProfile outputProfile)
	{
		ArgumentNullException.ThrowIfNull(selection);
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentNullException.ThrowIfNull(queue);

		var selectedBooks = selection.Select(item => item.LibraryBook).ToArray();
		var issues = new List<FlightPreflightIssue>();
		if (selectedBooks.Length == 0)
			issues.Add(new(FlightPreflightSeverity.Blocking, global::LibationAvalonia.Properties.Resources.FlightPreflightSelectAtLeastOneTitleBeforeProcessing));
		if (!Enum.IsDefined(outputProfile))
			issues.Add(new(FlightPreflightSeverity.Blocking, global::LibationAvalonia.Properties.Resources.FlightPreflightTheSelectedOutputProfileIsNotSupported));
		if (configuration.Books is null || string.IsNullOrWhiteSpace(configuration.Books.PathWithoutPrefix))
			issues.Add(new(FlightPreflightSeverity.Blocking, global::LibationAvalonia.Properties.Resources.FlightPreflightChooseAValidBooksLocationInSettings));

		int unavailable = selectedBooks.Count(book => book.AbsentFromLastScan);
		if (unavailable > 0)
			issues.Add(new(FlightPreflightSeverity.Blocking, string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.FlightPreflight0SelectedTitleSAreUnavailableIn, unavailable)));

		AddAuthorizationIssues(selectedBooks, issues);

		var queuedIds = queue.Queue
			.Where(item => item?.LibraryBook?.Book is not null
				&& item.Status is ProcessBookStatus.Queued or ProcessBookStatus.Working)
			.Select(item => item.LibraryBook.Book.AudibleProductId)
			.ToHashSet(StringComparer.Ordinal);
		int alreadyActive = selectedBooks.Count(book => queuedIds.Contains(book.Book.AudibleProductId));
		if (alreadyActive > 0)
			issues.Add(new(FlightPreflightSeverity.Warning, string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.FlightPreflight0SelectedTitleSAlreadyHaveActive, alreadyActive)));
		var books = selectedBooks
			.Where(book => !queuedIds.Contains(book.Book.AudibleProductId))
			.ToArray();
		if (selectedBooks.Length > 0 && books.Length == 0 && alreadyActive == selectedBooks.Length)
			issues.Add(new(FlightPreflightSeverity.Blocking, global::LibationAvalonia.Properties.Resources.FlightPreflightAllSelectedTitlesAlreadyHaveActiveProcessing));

		int alreadyComplete = books.Count(book => book.Book.AudioExists);
		if (alreadyComplete > 0)
			issues.Add(new(FlightPreflightSeverity.Warning, string.Format(Resources.FlightDuplicateOutputFormat, alreadyComplete)));

		AddDiskSpaceIssues(books.Length, configuration, issues);

		return new(books, issues);
	}

	private static void AddAuthorizationIssues(
		IReadOnlyList<LibraryBook> selectedBooks,
		ICollection<FlightPreflightIssue> issues)
	{
		if (selectedBooks.Count == 0)
			return;

		try
		{
			using var persister = AudibleApiStorage.GetAccountsSettingsPersister();
			int unauthorized = selectedBooks.Count(book =>
				persister.AccountsSettings.GetAccount(book.Account, book.Book.Locale)?.IdentityTokens?.IsValid != true);
			if (unauthorized > 0)
				issues.Add(new(
					FlightPreflightSeverity.Warning,
					string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.FlightPreflight0SelectedTitleSDoNotCurrently, unauthorized)));
		}
		catch (Exception ex)
		{
			Serilog.Log.Logger.Warning(ex, global::LibationAvalonia.Properties.Resources.FlightPreflightUnableToInspectCurrentFlightAccountAuthorization);
			issues.Add(new(
				FlightPreflightSeverity.Warning,
				global::LibationAvalonia.Properties.Resources.FlightPreflightLibationCouldNotInspectStoredAccountAuthorization));
		}
	}

	private static void AddDiskSpaceIssues(
		int bookCount,
		Configuration configuration,
		ICollection<FlightPreflightIssue> issues)
	{
		if (bookCount == 0 || configuration.Books is null)
			return;

		var drives = DiskSpaceHelper.GetBackupDriveSpaces(configuration, bookCount);
		if (DiskSpaceHelper.HasSufficientSpaceForBulkBackup(drives))
			return;

		string estimate = DiskSpaceHelper.FormatBytes((long)bookCount * DiskSpaceHelper.EstimatedBytesPerAudiobookBackup);
		if (DiskSpaceHelper.AnyDriveCriticallyLow(drives))
		{
			issues.Add(new(
				FlightPreflightSeverity.Blocking,
				string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.FlightPreflightAConfiguredBooksOrInProgressDrive, estimate, bookCount)));
		}
		else
		{
			issues.Add(new(
				FlightPreflightSeverity.Warning,
				string.Format(Resources.FlightDiskSpaceWarningFormat, estimate, bookCount)));
		}
	}
}
