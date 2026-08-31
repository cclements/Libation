using LibationFileManager;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace ContemporaryExperienceSettingsTests;

[TestClass]
[DoNotParallelize]
public class ContemporaryExperienceSettingsTests
{
	[TestCleanup]
	public void Cleanup() => Configuration.RestoreSingletonInstance();

	[TestMethod]
	public void Missing_settings_preserve_the_current_interface_and_backward_compatible_defaults()
	{
		var config = Configuration.CreateMockInstance();

		Assert.IsFalse(config.Exists(nameof(Configuration.UseContemporaryShell)));
		Assert.AreEqual(
			new ContemporaryExperienceSettings(
				ExperienceStyle.FollowSystem,
				DensityMode.Comfortable,
				DecorationLevel.Full,
				ReducedMotionPreference.FollowSystem,
				UseSystemTypography: false,
				LibraryViewMode.Details,
				NavigationRailPreference.Automatic,
				ShowDecanterDock: true,
				PersistFlightBetweenSessions: false,
				UseContemporaryShell: false),
			config.GetContemporaryExperienceSettings());
	}

	[TestMethod]
	[DataRow("FollowSystem", ExperienceStyle.FollowSystem)]
	[DataRow("Cellar", ExperienceStyle.Cellar)]
	[DataRow("TastingRoom", ExperienceStyle.TastingRoom)]
	[DataRow("CurrentAvalonia", ExperienceStyle.CurrentAvalonia)]
	[DataRow("HighContrast", ExperienceStyle.HighContrast)]
	public void Persisted_profile_names_parse(string persistedValue, ExperienceStyle expected)
	{
		var config = Configuration.CreateMockInstance();
		config.SetNonString(persistedValue, nameof(Configuration.ExperienceStyle));

		Assert.AreEqual(expected, config.ExperienceStyle);
	}

	[TestMethod]
	public void Experience_settings_round_trip_and_notify_shell_activation_last()
	{
		var config = Configuration.CreateMockInstance();
		var expected = new ContemporaryExperienceSettings(
			ExperienceStyle.Cellar,
			DensityMode.Compact,
			DecorationLevel.Reduced,
			ReducedMotionPreference.Reduce,
			UseSystemTypography: true,
			LibraryViewMode.Gallery,
			NavigationRailPreference.Expanded,
			ShowDecanterDock: false,
			PersistFlightBetweenSessions: true,
			UseContemporaryShell: true);
		var changedProperties = new List<string>();
		config.PropertyChanged += (_, args) =>
		{
			if (args.PropertyName is { } propertyName)
				changedProperties.Add(propertyName);
		};

		config.SaveContemporaryExperienceSettings(expected);

		Assert.AreEqual(expected, config.GetContemporaryExperienceSettings());
		Assert.AreEqual(expected, config.CreateEphemeralCopy().GetContemporaryExperienceSettings());
		CollectionAssert.AreEqual(
			new[]
			{
				nameof(Configuration.ExperienceStyle),
				nameof(Configuration.DensityMode),
				nameof(Configuration.DecorationLevel),
				nameof(Configuration.ReducedMotionPreference),
				nameof(Configuration.UseSystemTypography),
				nameof(Configuration.LibraryViewMode),
				nameof(Configuration.NavigationRailPreference),
				nameof(Configuration.ShowDecanterDock),
				nameof(Configuration.PersistFlightBetweenSessions),
				nameof(Configuration.UseContemporaryShell),
			},
			changedProperties);
	}

	[TestMethod]
	public void Persisted_flight_ID_JSON_array_round_trips_without_disabling_the_shell()
	{
		var config = Configuration.CreateMockInstance();
		config.UseContemporaryShell = true;
		config.PersistFlightBetweenSessions = true;
		config.ContemporaryFlightProductIds = ["B001", "", "B002", "B001", "   "];

		var persistedCopy = config.CreateEphemeralCopy();
		var restoredProductIds = persistedCopy.ContemporaryFlightProductIds;

		CollectionAssert.AreEqual(new[] { "B001", "B002" }, restoredProductIds);
		Assert.IsTrue(persistedCopy.PersistFlightBetweenSessions);
		Assert.IsTrue(persistedCopy.UseContemporaryShell);
	}
}
