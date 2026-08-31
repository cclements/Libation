using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Threading;
using System;
using System.Collections;
using System.Windows.Input;

namespace LibationAvalonia.DesignSystem.Components;

public partial class FlightTray : UserControl
{
	public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty = AvaloniaProperty.Register<FlightTray, IEnumerable?>(nameof(ItemsSource));
	public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty = AvaloniaProperty.Register<FlightTray, IDataTemplate?>(nameof(ItemTemplate));
	public static readonly StyledProperty<string> TitleProperty = AvaloniaProperty.Register<FlightTray, string>(nameof(Title), "Flight");
	public static readonly StyledProperty<string?> CountTextProperty = AvaloniaProperty.Register<FlightTray, string?>(nameof(CountText));
	public static readonly StyledProperty<string?> DurationTextProperty = AvaloniaProperty.Register<FlightTray, string?>(nameof(DurationText));
	public static readonly StyledProperty<string?> EstimatedSizeTextProperty = AvaloniaProperty.Register<FlightTray, string?>(nameof(EstimatedSizeText));
	public static readonly StyledProperty<string?> WarningTextProperty = AvaloniaProperty.Register<FlightTray, string?>(nameof(WarningText));
	public static readonly StyledProperty<bool> FocusWarningOnShowProperty = AvaloniaProperty.Register<FlightTray, bool>(nameof(FocusWarningOnShow));
	public static readonly StyledProperty<string?> OutputProfileTextProperty = AvaloniaProperty.Register<FlightTray, string?>(nameof(OutputProfileText));
	public static readonly StyledProperty<ICommand?> ProcessCommandProperty = AvaloniaProperty.Register<FlightTray, ICommand?>(nameof(ProcessCommand));
	public static readonly StyledProperty<string> ProcessActionTextProperty = AvaloniaProperty.Register<FlightTray, string>(nameof(ProcessActionText), "Process");
	public static readonly StyledProperty<object?> ProcessCommandParameterProperty = AvaloniaProperty.Register<FlightTray, object?>(nameof(ProcessCommandParameter));
	public static readonly StyledProperty<ICommand?> ClearCommandProperty = AvaloniaProperty.Register<FlightTray, ICommand?>(nameof(ClearCommand));
	public static readonly StyledProperty<object?> ClearCommandParameterProperty = AvaloniaProperty.Register<FlightTray, object?>(nameof(ClearCommandParameter));
	public static readonly StyledProperty<ICommand?> UndoCommandProperty = AvaloniaProperty.Register<FlightTray, ICommand?>(nameof(UndoCommand));
	public static readonly StyledProperty<string?> UndoActionTextProperty = AvaloniaProperty.Register<FlightTray, string?>(nameof(UndoActionText));

	public FlightTray()
	{
		InitializeComponent();
		AttachedToVisualTree += (_, _) => QueueWarningFocusIfNeeded();
	}

	public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
	public IDataTemplate? ItemTemplate { get => GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }
	public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
	public string? CountText { get => GetValue(CountTextProperty); set => SetValue(CountTextProperty, value); }
	public string? DurationText { get => GetValue(DurationTextProperty); set => SetValue(DurationTextProperty, value); }
	public string? EstimatedSizeText { get => GetValue(EstimatedSizeTextProperty); set => SetValue(EstimatedSizeTextProperty, value); }
	public string? WarningText { get => GetValue(WarningTextProperty); set => SetValue(WarningTextProperty, value); }
	public bool FocusWarningOnShow { get => GetValue(FocusWarningOnShowProperty); set => SetValue(FocusWarningOnShowProperty, value); }
	public string? OutputProfileText { get => GetValue(OutputProfileTextProperty); set => SetValue(OutputProfileTextProperty, value); }
	public ICommand? ProcessCommand { get => GetValue(ProcessCommandProperty); set => SetValue(ProcessCommandProperty, value); }
	public string ProcessActionText { get => GetValue(ProcessActionTextProperty); set => SetValue(ProcessActionTextProperty, value); }
	public object? ProcessCommandParameter { get => GetValue(ProcessCommandParameterProperty); set => SetValue(ProcessCommandParameterProperty, value); }
	public ICommand? ClearCommand { get => GetValue(ClearCommandProperty); set => SetValue(ClearCommandProperty, value); }
	public object? ClearCommandParameter { get => GetValue(ClearCommandParameterProperty); set => SetValue(ClearCommandParameterProperty, value); }
	public ICommand? UndoCommand { get => GetValue(UndoCommandProperty); set => SetValue(UndoCommandProperty, value); }
	public string? UndoActionText { get => GetValue(UndoActionTextProperty); set => SetValue(UndoActionTextProperty, value); }

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);
		if (change.Property == WarningTextProperty || change.Property == FocusWarningOnShowProperty)
			QueueWarningFocusIfNeeded();
	}

	private void QueueWarningFocusIfNeeded()
	{
		if (!FocusWarningOnShow || string.IsNullOrWhiteSpace(WarningText))
			return;
		Dispatcher.UIThread.Post(() =>
		{
			if (FocusWarningOnShow && WarningSummary.IsEffectivelyVisible)
				WarningSummary.Focus();
		}, DispatcherPriority.Input);
	}
}
