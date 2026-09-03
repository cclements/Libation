using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DataLayer;
using LibationAvalonia.Diagnostics;
using LibationAvalonia.DesignSystem.Components;
using LibationAvalonia.Features.Library;
using LibationAvalonia.Features.Onboarding;
using LibationAvalonia.Shell;
using LibationFileManager;
using LibationUiBase.ProcessQueue;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibationAvalonia.Views;

/// <summary>
/// Inert capture mode for visual verification. Active only when LIBATION_CAPTURE_PLAN is set;
/// walks the plan inside the real shell, renders the window, and exits. See Scripts/capture-ui.sh.
/// </summary>
public partial class MainWindow
{
	private CapturePlan? capturePlanPreparedBeforeShow;
	private Exception? capturePlanPreparationFailure;

	/// <summary>
	/// Gives the native macOS window its first planned size before it is shown. This
	/// prevents the compositor from stretching the restored compact backing surface
	/// across a Wide capture window.
	/// </summary>
	internal void PrepareCaptureWindowBeforeShow()
	{
		if (!CaptureEnvironment.IsRequested)
			return;

		try
		{
			capturePlanPreparedBeforeShow = CapturePlan.Load(CaptureEnvironment.PlanPath);
			PrepareInitialCaptureSize(capturePlanPreparedBeforeShow.Entries[0]);
		}
		catch (Exception ex)
		{
			// RunCapturePlanIfRequestedAsync records the original failure after the
			// window opens, preserving the capture driver's existing error contract.
			capturePlanPreparationFailure = ex;
		}
	}

	private async Task RunCapturePlanIfRequestedAsync()
	{
		if (!CaptureEnvironment.IsRequested)
			return;

		var log = new StringBuilder();
		var exitCode = 0;
		try
		{
			if (capturePlanPreparationFailure is not null)
				throw capturePlanPreparationFailure;
			var plan = capturePlanPreparedBeforeShow ?? CapturePlan.Load(CaptureEnvironment.PlanPath);
			var outDir = Directory.CreateDirectory(CaptureEnvironment.OutputDirectory).FullName;
			HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
			VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
			var osHandshake = CaptureEnvironment.OsHandshakeDirectory;
			if (osHandshake is not null)
				Directory.CreateDirectory(osHandshake);
			await WaitForLibraryReadyAsync();
			var routeContent = Content as AppShellView
				?? throw new InvalidOperationException("The contemporary shell was not available for capture.");
			var main = ViewModel
				?? throw new InvalidOperationException("The main view model was not available for capture.");
			Content = null;
			await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
			var galleryContent = new ComponentGallery { IsVisible = false };
			using var onboardingViewModel = new OnboardingViewModel(
				new LibationCommandAdapter(main),
				isManualReentry: true,
				Configuration.Instance);
			var onboardingContent = new OnboardingView
			{
				DataContext = onboardingViewModel,
				IsVisible = false,
			};
			var captureHost = new Grid();
			captureHost.Children.Add(routeContent);
			captureHost.Children.Add(galleryContent);
			captureHost.Children.Add(onboardingContent);
			Content = captureHost;
			await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);

			for (var index = 0; index < plan.Entries.Count; index++)
			{
				try
				{
					var entry = plan.Entries[index];
					SizeCaptureHost(captureHost, entry);
					SizeCaptureSurface(routeContent, entry);
					SizeCaptureSurface(galleryContent, entry);
					SizeCaptureSurface(onboardingContent, entry);
					var baseline = Configuration.Instance.GetContemporaryExperienceSettings();
					Configuration.Instance.SaveContemporaryExperienceSettings(baseline with
					{
						ExperienceStyle = entry.Profile,
						DensityMode = entry.Density,
						DecorationLevel = entry.Decoration,
						ReducedMotionPreference = entry.Motion,
						LibraryViewMode = entry.LibraryView ?? baseline.LibraryViewMode,
						UseContemporaryShell = true,
					});
					await WaitForCaptureExperienceAsync(entry);
					await SettleAsync(plan.SettleMs / 2);

					if (entry.Surface == CaptureSurface.ComponentGallery)
					{
						routeContent.IsVisible = false;
						onboardingContent.IsVisible = false;
						galleryContent.PreviewStyle = entry.Profile;
						galleryContent.PreviewDensity = entry.Density;
						galleryContent.PreviewDecoration = entry.Decoration;
						galleryContent.PreviewMotion = entry.Motion;
						galleryContent.UseSystemTypography = false;
						galleryContent.IsVisible = true;
						ResizeForCapture(entry);
						await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
					}
					else if (entry.Surface == CaptureSurface.Onboarding)
					{
						routeContent.IsVisible = false;
						galleryContent.IsVisible = false;
						onboardingViewModel.PrepareCaptureState(entry.OnboardingStep, entry.OnboardingScanActive);
						onboardingContent.IsVisible = true;
						ResizeForCapture(entry);
						await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
					}
					else
					{
						galleryContent.IsVisible = false;
						onboardingContent.IsVisible = false;
						routeContent.IsVisible = true;
						ResizeForCapture(entry);
						NavigateContemporary(entry.Route);
						await WaitForRouteReadyAsync(entry.Route);
						await WaitForRenderedRouteAsync(routeContent, entry.Route);
						PrepareFlightForCapture(entry);
						await PrepareProcessingForCaptureAsync(entry);
						PrepareDecanterForCapture(entry);
						await FocusFailedProcessingItemForCaptureAsync(entry);
					}
					await WaitForVisibleCoverLoadsAsync();
					await SettleAsync(plan.SettleMs);
					VerifyCaptureState(entry);
					await PresentCaptureFrameAsync(captureHost, plan.SettleMs);

					if (osHandshake is null)
					{
						var path = Path.Combine(outDir, entry.FileName);
						var actual = SaveWindowBitmap(path);
						log.AppendLine($"{entry.FileName}\t{actual.Width}x{actual.Height}\trequested {entry.Width}x{entry.Height}");
					}
					else
					{
						await WaitForOsCaptureAsync(osHandshake, index, entry);
						log.AppendLine($"{entry.FileName}\tmacOS screencapture handshake\trequested {entry.Width}x{entry.Height}");
					}
				}
				finally
				{
					ClearCaptureSeedState();
				}
			}
		}
		catch (Exception ex)
		{
			log.AppendLine($"FAILED\t{ex}");
			exitCode = 3;
		}

