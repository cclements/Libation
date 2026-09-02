using LibationAvalonia.DesignSystem.Components;
using System.Collections.Generic;
using System.Windows.Input;

namespace LibationAvalonia.Shell;

/// <summary>
/// The single presentation contract consumed by the contemporary shell header.
/// Feature view models keep command ownership; the shell owns where the header is rendered.
/// </summary>
public interface IRoutePresentation
{
	string RouteEyebrow { get; }
	string RouteTitle { get; }
	string? RouteSubtitle { get; }
	RouteCommandPresentation? RoutePrimaryCommand { get; }
	IReadOnlyList<RouteCommandPresentation> RouteSecondaryCommands { get; }
	RouteStatusPresentation? RouteStatusBadge { get; }
}

public sealed record RouteCommandPresentation(
	string Text,
	ICommand Command,
	object? Parameter = null);

public sealed record RouteStatusPresentation(
	string Text,
	LibationStatusKind Status);

public sealed record StaticRoutePresentation(
	string RouteEyebrow,
	string RouteTitle,
	string? RouteSubtitle,
	RouteCommandPresentation? RoutePrimaryCommand,
	IReadOnlyList<RouteCommandPresentation> RouteSecondaryCommands,
	RouteStatusPresentation? RouteStatusBadge) : IRoutePresentation;
