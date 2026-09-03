using ApplicationServices;
using AudibleUtilities;
using Avalonia.Threading;
using DataLayer;
using LibationAvalonia.DesignSystem.Components;
using LibationAvalonia.Diagnostics;
using LibationAvalonia.Dialogs;
using LibationAvalonia.Features.Accounts;
using LibationAvalonia.Features.Downloads;
using LibationAvalonia.Features.History;
using LibationAvalonia.Features.Onboarding;
using LibationAvalonia.Features.Settings;
using LibationAvalonia.Features.Tools;
using LibationAvalonia.Features.Trash;
using LibationAvalonia.Shell;
using LibationAvalonia.Views;
using LibationFileManager;
using LibationUiBase.ProcessQueue;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LibationAvalonia.Tests;

[TestClass]
[DoNotParallelize]
public class S6SecondaryDestinationsContractTests
{
	[TestMethod]
	public async Task Downloads_ProjectsFourTruthfulSectionsAndContextualActions()
	{
		var (window, shell) = await CreateShellAsync();
		try
		{
			await HeadlessTestHost.Dispatch(() =>
			{
				var pendingBook = CreateBook("S6DOWN00001", "Pending", DateTime.Today, account: "reader@example.com");
				var activeBook = CreateBook("S6DOWN00002", "Downloading", DateTime.Today.AddDays(-1));
				var downloadedBook = CreateBook("S6DOWN00003", "Downloaded", DateTime.Today.AddDays(-2));
				var unavailableBook = CreateBook("S6DOWN00004", "Unavailable", DateTime.Today.AddDays(-3));
				var retainedLocalBook = CreateBook("S6DOWN00005", "Retained local copy", DateTime.Today.AddDays(-4));
				retainedLocalBook.AbsentFromLastScan = true;
				using var pending = new DownloadBookItemViewModel(shell.Downloads, pendingBook, LiberatedStatus.NotLiberated, null);
				using var active = new DownloadBookItemViewModel(shell.Downloads, activeBook, LiberatedStatus.NotLiberated, null);
				using var downloaded = new DownloadBookItemViewModel(shell.Downloads, downloadedBook, LiberatedStatus.Liberated, 4096);
				using var unavailable = new DownloadBookItemViewModel(shell.Downloads, unavailableBook, LiberatedStatus.Error, null);
				using var retainedLocal = new DownloadBookItemViewModel(shell.Downloads, retainedLocalBook, LiberatedStatus.Liberated, 4096);
				var queued = new TestProcessBookViewModel(activeBook, HeadlessTestHost.Configuration).AddDownloadDecryptBook();
				queued.Status = ProcessBookStatus.Queued;
				active.UpdateQueue(queued);

				Assert.AreEqual(DownloadsSectionKind.DownloadPending, pending.Section);
				Assert.AreEqual(DownloadsSectionKind.Downloading, active.Section);
				Assert.AreEqual(DownloadsSectionKind.Downloaded, downloaded.Section);
				Assert.AreEqual(DownloadsSectionKind.Unavailable, unavailable.Section);
				Assert.AreEqual(DownloadsSectionKind.Downloaded, retainedLocal.Section);
				Assert.AreEqual(LibationStatusKind.Completed, retainedLocal.Status);
				Assert.AreEqual("Download", pending.PrimaryActionText);
				Assert.IsNotNull(pending.LocateCommand);
				Assert.IsFalse(pending.MaskedAccount.Contains("reader@example.com", StringComparison.OrdinalIgnoreCase));
				Assert.AreEqual(DiskSpaceHelper.FormatBytes(4096), downloaded.SizeText);
				Assert.IsNull(unavailable.SizeText, "Unknown byte counts must stay absent instead of becoming estimates.");
			});
		}
		finally
		{
			await HeadlessTestHost.Dispatch(window.Close);
		}
	}

