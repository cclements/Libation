using Avalonia;
using Avalonia.Controls;
using System.Collections;
using System.Windows.Input;

namespace LibationAvalonia.DesignSystem.Components;

public partial class DecanterSummary : UserControl
{
	public static readonly StyledProperty<string?> SummaryTextProperty = AvaloniaProperty.Register<DecanterSummary, string?>(nameof(SummaryText));
	public static readonly StyledProperty<bool> IsCellarProperty = AvaloniaProperty.Register<DecanterSummary, bool>(nameof(IsCellar), true);
	public static readonly StyledProperty<bool> IsTastingRoomProperty = AvaloniaProperty.Register<DecanterSummary, bool>(nameof(IsTastingRoom));
	public static readonly StyledProperty<bool> HasWorkProperty = AvaloniaProperty.Register<DecanterSummary, bool>(nameof(HasWork));
	public static readonly StyledProperty<bool> IsIdleProperty = AvaloniaProperty.Register<DecanterSummary, bool>(nameof(IsIdle), true);
	public static readonly StyledProperty<IEnumerable?> ActiveItemsProperty = AvaloniaProperty.Register<DecanterSummary, IEnumerable?>(nameof(ActiveItems));
	public static readonly StyledProperty<string?> InQueueTextProperty = AvaloniaProperty.Register<DecanterSummary, string?>(nameof(InQueueText));
	public static readonly StyledProperty<string?> ConvertingTextProperty = AvaloniaProperty.Register<DecanterSummary, string?>(nameof(ConvertingText));
	public static readonly StyledProperty<string?> RunningTimeTextProperty = AvaloniaProperty.Register<DecanterSummary, string?>(nameof(RunningTimeText));
	public static readonly StyledProperty<string?> CurrentTitleProperty = AvaloniaProperty.Register<DecanterSummary, string?>(nameof(CurrentTitle));
	public static readonly StyledProperty<string?> CurrentStageTextProperty = AvaloniaProperty.Register<DecanterSummary, string?>(nameof(CurrentStageText));
	public static readonly StyledProperty<string?> CurrentStageAccessibleNameProperty = AvaloniaProperty.Register<DecanterSummary, string?>(nameof(CurrentStageAccessibleName));
	public static readonly StyledProperty<string?> CurrentOutputTextProperty = AvaloniaProperty.Register<DecanterSummary, string?>(nameof(CurrentOutputText));
	public static readonly StyledProperty<double> ProgressProperty = AvaloniaProperty.Register<DecanterSummary, double>(nameof(Progress));
	public static readonly StyledProperty<string?> ProgressTextProperty = AvaloniaProperty.Register<DecanterSummary, string?>(nameof(ProgressText));
	public static readonly StyledProperty<string?> ProgressAccessibleNameProperty = AvaloniaProperty.Register<DecanterSummary, string?>(nameof(ProgressAccessibleName));
	public static readonly StyledProperty<bool> ShowProgressProperty = AvaloniaProperty.Register<DecanterSummary, bool>(nameof(ShowProgress));
	public static readonly StyledProperty<ICommand?> OpenProcessingCommandProperty = AvaloniaProperty.Register<DecanterSummary, ICommand?>(nameof(OpenProcessingCommand));
	public static readonly StyledProperty<object?> OpenProcessingCommandParameterProperty = AvaloniaProperty.Register<DecanterSummary, object?>(nameof(OpenProcessingCommandParameter));
	public static readonly StyledProperty<ICommand?> CancelCommandProperty = AvaloniaProperty.Register<DecanterSummary, ICommand?>(nameof(CancelCommand));
	public static readonly StyledProperty<bool> CanCancelProperty = AvaloniaProperty.Register<DecanterSummary, bool>(nameof(CanCancel));
	public static readonly StyledProperty<string?> CancelAccessibleNameProperty = AvaloniaProperty.Register<DecanterSummary, string?>(nameof(CancelAccessibleName));

