using LibationAvalonia.Shell;
using LibationFileManager;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LibationAvalonia.Diagnostics;

/// <summary>Raised when a capture plan file cannot be used.</summary>
public sealed class CapturePlanException(string message) : Exception(message);

/// <summary>Environment contract for the inert capture mode used by Scripts/capture-ui.sh.</summary>
public static class CaptureEnvironment
{
	public const string PlanVariable = "LIBATION_CAPTURE_PLAN";
	public const string OutputVariable = "LIBATION_CAPTURE_OUT";
	public const string OsHandshakeVariable = "LIBATION_CAPTURE_OS_HANDSHAKE";

	public static bool IsRequested
		=> !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PlanVariable));

	public static string PlanPath
		=> Environment.GetEnvironmentVariable(PlanVariable)
			?? throw new CapturePlanException($"{PlanVariable} is not set.");

	public static string OutputDirectory
		=> Environment.GetEnvironmentVariable(OutputVariable)
			?? Path.Combine(Path.GetDirectoryName(PlanPath) ?? ".", "captures");

	public static string? OsHandshakeDirectory
		=> Environment.GetEnvironmentVariable(OsHandshakeVariable) is { } directory
			&& !string.IsNullOrWhiteSpace(directory)
			? directory
			: null;
}

public enum CaptureSurface
{
	Route,
	ComponentGallery,
	Onboarding,
	Dialog,
	Window,
	Message,
}

public enum CaptureFixture { Settings, About, RemoveConfirmation }
public enum CaptureWindowSize { Natural, Clamped }

public enum ProcessingCaptureScenario
{
	Default,
	Empty,
	Mixed,
}

public sealed record CaptureEntry(
	ExperienceStyle Profile,
	CaptureSurface Surface,
	AppRouteId Route,
	int Width,
	int Height,
	DensityMode Density,
	DecorationLevel Decoration,
	LibraryViewMode? LibraryView,
	int FlightSelectionCount,
	int ProcessingSeedCount,
	bool OpenFlight,
	bool OpenDetails,
	string? File)
{
	/// <summary>
	/// Semantic processing state for captures. Default preserves the legacy
	/// <see cref="ProcessingSeedCount"/> behavior.
	/// </summary>
	public ProcessingCaptureScenario ProcessingScenario { get; init; } = ProcessingCaptureScenario.Default;
	public ReducedMotionPreference Motion { get; init; } = ReducedMotionPreference.Full;
	public double LogicalScale { get; init; } = 1d;
	public bool OpenDecanter { get; init; }
	public bool FocusFailedProcessingItem { get; init; }
	public int OnboardingStep { get; init; } = 1;
	public bool OnboardingScanActive { get; init; }
	public CaptureFixture? Fixture { get; init; }
	public CaptureWindowSize WindowSize { get; init; } = CaptureWindowSize.Natural;
	public bool WaitForNativeClose { get; init; }
	public bool IsTopLevel => Surface is CaptureSurface.Dialog or CaptureSurface.Window or CaptureSurface.Message;
	public string FileName => File ?? CapturePlan.DefaultFileName(this);
}

public sealed record CapturePlan(int SettleMs, IReadOnlyList<CaptureEntry> Entries)
{
	public const int DefaultSettleMs = 800;
	public const int DefaultStageTimeoutMs = 30_000;
	public int StageTimeoutMs { get; init; } = DefaultStageTimeoutMs;

	private static readonly JsonSerializerOptions options = new()
	{
		PropertyNameCaseInsensitive = true,
		ReadCommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true,
	};

	public static string DefaultFileName(CaptureEntry entry)
	{
		var surface = entry.Surface switch
		{
			CaptureSurface.ComponentGallery => "componentgallery",
			CaptureSurface.Onboarding => $"onboarding-step{entry.OnboardingStep}",
			CaptureSurface.Dialog or CaptureSurface.Window or CaptureSurface.Message => $"{entry.Surface}-{entry.Fixture}-{entry.WindowSize}".ToLowerInvariant(),
			_ => entry.Route.ToString().ToLowerInvariant(),
		};
		return $"{entry.Profile.ToString().ToLowerInvariant()}-{surface}-{entry.Width}x{entry.Height}.png";
	}

	public static CapturePlan Load(string path)
		=> Parse(System.IO.File.ReadAllText(path));

