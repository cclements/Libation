using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using System.Collections;
using System.Windows.Input;

namespace LibationAvalonia.DesignSystem.Components;

public partial class PageHeader : UserControl
{
	public static readonly StyledProperty<string?> EyebrowProperty = AvaloniaProperty.Register<PageHeader, string?>(nameof(Eyebrow));
	public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<PageHeader, string?>(nameof(Title));
	public static readonly StyledProperty<string?> SupportingTextProperty = AvaloniaProperty.Register<PageHeader, string?>(nameof(SupportingText));
	public static readonly StyledProperty<string?> PrimaryActionTextProperty = AvaloniaProperty.Register<PageHeader, string?>(nameof(PrimaryActionText));
	public static readonly StyledProperty<ICommand?> PrimaryCommandProperty = AvaloniaProperty.Register<PageHeader, ICommand?>(nameof(PrimaryCommand));
	public static readonly StyledProperty<object?> PrimaryCommandParameterProperty = AvaloniaProperty.Register<PageHeader, object?>(nameof(PrimaryCommandParameter));
	public static readonly StyledProperty<IEnumerable?> SecondaryActionsProperty = AvaloniaProperty.Register<PageHeader, IEnumerable?>(nameof(SecondaryActions));
	public static readonly StyledProperty<IDataTemplate?> SecondaryActionTemplateProperty = AvaloniaProperty.Register<PageHeader, IDataTemplate?>(nameof(SecondaryActionTemplate));
	public static readonly StyledProperty<object?> StatusContentProperty = AvaloniaProperty.Register<PageHeader, object?>(nameof(StatusContent));
	public static readonly StyledProperty<object?> HeroArtProperty = AvaloniaProperty.Register<PageHeader, object?>(nameof(HeroArt));

	public PageHeader() => InitializeComponent();

	public string? Eyebrow { get => GetValue(EyebrowProperty); set => SetValue(EyebrowProperty, value); }
	public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
	public string? SupportingText { get => GetValue(SupportingTextProperty); set => SetValue(SupportingTextProperty, value); }
	public string? PrimaryActionText { get => GetValue(PrimaryActionTextProperty); set => SetValue(PrimaryActionTextProperty, value); }
	public ICommand? PrimaryCommand { get => GetValue(PrimaryCommandProperty); set => SetValue(PrimaryCommandProperty, value); }
	public object? PrimaryCommandParameter { get => GetValue(PrimaryCommandParameterProperty); set => SetValue(PrimaryCommandParameterProperty, value); }
	public IEnumerable? SecondaryActions { get => GetValue(SecondaryActionsProperty); set => SetValue(SecondaryActionsProperty, value); }
	public IDataTemplate? SecondaryActionTemplate { get => GetValue(SecondaryActionTemplateProperty); set => SetValue(SecondaryActionTemplateProperty, value); }
	public object? StatusContent { get => GetValue(StatusContentProperty); set => SetValue(StatusContentProperty, value); }
	public object? HeroArt { get => GetValue(HeroArtProperty); set => SetValue(HeroArtProperty, value); }
}
