using Avalonia.Input.Platform;
using LibationAvalonia.DesignSystem;
using LibationAvalonia.DesignSystem.Components;
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
	private bool disposed;

	internal CurrentFlightItemViewModel(FlightItemViewModel source, Action<FlightItemId> remove)
	{
		Source = source;
		removeCommand = ReactiveCommand.Create(() => remove(Source.Id));
		Source.PropertyChanged += Source_PropertyChanged;
	}

	public FlightItemViewModel Source { get; }
	public string Title => Source.Title;
	public string Author => Source.Author;
	public string DurationText => Source.DurationMinutes <= 0
		? "Duration unavailable"
		: $"{Source.DurationMinutes / 60} hr {Source.DurationMinutes % 60} min";
	public ReactiveCommand<Unit, Unit> RemoveCommand => removeCommand;

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
	private string processActionText = "Process";
	private string announcement = "Current Flight is empty.";
	private UserFacingError? currentError;
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
			new(FlightOutputProfile.CurrentSettings, "Current settings", "Use the saved Download/Decrypt settings."),
			new(FlightOutputProfile.M4b, "M4B", "Create one M4B file per title."),
			new(FlightOutputProfile.Mp3, "MP3", "Create MP3 output without changing saved settings."),
			new(FlightOutputProfile.SplitByChapter, "Split by chapter", "Create chapter-separated output without changing saved settings."),
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
	public string CountText => Count == 1 ? "1 title" : $"{Count} titles";
	public string DurationText => flight.TotalDurationMinutes <= 0
		? "Duration unavailable"
		: $"{flight.TotalDurationMinutes / 60} hr {flight.TotalDurationMinutes % 60} min total";
	public string EstimatedSizeText => Count == 0
		? "Estimated size unavailable"
		: $"Estimated {DiskSpaceHelper.FormatBytes(flight.EstimatedBytes)}";
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
	public string OutputProfileText => $"Output: {SelectedOutputProfile.Label}";
	public string? UndoActionText => undoToken?.CanRestore == true ? "Undo" : null;
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
			? $"{flight.HiddenCount} Current Flight title(s) are outside the current Library results."
			: "All Current Flight titles are visible in the current Library results.";
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
				itemMap.Add(item.Id, new CurrentFlightItemViewModel(item, Remove));
		}

		var ordered = flight.Items.Select(item => itemMap[item.Id]).ToArray();
		if (!Items.SequenceEqual(ordered))
		{
			Items.Clear();
			foreach (var item in ordered)
				Items.Add(item);
		}

		this.RaisePropertyChanged(nameof(Count));
		this.RaisePropertyChanged(nameof(CountText));
		this.RaisePropertyChanged(nameof(DurationText));
		this.RaisePropertyChanged(nameof(EstimatedSizeText));
		if (flight.HiddenCount > 0)
			WarningText = $"{flight.HiddenCount} selected title(s) are hidden by the current Library filter.";
	}

	private void Remove(FlightItemId id)
	{
		undoToken = flight.Remove(id);
		ShowUndo("Removed a title from Current Flight.");
	}

	private void Clear()
	{
		undoToken = flight.Clear();
		Notifications.Clear();
		this.RaisePropertyChanged(nameof(UndoActionText));
		if (undoToken.CanRestore)
			ShowUndo("Cleared Current Flight.");
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
		Notifications.Add(new ToastMessage(message, ToastKind.Undo, "Undo", UndoCommand));
		this.RaisePropertyChanged(nameof(UndoActionText));
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
			ShowExceptionError(ex, "inspect Current Flight preflight", "Libation could not inspect Current Flight preflight conditions.");
			return;
		}
		CurrentError = null;
		if (result.Issues.Count == 0)
		{
			FocusWarning = false;
			WarningText = null;
			Announcement = $"Preflight passed for {result.Books.Count} Current Flight title(s).";
			return;
		}

		FocusWarning = true;
		WarningText = string.Join(Environment.NewLine, result.Issues.Select(issue => issue.Message));
		Announcement = result.CanProceed
			? $"Preflight found warnings. {WarningText}"
			: $"Preflight blocked processing. {WarningText}";
	}

	private Task ExportMetadataAsync()
		=> RunActionAsync(actions.ExportMetadataAsync, "export Current Flight metadata");

	private Task AddTagsAsync()
		=> RunActionAsync(actions.AddTagsAsync, "add Current Flight tags");

	private Task ReplaceTagsAsync()
		=> RunActionAsync(actions.ReplaceTagsAsync, "replace Current Flight tags");

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
			ShowExceptionError(ex, actionName, $"Libation could not {actionName}. No completion was recorded.");
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
			ShowExceptionError(ex, "evaluate Current Flight processing", "Libation could not inspect Current Flight processing conditions.");
			return;
		}
		CurrentError = null;
		var blocking = result.Issues.Where(issue => issue.Severity == FlightPreflightSeverity.Blocking).ToArray();
		var warnings = result.Issues.Where(issue => issue.Severity == FlightPreflightSeverity.Warning).ToArray();
		FocusWarning = result.Issues.Count > 0;
		WarningText = string.Join(Environment.NewLine, result.Issues.Select(issue => issue.Message));

		if (blocking.Length > 0)
		{
			ProcessActionText = "Process";
			pendingWarningSignature = null;
			Announcement = $"Processing blocked. {WarningText}";
			return;
		}

		var signature = BuildSignature(result);
		if (warnings.Length > 0 && pendingWarningSignature != signature)
		{
			pendingWarningSignature = signature;
			ProcessActionText = "Process anyway";
			Announcement = $"Review processing warnings. {WarningText}";
			return;
		}

		FlightProcessResult processResult;
		try
		{
			processResult = await processor.ProcessAsync(result.Books, SelectedOutputProfile.Value);
		}
		catch (Exception ex)
		{
			ShowExceptionError(ex, "submit Current Flight to Processing", "Libation could not submit Current Flight to Processing.");
			return;
		}
		pendingWarningSignature = null;
		ProcessActionText = "Process";
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
				"Processing not started",
				processResult.Message,
				"Review Current Flight preflight and the Processing queue, then try again.",
				ErrorSeverity.Warning,
				canRetry: true,
				canOpenSettings: true,
				canRevealPath: false,
				technicalDetails: $"Operation: submit Current Flight to Processing{Environment.NewLine}Result: no eligible work was queued");
			FocusWarning = true;
			WarningText = CurrentError.PrimaryMessage;
			Serilog.Log.Logger.Warning(
				"Current Flight was not submitted to Processing. Correlation ID: {CorrelationId}. Category: {ErrorCategory}",
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
		ProcessActionText = "Process";
		FocusWarning = true;
		WarningText = CurrentError.PrimaryMessage;
		Announcement = WarningText;
		Notifications.Clear();
		Notifications.Add(new ToastMessage(WarningText, ToastKind.Warning));
		Serilog.Log.Logger.Error(
			exception,
			"Current Flight action failed: {CurrentFlightAction}. Correlation ID: {CorrelationId}. Category: {ErrorCategory}",
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
				"Unable to copy Current Flight diagnostics. Correlation ID: {CorrelationId}. {TechnicalDetails}",
				error.CorrelationId,
				UserFacingErrorFactory.Scrub(ex.ToString()));
		}
	}

	private string BuildSignature(FlightPreflightResult result)
		=> $"{SelectedOutputProfile.Value}:{string.Join("|", result.Books.Select(book => book.Book.AudibleProductId))}";

	private void ResetPendingPreflight()
	{
		pendingWarningSignature = null;
		ProcessActionText = "Process";
		FocusWarning = CurrentError is not null;
		WarningText = CurrentError?.PrimaryMessage
			?? (flight.HiddenCount > 0
				? $"{flight.HiddenCount} selected title(s) are hidden by the current Library filter."
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
