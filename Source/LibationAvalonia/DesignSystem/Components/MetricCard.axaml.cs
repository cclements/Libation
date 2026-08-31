using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System.Windows.Input;

namespace LibationAvalonia.DesignSystem.Components;

public partial class MetricCard : UserControl
{
	public static readonly StyledProperty<Geometry?> IconDataProperty = AvaloniaProperty.Register<MetricCard, Geometry?>(nameof(IconData));
	public static readonly StyledProperty<string?> ValueProperty = AvaloniaProperty.Register<MetricCard, string?>(nameof(Value));
	public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<MetricCard, string?>(nameof(Label));
	public static readonly StyledProperty<string?> DeltaTextProperty = AvaloniaProperty.Register<MetricCard, string?>(nameof(DeltaText));
	public static readonly StyledProperty<string?> StatusTextProperty = AvaloniaProperty.Register<MetricCard, string?>(nameof(StatusText));
	public static readonly StyledProperty<ComponentSeverity> SeverityProperty = AvaloniaProperty.Register<MetricCard, ComponentSeverity>(nameof(Severity));
	public static readonly StyledProperty<string?> CommandTextProperty = AvaloniaProperty.Register<MetricCard, string?>(nameof(CommandText));
	public static readonly StyledProperty<ICommand?> CommandProperty = AvaloniaProperty.Register<MetricCard, ICommand?>(nameof(Command));
	public static readonly StyledProperty<object?> CommandParameterProperty = AvaloniaProperty.Register<MetricCard, object?>(nameof(CommandParameter));

	public MetricCard()
	{
		InitializeComponent();
		UpdateSeverityState();
	}

	public Geometry? IconData { get => GetValue(IconDataProperty); set => SetValue(IconDataProperty, value); }
	public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
	public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
	public string? DeltaText { get => GetValue(DeltaTextProperty); set => SetValue(DeltaTextProperty, value); }
	public string? StatusText { get => GetValue(StatusTextProperty); set => SetValue(StatusTextProperty, value); }
	public ComponentSeverity Severity { get => GetValue(SeverityProperty); set => SetValue(SeverityProperty, value); }
	public string? CommandText { get => GetValue(CommandTextProperty); set => SetValue(CommandTextProperty, value); }
	public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
	public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);
		if (change.Property == SeverityProperty)
			UpdateSeverityState();
	}

	private void UpdateSeverityState()
	{
		PseudoClasses.Set(":info", Severity == ComponentSeverity.Info);
		PseudoClasses.Set(":success", Severity == ComponentSeverity.Success);
		PseudoClasses.Set(":warning", Severity == ComponentSeverity.Warning);
		PseudoClasses.Set(":danger", Severity == ComponentSeverity.Danger);
	}
}
