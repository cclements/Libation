using Avalonia.Input.Platform;
using LibationAvalonia.DesignSystem;
using LibationAvalonia.DesignSystem.Components;
using LibationAvalonia.Features.Library;
using LibationAvalonia.ViewModels;
using LibationFileManager;
using LibationUiBase.ProcessQueue;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;

namespace LibationAvalonia.Features.Flight;

public sealed record FlightOutputProfileOption(FlightOutputProfile Value, string Label, string Description);

public sealed class CurrentFlightItemViewModel : ReactiveObject, IDisposable
{
	private readonly ReactiveCommand<Unit, Unit> removeCommand;
	private readonly ReactiveCommand<Unit, Unit> moveUpCommand;
	private readonly ReactiveCommand<Unit, Unit> moveDownCommand;
	private CoverImageCache? coverCache;
	private bool disposed;

	internal CurrentFlightItemViewModel(
		FlightItemViewModel source,
		Action<FlightItemId> remove,
		Action<FlightItemId, int> moveBy,
		CoverImageCache? coverCache)
	{
		Source = source;
		this.coverCache = coverCache;
		removeCommand = ReactiveCommand.Create(() => remove(Source.Id));
		moveUpCommand = ReactiveCommand.Create(() => moveBy(Source.Id, -1));
		moveDownCommand = ReactiveCommand.Create(() => moveBy(Source.Id, 1));
		Source.PropertyChanged += Source_PropertyChanged;
	}

	public FlightItemViewModel Source { get; }
	public string Title => Source.Title;
	public string Author => Source.Author;
	public string DurationText => Source.DurationMinutes <= 0
		? string.Empty
		: string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.CurrentFlightViewModel0Hr1Min, Source.DurationMinutes / 60, Source.DurationMinutes % 60);
	public CoverImageCache? CoverCache
	{
		get => coverCache;
		internal set => this.RaiseAndSetIfChanged(ref coverCache, value);
	}
	public ReactiveCommand<Unit, Unit> RemoveCommand => removeCommand;
	public ReactiveCommand<Unit, Unit> MoveUpCommand => moveUpCommand;
	public ReactiveCommand<Unit, Unit> MoveDownCommand => moveDownCommand;

	private void Source_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(FlightItemViewModel.Title))
			this.RaisePropertyChanged(nameof(Title));
		if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(FlightItemViewModel.Author))
			this.RaisePropertyChanged(nameof(Author));
		if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(FlightItemViewModel.DurationMinutes))
			this.RaisePropertyChanged(nameof(DurationText));
	}

	public void Dispose()
	{
		if (disposed)
			return;
		disposed = true;
		Source.PropertyChanged -= Source_PropertyChanged;
		removeCommand.Dispose();
		moveUpCommand.Dispose();
		moveDownCommand.Dispose();
	}
}

/// <summary>
/// Presentation and explicit-intent layer for the single shell-scoped
/// <see cref="IFlightService"/> selection.
/// </summary>
public sealed class CurrentFlightViewModel : ReactiveObject, IDisposable
{
	private readonly IFlightService flight;
	private readonly Configuration configuration;
	private readonly ProcessQueueViewModel queue;
	private readonly IFlightProcessAdapter processor;
	private readonly IFlightActionAdapter actions;
	private readonly INotifyPropertyChanged? observableFlight;
	private readonly Dictionary<FlightItemId, CurrentFlightItemViewModel> itemMap = new();
	private FlightOutputProfileOption selectedOutputProfile;
	private FlightUndoToken? undoToken;
	private string? warningText;
	private string? pendingWarningSignature;
	private string processActionText = global::LibationAvalonia.Properties.Resources.BookDetailsPaneProcess;
	private string announcement = global::LibationAvalonia.Properties.Resources.CurrentFlightViewModelCurrentFlightIsEmpty;
	private UserFacingError? currentError;
	private CoverImageCache? coverCache;
	private bool focusWarning;
	private bool disposed;