	public static CapturePlan Parse(string json)
	{
		RawPlan? raw;
		try
		{
			raw = JsonSerializer.Deserialize<RawPlan>(json, options);
		}
		catch (JsonException ex)
		{
			throw new CapturePlanException($"Capture plan is not valid JSON: {ex.Message}");
		}

		if (raw?.Entries is not { Count: > 0 })
			throw new CapturePlanException("Capture plan has no entries.");

		var settleMs = raw.SettleMs ?? DefaultSettleMs;
		var stageTimeoutMs = raw.StageTimeoutMs ?? DefaultStageTimeoutMs;
		if (settleMs is < 0 or > 10_000)
			throw new CapturePlanException("settleMs must be between 0 and 10000.");
		if (stageTimeoutMs is < 100 or > 120_000)
			throw new CapturePlanException("stageTimeoutMs must be between 100 and 120000.");

		var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var entries = new List<CaptureEntry>(raw.Entries.Count);
		foreach (var entry in raw.Entries)
		{
			if (!TryParseDefined(entry.Profile, out ExperienceStyle profile))
				throw new CapturePlanException($"Unknown profile '{entry.Profile}'.");
			if (profile is ExperienceStyle.FollowSystem or ExperienceStyle.CurrentAvalonia)
				throw new CapturePlanException($"Capture profile '{profile}' is not an explicit contemporary profile; use Cellar, TastingRoom, or HighContrast.");
			var surface = entry.Surface is null ? CaptureSurface.Route
				: TryParseDefined(entry.Surface, out CaptureSurface parsedSurface) ? parsedSurface
				: throw new CapturePlanException($"Unknown surface '{entry.Surface}'.");
			var route = entry.Route is null && surface != CaptureSurface.Route
				? AppRouteId.Overview
				: TryParseDefined(entry.Route, out AppRouteId parsedRoute) ? parsedRoute
				: throw new CapturePlanException($"Unknown route '{entry.Route}'.");
			if (surface == CaptureSurface.Route && route == AppRouteId.About)
				throw new CapturePlanException($"Entry {entries.Count} ({entry.File ?? $"{profile}/About"}): About is a utility dialog with no capturable route body; use surface Window with fixture About for the inert informational window.");
			var isTopLevel = surface is CaptureSurface.Dialog or CaptureSurface.Window or CaptureSurface.Message;
			CaptureFixture? fixture = entry.Fixture is null ? null
				: TryParseDefined(entry.Fixture, out CaptureFixture parsedFixture) ? parsedFixture
				: throw new CapturePlanException($"Unknown fixture '{entry.Fixture}'.");
			var windowSize = entry.WindowSize is null ? CaptureWindowSize.Natural
				: TryParseDefined(entry.WindowSize, out CaptureWindowSize parsedWindowSize) ? parsedWindowSize
				: throw new CapturePlanException($"Unknown windowSize '{entry.WindowSize}'.");
			if (isTopLevel && (surface, fixture) is not
				((CaptureSurface.Dialog, CaptureFixture.Settings) or (CaptureSurface.Window, CaptureFixture.About) or (CaptureSurface.Message, CaptureFixture.RemoveConfirmation)))
				throw new CapturePlanException($"Entry {profile}/{surface} requires its explicit supported fixture: Dialog/Settings, Window/About, or Message/RemoveConfirmation.");
			if (!isTopLevel && (fixture is not null || entry.WindowSize is not null || entry.WaitForNativeClose))
				throw new CapturePlanException("fixture, windowSize, and waitForNativeClose are only supported for Dialog, Window, and Message captures.");
			if (isTopLevel && (entry.Route is not null || entry.FlightSelectionCount != 0 || entry.ProcessingSeedCount != 0
				|| entry.ProcessingScenario is not null || entry.OpenFlight || entry.OpenDetails || entry.OpenDecanter
				|| entry.FocusFailedProcessingItem || entry.OnboardingScanActive || entry.OnboardingStep != 1 || entry.LibraryView is not null))
				throw new CapturePlanException("Top-level fixtures cannot request route, queue, selection, onboarding, or library-view state.");
			if (entry.Width < (isTopLevel ? 265 : 720) || entry.Height < (isTopLevel ? 110 : 560))
				throw new CapturePlanException($"Entry {profile}/{surface} is below its minimum capture viewport.");
			if (entry.FlightSelectionCount < 0)
				throw new CapturePlanException($"Entry {profile}/{surface} has a negative Flight selection count.");
			if (entry.ProcessingSeedCount < 0)
				throw new CapturePlanException($"Entry {profile}/{surface} has a negative processing seed count.");
			if (surface == CaptureSurface.Onboarding && entry.OnboardingStep is < 1 or > 5)
				throw new CapturePlanException($"Entry {profile}/{surface} has onboarding step {entry.OnboardingStep}; expected 1 through 5.");

			var density = entry.Density is null ? DensityMode.Comfortable
				: TryParseDefined(entry.Density, out DensityMode parsedDensity) ? parsedDensity
				: throw new CapturePlanException($"Unknown density '{entry.Density}'.");
			var decoration = entry.Decoration is null ? DecorationLevel.Full
				: TryParseDefined(entry.Decoration, out DecorationLevel parsedDecoration) ? parsedDecoration
				: throw new CapturePlanException($"Unknown decoration '{entry.Decoration}'.");
			LibraryViewMode? libraryView = entry.LibraryView is null ? null
				: TryParseDefined(entry.LibraryView, out LibraryViewMode parsedLibraryView) ? parsedLibraryView
				: throw new CapturePlanException($"Unknown library view '{entry.LibraryView}'.");
			var processingScenario = entry.ProcessingScenario is null ? ProcessingCaptureScenario.Default
				: TryParseDefined(entry.ProcessingScenario, out ProcessingCaptureScenario parsedProcessingScenario) ? parsedProcessingScenario
				: throw new CapturePlanException($"Unknown processing scenario '{entry.ProcessingScenario}'.");
			var motion = entry.Motion is null ? ReducedMotionPreference.Full
				: TryParseDefined(entry.Motion, out ReducedMotionPreference parsedMotion) ? parsedMotion
				: throw new CapturePlanException($"Unknown motion preference '{entry.Motion}'.");
			var logicalScale = entry.LogicalScale ?? 1d;
			if (isTopLevel && logicalScale != 1d)
				throw new CapturePlanException("Top-level fixtures use native rendering; logicalScale must be 1.");
			if (!double.IsFinite(logicalScale) || logicalScale <= 0)
				throw new CapturePlanException($"Entry {profile}/{surface} has invalid logical scale {logicalScale}.");

			var captureEntry = new CaptureEntry(
				profile,
				surface,
				route,
				entry.Width,
				entry.Height,
				density,
				decoration,
				libraryView,
				entry.FlightSelectionCount,
				entry.ProcessingSeedCount,
				entry.OpenFlight,
				entry.OpenDetails,
				entry.File)
			{
				Fixture = fixture,
				WindowSize = windowSize,
				WaitForNativeClose = entry.WaitForNativeClose,
				ProcessingScenario = processingScenario,
				Motion = motion,
				LogicalScale = logicalScale,
				OpenDecanter = entry.OpenDecanter,
				FocusFailedProcessingItem = entry.FocusFailedProcessingItem,
				OnboardingStep = entry.OnboardingStep,
				OnboardingScanActive = entry.OnboardingScanActive,
			};
			var name = captureEntry.FileName;
			if (string.IsNullOrWhiteSpace(name) || Path.IsPathRooted(name)
				|| name.Split('/')[0].Equals("capture-diagnostics", StringComparison.OrdinalIgnoreCase)
				|| name.Contains('\\') || name.Contains(':') || name.Split('/').Any(segment => segment is "" or "." or "..")
				|| name.Any(char.IsControl) || !name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
				throw new CapturePlanException($"Entry {entries.Count} has an invalid relative PNG file name '{name}'.");
			if (!names.Add(name))
				throw new CapturePlanException($"Entry {entries.Count} duplicates capture file '{name}'; each entry needs a unique file name.");
			entries.Add(captureEntry);
		}

		return new CapturePlan(settleMs, entries) { StageTimeoutMs = stageTimeoutMs };
	}

	private static bool TryParseDefined<TEnum>(string? value, out TEnum parsed)
		where TEnum : struct, Enum
		=> Enum.TryParse(value, ignoreCase: true, out parsed) && Enum.IsDefined(parsed);

	private sealed class RawPlan
	{
		[JsonPropertyName("settleMs")] public int? SettleMs { get; set; }
		[JsonPropertyName("stageTimeoutMs")] public int? StageTimeoutMs { get; set; }
		[JsonPropertyName("entries")] public List<RawEntry>? Entries { get; set; }
	}

	private sealed class RawEntry
	{
		[JsonPropertyName("profile")] public string? Profile { get; set; }
		[JsonPropertyName("surface")] public string? Surface { get; set; }
		[JsonPropertyName("fixture")] public string? Fixture { get; set; }
		[JsonPropertyName("windowSize")] public string? WindowSize { get; set; }
		[JsonPropertyName("waitForNativeClose")] public bool WaitForNativeClose { get; set; }
		[JsonPropertyName("route")] public string? Route { get; set; }
		[JsonPropertyName("width")] public int Width { get; set; }
		[JsonPropertyName("height")] public int Height { get; set; }
		[JsonPropertyName("density")] public string? Density { get; set; }
		[JsonPropertyName("decoration")] public string? Decoration { get; set; }
		[JsonPropertyName("libraryView")] public string? LibraryView { get; set; }
		[JsonPropertyName("flightSelectionCount")] public int FlightSelectionCount { get; set; }
		[JsonPropertyName("processingSeedCount")] public int ProcessingSeedCount { get; set; }
		[JsonPropertyName("processingScenario")] public string? ProcessingScenario { get; set; }
		[JsonPropertyName("motion")] public string? Motion { get; set; }
		[JsonPropertyName("logicalScale")] public double? LogicalScale { get; set; }
		[JsonPropertyName("openFlight")] public bool OpenFlight { get; set; }
		[JsonPropertyName("openDetails")] public bool OpenDetails { get; set; }
		[JsonPropertyName("openDecanter")] public bool OpenDecanter { get; set; }
		[JsonPropertyName("focusFailedProcessingItem")] public bool FocusFailedProcessingItem { get; set; }
		[JsonPropertyName("onboardingStep")] public int OnboardingStep { get; set; } = 1;
		[JsonPropertyName("onboardingScanActive")] public bool OnboardingScanActive { get; set; }
		[JsonPropertyName("file")] public string? File { get; set; }
	}
}
