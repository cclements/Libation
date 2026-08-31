using Avalonia.Controls;
using Avalonia.Input;

namespace LibationAvalonia.Features.Overview;

/// <summary>
/// Applies the four effective-width classes defined by implementation plan section 8.
/// It changes composition only; the DataContext is never replaced during reflow.
/// </summary>
public abstract class OverviewViewBase : UserControl
{
	private string? currentClass;

	protected OverviewViewBase()
	{
		SizeChanged += (_, e) => UpdateLayoutClass(e.NewSize.Width);
		AttachedToVisualTree += (_, _) => UpdateLayoutClass(Bounds.Width);
	}

	private void UpdateLayoutClass(double width)
	{
		if (width <= 0)
			return;
		string next = width >= 1360 ? ":wide"
			: width >= 1080 ? ":standard"
			: width >= 840 ? ":compact"
			: ":narrow";
		if (next == currentClass)
			return;

		foreach (var layoutClass in new[] { ":wide", ":standard", ":compact", ":narrow" })
			PseudoClasses.Set(layoutClass, layoutClass == next);
		currentClass = next;
	}

	protected void SearchBox_KeyDown(object? sender, KeyEventArgs e)
	{
		if (e.Key != Key.Enter || DataContext is not DashboardViewModel dashboard)
			return;
		if (dashboard.ApplySearchCommand.CanExecute(null))
			dashboard.ApplySearchCommand.Execute(null);
		e.Handled = true;
	}
}
