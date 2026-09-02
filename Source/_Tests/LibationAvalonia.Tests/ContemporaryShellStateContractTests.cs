using LibationAvalonia.DesignSystem;
using LibationAvalonia.Shell;
using LibationAvalonia.ViewModels;
using LibationAvalonia.Views;
using LibationFileManager;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

namespace LibationAvalonia.Tests;

[TestClass]
[DoNotParallelize]
public class ContemporaryShellStateContractTests
{
	[TestMethod]
	public async Task QuickFilterMenus_DoNotShareVisualParents()
	{
		await HeadlessTestHost.Reset();
		await HeadlessTestHost.Dispatch(() =>
			HeadlessTestHost.Configuration.ContemporaryLastRoute = nameof(AppRouteId.Library));
		MainWindow? window = null;
		await HeadlessTestHost.Dispatch(() =>
		{
			window = new MainWindow(HeadlessTestHost.ExperienceManager, null);
			window.Show();
			var main = window.DataContext as MainVM;
			Assert.IsNotNull(main);
			Assert.AreEqual(main.QuickFilterMenuItems.Count, main.ContemporaryQuickFilterMenuItems.Count);
			for (var index = 0; index < main.QuickFilterMenuItems.Count; index++)
				Assert.AreNotSame(main.QuickFilterMenuItems[index], main.ContemporaryQuickFilterMenuItems[index]);

			Assert.IsInstanceOfType<AppShellView>(window.Content);
			window.Close();
		});
	}

	[TestMethod]
	public async Task NavigationResponsiveAndFeatureOffState_RemainCoherent()
	{
		await HeadlessTestHost.Reset(ExperienceStyle.Cellar);
		await HeadlessTestHost.Dispatch(() =>
			HeadlessTestHost.Configuration.ContemporaryLastRoute = nameof(AppRouteId.Library));
		MainWindow? window = null;
		AppShellView? shell = null;
		AppShellViewModel? viewModel = null;
		await HeadlessTestHost.Dispatch(() =>
		{
			window = new MainWindow(HeadlessTestHost.ExperienceManager, null);
			window.Show();
			shell = window.Content as AppShellView;
			Assert.IsNotNull(shell);
			viewModel = shell.DataContext as AppShellViewModel;
			Assert.IsNotNull(viewModel);

			viewModel.Navigation.Navigate(AppRouteId.History);
			Assert.AreEqual(nameof(AppRouteId.History), HeadlessTestHost.Configuration.ContemporaryLastRoute);
			Assert.IsTrue(viewModel.History.IsActive);
			Assert.IsFalse(viewModel.Dashboard.IsActive);

			viewModel.UpdateLayout(new(1360, 768));
			Assert.AreEqual(DesktopLayoutClass.Wide, viewModel.Responsive.Current.LayoutClass);
			Assert.AreEqual(NavigationRailState.Expanded, viewModel.Responsive.Current.NavigationRail);
			viewModel.UpdateLayout(new(1080, 768));
			Assert.AreEqual(DesktopLayoutClass.Standard, viewModel.Responsive.Current.LayoutClass);
			viewModel.UpdateLayout(new(840, 768));
			Assert.AreEqual(DesktopLayoutClass.Compact, viewModel.Responsive.Current.LayoutClass);
			Assert.AreEqual(NavigationRailState.Compact, viewModel.Responsive.Current.NavigationRail);
			viewModel.UpdateLayout(new(720, 560));
			Assert.AreEqual(DesktopLayoutClass.Narrow, viewModel.Responsive.Current.LayoutClass);
			Assert.AreEqual(NavigationRailState.Overlay, viewModel.Responsive.Current.NavigationRail);

			HeadlessTestHost.Configuration.UseContemporaryShell = false;
		});

		await HeadlessTestHost.Dispatch(() =>
		{
			Assert.IsNotNull(viewModel);
			Assert.IsFalse(viewModel.History.IsActive);
			Assert.IsFalse(viewModel.Dashboard.IsActive);
			Assert.AreEqual(AppRouteId.History, viewModel.Navigation.CurrentRoute.Id);

			HeadlessTestHost.Configuration.UseContemporaryShell = true;
		});

		await HeadlessTestHost.Dispatch(() =>
		{
			Assert.IsNotNull(window);
			Assert.IsNotNull(shell);
			Assert.IsNotNull(viewModel);
			var replacementShell = window.Content as AppShellView;
			Assert.IsNotNull(replacementShell);
			var replacementViewModel = replacementShell.DataContext as AppShellViewModel;
			Assert.IsNotNull(replacementViewModel);
			Assert.AreNotSame(shell, replacementShell);
			Assert.AreNotSame(viewModel, replacementViewModel);
			Assert.IsNull(shell.DataContext);
			Assert.IsFalse(viewModel.History.IsActive);
			Assert.IsFalse(viewModel.Dashboard.IsActive);
			Assert.IsTrue(replacementViewModel.History.IsActive);
			Assert.AreEqual(AppRouteId.History, replacementViewModel.Navigation.CurrentRoute.Id);
			window.Close();
		});
	}
}
