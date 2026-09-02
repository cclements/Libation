using Avalonia.Controls;
using Avalonia.Input;
using LibationAvalonia.Shell;

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
		AttachedToVisualTree += (_, _) => ApplyLayout(DesktopLayoutClass.Wide, onlyWhenUnset: true);
	}

	internal void ApplyLayout(DesktopLayoutClass layoutClass) => ApplyLayout(layoutClass, onlyWhenUnset: false);

	private void ApplyLayout(DesktopLayoutClass layoutClass, bool onlyWhenUnset)
	{
		if (onlyWhenUnset && currentClass is not null)
			return;
		string next = layoutClass switch
		{
			DesktopLayoutClass.Wide => ":wide",
			DesktopLayoutClass.Standard => ":standard",
			DesktopLayoutClass.Compact => ":compact",
			_ => ":narrow",
		};
		if (next == currentClass)
			return;

		foreach (var pseudoClass in new[] { ":wide", ":standard", ":compact", ":narrow" })
			PseudoClasses.Set(pseudoClass, pseudoClass == next);
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
