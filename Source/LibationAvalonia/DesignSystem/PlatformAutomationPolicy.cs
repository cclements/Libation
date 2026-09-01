using Avalonia.Automation;
using System;

namespace LibationAvalonia.DesignSystem;

/// <summary>
/// Keeps live-region announcements available except where Avalonia's native macOS
/// bridge can abort while translating an announcement name into Cocoa user info.
/// Ordinary automation names and help text remain available on every platform.
/// </summary>
public static class PlatformAutomationPolicy
{
	public static AutomationLiveSetting Polite => OperatingSystem.IsMacOSVersionAtLeast(27)
		? AutomationLiveSetting.Off
		: AutomationLiveSetting.Polite;
}
