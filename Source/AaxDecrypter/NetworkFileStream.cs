using Dinah.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AaxDecrypter;

/// <summary>A resumable, simultaneous file downloader and reader.</summary>
public class NetworkFileStream : Stream, IUpdatable
{
	public event EventHandler? Updated;

	[JsonProperty(Required = Required.Always)]
	public string SaveFilePath { get; }

	[JsonProperty(Required = Required.Always)]
	public Uri Uri { get; private set; }

	[JsonProperty(Required = Required.Always)]
	public Dictionary<string, string> RequestHeaders { get; private set; }

	private long writePosition;
	/// <summary>Only bytes written and flushed to disk are committed for readers and resume.</summary>
	[JsonProperty(Required = Required.Always)]
	public long WritePosition { get => Interlocked.Read(ref writePosition); private set => Interlocked.Exchange(ref writePosition, value); }

	[JsonProperty(Required = Required.Always)]
	public long ContentLength { get; private set; }

	/// <summary>The strong HTTP entity tag associated with committed partial bytes, when available.</summary>
	[JsonProperty]
	public string? EntityTag { get; private set; }

	/// <summary>A nonsecret digest binding the saved bytes to the selected resource.</summary>
	[JsonProperty]
	public string? ResourceIdentity { get; private set; }

	/// <summary>The admitted final response target, including redirects, under the same identity rules.</summary>
	[JsonProperty]
	public string? EffectiveResourceIdentity { get; private set; }

	[JsonIgnore]
	public bool IsCancelled => cancellationSource.IsCancellationRequested;

	[JsonIgnore]
	public Task? DownloadTask { get; private set; }

	private long speedLimit;
	/// <summary>Bytes per second; zero disables throttling.</summary>
	public long SpeedLimit { get => Interlocked.Read(ref speedLimit); set => Interlocked.Exchange(ref speedLimit, value <= 0 ? 0 : Math.Max(value, MIN_BYTES_PER_SECOND)); }

	private readonly FileStream writeFile;
	private readonly FileStream readFile;
	private readonly CancellationTokenSource cancellationSource = new();
	private readonly object startGate = new();
	private readonly object progressGate = new();
	private Task? beginTask;
	private volatile ExceptionDispatchInfo? downloadFailure;
	private string requestedIdentity;
	private string? requestedDownloadIdentity;
	private DateTime nextUpdateTime;
	private bool disposed;

	private const int DOWNLOAD_BUFF_SZ = 8 * 1024;
	private const int DATA_FLUSH_SZ = 1024 * 1024;
	private const int THROTTLE_FREQUENCY = 8;
	private const int MAX_CONNECTION_RETRIES = 5;
	public const int MIN_BYTES_PER_SECOND = DOWNLOAD_BUFF_SZ * THROTTLE_FREQUENCY;