	[TestMethod]
	public async Task History_FiltersTypedOutcomesAndHidesCorrelationFromVisibleSearchText()
	{
		var (window, shell) = await CreateShellAsync();
		try
		{
			var correlation = Guid.NewGuid();
			await HeadlessTestHost.Dispatch(() =>
			{
				using var history = new HistoryViewModel(shell.Main);
				var book = CreateBook("S6HIST00001", "Recorded title", DateTime.Today);
				shell.Main.LibraryStats = CreateStats([book]);
				shell.Main.ProcessQueue.LogEntries.Add(new(DateTime.Now, $"[{correlation:N}] Queue detail without a public identifier"));
				AwaitOnDispatcher(history.RefreshAsync());

				var queue = history.VisibleItems.Single(item => item.CorrelationId == correlation);
				Assert.AreEqual("Queue detail without a public identifier", queue.Detail);
				Assert.IsFalse(queue.Detail.Contains(correlation.ToString("N"), StringComparison.OrdinalIgnoreCase));
				var catalogued = history.VisibleItems.Single(item => item.Action == "Catalogued");
				Assert.AreEqual(LibationStatusKind.Completed, catalogued.Status);

				history.SearchText = correlation.ToString("N");
				Assert.AreEqual(0, history.VisibleItems.Count, "Typed correlation data must not become visible search prose.");
				history.SearchText = string.Empty;
				history.SelectedAction = "Catalogued";
				Assert.AreEqual(1, history.VisibleItems.Count);
				history.FromDate = DateTimeOffset.Now.AddDays(1);
				Assert.AreEqual(0, history.VisibleItems.Count);
			});
		}
		finally
		{
			await HeadlessTestHost.Dispatch(window.Close);
		}
	}

	[TestMethod]
	public async Task Accounts_ExposeSafeSnapshotsAndDelegateCardSpecificActions()
	{
		await HeadlessTestHost.Reset();
		await HeadlessTestHost.Dispatch(() =>
		{
			const string rawLogin = "somebody@example.com";
			var generated = new Account(rawLogin) { AccountName = $"{rawLogin} - us" };
			var safeName = AccountPresentationSource.SafeDisplayName(generated);
			Assert.IsFalse(safeName.Contains(rawLogin, StringComparison.OrdinalIgnoreCase));
			var nickname = new Account(rawLogin) { AccountName = "Listening room" };
			Assert.AreEqual("Listening room", AccountPresentationSource.SafeDisplayName(nickname));

			var snapshot = new AccountPresentationSnapshot(
				"opaque-card-id",
				safeName,
				["us", "uk"],
				12,
				AccountAuthorizationState.StoredSessionNeedsRenewal,
				includedInLibraryScans: false,
				actionsAvailable: true);
			using var source = new FakeAccountSource(snapshot);
			using var viewModel = new AccountsViewModel(source);
			var card = viewModel.Accounts.Single();
			Assert.AreEqual("Excluded from automatic scans", card.ScanInclusionText);
			Assert.AreEqual("Stored session needs renewal", card.AuthorizationText);
			Assert.IsFalse(card.DisplayName.Contains(rawLogin, StringComparison.OrdinalIgnoreCase));
			viewModel.ScanAccountCommand.Execute(card);
			viewModel.EditMarketplacesCommand.Execute(card);
			viewModel.ReauthenticateCommand.Execute(card);
			viewModel.RemoveAccountCommand.Execute(card);
			Assert.AreEqual(1, source.ScanCalls);
			Assert.AreEqual(1, source.EditCalls);
			Assert.AreEqual(1, source.ReauthenticateCalls);
			Assert.AreEqual(1, source.RemoveCalls);
			StringAssert.Contains(AccountPresentationSource.RemovalConsequenceText, "local audiobook files are not deleted");
		});
	}

