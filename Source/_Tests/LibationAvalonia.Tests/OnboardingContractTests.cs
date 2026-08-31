using LibationAvalonia.Features.Onboarding;
using LibationAvalonia.Shell;
using LibationAvalonia.Views;
using LibationFileManager;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

namespace LibationAvalonia.Tests;

[TestClass]
[DoNotParallelize]
public class OnboardingContractTests
{
	[TestMethod]
	public async Task OnboardingCommit_AttachesShellAfterResourceTransaction()
	{
		await HeadlessTestHost.Reset(useContemporaryShell: false);
		MainWindow? window = null;
		OnboardingView? onboarding = null;
		await HeadlessTestHost.Dispatch(() =>
		{
			HeadlessTestHost.Configuration.FirstLaunch = true;
			HeadlessTestHost.Configuration.ContemporaryLastRoute = nameof(AppRouteId.Library);
			window = new MainWindow(HeadlessTestHost.ExperienceManager, null);
			window.ShowOnboarding(isManualReentry: false);
			window.Show();

			onboarding = window.Content as OnboardingView;
			Assert.IsNotNull(onboarding);
			var viewModel = onboarding.DataContext as OnboardingViewModel;
			Assert.IsNotNull(viewModel);
			viewModel.SelectCellarCommand.Execute(null);
			for (var step = 0; step < 5; step++)
				viewModel.NextCommand.Execute(null);

			Assert.IsTrue(HeadlessTestHost.Configuration.UseContemporaryShell);
			Assert.AreEqual(ExperienceStyle.Cellar, HeadlessTestHost.Configuration.ExperienceStyle);
			Assert.AreSame(onboarding, window.Content, "The onboarding surface was replaced before the queued resource commit ran.");
		});

		await HeadlessTestHost.Dispatch(() =>
		{
			Assert.IsNotNull(window);
			Assert.AreEqual(ExperienceStyle.Cellar, HeadlessTestHost.ExperienceManager.CurrentProfile.Style);
			Assert.IsInstanceOfType<AppShellView>(window.Content);
			Assert.IsTrue(App.Current.TryGetResource(
				"Libation.Thickness.4",
				App.Current.ActualThemeVariant,
				out var value));
			Assert.IsInstanceOfType<Avalonia.Thickness>(value);
			window.Close();
		});
	}
}
