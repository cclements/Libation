using ApplicationServices;
using Avalonia;
using Avalonia.Controls;
using DataLayer;
using LibationAvalonia.DesignSystem;
using LibationAvalonia.DesignSystem.Components;
using LibationAvalonia.Features.Flight;
using LibationAvalonia.Features.Overview;
using LibationAvalonia.Shell;
using LibationAvalonia.ViewModels;
using LibationAvalonia.Views;
using LibationFileManager;
using LibationUiBase.ProcessQueue;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LibationAvalonia.Tests;

[TestClass]
[DoNotParallelize]
public class DashboardOverviewContractTests
{
	[TestMethod]
	public async Task ProfileSwitch_ReusesSharedDashboardLibraryFlightAndProcessingOwners()
	{
		await HeadlessTestHost.Reset(ExperienceStyle.Cellar);
		await HeadlessTestHost.Dispatch(() =>
			HeadlessTestHost.Configuration.ContemporaryLastRoute = nameof(AppRouteId.Overview));
		MainWindow? window = null;
		AppShellView? shell = null;
		AppShellViewModel? viewModel = null;
		DashboardViewModel? dashboard = null;
		object? library = null;
		object? flight = null;
		object? currentFlight = null;
		object? processing = null;

		try
		{
			await HeadlessTestHost.Dispatch(() =>
			{
				window = new MainWindow(HeadlessTestHost.ExperienceManager, null);
				window.Show();
				shell = window.Content as AppShellView;
				Assert.IsNotNull(shell);
				viewModel = shell.DataContext as AppShellViewModel;
				Assert.IsNotNull(viewModel);
				dashboard = viewModel.Dashboard;
				library = viewModel.Library;
				flight = viewModel.Flight;
				currentFlight = viewModel.CurrentFlight;
				processing = viewModel.Processing;
			});

			await HeadlessTestHost.Dispatch(() =>
				HeadlessTestHost.Configuration.ExperienceStyle = ExperienceStyle.TastingRoom);
			await HeadlessTestHost.Dispatch(() =>
			{
				Assert.IsNotNull(window);
				Assert.IsNotNull(shell);
				Assert.IsNotNull(viewModel);
				Assert.AreSame(shell, window.Content);
				Assert.AreSame(viewModel, shell.DataContext);
				Assert.AreSame(dashboard, viewModel.Dashboard);
				Assert.AreSame(library, viewModel.Library);
				Assert.AreSame(flight, viewModel.Flight);
				Assert.AreSame(currentFlight, viewModel.CurrentFlight);
				Assert.AreSame(processing, viewModel.Processing);
				Assert.AreEqual(DashboardLayoutKind.TastingRoom, viewModel.Profile.DashboardLayout);
				Assert.AreEqual("Today’s Selection", viewModel.Dashboard.RouteTitle);
			});
		}
		finally
		{
			if (window is not null)
				await HeadlessTestHost.Dispatch(window.Close);
		}
	}

	[TestMethod]
	public async Task ResponsiveOverviewHosts_ReparentSharedSurfacesWithoutReplacingThem()
	{
		await HeadlessTestHost.Reset(ExperienceStyle.Cellar);
		await HeadlessTestHost.Dispatch(() =>
			HeadlessTestHost.Configuration.ContemporaryLastRoute = nameof(AppRouteId.Overview));
		MainWindow? window = null;
		AppShellView? shell = null;
		AppShellViewModel? viewModel = null;
		CurrentFlightView? flightSurface = null;
		DecanterSummary? decanterSurface = null;

		try
		{
			await HeadlessTestHost.Dispatch(() =>
			{
				window = new MainWindow(HeadlessTestHost.ExperienceManager, null);
				window.Show();
				shell = window.Content as AppShellView;
				Assert.IsNotNull(shell);
				viewModel = shell.DataContext as AppShellViewModel;
				Assert.IsNotNull(viewModel);
				flightSurface = shell.FindControl<CurrentFlightView>("SharedFlightSurface");
				decanterSurface = shell.FindControl<DecanterSummary>("SharedDecanterSurface");
				Assert.IsNotNull(flightSurface);
				Assert.IsNotNull(decanterSurface);
				Assert.AreSame(viewModel.CurrentFlight, flightSurface.DataContext);

				viewModel.UpdateLayout(new Size(1456, 1060));
				Assert.AreEqual("FlightSurfaceHost", (flightSurface.Parent as Control)?.Name);
				Assert.AreEqual("QueueDock", (decanterSurface.Parent as Control)?.Name);

				viewModel.UpdateLayout(new Size(960, 720));
				Assert.AreEqual("FlightParkingHost", (flightSurface.Parent as Control)?.Name);
				Assert.AreEqual("DecanterParkingHost", (decanterSurface.Parent as Control)?.Name);
			});

			await HeadlessTestHost.Dispatch(() =>
				HeadlessTestHost.Configuration.ExperienceStyle = ExperienceStyle.TastingRoom);
			await HeadlessTestHost.Dispatch(() =>
			{
				Assert.IsNotNull(shell);
				Assert.IsNotNull(viewModel);
				Assert.IsNotNull(flightSurface);
				Assert.IsNotNull(decanterSurface);
				Assert.AreSame(flightSurface, shell.FindControl<CurrentFlightView>("SharedFlightSurface"));
				Assert.AreSame(decanterSurface, shell.FindControl<DecanterSummary>("SharedDecanterSurface"));
				Assert.AreSame(viewModel.CurrentFlight, flightSurface.DataContext);
				Assert.AreEqual("FlightParkingHost", (flightSurface.Parent as Control)?.Name);
				Assert.AreEqual("DecanterSurfaceHost", (decanterSurface.Parent as Control)?.Name);
			});
		}
		finally
		{
			if (window is not null)
				await HeadlessTestHost.Dispatch(window.Close);
		}
	}

