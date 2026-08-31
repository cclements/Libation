using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using System;
using System.Windows.Input;

namespace LibationAvalonia.DesignSystem.Components;

public partial class AttentionBanner : UserControl
{
	public static readonly StyledProperty<ComponentSeverity> SeverityProperty = AvaloniaProperty.Register<AttentionBanner, ComponentSeverity>(nameof(Severity), ComponentSeverity.Warning);
	public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<AttentionBanner, string?>(nameof(Title));
	public static readonly StyledProperty<string?> MessageProperty = AvaloniaProperty.Register<AttentionBanner, string?>(nameof(Message));
	public static readonly StyledProperty<string?> ActionTextProperty = AvaloniaProperty.Register<AttentionBanner, string?>(nameof(ActionText));
	public static readonly StyledProperty<ICommand?> ActionCommandProperty = AvaloniaProperty.Register<AttentionBanner, ICommand?>(nameof(ActionCommand));
	public static readonly StyledProperty<object?> ActionCommandParameterProperty = AvaloniaProperty.Register<AttentionBanner, object?>(nameof(ActionCommandParameter));
	public static readonly StyledProperty<string?> SecondaryActionTextProperty = AvaloniaProperty.Register<AttentionBanner, string?>(nameof(SecondaryActionText));
	public static readonly StyledProperty<ICommand?> SecondaryActionCommandProperty = AvaloniaProperty.Register<AttentionBanner, ICommand?>(nameof(SecondaryActionCommand));
	public static readonly StyledProperty<object?> SecondaryActionCommandParameterProperty = AvaloniaProperty.Register<AttentionBanner, object?>(nameof(SecondaryActionCommandParameter));
	public static readonly StyledProperty<ICommand?> DismissCommandProperty = AvaloniaProperty.Register<AttentionBanner, ICommand?>(nameof(DismissCommand));
	public static readonly StyledProperty<object?> DismissCommandParameterProperty = AvaloniaProperty.Register<AttentionBanner, object?>(nameof(DismissCommandParameter));
	public static readonly StyledProperty<bool> FocusOnShowProperty = AvaloniaProperty.Register<AttentionBanner, bool>(nameof(FocusOnShow));

	public AttentionBanner()
	{
		InitializeComponent();
		UpdateSeverityState();
		AttachedToVisualTree += (_, _) => QueueFocusIfNeeded();
	}

	public ComponentSeverity Severity { get => GetValue(SeverityProperty); set => SetValue(SeverityProperty, value); }
	public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
	public string? Message { get => GetValue(MessageProperty); set => SetValue(MessageProperty, value); }
	public string? ActionText { get => GetValue(ActionTextProperty); set => SetValue(ActionTextProperty, value); }
	public ICommand? ActionCommand { get => GetValue(ActionCommandProperty); set => SetValue(ActionCommandProperty, value); }
	public object? ActionCommandParameter { get => GetValue(ActionCommandParameterProperty); set => SetValue(ActionCommandParameterProperty, value); }
	public string? SecondaryActionText { get => GetValue(SecondaryActionTextProperty); set => SetValue(SecondaryActionTextProperty, value); }
	public ICommand? SecondaryActionCommand { get => GetValue(SecondaryActionCommandProperty); set => SetValue(SecondaryActionCommandProperty, value); }
	public object? SecondaryActionCommandParameter { get => GetValue(SecondaryActionCommandParameterProperty); set => SetValue(SecondaryActionCommandParameterProperty, value); }
	public ICommand? DismissCommand { get => GetValue(DismissCommandProperty); set => SetValue(DismissCommandProperty, value); }
	public object? DismissCommandParameter { get => GetValue(DismissCommandParameterProperty); set => SetValue(DismissCommandParameterProperty, value); }
	public bool FocusOnShow { get => GetValue(FocusOnShowProperty); set => SetValue(FocusOnShowProperty, value); }

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);
		if (change.Property == SeverityProperty)
			UpdateSeverityState();
		if (change.Property == IsVisibleProperty || change.Property == MessageProperty || change.Property == FocusOnShowProperty)
			QueueFocusIfNeeded();
	}

	private void QueueFocusIfNeeded()
	{
		if (!FocusOnShow || !IsVisible || string.IsNullOrWhiteSpace(Message))
			return;
		Dispatcher.UIThread.Post(() =>
		{
			if (FocusOnShow && IsEffectivelyVisible)
				BannerRoot.Focus();
		}, DispatcherPriority.Input);
	}

	private void UpdateSeverityState()
	{
		PseudoClasses.Set(":info", Severity == ComponentSeverity.Info);
		PseudoClasses.Set(":success", Severity == ComponentSeverity.Success);
		PseudoClasses.Set(":warning", Severity == ComponentSeverity.Warning);
		PseudoClasses.Set(":danger", Severity == ComponentSeverity.Danger);
	}
}
