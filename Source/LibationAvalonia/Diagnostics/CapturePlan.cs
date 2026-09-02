using LibationAvalonia.Shell;
using LibationFileManager;
using System;
using System.Collections.Generic;
using System.IO;
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

	public static bool IsRequested
		=> !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PlanVariable));

	public static string PlanPath
		=> Environment.GetEnvironmentVariable(PlanVariable)
			?? throw new CapturePlanException($"{PlanVariable} is not set.");

	public static string OutputDirectory
		=> Environment.GetEnvironmentVariable(OutputVariable)
			?? Path.Combine(Path.GetDirectoryName(PlanPath) ?? ".", "captures");
}

public sealed record CaptureEntry(
	ExperienceStyle Profile,
	AppRouteId Route,
	int Width,
	int Height,
	DensityMode Density,
	DecorationLevel Decoration,
	string? File)
{
	public string FileName => File ?? CapturePlan.DefaultFileName(this);
}

public sealed record CapturePlan(int SettleMs, IReadOnlyList<CaptureEntry> Entries)
{
	public const int DefaultSettleMs = 800;

	private static readonly JsonSerializerOptions options = new()
	{
		PropertyNameCaseInsensitive = true,
		ReadCommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true,
	};

	public static string DefaultFileName(CaptureEntry entry)
		=> $"{entry.Profile.ToString().ToLowerInvariant()}-{entry.Route.ToString().ToLowerInvariant()}-{entry.Width}x{entry.Height}.png";

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

		var entries = new List<CaptureEntry>(raw.Entries.Count);
		foreach (var entry in raw.Entries)
		{
			if (!TryParseDefined(entry.Profile, out ExperienceStyle profile))
				throw new CapturePlanException($"Unknown profile '{entry.Profile}'.");
			if (!TryParseDefined(entry.Route, out AppRouteId route))
				throw new CapturePlanException($"Unknown route '{entry.Route}'.");
			if (entry.Width < 720 || entry.Height < 560)
				throw new CapturePlanException($"Entry {profile}/{route} is below the 720x560 minimum window.");

			var density = entry.Density is null ? DensityMode.Comfortable
				: TryParseDefined(entry.Density, out DensityMode parsedDensity) ? parsedDensity
				: throw new CapturePlanException($"Unknown density '{entry.Density}'.");
			var decoration = entry.Decoration is null ? DecorationLevel.Full
				: TryParseDefined(entry.Decoration, out DecorationLevel parsedDecoration) ? parsedDecoration
				: throw new CapturePlanException($"Unknown decoration '{entry.Decoration}'.");

			entries.Add(new CaptureEntry(
				profile,
				route,
				entry.Width,
				entry.Height,
				density,
				decoration,
				entry.File));
		}

		return new CapturePlan(raw.SettleMs ?? DefaultSettleMs, entries);
	}

	private static bool TryParseDefined<TEnum>(string? value, out TEnum parsed)
		where TEnum : struct, Enum
		=> Enum.TryParse(value, ignoreCase: true, out parsed) && Enum.IsDefined(parsed);

	private sealed class RawPlan
	{
		[JsonPropertyName("settleMs")] public int? SettleMs { get; set; }
		[JsonPropertyName("entries")] public List<RawEntry>? Entries { get; set; }
	}

	private sealed class RawEntry
	{
		[JsonPropertyName("profile")] public string? Profile { get; set; }
		[JsonPropertyName("route")] public string? Route { get; set; }
		[JsonPropertyName("width")] public int Width { get; set; }
		[JsonPropertyName("height")] public int Height { get; set; }
		[JsonPropertyName("density")] public string? Density { get; set; }
		[JsonPropertyName("decoration")] public string? Decoration { get; set; }
		[JsonPropertyName("file")] public string? File { get; set; }
	}
}
