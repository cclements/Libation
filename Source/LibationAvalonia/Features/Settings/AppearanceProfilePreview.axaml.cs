using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using LibationAvalonia.DesignSystem.Components;
using LibationFileManager;
using System.Windows.Input;

namespace LibationAvalonia.Features.Settings;

/// <summary>Draft-aware, isolated profile preview; it never changes application resources.</summary>
public partial class AppearanceProfilePreview : UserControl
{
	public static readonly StyledProperty<ExperienceStyle> ProfileStyleProperty =
		AvaloniaProperty.Register<AppearanceProfilePreview, ExperienceStyle>(nameof(ProfileStyle), ExperienceStyle.Cellar);
	public static readonly StyledProperty<string> ProfileNameProperty =
		AvaloniaProperty.Register<AppearanceProfilePreview, string>(nameof(ProfileName), "Profile");
	public static readonly StyledProperty<string?> DescriptionProperty =
		AvaloniaProperty.Register<AppearanceProfilePreview, string?>(nameof(Description));
	public static readonly StyledProperty<ICommand?> SelectCommandProperty =
		AvaloniaProperty.Register<AppearanceProfilePreview, ICommand?>(nameof(SelectCommand));
	public static readonly StyledProperty<bool> IsSelectedProperty =
		AvaloniaProperty.Register<AppearanceProfilePreview, bool>(nameof(IsSelected));
	public static readonly StyledProperty<DensityMode> DensityProperty =
		AvaloniaProperty.Register<AppearanceProfilePreview, DensityMode>(nameof(Density), DensityMode.Comfortable);
	public static readonly StyledProperty<DecorationLevel> DecorationProperty =
		AvaloniaProperty.Register<AppearanceProfilePreview, DecorationLevel>(nameof(Decoration), DecorationLevel.Full);
	public static readonly StyledProperty<ReducedMotionPreference> MotionProperty =
		AvaloniaProperty.Register<AppearanceProfilePreview, ReducedMotionPreference>(nameof(Motion), ReducedMotionPreference.FollowSystem);
	public static readonly StyledProperty<bool> UseSystemTypographyProperty =
		AvaloniaProperty.Register<AppearanceProfilePreview, bool>(nameof(UseSystemTypography));
	public static readonly StyledProperty<LibationStatusKind> SelectionStatusProperty =
		AvaloniaProperty.Register<AppearanceProfilePreview, LibationStatusKind>(nameof(SelectionStatus), LibationStatusKind.DownloadPending);
	public static readonly StyledProperty<string> SelectionTextProperty =
		AvaloniaProperty.Register<AppearanceProfilePreview, string>(nameof(SelectionText), "Preview");

	public AppearanceProfilePreview()
	{
		InitializeComponent();
		AttachedToVisualTree += AttachedToVisualTreeHandler;
	}

	public ExperienceStyle ProfileStyle { get => GetValue(ProfileStyleProperty); set => SetValue(ProfileStyleProperty, value); }
	public string ProfileName { get => GetValue(ProfileNameProperty); set => SetValue(ProfileNameProperty, value); }
	public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
	public ICommand? SelectCommand { get => GetValue(SelectCommandProperty); set => SetValue(SelectCommandProperty, value); }
	public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }
	public DensityMode Density { get => GetValue(DensityProperty); set => SetValue(DensityProperty, value); }
	public DecorationLevel Decoration { get => GetValue(DecorationProperty); set => SetValue(DecorationProperty, value); }
	public ReducedMotionPreference Motion { get => GetValue(MotionProperty); set => SetValue(MotionProperty, value); }
	public bool UseSystemTypography { get => GetValue(UseSystemTypographyProperty); set => SetValue(UseSystemTypographyProperty, value); }
	public LibationStatusKind SelectionStatus { get => GetValue(SelectionStatusProperty); private set => SetValue(SelectionStatusProperty, value); }
	public string SelectionText { get => GetValue(SelectionTextProperty); private set => SetValue(SelectionTextProperty, value); }

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);
		if (change.Property == IsSelectedProperty)
		{
			SelectionStatus = IsSelected ? LibationStatusKind.Completed : LibationStatusKind.DownloadPending;
			SelectionText = IsSelected ? "Selected" : "Preview";
		}
		if (change.Property == ProfileStyleProperty
			|| change.Property == DensityProperty
			|| change.Property == DecorationProperty
			|| change.Property == MotionProperty
			|| change.Property == UseSystemTypographyProperty)
			ApplyPreview();
	}

	private void AttachedToVisualTreeHandler(object? sender, VisualTreeAttachmentEventArgs e) => ApplyPreview();

	private void ApplyPreview()
	{
		if (PreviewScope is null || App.ExperienceManager is not { } manager)
			return;
		var preview = manager.CreatePreviewScope(ProfileStyle, Density, Decoration, Motion, UseSystemTypography);
		PreviewScope.Resources = preview.Resources;
		PreviewScope.RequestedThemeVariant = preview.RequestedThemeVariant;
	}
}
