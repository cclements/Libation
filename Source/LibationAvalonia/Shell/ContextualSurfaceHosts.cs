using Avalonia.Controls;
using LibationAvalonia.DesignSystem.Components;
using LibationAvalonia.Features.Flight;

namespace LibationAvalonia.Shell;

/// <summary>Moves the one Current Flight control without recreating its view or state.</summary>
public sealed class FlightSurfaceHost(CurrentFlightView surface)
{
	public CurrentFlightView Surface { get; } = surface;
	public void AttachTo(ContentControl target) => Reparent(Surface, target);

	private static void Reparent(Control surface, ContentControl target)
	{
		if (ReferenceEquals(surface.Parent, target))
			return;
		if (surface.Parent is ContentControl current)
			current.Content = null;
		target.Content = surface;
	}
}

/// <summary>Moves the one Decanter summary without creating a parallel queue surface.</summary>
public sealed class DecanterSurfaceHost(DecanterSummary surface)
{
	public DecanterSummary Surface { get; } = surface;
	public void AttachTo(ContentControl target)
	{
		if (ReferenceEquals(Surface.Parent, target))
			return;
		if (Surface.Parent is ContentControl current)
			current.Content = null;
		target.Content = Surface;
	}
}
