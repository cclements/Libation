using Microsoft.Win32.SafeHandles;
using Serilog;
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace FileManager;

public class MoveFileProgressEventArgs : EventArgs
{
	public long TotalFileSize { get; }
	public long TotalBytesTransferred { get; }
	public long BytesMoved { get; }
	public bool Continue { get; set; } = true;

	internal MoveFileProgressEventArgs(long bytesMoved, long totalBytesTransferred, long totalFileSize)
	{
		BytesMoved = bytesMoved;
		TotalBytesTransferred = totalBytesTransferred;
		TotalFileSize = totalFileSize;
	}
}

public class MoveWithProgress
{
	public event EventHandler<MoveFileProgressEventArgs>? MoveProgress;

	private readonly Func<FileSystemInfo?, string?> deviceId;
	public MoveWithProgress() : this(GetDeviceId) { }
	internal MoveWithProgress(Func<FileSystemInfo?, string?> deviceId) => this.deviceId = deviceId;

	public async Task<bool> MoveAsync(LongPath source, LongPath destination, bool overwrite = false, CancellationToken cancellation = default)
	{
		ArgumentException.ThrowIfNullOrEmpty(source, nameof(source));
		ArgumentException.ThrowIfNullOrEmpty(destination, nameof(destination));
		cancellation.ThrowIfCancellationRequested();
		var sourceFileInfo = new FileInfo(source);

		if (!sourceFileInfo.Exists)
			throw new FileNotFoundException($"Source file '{source}' does not exist.", source);

		var destinationFile = new FileInfo(destination);
		var sourceDevice = deviceId(sourceFileInfo);
		var destinationDevice = deviceId(destinationFile.Directory);

		if (string.Equals(sourceFileInfo.FullName, destinationFile.FullName,
			LongPath.IsWindows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
			|| (sourceDevice is not null && sourceDevice == destinationDevice))
		{
			cancellation.ThrowIfCancellationRequested();
			File.Move(sourceFileInfo.FullName, destinationFile.FullName, overwrite);
			MoveProgress?.Invoke(this, new MoveFileProgressEventArgs(destinationFile.Length, destinationFile.Length, sourceFileInfo.Length));
			return true;
		}

		// Unknown device identity must not turn a same-path move into copy/replace/
		// delete. Also reject ambiguous case-only aliases on the fallback path.
		if (string.Equals(sourceFileInfo.FullName, destinationFile.FullName, StringComparison.OrdinalIgnoreCase))
			throw new IOException("Source and destination must be distinct for a staged copy.");

		if (destinationFile.Exists && !overwrite)
			throw new IOException("The file exists.");

		// Copy beside the destination, then publish by rename on that filesystem.
		// A failed/cancelled copy must never modify or delete the previous book.
		var staged = new FileInfo(Path.Combine(destinationFile.DirectoryName!, $".libation-transfer-{Guid.NewGuid():N}.tmp"));
		try
		{
			if (!await CopyWithProgressAsync(sourceFileInfo, staged, cancellation))
				return false;
			cancellation.ThrowIfCancellationRequested();
			File.Move(staged.FullName, destinationFile.FullName, overwrite);
			FileUtility.SaferDelete(sourceFileInfo.FullName);
			return true;
		}
		finally
		{
			// Best effort cleanup cannot hide the copy/publication failure. Only our
			// unique staging path is eligible; the destination is never removed here.
			FileUtility.TrySaferDelete(staged.FullName);
		}
	}

	private static string? GetDeviceId(FileSystemInfo? fsEntry)
		=> fsEntry?.FullName is not string path ? null
		: LongPath.IsWindows ? GetDriveSerialNumber(path)
		: GetUnixDeviceId(path);

	private async Task<bool> CopyWithProgressAsync(FileInfo sourceFileInfo, FileInfo destinationFile, CancellationToken cancellation)
	{
		const int BlockSizeMb = 8;
		const int BlockSizeBytes = BlockSizeMb * (1 << 20);
		using FileStream sourceStream = sourceFileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
		var createOptions = new FileStreamOptions
		{
			Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None,
			Options = FileOptions.Asynchronous
		};
		// Preserve restricted key/archive modes on the copy path too. Creation mode
		// prevents an initial window with broader permissions on Unix.
		if (!OperatingSystem.IsWindows())
			createOptions.UnixCreateMode = File.GetUnixFileMode(sourceFileInfo.FullName);
		using FileStream destinationStream = new(destinationFile.FullName, createOptions);

		using IMemoryOwner<byte> pool = MemoryPool<byte>.Shared.Rent(2 * BlockSizeBytes);
		Memory<byte> readBuff = pool.Memory.Slice(0, BlockSizeBytes);
		Memory<byte> writeBuff = pool.Memory.Slice(BlockSizeBytes, BlockSizeBytes);

		long totalCopied = 0, bytesMovedSinceLastReport = 0;
		DateTime nextReport = default;
		int bytesRead = await sourceStream.ReadAsync(writeBuff, cancellation);
		while (bytesRead > 0)
		{
			totalCopied += bytesRead;
			bytesMovedSinceLastReport += bytesRead;

			var readTask = sourceStream.ReadAsync(readBuff, cancellation);
			try { await destinationStream.WriteAsync(writeBuff[..bytesRead], cancellation); }
			catch
			{
				// The read owns pooled memory until it finishes, even if writing failed.
				try { await readTask; } catch { }
				throw;
			}
			int nextBytesRead = await readTask;

			if (DateTime.UtcNow >= nextReport)
			{
				var args = new MoveFileProgressEventArgs(bytesMovedSinceLastReport, totalCopied, sourceFileInfo.Length);
				bytesMovedSinceLastReport = 0;
				MoveProgress?.Invoke(this, args);
				if (!args.Continue)
					return false;
				nextReport = DateTime.UtcNow.AddMilliseconds(200.0);
			}
			bytesRead = nextBytesRead;
			(readBuff, writeBuff) = (writeBuff, readBuff);
		}

		destinationStream.SetLength(totalCopied);
		await destinationStream.FlushAsync(cancellation);
		destinationStream.Flush(flushToDisk: true);
		var finalProgress = new MoveFileProgressEventArgs(bytesMovedSinceLastReport, totalCopied, sourceFileInfo.Length);
		MoveProgress?.Invoke(this, finalProgress);
		return finalProgress.Continue && totalCopied == sourceFileInfo.Length;
	}

	private static string? GetUnixDeviceId(string path)
	{
		var psi = new ProcessStartInfo
		{
			FileName = "/usr/bin/stat",
			RedirectStandardOutput = true, RedirectStandardError = true,
			UseShellExecute = false, CreateNoWindow = true
		};
		psi.ArgumentList.Add("-L");
		psi.ArgumentList.Add(LongPath.IsOSX ? "-f" : "-c");
		psi.ArgumentList.Add("%d");
		psi.ArgumentList.Add(path);
		try
		{
			using var proc = Process.Start(psi);
			if (proc is null) return null;
			var output = proc.StandardOutput.ReadToEndAsync();
			var error = proc.StandardError.ReadToEndAsync();
			if (!proc.WaitForExit(5000))
			{
				proc.Kill(entireProcessTree: true);
				proc.WaitForExit(1000);
				return null;
			}
			error.GetAwaiter().GetResult();
			string value = output.GetAwaiter().GetResult().Trim();
			return proc.ExitCode == 0 && !string.IsNullOrEmpty(value) ? value : null;
		}
		catch (Exception e)
		{
			Log.Logger.Warning(e, "Could not determine filesystem device; using staged file copy.");
			return null;
		}
	}

	private static string? GetDriveSerialNumber(string path)
	{
		const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
		const uint OPEN_EXISTING = 3;
		var handle = CreateFile(path, FileAccess.Read, FileShare.Read, 0, OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS, 0);
		if (handle.IsInvalid)
			return null;

		try
		{
			BY_HANDLE_FILE_INFORMATION info = default;
			if (!GetFileInformationByHandle(handle, ref info))
			{
				return null;
			}
			return info.dwVolumeSerialNumber.ToString("x8");
		}
		finally
		{
			handle.Close();
		}
	}

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetFileInformationByHandle(SafeFileHandle hFile, ref BY_HANDLE_FILE_INFORMATION lpFileInformation);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern SafeFileHandle CreateFile(string fileName, FileAccess fileAccess, FileShare fileShare, nint lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, nint hTemplateFile);

	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	private struct BY_HANDLE_FILE_INFORMATION
	{
		private readonly uint dwFileAttributes;
		private readonly long ftCreationTime;
		private readonly long ftLastAccessTime;
		private readonly long ftLastWriteTime;
		public uint dwVolumeSerialNumber;
		private readonly uint nFileSizeHigh;
		private readonly uint nFileSizeLow;
		private readonly uint nNumberOfLinks;
		private readonly uint nFileIndexHigh;
		private readonly uint nFileIndexLow;
	}
}