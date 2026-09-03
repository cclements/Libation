using Avalonia.Threading;
using LibationAvalonia.DesignSystem.Components;
using LibationAvalonia.Properties;
using LibationAvalonia.Shell;
using LibationAvalonia.ViewModels;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Input;

namespace LibationAvalonia.Features.Tools;

public enum ToolRiskKind
{
	ReadOnly,
	NeedsReview,
	ChangesData,
	Destructive,
	External,
}

public sealed class ToolActionItem : ViewModelBase
{
	public ToolActionItem(
		string name,
		string consequence,
		string riskText,
		ToolRiskKind risk,
		string actionText,
		ICommand command,
		string? stateText = null)
	{
		Name = name;
		Consequence = consequence;
		RiskText = riskText;
		Risk = risk;
		ActionText = actionText;
		Command = command;
		StateText = stateText;
	}

	public string Name { get; }
	public string Consequence { get; }
	public string RiskText { get; }
	public ToolRiskKind Risk { get; }
	public bool IsReadOnlyRisk => Risk == ToolRiskKind.ReadOnly;
	public bool IsReviewRisk => Risk == ToolRiskKind.NeedsReview;
	public bool IsDataChangeRisk => Risk == ToolRiskKind.ChangesData;
	public bool IsDestructiveRisk => Risk == ToolRiskKind.Destructive;
	public bool IsExternalRisk => Risk == ToolRiskKind.External;
	public string ActionText { get; }
	public ICommand Command { get; }
	public string? StateText { get => field; private set => this.RaiseAndSetIfChanged(ref field, value); }

	internal void UpdateState(string? stateText) => StateText = stateText;
}

public sealed record ToolActionGroup(string Name, string Description, IReadOnlyList<ToolActionItem> Actions);

/// <summary>
/// Discoverable index over existing advanced commands. Each action delegates to its
/// established owner, retaining existing dialogs and confirmations.
/// </summary>
public sealed class ToolsViewModel : SecondaryDestinationViewModel, IRoutePresentation
{
	private readonly MainVM main;
	private readonly ToolActionItem aboutItem;
	private bool disposed;

	public ToolsViewModel(ILibationCommandAdapter commands)
	{
		ArgumentNullException.ThrowIfNull(commands);
		main = commands.Main;

		var processVisible = CreateOwnerCommand(commands.ProcessVisibleConfirmedAsync, "process visible titles", "Libation could not queue the visible titles. Review the current Library filter and try again.");
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
		var filterHelp = CreateOwnerCommand(commands.OpenFilterHelp, "open filter syntax help", "Libation could not open filter syntax help.");
		var about = CreateOwnerCommand(main.ShowAboutAsync, "open About and update status", "Libation could not open About. Try the native application menu instead.");

		var processVisibleItem = new ToolActionItem(
			"Process visible titles",
			"Reviews the current filtered scope, then queues every visible title still awaiting an open local copy only after you confirm.",
			"Confirmation required",
			ToolRiskKind.NeedsReview,
			"Review and process",
			processVisible,
			"The confirmation counts the exact eligible scope, including titles that need only a PDF.");
		aboutItem = new(
			"About, version, and updates",
			Resources.ToolsAboutDescription,
			"Network only on request",
			ToolRiskKind.External,
			"Open About",
			about,
			main.ApplicationUpdateState);

		Groups =
		[
			new("Library maintenance", "Operate on the titles currently visible in the shared Library filter.",
			[
				processVisibleItem,
				new("Detect downloaded status", "Inspects files for visible titles, previews proposed status changes, and asks before applying them.", "Changes status after review", ToolRiskKind.NeedsReview, "Review detected status", detectStatus),
				new("Set audiobook status manually", Resources.ToolsManualBookStatusDescription, "Changes status after confirmation", ToolRiskKind.NeedsReview, "Set audiobook status", setBookStatus),
				new("Set PDF status manually", Resources.ToolsManualPdfStatusDescription, "Changes status after confirmation", ToolRiskKind.NeedsReview, "Set PDF status", setPdfStatus),
				new("Move visible titles to Trash", Resources.ToolsTrashDescription, "Moves records after confirmation", ToolRiskKind.Destructive, "Review and move", removeVisible),
			]),
			new("File discovery", Resources.ToolsFileDiscoveryDescription,
			[
				new("Locate previously processed audiobooks", Resources.ToolsLocateDescription, "Records matches and updates title status", ToolRiskKind.ChangesData, "Choose folders", locate),
			]),
			new("Metadata", "Change metadata for the current visible-title scope.",
			[
				new("Replace tags for visible titles", "Opens tag input, shows the affected visible-title scope, and asks before replacing tags.", "Replaces metadata after confirmation", ToolRiskKind.Destructive, "Review and replace", replaceTags),
			]),
			new("Quality", "Inspect the library for source-quality improvements without changing files immediately.",
			[
				new("Scan for better-quality audiobooks", Resources.ToolsQualityDescription, "Read-only scan", ToolRiskKind.ReadOnly, "Start quality scan", quality),
			]),
			new("Import, export, and filters", Resources.ToolsImportExportDescription,
			[
				new("Export library", "Writes the current library catalogue to a user-selected XLSX, CSV, or JSON file.", "Writes selected file", ToolRiskKind.ChangesData, "Choose export", export),
				new("Edit quick filters", Resources.ToolsEditFiltersDescription, "Changes saved filters", ToolRiskKind.ChangesData, "Edit filters", editFilters),
				new("Save current filter", Resources.ToolsSaveFilterDescription, "Changes saved filters", ToolRiskKind.ChangesData, "Save filter", addFilter),
				new("Open filter syntax help", Resources.ToolsFilterHelpDescription, "No data change", ToolRiskKind.ReadOnly, "Open help", filterHelp),
			]),
			new("Diagnostics and legacy utilities", Resources.ToolsDiagnosticsDescription,
			[
				aboutItem,
				new("Launch Hangover", Resources.ToolsHangoverDescription, "Opens external application", ToolRiskKind.External, "Launch Hangover", main.LaunchHangover),
			]),
		];

		main.PropertyChanged += Main_PropertyChanged;
	}

