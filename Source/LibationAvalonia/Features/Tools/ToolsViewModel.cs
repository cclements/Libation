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

		var processVisible = CreateOwnerCommand(commands.ProcessVisibleConfirmedAsync, global::LibationAvalonia.Properties.Resources.ToolsViewModelProcessVisibleTitles, global::LibationAvalonia.Properties.Resources.ToolsViewModelLibationCouldNotQueueTheVisibleTitles);
		var replaceTags = CreateOwnerCommand(commands.ReplaceVisibleTagsAsync, global::LibationAvalonia.Properties.Resources.ToolsViewModelReplaceVisibleTitleTags, global::LibationAvalonia.Properties.Resources.ToolsViewModelLibationCouldNotOpenTheTagReplacement);
		var setBookStatus = CreateOwnerCommand(commands.SetVisibleBookStatusAsync, global::LibationAvalonia.Properties.Resources.ToolsViewModelSetVisibleAudiobookStatus, global::LibationAvalonia.Properties.Resources.ToolsViewModelLibationCouldNotOpenTheAudiobookStatus);
		var setPdfStatus = CreateOwnerCommand(commands.SetVisiblePdfStatusAsync, global::LibationAvalonia.Properties.Resources.ToolsViewModelSetVisiblePDFStatus, global::LibationAvalonia.Properties.Resources.ToolsViewModelLibationCouldNotOpenThePDFStatus);
		var detectStatus = CreateOwnerCommand(commands.DetectVisibleStatusAsync, global::LibationAvalonia.Properties.Resources.ToolsViewModelDetectVisibleDownloadStatus, global::LibationAvalonia.Properties.Resources.ToolsViewModelLibationCouldNotInspectVisibleTitleFiles);
		var removeVisible = CreateOwnerCommand(commands.RemoveVisibleAsync, global::LibationAvalonia.Properties.Resources.ToolsViewModelRemoveVisibleTitles, global::LibationAvalonia.Properties.Resources.ToolsViewModelLibationCouldNotOpenTheRemoveVisible);
		var locate = CreateOwnerCommand(commands.LocateAudiobooksAsync, global::LibationAvalonia.Properties.Resources.ToolsViewModelLocateAudiobookFiles, global::LibationAvalonia.Properties.Resources.ToolsViewModelLibationCouldNotOpenTheAudiobookLocator);
		var quality = CreateOwnerCommand(commands.ShowQualityScanAsync, global::LibationAvalonia.Properties.Resources.ToolsViewModelScanForBetterQualityAudiobooks, global::LibationAvalonia.Properties.Resources.ToolsViewModelLibationCouldNotOpenTheQualityScan);
		var export = CreateOwnerCommand(commands.ExportLibraryAsync, global::LibationAvalonia.Properties.Resources.ToolsViewModelExportTheLibrary, global::LibationAvalonia.Properties.Resources.ToolsViewModelLibationCouldNotOpenTheLibraryExport);
		var editFilters = CreateOwnerCommand(commands.EditQuickFiltersAsync, global::LibationAvalonia.Properties.Resources.ToolsViewModelEditQuickFilters, global::LibationAvalonia.Properties.Resources.ToolsViewModelLibationCouldNotOpenTheQuickFilter);
		var addFilter = CreateOwnerCommand(commands.AddCurrentFilter, global::LibationAvalonia.Properties.Resources.ToolsViewModelSaveTheCurrentFilter, global::LibationAvalonia.Properties.Resources.ToolsViewModelLibationCouldNotSaveTheCurrentFilter);
		var filterHelp = CreateOwnerCommand(commands.OpenFilterHelp, global::LibationAvalonia.Properties.Resources.ToolsViewModelOpenFilterSyntaxHelp, global::LibationAvalonia.Properties.Resources.ToolsViewModelLibationCouldNotOpenFilterSyntaxHelp);
		var about = CreateOwnerCommand(main.ShowAboutAsync, global::LibationAvalonia.Properties.Resources.SettingsViewModelOpenAboutAndUpdateStatus, global::LibationAvalonia.Properties.Resources.SettingsViewModelLibationCouldNotOpenAboutTryThe);

		var processVisibleItem = new ToolActionItem(
			global::LibationAvalonia.Properties.Resources.ToolsViewModelProcessVisibleTitles2,
			global::LibationAvalonia.Properties.Resources.ToolsViewModelReviewsTheCurrentFilteredScopeThenQueues,
			global::LibationAvalonia.Properties.Resources.ToolsViewModelConfirmationRequired,
			ToolRiskKind.NeedsReview,
			global::LibationAvalonia.Properties.Resources.ToolsViewModelReviewAndProcess,
			processVisible,
			global::LibationAvalonia.Properties.Resources.ToolsViewModelTheConfirmationCountsTheExactEligibleScope);
		aboutItem = new(
			global::LibationAvalonia.Properties.Resources.ToolsViewModelAboutVersionAndUpdates,
			Resources.ToolsAboutDescription,
			global::LibationAvalonia.Properties.Resources.ToolsViewModelNetworkOnlyOnRequest,
			ToolRiskKind.External,
			global::LibationAvalonia.Properties.Resources.ShellOpenAboutCommandLabel,
			about,
			main.ApplicationUpdateState);

		Groups =
		[
			new(global::LibationAvalonia.Properties.Resources.ToolsViewModelLibraryMaintenance, global::LibationAvalonia.Properties.Resources.ToolsViewModelOperateOnTheTitlesCurrentlyVisibleIn,
			[
				processVisibleItem,
				new(global::LibationAvalonia.Properties.Resources.ToolsViewModelDetectDownloadedStatus, global::LibationAvalonia.Properties.Resources.ToolsViewModelInspectsFilesForVisibleTitlesPreviewsProposed, global::LibationAvalonia.Properties.Resources.ToolsViewModelChangesStatusAfterReview, ToolRiskKind.NeedsReview, global::LibationAvalonia.Properties.Resources.ToolsViewModelReviewDetectedStatus, detectStatus),
				new(global::LibationAvalonia.Properties.Resources.ToolsViewModelSetAudiobookStatusManually, Resources.ToolsManualBookStatusDescription, global::LibationAvalonia.Properties.Resources.ToolsViewModelChangesStatusAfterConfirmation, ToolRiskKind.NeedsReview, global::LibationAvalonia.Properties.Resources.ToolsViewModelSetAudiobookStatus, setBookStatus),
				new(global::LibationAvalonia.Properties.Resources.ToolsViewModelSetPDFStatusManually, Resources.ToolsManualPdfStatusDescription, global::LibationAvalonia.Properties.Resources.ToolsViewModelChangesStatusAfterConfirmation, ToolRiskKind.NeedsReview, global::LibationAvalonia.Properties.Resources.ToolsViewModelSetPDFStatus, setPdfStatus),
				new(global::LibationAvalonia.Properties.Resources.ToolsViewModelMoveVisibleTitlesToTrash, Resources.ToolsTrashDescription, global::LibationAvalonia.Properties.Resources.ToolsViewModelMovesRecordsAfterConfirmation, ToolRiskKind.Destructive, global::LibationAvalonia.Properties.Resources.ToolsViewModelReviewAndMove, removeVisible),
			]),
			new(global::LibationAvalonia.Properties.Resources.ToolsViewModelFileDiscovery, Resources.ToolsFileDiscoveryDescription,
			[
				new(global::LibationAvalonia.Properties.Resources.ToolsViewModelLocatePreviouslyProcessedAudiobooks, Resources.ToolsLocateDescription, global::LibationAvalonia.Properties.Resources.ToolsViewModelRecordsMatchesAndUpdatesTitleStatus, ToolRiskKind.ChangesData, global::LibationAvalonia.Properties.Resources.ToolsViewModelChooseFolders, locate),
			]),
			new(global::LibationAvalonia.Properties.Resources.BookDetailsPaneMetadata, global::LibationAvalonia.Properties.Resources.ToolsViewModelChangeMetadataForTheCurrentVisibleTitle,
			[
				new(global::LibationAvalonia.Properties.Resources.ToolsViewModelReplaceTagsForVisibleTitles, global::LibationAvalonia.Properties.Resources.ToolsViewModelOpensTagInputShowsTheAffectedVisible, global::LibationAvalonia.Properties.Resources.ToolsViewModelReplacesMetadataAfterConfirmation, ToolRiskKind.Destructive, global::LibationAvalonia.Properties.Resources.ToolsViewModelReviewAndReplace, replaceTags),
			]),
			new(global::LibationAvalonia.Properties.Resources.ToolsViewModelQuality, global::LibationAvalonia.Properties.Resources.ToolsViewModelInspectTheLibraryForSourceQualityImprovements,
			[
				new(global::LibationAvalonia.Properties.Resources.ToolsViewModelScanForBetterQualityAudiobooks2, Resources.ToolsQualityDescription, global::LibationAvalonia.Properties.Resources.ToolsViewModelReadOnlyScan, ToolRiskKind.ReadOnly, global::LibationAvalonia.Properties.Resources.ToolsViewModelStartQualityScan, quality),
			]),
			new(global::LibationAvalonia.Properties.Resources.ToolsViewModelImportExportAndFilters, Resources.ToolsImportExportDescription,
			[
				new(global::LibationAvalonia.Properties.Resources.ToolsViewModelExportLibrary, global::LibationAvalonia.Properties.Resources.ToolsViewModelWritesTheCurrentLibraryCatalogueToA, global::LibationAvalonia.Properties.Resources.ToolsViewModelWritesSelectedFile, ToolRiskKind.ChangesData, global::LibationAvalonia.Properties.Resources.ToolsViewModelChooseExport, export),
				new(global::LibationAvalonia.Properties.Resources.ToolsViewModelEditQuickFilters2, Resources.ToolsEditFiltersDescription, global::LibationAvalonia.Properties.Resources.ToolsViewModelChangesSavedFilters, ToolRiskKind.ChangesData, global::LibationAvalonia.Properties.Resources.ToolsViewModelEditFilters, editFilters),
				new(global::LibationAvalonia.Properties.Resources.ToolsViewModelSaveCurrentFilter, Resources.ToolsSaveFilterDescription, global::LibationAvalonia.Properties.Resources.ToolsViewModelChangesSavedFilters, ToolRiskKind.ChangesData, global::LibationAvalonia.Properties.Resources.ToolsViewModelSaveFilter, addFilter),
				new(global::LibationAvalonia.Properties.Resources.ToolsViewModelOpenFilterSyntaxHelp2, Resources.ToolsFilterHelpDescription, global::LibationAvalonia.Properties.Resources.ToolsViewModelNoDataChange, ToolRiskKind.ReadOnly, global::LibationAvalonia.Properties.Resources.ToolsViewModelOpenHelp, filterHelp),
			]),
			new(global::LibationAvalonia.Properties.Resources.ToolsViewModelDiagnosticsAndLegacyUtilities, Resources.ToolsDiagnosticsDescription,
			[
				aboutItem,
				new(global::LibationAvalonia.Properties.Resources.ToolsViewModelLaunchHangover, Resources.ToolsHangoverDescription, global::LibationAvalonia.Properties.Resources.ToolsViewModelOpensExternalApplication, ToolRiskKind.External, global::LibationAvalonia.Properties.Resources.ToolsViewModelLaunchHangover, main.LaunchHangover),
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
					global::LibationAvalonia.Properties.Resources.ToolsViewModelChangeTheStartupQuickFilterPreference,
					global::LibationAvalonia.Properties.Resources.ToolsViewModelLibationCouldNotChangeTheStartupQuick);
				Serilog.Log.Logger.Error(
					ex,
					global::LibationAvalonia.Properties.Resources.ToolsViewModelUnableToChangeStartupQuickFilterPreference,
					CurrentError.CorrelationId);
			}
			RaiseStartupFilterState();
		}
	}
	public string StartupFilterStateText => FirstFilterIsDefault
		? global::LibationAvalonia.Properties.Resources.ToolsViewModelOnTheFirstSavedQuickFilterIs
		: global::LibationAvalonia.Properties.Resources.ToolsViewModelOffLibationStartsWithoutAutomaticallyApplyingThe;
	public LibationStatusKind StartupFilterStatus => FirstFilterIsDefault
		? LibationStatusKind.Connected
		: LibationStatusKind.Completed;
	public string StartupFilterBadgeText => FirstFilterIsDefault ? global::LibationAvalonia.Properties.Resources.ToolsViewModelOn : global::LibationAvalonia.Properties.Resources.ToolsViewModelOff;
	public string RouteEyebrow => global::LibationAvalonia.Properties.Resources.ToolsViewModelAdvancedOperations;
	public string RouteTitle => global::LibationAvalonia.Properties.Resources.RouteToolsLabel;
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