	// Kept as source-compatible presentation inputs for the component gallery and extensions.
	public static readonly StyledProperty<string?> ActiveTextProperty = AvaloniaProperty.Register<DecanterSummary, string?>(nameof(ActiveText));
	public static readonly StyledProperty<bool> IsExpandedProperty = AvaloniaProperty.Register<DecanterSummary, bool>(nameof(IsExpanded));
	public static readonly StyledProperty<object?> IllustrationContentProperty = AvaloniaProperty.Register<DecanterSummary, object?>(nameof(IllustrationContent));
	public static readonly StyledProperty<double> IllustrationWidthProperty = AvaloniaProperty.Register<DecanterSummary, double>(nameof(IllustrationWidth), 120);
	public static readonly StyledProperty<double> IllustrationHeightProperty = AvaloniaProperty.Register<DecanterSummary, double>(nameof(IllustrationHeight), 92);
	public static readonly StyledProperty<object?> DetailsContentProperty = AvaloniaProperty.Register<DecanterSummary, object?>(nameof(DetailsContent));
	public static readonly StyledProperty<ICommand?> PauseCommandProperty = AvaloniaProperty.Register<DecanterSummary, ICommand?>(nameof(PauseCommand));
	public static readonly StyledProperty<bool> CanPauseProperty = AvaloniaProperty.Register<DecanterSummary, bool>(nameof(CanPause));

	public DecanterSummary() => InitializeComponent();

	public string? SummaryText { get => GetValue(SummaryTextProperty); set => SetValue(SummaryTextProperty, value); }
	public bool IsCellar { get => GetValue(IsCellarProperty); set => SetValue(IsCellarProperty, value); }
	public bool IsTastingRoom { get => GetValue(IsTastingRoomProperty); set => SetValue(IsTastingRoomProperty, value); }
	public bool HasWork { get => GetValue(HasWorkProperty); set => SetValue(HasWorkProperty, value); }
	public bool IsIdle { get => GetValue(IsIdleProperty); set => SetValue(IsIdleProperty, value); }
	public IEnumerable? ActiveItems { get => GetValue(ActiveItemsProperty); set => SetValue(ActiveItemsProperty, value); }
	public string? InQueueText { get => GetValue(InQueueTextProperty); set => SetValue(InQueueTextProperty, value); }
	public string? ConvertingText { get => GetValue(ConvertingTextProperty); set => SetValue(ConvertingTextProperty, value); }
	public string? RunningTimeText { get => GetValue(RunningTimeTextProperty); set => SetValue(RunningTimeTextProperty, value); }
	public string? CurrentTitle { get => GetValue(CurrentTitleProperty); set => SetValue(CurrentTitleProperty, value); }
	public string? CurrentStageText { get => GetValue(CurrentStageTextProperty); set => SetValue(CurrentStageTextProperty, value); }
	public string? CurrentStageAccessibleName { get => GetValue(CurrentStageAccessibleNameProperty); set => SetValue(CurrentStageAccessibleNameProperty, value); }
	public string? CurrentOutputText { get => GetValue(CurrentOutputTextProperty); set => SetValue(CurrentOutputTextProperty, value); }
	public double Progress { get => GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
	public string? ProgressText { get => GetValue(ProgressTextProperty); set => SetValue(ProgressTextProperty, value); }
	public string? ProgressAccessibleName { get => GetValue(ProgressAccessibleNameProperty); set => SetValue(ProgressAccessibleNameProperty, value); }
	public bool ShowProgress { get => GetValue(ShowProgressProperty); set => SetValue(ShowProgressProperty, value); }
	public ICommand? OpenProcessingCommand { get => GetValue(OpenProcessingCommandProperty); set => SetValue(OpenProcessingCommandProperty, value); }
	public object? OpenProcessingCommandParameter { get => GetValue(OpenProcessingCommandParameterProperty); set => SetValue(OpenProcessingCommandParameterProperty, value); }
	public ICommand? CancelCommand { get => GetValue(CancelCommandProperty); set => SetValue(CancelCommandProperty, value); }
	public bool CanCancel { get => GetValue(CanCancelProperty); set => SetValue(CanCancelProperty, value); }
	public string? CancelAccessibleName { get => GetValue(CancelAccessibleNameProperty); set => SetValue(CancelAccessibleNameProperty, value); }

	public string? ActiveText { get => GetValue(ActiveTextProperty); set => SetValue(ActiveTextProperty, value); }
	public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
	public object? IllustrationContent { get => GetValue(IllustrationContentProperty); set => SetValue(IllustrationContentProperty, value); }
	public double IllustrationWidth { get => GetValue(IllustrationWidthProperty); set => SetValue(IllustrationWidthProperty, value); }
	public double IllustrationHeight { get => GetValue(IllustrationHeightProperty); set => SetValue(IllustrationHeightProperty, value); }
	public object? DetailsContent { get => GetValue(DetailsContentProperty); set => SetValue(DetailsContentProperty, value); }
	public ICommand? PauseCommand { get => GetValue(PauseCommandProperty); set => SetValue(PauseCommandProperty, value); }
	public bool CanPause { get => GetValue(CanPauseProperty); set => SetValue(CanPauseProperty, value); }
}