	[TestMethod]
	public async Task Settings_UsesFiveRealTabsAndOneAtomicAppearanceDraft()
	{
		var (window, shell) = await CreateShellAsync();
		try
		{
			await HeadlessTestHost.Dispatch(() =>
			{
				Assert.AreEqual(5, shell.Settings.VisibleCategories.Count);
				Assert.AreEqual(5, Enum.GetValues<SettingsDialogSection>().Length);
				var appearance = shell.Settings.Appearance;
				appearance.SelectTastingRoomCommand.Execute(null);
				appearance.IsHighContrast = true;
				Assert.AreEqual(ExperienceStyle.HighContrast, appearance.EffectiveStyle);
				appearance.IsHighContrast = false;
				Assert.AreEqual(ExperienceStyle.TastingRoom, appearance.EffectiveStyle);
				appearance.SelectedDensityMode = appearance.DensityModes.Single(item => item.Value == DensityMode.Compact);
				appearance.SelectedDecorationLevel = appearance.DecorationLevels.Single(item => item.Value == DecorationLevel.Reduced);
				appearance.SelectedMotionPreference = appearance.MotionPreferences.Single(item => item.Value == ReducedMotionPreference.Reduce);
				appearance.SelectedLibraryView = appearance.LibraryViews.Single(item => item.Value == LibraryViewMode.Gallery);
				appearance.SelectedNavigationRail = appearance.NavigationRails.Single(item => item.Value == NavigationRailPreference.Compact);
				appearance.UseSystemTypography = true;
				appearance.ShowDecanterDock = false;
				appearance.PersistFlightBetweenSessions = true;
				appearance.Apply();

				var saved = HeadlessTestHost.Configuration.GetContemporaryExperienceSettings();
				Assert.AreEqual(ExperienceStyle.TastingRoom, saved.ExperienceStyle);
				Assert.AreEqual(DensityMode.Compact, saved.DensityMode);
				Assert.AreEqual(DecorationLevel.Reduced, saved.DecorationLevel);
				Assert.AreEqual(ReducedMotionPreference.Reduce, saved.ReducedMotionPreference);
				Assert.IsTrue(saved.UseSystemTypography);
				Assert.AreEqual(LibraryViewMode.Gallery, saved.LibraryViewMode);
				Assert.AreEqual(NavigationRailPreference.Compact, saved.NavigationRailPreference);
				Assert.IsFalse(saved.ShowDecanterDock);
				Assert.IsTrue(saved.PersistFlightBetweenSessions);
				Assert.IsTrue(saved.UseContemporaryShell);

				appearance.ResetAndApply();
				saved = HeadlessTestHost.Configuration.GetContemporaryExperienceSettings();
				Assert.AreEqual(ExperienceStyle.FollowSystem, saved.ExperienceStyle);
				Assert.AreEqual(DensityMode.Comfortable, saved.DensityMode);
				Assert.AreEqual(DecorationLevel.Full, saved.DecorationLevel);
				Assert.IsTrue(saved.UseContemporaryShell);
			});
		}
		finally
		{
			await HeadlessTestHost.Dispatch(window.Close);
		}
	}

	[TestMethod]
	public async Task Tools_PresentsDistinctRiskKindsAndLiveStartupFilterSwitch()
	{
		var (window, shell) = await CreateShellAsync();
		try
		{
			await HeadlessTestHost.Dispatch(() =>
			{
				var risks = shell.Tools.Groups.SelectMany(group => group.Actions).Select(action => action.Risk).Distinct().ToArray();
				CollectionAssert.AreEquivalent(Enum.GetValues<ToolRiskKind>(), risks);
				var process = shell.Tools.Groups.SelectMany(group => group.Actions).Single(action => action.Name == "Process visible titles");
				Assert.AreEqual(ToolRiskKind.NeedsReview, process.Risk);
				StringAssert.Contains(process.RiskText, "Confirmation");

				var initial = shell.Main.FirstFilterIsDefault;
				shell.Tools.FirstFilterIsDefault = !initial;
				Assert.AreEqual(!initial, shell.Main.FirstFilterIsDefault);
				shell.Tools.FirstFilterIsDefault = initial;
				Assert.AreEqual(initial, shell.Main.FirstFilterIsDefault);
			});
		}
		finally
		{
			await HeadlessTestHost.Dispatch(window.Close);
		}
	}

	[TestMethod]
	public async Task Trash_AllowsOnlyDeletedNonParentRowsToEnterActions()
	{
		await HeadlessTestHost.Reset();
		await HeadlessTestHost.Dispatch(() =>
		{
			var deleted = CreateBook("S6TRASH0001", "Removed title", DateTime.Today);
			deleted.IsDeleted = true;
			var retained = CreateBook("S6TRASH0002", "Retained context", DateTime.Today.AddDays(-1));
			var parent = CreateBook("S6TRASH0003", "Series container", DateTime.Today.AddDays(-2), ContentType.Parent);
			parent.IsDeleted = true;
			var deletedRow = new TrashItemViewModel(deleted);
			var retainedRow = new TrashItemViewModel(retained);
			var parentRow = new TrashItemViewModel(parent);

			Assert.IsTrue(deletedRow.CanSelect);
			Assert.IsFalse(retainedRow.CanSelect);
			Assert.IsFalse(parentRow.CanSelect);
			parentRow.IsSelected = true;
			Assert.IsFalse(parentRow.IsSelected);
			StringAssert.Contains(parentRow.Detail, "never selected");
		});
	}