	public CurrentFlightViewModel(
		IFlightService flight,
		Configuration configuration,
		ProcessQueueViewModel queue,
		IFlightProcessAdapter processor,
		IFlightActionAdapter actions)
	{
		this.flight = flight;
		this.configuration = configuration;
		this.queue = queue;
		this.processor = processor;
		this.actions = actions;
		OutputProfiles =
		[
			new(FlightOutputProfile.CurrentSettings, global::LibationAvalonia.Properties.Resources.CurrentFlightViewModelCurrentSettings, global::LibationAvalonia.Properties.Resources.CurrentFlightViewModelUseTheSavedDownloadDecryptSettings),
			new(FlightOutputProfile.M4b, global::LibationAvalonia.Properties.Resources.CurrentFlightViewModelM4B, global::LibationAvalonia.Properties.Resources.CurrentFlightViewModelCreateOneM4BFilePerTitle),
			new(FlightOutputProfile.Mp3, global::LibationAvalonia.Properties.Resources.CurrentFlightViewModelMP3, global::LibationAvalonia.Properties.Resources.CurrentFlightViewModelCreateMP3OutputWithoutChangingSavedSettings),
			new(FlightOutputProfile.SplitByChapter, global::LibationAvalonia.Properties.Resources.CurrentFlightViewModelSplitByChapter, global::LibationAvalonia.Properties.Resources.CurrentFlightViewModelCreateChapterSeparatedOutputWithoutChangingSaved),
		];
		selectedOutputProfile = OutputProfiles[0];
		RemoveCommand = ReactiveCommand.Create<CurrentFlightItemViewModel>(item => Remove(item.Source.Id));
		ClearCommand = ReactiveCommand.Create(Clear);
		UndoCommand = ReactiveCommand.Create(Undo);
		InspectPreflightCommand = ReactiveCommand.Create(InspectPreflight);
		ExportMetadataCommand = ReactiveCommand.CreateFromTask(ExportMetadataAsync);
		AddTagsCommand = ReactiveCommand.CreateFromTask(AddTagsAsync);
		ReplaceTagsCommand = ReactiveCommand.CreateFromTask(ReplaceTagsAsync);
		CopyTechnicalDetailsCommand = ReactiveCommand.CreateFromTask(CopyTechnicalDetailsAsync);
		ProcessCommand = ReactiveCommand.CreateFromTask(ProcessAsync);
		flight.SelectionChanged += Flight_SelectionChanged;
		observableFlight = flight as INotifyPropertyChanged;
		if (observableFlight is not null)
			observableFlight.PropertyChanged += Flight_PropertyChanged;
		RefreshItems();
	}