	public NetworkFileStream(string saveFilePath, Uri uri, long writePosition = 0,
		Dictionary<string, string>? requestHeaders = null, string? downloadIdentity = null)
	{
		SaveFilePath = ArgumentValidator.EnsureNotNullOrWhiteSpace(saveFilePath, nameof(saveFilePath));
		Uri = ArgumentValidator.EnsureNotNull(uri, nameof(uri));
		WritePosition = ArgumentValidator.EnsureGreaterThan(writePosition, nameof(writePosition), -1);
		ResourceIdentity = requestedIdentity = GetResourceIdentity(uri, downloadIdentity);
		requestedDownloadIdentity = downloadIdentity;
		RequestHeaders = requestHeaders ?? new();

		if (!Directory.Exists(Path.GetDirectoryName(saveFilePath)))
			throw new ArgumentException($"The download directory does not exist: {Path.GetDirectoryName(saveFilePath)}");

		writeFile = new FileStream(SaveFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
		try
		{
			if (writeFile.Length < WritePosition)
				throw new InvalidDataException("The cached file is shorter than its saved download position.");
			writeFile.Position = WritePosition;
			readFile = new FileStream(SaveFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
		}
		catch
		{
			writeFile.Dispose();
			cancellationSource.Dispose();
			throw;
		}
	}

	// Missing identity in legacy JSON must remain missing. Constructing a new URI digest here would
	// incorrectly turn old, unvalidated bytes into an admitted resumable state.
	[JsonConstructor]
	private NetworkFileStream(string saveFilePath, Uri uri, Dictionary<string, string> requestHeaders,
		long writePosition, long contentLength, string? entityTag, string? resourceIdentity, string? effectiveResourceIdentity)
		: this(saveFilePath, uri, writePosition, requestHeaders)
	{
		ContentLength = contentLength;
		EntityTag = entityTag;
		ResourceIdentity = resourceIdentity;
		EffectiveResourceIdentity = effectiveResourceIdentity;
	}

	/// <summary>Bind a renewed URL to the current selection before starting the download.</summary>
	public void SetUriForSameFile(Uri uriToSameFile, string? downloadIdentity = null)
	{
		string identity = GetResourceIdentity(uriToSameFile, downloadIdentity);
		lock (startGate)
		{
			ObjectDisposedException.ThrowIf(disposed, this);
			if (beginTask is not null)
				throw new InvalidOperationException("Cannot change the resource after downloading has started.");
			// Reject a mismatch in Begin, not in the caller's legacy load-error path that deletes cache files.
			requestedIdentity = identity;
			requestedDownloadIdentity = downloadIdentity;
			Uri = uriToSameFile;
		}
	}

	private static string GetResourceIdentity(Uri uri, string? downloadIdentity)
	{
		ArgumentNullException.ThrowIfNull(uri);
		if (!uri.IsAbsoluteUri || uri.Scheme is not ("http" or "https"))
			throw new ArgumentException("Downloads require an absolute HTTP or HTTPS URI.", nameof(uri));
		string resource = string.IsNullOrEmpty(downloadIdentity)
			? "uri\n" + uri.GetComponents(UriComponents.HttpRequestUrl, UriFormat.UriEscaped)
			: "content\n" + uri.GetLeftPart(UriPartial.Path) + "\n" + downloadIdentity;
		return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(resource)));
	}

	/// <summary>Return after the first response is admitted; DownloadTask owns the full transfer.</summary>
	public Task BeginDownloadingAsync()
	{
		lock (startGate)
		{
			ObjectDisposedException.ThrowIf(disposed, this);
			if (beginTask is not null) return beginTask;
			var admitted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			beginTask = admitted.Task;
			// Do not pass a cancellation token to Task.Run: even pre-start cancellation must execute cleanup.
			DownloadTask = Task.Run(() => DownloadLoopInternal(admitted));
			return beginTask;
		}
	}

	private async Task DownloadLoopInternal(TaskCompletionSource admitted)
	{
		try
		{
			cancellationSource.Token.ThrowIfCancellationRequested();
			if (ResourceIdentity is null || !string.Equals(ResourceIdentity, requestedIdentity, StringComparison.Ordinal))
				throw new InvalidDataException("The cached download has a missing or different resource identity; its partial bytes were retained.");
			if (ContentLength < 0 || (WritePosition > 0 && (ContentLength == 0 || WritePosition > ContentLength)))
				throw new InvalidDataException("The saved download position and content length are inconsistent.");
			if (WritePosition > 0 && EffectiveResourceIdentity is null)
				throw new InvalidDataException("The cached download has no final response identity; its bytes were retained.");
			if (ContentLength > 0 && WritePosition == ContentLength)
			{
				admitted.TrySetResult();
				return;
			}
			if (WritePosition > 0 && !TryStrongEntityTag(EntityTag, out _))
				throw new InvalidDataException("The cached partial has no strong entity tag and cannot be safely resumed; its bytes were retained.");

			using var client = new HttpClient();
			bool firstResponse = true;
			int retries = 0;
			do
			{
				using var block = await RequestNextByteRangeAsync(client).ConfigureAwait(false);
				cancellationSource.Token.ThrowIfCancellationRequested();
				if (firstResponse)
				{
					ContentLength = block.FileSize;
					EntityTag = block.EntityTag;
					EffectiveResourceIdentity = block.EffectiveResourceIdentity;
					// A previous failed write can leave an uncommitted tail. Only discard it after admission.
					writeFile.SetLength(WritePosition);
					writeFile.Position = WritePosition;
					firstResponse = false;
					OnUpdate(waitForWrite: true);
					admitted.TrySetResult();
				}

				long attemptStart = WritePosition;
				try
				{
					await DownloadToFile(block).ConfigureAwait(false);
				}
				catch (IOException ex) when (!IsCancelled && WritePosition > attemptStart && WritePosition < ContentLength
					&& TryStrongEntityTag(EntityTag, out _) && IsRetryableConnectionFailure(ex) && ++retries <= MAX_CONNECTION_RETRIES)
				{
					// DownloadToFile already rolled its uncommitted tail back. The next request revalidates
					// the same entity and total length before any further bytes can be appended.
					Serilog.Log.Debug("Resuming an interrupted download at committed position {Position}", WritePosition);
				}
			} while (WritePosition < ContentLength);
		}
		catch (Exception ex)
		{
			downloadFailure = ExceptionDispatchInfo.Capture(ex);
			admitted.TrySetException(ex);
			try { readFile.Dispose(); }
			catch (Exception closeError) { Serilog.Log.Error(closeError, "Could not close the failed download reader."); }
			throw;
		}
		finally
		{
			try { writeFile.Dispose(); }
			catch (Exception closeError)
			{
				if (downloadFailure is null)
				{
					downloadFailure = ExceptionDispatchInfo.Capture(closeError);
					admitted.TrySetException(closeError);
					try { readFile.Dispose(); }
					catch (Exception readCloseError) { Serilog.Log.Error(readCloseError, "Could not close the failed download reader."); }
					SignalReaders();
					throw;
				}
				Serilog.Log.Error(closeError, "Could not close the failed download writer.");
			}
			SignalReaders();
			OnUpdate(waitForWrite: true);
		}
	}

