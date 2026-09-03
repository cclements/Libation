using Dinah.Core;
using LibationAvalonia.ViewModels;
using LibationFileManager;
using LibationUiBase;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace LibationAvalonia.Features.Settings;

/// <summary>
/// Local appearance draft. Preview and navigation through this object never write
/// configuration; Apply and Reset each use the configuration owner's atomic batch.
/// </summary>
public sealed class AppearanceSettingsViewModel : ViewModelBase, IDisposable
{
	private readonly Configuration configuration;
	private bool synchronizing;
	private bool applying;
	private bool disposed;

	public AppearanceSettingsViewModel(Configuration configuration)
	{
		ArgumentNullException.ThrowIfNull(configuration);
		this.configuration = configuration;
		BaseStyles =
		[
			new(ExperienceStyle.FollowSystem),
			new(ExperienceStyle.Cellar),
			new(ExperienceStyle.TastingRoom),
		];
		DensityModes = Enum.GetValues<DensityMode>().Select(value => new EnumDisplay<DensityMode>(value)).ToArray();
		DecorationLevels = Enum.GetValues<DecorationLevel>().Select(value => new EnumDisplay<DecorationLevel>(value)).ToArray();
		MotionPreferences = Enum.GetValues<ReducedMotionPreference>().Select(value => new EnumDisplay<ReducedMotionPreference>(value)).ToArray();
		LibraryViews = Enum.GetValues<LibraryViewMode>().Select(value => new EnumDisplay<LibraryViewMode>(value)).ToArray();
		NavigationRails = Enum.GetValues<NavigationRailPreference>().Select(value => new EnumDisplay<NavigationRailPreference>(value)).ToArray();
		SelectCellarCommand = ReactiveCommand.Create(() => SelectBaseStyle(ExperienceStyle.Cellar));
		SelectTastingRoomCommand = ReactiveCommand.Create(() => SelectBaseStyle(ExperienceStyle.TastingRoom));
		SynchronizeFromConfiguration();
	}

	public IReadOnlyList<EnumDisplay<ExperienceStyle>> BaseStyles { get; }
	public IReadOnlyList<EnumDisplay<DensityMode>> DensityModes { get; }
	public IReadOnlyList<EnumDisplay<DecorationLevel>> DecorationLevels { get; }
	public IReadOnlyList<EnumDisplay<ReducedMotionPreference>> MotionPreferences { get; }
	public IReadOnlyList<EnumDisplay<LibraryViewMode>> LibraryViews { get; }
	public IReadOnlyList<EnumDisplay<NavigationRailPreference>> NavigationRails { get; }

	public EnumDisplay<ExperienceStyle> SelectedBaseStyle
	{
		get => field;
		set
		{
			if (value is null || Equals(field, value))
				return;
			this.RaiseAndSetIfChanged(ref field, value);
			RaiseDraftState();
		}
	} = null!;

	public bool IsHighContrast
	{
		get => field;
		set
		{
			if (value == field)
				return;
			this.RaiseAndSetIfChanged(ref field, value);
			RaiseDraftState();
		}
	}

	public EnumDisplay<DensityMode> SelectedDensityMode
	{
		get => field;
		set
		{
			if (value is null || Equals(field, value)) return;
			this.RaiseAndSetIfChanged(ref field, value);
			RaiseDraftState();
		}
	} = null!;

	public EnumDisplay<DecorationLevel> SelectedDecorationLevel
	{
		get => field;
		set
		{
			if (value is null || Equals(field, value)) return;
			this.RaiseAndSetIfChanged(ref field, value);
			RaiseDraftState();
		}
	} = null!;

	public EnumDisplay<ReducedMotionPreference> SelectedMotionPreference
	{
		get => field;
		set
		{
			if (value is null || Equals(field, value)) return;
			this.RaiseAndSetIfChanged(ref field, value);
			RaiseDraftState();
		}
	} = null!;

	public EnumDisplay<LibraryViewMode> SelectedLibraryView
	{
		get => field;
		set
		{
			if (value is null || Equals(field, value)) return;
			this.RaiseAndSetIfChanged(ref field, value);
			RaiseDraftState();
		}
	} = null!;

