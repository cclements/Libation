using Avalonia;
using Avalonia.Controls;
using System.Windows.Input;

namespace LibationAvalonia.DesignSystem.Components;

public partial class ThemePreviewCard : UserControl
{
	public static readonly StyledProperty<string?> ProfileNameProperty = AvaloniaProperty.Register<ThemePreviewCard, string?>(nameof(ProfileName));
	public static readonly StyledProperty<string?> DescriptionProperty = AvaloniaProperty.Register<ThemePreviewCard, string?>(nameof(Description));
	public static readonly StyledProperty<string> ActionTextProperty = AvaloniaProperty.Register<ThemePreviewCard, string>(nameof(ActionText), "Preview action");
	public static readonly StyledProperty<ICommand?> ActionCommandProperty = AvaloniaProperty.Register<ThemePreviewCard, ICommand?>(nameof(ActionCommand));
	public static readonly StyledProperty<LibationStatusKind> BadgeStatusProperty = AvaloniaProperty.Register<ThemePreviewCard, LibationStatusKind>(nameof(BadgeStatus), LibationStatusKind.Completed);
	public static readonly StyledProperty<string?> BadgeTextProperty = AvaloniaProperty.Register<ThemePreviewCard, string?>(nameof(BadgeText));
	public static readonly StyledProperty<double> ProgressProperty = AvaloniaProperty.Register<ThemePreviewCard, double>(nameof(Progress), 64);
	public static readonly StyledProperty<string> SelectedRowTitleProperty = AvaloniaProperty.Register<ThemePreviewCard, string>(nameof(SelectedRowTitle), "Selected audiobook");
	public static readonly StyledProperty<string?> SelectedRowMetadataProperty = AvaloniaProperty.Register<ThemePreviewCard, string?>(nameof(SelectedRowMetadata));

	public ThemePreviewCard() => InitializeComponent();

	public string? ProfileName { get => GetValue(ProfileNameProperty); set => SetValue(ProfileNameProperty, value); }
	public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
	public string ActionText { get => GetValue(ActionTextProperty); set => SetValue(ActionTextProperty, value); }
	public ICommand? ActionCommand { get => GetValue(ActionCommandProperty); set => SetValue(ActionCommandProperty, value); }
	public LibationStatusKind BadgeStatus { get => GetValue(BadgeStatusProperty); set => SetValue(BadgeStatusProperty, value); }
	public string? BadgeText { get => GetValue(BadgeTextProperty); set => SetValue(BadgeTextProperty, value); }
	public double Progress { get => GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
	public string SelectedRowTitle { get => GetValue(SelectedRowTitleProperty); set => SetValue(SelectedRowTitleProperty, value); }
	public string? SelectedRowMetadata { get => GetValue(SelectedRowMetadataProperty); set => SetValue(SelectedRowMetadataProperty, value); }
}
