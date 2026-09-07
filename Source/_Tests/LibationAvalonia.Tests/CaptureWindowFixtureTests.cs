using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Input;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using LibationAvalonia.Diagnostics;
using LibationAvalonia.DesignSystem;
using Avalonia.Styling;
using LibationAvalonia.ViewModels.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LibationAvalonia.Tests;

[TestClass]
[DoNotParallelize]
public class CaptureWindowFixtureTests
{
	private static readonly HeadlessUnitTestSession Session = HeadlessUnitTestSession.StartNew(
		typeof(HeadlessTestAppBuilder), AvaloniaTestIsolationLevel.PerTest);

	// Native dialogs initialize additional font/layout caches. Keep those windows
	// in a separate application scope instead of altering other tests' renderer.
	private static Task Dispatch(Action action) => DispatchAsync(() => { action(); return Task.CompletedTask; });
	private static Task DispatchAsync(Func<Task> action)
	{
		var configuration = HeadlessTestHost.Configuration;
		return Session.Dispatch(async () =>
		{
			using var manager = new ExperienceManager(App.Current, configuration.CreateEphemeralCopy(),
				reducedMotionResolver: new UnavailableSystemReducedMotionResolver());
			manager.Initialize();
			App.Current.RequestedThemeVariant = ThemeVariant.Light;
			await action();
			return true;
		}, CancellationToken.None);
	}

	[ClassCleanup]
	public static async Task Cleanup() => await Session.DisposeAsync();

	private static IEnumerable<AutomationPeer> Descendants(AutomationPeer peer)
	{
		foreach (var child in peer.GetChildren())
		{
			yield return child;
			foreach (var descendant in Descendants(child)) yield return descendant;
		}
	}

	private static CaptureEntry Entry(string surface, string fixture, int width = 1280, int height = 900, string size = "Natural")
		=> CapturePlan.Parse($$"""
			{"entries":[{"profile":"Cellar","surface":"{{surface}}","fixture":"{{fixture}}","width":{{width}},"height":{{height}},"windowSize":"{{size}}"}]}
			""").Entries[0];

	[TestMethod]
	public async Task FixturesUseExactOwnedWindowAndLeaveUnrelatedSiblingOpen()
	{
		await Dispatch(() =>
		{
			var owner = new Window { Width = 1280, Height = 900 };
			var sibling = new Window { Width = 300, Height = 200 };
			owner.Show(); sibling.Show(owner);
			try
			{
				foreach (var (surface, fixture, type) in new[] { ("Dialog", "Settings", "SettingsDialog"), ("Window", "About", "AboutDialog"), ("Message", "RemoveConfirmation", "MessageBoxWindow") })
				{
					using var capture = new CaptureWindowFixture(owner, Entry(surface, fixture), HeadlessTestHost.Configuration);
					capture.Show(); Dispatcher.UIThread.RunJobs();
					capture.Validate();
					var windowPeer = ControlAutomationPeer.CreatePeerForElement(capture.Window)!;
					var shieldPeer = ControlAutomationPeer.CreatePeerForElement((Control)capture.Window.Content!)!;
					Assert.AreEqual(0, shieldPeer.GetChildren().Count);
					var exposed = Descendants(windowPeer).ToArray();
					Assert.IsTrue(exposed.Contains(shieldPeer), "The accessibility window must use the guarded content peer.");
					Assert.IsFalse(exposed.OfType<IInvokeProvider>().Any(), "Accessibility invocation must not reach production action handlers.");
					if (surface != "Message")
					{
						var focused = capture.Window.FocusManager?.GetFocusedElement();
						Assert.IsTrue(focused is null || ReferenceEquals(focused, capture.Window), "A hidden action control must not escape through the focused-element channel.");
					}
					Assert.AreEqual(type, capture.Window.GetType().Name);
					Assert.AreSame(owner, capture.Window.Owner);
					Assert.AreEqual(surface != "Window", capture.IsModal);
					capture.Dispose(); Dispatcher.UIThread.RunJobs();
					Assert.IsTrue(capture.IsClosed);
					Assert.IsTrue(owner.IsVisible);
					Assert.IsTrue(sibling.IsVisible);
					Assert.ThrowsExactly<ObjectDisposedException>(capture.Show);
				}
			}
			finally { sibling.Close(); owner.Close(); }
		});
	}

