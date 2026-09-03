using ApplicationServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using DataLayer;
using LibationAvalonia.DesignSystem;
using LibationAvalonia.DesignSystem.Components;
using LibationAvalonia.Features.Flight;
using LibationAvalonia.Features.Library;
using LibationAvalonia.Features.Onboarding;
using LibationAvalonia.Features.Overview;
using LibationAvalonia.Shell;
using LibationAvalonia.ViewModels;
using LibationAvalonia.Views;
using LibationFileManager;
using LibationSearchEngine;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections;
using System.Collections.Specialized;
using System.Reflection;
using System.Runtime.InteropServices;

namespace LibationAvalonia.Tests;

[TestClass]
[DoNotParallelize]
public class S8TestsAndEvidenceContractTests
{
	private const int BaselineWidth = 960;
	private const int BaselineHeight = 720;
	private static readonly string[] DashboardErrorPropertyNames =
	[
		nameof(DashboardViewModel.ErrorMessage),
		nameof(DashboardViewModel.CurrentError),
		nameof(DashboardViewModel.CanCopyTechnicalDetails),
		nameof(DashboardViewModel.HasError),
		nameof(DashboardViewModel.IsLoading),
		nameof(DashboardViewModel.ShowInitialError),
	];

	[TestMethod]
	public async Task HeadlessCapture_MatchesCommittedCellarAndTastingRoomBaselines()
	{
		await HeadlessTestHost.Reset(ExperienceStyle.Cellar);
		var updateBaselines = string.Equals(
			Environment.GetEnvironmentVariable("LIBATION_UPDATE_HEADLESS_BASELINES"),
			"1",
			StringComparison.Ordinal);
		var baselineDirectory = Path.Combine(
			FindRepositoryRoot(),
			"Source", "_Tests", "LibationAvalonia.Tests", "Baselines", "S8");

		await HeadlessTestHost.Dispatch(() =>
		{
			var experienceProperty = typeof(App).GetProperty(
				nameof(App.ExperienceManager),
				BindingFlags.Public | BindingFlags.Static);
			Assert.IsNotNull(experienceProperty);
			experienceProperty.SetValue(null, HeadlessTestHost.ExperienceManager);

			foreach (var (style, fileName) in new[]
			{
				(ExperienceStyle.Cellar, "cellar-component-gallery-960x720.png"),
				(ExperienceStyle.TastingRoom, "tasting-room-component-gallery-960x720.png"),
			})
			{
				var gallery = new ComponentGallery
				{
					Width = BaselineWidth,
					Height = BaselineHeight,
					PreviewStyle = style,
					PreviewDensity = DensityMode.Comfortable,
					PreviewDecoration = DecorationLevel.Full,
					PreviewMotion = ReducedMotionPreference.Reduce,
					UseSystemTypography = false,
				};
				var window = new Window
				{
					Width = BaselineWidth,
					Height = BaselineHeight,
					ShowActivated = false,
					Content = gallery,
				};
				try
				{
					window.Show();
					window.UpdateLayout();
					var previewScope = gallery.FindControl<ThemeVariantScope>("PreviewScope");
					Assert.IsNotNull(previewScope);
					var profileRegion = ToPixelRect(previewScope.Bounds);

					using var frame = new RenderTargetBitmap(
						new PixelSize(BaselineWidth, BaselineHeight),
						new Vector(96, 96));
					frame.Render(gallery);
					var actualPath = Path.Combine(HeadlessTestHost.RootDirectory, fileName);
					frame.Save(actualPath);
					var actual = File.ReadAllBytes(actualPath);
					Assert.IsGreaterThan(0, actual.Length, $"The {style} headless capture was empty.");
					var baselinePath = Path.Combine(baselineDirectory, fileName);

					if (updateBaselines)
					{
						Directory.CreateDirectory(baselineDirectory);
						File.WriteAllBytes(baselinePath, actual);
						continue;
					}

					Assert.IsTrue(File.Exists(baselinePath), $"Missing committed S8 baseline: {baselinePath}");
					var expectedPixels = ReadPixels(baselinePath, out var expectedSize, out var expectedRowBytes);
					var actualPixels = ReadPixels(actualPath, out var actualSize, out var actualRowBytes);
					Assert.AreEqual(expectedSize, actualSize);
					CollectionAssert.AreEqual(
						ReadPixelRegion(expectedPixels, expectedRowBytes, profileRegion),
						ReadPixelRegion(actualPixels, actualRowBytes, profileRegion),
						$"The {style} 960x720 headless profile preview changed from its committed baseline.");
				}
				finally
				{
					window.Close();
				}
			}
		});
	}

