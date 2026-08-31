using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using System.Windows.Input;

namespace LibationAvalonia.DesignSystem.Components;

public partial class QueueItem : UserControl
{
	public static readonly StyledProperty<IImage?> CoverProperty = AvaloniaProperty.Register<QueueItem, IImage?>(nameof(Cover));
	public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<QueueItem, string?>(nameof(Title));
	public static readonly StyledProperty<string?> StageProperty = AvaloniaProperty.Register<QueueItem, string?>(nameof(Stage));
	public static readonly StyledProperty<string?> MessageProperty = AvaloniaProperty.Register<QueueItem, string?>(nameof(Message));
	public static readonly StyledProperty<LibationStatusKind> StatusProperty = AvaloniaProperty.Register<QueueItem, LibationStatusKind>(nameof(Status), LibationStatusKind.Processing);
	public static readonly StyledProperty<string?> StatusTextProperty = AvaloniaProperty.Register<QueueItem, string?>(nameof(StatusText));
	public static readonly StyledProperty<double> ProgressProperty = AvaloniaProperty.Register<QueueItem, double>(nameof(Progress));
	public static readonly StyledProperty<bool> ShowProgressProperty = AvaloniaProperty.Register<QueueItem, bool>(nameof(ShowProgress), true);
	public static readonly StyledProperty<string?> ErrorDetailsProperty = AvaloniaProperty.Register<QueueItem, string?>(nameof(ErrorDetails));
	public static readonly StyledProperty<bool> IsExpandedProperty = AvaloniaProperty.Register<QueueItem, bool>(nameof(IsExpanded), defaultBindingMode: BindingMode.TwoWay);
	public static readonly StyledProperty<ICommand?> RetryCommandProperty = AvaloniaProperty.Register<QueueItem, ICommand?>(nameof(RetryCommand));
	public static readonly StyledProperty<ICommand?> RevealCommandProperty = AvaloniaProperty.Register<QueueItem, ICommand?>(nameof(RevealCommand));
	public static readonly StyledProperty<ICommand?> CopyTechnicalDetailsCommandProperty = AvaloniaProperty.Register<QueueItem, ICommand?>(nameof(CopyTechnicalDetailsCommand));
	public static readonly StyledProperty<ICommand?> OpenLogCommandProperty = AvaloniaProperty.Register<QueueItem, ICommand?>(nameof(OpenLogCommand));
	public static readonly StyledProperty<ICommand?> CancelCommandProperty = AvaloniaProperty.Register<QueueItem, ICommand?>(nameof(CancelCommand));
	public static readonly StyledProperty<object?> CommandParameterProperty = AvaloniaProperty.Register<QueueItem, object?>(nameof(CommandParameter));

	public QueueItem() => InitializeComponent();

	public IImage? Cover { get => GetValue(CoverProperty); set => SetValue(CoverProperty, value); }
	public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
	public string? Stage { get => GetValue(StageProperty); set => SetValue(StageProperty, value); }
	public string? Message { get => GetValue(MessageProperty); set => SetValue(MessageProperty, value); }
	public LibationStatusKind Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }
	public string? StatusText { get => GetValue(StatusTextProperty); set => SetValue(StatusTextProperty, value); }
	public double Progress { get => GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
	public bool ShowProgress { get => GetValue(ShowProgressProperty); set => SetValue(ShowProgressProperty, value); }
	public string? ErrorDetails { get => GetValue(ErrorDetailsProperty); set => SetValue(ErrorDetailsProperty, value); }
	public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
	public ICommand? RetryCommand { get => GetValue(RetryCommandProperty); set => SetValue(RetryCommandProperty, value); }
	public ICommand? RevealCommand { get => GetValue(RevealCommandProperty); set => SetValue(RevealCommandProperty, value); }
	public ICommand? CopyTechnicalDetailsCommand { get => GetValue(CopyTechnicalDetailsCommandProperty); set => SetValue(CopyTechnicalDetailsCommandProperty, value); }
	public ICommand? OpenLogCommand { get => GetValue(OpenLogCommandProperty); set => SetValue(OpenLogCommandProperty, value); }
	public ICommand? CancelCommand { get => GetValue(CancelCommandProperty); set => SetValue(CancelCommandProperty, value); }
	public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }
}
