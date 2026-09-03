using ApplicationServices;
using LibationAvalonia.ViewModels;
using LibationFileManager;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace LibationAvalonia.Features.Overview;

/// <summary>
/// Supplies only host facts with a reliable current owner: network availability,
/// scan completion observed in this process, locally measured audiobook bytes,
/// and the established updater's current status. A stale-scan threshold and
/// estimated storage savings remain unknown until product policy defines them.
/// </summary>
public sealed class MainDashboardSupplementSource : IDashboardSupplementSource, IDisposable
{
	private readonly MainVM main;
	private readonly object storageSync = new();
	private LibraryCommands.LibraryStats? measuredStats;
	private long? measuredBytes;
	private bool disposed;

	public MainDashboardSupplementSource(MainVM main)
	{
		this.main = main ?? throw new ArgumentNullException(nameof(main));
		main.PropertyChanged += Main_PropertyChanged;
		main.ProcessQueue.ProcessEnd += ProcessQueue_ProcessEnd;
		LibraryCommands.LibrarySizeChanged += LibraryCommands_LibrarySizeChanged;
		NetworkChange.NetworkAvailabilityChanged += NetworkChange_NetworkAvailabilityChanged;
	}

	public event EventHandler? Invalidated;

	public async Task<DashboardSupplement> LoadAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(disposed, this);
		var stats = main.LibraryStats;
		long? localBytes = await GetLocalStorageBytesAsync(stats, cancellationToken);
		return new()
		{
			Connectivity = NetworkInterface.GetIsNetworkAvailable()
				? DashboardConnectivityState.Online
				: DashboardConnectivityState.Offline,
			ScanFreshness = main.LastSuccessfulScan.HasValue
				? DashboardScanFreshness.Current
				: DashboardScanFreshness.Unknown,
			LastSuccessfulScan = main.LastSuccessfulScan,
			TotalLocalStorageBytes = localBytes,
			ApplicationUpdateState = main.ApplicationUpdateState,
		};
	}

	private Task<long?> GetLocalStorageBytesAsync(
		LibraryCommands.LibraryStats? stats,
		CancellationToken cancellationToken)
	{
		if (stats is null)
			return Task.FromResult<long?>(null);
		lock (storageSync)
			if (ReferenceEquals(measuredStats, stats) && measuredBytes.HasValue)
				return Task.FromResult(measuredBytes);

		var library = stats.LibraryBooks.ToArray();
		return Task.Run<long?>(() =>
		{
			long total = 0;
			try
			{
				foreach (var book in library)
				{
					cancellationToken.ThrowIfCancellationRequested();
					var path = AudibleFileStorage.Audio.GetPath(book.Book.AudibleProductId);
					if (path is null)
						continue;
					var file = new FileInfo(path.Path);
					if (file.Exists)
						total = checked(total + file.Length);
				}
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex)
			{
				Serilog.Log.Logger.Warning(ex, global::LibationAvalonia.Properties.Resources.MainDashboardSupplementSourceUnableToMeasureLocalAudiobookStorageFor);
				return null;
			}

			lock (storageSync)
			{
				measuredStats = stats;
				measuredBytes = total;
			}
			return total;
		}, cancellationToken);
	}

	private void Main_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName is nameof(MainVM.LastSuccessfulScan) or nameof(MainVM.ApplicationUpdateState))
			OnInvalidated();
	}

	private void ProcessQueue_ProcessEnd(object? sender, LibationUiBase.ProcessQueue.ProcessBookViewModel e)
	{
		InvalidateStorageMeasurement();
		OnInvalidated();
	}

	private void LibraryCommands_LibrarySizeChanged(object? sender, System.Collections.Generic.List<DataLayer.LibraryBook> e)
	{
		InvalidateStorageMeasurement();
		OnInvalidated();
	}

	private void NetworkChange_NetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
		=> OnInvalidated();

	private void InvalidateStorageMeasurement()
	{
		lock (storageSync)
		{
			measuredStats = null;
			measuredBytes = null;
		}
	}

	private void OnInvalidated()
	{
		if (!disposed)
			Invalidated?.Invoke(this, EventArgs.Empty);
	}

	public void Dispose()
	{
		if (disposed)
			return;
		disposed = true;
		main.PropertyChanged -= Main_PropertyChanged;
		main.ProcessQueue.ProcessEnd -= ProcessQueue_ProcessEnd;
		LibraryCommands.LibrarySizeChanged -= LibraryCommands_LibrarySizeChanged;
		NetworkChange.NetworkAvailabilityChanged -= NetworkChange_NetworkAvailabilityChanged;
	}
}
