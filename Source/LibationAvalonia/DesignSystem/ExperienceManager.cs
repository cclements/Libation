using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Dinah.Core;
using LibationAvalonia.Themes;
using LibationFileManager;
using System;
using System.Collections.Frozen;
using System.ComponentModel;
using System.Threading;

namespace LibationAvalonia.DesignSystem;

/// <summary>
/// Resolves and applies presentation resources. It never creates or replaces
/// library, selection, queue, or other domain state.
/// </summary>
public sealed class ExperienceManager : IDisposable
{
	private static readonly Uri ResourceBase = new("avares://Libation/");
	private static readonly FrozenSet<string> ObservedSettings = new[]
	{
		nameof(Configuration.UseContemporaryShell),
		nameof(Configuration.ExperienceStyle),
		nameof(Configuration.ThemeVariant),
		nameof(Configuration.DensityMode),
		nameof(Configuration.DecorationLevel),
		nameof(Configuration.ReducedMotionPreference),
		nameof(Configuration.UseSystemTypography),
	}.ToFrozenSet(StringComparer.Ordinal);

	private readonly Application application;
	private readonly Configuration configuration;
	private readonly ThemeResourceValidator validator;
	private readonly ISystemReducedMotionResolver reducedMotionResolver;
	private IResourceProvider? activeExperienceResources;
	private bool applying;
	private bool disposed;
	private int applyScheduled;

	public ExperienceManager(
		Application application,
		Configuration configuration,
		ThemeResourceValidator? validator = null,
		ISystemReducedMotionResolver? reducedMotionResolver = null)
	{
		this.application = application;
		this.configuration = configuration;
		this.validator = validator ?? new ThemeResourceValidator();
		this.reducedMotionResolver = reducedMotionResolver ?? new PlatformSystemReducedMotionResolver();
		CurrentProfile = ExperienceCatalog.Resolve(ExperienceStyle.CurrentAvalonia, application.ActualThemeVariant);
	}

	public ExperienceProfile CurrentProfile { get; private set; }
	public ExperienceStyle SelectedStyle => configuration.ExperienceStyle;
	public bool IsContemporaryShellEnabled => configuration.UseContemporaryShell;
	public bool IsReducedMotionEnabled => configuration.ReducedMotionPreference switch
	{
		ReducedMotionPreference.Reduce => true,
		ReducedMotionPreference.Full => false,
		_ => reducedMotionResolver.IsReducedMotionPreferred ?? false,
	};
	public event EventHandler<ExperienceProfileChangedEventArgs>? ProfileChanged;

	public void RefreshSystemPreferences() => reducedMotionResolver.Refresh();

	public void Initialize()
	{
		ObjectDisposedException.ThrowIf(disposed, this);
		if (configuration.UseContemporaryShell)
			App.DefaultThemeColors?.PrepareFluentVariants();
		configuration.PropertyChanged += Configuration_PropertyChanged;
		application.ActualThemeVariantChanged += Application_ActualThemeVariantChanged;
		reducedMotionResolver.PreferenceChanged += ReducedMotionResolver_PreferenceChanged;
		ApplyRequestedProfile();
	}

	public ExperiencePreviewScope CreatePreviewScope(ExperienceStyle requestedStyle)
		=> CreatePreviewScope(
			requestedStyle,
			configuration.DensityMode,
			configuration.DecorationLevel,
			configuration.ReducedMotionPreference,
			configuration.UseSystemTypography);

	public ExperiencePreviewScope CreatePreviewScope(
		ExperienceStyle requestedStyle,
		DensityMode density,
		DecorationLevel decoration,
		ReducedMotionPreference motion,
		bool useSystemTypography)
	{
		ObjectDisposedException.ThrowIf(disposed, this);
		var resolutionTheme = requestedStyle == ExperienceStyle.FollowSystem
			? GetPlatformThemeVariant()
			: application.ActualThemeVariant;
		var profile = ExperienceCatalog.Resolve(requestedStyle, resolutionTheme);
		var resources = CreateExperienceResources(profile, density, decoration, motion, useSystemTypography);
		var requestedTheme = PreviewTheme(profile);
		validator.ValidateResources(resources, requestedTheme, profile.Style);
		return new ExperiencePreviewScope(profile, resources, requestedTheme);
	}

