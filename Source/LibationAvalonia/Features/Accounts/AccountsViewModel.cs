using LibationAvalonia.DesignSystem;
using LibationAvalonia.DesignSystem.Components;
using LibationAvalonia.Features.Tools;
using LibationAvalonia.Shell;
using LibationAvalonia.ViewModels;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace LibationAvalonia.Features.Accounts;

public sealed class AccountCardViewModel : ViewModelBase
{
	private AccountPresentationSnapshot snapshot;

	internal AccountCardViewModel(
		AccountPresentationSnapshot snapshot,
		bool isScanning,
		ICommand scanNowCommand,
		ICommand editMarketplacesCommand,
		ICommand reauthenticateCommand,
		ICommand removeCommand)
	{
		ArgumentNullException.ThrowIfNull(snapshot);
		this.snapshot = snapshot;
		ScanNowCommand = scanNowCommand;
		EditMarketplacesCommand = editMarketplacesCommand;
		ReauthenticateCommand = reauthenticateCommand;
		RemoveCommand = removeCommand;
		CanScanNow = snapshot.ActionsAvailable && !isScanning;
	}

	internal AccountPresentationSnapshot Snapshot => snapshot;
	public string DisplayName => snapshot.DisplayName;
	public string MarketplacesText => snapshot.Marketplaces.Count switch
	{
		0 => "Marketplace unavailable",
		1 => snapshot.Marketplaces[0],
		_ => string.Join(", ", snapshot.Marketplaces),
	};
	public string MarketplaceSummary => snapshot.Marketplaces.Count == 1
		? "1 marketplace"
		: $"{snapshot.Marketplaces.Count.ToString("N0", CultureInfo.CurrentCulture)} marketplaces";
	public string TitleCountText => snapshot.TitleCount switch
	{
		null => "Title count is loading",
		1 => "1 catalogued title",
		int count => $"{count.ToString("N0", CultureInfo.CurrentCulture)} catalogued titles",
	};
	public string AuthorizationText => snapshot.AuthorizationText;
	public LibationStatusKind AuthorizationStatus => snapshot.AuthorizationStatus;
	public string AuthorizationAccessibleName => $"{DisplayName}: {AuthorizationText}";
	public string ScanInclusionText => snapshot.IncludedInLibraryScans
		? "Included in automatic scans"
		: "Excluded from automatic scans";
	public bool CanScanNow { get; private set; }
	public bool CanManage => snapshot.ActionsAvailable;
	public string ScanAccessibleName => $"Scan {DisplayName} now";
	public string EditMarketplacesAccessibleName => $"Edit marketplaces for {DisplayName}";
	public string ReauthenticateAccessibleName => $"Reauthenticate {DisplayName}";
	public string RemoveAccessibleName => $"Remove {DisplayName} from Libation";
	public string RemovalConsequenceText => AccountPresentationSource.RemovalConsequenceText;
	public ICommand ScanNowCommand { get; }
	public ICommand EditMarketplacesCommand { get; }
	public ICommand ReauthenticateCommand { get; }
	public ICommand RemoveCommand { get; }

	internal void Update(AccountPresentationSnapshot replacement, bool isScanning)
	{
		ArgumentNullException.ThrowIfNull(replacement);
		if (snapshot.PresentationId != replacement.PresentationId)
			throw new InvalidOperationException("An account card's stable presentation identity cannot change during refresh.");
		snapshot = replacement;
		CanScanNow = snapshot.ActionsAvailable && !isScanning;
		foreach (var property in new[]
		{
			nameof(DisplayName), nameof(MarketplacesText), nameof(MarketplaceSummary), nameof(TitleCountText),
			nameof(AuthorizationText), nameof(AuthorizationStatus), nameof(AuthorizationAccessibleName),
			nameof(ScanInclusionText), nameof(CanScanNow), nameof(CanManage), nameof(ScanAccessibleName),
			nameof(EditMarketplacesAccessibleName), nameof(ReauthenticateAccessibleName),
			nameof(RemoveAccessibleName), nameof(RemovalConsequenceText),
		})
			this.RaisePropertyChanged(property);
	}
}

/// <summary>
/// Safe presentation over a source that alone resolves private account identity.
/// </summary>
public sealed class AccountsViewModel : SecondaryDestinationViewModel, IRoutePresentation
{
	private readonly IAccountPresentationSource source;
	private readonly Dictionary<string, AccountCardViewModel> accountsById = new(StringComparer.Ordinal);

	public AccountsViewModel(ILibationCommandAdapter commands)
		: this(new AccountPresentationSource(commands)) { }

	public AccountsViewModel(IAccountPresentationSource source)
	{
		ArgumentNullException.ThrowIfNull(source);
		this.source = source;
		AddAccountCommand = CreateOwnerCommand(
			source.AddAccountAsync,
			"add an Audible account",
			"Libation could not open account setup. No account data was changed.");
		ManageAccountsCommand = CreateOwnerCommand(
			source.ManageAccountsAsync,
			"open account management",
			"Libation could not open account management. No account data was changed.");
		ScanAccountCommand = Track(ReactiveCommand.CreateFromTask<AccountCardViewModel>(card => RunAccountActionAsync(
			card,
			source.ScanNowAsync,
			"scan an account",
			"Libation could not scan that account. Review its stored authorization and try again.")));
		EditMarketplacesCommand = Track(ReactiveCommand.CreateFromTask<AccountCardViewModel>(card => RunAccountActionAsync(
			card,
			source.EditMarketplacesAsync,
			"edit account marketplaces",
			"Libation could not open marketplace settings. No account data was changed.")));
		ReauthenticateCommand = Track(ReactiveCommand.CreateFromTask<AccountCardViewModel>(card => RunAccountActionAsync(
			card,
			source.ReauthenticateAsync,
			"reauthenticate an account",
			"Libation could not reauthenticate that account. Its existing stored authorization was not removed.")));
		RemoveAccountCommand = Track(ReactiveCommand.CreateFromTask<AccountCardViewModel>(card => RunAccountActionAsync(
			card,
			source.RemoveAsync,
			"remove an account",
			"Libation could not remove that account. Its settings and Library records were left unchanged.")));
		source.Changed += Source_Changed;
		RefreshAccounts();
	}