	[TestMethod]
	public async Task Onboarding_EmitsNewestThreeRequestWithoutOwningFlightSelection()
	{
		var (window, shell) = await CreateShellAsync();
		try
		{
			await HeadlessTestHost.Dispatch(() =>
			{
				using var onboarding = new OnboardingViewModel(shell.CommandAdapter, isManualReentry: true, HeadlessTestHost.Configuration);
				OnboardingExitEventArgs? exit = null;
				onboarding.ExitRequested += (_, args) => exit = args;
				for (var step = 0; step < 4; step++)
					onboarding.NextCommand.Execute(null);
				Assert.IsTrue(onboarding.IsFirstFlightStep);
				onboarding.AddNewestToFlightCommand.Execute(null);

				Assert.IsNotNull(exit?.CompletionRequest);
				Assert.AreEqual(3, exit.CompletionRequest.NewestEligibleTitleCount);
				Assert.AreEqual(AppRouteId.Library, exit.CompletionRequest.Destination);
				Assert.AreEqual(0, shell.Flight.Count, "Onboarding must request the shell owner rather than mutate Flight itself.");

				var newest = CreateBook("S6FLIGHT001", "Newest", DateTime.Today);
				var next = CreateBook("S6FLIGHT002", "Next", DateTime.Today.AddDays(-1));
				var third = CreateBook("S6FLIGHT003", "Third", DateTime.Today.AddDays(-2));
				var fourth = CreateBook("S6FLIGHT004", "Fourth", DateTime.Today.AddDays(-3));
				var deleted = CreateBook("S6FLIGHT005", "Deleted newer", DateTime.Today.AddDays(2));
				deleted.IsDeleted = true;
				var absent = CreateBook("S6FLIGHT006", "Absent newer", DateTime.Today.AddDays(1));
				absent.AbsentFromLastScan = true;
				var parent = CreateBook("S6FLIGHT007", "Parent newer", DateTime.Today.AddDays(3), ContentType.Parent);
				var selected = MainWindow.SelectNewestEligibleTitles(
					[fourth, deleted, parent, third, newest, absent, next],
					exit.CompletionRequest.NewestEligibleTitleCount);
				CollectionAssert.AreEqual(
					new[] { "S6FLIGHT001", "S6FLIGHT002", "S6FLIGHT003" },
					selected.Select(book => book.Book.AudibleProductId).ToArray());
			});
		}
		finally
		{
			await HeadlessTestHost.Dispatch(window.Close);
		}
	}

