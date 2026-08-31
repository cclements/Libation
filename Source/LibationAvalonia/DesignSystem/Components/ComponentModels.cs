using System.Windows.Input;

namespace LibationAvalonia.DesignSystem.Components;

public enum ComponentSeverity
{
	Neutral,
	Info,
	Success,
	Warning,
	Danger,
}

public enum LibationStatusKind
{
	DownloadPending,
	Downloading,
	Downloaded,
	Processing,
	Completed,
	Connected,
	Failed,
	Cancelled,
	Unavailable,
	NeedsAttention,
}

public enum ToastKind
{
	Undo,
	Completed,
	Warning,
	Copied,
	Failure,
}

/// <summary>
/// Presentation-only toast data. The host owns message lifetime and any action;
/// ToastHost never mutates application or domain state.
/// </summary>
public sealed record ToastMessage(
	string Message,
	ToastKind Kind = ToastKind.Completed,
	string? ActionText = null,
	ICommand? ActionCommand = null,
	object? ActionCommandParameter = null)
{
	public LibationStatusKind Status => Kind switch
	{
		ToastKind.Warning => LibationStatusKind.NeedsAttention,
		ToastKind.Failure => LibationStatusKind.Failed,
		ToastKind.Undo => LibationStatusKind.Cancelled,
		_ => LibationStatusKind.Completed,
	};
}
