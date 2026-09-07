using Avalonia;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LibationAvalonia.Diagnostics;

/// <summary>Finite, cancellable capture stages. Failed stages never leave property subscriptions behind.</summary>
internal static class CaptureReadiness
{
	internal static async Task RunAsync(string identity, string stage, int timeoutMs, Func<CancellationToken, Task> action)
	{
		using var timeout = new CancellationTokenSource(timeoutMs);
		try
		{
			await action(timeout.Token).WaitAsync(timeout.Token);
		}
		catch (OperationCanceledException) when (timeout.IsCancellationRequested)
		{
			throw new CapturePlanException($"Capture '{identity}' timed out during {stage} after {timeoutMs} ms.");
		}
	}

	internal static async Task WaitUntilAsync(Func<bool> predicate, CancellationToken cancellationToken)
	{
		while (!predicate())
			await Task.Delay(50, cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
	}

	internal static void ValidateGeometry(CaptureEntry entry, Size client, Rect host, Rect surface, Matrix transform, double renderScale)
	{
		static bool Near(double actual, double expected)
			=> double.IsFinite(actual) && Math.Abs(actual - expected) <= 0.5;
		if (!Near(client.Width, entry.Width) || !Near(client.Height, entry.Height)
			|| !Near(host.X, 0) || !Near(host.Y, 0)
			|| !Near(host.Width, entry.Width) || !Near(host.Height, entry.Height)
			|| !Near(surface.X, 0) || !Near(surface.Y, 0)
			|| !Near(surface.Width * entry.LogicalScale, entry.Width)
			|| !Near(surface.Height * entry.LogicalScale, entry.Height)
			|| transform != Matrix.CreateScale(entry.LogicalScale, entry.LogicalScale)
			|| !double.IsFinite(renderScale) || renderScale <= 0)
			throw new CapturePlanException($"Capture '{entry.FileName}' has invalid geometry: requested {entry.Width}x{entry.Height}, client {client}, host {host}, surface {surface}, transform {transform}, render scale {renderScale}.");
	}
}
