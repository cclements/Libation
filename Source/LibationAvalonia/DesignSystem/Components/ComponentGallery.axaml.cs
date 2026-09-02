using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using LibationAvalonia.Properties;
using LibationFileManager;
using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace LibationAvalonia.DesignSystem.Components;

/// <summary>
/// Developer-only visual inventory. All data and commands are local samples;
/// profile changes are isolated to PreviewScope and never alter Configuration.
/// </summary>
public partial class ComponentGallery : UserControl
{
	public static IReadOnlyList<ExperienceStyle> ProfileOptions { get; } =
		[ExperienceStyle.Cellar, ExperienceStyle.TastingRoom, ExperienceStyle.HighContrast];
	public static IReadOnlyList<DensityMode> DensityOptions { get; } =
		[DensityMode.Comfortable, DensityMode.Compact];
	public static IReadOnlyList<DecorationLevel> DecorationOptions { get; } =
		[DecorationLevel.Full, DecorationLevel.Reduced, DecorationLevel.Off];
	public static IReadOnlyList<ReducedMotionPreference> MotionOptions { get; } =
		[ReducedMotionPreference.FollowSystem, ReducedMotionPreference.Reduce, ReducedMotionPreference.Full];

	public static readonly StyledProperty<ExperienceStyle> PreviewStyleProperty =
		AvaloniaProperty.Register<ComponentGallery, ExperienceStyle>(nameof(PreviewStyle), ExperienceStyle.Cellar);
	public static readonly StyledProperty<DensityMode> PreviewDensityProperty =
		AvaloniaProperty.Register<ComponentGallery, DensityMode>(nameof(PreviewDensity), DensityMode.Comfortable);
	public static readonly StyledProperty<DecorationLevel> PreviewDecorationProperty =
		AvaloniaProperty.Register<ComponentGallery, DecorationLevel>(nameof(PreviewDecoration), DecorationLevel.Full);
	public static readonly StyledProperty<ReducedMotionPreference> PreviewMotionProperty =
		AvaloniaProperty.Register<ComponentGallery, ReducedMotionPreference>(nameof(PreviewMotion), ReducedMotionPreference.FollowSystem);
	public static readonly StyledProperty<bool> UseSystemTypographyProperty =
		AvaloniaProperty.Register<ComponentGallery, bool>(nameof(UseSystemTypography));

	private bool initialized;

	public ComponentGallery()
	{
		InertCommand = new SampleCommand();
		NavigationItems =
		[
			new("Overview", null),
			new("Library", "12"),
			new("Downloads", "2"),
			new("Processing", "!"),
		];
		GalleryBooks =
		[
			new("The Long Way Home", "A. Listener", "11 hr 42 min", LibationStatusKind.Downloaded, 100),
			new("Cellar Notes", "B. Narrator", "Processing chapter 8", LibationStatusKind.Processing, 62),
			new("A Missing Vintage", "C. Author", "Permission required", LibationStatusKind.NeedsAttention, 18),
		];
		ToastMessages =
		[
			new("Book added to Flight.", ToastKind.Undo, "Undo", InertCommand),
			new("Processing completed.", ToastKind.Completed),
			new("Output folder is nearly full.", ToastKind.Warning, "Review", InertCommand),
			new("Path copied.", ToastKind.Copied),
			new("The transient operation failed.", ToastKind.Failure, "Retry", InertCommand),
		];

		InitializeComponent();
		NavigationSample.SelectedItem = NavigationItems[1];
		initialized = true;
		AttachedToVisualTree += ComponentGallery_AttachedToVisualTree;
	}

	public static void ShowWindow(Window? owner = null)
	{
		var window = new Window
		{
			Title = LibationAvalonia.Properties.Resources.ComponentGalleryWindowTitle,
			Width = 1280,
			Height = 900,
			MinWidth = 720,
			MinHeight = 560,
			Content = new ComponentGallery(),
			WindowStartupLocation = owner is null
				? WindowStartupLocation.CenterScreen
				: WindowStartupLocation.CenterOwner,
		};

		if (owner is null)
			window.Show();
		else
			window.Show(owner);
	}

	public ExperienceStyle PreviewStyle { get => GetValue(PreviewStyleProperty); set => SetValue(PreviewStyleProperty, value); }
	public DensityMode PreviewDensity { get => GetValue(PreviewDensityProperty); set => SetValue(PreviewDensityProperty, value); }
	public DecorationLevel PreviewDecoration { get => GetValue(PreviewDecorationProperty); set => SetValue(PreviewDecorationProperty, value); }
	public ReducedMotionPreference PreviewMotion { get => GetValue(PreviewMotionProperty); set => SetValue(PreviewMotionProperty, value); }
	public bool UseSystemTypography { get => GetValue(UseSystemTypographyProperty); set => SetValue(UseSystemTypographyProperty, value); }
	public ICommand InertCommand { get; }
	public IReadOnlyList<GalleryNavigationItem> NavigationItems { get; }
	public IReadOnlyList<GalleryBook> GalleryBooks { get; }
	public IReadOnlyList<ToastMessage> ToastMessages { get; }

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);
		if (initialized && (change.Property == PreviewStyleProperty
			|| change.Property == PreviewDensityProperty
			|| change.Property == PreviewDecorationProperty
			|| change.Property == PreviewMotionProperty
			|| change.Property == UseSystemTypographyProperty))
			ApplyPreviewScope();
	}

	private void ComponentGallery_AttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e) => ApplyPreviewScope();

	private void ApplyPreviewScope()
	{
		if (App.ExperienceManager is not { } manager)
		{
			PreviewDescription.Text = "Preview manager is not initialized; inherited application resources are shown.";
			return;
		}

		var preview = manager.CreatePreviewScope(
			PreviewStyle,
			PreviewDensity,
			PreviewDecoration,
			PreviewMotion,
			UseSystemTypography);
		PreviewScope.Resources = preview.Resources;
		PreviewScope.RequestedThemeVariant = preview.RequestedThemeVariant;
		PreviewDescription.Text = $"{preview.Profile.DisplayName} · {PreviewDensity} · {PreviewDecoration} decoration · {PreviewMotion} motion";
	}

	private sealed class SampleCommand : ICommand
	{
		public event EventHandler? CanExecuteChanged { add { } remove { } }
		public bool CanExecute(object? parameter) => true;
		public void Execute(object? parameter) { }
	}
}

public sealed record GalleryNavigationItem(string Label, string? Badge)
{
	public override string ToString() => Label;
}

public sealed record GalleryBook(
	string Title,
	string SupportingText,
	string Metadata,
	LibationStatusKind Status,
	double Progress);
