using Avalonia.Media;
using Avalonia.Media.Imaging;
using DataLayer;
using LibationFileManager;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LibationAvalonia.Features.Library;

public enum CoverVariant
{
	Small,
	Medium,
}

/// <summary>
/// Presentation-only decoded-cover cache. Its byte budget is supplied from the current
/// Gallery viewport (one decoded image per visible slot, plus the open details cover),
/// so it has no guessed global item limit.
/// </summary>
public sealed class CoverImageCache : IDisposable
{
	private readonly object gate = new();
	private readonly Dictionary<CoverCacheKey, CacheEntry> entries = new();
	private long byteBudget;
	private long decodedBytes;
	private long accessClock;
	private bool disposed;

	public long ByteBudget
	{
		get { lock (gate) return byteBudget; }
	}
	public long DecodedBytes
	{
		get { lock (gate) return decodedBytes; }
	}
	public int EntryCount
	{
		get { lock (gate) return entries.Count; }
	}

	public void ConfigureViewportBudget(
		int visibleSmallCoverSlots,
		int smallDecodePixelWidth,
		bool includeMediumCover,
		int mediumDecodePixelWidth)
	{
		if (visibleSmallCoverSlots < 0)
			throw new ArgumentOutOfRangeException(nameof(visibleSmallCoverSlots));
		if (smallDecodePixelWidth <= 0)
			throw new ArgumentOutOfRangeException(nameof(smallDecodePixelWidth));
		if (mediumDecodePixelWidth <= 0)
			throw new ArgumentOutOfRangeException(nameof(mediumDecodePixelWidth));

		long smallBytes = EstimateSquareRgbaBytes(smallDecodePixelWidth) * visibleSmallCoverSlots;
		long mediumBytes = includeMediumCover ? EstimateSquareRgbaBytes(mediumDecodePixelWidth) : 0;
		lock (gate)
		{
			ThrowIfDisposed();
			byteBudget = checked(smallBytes + mediumBytes);
			TrimUnlocked();
		}
	}

	public async Task<CoverLease?> AcquireAsync(
		LibraryBook book,
		CoverVariant variant,
		int decodePixelWidth,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(book);
		if (decodePixelWidth <= 0)
			throw new ArgumentOutOfRangeException(nameof(decodePixelWidth));

		string? pictureId = book.Book?.PictureId ?? book.Book?.PictureLarge;
		if (string.IsNullOrWhiteSpace(pictureId))
			return null;

		var key = new CoverCacheKey(pictureId, variant, decodePixelWidth);
		lock (gate)
		{
			ThrowIfDisposed();
			if (entries.TryGetValue(key, out var cached))
			{
				cached.ReferenceCount++;
				cached.LastAccess = ++accessClock;
				return new CoverLease(this, key, cached.Bitmap);
			}
		}

		var pictureSize = variant == CoverVariant.Small ? PictureSize._300x300 : PictureSize._500x500;
		var definition = new PictureDefinition(pictureId, pictureSize);
		byte[] bytes = await Task.Run(
			() => PictureStorage.GetPictureSynchronously(definition, cancellationToken),
			cancellationToken).ConfigureAwait(false);
		cancellationToken.ThrowIfCancellationRequested();

		var fallback = PictureStorage.GetDefaultImage(pictureSize);
		if (bytes.Length == 0 || ReferenceEquals(bytes, fallback))
			return null;

		Bitmap decoded;
		using (var stream = new MemoryStream(bytes, writable: false))
			decoded = Bitmap.DecodeToWidth(stream, decodePixelWidth, BitmapInterpolationMode.HighQuality);
		if (cancellationToken.IsCancellationRequested)
		{
			decoded.Dispose();
			cancellationToken.ThrowIfCancellationRequested();
		}

		lock (gate)
		{
			if (disposed)
			{
				decoded.Dispose();
				throw new ObjectDisposedException(nameof(CoverImageCache));
			}

			if (entries.TryGetValue(key, out var raced))
			{
				decoded.Dispose();
				raced.ReferenceCount++;
				raced.LastAccess = ++accessClock;
				return new CoverLease(this, key, raced.Bitmap);
			}

			var entry = new CacheEntry(decoded, GetDecodedBytes(decoded), ++accessClock) { ReferenceCount = 1 };
			entries.Add(key, entry);
			decodedBytes += entry.DecodedBytes;
			TrimUnlocked();
			return new CoverLease(this, key, decoded);
		}
	}

	private void Release(CoverCacheKey key)
	{
		lock (gate)
		{
			if (disposed || !entries.TryGetValue(key, out var entry))
				return;
			if (entry.ReferenceCount > 0)
				entry.ReferenceCount--;
			entry.LastAccess = ++accessClock;
			TrimUnlocked();
		}
	}

	private void TrimUnlocked()
	{
		foreach (var candidate in entries
			.Where(pair => pair.Value.ReferenceCount == 0)
			.OrderBy(pair => pair.Value.LastAccess)
			.ToArray())
		{
			if (decodedBytes <= byteBudget)
				break;
			entries.Remove(candidate.Key);
			decodedBytes -= candidate.Value.DecodedBytes;
			candidate.Value.Bitmap.Dispose();
		}
	}

	private static long EstimateSquareRgbaBytes(int width) => checked((long)width * width * 4);
	private static long GetDecodedBytes(Bitmap bitmap)
		=> checked((long)bitmap.PixelSize.Width * bitmap.PixelSize.Height * 4);

	private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);

	public void Dispose()
	{
		lock (gate)
		{
			if (disposed)
				return;
			disposed = true;
			foreach (var entry in entries.Values)
				entry.Bitmap.Dispose();
			entries.Clear();
			decodedBytes = 0;
			byteBudget = 0;
		}
	}

	internal readonly record struct CoverCacheKey(string PictureId, CoverVariant Variant, int DecodePixelWidth);

	private sealed class CacheEntry(Bitmap bitmap, long decodedBytes, long lastAccess)
	{
		public Bitmap Bitmap { get; } = bitmap;
		public long DecodedBytes { get; } = decodedBytes;
		public long LastAccess { get; set; } = lastAccess;
		public int ReferenceCount { get; set; }
	}

	public sealed class CoverLease : IDisposable
	{
		private CoverImageCache? owner;
		private readonly CoverCacheKey key;

		internal CoverLease(CoverImageCache owner, CoverCacheKey key, Bitmap bitmap)
		{
			this.owner = owner;
			this.key = key;
			Image = bitmap;
		}

		public IImage Image { get; }

		public void Dispose() => Interlocked.Exchange(ref owner, null)?.Release(key);
	}
}