	public EnumDisplay<NavigationRailPreference> SelectedNavigationRail
	{
		get => field;
		set
		{
			if (value is null || Equals(field, value)) return;
			this.RaiseAndSetIfChanged(ref field, value);
			RaiseDraftState();
		}
	} = null!;

	public bool UseSystemTypography
	{
		get => field;
		set { if (value != field) { this.RaiseAndSetIfChanged(ref field, value); RaiseDraftState(); } }
	}
	public bool ShowDecanterDock
	{
		get => field;
		set { if (value != field) { this.RaiseAndSetIfChanged(ref field, value); RaiseDraftState(); } }
	}
	public bool PersistFlightBetweenSessions
	{
		get => field;
		set { if (value != field) { this.RaiseAndSetIfChanged(ref field, value); RaiseDraftState(); } }
	}

	public ExperienceStyle EffectiveStyle => IsHighContrast ? ExperienceStyle.HighContrast : SelectedBaseStyle.Value;
	public DensityMode Density => SelectedDensityMode.Value;
	public DecorationLevel Decoration => SelectedDecorationLevel.Value;
	public ReducedMotionPreference Motion => SelectedMotionPreference.Value;
	public bool IsCellarSelected => !IsHighContrast && SelectedBaseStyle.Value == ExperienceStyle.Cellar;
	public bool IsTastingRoomSelected => !IsHighContrast && SelectedBaseStyle.Value == ExperienceStyle.TastingRoom;
	public string SelectedStyleText => EffectiveStyle switch
	{
		ExperienceStyle.HighContrast => global::LibationAvalonia.Properties.Resources.OnboardingViewHighContrast,
		ExperienceStyle.TastingRoom => global::LibationAvalonia.Properties.Resources.OnboardingViewTastingRoom,
		ExperienceStyle.Cellar => global::LibationAvalonia.Properties.Resources.OnboardingViewCellar,
		_ => global::LibationAvalonia.Properties.Resources.OnboardingViewFollowSystem,
	};
	public string DraftStateText => HasUnsavedChanges
		? global::LibationAvalonia.Properties.Resources.AppearanceSettingsViewModelAppearanceChangesAreReadyToApply
		: global::LibationAvalonia.Properties.Resources.AppearanceSettingsViewModelAppearanceMatchesTheSavedSettings;
	public bool HasUnsavedChanges
	{
		get
		{
			var saved = configuration.GetContemporaryExperienceSettings();
			return saved.ExperienceStyle != EffectiveStyle
				|| saved.DensityMode != SelectedDensityMode.Value
				|| saved.DecorationLevel != SelectedDecorationLevel.Value
				|| saved.ReducedMotionPreference != SelectedMotionPreference.Value
				|| saved.UseSystemTypography != UseSystemTypography
				|| saved.LibraryViewMode != SelectedLibraryView.Value
				|| saved.NavigationRailPreference != SelectedNavigationRail.Value
				|| saved.ShowDecanterDock != ShowDecanterDock
				|| saved.PersistFlightBetweenSessions != PersistFlightBetweenSessions
				|| !saved.UseContemporaryShell;
		}
	}

	public ICommand SelectCellarCommand { get; }
	public ICommand SelectTastingRoomCommand { get; }

	public void Apply()
	{
		ObjectDisposedException.ThrowIf(disposed, this);
		var saved = configuration.GetContemporaryExperienceSettings();
		applying = true;
		try
		{
			configuration.SaveContemporaryExperienceSettings(saved with
			{
				ExperienceStyle = EffectiveStyle,
				DensityMode = SelectedDensityMode.Value,
				DecorationLevel = SelectedDecorationLevel.Value,
				ReducedMotionPreference = SelectedMotionPreference.Value,
				UseSystemTypography = UseSystemTypography,
				LibraryViewMode = SelectedLibraryView.Value,
				NavigationRailPreference = SelectedNavigationRail.Value,
				ShowDecanterDock = ShowDecanterDock,
				PersistFlightBetweenSessions = PersistFlightBetweenSessions,
				UseContemporaryShell = true,
			});
		}
		finally
		{
			applying = false;
			RaiseDraftState();
		}
	}

