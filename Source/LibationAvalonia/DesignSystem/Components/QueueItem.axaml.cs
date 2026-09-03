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
	public static readonly StyledProperty<string?> AuthorProperty = AvaloniaProperty.Register<QueueItem, string?>(nameof(Author));
	public static readonly StyledProperty<string?> StageProperty = AvaloniaProperty.Register<QueueItem, string?>(nameof(Stage));
	public static readonly StyledProperty<string?> AccessibleNameProperty = AvaloniaProperty.Register<QueueItem, string?>(nameof(AccessibleName));
	public static readonly StyledProperty<string?> StageAccessibleNameProperty = AvaloniaProperty.Register<QueueItem, string?>(nameof(StageAccessibleName));
	public static readonly StyledProperty<string?> MessageProperty = AvaloniaProperty.Register<QueueItem, string?>(nameof(Message));
	public static readonly StyledProperty<LibationStatusKind> StatusProperty = AvaloniaProperty.Register<QueueItem, LibationStatusKind>(nameof(Status), LibationStatusKind.Processing);
	public static readonly StyledProperty<string?> StatusTextProperty = AvaloniaProperty.Register<QueueItem, string?>(nameof(StatusText));
	public static readonly StyledProperty<double> ProgressProperty = AvaloniaProperty.Register<QueueItem, double>(nameof(Progress));
	public static readonly StyledProperty<bool> ShowProgressProperty = AvaloniaProperty.Register<QueueItem, bool>(nameof(ShowProgress), true);
	public static readonly StyledProperty<string?> ProgressTextProperty = AvaloniaProperty.Register<QueueItem, string?>(nameof(ProgressText));
	public static readonly StyledProperty<string?> ProgressAccessibleNameProperty = AvaloniaProperty.Register<QueueItem, string?>(nameof(ProgressAccessibleName));
	public static readonly StyledProperty<string?> EtaTextProperty = AvaloniaProperty.Register<QueueItem, string?>(nameof(EtaText));
	public static readonly StyledProperty<string?> OutputProfileTextProperty = AvaloniaProperty.Register<QueueItem, string?>(nameof(OutputProfileText));
	public static readonly StyledProperty<string?> ErrorDetailsProperty = AvaloniaProperty.Register<QueueItem, string?>(nameof(ErrorDetails));
	public static readonly StyledProperty<string?> RecommendedActionProperty = AvaloniaProperty.Register<QueueItem, string?>(nameof(RecommendedAction));
	public static readonly StyledProperty<string?> ReferenceTextProperty = AvaloniaProperty.Register<QueueItem, string?>(nameof(ReferenceText));
	public static readonly StyledProperty<bool> IsExpandedProperty = AvaloniaProperty.Register<QueueItem, bool>(nameof(IsExpanded), defaultBindingMode: BindingMode.TwoWay);
	public static readonly StyledProperty<ICommand?> MoveUpCommandProperty = AvaloniaProperty.Register<QueueItem, ICommand?>(nameof(MoveUpCommand));
	public static readonly StyledProperty<ICommand?> MoveDownCommandProperty = AvaloniaProperty.Register<QueueItem, ICommand?>(nameof(MoveDownCommand));
	public static readonly StyledProperty<ICommand?> RetryCommandProperty = AvaloniaProperty.Register<QueueItem, ICommand?>(nameof(RetryCommand));
	public static readonly StyledProperty<ICommand?> RevealCommandProperty = AvaloniaProperty.Register<QueueItem, ICommand?>(nameof(RevealCommand));
	public static readonly StyledProperty<ICommand?> CopyTechnicalDetailsCommandProperty = AvaloniaProperty.Register<QueueItem, ICommand?>(nameof(CopyTechnicalDetailsCommand));
	public static readonly StyledProperty<ICommand?> OpenLogCommandProperty = AvaloniaProperty.Register<QueueItem, ICommand?>(nameof(OpenLogCommand));
	public static readonly StyledProperty<ICommand?> CancelCommandProperty = AvaloniaProperty.Register<QueueItem, ICommand?>(nameof(CancelCommand));
	public static readonly StyledProperty<object?> CommandParameterProperty = AvaloniaProperty.Register<QueueItem, object?>(nameof(CommandParameter));
	public static readonly StyledProperty<string?> MoveUpAccessibleNameProperty = AvaloniaProperty.Register<QueueItem, string?>(nameof(MoveUpAccessibleName));
	public static readonly StyledProperty<string?> MoveDownAccessibleNameProperty = AvaloniaProperty.Register<QueueItem, string?>(nameof(MoveDownAccessibleName));
	public static readonly StyledProperty<string?> RetryAccessibleNameProperty = AvaloniaProperty.Register<QueueItem, string?>(nameof(RetryAccessibleName));
	public static readonly StyledProperty<string?> RevealAccessibleNameProperty = AvaloniaProperty.Register<QueueItem, string?>(nameof(RevealAccessibleName));
	public static readonly StyledProperty<string?> CopyDetailsAccessibleNameProperty = AvaloniaProperty.Register<QueueItem, string?>(nameof(CopyDetailsAccessibleName));
	public static readonly StyledProperty<string?> OpenLogAccessibleNameProperty = AvaloniaProperty.Register<QueueItem, string?>(nameof(OpenLogAccessibleName));
	public static readonly StyledProperty<string?> CancelAccessibleNameProperty = AvaloniaProperty.Register<QueueItem, string?>(nameof(CancelAccessibleName));

	public QueueItem() => InitializeComponent();

	public IImage? Cover { get => GetValue(CoverProperty); set => SetValue(CoverProperty, value); }
	public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
	public string? Author { get => GetValue(AuthorProperty); set => SetValue(AuthorProperty, value); }
	public string? Stage { get => GetValue(StageProperty); set => SetValue(StageProperty, value); }
	public string? AccessibleName { get => GetValue(AccessibleNameProperty); set => SetValue(AccessibleNameProperty, value); }
	public string? StageAccessibleName { get => GetValue(StageAccessibleNameProperty); set => SetValue(StageAccessibleNameProperty, value); }
	public string? Message { get => GetValue(MessageProperty); set => SetValue(MessageProperty, value); }
	public LibationStatusKind Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }
	public string? StatusText { get => GetValue(StatusTextProperty); set => SetValue(StatusTextProperty, value); }
	public double Progress { get => GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
	public bool ShowProgress { get => GetValue(ShowProgressProperty); set => SetValue(ShowProgressProperty, value); }
	public string? ProgressText { get => GetValue(ProgressTextProperty); set => SetValue(ProgressTextProperty, value); }
	public string? ProgressAccessibleName { get => GetValue(ProgressAccessibleNameProperty); set => SetValue(ProgressAccessibleNameProperty, value); }
	public string? EtaText { get => GetValue(EtaTextProperty); set => SetValue(EtaTextProperty, value); }
	public string? OutputProfileText { get => GetValue(OutputProfileTextProperty); set => SetValue(OutputProfileTextProperty, value); }
	public string? ErrorDetails { get => GetValue(ErrorDetailsProperty); set => SetValue(ErrorDetailsProperty, value); }
	public string? RecommendedAction { get => GetValue(RecommendedActionProperty); set => SetValue(RecommendedActionProperty, value); }
	public string? ReferenceText { get => GetValue(ReferenceTextProperty); set => SetValue(ReferenceTextProperty, value); }
	public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
	public ICommand? MoveUpCommand { get => GetValue(MoveUpCommandProperty); set => SetValue(MoveUpCommandProperty, value); }
	public ICommand? MoveDownCommand { get => GetValue(MoveDownCommandProperty); set => SetValue(MoveDownCommandProperty, value); }
	public ICommand? RetryCommand { get => GetValue(RetryCommandProperty); set => SetValue(RetryCommandProperty, value); }
	public ICommand? RevealCommand { get => GetValue(RevealCommandProperty); set => SetValue(RevealCommandProperty, value); }
	public ICommand? CopyTechnicalDetailsCommand { get => GetValue(CopyTechnicalDetailsCommandProperty); set => SetValue(CopyTechnicalDetailsCommandProperty, value); }
	public ICommand? OpenLogCommand { get => GetValue(OpenLogCommandProperty); set => SetValue(OpenLogCommandProperty, value); }
	public ICommand? CancelCommand { get => GetValue(CancelCommandProperty); set => SetValue(CancelCommandProperty, value); }
	public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }
	public string? MoveUpAccessibleName { get => GetValue(MoveUpAccessibleNameProperty); set => SetValue(MoveUpAccessibleNameProperty, value); }
	public string? MoveDownAccessibleName { get => GetValue(MoveDownAccessibleNameProperty); set => SetValue(MoveDownAccessibleNameProperty, value); }
	public string? RetryAccessibleName { get => GetValue(RetryAccessibleNameProperty); set => SetValue(RetryAccessibleNameProperty, value); }
	public string? RevealAccessibleName { get => GetValue(RevealAccessibleNameProperty); set => SetValue(RevealAccessibleNameProperty, value); }
	public string? CopyDetailsAccessibleName { get => GetValue(CopyDetailsAccessibleNameProperty); set => SetValue(CopyDetailsAccessibleNameProperty, value); }
	public string? OpenLogAccessibleName { get => GetValue(OpenLogAccessibleNameProperty); set => SetValue(OpenLogAccessibleNameProperty, value); }
	public string? CancelAccessibleName { get => GetValue(CancelAccessibleNameProperty); set => SetValue(CancelAccessibleNameProperty, value); }
}
