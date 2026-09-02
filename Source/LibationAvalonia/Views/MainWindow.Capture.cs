using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LibationAvalonia.Diagnostics;
using LibationAvalonia.Features.Library;
using LibationFileManager;
using System;
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
	private async Task RunCapturePlanIfRequestedAsync()
	{
		if (!CaptureEnvironment.IsRequested)
			return;

		var log = new StringBuilder();
		var exitCode = 0;
		try
		{
			var plan = CapturePlan.Load(CaptureEnvironment.PlanPath);
			var outDir = Directory.CreateDirectory(CaptureEnvironment.OutputDirectory).FullName;
			var osHandshake = CaptureEnvironment.OsHandshakeDirectory;
			if (osHandshake is not null)
				Directory.CreateDirectory(osHandshake);
			await WaitForLibraryReadyAsync();

			for (var index = 0; index < plan.Entries.Count; index++)
			{
				var entry = plan.Entries[index];
				var baseline = Configuration.Instance.GetContemporaryExperienceSettings();
				Configuration.Instance.SaveContemporaryExperienceSettings(baseline with
				{
					ExperienceStyle = entry.Profile,
					DensityMode = entry.Density,
					DecorationLevel = entry.Decoration,
					UseContemporaryShell = true,
				});
				await SettleAsync(plan.SettleMs / 2);

				NavigateContemporary(entry.Route);
				Width = entry.Width;
				Height = entry.Height;
				await WaitForRouteReadyAsync(entry.Route);
				await WaitForVisibleCoverLoadsAsync();
				await SettleAsync(plan.SettleMs);

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

		await WaitForPropertyAsync(shell.Library, () => !shell.Library.IsLoading);
		if (route == Shell.AppRouteId.Overview)
			await WaitForPropertyAsync(shell.Dashboard, () => shell.Dashboard.HasDashboardData || shell.Dashboard.HasError);
		else if (route == Shell.AppRouteId.Downloads)
			await WaitForPropertyAsync(shell.Downloads, () => !shell.Downloads.IsLoading);
		else if (route == Shell.AppRouteId.History)
			await WaitForPropertyAsync(shell.History, () => !shell.History.IsLoading);

		await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
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
}
