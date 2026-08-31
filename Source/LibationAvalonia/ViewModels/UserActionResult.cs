namespace LibationAvalonia.ViewModels;

/// <summary>
/// Outcome returned by an established UI command owner when a contemporary
/// surface needs to present completion without inferring it from message text.
/// </summary>
public enum UserActionOutcome
{
	Completed,
	Cancelled,
	NoChange,
}

public sealed record UserActionResult(UserActionOutcome Outcome, string Message);
