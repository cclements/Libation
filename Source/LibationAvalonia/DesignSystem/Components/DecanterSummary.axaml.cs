using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using System.Windows.Input;

namespace LibationAvalonia.DesignSystem.Components;

public partial class DecanterSummary : UserControl
{
	public static readonly StyledProperty<string> TitleProperty = AvaloniaProperty.Register<DecanterSummary, string>(nameof(Title), "Decanter");
	public static readonly StyledProperty<string?> SummaryTextProperty = AvaloniaProperty.Register<DecanterSummary, string?>(nameof(SummaryText));
	public static readonly StyledProperty<string?> ActiveTextProperty = AvaloniaProperty.Register<DecanterSummary, string?>(nameof(ActiveText));
	public static readonly StyledProperty<double> ProgressProperty = AvaloniaProperty.Register<DecanterSummary, double>(nameof(Progress));
	public static readonly StyledProperty<bool> ShowProgressProperty = AvaloniaProperty.Register<DecanterSummary, bool>(nameof(ShowProgress));
	public static readonly StyledProperty<bool> IsExpandedProperty = AvaloniaProperty.Register<DecanterSummary, bool>(nameof(IsExpanded), defaultBindingMode: BindingMode.TwoWay);
	public static readonly StyledProperty<object?> DetailsContentProperty = AvaloniaProperty.Register<DecanterSummary, object?>(nameof(DetailsContent));
	public static readonly StyledProperty<ICommand?> PauseCommandProperty = AvaloniaProperty.Register<DecanterSummary, ICommand?>(nameof(PauseCommand));
	public static readonly StyledProperty<ICommand?> CancelCommandProperty = AvaloniaProperty.Register<DecanterSummary, ICommand?>(nameof(CancelCommand));
	public static readonly StyledProperty<bool> CanPauseProperty = AvaloniaProperty.Register<DecanterSummary, bool>(nameof(CanPause));
	public static readonly StyledProperty<bool> CanCancelProperty = AvaloniaProperty.Register<DecanterSummary, bool>(nameof(CanCancel));

	public DecanterSummary() => InitializeComponent();

	public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
	public string? SummaryText { get => GetValue(SummaryTextProperty); set => SetValue(SummaryTextProperty, value); }
	public string? ActiveText { get => GetValue(ActiveTextProperty); set => SetValue(ActiveTextProperty, value); }
	public double Progress { get => GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
	public bool ShowProgress { get => GetValue(ShowProgressProperty); set => SetValue(ShowProgressProperty, value); }
	public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
	public object? DetailsContent { get => GetValue(DetailsContentProperty); set => SetValue(DetailsContentProperty, value); }
	public ICommand? PauseCommand { get => GetValue(PauseCommandProperty); set => SetValue(PauseCommandProperty, value); }
	public ICommand? CancelCommand { get => GetValue(CancelCommandProperty); set => SetValue(CancelCommandProperty, value); }
	public bool CanPause { get => GetValue(CanPauseProperty); set => SetValue(CanPauseProperty, value); }
	public bool CanCancel { get => GetValue(CanCancelProperty); set => SetValue(CanCancelProperty, value); }
}