	public void ResetAndApply()
	{
		ObjectDisposedException.ThrowIf(disposed, this);
		synchronizing = true;
		try
		{
			SelectedBaseStyle = Find(BaseStyles, ExperienceStyle.FollowSystem);
			IsHighContrast = false;
			SelectedDensityMode = Find(DensityModes, DensityMode.Comfortable);
			SelectedDecorationLevel = Find(DecorationLevels, DecorationLevel.Full);
			SelectedMotionPreference = Find(MotionPreferences, ReducedMotionPreference.FollowSystem);
			UseSystemTypography = false;
			SelectedLibraryView = Find(LibraryViews, LibraryViewMode.Details);
			SelectedNavigationRail = Find(NavigationRails, NavigationRailPreference.Automatic);
			ShowDecanterDock = true;
			PersistFlightBetweenSessions = false;
		}
		finally
		{
			synchronizing = false;
		}
		RaiseDraftState();
		Apply();
	}

	public void SynchronizeFromConfiguration()
	{
		if (disposed || applying)
			return;
		var saved = configuration.GetContemporaryExperienceSettings();
		var rememberedBase = SelectedBaseStyle?.Value is ExperienceStyle.Cellar or ExperienceStyle.TastingRoom or ExperienceStyle.FollowSystem
			? SelectedBaseStyle.Value
			: ExperienceStyle.FollowSystem;
		var baseStyle = saved.ExperienceStyle == ExperienceStyle.HighContrast
			? rememberedBase
			: NormalizeBaseStyle(saved.ExperienceStyle);

		synchronizing = true;
		try
		{
			SelectedBaseStyle = Find(BaseStyles, baseStyle);
			IsHighContrast = saved.ExperienceStyle == ExperienceStyle.HighContrast;
			SelectedDensityMode = Find(DensityModes, saved.DensityMode);
			SelectedDecorationLevel = Find(DecorationLevels, saved.DecorationLevel);
			SelectedMotionPreference = Find(MotionPreferences, saved.ReducedMotionPreference);
			UseSystemTypography = saved.UseSystemTypography;
			SelectedLibraryView = Find(LibraryViews, saved.LibraryViewMode);
			SelectedNavigationRail = Find(NavigationRails, saved.NavigationRailPreference);
			ShowDecanterDock = saved.ShowDecanterDock;
			PersistFlightBetweenSessions = saved.PersistFlightBetweenSessions;
		}
		finally
		{
			synchronizing = false;
		}
		RaiseDraftState();
	}

	private void SelectBaseStyle(ExperienceStyle style)
	{
		SelectedBaseStyle = Find(BaseStyles, style);
		IsHighContrast = false;
	}

	private void RaiseDraftState()
	{
		if (synchronizing)
			return;
		foreach (var property in new[]
		{
			nameof(EffectiveStyle), nameof(Density), nameof(Decoration), nameof(Motion),
			nameof(IsCellarSelected), nameof(IsTastingRoomSelected), nameof(SelectedStyleText),
			nameof(HasUnsavedChanges), nameof(DraftStateText),
		})
			this.RaisePropertyChanged(property);
	}

	private static ExperienceStyle NormalizeBaseStyle(ExperienceStyle style)
		=> style is ExperienceStyle.Cellar or ExperienceStyle.TastingRoom or ExperienceStyle.FollowSystem
			? style
			: ExperienceStyle.FollowSystem;

	private static EnumDisplay<T> Find<T>(IEnumerable<EnumDisplay<T>> values, T value) where T : struct, Enum
		=> values.Single(item => EqualityComparer<T>.Default.Equals(item.Value, value));

	public void Dispose()
	{
		if (disposed)
			return;
		disposed = true;
		(SelectCellarCommand as IDisposable)?.Dispose();
		(SelectTastingRoomCommand as IDisposable)?.Dispose();
	}
}
