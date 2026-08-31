using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Styling;
using LibationAvalonia.Features.Onboarding;
using LibationAvalonia.Shell;
using LibationAvalonia.Views;
using LibationFileManager;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

namespace LibationAvalonia.Tests;

[TestClass]
[DoNotParallelize]
public class ContemporaryVisualTreeContractTests
{
	[TestMethod]
	public async Task AppShellAndOnboarding_AttachAcrossEveryProfileAndTheme()
	{
		foreach (var style in new[]
		{
			ExperienceStyle.FollowSystem,
			ExperienceStyle.Cellar,
			ExperienceStyle.TastingRoom,
			ExperienceStyle.HighContrast,
		})
		{
			foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
			{
				await HeadlessTestHost.Reset(style);
				await HeadlessTestHost.Dispatch(() =>
				{
					App.Current.RequestedThemeVariant = theme;

					var shell = new AppShellView();
					var onboarding = new OnboardingView();
					var window = new Window
					{
						Width = 1360,
						Height = 768,
						Content = new Grid
						{
							Children = { shell, onboarding },
						},
					};

					window.Show();
					Assert.AreSame(window, TopLevel.GetTopLevel(shell), $"Shell did not attach for {style}/{theme}.");
					Assert.AreSame(window, TopLevel.GetTopLevel(onboarding), $"Onboarding did not attach for {style}/{theme}.");
					window.Close();
				});
			}
		}
	}

	[TestMethod]
	public async Task ProfileAndThemeChanges_KeepAttachedVisualTreeValid()
	{
		await HeadlessTestHost.Reset();
		Window? window = null;
		AppShellView? shell = null;
		OnboardingView? onboarding = null;
		await HeadlessTestHost.Dispatch(() =>
		{
			shell = new AppShellView();
			onboarding = new OnboardingView();
			window = new Window
			{
				Width = 1360,
				Height = 768,
				Content = new Grid { Children = { shell, onboarding } },
			};
			window.Show();
		});

		foreach (var style in new[]
		{
			ExperienceStyle.TastingRoom,
			ExperienceStyle.HighContrast,
			ExperienceStyle.Cellar,
			ExperienceStyle.FollowSystem,
		})
		{
			await HeadlessTestHost.Dispatch(() => HeadlessTestHost.Configuration.ExperienceStyle = style);
			await HeadlessTestHost.Dispatch(() =>
			{
				Assert.IsNotNull(window);
				Assert.IsNotNull(shell);
				Assert.IsNotNull(onboarding);
				foreach (var theme in new[] { ThemeVariant.Dark, ThemeVariant.Light })
				{
					App.Current.RequestedThemeVariant = theme;
					Assert.AreSame(window, TopLevel.GetTopLevel(shell));
					Assert.AreSame(window, TopLevel.GetTopLevel(onboarding));
				}
			});
		}

		await HeadlessTestHost.Dispatch(() => window!.Close());
	}

	[TestMethod]
	public async Task InvalidThicknessResource_RestoresClassicShell()
	{
		await HeadlessTestHost.Reset(useContemporaryShell: false);
		MainWindow? window = null;
		await HeadlessTestHost.Dispatch(() =>
		{
			window = new MainWindow(
				HeadlessTestHost.ExperienceManager,
				() => new InvalidThicknessOnAttachShell());
			window.Show();
			HeadlessTestHost.Configuration.UseContemporaryShell = true;
		});

		await HeadlessTestHost.Dispatch(() =>
		{
			Assert.IsNotNull(window);
			Assert.IsFalse(HeadlessTestHost.Configuration.UseContemporaryShell);
			Assert.IsNotInstanceOfType<AppShellView>(window.Content);
			window.Close();
		});
	}

	private sealed class InvalidThicknessOnAttachShell : AppShellView
	{
		protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
		{
			base.OnAttachedToLogicalTree(e);
			Resources["Libation.Thickness.4"] = 16d;
		}
	}
}
