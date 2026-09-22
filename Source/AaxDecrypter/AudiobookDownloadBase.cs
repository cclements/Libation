using Dinah.Core;
using Dinah.Core.Net.Http;
using Dinah.Core.StepRunner;
using FileManager;
using System;
using System.IO;
using System.Threading.Tasks;

namespace AaxDecrypter;

public enum OutputFormat { M4b, Mp3 }

public abstract class AudiobookDownloadBase
{
	public event EventHandler<string?>? RetrievedTitle;
	public event EventHandler<string?>? RetrievedAuthors;
	public event EventHandler<string?>? RetrievedNarrators;
	public event EventHandler<byte[]?>? RetrievedCoverArt;
	public event EventHandler<DownloadProgress>? DecryptProgressUpdate;
	public event EventHandler<TimeSpan>? DecryptTimeRemaining;
	public event EventHandler<TempFile>? TempFileCreated;

	public bool IsCanceled { get; protected set; }
	protected AsyncStepSequence AsyncSteps { get; } = new();
	protected string OutputDirectory { get; }
	public IDownloadOptions DownloadOptions { get; }
	protected NetworkFileStream InputFileStream => NfsPersister.NetworkFileStream;
	protected virtual long InputFilePosition
	{
		get
		{
			//Use try/catch instread of checking CanRead to avoid
			//a race with the background download completing
			//between the check and the Position call.
			try { return InputFileStream.Position; }
			catch { return InputFileStream.Length; }
		}
	}
	private bool downloadFinished;

	private NetworkFileStreamPersister? m_nfsPersister;
	private NetworkFileStreamPersister NfsPersister => m_nfsPersister ??= OpenNetworkFileStream();
	private readonly DownloadProgress zeroProgress;
	private readonly string jsonDownloadState;
	private readonly string tempFilePath;

	protected AudiobookDownloadBase(string outDirectory, string cacheDirectory, IDownloadOptions dlOptions)
	{
		OutputDirectory = ArgumentValidator.EnsureNotNullOrWhiteSpace(outDirectory, nameof(outDirectory));
		DownloadOptions = ArgumentValidator.EnsureNotNull(dlOptions, nameof(dlOptions));
		DownloadOptions.DownloadSpeedChanged += (_, speed) => InputFileStream.SpeedLimit = speed;

		if (!Directory.Exists(OutputDirectory))
			Directory.CreateDirectory(OutputDirectory);

		if (!Directory.Exists(cacheDirectory))
			Directory.CreateDirectory(cacheDirectory);

		jsonDownloadState = Path.Combine(cacheDirectory, $"{DownloadOptions.AudibleProductId}.json");
		tempFilePath = Path.ChangeExtension(jsonDownloadState, ".aaxc");

		zeroProgress = new DownloadProgress
		{
			BytesReceived = 0,
			ProgressPercentage = 0,
			TotalBytesToReceive = 0
		};

		OnDecryptProgressUpdate(zeroProgress);
	}

	protected TempFile GetNewTempFilePath(string extension)
	{
		extension = FileUtility.GetStandardizedExtension(extension);
		var path = Path.Combine(OutputDirectory, Guid.NewGuid().ToString("N") + extension);
		return new(path, extension);
	}

	public async Task<bool> RunAsync()
	{
		Task progressTask = Task.CompletedTask;
		try
		{
			await InputFileStream.BeginDownloadingAsync();
			progressTask = Task.Run(reportProgress);
			(bool success, var elapsed) = await AsyncSteps.RunAsync();
			if (!success)
				NfsPersister.Dispose();
			await progressTask;
			var speedup = DownloadOptions.RuntimeLength / elapsed;
			Serilog.Log.Information($"Speedup is {speedup:F0}x realtime.");
			return success;
		}
		finally
		{
			// Includes first-request/admission failures and exceptions before a conversion can finalize.
			downloadFinished = true;
			m_nfsPersister?.Dispose();
			try { await progressTask; }
			catch (Exception ex) { Serilog.Log.Warning(ex, "Download progress reporting stopped with an error."); }
		}

		async Task reportProgress()
		{
			AverageSpeed averageSpeed = new();

			while (
				InputFileStream.CanRead
				&& InputFileStream.Length > InputFilePosition
				&& !InputFileStream.IsCancelled
				&& !downloadFinished)
			{
				averageSpeed.AddPosition(InputFilePosition);

				var estSecsRemaining = (InputFileStream.Length - InputFilePosition) / averageSpeed.Average;

				if (double.IsNormal(estSecsRemaining))
					OnDecryptTimeRemaining(TimeSpan.FromSeconds(estSecsRemaining));

				var progressPercent = 100d * InputFilePosition / InputFileStream.Length;

				OnDecryptProgressUpdate(
					new DownloadProgress
					{
						ProgressPercentage = progressPercent,
						BytesReceived = InputFilePosition,
						TotalBytesToReceive = InputFileStream.Length
					});

				await Task.Delay(200);
			}

			OnDecryptTimeRemaining(TimeSpan.Zero);
			OnDecryptProgressUpdate(zeroProgress);
		}
	}