	[TestMethod]
	public async Task DashboardRefresh_RaisesOnlyItsBoundedNotificationContract()
	{
		var (window, shell) = await CreateShellAsync(AppRouteId.History);
		try
		{
			await HeadlessTestHost.Dispatch(() => AwaitOnDispatcher(shell.Dashboard.RefreshAsync()));

			var notifications = new List<string>();
			await HeadlessTestHost.Dispatch(() =>
			{
				shell.Dashboard.PropertyChanged += (_, args) =>
				{
					if (args.PropertyName is { } propertyName)
						notifications.Add(propertyName);
				};
				AwaitOnDispatcher(shell.Dashboard.RefreshAsync());
			});

			var field = typeof(DashboardViewModel).GetField(
				"SnapshotPropertyNames",
				BindingFlags.NonPublic | BindingFlags.Static);
			Assert.IsNotNull(field, "DashboardViewModel must retain one explicit snapshot notification contract.");
			var snapshotPropertyNames = field.GetValue(null) as string[];
			Assert.IsNotNull(snapshotPropertyNames);
			var expected = snapshotPropertyNames
				.Concat(DashboardErrorPropertyNames)
				.Concat([nameof(DashboardViewModel.IsRefreshing), nameof(DashboardViewModel.IsRefreshing)])
				.Order(StringComparer.Ordinal)
				.ToArray();
			CollectionAssert.AreEqual(expected, notifications.Order(StringComparer.Ordinal).ToArray());
		}
		finally
		{
			await HeadlessTestHost.Dispatch(window.Close);
		}
	}

	[TestMethod]
	public async Task FlightReplace_UsesOneLinearResetAndRetainsStableRows()
	{
		await HeadlessTestHost.Reset();
		var books = Enumerable.Range(0, 50_000)
			.Select(index => CreateBook(index, $"Flight {index}"))
			.ToArray();

		await HeadlessTestHost.Dispatch(() =>
		{
			using var flight = new FlightService(HeadlessTestHost.Configuration);
			Assert.AreEqual(books.Length, flight.AddRange(books));
			var originalItems = flight.Items.ToDictionary(item => item.Id);
			var requested = new CountingCollection<FlightItemId>(
				flight.Items.Reverse().Select(item => item.Id).ToArray());
			var collectionChanges = new List<NotifyCollectionChangedAction>();
			((INotifyCollectionChanged)flight.Items).CollectionChanged += (_, args) => collectionChanges.Add(args.Action);

			Assert.IsTrue(flight.Replace(requested));
			Assert.AreEqual(1, requested.EnumerationCount, "Replace must make one pass over the requested identifiers.");
			CollectionAssert.AreEqual(
				new[] { NotifyCollectionChangedAction.Reset },
				collectionChanges.ToArray(),
				"A replacement gesture must publish one projection reset instead of one move per title.");
			CollectionAssert.AreEqual(requested.Items.ToArray(), flight.Items.Select(item => item.Id).ToArray());
			foreach (var item in flight.Items)
				Assert.AreSame(originalItems[item.Id], item, $"Flight row identity changed for {item.Id}.");
		});
	}

	[TestMethod]
	public async Task Filtering_RunsOffDispatcherAndObservesCancellation()
	{
		await HeadlessTestHost.Reset();
		var products = new ProductsDisplayViewModel();
		var books = Enumerable.Range(0, 2).Select(index => CreateBook(index, $"Filter {index}")).ToList();
		using var cancellation = new CancellationTokenSource();
		BlockingSearchEngine? search = null;
		Task? filtering = null;

		await HeadlessTestHost.Dispatch(() =>
		{
			AwaitOnDispatcher(products.BindToGridAsync(books));
			var dispatcherThreadId = Environment.CurrentManagedThreadId;
			search = new BlockingSearchEngine(dispatcherThreadId);
			products.SearchEngine = search;
			filtering = products.Filter("blocked", cancellation.Token);
		});

		Assert.IsNotNull(search);
		Assert.IsNotNull(filtering);
		try
		{
			var searchThreadId = await search.Started.Task;
			Assert.AreNotEqual(
				search.DispatcherThreadId,
				searchThreadId,
				"The search engine ran on the Avalonia dispatcher instead of the cancellable worker path.");
			cancellation.Cancel();
			search.Release();

			Exception? observed = null;
			await HeadlessTestHost.Dispatch(() =>
			{
				try
				{
					AwaitOnDispatcher(filtering);
				}
				catch (Exception exception)
				{
					observed = exception;
				}
			});
			Assert.IsInstanceOfType<OperationCanceledException>(observed);
		}
		finally
		{
			search.Release();
			search.Dispose();
		}
	}