	private ThemeVariant GetPlatformThemeVariant()
	{
		try
		{
			return application.PlatformSettings?.GetColorValues().ThemeVariant == PlatformThemeVariant.Dark
				? ThemeVariant.Dark
				: ThemeVariant.Light;
		}
		catch (Exception ex)
		{
			StartupLog.Warning(ex, "The operating-system appearance could not be read for a profile preview. Libation will use the active appearance for the preview.");
			return application.ActualThemeVariant;
		}
	}

	private void ReducedMotionResolver_PreferenceChanged(object? sender, EventArgs e)
	{
		if (configuration.ReducedMotionPreference == ReducedMotionPreference.FollowSystem)
			ScheduleApplyRequestedProfile();
	}

	private void Configuration_PropertyChanged(object sender, PropertyChangedEventArgsEx e)
	{
		if (e.PropertyName is not null && ObservedSettings.Contains(e.PropertyName))
			ScheduleApplyRequestedProfile();
	}

	private void Application_ActualThemeVariantChanged(object? sender, EventArgs e)
	{
		if (!applying && (!configuration.UseContemporaryShell || configuration.ExperienceStyle == ExperienceStyle.FollowSystem))
			ScheduleApplyRequestedProfile();
	}

	private void ScheduleApplyRequestedProfile()
	{
		if (disposed || Interlocked.Exchange(ref applyScheduled, 1) != 0)
			return;
		Dispatcher.UIThread.Post(() =>
		{
			Interlocked.Exchange(ref applyScheduled, 0);
			ApplyRequestedProfile();
		}, DispatcherPriority.Normal);
	}

	private void ApplyRequestedProfile()
	{
		if (applying || disposed)
			return;

		if (configuration.UseContemporaryShell && configuration.ExperienceStyle == ExperienceStyle.CurrentAvalonia)
		{
			StartupLog.Warning("The current-interface appearance cannot host the contemporary shell. Libation restored the current interface setting.");
			configuration.UseContemporaryShell = false;
		}

		var requestedStyle = configuration.UseContemporaryShell
			? configuration.ExperienceStyle
			: ExperienceStyle.CurrentAvalonia;
		bool followsSystem = configuration.UseContemporaryShell && requestedStyle == ExperienceStyle.FollowSystem;
		if (followsSystem && application.RequestedThemeVariant != ThemeVariant.Default)
		{
			applying = true;
			try
			{
				application.RequestedThemeVariant = ThemeVariant.Default;
			}
			finally
			{
				applying = false;
			}
		}
		var requestedProfile = ExperienceCatalog.Resolve(requestedStyle, application.ActualThemeVariant);

		try
		{
			ApplyProfile(requestedProfile, followsSystem: followsSystem);
		}
		catch (Exception ex)
		{
			StartupLog.Warning(ex, $"The '{requestedProfile.DisplayName}' appearance could not be loaded. Libation will use a safe fallback for this session.");
			TryApplyFallback(requestedProfile.Style);
		}
	}

	private void TryApplyFallback(ExperienceStyle failedStyle)
	{
		if (failedStyle != ExperienceStyle.HighContrast)
		{
			try
			{
				ApplyProfile(ExperienceCatalog.Resolve(ExperienceStyle.HighContrast, ThemeVariant.Dark));
				return;
			}
			catch (Exception highContrastFailure)
			{
				StartupLog.Warning(highContrastFailure, "The High Contrast appearance could not be loaded. Libation will use the current interface for this session.");
			}
		}

		try
		{
			ApplyProfile(ExperienceCatalog.Resolve(ExperienceStyle.CurrentAvalonia, application.ActualThemeVariant), validateActiveResources: false);
		}
		catch (Exception currentFailure)
		{
			StartupLog.Error(currentFailure, "The current interface appearance could not be restored. Libation will continue with its existing resources.");
		}
		finally
		{
			// MainWindow selects its presentation from this persisted escape hatch.
			// A semantic-resource fallback is incomplete if the contemporary shell
			// remains attached over current-interface resources.
			if (configuration.UseContemporaryShell)
				configuration.UseContemporaryShell = false;
		}
	}

