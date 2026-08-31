using LibationAvalonia.DesignSystem.Components;
using System;

namespace LibationAvalonia.Features.History;

public sealed record HistoryItem(
	DateTime Timestamp,
	string DateText,
	string Action,
	string Title,
	string Detail,
	string Result,
	LibationStatusKind Status);

internal sealed record HistoryBookRaw(
	string Title,
	DateTime DateAdded,
	DateTime? LastDownloaded);

internal sealed record HistoryLogRaw(DateTime Timestamp, string Message);
