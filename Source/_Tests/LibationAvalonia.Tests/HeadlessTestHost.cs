using Avalonia;
using Avalonia.Headless;
using Avalonia.Styling;
using LibationAvalonia.DesignSystem;
using LibationFileManager;
using ReactiveUI.Avalonia;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LibationAvalonia.Tests;

internal static class HeadlessTestAppBuilder
{
	public static AppBuilder BuildAvaloniaApp()
		=> AppBuilder.Configure<App>()
			.UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
			.UseSkia()
			.UseReactiveUI(_ => { });
}

internal static class HeadlessTestHost
{
	private static readonly string? PreviousLibationFilesDirectory;
	private static readonly HeadlessUnitTestSession Session;
	private static ExperienceManager? experienceManager;

	static HeadlessTestHost()
	{
		RootDirectory = Path.Combine(Path.GetTempPath(), $"libation-avalonia-tests-{Guid.NewGuid():N}");
		BooksDirectory = Path.Combine(RootDirectory, "Books");
		Directory.CreateDirectory(BooksDirectory);

		PreviousLibationFilesDirectory = Environment.GetEnvironmentVariable(LibationFiles.LIBATION_FILES_DIR);
		Environment.SetEnvironmentVariable(LibationFiles.LIBATION_FILES_DIR, RootDirectory);
		Configuration = Configuration.CreateMockInstance();
		Configuration.Books = BooksDirectory;
		Configuration.CheckForUpgradesAtStartup = false;
		Configuration.FirstLaunch = false;
		Configuration.SaveContemporaryExperienceSettings(DefaultExperienceSettings);

		Session = HeadlessUnitTestSession.StartNew(
			typeof(HeadlessTestAppBuilder),
			AvaloniaTestIsolationLevel.PerAssembly);
	}

	public static string RootDirectory { get; }
	public static string BooksDirectory { get; }
	public static Configuration Configuration { get; }
	public static ExperienceManager ExperienceManager
		=> experienceManager ?? throw new InvalidOperationException("The headless experience manager has not been initialized.");

	public static ContemporaryExperienceSettings DefaultExperienceSettings => new(
		ExperienceStyle.Cellar,
		DensityMode.Comfortable,
		DecorationLevel.Full,
		ReducedMotionPreference.Full,
		UseSystemTypography: false,
		LibraryViewMode.Details,
		NavigationRailPreference.Automatic,
		ShowDecanterDock: true,
		PersistFlightBetweenSessions: false,
		UseContemporaryShell: true);

	public static Task Dispatch(Action action)
		=> Session.Dispatch(action, CancellationToken.None);

	public static Task Reset(
		ExperienceStyle style = ExperienceStyle.Cellar,
		bool useContemporaryShell = true)
		=> Dispatch(() =>
		{
			if (experienceManager is null)
			{
				experienceManager = new(
					App.Current,
					Configuration,
					reducedMotionResolver: new UnavailableSystemReducedMotionResolver());
				experienceManager.Initialize();
			}

			Configuration.ContemporaryLastRoute = string.Empty;
			Configuration.ContemporaryFlightProductIds = [];
			Configuration.FirstLaunch = false;
			Configuration.Books = BooksDirectory;
			Configuration.CheckForUpgradesAtStartup = false;
			Configuration.SaveContemporaryExperienceSettings(
				DefaultExperienceSettings with
				{
					ExperienceStyle = style,
					UseContemporaryShell = useContemporaryShell,
				});
			App.Current.RequestedThemeVariant = ThemeVariant.Light;
		});

	public static async Task DisposeAsync()
	{
		if (experienceManager is not null)
			await Session.Dispatch(() => experienceManager.Dispose(), CancellationToken.None);
		await Session.DisposeAsync();
		Configuration.RestoreSingletonInstance();
		Environment.SetEnvironmentVariable(LibationFiles.LIBATION_FILES_DIR, PreviousLibationFilesDirectory);
		if (Directory.Exists(RootDirectory))
			Directory.Delete(RootDirectory, recursive: true);
	}
}