	private void ApplyProfile(ExperienceProfile profile, bool validateActiveResources = true, bool followsSystem = false)
	{
		applying = true;
		var previousRequestedTheme = application.RequestedThemeVariant;
		var previousResources = activeExperienceResources;
		var previousProfile = CurrentProfile;
		ResourceDictionary? candidate = null;
		bool profileChanged = false;
		try
		{
			if (profile.Style == ExperienceStyle.CurrentAvalonia)
			{
				using ChardonnayThemePersister? themePersister = ChardonnayThemePersister.Create();
				themePersister?.Target.ApplyTheme(configuration.ThemeVariant);
			}
			else
			{
				// Modern profiles start from the captured, unmodified Fluent palette so
				// Chardonnay overrides cannot bleed into their standard controls. The
				// persisted Chardonnay file is deliberately left untouched.
				App.DefaultThemeColors?.ApplyTheme(profile.ThemeVariant);
				application.RequestedThemeVariant = followsSystem ? ThemeVariant.Default : profile.ThemeVariant;
			}

			var validationTheme = PreviewTheme(profile);
			candidate = CreateExperienceResources(
				profile,
				configuration.DensityMode,
				configuration.DecorationLevel,
				configuration.ReducedMotionPreference,
				configuration.UseSystemTypography);
			// Validate the candidate in isolation so a key from the previous active
			// profile cannot make an incomplete replacement appear valid.
			validator.ValidateResources(candidate, validationTheme, profile.Style);
			application.Resources.MergedDictionaries.Add(candidate);

			if (validateActiveResources)
				validator.ValidateActiveResources(application, validationTheme, profile.Style);

			if (previousResources is not null)
				application.Resources.MergedDictionaries.Remove(previousResources);
			activeExperienceResources = candidate;

			CurrentProfile = profile;
			profileChanged = previousProfile != profile;
		}
		catch
		{
			if (candidate is not null)
				application.Resources.MergedDictionaries.Remove(candidate);
			application.RequestedThemeVariant = previousRequestedTheme;
			activeExperienceResources = previousResources;
			CurrentProfile = previousProfile;
			if (previousProfile.Style == ExperienceStyle.CurrentAvalonia)
			{
				using ChardonnayThemePersister? themePersister = ChardonnayThemePersister.Create();
				themePersister?.Target.ApplyTheme(configuration.ThemeVariant);
			}
			else
			{
				App.DefaultThemeColors?.ApplyTheme(CurrentProfile.ThemeVariant);
				application.RequestedThemeVariant = previousRequestedTheme;
			}
			throw;
		}
		finally
		{
			applying = false;
		}

		// Subscribers are consumers of committed presentation state. A faulty
		// subscriber is logged but cannot roll back the resource transaction.
		if (profileChanged)
		{
			try
			{
				ProfileChanged?.Invoke(this, new(previousProfile, profile));
			}
			catch (Exception subscriberFailure)
			{
				StartupLog.Error(subscriberFailure, "A profile-change listener failed after the appearance was applied.");
			}
		}
	}

	private ResourceDictionary CreateExperienceResources(
		ExperienceProfile profile,
		DensityMode density,
		DecorationLevel decoration,
		ReducedMotionPreference motion,
		bool useSystemTypography)
	{
		var resources = new ResourceDictionary();
		foreach (var source in new[]
		{
			new Uri("avares://Libation/DesignSystem/Tokens/ColorTokens.axaml"),
			new Uri("avares://Libation/DesignSystem/Tokens/MetricTokens.axaml"),
			new Uri("avares://Libation/DesignSystem/Tokens/TypographyTokens.axaml"),
			new Uri("avares://Libation/DesignSystem/Tokens/MotionTokens.axaml"),
		})
			resources.MergedDictionaries.Add(CreateInclude(source));

		var palette = CreatePalette(profile);
		validator.ValidatePalette(GetLoadedPalette(palette), PreviewTheme(profile), profile.Style);
		resources.MergedDictionaries.Add(palette);
		resources.MergedDictionaries.Add(CreateProfileAssetAliases(profile));
		resources.MergedDictionaries.Add(CreatePreferenceResources(
			profile,
			density,
			decoration,
			motion,
			useSystemTypography,
			resources,
			PreviewTheme(profile)));
		return resources;
	}

