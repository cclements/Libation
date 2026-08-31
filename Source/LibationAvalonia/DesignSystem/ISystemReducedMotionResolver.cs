using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace LibationAvalonia.DesignSystem;

/// <summary>
/// Platform seam for the operating system's reduced-motion preference.
/// Avalonia 12 does not expose one cross-platform, so platform packaging may
/// provide an implementation without coupling profile resolution to an OS API.
/// </summary>
public interface ISystemReducedMotionResolver : IDisposable
{
	/// <summary>True/false when supported; null when this platform adapter cannot determine the preference.</summary>
	bool? IsReducedMotionPreferred { get; }
	event EventHandler? PreferenceChanged;
	void Refresh() { }
}

public sealed class UnavailableSystemReducedMotionResolver : ISystemReducedMotionResolver
{
	public bool? IsReducedMotionPreferred => null;
	public event EventHandler? PreferenceChanged
	{
		add { }
		remove { }
	}
	public void Dispose() { }
}

/// <summary>
/// Reads the native reduced-motion setting on Windows and macOS without adding
/// a UI-framework dependency. Linux desktop environments do not expose one
/// stable cross-desktop contract, so they retain the explicit Reduce/Full
/// application preference when this resolver returns null.
/// </summary>
public sealed class PlatformSystemReducedMotionResolver : ISystemReducedMotionResolver
{
	private bool? currentPreference;

	public PlatformSystemReducedMotionResolver() => currentPreference = ReadNativePreference();

	public bool? IsReducedMotionPreferred => currentPreference;

	public event EventHandler? PreferenceChanged;

	public void Refresh()
	{
		var next = ReadNativePreference();
		if (next == currentPreference)
			return;
		currentPreference = next;
		PreferenceChanged?.Invoke(this, EventArgs.Empty);
	}

	public void Dispose() { }

	private static bool? ReadNativePreference()
	{
		try
		{
			if (OperatingSystem.IsWindows())
				return ReadWindowsPreference();
			if (OperatingSystem.IsMacOS())
				return ReadMacPreference();
		}
		catch
		{
			// An unavailable OS setting must never prevent the application from
			// starting. Explicit Reduce and Full preferences remain authoritative.
		}
		return null;
	}

	[SupportedOSPlatform("windows")]
	private static bool? ReadWindowsPreference()
	{
		using var accessibility = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Control Panel\Accessibility\Animation");
		if (TryReadEnabled(accessibility?.GetValue("Animation"), out var animationsEnabled))
			return !animationsEnabled;

		using var metrics = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop\WindowMetrics");
		return TryReadEnabled(metrics?.GetValue("MinAnimate"), out animationsEnabled)
			? !animationsEnabled
			: null;
	}

	private static bool TryReadEnabled(object? value, out bool enabled)
	{
		enabled = false;
		if (value is int number)
		{
			enabled = number != 0;
			return true;
		}
		if (value is string text && int.TryParse(text, out number))
		{
			enabled = number != 0;
			return true;
		}
		return false;
	}

	[SupportedOSPlatform("macos")]
	private static bool? ReadMacPreference()
	{
		var workspaceClass = objc_getClass("NSWorkspace");
		if (workspaceClass == IntPtr.Zero)
			return null;
		var workspace = IntPtr_objc_msgSend(workspaceClass, sel_registerName("sharedWorkspace"));
		if (workspace == IntPtr.Zero)
			return null;
		return Bool_objc_msgSend(workspace, sel_registerName("accessibilityDisplayShouldReduceMotion"));
	}

	[DllImport("/usr/lib/libobjc.A.dylib")]
	private static extern IntPtr objc_getClass(string name);

	[DllImport("/usr/lib/libobjc.A.dylib")]
	private static extern IntPtr sel_registerName(string name);

	[DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
	private static extern IntPtr IntPtr_objc_msgSend(IntPtr receiver, IntPtr selector);

	[DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
	[return: MarshalAs(UnmanagedType.I1)]
	private static extern bool Bool_objc_msgSend(IntPtr receiver, IntPtr selector);
}