		try
		{
			Directory.CreateDirectory(CaptureEnvironment.OutputDirectory);
			File.WriteAllText(
				Path.Combine(CaptureEnvironment.OutputDirectory, "capture-log.txt"),
				log.ToString());
		}
		catch
		{
			// The process exit code still reports the outcome when the log itself cannot be written.
		}

		if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
			desktop.Shutdown(exitCode);
		else
			Environment.Exit(exitCode);
	}

	private void PrepareFlightForCapture(CaptureEntry entry)
	{
		if (contemporaryShellViewModel is not { } shell)
			return;
		if (shell.Flight.Count > 0)
			shell.Flight.Clear();
		shell.Library.IsDetailsPaneOpen = false;
		if (entry.FlightSelectionCount > 0)
			shell.Flight.AddRange(shell.Library.VisibleItems
				.Take(entry.FlightSelectionCount)
				.Select(item => item.LibraryBook));

		bool overlayIsOpen = shell.Layout.ShowFlightOverlay;
		bool flightIsAlreadyVisible = overlayIsOpen || shell.Layout.ShowPersistentFlight || shell.Layout.HostFlightInOverview;
		if (entry.OpenFlight != flightIsAlreadyVisible && !shell.Layout.ShowPersistentFlight && !shell.Layout.HostFlightInOverview)
			((System.Windows.Input.ICommand)shell.ToggleFlightCommand).Execute(null);
		if (entry.OpenDetails && shell.Library.VisibleItems.FirstOrDefault() is { } first)
			shell.Library.OpenItem(first);
	}

	private async Task PrepareProcessingForCaptureAsync(CaptureEntry entry)
	{
		if (contemporaryShellViewModel is not { } shell)
			return;
		if (shell.Processing.Source.Running)
			throw new CapturePlanException("Capture processing state cannot be prepared while the real queue runner is active.");

		ClearProcessingCaptureState(shell);
		var queue = shell.Processing.Source.Queue;
		// Use the queue container directly: unlike ProcessQueueViewModel.AddToQueue,
		// these deterministic lifecycle transitions never signal or start the runner.

		if (entry.ProcessingScenario == ProcessingCaptureScenario.Mixed)
		{
			var sourceItems = shell.Library.VisibleItems.Take(4).ToArray();
			if (sourceItems.Length != 4)
				throw new CapturePlanException("The mixed processing scenario requires four demo-library titles.");

			var completed = CreateCaptureProcess(sourceItems[0].LibraryBook);
			var failed = CreateCaptureProcess(sourceItems[2].LibraryBook);
			var active = CreateCaptureProcess(sourceItems[1].LibraryBook);
			active.AddConvertToMp3();
			var waiting = CreateCaptureProcess(sourceItems[3].LibraryBook);
			queue.Enqueue([completed, failed, active, waiting]);

			MoveNext(queue, completed);
			completed.SetCaptureState(
				ProcessBookStatus.Completed,
				ProcessBookResult.Success,
				ProcessBookPresentationStage.Completed,
				100);
			queue.MarkCompleted(completed);

			MoveNext(queue, failed);
			failed.SetCaptureState(
				ProcessBookStatus.Failed,
				ProcessBookResult.DiskFull,
				ProcessBookPresentationStage.None,
				64,
				lastPresentationStage: ProcessBookPresentationStage.Decrypting);
			queue.MarkCompleted(failed);

			MoveNext(queue, active);
			active.SetCaptureState(
				ProcessBookStatus.Working,
				ProcessBookResult.None,
				ProcessBookPresentationStage.Converting,
				43,
				TimeSpan.FromMinutes(4) + TimeSpan.FromSeconds(12));
			waiting.SetCaptureState(
				ProcessBookStatus.Queued,
				ProcessBookResult.None,
				ProcessBookPresentationStage.None,
				0,
				statusOverride: "Waiting to process");
			shell.Processing.Source.RunningTime = "03:18";

			await WaitForPropertyAsync(shell.Processing, () =>
				shell.Processing.Active.Count == 1
				&& ReferenceEquals(shell.Processing.Active[0].Source, active)
				&& shell.Processing.Waiting.Count == 1
				&& ReferenceEquals(shell.Processing.Waiting[0].Source, waiting)
				&& shell.Processing.Completed.Count == 1
				&& ReferenceEquals(shell.Processing.Completed[0].Source, completed)
				&& shell.Processing.Failed.Count == 1
				&& ReferenceEquals(shell.Processing.Failed[0].Source, failed));
			return;
		}

		if (entry.ProcessingScenario == ProcessingCaptureScenario.Empty || entry.ProcessingSeedCount == 0)
		{
			await WaitForPropertyAsync(shell.Processing, () =>
				shell.Processing.Active.Count == 0
				&& shell.Processing.Waiting.Count == 0
				&& shell.Processing.Completed.Count == 0
				&& shell.Processing.Failed.Count == 0);
			return;
		}

		var seeded = shell.Library.VisibleItems
			.Take(entry.ProcessingSeedCount)
			.Select(item => CreateCaptureProcess(item.LibraryBook))
			.ToArray();
		if (seeded.Length == 0)
		{
			await WaitForPropertyAsync(shell.Processing, () =>
				shell.Processing.Active.Count == 0
				&& shell.Processing.Waiting.Count == 0
				&& shell.Processing.Completed.Count == 0
				&& shell.Processing.Failed.Count == 0);
			return;
		}
		queue.Enqueue(seeded);
		if (seeded.FirstOrDefault() is { } activeSeed)
		{
			MoveNext(queue, activeSeed);
			activeSeed.SetCaptureState(
				ProcessBookStatus.Working,
				ProcessBookResult.None,
				ProcessBookPresentationStage.Downloading,
				43);
		}
		foreach (var waitingSeed in seeded.Skip(1))
			waitingSeed.SetCaptureState(
				ProcessBookStatus.Queued,
				ProcessBookResult.None,
				ProcessBookPresentationStage.None,
				0,
				statusOverride: "Waiting to process");

		await WaitForPropertyAsync(shell.Processing, () =>
			shell.Processing.Active.Count == 1
			&& ReferenceEquals(shell.Processing.Active[0].Source, seeded[0])
			&& shell.Processing.Waiting.Count == seeded.Length - 1
			&& shell.Processing.Waiting.Select(item => item.Source).SequenceEqual(seeded.Skip(1))
			&& shell.Processing.Completed.Count == 0
			&& shell.Processing.Failed.Count == 0);
	}

	private void ClearCaptureSeedState()
	{
		if (contemporaryShellViewModel is not { } shell)
			return;

		ClearProcessingCaptureState(shell);
		if (shell.Flight.Count > 0)
			shell.Flight.Clear();
		shell.Library.IsDetailsPaneOpen = false;
	}

	private static void ClearProcessingCaptureState(Shell.AppShellViewModel shell)
	{
		var queue = shell.Processing.Source.Queue;
		foreach (var active in queue.GetActive())
			queue.RemoveActive(active);
		queue.ClearQueue();
		queue.ClearCompleted();
		shell.Processing.Source.RunningTime = string.Empty;
	}

	private static CaptureProcessBookViewModel CreateCaptureProcess(LibraryBook book)
	{
		var process = new CaptureProcessBookViewModel(book, Configuration.Instance);
		process.AddDownloadDecryptBook();
		return process;
	}

	private static void MoveNext(
		LibationUiBase.TrackedQueue<ProcessBookViewModel> queue,
		ProcessBookViewModel expected)
	{
		if (!queue.TryDequeueNext(out var actual) || !ReferenceEquals(actual, expected))
			throw new InvalidOperationException("The capture queue did not preserve its deterministic item order.");
	}

	private void PrepareDecanterForCapture(CaptureEntry entry)
	{
		if (contemporaryShellViewModel is not { } shell)
			return;

		bool drawerIsOpen = shell.Layout.ShowDecanterDrawer;
		bool decanterIsPermanentlyVisible = shell.Layout.ShowQueueDock || shell.Layout.HostDecanterInOverview;
		if (entry.OpenDecanter != drawerIsOpen && !decanterIsPermanentlyVisible)
			((System.Windows.Input.ICommand)shell.ToggleDecanterDrawerCommand).Execute(null);
	}

	private async Task FocusFailedProcessingItemForCaptureAsync(CaptureEntry entry)
	{
		if (!entry.FocusFailedProcessingItem)
			return;
		if (entry.Route != Shell.AppRouteId.Processing
			|| entry.ProcessingScenario != ProcessingCaptureScenario.Mixed
			|| contemporaryShellViewModel is not { } shell
			|| shell.Processing.Failed.SingleOrDefault() is not { } failed)
			throw new CapturePlanException("A failed Processing-item focus requires the mixed Processing route with exactly one failed item.");

		await Dispatcher.UIThread.InvokeAsync(() =>
		{
			var view = this.GetVisualDescendants().OfType<Features.Processing.ProcessingView>().SingleOrDefault();
			if (view?.TryScrollItemIntoView(failed.Source) != true)
				throw new CapturePlanException("The failed Processing item was not available in the rendered queue.");
		}, DispatcherPriority.Background);
	}

	private void ResizeForCapture(CaptureEntry entry)
	{
		WindowState = WindowState.Normal;
		MinWidth = 720;
		MinHeight = 560;
		MaxWidth = double.PositiveInfinity;
		MaxHeight = double.PositiveInfinity;
		Width = entry.Width;
		Height = entry.Height;
		MaxWidth = entry.Width;
		MaxHeight = entry.Height;
		MinWidth = entry.Width;
		MinHeight = entry.Height;
		ClientSize = new Size(entry.Width, entry.Height);
	}

	private static void SizeCaptureSurface(Control surface, CaptureEntry entry)
	{
		// The route, gallery, and onboarding controls share a temporary Grid during
		// capture.  An inactive surface can otherwise contribute an oversized desired
		// size when the shell is re-parented, causing the active surface to be centered
		// and clipped instead of occupying the requested client area.
		surface.Width = entry.Width / entry.LogicalScale;
		surface.Height = entry.Height / entry.LogicalScale;
		surface.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
		surface.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
		surface.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative);
		surface.RenderTransform = Avalonia.Media.Transformation.TransformOperations.Parse($"scale({entry.LogicalScale})");
	}

	private static void SizeCaptureHost(Control surface, CaptureEntry entry)
	{
		surface.Width = entry.Width;
		surface.Height = entry.Height;
		surface.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
		surface.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
	}

	private void PrepareInitialCaptureSize(CaptureEntry entry)
	{
		// ClientSize requires the native platform window, so the pre-show path sets
		// only layout constraints. ResizeForCapture applies ClientSize after Opened.
		WindowState = WindowState.Normal;
		MinWidth = 720;
		MinHeight = 560;
		MaxWidth = double.PositiveInfinity;
		MaxHeight = double.PositiveInfinity;
		Width = entry.Width;
		Height = entry.Height;
		MaxWidth = entry.Width;
		MaxHeight = entry.Height;
		MinWidth = entry.Width;
		MinHeight = entry.Height;
	}

	private async Task WaitForLibraryReadyAsync()
	{
		while (loadedLibrary is null || ViewModel?.BindToGridTask is null)
			await Task.Delay(50);

		await ViewModel.BindToGridTask;
		await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
	}

	private static async Task WaitForOsCaptureAsync(
		string handshakeDirectory,
		int index,
		CaptureEntry entry)
	{
		var stem = index.ToString("D4");
		var ready = Path.Combine(handshakeDirectory, $"ready-{stem}.txt");
		var acknowledged = Path.Combine(handshakeDirectory, $"ack-{stem}.txt");
		File.WriteAllText(ready, $"{entry.FileName}\t{entry.Width}\t{entry.Height}{Environment.NewLine}");
		while (!File.Exists(acknowledged))
			await Task.Delay(50);
	}

	private async Task WaitForRouteReadyAsync(Shell.AppRouteId route)
	{
		if (contemporaryShellViewModel is not { } shell)
			throw new InvalidOperationException("The contemporary shell was not available for capture.");

		await WaitForPropertyAsync(shell, () => shell.CurrentRoute.Id == route);
		await WaitForPropertyAsync(shell.Library, () => !shell.Library.IsLoading);
		if (route == Shell.AppRouteId.Overview)
			await WaitForPropertyAsync(shell.Dashboard, () => shell.Dashboard.HasDashboardData || shell.Dashboard.HasError);
		else if (route == Shell.AppRouteId.Downloads)
			await WaitForPropertyAsync(shell.Downloads, () => !shell.Downloads.IsLoading);
		else if (route == Shell.AppRouteId.History)
			await WaitForPropertyAsync(shell.History, () => !shell.History.IsLoading);
		else if (route == Shell.AppRouteId.Trash)
			await WaitForPropertyAsync(shell.Trash, () => !shell.Trash.IsLoading);

		await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
	}

	private static async Task WaitForRenderedRouteAsync(AppShellView shellView, Shell.AppRouteId route)
	{
		while (!shellView.IsRoutePresented(route))
		{
			await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
			await Task.Delay(50);
		}
		await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
	}

	private async Task WaitForCaptureExperienceAsync(CaptureEntry entry)
	{
		if (contemporaryShellViewModel is not { } shell)
			throw new InvalidOperationException("The contemporary shell was not available for capture.");

		await WaitForPropertyAsync(shell, () => shell.Profile.Style == entry.Profile);
		await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
	}

	private void VerifyCaptureState(CaptureEntry entry)
	{
		if (entry.Surface != CaptureSurface.Route)
			return;
		if (contemporaryShellViewModel is not { } shell)
			throw new InvalidOperationException("The contemporary shell was not available for capture.");
		if (shell.Profile.Style != entry.Profile)
			throw new CapturePlanException($"Capture profile drifted from {entry.Profile} to {shell.Profile.Style} before presentation.");
		if (shell.CurrentRoute.Id != entry.Route)
			throw new CapturePlanException($"Capture route drifted from {entry.Route} to {shell.CurrentRoute.Id} before presentation.");
		if (entry.ProcessingScenario == ProcessingCaptureScenario.Mixed
			&& (shell.Processing.ActiveCount != 1
				|| shell.Processing.WaitingCount != 1
				|| shell.Processing.CompletedCount != 1
				|| shell.Processing.FailedCount != 1))
		{
			throw new CapturePlanException("The mixed Processing capture lost its deterministic 1/1/1/1 queue state before presentation.");
		}
	}

	private static async Task PresentCaptureFrameAsync(Control captureHost, int settleMs)
	{
		await Dispatcher.UIThread.InvokeAsync(() =>
		{
			captureHost.InvalidateMeasure();
			captureHost.InvalidateArrange();
			captureHost.InvalidateVisual();
		}, DispatcherPriority.Render);
		// Dispatcher completion only proves that Avalonia scheduled the frame. The
		// native-window capture must also wait for the compositor to present it.
		await SettleAsync(settleMs);
	}

	private async Task WaitForVisibleCoverLoadsAsync()
	{
		for (;;)
		{
			await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
			var pending = this.GetVisualDescendants()
				.Select(control => control switch
				{
					GalleryBookCard gallery => gallery.CoverLoadTask,
					CachedCover details => details.CoverLoadTask,
					_ => null,
				})
				.Where(task => task is { IsCompleted: false })
				.Cast<Task>()
				.Distinct()
				.ToArray();
			if (pending.Length == 0)
			{
				await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
				if (!this.GetVisualDescendants().Any(control => control switch
					{
						GalleryBookCard gallery => !gallery.CoverLoadTask.IsCompleted,
						CachedCover details => !details.CoverLoadTask.IsCompleted,
						_ => false,
					}))
					return;
				continue;
			}

			await Task.WhenAll(pending);
		}
	}

	private static Task WaitForPropertyAsync(INotifyPropertyChanged source, Func<bool> predicate)
	{
		if (predicate())
			return Task.CompletedTask;

		var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		PropertyChangedEventHandler? handler = null;
		handler = (_, _) =>
		{
			if (!predicate())
				return;
			source.PropertyChanged -= handler;
			completion.TrySetResult();
		};
		source.PropertyChanged += handler;
		if (predicate())
		{
			source.PropertyChanged -= handler;
			completion.TrySetResult();
		}
		return completion.Task;
	}

	/// <summary>Two dispatcher passes at Background priority plus a real delay, so layout, bindings and dashboard refresh settle.</summary>
	private static async Task SettleAsync(int milliseconds)
	{
		await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
		await Task.Delay(milliseconds);
		await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
	}

	/// <summary>Renders the whole window at its render scaling and returns the pixel size written.</summary>
	private PixelSize SaveWindowBitmap(string path)
	{
		var scale = RenderScaling;
		var size = new PixelSize(
			Math.Max(1, (int)Math.Round(Bounds.Width * scale)),
			Math.Max(1, (int)Math.Round(Bounds.Height * scale)));
		using var bitmap = new RenderTargetBitmap(size, new Vector(96 * scale, 96 * scale));
		bitmap.Render((Visual)(Content ?? throw new InvalidOperationException("The capture window has no content.")));
		bitmap.Save(path);
		return size;
	}

	private sealed class CaptureProcessBookViewModel(LibraryBook book, Configuration configuration)
		: ProcessBookViewModel(book, configuration)
	{
		public void SetCaptureState(
			ProcessBookStatus status,
			ProcessBookResult result,
			ProcessBookPresentationStage presentationStage,
			int progress,
			TimeSpan? timeRemaining = null,
			string? statusOverride = null,
			ProcessBookPresentationStage? lastPresentationStage = null)
		{
			Result = result;
			Status = status;
			PresentationStage = presentationStage;
			LastPresentationStage = lastPresentationStage ?? presentationStage;
			StatusOverride = statusOverride;
			Progress = progress;
			if (timeRemaining is { } remaining)
				TimeRemaining = remaining;
		}
	}
}