	public virtual Task CancelAsync()
	{
		IsCanceled = true;
		FinalizeDownload();
		return Task.CompletedTask;
	}
	protected abstract Task<bool> Step_DownloadAndDecryptAudiobookAsync();

	public virtual void SetCoverArt(byte[] coverArt) { }
	protected void OnRetrievedTitle(string? title)
		=> RetrievedTitle?.Invoke(this, title);
	protected void OnRetrievedAuthors(string? authors)
		=> RetrievedAuthors?.Invoke(this, authors);
	protected void OnRetrievedNarrators(string? narrators)
		=> RetrievedNarrators?.Invoke(this, narrators);
	protected void OnRetrievedCoverArt(byte[]? coverArt)
		=> RetrievedCoverArt?.Invoke(this, coverArt);
	protected void OnDecryptProgressUpdate(DownloadProgress downloadProgress)
		=> DecryptProgressUpdate?.Invoke(this, downloadProgress);
	protected void OnDecryptTimeRemaining(TimeSpan timeRemaining)
		=> DecryptTimeRemaining?.Invoke(this, timeRemaining);
	public void OnTempFileCreated(TempFile path)
		=> TempFileCreated?.Invoke(this, path);

	protected virtual void FinalizeDownload()
	{
		NfsPersister.Dispose();
		downloadFinished = true;
	}

	protected async Task<bool> Step_CreateCueAsync()
	{
		if (!DownloadOptions.CreateCueSheet) return !IsCanceled;

		if (DownloadOptions.ChapterInfo.Count <= 1)
		{
			Serilog.Log.Logger.Information($"Skipped creating .cue because book has no chapters.");
			return !IsCanceled;
		}

		// not a critical step. its failure should not prevent future steps from running
		try
		{
			var tempFile = GetNewTempFilePath(".cue");
			await File.WriteAllTextAsync(tempFile.FilePath, Cue.CreateContents(Path.GetFileName(tempFile.FilePath), DownloadOptions.ChapterInfo));
			OnTempFileCreated(tempFile);
		}
		catch (Exception ex)
		{
			Serilog.Log.Logger.Error(ex, $"{nameof(Step_CreateCueAsync)} Failed");
		}
		return !IsCanceled;
	}

	private NetworkFileStreamPersister OpenNetworkFileStream()
	{
		NetworkFileStreamPersister? nfsp = null;
		try
		{
			if (File.Exists(jsonDownloadState))
			{
				nfsp = new NetworkFileStreamPersister(jsonDownloadState);
				// Signed query parameters may expire. Begin validates the renewed selection
				// before any cached bytes are combined with a response.
				nfsp.NetworkFileStream.SetUriForSameFile(new Uri(DownloadOptions.DownloadUrl), DownloadOptions.DownloadIdentity);
			}
			else
			{
				if (File.Exists(tempFilePath) && new FileInfo(tempFilePath).Length > 0)
					throw new InvalidDataException("The cached download has no resume state; its partial bytes were retained.");
				var stream = new NetworkFileStream(tempFilePath, new Uri(DownloadOptions.DownloadUrl), 0,
					new() { { "User-Agent", DownloadOptions.UserAgent } }, DownloadOptions.DownloadIdentity);
				nfsp = new NetworkFileStreamPersister(stream, jsonDownloadState);
			}
			nfsp.NetworkFileStream.RequestHeaders["User-Agent"] = DownloadOptions.UserAgent;
			nfsp.NetworkFileStream.SpeedLimit = DownloadOptions.DownloadSpeedBps;
			OnTempFileCreated(new(tempFilePath, DownloadOptions.InputType.ToString()));
			OnTempFileCreated(new(jsonDownloadState));
			return nfsp;
		}
		catch
		{
			// A load, setup or subscriber failure is not permission to discard a partial
			// book. The lazy field has not yet taken ownership, so clean up locally.
			try { nfsp?.Dispose(); }
			catch (Exception ex) { Serilog.Log.Warning(ex, "Could not close the failed resume setup."); }
			throw;
		}
	}
}
