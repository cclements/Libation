using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LibationAvalonia.Features.Library;

public partial class CachedCover : UserControl
{
	public static readonly StyledProperty<IImage?> CoverProperty =
		AvaloniaProperty.Register<CachedCover, IImage?>(nameof(Cover));

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
	internal Task CoverLoadTask { get; private set; } = Task.CompletedTask;

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
		if (!isAttached || DataContext is not LibraryBookItemViewModel item)
		{
			CoverLoadTask = Task.CompletedTask;
			return;
		}

		var cancellation = new CancellationTokenSource();
		coverCancellation = cancellation;
		CoverLoadTask = LoadCoverAsync(item, cancellation);
	}

	private async Task LoadCoverAsync(LibraryBookItemViewModel item, CancellationTokenSource cancellation)
	{
		try
		{
			var lease = await item.Owner.CoverCache.AcquireAsync(
				item.LibraryBook,
				CoverVariant.Medium,
				item.Owner.MediumCoverDecodePixelWidth,
				cancellation.Token);
			if (cancellation.IsCancellationRequested || !ReferenceEquals(DataContext, item))
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
			Serilog.Log.Logger.Warning(ex, "Unable to load the details cover for {ProductId}.", item.ProductId);
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
