using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using LibationFileManager;
using ReactiveUI;
using System;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;

namespace LibationAvalonia.Features.Library;

public sealed class GalleryCardInteractionEventArgs(
	LibraryBookItemViewModel item,
	KeyModifiers modifiers,
	Control placementTarget) : EventArgs
{
	public LibraryBookItemViewModel Item { get; } = item;
	public KeyModifiers Modifiers { get; } = modifiers;
	public Control PlacementTarget { get; } = placementTarget;
}

public sealed class GalleryNavigationRequestedEventArgs(
	LibraryBookItemViewModel item,
	int itemOffset) : EventArgs
{
	public LibraryBookItemViewModel Item { get; } = item;
	public int ItemOffset { get; } = itemOffset;
}

public partial class GalleryBookCard : UserControl
{
	public static readonly StyledProperty<IImage?> CoverProperty =
		AvaloniaProperty.Register<GalleryBookCard, IImage?>(nameof(Cover));

	private CancellationTokenSource? coverCancellation;
	private CoverImageCache.CoverLease? coverLease;
	private bool isAttached;
	private LibraryViewModel? registeredOwner;

	public GalleryBookCard()
	{
		InitializeComponent();
		OpenCommand = ReactiveCommand.Create(RequestOpen);
		ContextCommand = ReactiveCommand.Create(RequestContextMenu);
		DataContextChanged += (_, _) =>
		{
			if (isAttached)
			{
				UnregisterConsumer();
				RegisterConsumer();
			}
			RestartCoverLoad();
		};
		AddHandler(InputElement.PointerPressedEvent, Card_PointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
		AddHandler(InputElement.DoubleTappedEvent, Card_DoubleTapped, RoutingStrategies.Bubble, handledEventsToo: true);
		AddHandler(InputElement.KeyDownEvent, Card_KeyDown, RoutingStrategies.Bubble, handledEventsToo: true);
		GotFocus += (_, _) =>
		{
			if (DataContext is LibraryBookItemViewModel item)
				FocusRequested?.Invoke(this, new(item, KeyModifiers.None, this));
		};
	}

	public event EventHandler<GalleryCardInteractionEventArgs>? SelectionRequested;
	public event EventHandler<GalleryCardInteractionEventArgs>? FocusRequested;
	public event EventHandler<GalleryCardInteractionEventArgs>? OpenRequested;
	public event EventHandler<GalleryCardInteractionEventArgs>? ContextMenuRequested;
	public event EventHandler<GalleryNavigationRequestedEventArgs>? NavigationRequested;

	public IImage? Cover { get => GetValue(CoverProperty); private set => SetValue(CoverProperty, value); }
	public ReactiveCommand<Unit, Unit> OpenCommand { get; }
	public ReactiveCommand<Unit, Unit> ContextCommand { get; }
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
				CoverVariant.Small,
				item.Owner.SmallCoverDecodePixelWidth,
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
			Serilog.Log.Logger.Warning(ex, "Unable to load a Gallery cover for {ProductId}.", item.ProductId);
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
		item.Owner.RegisterCoverConsumer(CoverVariant.Small, realized: true);
		registeredOwner = item.Owner;
	}

	private void UnregisterConsumer()
	{
		if (registeredOwner is not { } owner)
			return;
		owner.RegisterCoverConsumer(CoverVariant.Small, realized: false);
		registeredOwner = null;
	}

	private void Card_PointerPressed(object? sender, PointerPressedEventArgs e)
	{
		if (DataContext is not LibraryBookItemViewModel item)
			return;
		var point = e.GetCurrentPoint(this);
		bool contextGesture = point.Properties.IsRightButtonPressed
			|| (Configuration.IsMacOs && point.Properties.IsLeftButtonPressed && e.KeyModifiers.HasFlag(KeyModifiers.Control));
		if (contextGesture)
		{
			ContextMenuRequested?.Invoke(this, new(item, e.KeyModifiers, this));
			e.Handled = true;
			return;
		}
		if (point.Properties.IsLeftButtonPressed)
		{
			Focus();
			SelectionRequested?.Invoke(this, new(item, e.KeyModifiers, this));
		}
	}

	private void Card_DoubleTapped(object? sender, TappedEventArgs e)
	{
		RequestOpen();
		e.Handled = true;
	}

	private void Card_KeyDown(object? sender, KeyEventArgs e)
	{
		// The card owns spatial-navigation shortcuts only while the card itself has
		// focus. Enter/Space from the embedded More and Open buttons must retain
		// normal button semantics instead of bubbling into a second card action.
		if (!ReferenceEquals(e.Source, this) || DataContext is not LibraryBookItemViewModel item)
			return;
		int? offset = e.Key switch
		{
			Key.Left => -1,
			Key.Right => 1,
			Key.Up => -item.Owner.GalleryColumnCount,
			Key.Down => item.Owner.GalleryColumnCount,
			_ => null,
		};
		if (offset is not null)
		{
			NavigationRequested?.Invoke(this, new(item, offset.Value));
			e.Handled = true;
		}
		else if (e.Key == Key.Space)
		{
			SelectionRequested?.Invoke(this, new(item, e.KeyModifiers, this));
			e.Handled = true;
		}
		else if (e.Key == Key.Enter)
		{
			RequestOpen();
			e.Handled = true;
		}
		else if (e.Key == Key.F10 && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
		{
			RequestContextMenu();
			e.Handled = true;
		}
	}

	private void RequestOpen()
	{
		if (DataContext is LibraryBookItemViewModel item)
			OpenRequested?.Invoke(this, new(item, KeyModifiers.None, this));
	}

	private void RequestContextMenu()
	{
		if (DataContext is LibraryBookItemViewModel item)
			ContextMenuRequested?.Invoke(this, new(item, KeyModifiers.None, this));
	}
}
