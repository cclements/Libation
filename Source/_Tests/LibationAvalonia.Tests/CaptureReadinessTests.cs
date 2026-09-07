using Avalonia;
using LibationAvalonia.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LibationAvalonia.Tests;

[TestClass]
public class CaptureReadinessTests
{
	[TestMethod]
	public async Task FailedStage_TerminatesWithEntryAndStageIdentity()
	{
		var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var exception = await Assert.ThrowsExactlyAsync<CapturePlanException>(() => CaptureReadiness.RunAsync(
			"0003/missing.png", "rendered route", 100, async token =>
			{
				try { await CaptureReadiness.WaitUntilAsync(() => false, token); }
				finally { stopped.TrySetResult(); }
			}));
		await stopped.Task.WaitAsync(TimeSpan.FromSeconds(2));
		StringAssert.Contains(exception.Message, "0003/missing.png");
		StringAssert.Contains(exception.Message, "rendered route");
		StringAssert.Contains(exception.Message, "100 ms");
	}

	[TestMethod]
	public async Task SuccessfulStage_DoesNotWaitForDeadline()
	{
		await CaptureReadiness.RunAsync("ready.png", "ready", 1000,
			token => CaptureReadiness.WaitUntilAsync(() => true, token));
	}

	[TestMethod]
	public async Task FaultedStage_PreservesOriginalFailure()
	{
		var failure = new InvalidOperationException("fixture failed");
		var actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => CaptureReadiness.RunAsync(
			"fault.png", "fixture", 1000, _ => Task.FromException(failure)));
		Assert.AreSame(failure, actual);
	}

	[TestMethod]
	public void Geometry_RejectsStaleClientClippedSurfaceOffsetAndScale()
	{
		var entry = CapturePlan.Parse("""
			{"entries":[{"profile":"Cellar","route":"Library","width":720,"height":560,"logicalScale":2}]}
			""").Entries[0];
		var client = new Size(720, 560);
		var host = new Rect(0, 0, 720, 560);
		var surface = new Rect(0, 0, 360, 280);
		var scale = Matrix.CreateScale(2, 2);
		CaptureReadiness.ValidateGeometry(entry, client, host, surface, scale, 2);
		Assert.ThrowsExactly<CapturePlanException>(() => CaptureReadiness.ValidateGeometry(entry, new Size(960, 720), host, surface, scale, 2));
		Assert.ThrowsExactly<CapturePlanException>(() => CaptureReadiness.ValidateGeometry(entry, client, host, new Rect(0, 0, 360, 140), scale, 2));
		Assert.ThrowsExactly<CapturePlanException>(() => CaptureReadiness.ValidateGeometry(entry, client, host, new Rect(40, 0, 360, 280), scale, 2));
		Assert.ThrowsExactly<CapturePlanException>(() => CaptureReadiness.ValidateGeometry(entry, client, host, surface, Matrix.Identity, 2));
		Assert.ThrowsExactly<CapturePlanException>(() => CaptureReadiness.ValidateGeometry(entry, client, host, surface, scale, double.NaN));
	}
}