	[TestMethod]
	public async Task GalleryProjection_ReusesRowsAndBookWrappers()
	{
		var (window, shell) = await CreateShellAsync(AppRouteId.Library);
		var initialBooks = Enumerable.Range(0, 12)
			.Select(index => CreateBook(index, $"Original {index}"))
			.ToList();
		try
		{
			GalleryRowViewModel[] originalRows = [];
			Dictionary<FlightItemId, LibraryBookItemViewModel> originalItems = [];
			await HeadlessTestHost.Dispatch(() =>
			{
				AwaitOnDispatcher(shell.Main.ProductsDisplay.BindToGridAsync(initialBooks));
				shell.Library.UpdateGalleryViewport(new Size(640, BaselineHeight), 1);
				originalRows = shell.Library.GalleryRows.ToArray();
				originalItems = shell.Library.VisibleItems.ToDictionary(item => item.Id);
				Assert.AreEqual(4, originalRows.Length);
			});

			var refreshedBooks = Enumerable.Range(0, 12)
				.Select(index => CreateBook(index, $"Refreshed {index}"))
				.ToList();
			await HeadlessTestHost.Dispatch(() =>
			{
				AwaitOnDispatcher(shell.Main.ProductsDisplay.UpdateGridAsync(refreshedBooks));
				Assert.AreEqual(originalRows.Length, shell.Library.GalleryRows.Count);
				for (var index = 0; index < originalRows.Length; index++)
					Assert.AreSame(originalRows[index], shell.Library.GalleryRows[index]);
				foreach (var item in shell.Library.VisibleItems)
				{
					Assert.AreSame(originalItems[item.Id], item, $"Gallery item identity changed for {item.Id}.");
					StringAssert.StartsWith(item.Title, "Refreshed ");
				}
			});
		}
		finally
		{
			await HeadlessTestHost.Dispatch(window.Close);
		}
	}

	[TestMethod]
	public async Task ClassicMode_HasNoContemporaryBindingsAndFirstRunCanRemainClassic()
	{
		await HeadlessTestHost.Reset(useContemporaryShell: false);
		MainWindow? window = null;
		Control? classicContent = null;
		await HeadlessTestHost.Dispatch(() =>
		{
			window = new MainWindow(HeadlessTestHost.ExperienceManager, null);
			window.Show();
			classicContent = window.Content as Control;
			Assert.IsNotNull(classicContent);
			Assert.IsFalse(classicContent is AppShellView);
			AssertNoContemporaryRouteBindings(window);

			HeadlessTestHost.Configuration.FirstLaunch = true;
			window.ShowOnboarding(isManualReentry: false);
			var onboarding = window.Content as OnboardingView;
			Assert.IsNotNull(onboarding);
			var viewModel = onboarding.DataContext as OnboardingViewModel;
			Assert.IsNotNull(viewModel);
			viewModel.SelectCurrentInterfaceCommand.Execute(null);
			for (var step = 0; step < 5; step++)
				viewModel.NextCommand.Execute(null);

			Assert.IsFalse(HeadlessTestHost.Configuration.UseContemporaryShell);
			Assert.IsFalse(HeadlessTestHost.Configuration.FirstLaunch);
			Assert.AreSame(onboarding, window.Content, "The onboarding surface changed before its queued shell commit.");
		});

		await HeadlessTestHost.Dispatch(() =>
		{
			Assert.IsNotNull(window);
			Assert.AreSame(classicContent, window.Content);
			Assert.IsFalse(window.Content is AppShellView);
			AssertNoContemporaryRouteBindings(window);
			window.Close();
		});
	}

	private static void AssertNoContemporaryRouteBindings(MainWindow window)
	{
		var routeKeys = new[] { Key.D1, Key.D2, Key.D3, Key.D4, Key.D5, Key.D6, Key.D7, Key.D8 };
		var routeModifiers = global::LibationAvalonia.KeyGestureHelper.CommandModifier | KeyModifiers.Alt;
		Assert.IsFalse(window.KeyBindings.Any(binding =>
			binding.Gesture is { } gesture
			&& routeKeys.Contains(gesture.Key)
			&& gesture.KeyModifiers == routeModifiers));
		Assert.IsFalse(window.KeyBindings.Any(binding =>
			binding.Gesture is { Key: Key.F6, KeyModifiers: KeyModifiers.None }));
	}

