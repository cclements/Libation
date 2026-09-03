using DataLayer;
using LibationAvalonia.DesignSystem.Components;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace LibationAvalonia.Features.Overview;

public enum DashboardConnectivityState
{
	Unknown,
	Online,
	Offline,
}

public enum DashboardScanFreshness
{
	Unknown,
	Current,
	Stale,
}

/// <summary>
/// Optional facts which do not currently have one authoritative owner on <c>MainVM</c>.
/// A host may supply them without teaching either overview profile how to query storage,
/// connectivity, scan-age policy, or application updates.
/// </summary>
public sealed record DashboardSupplement
{
	public static DashboardSupplement Unknown { get; } = new();

	public DashboardConnectivityState Connectivity { get; init; } = DashboardConnectivityState.Unknown;
	public DashboardScanFreshness ScanFreshness { get; init; } = DashboardScanFreshness.Unknown;
	public DateTimeOffset? LastSuccessfulScan { get; init; }
	public long? TotalLocalStorageBytes { get; init; }
	public long? StorageSavedBytes { get; init; }
	public string? ApplicationUpdateState { get; init; }
	public string? ErrorMessage { get; init; }
}

/// <summary>
/// Host-owned asynchronous seam for dashboard facts that MainVM does not expose. The
/// provider, not the overview, owns definitions such as when a scan becomes stale.
/// </summary>
public interface IDashboardSupplementSource
{
	event EventHandler? Invalidated;
	Task<DashboardSupplement> LoadAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Navigation remains a shell concern. This adapter keeps overview rows and metric cards
/// literal and functional without importing route or window policy into the feature.
/// </summary>
public interface IDashboardNavigation
{
	Task OpenBookAsync(LibraryBook book);
	Task OpenLibraryAsync();
	Task OpenProcessingAsync();
}

public sealed record DashboardBookItem(
	LibraryBook LibraryBook,
	string ProductId,
	string Title,
	string SupportingText,
	string Author,
	string Narrator,
	string DurationText,
	string AddedText,
	string Metadata,
	LibationStatusKind Status,
	string StatusText)
{
	public ICommand? OpenCommand { get; init; }
	public double ProcessingProgress { get; init; }
	public bool ShowProcessingProgress { get; init; }
	public string? ProcessingStatusText { get; init; }
}

/// <summary>
/// Immutable result of one dashboard aggregation pass. Both profile views bind to the
/// same instance of <see cref="DashboardViewModel"/>, which replaces this snapshot atomically.
/// </summary>
public sealed record DashboardSnapshot
{
	public static DashboardSnapshot Loading { get; } = new();

	public bool IsDataReady { get; init; }
	public int TotalTitles { get; init; }
	public int VisibleTitles { get; init; }
	public int DownloadPendingCount { get; init; }
	public int CompletedCount { get; init; }
	public int FailedJobCount { get; init; }
	public int AddedThisWeekCount { get; init; }
	public int ActiveDownloadCount { get; init; }
	public int AccountCount { get; init; }
	public bool IsScanning { get; init; }
	public string ScanProgressText { get; init; } = string.Empty;
	public string SearchText { get; init; } = string.Empty;
	public IReadOnlyList<DashboardBookItem> RecentAdditions { get; init; } = [];
	public IReadOnlyList<DashboardBookItem> RecentCompletions { get; init; } = [];
	public IReadOnlyList<DashboardBookItem> CurrentFlight { get; init; } = [];
	public DashboardSupplement Supplement { get; init; } = DashboardSupplement.Unknown;
}

public interface IDashboardDataSource : IDisposable
{
	event EventHandler? Invalidated;
	Task<DashboardSnapshot> LoadAsync(CancellationToken cancellationToken);
}