	[TestMethod]
	public async Task SettingsDraftAndCommitKeyCannotPersistAndNextFixtureStartsFresh()
	{
		await Dispatch(() =>
		{
			var config = HeadlessTestHost.Configuration;
			var before = config.CreateCueSheet;
			var owner = new Window { Width = 1280, Height = 900 }; owner.Show();
			try
			{
				using (var capture = new CaptureWindowFixture(owner, Entry("Dialog", "Settings"), config))
				{
					capture.Show(); Dispatcher.UIThread.RunJobs();
					var draft = (SettingsVM)capture.Window.DataContext!;
					draft.AudioSettings.CreateCueSheet = !before;
					var enter = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Return };
					capture.Window.RaiseEvent(enter); Dispatcher.UIThread.RunJobs();
					Assert.IsTrue(enter.Handled);
					Assert.IsFalse(capture.IsClosed);
					Assert.AreEqual(before, config.CreateCueSheet);
					var grid = (Grid)capture.Window.Content!;
					capture.Window.UpdateLayout();
					AvaloniaHeadlessPlatform.ForceRenderTimerTick();
					Assert.IsInstanceOfType<Border>(capture.Window.InputHitTest(new Point(450, 350)));
					Assert.AreEqual(2, grid.Children.Count);
				}
				using var next = new CaptureWindowFixture(owner, Entry("Dialog", "Settings"), config);
				Assert.AreEqual(before, ((SettingsVM)next.Window.DataContext!).AudioSettings.CreateCueSheet);
			}
			finally { owner.Close(); }
		});
	}

	[TestMethod]
	public async Task NaturalOverflowRejectsBeforeShowAndClampedCaptureKeepsRealControls()
	{
		await Dispatch(() =>
		{
			var owner = new Window { Width = 1280, Height = 900 }; owner.Show();
			try
			{
				Assert.ThrowsExactly<CapturePlanException>(() => new CaptureWindowFixture(owner, Entry("Dialog", "Settings", 720, 560), HeadlessTestHost.Configuration));
				using var capture = new CaptureWindowFixture(owner, Entry("Dialog", "Settings", 720, 560, "Clamped"), HeadlessTestHost.Configuration);
				capture.Show(); Dispatcher.UIThread.RunJobs(); capture.Validate();
				Assert.AreEqual(new Size(720, 560), capture.Window.ClientSize);
				Assert.IsInstanceOfType<SettingsVM>(capture.Window.DataContext);
			}
			finally { owner.Close(); }
		});
	}

	[TestMethod]
	public async Task OwnerClosureClosesItsFixtureAndClosedFixtureCannotBecomeReady()
	{
		await DispatchAsync(async () =>
		{
			var owner = new Window { Width = 1280, Height = 900 }; owner.Show();
			using var capture = new CaptureWindowFixture(owner, Entry("Window", "About"), HeadlessTestHost.Configuration);
			capture.Show(); Dispatcher.UIThread.RunJobs(); owner.Close(); Dispatcher.UIThread.RunJobs();
			Assert.IsTrue(capture.IsClosed);
			await Assert.ThrowsExactlyAsync<CapturePlanException>(() => capture.WaitUntilReadyAsync(CancellationToken.None));
			await capture.WaitUntilClosedAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(1));
		});
	}

	[TestMethod]
	public async Task ReadinessCancellationAndStageFailureStillCloseOnlyFixture()
	{
		await DispatchAsync(async () =>
		{
			var owner = new Window { Width = 1280, Height = 900 }; owner.Show();
			var capture = new CaptureWindowFixture(owner, Entry("Message", "RemoveConfirmation"), HeadlessTestHost.Configuration);
			try
			{
				// Deliberately leave it unshown so readiness cannot complete.
				await Assert.ThrowsExactlyAsync<CapturePlanException>(() => CaptureReadiness.RunAsync(
					"0000/remove.png", "native fixture readiness", 100, capture.WaitUntilReadyAsync));
			}
			finally
			{
				capture.Dispose(); Assert.IsTrue(capture.IsClosed);
				Assert.IsTrue(owner.IsVisible); Assert.IsTrue(owner.IsEnabled); owner.Close();
			}
		});
	}

	[TestMethod]
	public async Task FailedShowRestoresOwnerAndDoesNotWaitForAnUnopenedWindow()
	{
		await DispatchAsync(async () =>
		{
			var owner = new Window { Width = 1280, Height = 900 };
			// A modal cannot be shown before its owner is visible.
			using var capture = new CaptureWindowFixture(owner, Entry("Message", "RemoveConfirmation"), HeadlessTestHost.Configuration);
			Assert.ThrowsExactly<InvalidOperationException>(capture.Show);
			Assert.ThrowsExactly<CapturePlanException>(capture.Show);
			capture.Dispose();
			Assert.IsTrue(owner.IsEnabled);
			await capture.WaitUntilClosedAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(1));
			Assert.IsTrue(capture.IsClosed);
			owner.Close();
		});
	}

	[TestMethod]
	public async Task EscapeUsesRealDialogCloseAndFailedValidationStillRestoresOwner()
	{
		await DispatchAsync(async () =>
		{
			var owner = new Window { Width = 1280, Height = 900 }; owner.Show();
			try
			{
				using (var capture = new CaptureWindowFixture(owner, Entry("Message", "RemoveConfirmation"), HeadlessTestHost.Configuration))
				{
					capture.Show(); Dispatcher.UIThread.RunJobs();
					Assert.IsFalse(owner.IsEnabled);
					capture.Window.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
					await capture.WaitUntilClosedAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(1));
					Assert.AreEqual("None", capture.CloseResult);
				}
				Assert.IsTrue(owner.IsEnabled);
				using (var capture = new CaptureWindowFixture(owner, Entry("Window", "About"), HeadlessTestHost.Configuration))
				{
					capture.Show(); Dispatcher.UIThread.RunJobs();
					capture.Window.Title = "Wrong identity";
					Assert.ThrowsExactly<CapturePlanException>(capture.Validate);
				}
				Assert.IsTrue(owner.IsEnabled); Assert.IsTrue(owner.IsVisible);
			}
			finally { owner.Close(); }
		});
	}
}