	private static bool IsRetryableConnectionFailure(IOException error)
		=> error is HttpIOException { HttpRequestError: HttpRequestError.ResponseEnded }
			|| error.InnerException is System.ComponentModel.Win32Exception { NativeErrorCode: -2146893008 };

	private async Task<BlockResponse> RequestNextByteRangeAsync(HttpClient client)
	{
		long start = WritePosition;
		using var request = new HttpRequestMessage(HttpMethod.Get, Uri);
		foreach (var header in RequestHeaders)
		{
			if (!header.Key.Equals("Range", StringComparison.OrdinalIgnoreCase)
				&& !header.Key.Equals("If-Range", StringComparison.OrdinalIgnoreCase)
				&& !header.Key.Equals("Accept-Encoding", StringComparison.OrdinalIgnoreCase))
				request.Headers.Add(header.Key, header.Value);
		}
		request.Headers.Range = new RangeHeaderValue(start, null);
		request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("identity"));
		if (start > 0)
		{
			if (!TryStrongEntityTag(EntityTag, out var tag))
				throw new InvalidDataException("A strong entity tag is required before combining partial responses.");
			request.Headers.IfRange = new RangeConditionHeaderValue(tag!);
		}

		var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationSource.Token).ConfigureAwait(false);
		try
		{
			if (response.Content.Headers.ContentEncoding.Any(x => !x.Equals("identity", StringComparison.OrdinalIgnoreCase)))
				throw new InvalidDataException("Encoded HTTP responses are not supported for byte-exact audiobook ranges.");
			long size;
			long total;
			if (response.StatusCode == HttpStatusCode.PartialContent)
			{
				var range = response.Content.Headers.ContentRange;
				if (range is null || !range.Unit.Equals("bytes", StringComparison.OrdinalIgnoreCase)
					|| !range.HasRange || !range.HasLength || range.From != start || range.To < range.From
					|| range.Length <= 0 || range.To >= range.Length)
					throw new InvalidDataException("The response does not describe the exact requested byte range and a finite total length.");
				total = range.Length!.Value;
				size = range.To!.Value - start + 1;
				if (response.Content.Headers.ContentLength is long declared && declared != size)
					throw new InvalidDataException("Content-Length does not match Content-Range.");
			}
			else if (response.StatusCode == HttpStatusCode.OK && start == 0)
			{
				if (response.Content.Headers.ContentLength is not long declared || declared < 0 || response.Content.Headers.Contains("Content-Range"))
					throw new InvalidDataException("A full response must have an explicit content length and no Content-Range.");
				total = size = declared;
			}
			else
				throw new WebException($"The server responded with unexpected download status {response.StatusCode}; cached bytes were retained.");

			var effectiveUri = response.RequestMessage?.RequestUri
				?? throw new InvalidDataException("The response has no final resource URI.");
			string effectiveIdentity = GetResourceIdentity(effectiveUri, requestedDownloadIdentity);
			if (start > 0 && !string.Equals(EffectiveResourceIdentity, effectiveIdentity, StringComparison.Ordinal))
				throw new InvalidDataException("The resumed response selected a different final resource; cached bytes were retained.");

			if (ContentLength != 0 && ContentLength != total)
				throw new InvalidDataException("The response total length differs from the saved resource length.");
			string? responseTag = TryStrongEntityTag(response.Headers.ETag?.ToString(), out var strongTag) ? strongTag!.ToString() : null;
			if (start > 0 && !string.Equals(EntityTag, responseTag, StringComparison.Ordinal))
				throw new InvalidDataException("The resumed response did not preserve the strong entity tag of the cached bytes.");
			if (start + size < total && responseTag is null)
				throw new InvalidDataException("A bounded partial response requires a strong entity tag before another range can be combined.");
			return new BlockResponse(response, size, total, responseTag, effectiveIdentity);
		}
		catch
		{
			response.Dispose();
			throw;
		}
	}

	private static bool TryStrongEntityTag(string? value, out EntityTagHeaderValue? tag)
		=> EntityTagHeaderValue.TryParse(value, out tag) && !tag.IsWeak && tag.Tag != "*";

	private sealed record BlockResponse(HttpResponseMessage Response, long BlockSize, long FileSize, string? EntityTag, string EffectiveResourceIdentity) : IDisposable
	{
		public void Dispose() => Response.Dispose();
	}

	private async Task DownloadToFile(BlockResponse block)
	{
		long endPosition = WritePosition + block.BlockSize;
		long position = WritePosition;
		long nextFlush = position + Math.Min(DATA_FLUSH_SZ, block.FileSize - position);
		bool complete = false;
		using var network = await block.Response.Content.ReadAsStreamAsync(cancellationSource.Token).ConfigureAwait(false);
		var buffer = new byte[DOWNLOAD_BUFF_SZ];
		try
		{
			DateTime throttleStart = DateTime.UtcNow;
			long bytesSinceThrottle = 0;
			while (position < endPosition)
			{
				int count = (int)Math.Min(buffer.Length, Math.Min(endPosition - position, nextFlush - position));
				int read = await network.ReadAsync(buffer.AsMemory(0, count), cancellationSource.Token).ConfigureAwait(false);
				if (read == 0) throw new EndOfStreamException("The HTTP body ended before its advertised range was complete.");
				await writeFile.WriteAsync(buffer.AsMemory(0, read), cancellationSource.Token).ConfigureAwait(false);
				position += read;
				if (position >= nextFlush && position < endPosition)
				{
					await CommitPosition(position).ConfigureAwait(false);
					nextFlush = position + Math.Min(DATA_FLUSH_SZ, block.FileSize - position);
				}

				bytesSinceThrottle += read;
				long limit = SpeedLimit;
				if (limit >= MIN_BYTES_PER_SECOND && bytesSinceThrottle > limit / THROTTLE_FREQUENCY)
				{
					int delay = (int)(throttleStart.AddSeconds(1d / THROTTLE_FREQUENCY) - DateTime.UtcNow).TotalMilliseconds;
					if (delay > 0) await Task.Delay(delay, cancellationSource.Token).ConfigureAwait(false);
					throttleStart = DateTime.UtcNow;
					bytesSinceThrottle = 0;
				}
			}
			// Even the final advertised byte is not committed until HTTP framing proves the block ended.
			if (await network.ReadAsync(buffer.AsMemory(0, 1), cancellationSource.Token).ConfigureAwait(false) != 0)
				throw new InvalidDataException("The HTTP body exceeds its advertised byte range.");
			await CommitPosition(position).ConfigureAwait(false);
			complete = true;
		}
		finally
		{
			if (!complete)
			{
				try { writeFile.SetLength(WritePosition); writeFile.Position = WritePosition; }
				catch (Exception ex) { Serilog.Log.Error(ex, "Could not remove an uncommitted download tail."); }
			}
			SignalReaders();
			OnUpdate(waitForWrite: true);
		}
	}

	private async Task CommitPosition(long position)
	{
		await writeFile.FlushAsync(cancellationSource.Token).ConfigureAwait(false);
		WritePosition = position;
		SignalReaders();
		OnUpdate();
	}

	private void SignalReaders()
	{
		lock (progressGate) Monitor.PulseAll(progressGate);
	}

	private void OnUpdate(bool waitForWrite = false)
	{
		try
		{
			if (waitForWrite || DateTime.UtcNow > nextUpdateTime)
			{
				Updated?.Invoke(this, EventArgs.Empty);
				nextUpdateTime = DateTime.UtcNow.AddMilliseconds(110);
			}
		}
		catch (Exception ex) { Serilog.Log.Error(ex, "Could not save download progress."); }
	}

	[JsonIgnore] public override bool CanRead => readFile.CanRead;
	[JsonIgnore] public override bool CanSeek => readFile.CanSeek;
	[JsonIgnore] public override bool CanWrite => false;
	[JsonIgnore] public override bool CanTimeout => false;
	[JsonIgnore] public override int ReadTimeout { get => base.ReadTimeout; set => base.ReadTimeout = value; }
	[JsonIgnore] public override int WriteTimeout { get => base.WriteTimeout; set => base.WriteTimeout = value; }
	[JsonIgnore] public override long Position { get => readFile.Position; set => Seek(value, SeekOrigin.Begin); }
	[JsonIgnore]
	public override long Length => beginTask?.IsCompletedSuccessfully == true
		? ContentLength : throw new InvalidOperationException("The initial download response has not been admitted.");

	public override void Flush() => throw new NotSupportedException();
	public override void SetLength(long value) => throw new NotSupportedException();
	public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

	public override int Read(byte[] buffer, int offset, int count)
	{
		ArgumentNullException.ThrowIfNull(buffer);
		ArgumentOutOfRangeException.ThrowIfNegative(offset);
		ArgumentOutOfRangeException.ThrowIfNegative(count);
		if (offset > buffer.Length - count) throw new ArgumentException("The buffer range is invalid.");
		downloadFailure?.Throw();
		ObjectDisposedException.ThrowIf(disposed, this);
		try
		{
			int toRead = (int)Math.Min(count, Math.Max(0, Length - Position));
			if (toRead == 0) return 0;
			WaitToPosition(Position + toRead);
			return readFile.Read(buffer, offset, toRead);
		}
		catch (ObjectDisposedException) when (downloadFailure is not null)
		{
			downloadFailure.Throw();
			throw;
		}
	}

	public override long Seek(long offset, SeekOrigin origin)
	{
		downloadFailure?.Throw();
		ObjectDisposedException.ThrowIf(disposed, this);
		long position = origin switch
		{
			SeekOrigin.Begin => offset,
			SeekOrigin.Current => checked(Position + offset),
			SeekOrigin.End => checked(Length + offset),
			_ => throw new ArgumentException("Unknown seek origin.", nameof(origin))
		};
		if (position < 0) throw new IOException("Cannot seek before the start of the download.");
		WaitToPosition(Math.Min(position, Length));
		return readFile.Position = position;
	}

	private void WaitToPosition(long requiredPosition)
	{
		lock (progressGate)
		{
			while (WritePosition < requiredPosition)
			{
				downloadFailure?.Throw();
				ObjectDisposedException.ThrowIf(disposed, this);
				if (DownloadTask?.IsCompleted != false)
					throw new IOException("The download ended before the requested bytes were committed.");
				Monitor.Wait(progressGate, 50);
			}
			downloadFailure?.Throw();
			ObjectDisposedException.ThrowIf(disposed, this);
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			Task? producer;
			lock (startGate)
			{
				if (disposed) return;
				disposed = true;
				producer = DownloadTask;
			}
			cancellationSource.Cancel();
			SignalReaders();
			try { producer?.GetAwaiter().GetResult(); }
			catch { /* The original failure belongs to Begin/DownloadTask/readers, not cleanup. */ }
			readFile.Dispose();
			writeFile.Dispose();
			cancellationSource.Dispose();
			OnUpdate(waitForWrite: true);
		}
		base.Dispose(disposing);
	}
}
