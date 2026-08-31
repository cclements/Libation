using Avalonia.Input.Platform;
using LibationAvalonia.DesignSystem;
using LibationAvalonia.ViewModels;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace LibationAvalonia.Features.Tools;

/// <summary>
/// Presentation-only command host for contemporary secondary destinations. Owner
/// methods remain responsible for dialogs, confirmations, and domain mutations.
/// </summary>
public abstract class SecondaryDestinationViewModel : ViewModelBase, IDisposable
{
	private readonly SemaphoreSlim actionGate = new(1, 1);
	private readonly List<IDisposable> commandDisposables = [];
	private bool disposed;
	private UserFacingError? currentError;

	protected SecondaryDestinationViewModel()
	{
		CopyTechnicalDetailsCommand = Track(ReactiveCommand.CreateFromTask(CopyTechnicalDetailsAsync));
	}

	public UserFacingError? CurrentError
	{
		get => currentError;
		protected set
		{
			this.RaiseAndSetIfChanged(ref currentError, value);
			foreach (var property in new[]
			{
				nameof(ErrorMessage), nameof(ErrorTitle), nameof(ErrorRecommendedAction), nameof(ErrorCorrelationId),
				nameof(ErrorSeverity), nameof(CanRetry), nameof(CanOpenSettings), nameof(CanRevealPath),
				nameof(TechnicalDetails), nameof(HasError), nameof(CanCopyTechnicalDetails),
			})
				this.RaisePropertyChanged(property);
		}
	}

	public string? ErrorMessage => CurrentError?.PrimaryMessage;
	public string? ErrorTitle => CurrentError?.Title;
	public string? ErrorRecommendedAction => CurrentError?.RecommendedAction;
	public string? ErrorCorrelationId => CurrentError?.CorrelationId;
	public ErrorSeverity? ErrorSeverity => CurrentError?.Severity;
	public bool CanRetry => CurrentError?.CanRetry == true;
	public bool CanOpenSettings => CurrentError?.CanOpenSettings == true;
	public bool CanRevealPath => CurrentError?.CanRevealPath == true;
	public string? TechnicalDetails => CurrentError?.TechnicalDetails;
	public bool HasError => CurrentError is not null;
	public bool CanCopyTechnicalDetails => HasError && LibationAvalonia.App.MainWindow?.Clipboard is not null;
	public bool IsActionRunning { get => field; private set => this.RaiseAndSetIfChanged(ref field, value); }
	public ICommand CopyTechnicalDetailsCommand { get; }

	protected ICommand CreateOwnerCommand(Func<Task> action, string operation, string userError)
		=> Track(ReactiveCommand.CreateFromTask(() => RunOwnerActionAsync(action, operation, userError)));

	protected ICommand CreateOwnerCommand(Action action, string operation, string userError)
		=> CreateOwnerCommand(() =>
		{
			action();
			return Task.CompletedTask;
		}, operation, userError);

	protected T Track<T>(T command) where T : class, ICommand
	{
		if (command is IDisposable disposable)
			commandDisposables.Add(disposable);
		return command;
	}

	protected async Task RunOwnerActionAsync(Func<Task> action, string operation, string userError)
	{
		if (disposed)
			return;
		if (!await actionGate.WaitAsync(0))
		{
			var busyError = UserFacingErrorFactory.FromException(
				new InvalidOperationException("The destination action gate is already held."),
				operation,
				"Another action on this page is still running. Wait for it to finish, then try again.");
			CurrentError = busyError;
			Serilog.Log.Logger.Warning(
				"Contemporary destination action was already running: {SecondaryOperation}. Correlation ID: {CorrelationId}. Category: {ErrorCategory}",
				operation,
				busyError.CorrelationId,
				busyError.Category.ToDisplayName());
			return;
		}
		try
		{
			IsActionRunning = true;
			await action();
			CurrentError = null;
		}
		catch (Exception ex)
		{
			var error = UserFacingErrorFactory.FromException(ex, operation, userError);
			CurrentError = error;
			Serilog.Log.Logger.Error(
				ex,
				"Contemporary destination action failed: {SecondaryOperation}. Correlation ID: {CorrelationId}. Category: {ErrorCategory}",
				operation,
				error.CorrelationId,
				error.Category.ToDisplayName());
		}
		finally
		{
			IsActionRunning = false;
			actionGate.Release();
		}
	}

	private async Task CopyTechnicalDetailsAsync()
	{
		var error = CurrentError;
		var clipboard = LibationAvalonia.App.MainWindow?.Clipboard;
		if (error is null || clipboard is null)
			return;

		try
		{
			await clipboard.SetTextAsync(error.ToDiagnosticText());
		}
		catch (Exception ex)
		{
			Serilog.Log.Logger.Warning(
				"Unable to copy contemporary error diagnostics. Correlation ID: {CorrelationId}. Exception: {ExceptionType}. {TechnicalDetails}",
				error.CorrelationId,
				ex.GetType().FullName,
				UserFacingErrorFactory.Scrub(ex.ToString()));
		}
	}

	protected virtual void DisposeCore() { }

	public void Dispose()
	{
		if (disposed)
			return;
		disposed = true;
		DisposeCore();
		foreach (var command in commandDisposables)
			command.Dispose();
		commandDisposables.Clear();
		// An established owner action may still be completing while the shell closes.
		// SemaphoreSlim holds no native handle unless AvailableWaitHandle is requested;
		// retaining it avoids releasing a disposed gate from that action's finally block.
	}
}