	[TestMethod]
	public async Task RecentRows_AreBoundedToTenAndCarryProductIds()
	{
		await HeadlessTestHost.Reset(ExperienceStyle.TastingRoom);
		await HeadlessTestHost.Dispatch(() =>
			HeadlessTestHost.Configuration.ContemporaryLastRoute = nameof(AppRouteId.History));
		MainWindow? window = null;
		AppShellViewModel? viewModel = null;
		var books = Enumerable.Range(1, 12)
			.Select(index => CreateBook($"B{index:000000000}", $"Recent {index}", DateTime.Today.AddDays(-index)))
			.ToArray();

		try
		{
			await HeadlessTestHost.Dispatch(() =>
			{
				window = new MainWindow(HeadlessTestHost.ExperienceManager, null);
				window.Show();
				var shell = window.Content as AppShellView;
				Assert.IsNotNull(shell);
				viewModel = shell.DataContext as AppShellViewModel;
				Assert.IsNotNull(viewModel);
				viewModel.Main.LibraryStats = CreateStats(books);
			});

			Assert.IsNotNull(viewModel);
			using var source = new MainDashboardDataSource(viewModel.Main, viewModel.Flight, viewModel.Processing);
			var snapshot = await source.LoadAsync(CancellationToken.None);
			Assert.AreEqual(10, snapshot.RecentAdditions.Count);
			Assert.AreEqual(10, snapshot.RecentCompletions.Count);
			Assert.IsTrue(snapshot.RecentAdditions.All(item => !string.IsNullOrWhiteSpace(item.ProductId)));
			Assert.IsTrue(snapshot.RecentCompletions.All(item => !string.IsNullOrWhiteSpace(item.ProductId)));
			CollectionAssert.AreEqual(
				books.Take(10).Select(book => book.Book.AudibleProductId.ToString()).ToArray(),
				snapshot.RecentAdditions.Select(item => item.ProductId).ToArray());
		}
		finally
		{
			if (window is not null)
				await HeadlessTestHost.Dispatch(window.Close);
		}
	}

	[TestMethod]
	public async Task FlightQueueJoin_UsesProductIdInsteadOfDisplayTitle()
	{
		await HeadlessTestHost.Reset(ExperienceStyle.TastingRoom);
		await HeadlessTestHost.Dispatch(() =>
			HeadlessTestHost.Configuration.ContemporaryLastRoute = nameof(AppRouteId.History));
		MainWindow? window = null;
		AppShellViewModel? viewModel = null;
		var unqueued = CreateBook("B000000001", "Shared title", DateTime.Today.AddDays(-1));
		var queued = CreateBook("B000000002", "Shared title", DateTime.Today);

		try
		{
			await HeadlessTestHost.Dispatch(() =>
			{
				window = new MainWindow(HeadlessTestHost.ExperienceManager, null);
				window.Show();
				var shell = window.Content as AppShellView;
				Assert.IsNotNull(shell);
				viewModel = shell.DataContext as AppShellViewModel;
				Assert.IsNotNull(viewModel);
				viewModel.Main.LibraryStats = CreateStats([unqueued, queued]);
				viewModel.Flight.AddRange([unqueued, queued]);
				var queueItem = new TestProcessBookViewModel(queued, HeadlessTestHost.Configuration)
					.AddDownloadDecryptBook();
				queueItem.Status = ProcessBookStatus.Working;
				queueItem.StatusOverride = "Downloading the matching product";
				viewModel.Processing.Source.Queue.Enqueue([queueItem]);
			});

			Assert.IsNotNull(viewModel);
			using var source = new MainDashboardDataSource(viewModel.Main, viewModel.Flight, viewModel.Processing);
			var snapshot = await source.LoadAsync(CancellationToken.None);
			var unqueuedResult = snapshot.CurrentFlight.Single(item => item.ProductId == "B000000001");
			var queuedResult = snapshot.CurrentFlight.Single(item => item.ProductId == "B000000002");
			Assert.IsNull(unqueuedResult.ProcessingStatusText);
			Assert.AreEqual("Downloading the matching product", queuedResult.ProcessingStatusText);
			Assert.IsTrue(queuedResult.ShowProcessingProgress);
		}
		finally
		{
			if (window is not null)
				await HeadlessTestHost.Dispatch(window.Close);
		}
	}

	private static LibraryCommands.LibraryStats CreateStats(LibraryBook[] books)
		=> new(
			booksFullyBackedUp: books.Length,
			booksDownloadedOnly: 0,
			booksNoProgress: 0,
			booksError: 0,
			booksUnavailable: 0,
			pdfsDownloaded: 0,
			pdfsNotDownloaded: 0,
			pdfsUnavailable: 0,
			LibraryBooks: books);

	private static LibraryBook CreateBook(string productId, string title, DateTime dateAdded)
	{
		var book = new Book(
			new AudibleProductId(productId),
			title,
			string.Empty,
			"Dashboard contract fixture",
			120,
			ContentType.Product,
			[new Contributor("Author", "AUTHOR0001")],
			[new Contributor("Narrator", "NARRATOR01")],
			"us");
		book.UserDefinedItem.SetLastDownloaded(new Version(14, 0, 1), AudioFormat.Default, "1");
		return new(book, dateAdded, "test-account");
	}

	private sealed class TestProcessBookViewModel(LibraryBook book, Configuration configuration)
		: ProcessBookViewModel(book, configuration);
}
