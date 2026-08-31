using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using LibationAvalonia.DesignSystem.Components;
using LibationFileManager;
using System.Windows.Input;

namespace LibationAvalonia.Features.Onboarding;

/// <summary>Isolated, non-mutating profile preview for the onboarding draft.</summary>
public partial class ProfileChoicePreview : UserControl
{
	public static readonly StyledProperty<ExperienceStyle> ProfileStyleProperty =
		AvaloniaProperty.Register<ProfileChoicePreview, ExperienceStyle>(nameof(ProfileStyle), ExperienceStyle.Cellar);
	public static readonly StyledProperty<string> ProfileNameProperty =
		AvaloniaProperty.Register<ProfileChoicePreview, string>(nameof(ProfileName), "Profile");
	public static readonly StyledProperty<string?> DescriptionProperty =
		AvaloniaProperty.Register<ProfileChoicePreview, string?>(nameof(Description));
	public static readonly StyledProperty<string> ActionTextProperty =
		AvaloniaProperty.Register<ProfileChoicePreview, string>(nameof(ActionText), "Choose profile");
	public static readonly StyledProperty<ICommand?> SelectCommandProperty =
		AvaloniaProperty.Register<ProfileChoicePreview, ICommand?>(nameof(SelectCommand));
	public static readonly StyledProperty<bool> IsSelectedProperty =
		AvaloniaProperty.Register<ProfileChoicePreview, bool>(nameof(IsSelected));
	public static readonly StyledProperty<LibationStatusKind> SelectionStatusProperty =
		AvaloniaProperty.Register<ProfileChoicePreview, LibationStatusKind>(nameof(SelectionStatus), LibationStatusKind.DownloadPending);
	public static readonly StyledProperty<string> SelectionTextProperty =
		AvaloniaProperty.Register<ProfileChoicePreview, string>(nameof(SelectionText), "Preview");

	public ProfileChoicePreview()
	{
		InitializeComponent();
		AttachedToVisualTree += ProfileChoicePreview_AttachedToVisualTree;
	}

	public ExperienceStyle ProfileStyle { get => GetValue(ProfileStyleProperty); set => SetValue(ProfileStyleProperty, value); }
	public string ProfileName { get => GetValue(ProfileNameProperty); set => SetValue(ProfileNameProperty, value); }
	public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
	public string ActionText { get => GetValue(ActionTextProperty); set => SetValue(ActionTextProperty, value); }
	public ICommand? SelectCommand { get => GetValue(SelectCommandProperty); set => SetValue(SelectCommandProperty, value); }
	public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }
	public LibationStatusKind SelectionStatus { get => GetValue(SelectionStatusProperty); private set => SetValue(SelectionStatusProperty, value); }
	public string SelectionText { get => GetValue(SelectionTextProperty); private set => SetValue(SelectionTextProperty, value); }

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);
		if (change.Property == ProfileStyleProperty)
			ApplyPreview();
		if (change.Property == IsSelectedProperty)
		{
			SelectionStatus = IsSelected ? LibationStatusKind.Completed : LibationStatusKind.DownloadPending;
			SelectionText = IsSelected ? "Selected" : "Preview";
		}
	}

	private void ProfileChoicePreview_AttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e) => ApplyPreview();

	private void ApplyPreview()
	{
		if (PreviewScope is null || App.ExperienceManager is not { } manager)
			return;
		var preview = manager.CreatePreviewScope(ProfileStyle);
		PreviewScope.Resources = preview.Resources;
		PreviewScope.RequestedThemeVariant = preview.Host.RequestedThemeVariant;
	}
}