	public ObservableCollection<AccountCardViewModel> Accounts { get; } = new();
	public bool HasAccounts => Accounts.Count > 0;
	public bool IsScanning => source.IsScanning;
	public string ScanStateText => source.ScanStateText;
	public string AccountCountText => Accounts.Count == 1
		? "1 configured account"
		: $"{Accounts.Count.ToString("N0", CultureInfo.CurrentCulture)} configured accounts";
	public LibationStatusKind AccountStatus => !HasAccounts
		? LibationStatusKind.NeedsAttention
		: Accounts.Any(account => account.AuthorizationStatus == LibationStatusKind.NeedsAttention)
			? LibationStatusKind.NeedsAttention
			: Accounts.Any(account => account.AuthorizationStatus == LibationStatusKind.DownloadPending)
				? LibationStatusKind.DownloadPending
				: LibationStatusKind.Connected;
	public string EmptyStateTitle => HasError ? "Accounts unavailable" : "No Audible accounts configured";
	public string EmptyStateExplanation => HasError
		? "Libation could not safely read the account list. Use the preserved account manager to review the problem."
		: "Add an Audible account to scan its marketplaces and catalogue titles.";

	public ICommand AddAccountCommand { get; }
	public ICommand ManageAccountsCommand { get; }
	public ICommand ScanAccountCommand { get; }
	public ICommand EditMarketplacesCommand { get; }
	public ICommand ReauthenticateCommand { get; }
	public ICommand RemoveAccountCommand { get; }
	public string RouteEyebrow => "Audible access";
	public string RouteTitle => "Accounts";
	public string RouteSubtitle => "Review each account's local authorization, marketplaces, titles, and scan participation.";
	public RouteCommandPresentation RoutePrimaryCommand => new("Add account", AddAccountCommand);
	public IReadOnlyList<RouteCommandPresentation> RouteSecondaryCommands =>
	[
		new("Manage accounts", ManageAccountsCommand),
	];
	public RouteStatusPresentation RouteStatusBadge => new(AccountCountText, AccountStatus);

	private Task RunAccountActionAsync(
		AccountCardViewModel? card,
		Func<AccountPresentationSnapshot, Task> action,
		string operation,
		string userError)
		=> card is null
			? Task.CompletedTask
			: RunOwnerActionAsync(() => action(card.Snapshot), operation, userError);

	private void Source_Changed(object? sender, EventArgs e) => RefreshAccounts();

	private void RefreshAccounts()
	{
		try
		{
			var desired = new List<AccountCardViewModel>();
			foreach (var snapshot in source.GetAccounts())
			{
				if (!accountsById.TryGetValue(snapshot.PresentationId, out var account))
				{
					account = new AccountCardViewModel(
						snapshot,
						source.IsScanning,
						ScanAccountCommand,
						EditMarketplacesCommand,
						ReauthenticateCommand,
						RemoveAccountCommand);
					accountsById.Add(snapshot.PresentationId, account);
				}
				else
					account.Update(snapshot, source.IsScanning);
				desired.Add(account);
			}

			var desiredSet = desired.ToHashSet();
			foreach (var removed in Accounts.Where(account => !desiredSet.Contains(account)).ToArray())
			{
				Accounts.Remove(removed);
				accountsById.Remove(removed.Snapshot.PresentationId);
			}
			for (int index = 0; index < desired.Count; index++)
			{
				var account = desired[index];
				if (index < Accounts.Count && ReferenceEquals(Accounts[index], account))
					continue;
				int currentIndex = Accounts.IndexOf(account);
				if (currentIndex >= 0)
					Accounts.Move(currentIndex, index);
				else
					Accounts.Insert(index, account);
			}
			CurrentError = null;
		}
		catch (Exception ex)
		{
			Accounts.Clear();
			accountsById.Clear();
			CurrentError = UserFacingErrorFactory.FromException(
				ex,
				"read account presentation",
				"Libation could not safely read the account list. Open Manage Accounts and try again.");
			Serilog.Log.Logger.Error(
				"The contemporary Accounts destination could not load its privacy-safe account projection. Correlation ID: {CorrelationId}. {TechnicalDetails}",
				CurrentError.CorrelationId,
				UserFacingErrorFactory.Scrub(ex.ToString()));
		}

		foreach (var property in new[]
		{
			nameof(HasAccounts), nameof(IsScanning), nameof(ScanStateText), nameof(AccountCountText),
			nameof(AccountStatus), nameof(EmptyStateTitle), nameof(EmptyStateExplanation), nameof(RouteStatusBadge),
		})
			this.RaisePropertyChanged(property);
	}

	protected override void DisposeCore()
	{
		source.Changed -= Source_Changed;
		source.Dispose();
	}
}