	private ResourceDictionary CreateProfileAssetAliases(ExperienceProfile profile)
	{
		string libraryEmpty = profile.DashboardLayout == DashboardLayoutKind.Cellar
			? "illustration.cellar.empty-library"
			: "illustration.tasting-room.add-books";
		string decanterEmpty = profile.DashboardLayout == DashboardLayoutKind.Cellar
			? "illustration.cellar.empty-decanter"
			: "illustration.tasting-room.empty-decanter";
		return new ResourceDictionary
		{
			["illustration.library.empty"] = ResolveAssetTemplate(libraryEmpty, profile),
			["illustration.decanter.empty"] = ResolveAssetTemplate(decanterEmpty, profile),
		};
	}

	private IControlTemplate ResolveAssetTemplate(string key, ExperienceProfile profile)
	{
		if (!application.TryGetResource(key, PreviewTheme(profile), out var value) || value is not IControlTemplate template)
			throw new InvalidOperationException($"The {profile.DisplayName} experience asset '{key}' is missing or is not an IControlTemplate.");
		return template;
	}

	private ResourceDictionary CreatePreferenceResources(
		ExperienceProfile profile,
		DensityMode density,
		DecorationLevel decoration,
		ReducedMotionPreference motion,
		bool useSystemTypography,
		IResourceNode sharedResources,
		ThemeVariant theme)
	{
		bool compact = density == DensityMode.Compact;
		bool forceAccessiblePresentation = profile.Style == ExperienceStyle.HighContrast;
		double decorationOpacity = forceAccessiblePresentation ? 0 : decoration switch
		{
			DecorationLevel.Off => 0,
			DecorationLevel.Reduced => 0.45,
			_ => 1,
		};
		bool reduceMotion = motion switch
		{
			ReducedMotionPreference.Reduce => true,
			ReducedMotionPreference.Full => false,
			_ => reducedMotionResolver.IsReducedMotionPreferred ?? false,
		};
		double motionScale = reduceMotion ? 0 : 1;
		double fastDuration = ResolveDoubleToken(sharedResources, theme, "Libation.Motion.Duration.Fast");
		double defaultDuration = ResolveDoubleToken(sharedResources, theme, "Libation.Motion.Duration.Default");
		double deliberateDuration = ResolveDoubleToken(sharedResources, theme, "Libation.Motion.Duration.Deliberate");

		var resources = new ResourceDictionary
		{
			["Libation.Density.RowHeight"] = compact ? 44d : 56d,
			["Libation.Density.CardPadding"] = compact ? new Thickness(12) : new Thickness(16),
			["Libation.Density.ToolbarGap"] = compact ? 8d : 12d,
			["Libation.Density.QueueItemHeight"] = compact ? 60d : 72d,
			["Libation.Density.MetadataOpacity"] = compact ? 0.82d : 1d,
			["Libation.Decoration.Opacity"] = decorationOpacity,
			["Libation.Decoration.Visible"] = !forceAccessiblePresentation && decoration != DecorationLevel.Off,
			["Libation.Motion.Scale"] = motionScale,
			["Libation.Motion.EffectiveDuration.Fast"] = TimeSpan.FromMilliseconds(fastDuration * motionScale),
			["Libation.Motion.EffectiveDuration.Default"] = TimeSpan.FromMilliseconds(defaultDuration * motionScale),
			["Libation.Motion.EffectiveDuration.Deliberate"] = TimeSpan.FromMilliseconds(deliberateDuration * motionScale),
			["Libation.Font.Display"] = useSystemTypography
				? FontFamily.Default
				: new FontFamily("avares://Libation/Assets/Fonts#Source Serif 4, Georgia, Times New Roman, Noto Serif"),
		};

		if (forceAccessiblePresentation)
		{
			resources["Libation.Shadow.Low"] = default(BoxShadows);
			resources["Libation.Shadow.Medium"] = default(BoxShadows);
		}

		return resources;
	}

