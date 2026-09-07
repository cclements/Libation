using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation.Peers;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using LibationAvalonia.Dialogs;
using LibationFileManager;
using LibationUiBase.Forms;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LibationAvalonia.Diagnostics;

/// <summary>
/// One owned native window from a closed fixture list. The real controls render,
/// but pointer actions and commit keys cannot reach settings, account, update,
/// or link handlers. Escape and native close exercise the real close lifecycle.
/// </summary>
internal sealed class CaptureWindowFixture : IDisposable
{
	private readonly Window owner;
	private readonly CaptureEntry entry;
	private readonly TaskCompletionSource closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
	private readonly Control originalContent;
	private readonly Border inputShield = new() { Background = Brushes.Transparent };
	private readonly Grid shieldHost;
	private Task<DialogResult>? modalResult;
	private bool showAttempted;
	private bool shown;
	private bool ownerWasEnabled;
	private bool disposed;
	public DialogWindow Window { get; }
	public Size ExpectedClientSize { get; }
	public string ExpectedTitle { get; }
	public bool IsClosed => closed.Task.IsCompleted;
	public bool IsModal => entry.Surface != CaptureSurface.Window;
	public string CloseResult => Window.DialogResult.ToString();

	public CaptureWindowFixture(Window owner, CaptureEntry entry, Configuration configuration)
	{
		this.owner = owner;
		this.entry = entry;
		Window = (entry.Surface, entry.Fixture) switch
		{
			(CaptureSurface.Dialog, CaptureFixture.Settings) => new SettingsDialog(SettingsDialogSection.AudioFiles, configuration.CreateEphemeralCopy()),
			(CaptureSurface.Window, CaptureFixture.About) => new AboutDialog(),
			(CaptureSurface.Message, CaptureFixture.RemoveConfirmation) => MessageBox.CreateMessageBox(owner,
				"Remove these 2 books from the library?\n\nThe Clockmaker's Map\nA Field Guide to Quiet Places",
				"Remove Books", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2, false),
			_ => throw new CapturePlanException($"Unsupported native fixture {entry.Surface}/{entry.Fixture}."),
		};
		try
		{
			Window.SaveAndRestorePosition = false;
			ExpectedTitle = Window.Title ?? throw new CapturePlanException("A native fixture needs an exact window title.");
			var natural = new Size(Window.Width, Window.Height);
			if (!double.IsFinite(natural.Width) || !double.IsFinite(natural.Height) || natural.Width <= 0 || natural.Height <= 0)
				throw new CapturePlanException($"Fixture {entry.Fixture} has no finite natural size.");
			if (entry.WindowSize == CaptureWindowSize.Natural && (natural.Width > entry.Width || natural.Height > entry.Height))
				throw new CapturePlanException($"Fixture {entry.Fixture} natural size {natural} exceeds viewport {entry.Width}x{entry.Height}; request Clamped explicitly.");
			ExpectedClientSize = entry.WindowSize == CaptureWindowSize.Clamped
				? new Size(Math.Min(natural.Width, entry.Width), Math.Min(natural.Height, entry.Height)) : natural;
			// These constraints simulate the named viewport only. They do not repair
			// clipped descendants or establish responsive-layout acceptance.
			Window.MinWidth = Math.Min(Window.MinWidth, ExpectedClientSize.Width);
			Window.MinHeight = Math.Min(Window.MinHeight, ExpectedClientSize.Height);
			Window.MaxWidth = Window.Width = ExpectedClientSize.Width;
			Window.MaxHeight = Window.Height = ExpectedClientSize.Height;
			originalContent = Window.Content as Control ?? throw new CapturePlanException("The fixture has no control content.");
			Window.Content = null;
			shieldHost = new CaptureInputHost();
			shieldHost.Children.Add(originalContent);
			shieldHost.Children.Add(inputShield);
			Window.Content = shieldHost;
			Window.AddHandler(InputElement.KeyDownEvent, SuppressActionKeys, RoutingStrategies.Tunnel);
			Window.Closed += OnClosed;
			owner.Closed += OnOwnerClosed;
		}
		catch
		{
			Window.Close();
			throw;
		}
	}

	public void Show()
	{
		ObjectDisposedException.ThrowIf(disposed, this);
		if (showAttempted) throw new CapturePlanException("A native fixture cannot be shown twice.");
		showAttempted = true;
		ownerWasEnabled = owner.IsEnabled;
		owner.IsEnabled = false;
		if (IsModal) modalResult = Window.ShowDialog<DialogResult>(owner);
		else Window.Show(owner);
		shown = true;
	}

	public Task WaitUntilReadyAsync(CancellationToken token)
		=> CaptureReadiness.WaitUntilAsync(() =>
		{
			if (IsClosed) throw new CapturePlanException($"Fixture {entry.Fixture} closed before capture readiness.");
			return Window.IsVisible && Window.IsLoaded && Window.ClientSize == ExpectedClientSize
				&& originalContent.Bounds.Width > 0 && originalContent.Bounds.Height > 0;
		}, token);

	public void Validate()
	{
		if (disposed || IsClosed || !Window.IsVisible || !Window.IsLoaded || Window.Owner != owner
			|| Window.Title != ExpectedTitle || Window.ClientSize != ExpectedClientSize
			|| Window.Content != shieldHost || !inputShield.IsEffectivelyVisible || Window.SaveAndRestorePosition
			|| !double.IsFinite(Window.RenderScaling) || Window.RenderScaling <= 0)
			throw new CapturePlanException($"Native fixture {entry.Surface}/{entry.Fixture} lost its owner, identity, input shield, or geometry before capture.");
	}

	public async Task WaitUntilClosedAsync(CancellationToken token)
	{
		await closed.Task.WaitAsync(token);
		if (modalResult is not null) await modalResult.WaitAsync(token);
	}

	private sealed class CaptureInputHost : Grid
	{
		protected override AutomationPeer OnCreateAutomationPeer() => new CaptureInputPeer(this);
	}
	private sealed class CaptureInputPeer(Control owner) : ControlAutomationPeer(owner)
	{
		// Accessibility Invoke can bypass hit testing and key routing. The visual
		// fixture deliberately exposes no actionable descendants to that channel.
		protected override IReadOnlyList<AutomationPeer> GetChildrenCore() => [];
	}

	private static void SuppressActionKeys(object? sender, KeyEventArgs e)
	{
		if (e.Key != Key.Escape) e.Handled = true;
	}
	private void OnClosed(object? sender, EventArgs e) => closed.TrySetResult();
	private void OnOwnerClosed(object? sender, EventArgs e) => Window.Close(DialogResult.Cancel);

	public void Dispose()
	{
		if (disposed) return;
		disposed = true;
		try
		{
			if (!IsClosed) Window.Close(DialogResult.Cancel);
		}
		finally
		{
			if (showAttempted) owner.IsEnabled = ownerWasEnabled;
			owner.Closed -= OnOwnerClosed;
			Window.Closed -= OnClosed;
			Window.RemoveHandler(InputElement.KeyDownEvent, SuppressActionKeys);
			// Closing an unshown native window need not raise Closed.
			if (!shown) closed.TrySetResult();
		}
	}
}
