using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using LibationAvalonia.Features.Library;
using LibationAvalonia.Features.Processing;
using LibationAvalonia.Shell;
using LibationAvalonia.Views;
using LibationFileManager;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LibationAvalonia.Tests;

[TestClass]
[DoNotParallelize]
public class S7VariantMatrixContractTests
{
	private static readonly AppRouteId[] primaryRoutes =
	[
		AppRouteId.Overview,
		AppRouteId.Library,
		AppRouteId.Processing,
	];

	private static readonly ExperienceStyle[] primaryProfiles =
	[
		ExperienceStyle.Cellar,
		ExperienceStyle.TastingRoom,
	];

	[TestMethod]
	public async Task ReducedMotion_RouteChangesKeepPrimaryContentVisible()
	{
		foreach (var profile in primaryProfiles)
		{
			await HeadlessTestHost.Reset(profile);
			await HeadlessTestHost.Dispatch(() =>
			{
				var settings = HeadlessTestHost.Configuration.GetContemporaryExperienceSettings();
				HeadlessTestHost.Configuration.SaveContemporaryExperienceSettings(settings with
				{
					ReducedMotionPreference = ReducedMotionPreference.Reduce,
				});
			});
			await AssertPrimaryRoutesRemainVisibleAsync(profile, assertIdentityTransform: true);
		}
	}

	[TestMethod]
	public async Task HighContrast_RouteChangesKeepPrimaryContentVisible()
	{
		await HeadlessTestHost.Reset(ExperienceStyle.HighContrast);
		await AssertPrimaryRoutesRemainVisibleAsync(ExperienceStyle.HighContrast, assertIdentityTransform: false);
	}

	[TestMethod]
	public async Task PrimaryRoutes_ExposeNamedFocusableControlsAndFocusCues()
	{
		foreach (var profile in primaryProfiles)
		{
			await HeadlessTestHost.Reset(profile);
			MainWindow? window = null;
			AppShellView? shell = null;
			AppShellViewModel? viewModel = null;
			try
			{
				await HeadlessTestHost.Dispatch(() =>
				{
					window = new MainWindow(HeadlessTestHost.ExperienceManager, null);
					window.Show();
					shell = window.Content as AppShellView;
					Assert.IsNotNull(shell);
					viewModel = shell.DataContext as AppShellViewModel;
					Assert.IsNotNull(viewModel);
					viewModel.UpdateLayout(new Size(1456, 1060));
				});
				foreach (var route in primaryRoutes)
				{
					await HeadlessTestHost.Dispatch(() => viewModel!.Navigation.Navigate(route));
					await HeadlessTestHost.Dispatch(() =>
					{
						var content = shell!.FindControl<Grid>("ContentRegion");
						Assert.IsNotNull(content);
						Control focusScope = route == AppRouteId.Overview
							? shell!.FindControl<Border>("ShellToolbar")!
							: content;
						Assert.IsNotNull(focusScope);
						var focusables = focusScope.GetVisualDescendants()
							.OfType<Control>()
							.Where(IsContemporaryFocusableControl)
							.ToArray();
						Assert.IsGreaterThan(0, focusables.Length, $"{profile}/{route} exposes no contemporary focusable controls.");

						foreach (var control in focusables)
						{
							var peer = ControlAutomationPeer.CreatePeerForElement(control);
							Assert.IsNotNull(peer, $"{profile}/{route} {Describe(control)} has no automation peer.");
							Assert.IsFalse(
								string.IsNullOrWhiteSpace(peer.GetName()),
								$"{profile}/{route} {Describe(control)} has no accessible name.");
							Assert.IsTrue(
								control.Focus(NavigationMethod.Tab),
								$"{profile}/{route} {Describe(control)} rejected keyboard focus.");
							Assert.IsTrue(
								control.IsKeyboardFocusWithin,
								$"{profile}/{route} {Describe(control)} did not retain keyboard focus.");
							AssertFocusCue(control, profile, route);
						}
					});
				}
			}
			finally
			{
				if (window is not null)
					await HeadlessTestHost.Dispatch(window.Close);
			}
		}
	}

