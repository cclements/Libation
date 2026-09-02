using LibationAvalonia.DesignSystem.Components;
using LibationAvalonia.Features.Tools;
using LibationAvalonia.Properties;
using LibationAvalonia.Shell;
using LibationAvalonia.ViewModels;
using ReactiveUI;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;

namespace LibationAvalonia.Features.Accounts;

/// <summary>
/// Privacy-safe account summary. Only aggregate MainVM facts are projected; account
/// names, addresses, credentials, cookies, tokens, and marketplace identifiers never
/// cross this view-model boundary.
/// </summary>
public sealed class AccountsViewModel : SecondaryDestinationViewModel, IRoutePresentation
{
	private readonly MainVM main;

	public AccountsViewModel(ILibationCommandAdapter commands)
	{
		ArgumentNullException.ThrowIfNull(commands);
		main = commands.Main;
		ManageAccountsCommand = CreateOwnerCommand(
			commands.ShowAccountsAsync,
			"open account management",
			"Libation could not open account management. No account data was changed.");
		AddAccountCommand = CreateOwnerCommand(
			commands.AddAccountAsync,
			"add an Audible account",
			"Libation could not open account setup. No account data was changed.");
		ScanAllCommand = CreateOwnerCommand(
			commands.ScanLibraryAsync,
			"scan all accounts",
			"Libation could not scan the connected accounts. Review authorization in Manage Accounts and try again.");
		ScanSomeCommand = CreateOwnerCommand(
			commands.ScanSelectedAccountsAsync,
			"choose accounts to scan",
			"Libation could not open account selection for scanning. No library data was changed.");
		ToggleAutoScanCommand = CreateOwnerCommand(
			commands.ToggleAutomaticScan,
			"change automatic scanning",
			"Libation could not change the automatic-scan setting.");
		ReconcileAllCommand = CreateOwnerCommand(
			main.RemoveBooksAllAsync,
			"review missing titles for all accounts",
			"Libation could not begin the missing-title review. No titles were removed.");
		ReconcileSomeCommand = CreateOwnerCommand(
			main.RemoveBooksSomeAsync,
			"review missing titles for selected accounts",
			"Libation could not open account selection for missing-title review. No titles were removed.");

		main.PropertyChanged += Main_PropertyChanged;
	}

	public int AccountCount => main.AccountsCount;
	public bool HasAccounts => main.AnyAccounts;
	public bool HasMultipleAccounts => main.MultipleAccounts;
	public bool CanScan => HasAccounts && !main.ActivelyScanning;
	public bool CanScanSome => HasMultipleAccounts && !main.ActivelyScanning;
	public bool IsScanning => main.ActivelyScanning;
	public string AccountCountText => AccountCount == 1
		? "1 connected account"
		: $"{AccountCount.ToString("N0", CultureInfo.CurrentCulture)} connected accounts";
	public string LibraryTitleCountText
	{
		get
		{
			var stats = main.LibraryStats;
			int count = stats is null ? 0 : stats.booksFullyBackedUp + stats.booksDownloadedOnly + stats.booksNoProgress + stats.booksError + stats.booksUnavailable;
			return stats is null ? "Library count is loading" : count == 1 ? "1 catalogued title" : $"{count.ToString("N0", CultureInfo.CurrentCulture)} catalogued titles";
		}
	}
	public string ScanStateText => IsScanning
		? main.ScanningText
		: HasAccounts
			? Resources.AccountsScanIdle
			: "Connect an account before scanning.";
	public string AuthorizationSummary => HasAccounts
		? Resources.AccountsAuthorizationSummary
		: "No account authorization is configured.";
	public string AutoScanText => main.AutoScanChecked ? "Automatic scanning is on" : "Automatic scanning is off";
	public string AutoScanActionText => main.AutoScanChecked ? "Turn automatic scanning off" : "Turn automatic scanning on";
	public LibationStatusKind AccountStatus => !HasAccounts
		? LibationStatusKind.NeedsAttention
		: IsScanning
			? LibationStatusKind.Processing
			: LibationStatusKind.Connected;

	public ICommand ManageAccountsCommand { get; }
	public ICommand AddAccountCommand { get; }
	public ICommand ScanAllCommand { get; }
	public ICommand ScanSomeCommand { get; }
	public ICommand ToggleAutoScanCommand { get; }
	public ICommand ReconcileAllCommand { get; }
	public ICommand ReconcileSomeCommand { get; }
	public string RouteEyebrow => "Audible access";
	public string RouteTitle => "Accounts";
	public string RouteSubtitle => "Manage account access and scans without exposing private account details.";
	public RouteCommandPresentation RoutePrimaryCommand => new("Manage accounts", ManageAccountsCommand);
	public System.Collections.Generic.IReadOnlyList<RouteCommandPresentation> RouteSecondaryCommands =>
	[
		new("Add account", AddAccountCommand),
		new("Scan all", ScanAllCommand),
	];
	public RouteStatusPresentation RouteStatusBadge => new(AccountCountText, AccountStatus);

	private void Main_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (!string.IsNullOrEmpty(e.PropertyName)
			&& e.PropertyName is not nameof(MainVM.AccountsCount)
				and not nameof(MainVM.AnyAccounts)
				and not nameof(MainVM.OneAccount)
				and not nameof(MainVM.MultipleAccounts)
				and not nameof(MainVM.ActivelyScanning)
				and not nameof(MainVM.ScanningText)
				and not nameof(MainVM.AutoScanChecked)
				and not nameof(MainVM.LibraryStats))
			return;

		foreach (var property in new[]
		{
			nameof(AccountCount), nameof(HasAccounts), nameof(HasMultipleAccounts), nameof(CanScan), nameof(CanScanSome),
			nameof(IsScanning), nameof(AccountCountText), nameof(LibraryTitleCountText), nameof(ScanStateText),
			nameof(AuthorizationSummary), nameof(AutoScanText), nameof(AutoScanActionText), nameof(AccountStatus), nameof(RouteStatusBadge),
		})
			this.RaisePropertyChanged(property);
	}

	protected override void DisposeCore() => main.PropertyChanged -= Main_PropertyChanged;
}