	public ObservableCollection<CurrentFlightItemViewModel> Items { get; } = new();
	public ObservableCollection<ToastMessage> Notifications { get; } = new();
	public IReadOnlyList<FlightOutputProfileOption> OutputProfiles { get; }
	public ReactiveCommand<CurrentFlightItemViewModel, Unit> RemoveCommand { get; }
	public ReactiveCommand<Unit, Unit> ClearCommand { get; }
	public ReactiveCommand<Unit, Unit> UndoCommand { get; }
	public ReactiveCommand<Unit, Unit> InspectPreflightCommand { get; }
	public ReactiveCommand<Unit, Unit> ExportMetadataCommand { get; }
	public ReactiveCommand<Unit, Unit> AddTagsCommand { get; }
	public ReactiveCommand<Unit, Unit> ReplaceTagsCommand { get; }
	public ReactiveCommand<Unit, Unit> CopyTechnicalDetailsCommand { get; }
	public ReactiveCommand<Unit, Unit> ProcessCommand { get; }
	public int Count => flight.Count;
	public bool IsEmpty => Count == 0;
	public string CountText => Count == 1 ? global::LibationAvalonia.Properties.Resources.CurrentFlightViewModel1Title : string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.CurrentFlightViewModel0Titles, Count);
	public string EmptyStateTitle => IsEmpty ? global::LibationAvalonia.Properties.Resources.CurrentFlightViewModelCurrentFlightIsEmpty2 : string.Empty;
	public string EmptyStateText => IsEmpty ? global::LibationAvalonia.Properties.Resources.CurrentFlightViewModelSelectTitlesInTheLibraryToBuild : string.Empty;
	public string DurationText => flight.TotalDurationMinutes <= 0
		? string.Empty
		: string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.CurrentFlightViewModel0Hr1MinTotal, flight.TotalDurationMinutes / 60, flight.TotalDurationMinutes % 60);
	public string EstimatedSizeText => flight.EstimatedBytes <= 0
		? string.Empty
		: string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.CurrentFlightViewModelAbout0, DiskSpaceHelper.FormatBytes(flight.EstimatedBytes));
	public CoverImageCache? CoverCache
	{
		get => coverCache;
		set
		{
			if (ReferenceEquals(coverCache, value))
				return;
			this.RaiseAndSetIfChanged(ref coverCache, value);
			foreach (var item in Items)
				item.CoverCache = value;
		}
	}
	public string? WarningText
	{
		get => warningText;
		private set => this.RaiseAndSetIfChanged(ref warningText, value);
	}
	public bool FocusWarning
	{
		get => focusWarning;
		private set => this.RaiseAndSetIfChanged(ref focusWarning, value);
	}
	public string ProcessActionText
	{
		get => processActionText;
		private set => this.RaiseAndSetIfChanged(ref processActionText, value);
	}
	public string Announcement
	{
		get => announcement;
		private set => this.RaiseAndSetIfChanged(ref announcement, value);
	}
	public FlightOutputProfileOption SelectedOutputProfile
	{
		get => selectedOutputProfile;
		set
		{
			if (value is null || Equals(selectedOutputProfile, value))
				return;
			this.RaiseAndSetIfChanged(ref selectedOutputProfile, value);
			ResetPendingPreflight();
			this.RaisePropertyChanged(nameof(OutputProfileText));
		}
	}
	public string OutputProfileText => string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.CurrentFlightViewModelOutput0, SelectedOutputProfile.Label);
	public string? UndoActionText => undoToken?.CanRestore == true ? global::LibationAvalonia.Properties.Resources.CurrentFlightViewModelUndo : null;
	public UserFacingError? CurrentError
	{
		get => currentError;
		private set
		{
			this.RaiseAndSetIfChanged(ref currentError, value);
			this.RaisePropertyChanged(nameof(HasError));
		}
	}
	public bool HasError => CurrentError is not null;

	private void Flight_SelectionChanged(object? sender, FlightChangedEventArgs e)
	{
		Announcement = e.Announcement;
		ResetPendingPreflight();
		RefreshItems();
	}

	private void Flight_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(IFlightService.HiddenCount))
			return;

		ResetPendingPreflight();
		Announcement = flight.HiddenCount > 0
			? string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.CurrentFlightViewModel0CurrentFlightTitleSAreOutside, flight.HiddenCount)
			: global::LibationAvalonia.Properties.Resources.CurrentFlightViewModelAllCurrentFlightTitlesAreVisibleIn;
	}

	private void RefreshItems()
	{
		var liveIds = flight.Items.Select(item => item.Id).ToHashSet();
		foreach (var removed in itemMap.Keys.Where(id => !liveIds.Contains(id)).ToArray())
		{
			itemMap[removed].Dispose();
			itemMap.Remove(removed);
		}
		foreach (var item in flight.Items)
		{
			if (!itemMap.ContainsKey(item.Id))
				itemMap.Add(item.Id, new CurrentFlightItemViewModel(item, Remove, MoveBy, CoverCache));
		}

		var ordered = flight.Items.Select(item => itemMap[item.Id]).ToArray();
		var orderedSet = ordered.ToHashSet();
		for (int index = Items.Count - 1; index >= 0; index--)
			if (!orderedSet.Contains(Items[index]))
				Items.RemoveAt(index);
		for (int index = 0; index < ordered.Length; index++)
		{
			var desired = ordered[index];
			if (index < Items.Count && ReferenceEquals(Items[index], desired))
				continue;
			int currentIndex = Items.IndexOf(desired);
			if (currentIndex >= 0)
				Items.Move(currentIndex, index);
			else
				Items.Insert(index, desired);
		}

		this.RaisePropertyChanged(nameof(Count));
		this.RaisePropertyChanged(nameof(IsEmpty));
		this.RaisePropertyChanged(nameof(CountText));
		this.RaisePropertyChanged(nameof(EmptyStateTitle));
		this.RaisePropertyChanged(nameof(EmptyStateText));
		this.RaisePropertyChanged(nameof(DurationText));
		this.RaisePropertyChanged(nameof(EstimatedSizeText));
		if (flight.HiddenCount > 0)
			WarningText = string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.CurrentFlightViewModel0SelectedTitleSAreHiddenBy, flight.HiddenCount);
	}

	private void Remove(FlightItemId id)
	{
		undoToken = flight.Remove(id);
		ShowUndo(global::LibationAvalonia.Properties.Resources.CurrentFlightViewModelRemovedATitleFromCurrentFlight);
	}

	private void MoveBy(FlightItemId id, int offset)
	{
		int currentIndex = flight.Items.ToList().FindIndex(item => item.Id == id);
		if (currentIndex >= 0)
			flight.Move(id, currentIndex + offset);
	}

	public void MoveTo(FlightItemId id, FlightItemId targetId)
	{
		int targetIndex = flight.Items.ToList().FindIndex(item => item.Id == targetId);
		if (targetIndex >= 0)
			flight.Move(id, targetIndex);
	}

	private void Clear()
	{
		undoToken = flight.Clear();
		Notifications.Clear();
		this.RaisePropertyChanged(nameof(UndoActionText));
		if (undoToken.CanRestore)
			ShowUndo(global::LibationAvalonia.Properties.Resources.CurrentFlightViewModelClearedCurrentFlight);
	}

	private void Undo()
	{
		if (undoToken is null || !flight.Restore(undoToken))
			return;
		undoToken = null;
		Notifications.Clear();
		this.RaisePropertyChanged(nameof(UndoActionText));
	}

	private void ShowUndo(string message)
	{
		Notifications.Clear();
		Notifications.Add(new ToastMessage(message, ToastKind.Undo, global::LibationAvalonia.Properties.Resources.CurrentFlightViewModelUndo, UndoCommand));
		this.RaisePropertyChanged(nameof(UndoActionText));
	}

	internal void ReportNotice(string message)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(message);
		Announcement = message;
		Notifications.Clear();
		Notifications.Add(new ToastMessage(message, ToastKind.Warning));
	}

	private void InspectPreflight()
	{
		FlightPreflightResult result;
		try
		{
			result = EvaluatePreflight();
		}
		catch (Exception ex)
		{
			ShowExceptionError(ex, global::LibationAvalonia.Properties.Resources.CurrentFlightViewModelInspectCurrentFlightPreflight, global::LibationAvalonia.Properties.Resources.CurrentFlightViewModelLibationCouldNotInspectCurrentFlightPreflight);
			return;
		}
		CurrentError = null;
		if (result.Issues.Count == 0)
		{
			FocusWarning = false;
			WarningText = null;
			Announcement = string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.CurrentFlightViewModelPreflightPassedFor0CurrentFlightTitle, result.Books.Count);
			return;
		}

		FocusWarning = true;
		WarningText = string.Join(Environment.NewLine, result.Issues.Select(issue => issue.Message));
		Announcement = result.CanProceed
			? string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.CurrentFlightViewModelPreflightFoundWarnings0, WarningText)
			: string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.CurrentFlightViewModelPreflightBlockedProcessing0, WarningText);
	}

	private Task ExportMetadataAsync()
		=> RunActionAsync(actions.ExportMetadataAsync, global::LibationAvalonia.Properties.Resources.CurrentFlightViewModelExportCurrentFlightMetadata);

	private Task AddTagsAsync()
		=> RunActionAsync(actions.AddTagsAsync, global::LibationAvalonia.Properties.Resources.CurrentFlightViewModelAddCurrentFlightTags);

	private Task ReplaceTagsAsync()
		=> RunActionAsync(actions.ReplaceTagsAsync, global::LibationAvalonia.Properties.Resources.CurrentFlightViewModelReplaceCurrentFlightTags);

	private async Task RunActionAsync(
		Func<IReadOnlyList<DataLayer.LibraryBook>, Task<UserActionResult>> action,
		string actionName)
	{
		var books = flight.Items.Select(item => item.LibraryBook).ToArray();
		UserActionResult result;
		try
		{
			result = await action(books);
		}
		catch (Exception ex)
		{
			ShowExceptionError(ex, actionName, string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.CurrentFlightViewModelLibationCouldNot0NoCompletionWas, actionName));
			return;
		}

		Announcement = result.Message;
		Notifications.Clear();
		if (result.Outcome == UserActionOutcome.Completed)
		{
			CurrentError = null;
			ResetPendingPreflight();
			Notifications.Add(new ToastMessage(result.Message, ToastKind.Completed));
		}
		else
		{
			// Cancellation and no-change are visible outcomes, not successful work.
			// Preserve any persistent prior error until an owner action succeeds.
			Notifications.Add(new ToastMessage(result.Message, ToastKind.Warning));
		}
	}

	private FlightPreflightResult EvaluatePreflight()
		=> FlightPreflight.Evaluate(flight.Items, configuration, queue, SelectedOutputProfile.Value);

	private async Task ProcessAsync()
	{
		FlightPreflightResult result;
		try
		{
			result = EvaluatePreflight();
		}
		catch (Exception ex)
		{
			ShowExceptionError(ex, global::LibationAvalonia.Properties.Resources.CurrentFlightViewModelEvaluateCurrentFlightProcessing, global::LibationAvalonia.Properties.Resources.CurrentFlightViewModelLibationCouldNotInspectCurrentFlightProcessing);
			return;
		}
		CurrentError = null;
		var blocking = result.Issues.Where(issue => issue.Severity == FlightPreflightSeverity.Blocking).ToArray();
		var warnings = result.Issues.Where(issue => issue.Severity == FlightPreflightSeverity.Warning).ToArray();
		FocusWarning = result.Issues.Count > 0;
		WarningText = string.Join(Environment.NewLine, result.Issues.Select(issue => issue.Message));

		if (blocking.Length > 0)
		{
			ProcessActionText = global::LibationAvalonia.Properties.Resources.BookDetailsPaneProcess;
			pendingWarningSignature = null;
			Announcement = string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.CurrentFlightViewModelProcessingBlocked0, WarningText);
			return;
		}

		var signature = BuildSignature(result);
		if (warnings.Length > 0 && pendingWarningSignature != signature)
		{
			pendingWarningSignature = signature;
			ProcessActionText = global::LibationAvalonia.Properties.Resources.CurrentFlightViewModelProcessAnyway;
			Announcement = string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.CurrentFlightViewModelReviewProcessingWarnings0, WarningText);
			return;
		}

		FlightProcessResult processResult;
		try
		{
			processResult = await processor.ProcessAsync(result.Books, SelectedOutputProfile.Value);
		}
		catch (Exception ex)
		{
			ShowExceptionError(ex, global::LibationAvalonia.Properties.Resources.CurrentFlightViewModelSubmitCurrentFlightToProcessing, global::LibationAvalonia.Properties.Resources.CurrentFlightViewModelLibationCouldNotSubmitCurrentFlightTo);
			return;
		}
		pendingWarningSignature = null;
		ProcessActionText = global::LibationAvalonia.Properties.Resources.BookDetailsPaneProcess;
		if (processResult.Queued)
		{
			CurrentError = null;
			FocusWarning = false;
			WarningText = null;
		}
		else
		{
			CurrentError = UserFacingErrorFactory.FromMessage(
				UserFacingErrorCategory.Conversion,
				global::LibationAvalonia.Properties.Resources.CurrentFlightViewModelProcessingNotStarted,
				processResult.Message,
				global::LibationAvalonia.Properties.Resources.CurrentFlightViewModelReviewCurrentFlightPreflightAndTheProcessing,
				ErrorSeverity.Warning,
				canRetry: true,
				canOpenSettings: true,
				canRevealPath: false,
				technicalDetails: string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.CurrentFlightViewModelOperationSubmitCurrentFlightToProcessing0, Environment.NewLine));
			FocusWarning = true;
			WarningText = CurrentError.PrimaryMessage;
			Serilog.Log.Logger.Warning(
				global::LibationAvalonia.Properties.Resources.CurrentFlightViewModelCurrentFlightWasNotSubmittedToProcessing,
				CurrentError.CorrelationId,
				CurrentError.Category.ToDisplayName());
		}
		Announcement = processResult.Message;
		Notifications.Clear();
		Notifications.Add(new ToastMessage(
			processResult.Queued ? Announcement : CurrentError!.PrimaryMessage,
			processResult.Queued ? ToastKind.Completed : ToastKind.Warning));
	}

	private void ShowExceptionError(Exception exception, string operation, string summary)
	{
		CurrentError = UserFacingErrorFactory.FromException(exception, operation, summary);
		pendingWarningSignature = null;
		ProcessActionText = global::LibationAvalonia.Properties.Resources.BookDetailsPaneProcess;
		FocusWarning = true;
		WarningText = CurrentError.PrimaryMessage;
		Announcement = WarningText;
		Notifications.Clear();
		Notifications.Add(new ToastMessage(WarningText, ToastKind.Warning));
		Serilog.Log.Logger.Error(
			exception,
			global::LibationAvalonia.Properties.Resources.CurrentFlightViewModelCurrentFlightActionFailedCurrentFlightActionCorrelationID,
			operation,
			CurrentError.CorrelationId,
			CurrentError.Category.ToDisplayName());
	}

	private async Task CopyTechnicalDetailsAsync()
	{
		var error = CurrentError;
		if (error is null || App.MainWindow?.Clipboard is not { } clipboard)
			return;
		try
		{
			await clipboard.SetTextAsync(error.ToDiagnosticText());
		}
		catch (Exception ex)
		{
			Serilog.Log.Logger.Warning(
				global::LibationAvalonia.Properties.Resources.CurrentFlightViewModelUnableToCopyCurrentFlightDiagnosticsCorrelation,
				error.CorrelationId,
				UserFacingErrorFactory.Scrub(ex.ToString()));
		}
	}

	private string BuildSignature(FlightPreflightResult result)
		=> $"{SelectedOutputProfile.Value}:{string.Join("|", result.Books.Select(book => book.Book.AudibleProductId))}";

	private void ResetPendingPreflight()
	{
		pendingWarningSignature = null;
		ProcessActionText = global::LibationAvalonia.Properties.Resources.BookDetailsPaneProcess;
		FocusWarning = CurrentError is not null;
		WarningText = CurrentError?.PrimaryMessage
			?? (flight.HiddenCount > 0
				? string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.CurrentFlightViewModel0SelectedTitleSAreHiddenBy, flight.HiddenCount)
				: null);
	}

	public void Dispose()
	{
		if (disposed)
			return;
		disposed = true;
		flight.SelectionChanged -= Flight_SelectionChanged;
		if (observableFlight is not null)
			observableFlight.PropertyChanged -= Flight_PropertyChanged;
		foreach (var item in itemMap.Values)
			item.Dispose();
		itemMap.Clear();
		RemoveCommand.Dispose();
		ClearCommand.Dispose();
		UndoCommand.Dispose();
		InspectPreflightCommand.Dispose();
		ExportMetadataCommand.Dispose();
		AddTagsCommand.Dispose();
		ReplaceTagsCommand.Dispose();
		CopyTechnicalDetailsCommand.Dispose();
		ProcessCommand.Dispose();
	}
}
