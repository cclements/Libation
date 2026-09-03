using LibationAvalonia.DesignSystem.Components;
using System;

namespace LibationAvalonia.Features.History;

public sealed record HistoryItem(
	DateTime Timestamp,
	Guid? CorrelationId,
	string DateText,
	string Action,
	string Title,
	string Detail,
	string Result,
	LibationStatusKind Status)
{
	public string AccessibleName => $"{DateText}, {Action}, {Title}, {Result}";
	public string StatusAccessibleName => $"{Title}: {Result}";
}

internal sealed record HistoryBookRaw(
	string Title,
	DateTime DateAdded,
	DateTime? LastDownloaded);

internal sealed record HistoryLogRaw(DateTime Timestamp, string Message);
