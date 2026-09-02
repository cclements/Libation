#:project ../Source/AppScaffolding/AppScaffolding.csproj

using AppScaffolding;
using LibationFileManager;
using System.Diagnostics;
using System.Security.Cryptography;

// Creates or updates an isolated Libation profile for UI verification. Nothing here touches the
// real profile: the folder is selected through LIBATION_FILES_DIR before Configuration loads.
//
//   dotnet run Scripts/demo-profile.cs -- <profile-dir> [--style Cellar] [--route Overview]
//       [--view Gallery] [--accounts 1] [--reset]
//
// After this script, seed books with seed-demo-library.cs and covers with seed-demo-covers.py.

var dir = args.FirstOrDefault(a => !a.StartsWith("--"))
	?? throw new ArgumentException("Pass the profile directory as the first argument.");
dir = Path.GetFullPath(dir);

string Opt(string name, string fallback)
{
	var i = Array.IndexOf(args, name);
	return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
}

if (args.Contains("--reset") && Directory.Exists(dir))
	Directory.Delete(dir, recursive: true);

Directory.CreateDirectory(dir);
var books = Directory.CreateDirectory(Path.Combine(dir, "Books")).FullName;
var inProgress = Directory.CreateDirectory(Path.Combine(dir, "InProgress")).FullName;

var keyFile = Path.Combine(dir, "libation-master.key");
if (!File.Exists(keyFile))
	File.WriteAllBytes(keyFile, RandomNumberGenerator.GetBytes(32));

var settingsFile = Path.Combine(dir, LibationFiles.SETTINGS_JSON);
if (!File.Exists(settingsFile))
	File.WriteAllText(settingsFile, "{}");

Environment.SetEnvironmentVariable(LibationFiles.LIBATION_FILES_DIR, dir);
Environment.SetEnvironmentVariable("LIBATION_MASTER_KEY_FILE", keyFile);

var config = LibationScaffolding.RunPreConfigMigrations();
LibationScaffolding.RunPostConfigMigrations(config);
config.Books = books;
config.InProgress = inProgress;
config.FirstLaunch = false;
config.CheckForUpgradesAtStartup = false;
config.ContemporaryLastRoute = Opt("--route", "Overview");
config.SaveContemporaryExperienceSettings(new ContemporaryExperienceSettings(
	Enum.Parse<ExperienceStyle>(Opt("--style", "Cellar")),
	DensityMode.Comfortable,
	DecorationLevel.Full,
	ReducedMotionPreference.Full,
	UseSystemTypography: false,
	Enum.Parse<LibraryViewMode>(Opt("--view", "Gallery")),
	NavigationRailPreference.Automatic,
	ShowDecanterDock: true,
	PersistFlightBetweenSessions: false,
	UseContemporaryShell: true));

Console.WriteLine($"Settings written: {settingsFile}");

// The CLI runs the same migrations as the GUI and creates LibationContext.db and AccountsSettings.json.
var scriptsDir = Path.GetDirectoryName(Path.GetFullPath("Scripts/demo-profile.cs")) ?? ".";
var cliProject = FindUp("Source/LibationCli/LibationCli.csproj", scriptsDir)
	?? throw new FileNotFoundException(
		"Could not locate Source/LibationCli/LibationCli.csproj above Scripts/demo-profile.cs. Run this script from the repository root.");
var cliAssembly = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(cliProject)!, "..", "bin", "Release", "LibationCli.dll"));

Run(Dotnet(), $"build \"{cliProject}\" -c Release --disable-build-servers -m:1 -v:minimal");
Run(Dotnet(), $"\"{cliAssembly}\" version");
Run(Dotnet(), $"\"{cliAssembly}\" list-accounts");
Run(Dotnet(), $"\"{cliAssembly}\" search --bare __S0_DEMO_PROFILE_INIT__");

var accounts = int.Parse(Opt("--accounts", "0"));
if (accounts > 0)
{
	var accountsScript = Path.Combine(Path.GetDirectoryName(cliProject)!, "..", "..", "Scripts", "seed-demo-accounts.cs");
	Run(Dotnet(), $"run \"{Path.GetFullPath(accountsScript)}\" -- --count {accounts} \"{dir}\"");
}

var requiredFiles = new[] { "Settings.json", "libation-master.key", "LibationContext.db", "AccountsSettings.json" };
foreach (var required in requiredFiles)
	Console.WriteLine($"{(File.Exists(Path.Combine(dir, required)) ? "ok " : "MISSING ")}{required}");

return requiredFiles.All(required => File.Exists(Path.Combine(dir, required))) ? 0 : 2;

static string Dotnet()
	=> Environment.GetEnvironmentVariable("DOTNET") is { Length: > 0 } explicitPath
		? explicitPath
		: Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet", "dotnet");

static string? FindUp(string relative, string start)
{
	for (var d = new DirectoryInfo(start); d is not null; d = d.Parent)
	{
		var candidate = Path.Combine(d.FullName, relative);
		if (File.Exists(candidate))
			return candidate;
	}
	return null;
}

static void Run(string file, string arguments)
{
	var psi = new ProcessStartInfo(file, arguments) { UseShellExecute = false };
	psi.Environment["DOTNET_ROOT"] = Path.GetDirectoryName(file);
	using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Could not start {file}");
	process.WaitForExit();
	if (process.ExitCode != 0)
		throw new InvalidOperationException($"{file} {arguments} exited {process.ExitCode}");
}
