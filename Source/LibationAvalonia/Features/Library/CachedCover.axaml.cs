using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using DataLayer;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LibationAvalonia.Features.Library;

public partial class CachedCover : UserControl
{
	public static readonly StyledProperty<IImage?> CoverProperty =
		AvaloniaProperty.Register<CachedCover, IImage?>(nameof(Cover));
	public static readonly StyledProperty<LibraryBook?> BookProperty =
		AvaloniaProperty.Register<CachedCover, LibraryBook?>(nameof(Book));
	public static readonly StyledProperty<CoverImageCache?> CacheProperty =
		AvaloniaProperty.Register<CachedCover, CoverImageCache?>(nameof(Cache));
	public static readonly StyledProperty<CoverVariant> VariantProperty =
		AvaloniaProperty.Register<CachedCover, CoverVariant>(nameof(Variant), CoverVariant.Medium);
	public static readonly StyledProperty<int> DecodePixelWidthProperty =
		AvaloniaProperty.Register<CachedCover, int>(nameof(DecodePixelWidth));
	public static readonly StyledProperty<string?> AccessibleNameProperty =
		AvaloniaProperty.Register<CachedCover, string?>(nameof(AccessibleName));

	private CancellationTokenSource? coverCancellation;
	private CoverImageCache.CoverLease? coverLease;
	private bool isAttached;
	private LibraryViewModel? registeredOwner;

	public CachedCover()
	{
		InitializeComponent();
		DataContextChanged += (_, _) =>
		{
			if (isAttached)
			{
				UnregisterConsumer();
				RegisterConsumer();
			}
			RestartCoverLoad();
		};
	}

	public IImage? Cover { get => GetValue(CoverProperty); private set => SetValue(CoverProperty, value); }
	public LibraryBook? Book { get => GetValue(BookProperty); set => SetValue(BookProperty, value); }
	public CoverImageCache? Cache { get => GetValue(CacheProperty); set => SetValue(CacheProperty, value); }
	public CoverVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
	public int DecodePixelWidth { get => GetValue(DecodePixelWidthProperty); set => SetValue(DecodePixelWidthProperty, value); }
	public string? AccessibleName { get => GetValue(AccessibleNameProperty); set => SetValue(AccessibleNameProperty, value); }
	internal Task CoverLoadTask { get; private set; } = Task.CompletedTask;

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);
		if (change.Property != BookProperty
			&& change.Property != CacheProperty
			&& change.Property != VariantProperty
			&& change.Property != DecodePixelWidthProperty)
			return;
		if (isAttached)
			RestartCoverLoad();
	}

	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
	{
		base.OnAttachedToVisualTree(e);
		isAttached = true;
		RegisterConsumer();
		RestartCoverLoad();
	}

	protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
	{
		isAttached = false;
		CancelCoverLoad();
		UnregisterConsumer();
		base.OnDetachedFromVisualTree(e);
	}

	private void RestartCoverLoad()
	{
		CancelCoverLoad();
		var item = DataContext as LibraryBookItemViewModel;
		var book = Book ?? item?.LibraryBook;
		var cache = Cache ?? item?.Owner.CoverCache;
		int decodePixelWidth = DecodePixelWidth > 0
			? DecodePixelWidth
			: Variant == CoverVariant.Small
				? item?.Owner.SmallCoverDecodePixelWidth ?? 160
				: item?.Owner.MediumCoverDecodePixelWidth ?? 300;
		if (!isAttached || book is null || cache is null)
		{
			CoverLoadTask = Task.CompletedTask;
			return;
		}

		var cancellation = new CancellationTokenSource();
		coverCancellation = cancellation;
		CoverLoadTask = LoadCoverAsync(book, cache, Variant, decodePixelWidth, cancellation);
	}

	private async Task LoadCoverAsync(
		LibraryBook book,
		CoverImageCache cache,
		CoverVariant variant,
		int decodePixelWidth,
		CancellationTokenSource cancellation)
	{
		try
		{
			var lease = await cache.AcquireAsync(
				book,
				variant,
				Math.Max(1, decodePixelWidth),
				cancellation.Token);
			if (cancellation.IsCancellationRequested)
			{
				lease?.Dispose();
				return;
			}
			coverLease = lease;
			Cover = lease?.Image;
		}
		catch (OperationCanceledException)
		{
		}
		catch (ObjectDisposedException)
		{
		}
		catch (Exception ex)
		{
			Serilog.Log.Logger.Warning(ex, global::LibationAvalonia.Properties.Resources.CachedCoveraxamlUnableToLoadTheCachedCoverFor, book.Book.AudibleProductId);
		}
	}

	private void CancelCoverLoad()
	{
		coverCancellation?.Cancel();
		coverCancellation?.Dispose();
		coverCancellation = null;
		coverLease?.Dispose();
		coverLease = null;
		Cover = null;
	}

	private void RegisterConsumer()
	{
		if (registeredOwner is not null || DataContext is not LibraryBookItemViewModel item)
			return;
		item.Owner.RegisterCoverConsumer(CoverVariant.Medium, realized: true);
		registeredOwner = item.Owner;
	}

	private void UnregisterConsumer()
	{
		if (registeredOwner is not { } owner)
			return;
		owner.RegisterCoverConsumer(CoverVariant.Medium, realized: false);
		registeredOwner = null;
	}
}