	public IReadOnlyList<ToolActionGroup> Groups { get; }
	public bool FirstFilterIsDefault
	{
		get => main.FirstFilterIsDefault;
		set
		{
			if (value == main.FirstFilterIsDefault)
				return;
			try
			{
				main.ToggleFirstFilterIsDefault();
				CurrentError = null;
			}
			catch (Exception ex)
			{
				CurrentError = LibationAvalonia.DesignSystem.UserFacingErrorFactory.FromException(
					ex,
					"change the startup quick-filter preference",
					"Libation could not change the startup quick-filter preference. Your previous setting remains in effect.");
				Serilog.Log.Logger.Error(
					ex,
					"Unable to change startup quick-filter preference. Correlation ID: {CorrelationId}.",
					CurrentError.CorrelationId);
			}
			RaiseStartupFilterState();
		}
	}
	public string StartupFilterStateText => FirstFilterIsDefault
		? "On — the first saved quick filter is applied when Libation starts."
		: "Off — Libation starts without automatically applying the first saved quick filter.";
	public LibationStatusKind StartupFilterStatus => FirstFilterIsDefault
		? LibationStatusKind.Connected
		: LibationStatusKind.Completed;
	public string StartupFilterBadgeText => FirstFilterIsDefault ? "On" : "Off";
	public string RouteEyebrow => "Advanced operations";
	public string RouteTitle => "Tools";
	public string RouteSubtitle => Resources.ToolsSupportingText;
	public RouteCommandPresentation? RoutePrimaryCommand => null;
	public IReadOnlyList<RouteCommandPresentation> RouteSecondaryCommands => [];
	public RouteStatusPresentation? RouteStatusBadge => null;

	private void Main_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (disposed)
			return;
		if (!Dispatcher.UIThread.CheckAccess())
		{
			Dispatcher.UIThread.Post(() => Main_PropertyChanged(sender, e));
			return;
		}
		if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName is nameof(MainVM.FirstFilterIsDefault))
			RaiseStartupFilterState();
		if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName is nameof(MainVM.ApplicationUpdateState))
			aboutItem.UpdateState(main.ApplicationUpdateState);
	}

	private void RaiseStartupFilterState()
	{
		this.RaisePropertyChanged(nameof(FirstFilterIsDefault));
		this.RaisePropertyChanged(nameof(StartupFilterStateText));
		this.RaisePropertyChanged(nameof(StartupFilterStatus));
		this.RaisePropertyChanged(nameof(StartupFilterBadgeText));
	}

	protected override void DisposeCore()
	{
		disposed = true;
		main.PropertyChanged -= Main_PropertyChanged;
	}
}