	private static double ResolveDoubleToken(IResourceNode resources, ThemeVariant theme, string key)
	{
		if (resources.TryGetResource(key, theme, out var value) && value is double number)
			return number;
		throw new InvalidOperationException($"The shared motion token '{key}' is missing or is not a Double.");
	}

	private IResourceProvider CreatePalette(ExperienceProfile profile)
	{
		if (profile.Style == ExperienceStyle.CurrentAvalonia)
			return CreateCurrentSemanticPalette();
		if (profile.PaletteResource is null)
			throw new InvalidOperationException($"The {profile.DisplayName} experience does not declare a palette resource.");
		return CreateInclude(profile.PaletteResource);
	}

	private ResourceDictionary CreateCurrentSemanticPalette()
	{
		Color region = ResolveCurrentColor("SystemRegionColor");
		Color baseHigh = ResolveCurrentColor("SystemBaseHighColor");
		Color baseMedium = ResolveCurrentColor("SystemBaseMediumColor");
		Color baseLow = ResolveCurrentColor("SystemBaseLowColor");
		Color accent = ResolveCurrentColor("SystemAccentColor");
		Color onAccent = ResolveCurrentColor("SystemChromeAltLowColor");

		return new ResourceDictionary
		{
			["Libation.Color.Canvas"] = region,
			["Libation.Color.Sidebar"] = baseLow,
			["Libation.Color.Surface"] = region,
			["Libation.Color.SurfaceRaised"] = region,
			["Libation.Color.SurfaceSunken"] = baseLow,
			["Libation.Color.SurfaceInteractive"] = baseLow,
			["Libation.Color.TextPrimary"] = baseHigh,
			["Libation.Color.TextSecondary"] = baseMedium,
			["Libation.Color.TextTertiary"] = baseMedium,
			["Libation.Color.TextOnAccent"] = onAccent,
			["Libation.Color.BorderSubtle"] = baseLow,
			["Libation.Color.BorderStrong"] = baseMedium,
			["Libation.Color.AccentPrimary"] = accent,
			["Libation.Color.AccentSecondary"] = accent,
			["Libation.Color.AccentHover"] = accent,
			["Libation.Color.AccentPressed"] = accent,
			["Libation.Color.Focus"] = accent,
			["Libation.Color.Selection"] = accent,
			["Libation.Color.Success"] = ResolveCurrentColor("ProcessQueueBookCompletedBrush"),
			["Libation.Color.Warning"] = ResolveCurrentColor("ProcessQueueBookCancelledBrush"),
			["Libation.Color.Danger"] = ResolveCurrentColor("ProcessQueueBookFailedBrush"),
			["Libation.Color.Info"] = accent,
			["Libation.Color.ProgressTrack"] = baseLow,
			["Libation.Color.ProgressFill"] = accent,
			["Libation.Color.CoverPlaceholder"] = baseLow,
		};
	}

	private Color ResolveCurrentColor(string key)
	{
		if (!application.TryGetResource(key, application.ActualThemeVariant, out var value))
			throw new InvalidOperationException($"The current interface resource '{key}' could not be resolved.");
		return value switch
		{
			Color color => color,
			ISolidColorBrush brush => brush.Color,
			_ => throw new InvalidOperationException($"The current interface resource '{key}' must be a Color or solid brush, but is {value?.GetType().Name ?? "null"}."),
		};
	}

	private ThemeVariant PreviewTheme(ExperienceProfile profile)
		=> profile.ThemeVariant == ThemeVariant.Default ? application.ActualThemeVariant : profile.ThemeVariant;

	private static IResourceProvider GetLoadedPalette(IResourceProvider palette)
		=> palette is ResourceInclude include ? include.Loaded : palette;

	private static ResourceInclude CreateInclude(Uri source)
		=> new(ResourceBase) { Source = source };

	public void Dispose()
	{
		if (disposed)
			return;
		disposed = true;
		configuration.PropertyChanged -= Configuration_PropertyChanged;
		application.ActualThemeVariantChanged -= Application_ActualThemeVariantChanged;
		reducedMotionResolver.PreferenceChanged -= ReducedMotionResolver_PreferenceChanged;
		reducedMotionResolver.Dispose();
	}
}