	private static async Task AssertPrimaryRoutesRemainVisibleAsync(
		ExperienceStyle profile,
		bool assertIdentityTransform)
	{
		MainWindow? window = null;
		AppShellView? shell = null;
		AppShellViewModel? viewModel = null;
		try
		{
			await HeadlessTestHost.Dispatch(() =>
			{
				window = new MainWindow(HeadlessTestHost.ExperienceManager, null);
				window.Show();
				shell = window.Content as AppShellView;
				Assert.IsNotNull(shell);
				viewModel = shell.DataContext as AppShellViewModel;
				Assert.IsNotNull(viewModel);
				viewModel.UpdateLayout(new Size(1456, 1060));
			});

			foreach (var route in primaryRoutes)
			{
				await HeadlessTestHost.Dispatch(() => viewModel!.Navigation.Navigate(route));
				await HeadlessTestHost.Dispatch(() =>
				{
					var content = shell!.FindControl<Grid>("ContentRegion");
					Assert.IsNotNull(content);
					Assert.AreEqual(1d, content.Opacity, $"{profile}/{route} content opacity changed.");
					Assert.IsTrue(content.IsEffectivelyVisible, $"{profile}/{route} content region is hidden.");
					AssertRouteIsVisible(shell!, route, profile);
					if (assertIdentityTransform)
						Assert.AreEqual(
							Matrix.Identity,
							content.RenderTransform?.Value ?? Matrix.Identity,
							$"{profile}/{route} retained route motion while reduced motion was enabled.");
				});
			}
		}
		finally
		{
			if (window is not null)
				await HeadlessTestHost.Dispatch(window.Close);
		}
	}

	private static void AssertRouteIsVisible(AppShellView shell, AppRouteId route, ExperienceStyle profile)
	{
		Control? routeControl = route switch
		{
			AppRouteId.Overview => shell.FindControl<ContentControl>("OverviewHost"),
			AppRouteId.Library => shell.FindControl<LibraryView>("LibraryDisplay"),
			AppRouteId.Processing => shell.GetVisualDescendants().OfType<ProcessingView>().Single(),
			_ => null,
		};
		Assert.IsNotNull(routeControl);
		Assert.IsTrue(routeControl.IsEffectivelyVisible, $"{profile}/{route} route body is hidden.");
		Assert.IsGreaterThan(0d, routeControl.Bounds.Width, $"{profile}/{route} route body has no width.");
		Assert.IsGreaterThan(0d, routeControl.Bounds.Height, $"{profile}/{route} route body has no height.");
	}

	private static bool IsContemporaryFocusableControl(Control control)
		=> control.Focusable
			&& control.IsEffectivelyVisible
			&& control.IsEffectivelyEnabled
			&& control.Classes.Contains("contemporary")
			&& control is Button or TextBox or ComboBox or CheckBox;

	private static void AssertFocusCue(Control control, ExperienceStyle profile, AppRouteId route)
	{
		Assert.IsTrue(
			App.Current.TryGetResource("Libation.Brush.Focus", App.Current.ActualThemeVariant, out var focusResource),
			"The focus brush resource is unavailable.");
		Assert.IsInstanceOfType<IBrush>(focusResource);
		var expected = focusResource.ToString();

		if (control is CheckBox checkBox)
		{
			Assert.AreEqual(expected, checkBox.Foreground?.ToString(), $"{profile}/{route} {Describe(control)} has no visible focus cue.");
			return;
		}

		Assert.IsInstanceOfType<TemplatedControl>(control);
		var templated = (TemplatedControl)control;
		Assert.AreEqual(expected, templated.BorderBrush?.ToString(), $"{profile}/{route} {Describe(control)} has no focus-colored border.");
		Assert.IsGreaterThanOrEqualTo(2d, templated.BorderThickness.Left, $"{profile}/{route} {Describe(control)} focus border is too thin.");
	}

	private static string Describe(Control control)
		=> $"{control.GetType().Name}#{control.Name ?? "(unnamed)"}";
}
