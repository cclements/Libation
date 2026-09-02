using Avalonia;
using Avalonia.Controls;
using System.Windows.Input;

namespace LibationAvalonia.DesignSystem.Components;

public partial class ShellStatusBar : UserControl
{
	public static readonly StyledProperty<string?> VisibleTextProperty = AvaloniaProperty.Register<ShellStatusBar, string?>(nameof(VisibleText));
	public static readonly StyledProperty<string?> SelectedTextProperty = AvaloniaProperty.Register<ShellStatusBar, string?>(nameof(SelectedText));
	public static readonly StyledProperty<string?> TrashTextProperty = AvaloniaProperty.Register<ShellStatusBar, string?>(nameof(TrashText));
	public static readonly StyledProperty<string?> FlightTextProperty = AvaloniaProperty.Register<ShellStatusBar, string?>(nameof(FlightText));
	public static readonly StyledProperty<string?> QueueTextProperty = AvaloniaProperty.Register<ShellStatusBar, string?>(nameof(QueueText));
	public static readonly StyledProperty<string?> ScanTextProperty = AvaloniaProperty.Register<ShellStatusBar, string?>(nameof(ScanText));
	public static readonly StyledProperty<string?> UpdateTextProperty = AvaloniaProperty.Register<ShellStatusBar, string?>(nameof(UpdateText));
	public static readonly StyledProperty<double?> UpgradeProgressProperty = AvaloniaProperty.Register<ShellStatusBar, double?>(nameof(UpgradeProgress));
	public static readonly StyledProperty<bool> IsScanningProperty = AvaloniaProperty.Register<ShellStatusBar, bool>(nameof(IsScanning));
	public static readonly StyledProperty<bool> ShowFlightProperty = AvaloniaProperty.Register<ShellStatusBar, bool>(nameof(ShowFlight));
	public static readonly StyledProperty<bool> HasUpdateAvailableProperty = AvaloniaProperty.Register<ShellStatusBar, bool>(nameof(HasUpdateAvailable));
	public static readonly StyledProperty<ICommand?> FlightCommandProperty = AvaloniaProperty.Register<ShellStatusBar, ICommand?>(nameof(FlightCommand));
	public static readonly StyledProperty<ICommand?> QueueCommandProperty = AvaloniaProperty.Register<ShellStatusBar, ICommand?>(nameof(QueueCommand));
	public static readonly StyledProperty<ICommand?> UpdateCommandProperty = AvaloniaProperty.Register<ShellStatusBar, ICommand?>(nameof(UpdateCommand));

	public ShellStatusBar() => InitializeComponent();

	public string? VisibleText { get => GetValue(VisibleTextProperty); set => SetValue(VisibleTextProperty, value); }
	public string? SelectedText { get => GetValue(SelectedTextProperty); set => SetValue(SelectedTextProperty, value); }
	public string? TrashText { get => GetValue(TrashTextProperty); set => SetValue(TrashTextProperty, value); }
	public string? FlightText { get => GetValue(FlightTextProperty); set => SetValue(FlightTextProperty, value); }
	public string? QueueText { get => GetValue(QueueTextProperty); set => SetValue(QueueTextProperty, value); }
	public string? ScanText { get => GetValue(ScanTextProperty); set => SetValue(ScanTextProperty, value); }
	public string? UpdateText { get => GetValue(UpdateTextProperty); set => SetValue(UpdateTextProperty, value); }
	public double? UpgradeProgress { get => GetValue(UpgradeProgressProperty); set => SetValue(UpgradeProgressProperty, value); }
	public bool IsScanning { get => GetValue(IsScanningProperty); set => SetValue(IsScanningProperty, value); }
	public bool ShowFlight { get => GetValue(ShowFlightProperty); set => SetValue(ShowFlightProperty, value); }
	public bool HasUpdateAvailable { get => GetValue(HasUpdateAvailableProperty); set => SetValue(HasUpdateAvailableProperty, value); }
	public ICommand? FlightCommand { get => GetValue(FlightCommandProperty); set => SetValue(FlightCommandProperty, value); }
	public ICommand? QueueCommand { get => GetValue(QueueCommandProperty); set => SetValue(QueueCommandProperty, value); }
	public ICommand? UpdateCommand { get => GetValue(UpdateCommandProperty); set => SetValue(UpdateCommandProperty, value); }
}
