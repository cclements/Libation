using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using Resources = LibationAvalonia.Properties.Resources;

namespace LibationAvalonia.Shell;

/// <summary>Stable, typed destinations shared by both contemporary profiles.</summary>
public enum AppRouteId
{
	Overview,
	Library,
	Downloads,
	Processing,
	History,
	Accounts,
	Settings,
	Tools,
	Trash,
	About,
}

public sealed record AppRoute(
	AppRouteId Id,
	string Label,
	string GlyphResourceKey,
	string AccessibleDescription,
	bool IsUtility = false);

public static class AppRoutes
{
	private static readonly FrozenDictionary<AppRouteId, AppRoute> routes =
		new Dictionary<AppRouteId, AppRoute>
		{
			[AppRouteId.Overview] = new(AppRouteId.Overview, Resources.RouteOverviewLabel, "glyph.overview", Resources.RouteOverviewDescription),
			[AppRouteId.Library] = new(AppRouteId.Library, Resources.RouteLibraryLabel, "glyph.library", Resources.RouteLibraryDescription),
			[AppRouteId.Downloads] = new(AppRouteId.Downloads, Resources.RouteDownloadsLabel, "glyph.downloads", Resources.RouteDownloadsDescription),
			[AppRouteId.Processing] = new(AppRouteId.Processing, Resources.RouteProcessingLabel, "glyph.processing", Resources.RouteProcessingDescription),
			[AppRouteId.History] = new(AppRouteId.History, Resources.RouteHistoryLabel, "glyph.history", Resources.RouteHistoryDescription),
			[AppRouteId.Accounts] = new(AppRouteId.Accounts, Resources.RouteAccountsLabel, "glyph.accounts", Resources.RouteAccountsDescription, true),
			[AppRouteId.Settings] = new(AppRouteId.Settings, Resources.RouteSettingsLabel, "glyph.settings", Resources.RouteSettingsDescription, true),
			[AppRouteId.Tools] = new(AppRouteId.Tools, Resources.RouteToolsLabel, "glyph.tools", Resources.RouteToolsDescription, true),
			[AppRouteId.Trash] = new(AppRouteId.Trash, Resources.RouteTrashLabel, "glyph.trash", Resources.RouteTrashDescription, true),
			[AppRouteId.About] = new(AppRouteId.About, Resources.RouteAboutLabel, "brand.mark.one-color", Resources.RouteAboutDescription, true),
		}.ToFrozenDictionary();

	public static IReadOnlyDictionary<AppRouteId, AppRoute> All => routes;

	public static AppRoute Get(AppRouteId id)
		=> routes.TryGetValue(id, out var route)
			? route
			: throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown application route.");
}
