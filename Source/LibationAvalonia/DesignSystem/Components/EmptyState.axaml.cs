using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using System.Windows.Input;

namespace LibationAvalonia.DesignSystem.Components;

public partial class EmptyState : UserControl
{
	public static readonly StyledProperty<IControlTemplate?> IllustrationTemplateProperty = AvaloniaProperty.Register<EmptyState, IControlTemplate?>(nameof(IllustrationTemplate));
	public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<EmptyState, string?>(nameof(Title));
	public static readonly StyledProperty<string?> ExplanationProperty = AvaloniaProperty.Register<EmptyState, string?>(nameof(Explanation));
	public static readonly StyledProperty<string?> PrimaryActionTextProperty = AvaloniaProperty.Register<EmptyState, string?>(nameof(PrimaryActionText));
	public static readonly StyledProperty<ICommand?> PrimaryCommandProperty = AvaloniaProperty.Register<EmptyState, ICommand?>(nameof(PrimaryCommand));
	public static readonly StyledProperty<object?> PrimaryCommandParameterProperty = AvaloniaProperty.Register<EmptyState, object?>(nameof(PrimaryCommandParameter));
	public static readonly StyledProperty<string?> SecondaryActionTextProperty = AvaloniaProperty.Register<EmptyState, string?>(nameof(SecondaryActionText));
	public static readonly StyledProperty<ICommand?> SecondaryCommandProperty = AvaloniaProperty.Register<EmptyState, ICommand?>(nameof(SecondaryCommand));
	public static readonly StyledProperty<object?> SecondaryCommandParameterProperty = AvaloniaProperty.Register<EmptyState, object?>(nameof(SecondaryCommandParameter));

	public EmptyState() => InitializeComponent();

	public IControlTemplate? IllustrationTemplate { get => GetValue(IllustrationTemplateProperty); set => SetValue(IllustrationTemplateProperty, value); }
	public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
	public string? Explanation { get => GetValue(ExplanationProperty); set => SetValue(ExplanationProperty, value); }
	public string? PrimaryActionText { get => GetValue(PrimaryActionTextProperty); set => SetValue(PrimaryActionTextProperty, value); }
	public ICommand? PrimaryCommand { get => GetValue(PrimaryCommandProperty); set => SetValue(PrimaryCommandProperty, value); }
	public object? PrimaryCommandParameter { get => GetValue(PrimaryCommandParameterProperty); set => SetValue(PrimaryCommandParameterProperty, value); }
	public string? SecondaryActionText { get => GetValue(SecondaryActionTextProperty); set => SetValue(SecondaryActionTextProperty, value); }
	public ICommand? SecondaryCommand { get => GetValue(SecondaryCommandProperty); set => SetValue(SecondaryCommandProperty, value); }
	public object? SecondaryCommandParameter { get => GetValue(SecondaryCommandParameterProperty); set => SetValue(SecondaryCommandParameterProperty, value); }
}
