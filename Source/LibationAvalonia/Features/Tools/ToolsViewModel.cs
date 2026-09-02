using LibationAvalonia.DesignSystem.Components;
using LibationAvalonia.Properties;
using LibationAvalonia.Shell;
using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace LibationAvalonia.Features.Tools;

public sealed record ToolActionItem(
	string Name,
	string Consequence,
	string RiskText,
	LibationStatusKind RiskStatus,
	ICommand Command,
	string? StateText = null);

public sealed record ToolActionGroup(string Name, string Description, IReadOnlyList<ToolActionItem> Actions);

/// <summary>
/// Discoverable index over existing advanced commands. Each action delegates to its
/// established owner, retaining existing dialogs and confirmations.
/// </summary>
public sealed class ToolsViewModel : SecondaryDestinationViewModel, IRoutePresentation
{
	public ToolsViewModel(ILibationCommandAdapter commands)
	{
		ArgumentNullException.ThrowIfNull(commands);
		var main = commands.Main;

		var processVisible = CreateOwnerCommand(commands.ProcessVisible, "process visible titles", "Libation could not queue the visible titles. Review the current Library filter and try again.");
		var replaceTags = CreateOwnerCommand(commands.ReplaceVisibleTagsAsync, "replace visible-title tags", "Libation could not open the tag replacement workflow. No metadata was changed.");
		var setBookStatus = CreateOwnerCommand(commands.SetVisibleBookStatusAsync, "set visible audiobook status", "Libation could not open the audiobook-status workflow. No status was changed.");
		var setPdfStatus = CreateOwnerCommand(commands.SetVisiblePdfStatusAsync, "set visible PDF status", "Libation could not open the PDF-status workflow. No status was changed.");
		var detectStatus = CreateOwnerCommand(commands.DetectVisibleStatusAsync, "detect visible download status", "Libation could not inspect visible-title files. No status was changed.");
		var removeVisible = CreateOwnerCommand(commands.RemoveVisibleAsync, "remove visible titles", "Libation could not open the remove-visible workflow. No titles were removed.");
		var locate = CreateOwnerCommand(commands.LocateAudiobooksAsync, "locate audiobook files", "Libation could not open the audiobook locator. No files were changed.");
		var quality = CreateOwnerCommand(commands.ShowQualityScanAsync, "scan for better-quality audiobooks", "Libation could not open the quality scan. No library data was changed.");
		var export = CreateOwnerCommand(commands.ExportLibraryAsync, "export the library", "Libation could not open the library export workflow. No export was written.");
		var editFilters = CreateOwnerCommand(commands.EditQuickFiltersAsync, "edit quick filters", "Libation could not open the quick-filter editor. No filters were changed.");
		var addFilter = CreateOwnerCommand(commands.AddCurrentFilter, "save the current filter", "Libation could not save the current filter.");
		var toggleDefaultFilter = CreateOwnerCommand(main.ToggleFirstFilterIsDefault, "toggle the first quick filter at startup", "Libation could not change the startup quick-filter preference.");
		var filterHelp = CreateOwnerCommand(commands.OpenFilterHelp, "open filter syntax help", "Libation could not open filter syntax help.");
		var about = CreateOwnerCommand(main.ShowAboutAsync, "open About and update status", "Libation could not open About. Try the native application menu instead.");

		Groups =
		[
			new("Library maintenance", "Operate on the titles currently visible in the shared Library filter.",
			[
				new("Process visible titles", "Queues every visible title still awaiting an open local copy.", "Review scope", LibationStatusKind.NeedsAttention, processVisible),
				new("Detect downloaded status", "Inspects files for visible titles, previews proposed status changes, and asks before applying them.", "Review scope", LibationStatusKind.NeedsAttention, detectStatus),
				new("Set audiobook status manually", Resources.ToolsManualBookStatusDescription, "Review scope", LibationStatusKind.NeedsAttention, setBookStatus),
				new("Set PDF status manually", Resources.ToolsManualPdfStatusDescription, "Review scope", LibationStatusKind.NeedsAttention, setPdfStatus),
				new("Move visible titles to Trash", Resources.ToolsTrashDescription, "Destructive after confirmation", LibationStatusKind.NeedsAttention, removeVisible),
			]),
			new("File discovery", Resources.ToolsFileDiscoveryDescription,
			[
				new("Locate previously processed audiobooks", Resources.ToolsLocateDescription, "Low risk", LibationStatusKind.Completed, locate),
			]),
			new("Metadata", "Change metadata for the current visible-title scope.",
			[
				new("Replace tags for visible titles", "Opens tag input, shows the affected visible-title scope, and asks before replacing tags.", "Destructive after confirmation", LibationStatusKind.NeedsAttention, replaceTags),
			]),
			new("Quality", "Inspect the library for source-quality improvements without changing files immediately.",
			[
				new("Scan for better-quality audiobooks", Resources.ToolsQualityDescription, "Low risk", LibationStatusKind.Completed, quality),
			]),
			new("Import, export, and filters", Resources.ToolsImportExportDescription,
			[
				new("Export library", "Writes the current library catalogue to a user-selected XLSX, CSV, or JSON file.", "Writes selected file", LibationStatusKind.NeedsAttention, export),
				new("Edit quick filters", Resources.ToolsEditFiltersDescription, "Low risk", LibationStatusKind.Completed, editFilters),
				new("Save current filter", Resources.ToolsSaveFilterDescription, "Changes preferences", LibationStatusKind.NeedsAttention, addFilter),
				new("Toggle first quick filter at startup", "Changes whether Libation applies the first saved quick filter when the application starts.", "Changes preferences", LibationStatusKind.NeedsAttention, toggleDefaultFilter),
				new("Open filter syntax help", Resources.ToolsFilterHelpDescription, "No data change", LibationStatusKind.Completed, filterHelp),
			]),
			new("Diagnostics and legacy utilities", Resources.ToolsDiagnosticsDescription,
			[
				new("About, version, and updates", Resources.ToolsAboutDescription, "Network only on request", LibationStatusKind.Completed, about),
				new("Launch Hangover", Resources.ToolsHangoverDescription, "External application", LibationStatusKind.NeedsAttention, main.LaunchHangover),
			]),
		];
	}

	public IReadOnlyList<ToolActionGroup> Groups { get; }
	public string RouteEyebrow => "Advanced operations";
	public string RouteTitle => "Tools";
	public string RouteSubtitle => Resources.ToolsSupportingText;
	public RouteCommandPresentation? RoutePrimaryCommand => null;
	public IReadOnlyList<RouteCommandPresentation> RouteSecondaryCommands => [];
	public RouteStatusPresentation? RouteStatusBadge => null;
}
