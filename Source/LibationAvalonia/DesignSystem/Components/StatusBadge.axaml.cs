using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;

namespace LibationAvalonia.DesignSystem.Components;

public partial class StatusBadge : UserControl
{
	public static readonly StyledProperty<LibationStatusKind> StatusProperty =
		AvaloniaProperty.Register<StatusBadge, LibationStatusKind>(nameof(Status), LibationStatusKind.Downloaded);
	public static readonly StyledProperty<string?> TextOverrideProperty =
		AvaloniaProperty.Register<StatusBadge, string?>(nameof(TextOverride));
	public static readonly StyledProperty<string?> AccessibleNameProperty =
		AvaloniaProperty.Register<StatusBadge, string?>(nameof(AccessibleName));

	public StatusBadge()
	{
		InitializeComponent();
		UpdateStatus();
	}

	public LibationStatusKind Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }
	public string? TextOverride { get => GetValue(TextOverrideProperty); set => SetValue(TextOverrideProperty, value); }
	public string? AccessibleName { get => GetValue(AccessibleNameProperty); set => SetValue(AccessibleNameProperty, value); }

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);
		if (change.Property == StatusProperty || change.Property == TextOverrideProperty || change.Property == AccessibleNameProperty)
			UpdateStatus();
	}

	private void UpdateStatus()
	{
		foreach (var name in new[]
		{
			":download-pending", ":downloading", ":downloaded", ":processing", ":completed",
			":connected", ":failed", ":cancelled", ":unavailable", ":needs-attention",
		})
			PseudoClasses.Set(name, false);

		PseudoClasses.Set(Status switch
		{
			LibationStatusKind.DownloadPending => ":download-pending",
			LibationStatusKind.Downloading => ":downloading",
			LibationStatusKind.Downloaded => ":downloaded",
			LibationStatusKind.Processing => ":processing",
			LibationStatusKind.Completed => ":completed",
			LibationStatusKind.Connected => ":connected",
			LibationStatusKind.Failed => ":failed",
			LibationStatusKind.Cancelled => ":cancelled",
			LibationStatusKind.Unavailable => ":unavailable",
			_ => ":needs-attention",
		}, true);

		if (BadgeText is null || BadgeRoot is null)
			return;
		string label = string.IsNullOrWhiteSpace(TextOverride) ? CanonicalLabel(Status) : TextOverride;
		BadgeText.Text = label;
		AutomationProperties.SetName(BadgeRoot, string.IsNullOrWhiteSpace(AccessibleName) ? label : AccessibleName);
	}

	private static string CanonicalLabel(LibationStatusKind status) => status switch
	{
		LibationStatusKind.DownloadPending => "Download pending",
		LibationStatusKind.Downloading => "Downloading",
		LibationStatusKind.Downloaded => "Downloaded",
		LibationStatusKind.Processing => "Processing",
		LibationStatusKind.Completed => "Completed",
		LibationStatusKind.Connected => "Connected",
		LibationStatusKind.Failed => "Failed",
		LibationStatusKind.Cancelled => "Cancelled",
		LibationStatusKind.Unavailable => "Unavailable",
		_ => "Needs attention",
	};
}