	private static async Task<(MainWindow Window, AppShellViewModel Shell)> CreateShellAsync(AppRouteId initialRoute)
	{
		await HeadlessTestHost.Reset(ExperienceStyle.Cellar);
		MainWindow? window = null;
		AppShellViewModel? shell = null;
		await HeadlessTestHost.Dispatch(() =>
		{
			HeadlessTestHost.Configuration.ContemporaryLastRoute = initialRoute.ToString();
			window = new MainWindow(HeadlessTestHost.ExperienceManager, null);
			window.Show();
			shell = (window.Content as AppShellView)?.DataContext as AppShellViewModel;
			Assert.IsNotNull(shell);
		});
		return (window!, shell!);
	}

	private static LibraryBook CreateBook(int index, string title)
	{
		var book = new Book(
			new AudibleProductId($"S8{index:D8}"),
			title,
			string.Empty,
			"S8 contract fixture",
			120,
			ContentType.Product,
			[new Contributor("Author", "AUTHOR0001")],
			[new Contributor("Narrator", "NARRATOR01")],
			"us");
		return new(book, new DateTime(2026, 9, 3).AddMinutes(-index), "test-account");
	}

	private static string FindRepositoryRoot()
	{
		for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
		{
			var project = Path.Combine(directory.FullName, "Source", "LibationAvalonia", "LibationAvalonia.csproj");
			if (File.Exists(project))
				return directory.FullName;
		}
		throw new DirectoryNotFoundException("Could not locate the Libation repository from the test output.");
	}

	private static PixelRect ToPixelRect(Rect bounds)
	{
		var x = Math.Clamp((int)Math.Floor(bounds.X), 0, BaselineWidth);
		var y = Math.Clamp((int)Math.Floor(bounds.Y), 0, BaselineHeight);
		var right = Math.Clamp((int)Math.Ceiling(bounds.Right), x, BaselineWidth);
		var bottom = Math.Clamp((int)Math.Ceiling(bounds.Bottom), y, BaselineHeight);
		return new PixelRect(x, y, right - x, bottom - y);
	}

	private static byte[] ReadPixels(string path, out PixelSize size, out int rowBytes)
	{
		using var bitmap = new Bitmap(path);
		size = bitmap.PixelSize;
		using var normalized = new WriteableBitmap(
			size,
			new Vector(96, 96),
			PixelFormat.Bgra8888,
			AlphaFormat.Premul);
		using var framebuffer = normalized.Lock();
		bitmap.CopyPixels(framebuffer);
		rowBytes = framebuffer.RowBytes;
		var pixels = new byte[rowBytes * framebuffer.Size.Height];
		Marshal.Copy(framebuffer.Address, pixels, 0, pixels.Length);
		return pixels;
	}

	private static byte[] ReadPixelRegion(byte[] pixels, int rowBytes, PixelRect region)
	{
		const int bytesPerPixel = 4;
		var regionRowBytes = region.Width * bytesPerPixel;
		var result = new byte[regionRowBytes * region.Height];
		for (var row = 0; row < region.Height; row++)
			Buffer.BlockCopy(
				pixels,
				(region.Y + row) * rowBytes + region.X * bytesPerPixel,
				result,
				row * regionRowBytes,
				regionRowBytes);
		return result;
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

	private sealed class CountingCollection<T>(IReadOnlyList<T> items) : IReadOnlyCollection<T>
	{
		public IReadOnlyList<T> Items { get; } = items;
		public int Count => Items.Count;
		public int EnumerationCount { get; private set; }

		public IEnumerator<T> GetEnumerator()
		{
			EnumerationCount++;
			return Items.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}

	private sealed class BlockingSearchEngine(int dispatcherThreadId) : ISearchEngine, IDisposable
	{
		private readonly ManualResetEventSlim release = new();
		public int DispatcherThreadId { get; } = dispatcherThreadId;
		public TaskCompletionSource<int> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

		public SearchResultSet? GetSearchResultSet(string? searchString)
		{
			var currentThreadId = Environment.CurrentManagedThreadId;
			Started.TrySetResult(currentThreadId);
			if (currentThreadId != DispatcherThreadId)
				release.Wait();
			return null;
		}

		public void Release() => release.Set();
		public void Dispose() => release.Dispose();
	}
}