	[TestMethod]
	public void CapturePlan_ContainsExactSecondaryMatrixAndFiveInertOnboardingSteps()
	{
		var plan = CapturePlan.Load(FindRepositoryFile("Scripts/capture-plans/secondary.json"));
		var routeEntries = plan.Entries.Where(entry => entry.Surface == CaptureSurface.Route).ToArray();
		var onboarding = plan.Entries.Where(entry => entry.Surface == CaptureSurface.Onboarding).ToArray();
		Assert.AreEqual(29, plan.Entries.Count);
		Assert.AreEqual(24, routeEntries.Length);
		Assert.AreEqual(5, onboarding.Length);

		var routes = new[]
		{
			AppRouteId.Downloads, AppRouteId.History, AppRouteId.Accounts,
			AppRouteId.Settings, AppRouteId.Tools, AppRouteId.Trash,
		};
		foreach (var profile in new[] { ExperienceStyle.Cellar, ExperienceStyle.TastingRoom })
		foreach (var route in routes)
		foreach (var size in new[] { (Width: 1456, Height: 1060), (Width: 960, Height: 720) })
			Assert.AreEqual(1, routeEntries.Count(entry => entry.Profile == profile && entry.Route == route
				&& entry.Width == size.Width && entry.Height == size.Height));

		CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, onboarding.Select(entry => entry.OnboardingStep).ToArray());
		Assert.IsTrue(onboarding.Single(entry => entry.OnboardingStep == 4).OnboardingScanActive);
		Assert.IsTrue(onboarding.Where(entry => entry.OnboardingStep != 4).All(entry => !entry.OnboardingScanActive));
		Assert.AreEqual("cellar-onboarding-step4-1456x1060.png", onboarding.Single(entry => entry.OnboardingStep == 4).FileName);
	}

	private static async Task<(MainWindow Window, AppShellViewModel Shell)> CreateShellAsync()
	{
		await HeadlessTestHost.Reset(ExperienceStyle.Cellar);
		MainWindow? window = null;
		AppShellViewModel? shell = null;
		await HeadlessTestHost.Dispatch(() =>
		{
			window = new MainWindow(HeadlessTestHost.ExperienceManager, null);
			window.Show();
			shell = (window.Content as AppShellView)?.DataContext as AppShellViewModel;
			Assert.IsNotNull(shell);
		});
		return (window!, shell!);
	}

	private static LibraryCommands.LibraryStats CreateStats(LibraryBook[] books)
		=> new(
			booksFullyBackedUp: 0,
			booksDownloadedOnly: 0,
			booksNoProgress: books.Length,
			booksError: 0,
			booksUnavailable: 0,
			pdfsDownloaded: 0,
			pdfsNotDownloaded: 0,
			pdfsUnavailable: 0,
			LibraryBooks: books);

	private static LibraryBook CreateBook(
		string productId,
		string title,
		DateTime dateAdded,
		ContentType contentType = ContentType.Product,
		string account = "test-account")
	{
		var book = new Book(
			new AudibleProductId(productId),
			title,
			string.Empty,
			"S6 contract fixture",
			120,
			contentType,
			[new Contributor("Author", "AUTHOR0001")],
			[new Contributor("Narrator", "NARRATOR01")],
			"us");
		return new(book, dateAdded, account);
	}

	private static string FindRepositoryFile(string relativePath)
	{
		for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
		{
			var candidate = Path.Combine(directory.FullName, relativePath);
			if (File.Exists(candidate))
				return candidate;
		}
		throw new FileNotFoundException($"Could not locate repository file '{relativePath}' from the test output.");
	}

	private static void AwaitOnDispatcher(Task task)
	{
		if (task.IsCompleted)
		{
			task.GetAwaiter().GetResult();
			return;
		}

		var frame = new DispatcherFrame();
		_ = task.ContinueWith(
			_ => Dispatcher.UIThread.Post(() => frame.Continue = false),
			default,
			TaskContinuationOptions.ExecuteSynchronously,
			TaskScheduler.Default);
		Dispatcher.UIThread.PushFrame(frame);
		task.GetAwaiter().GetResult();
	}

	private sealed class TestProcessBookViewModel(LibraryBook book, Configuration configuration)
		: ProcessBookViewModel(book, configuration);

	private sealed class FakeAccountSource(AccountPresentationSnapshot snapshot) : IAccountPresentationSource
	{
		public event EventHandler? Changed { add { } remove { } }
		public bool IsScanning => false;
		public string ScanStateText => "No account scan is running.";
		public int ScanCalls { get; private set; }
		public int EditCalls { get; private set; }
		public int ReauthenticateCalls { get; private set; }
		public int RemoveCalls { get; private set; }
		public IReadOnlyList<AccountPresentationSnapshot> GetAccounts() => [snapshot];
		public Task AddAccountAsync() => Task.CompletedTask;
		public Task ManageAccountsAsync() => Task.CompletedTask;
		public Task ScanNowAsync(AccountPresentationSnapshot account) { ScanCalls++; return Task.CompletedTask; }
		public Task EditMarketplacesAsync(AccountPresentationSnapshot account) { EditCalls++; return Task.CompletedTask; }
		public Task ReauthenticateAsync(AccountPresentationSnapshot account) { ReauthenticateCalls++; return Task.CompletedTask; }
		public Task RemoveAsync(AccountPresentationSnapshot account) { RemoveCalls++; return Task.CompletedTask; }
		public void Dispose() { }
	}
}
