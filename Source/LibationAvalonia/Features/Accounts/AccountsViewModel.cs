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
		0 => global::LibationAvalonia.Properties.Resources.AccountsViewModelMarketplaceUnavailable,
		1 => snapshot.Marketplaces[0],
		_ => string.Join(", ", snapshot.Marketplaces),
	};
	public string MarketplaceSummary => snapshot.Marketplaces.Count == 1
		? global::LibationAvalonia.Properties.Resources.AccountsViewModel1Marketplace
		: string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.AccountsViewModel0Marketplaces, snapshot.Marketplaces.Count.ToString("N0", CultureInfo.CurrentCulture));
	public string TitleCountText => snapshot.TitleCount switch
	{
		null => global::LibationAvalonia.Properties.Resources.AccountsViewModelTitleCountIsLoading,
		1 => global::LibationAvalonia.Properties.Resources.AccountsViewModel1CataloguedTitle,
		int count => string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.AccountsViewModel0CataloguedTitles, count.ToString("N0", CultureInfo.CurrentCulture)),
	};
	public string AuthorizationText => snapshot.AuthorizationText;
	public LibationStatusKind AuthorizationStatus => snapshot.AuthorizationStatus;
	public string AuthorizationAccessibleName => $"{DisplayName}: {AuthorizationText}";
	public string ScanInclusionText => snapshot.IncludedInLibraryScans
		? global::LibationAvalonia.Properties.Resources.AccountsViewModelIncludedInAutomaticScans
		: global::LibationAvalonia.Properties.Resources.AccountsViewModelExcludedFromAutomaticScans;
	public bool CanScanNow { get; private set; }
	public bool CanManage => snapshot.ActionsAvailable;
	public string ScanAccessibleName => string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.AccountsViewModelScan0Now, DisplayName);
	public string EditMarketplacesAccessibleName => string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.AccountsViewModelEditMarketplacesFor0, DisplayName);
	public string ReauthenticateAccessibleName => string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.AccountsViewModelReauthenticate0, DisplayName);
	public string RemoveAccessibleName => string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.AccountsViewModelRemove0FromLibation, DisplayName);
	public string RemovalConsequenceText => AccountPresentationSource.RemovalConsequenceText;
	public ICommand ScanNowCommand { get; }
	public ICommand EditMarketplacesCommand { get; }
	public ICommand ReauthenticateCommand { get; }
	public ICommand RemoveCommand { get; }

	internal void Update(AccountPresentationSnapshot replacement, bool isScanning)
	{
		ArgumentNullException.ThrowIfNull(replacement);
		if (snapshot.PresentationId != replacement.PresentationId)
			throw new InvalidOperationException(global::LibationAvalonia.Properties.Resources.AccountsViewModelAnAccountCardSStablePresentationIdentity);
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
			global::LibationAvalonia.Properties.Resources.AccountsViewModelAddAnAudibleAccount,
			global::LibationAvalonia.Properties.Resources.AccountsViewModelLibationCouldNotOpenAccountSetupNo);
		ManageAccountsCommand = CreateOwnerCommand(
			source.ManageAccountsAsync,
			global::LibationAvalonia.Properties.Resources.AccountsViewModelOpenAccountManagement,
			global::LibationAvalonia.Properties.Resources.AccountsViewModelLibationCouldNotOpenAccountManagementNo);
		ScanAccountCommand = Track(ReactiveCommand.CreateFromTask<AccountCardViewModel>(card => RunAccountActionAsync(
			card,
			source.ScanNowAsync,
			global::LibationAvalonia.Properties.Resources.AccountsViewModelScanAnAccount,
			global::LibationAvalonia.Properties.Resources.AccountsViewModelLibationCouldNotScanThatAccountReview)));
		EditMarketplacesCommand = Track(ReactiveCommand.CreateFromTask<AccountCardViewModel>(card => RunAccountActionAsync(
			card,
			source.EditMarketplacesAsync,
			global::LibationAvalonia.Properties.Resources.AccountsViewModelEditAccountMarketplaces,
			global::LibationAvalonia.Properties.Resources.AccountsViewModelLibationCouldNotOpenMarketplaceSettingsNo)));
		ReauthenticateCommand = Track(ReactiveCommand.CreateFromTask<AccountCardViewModel>(card => RunAccountActionAsync(
			card,
			source.ReauthenticateAsync,
			global::LibationAvalonia.Properties.Resources.AccountsViewModelReauthenticateAnAccount,
			global::LibationAvalonia.Properties.Resources.AccountsViewModelLibationCouldNotReauthenticateThatAccountIts)));
		RemoveAccountCommand = Track(ReactiveCommand.CreateFromTask<AccountCardViewModel>(card => RunAccountActionAsync(
			card,
			source.RemoveAsync,
			global::LibationAvalonia.Properties.Resources.AccountsViewModelRemoveAnAccount,
			global::LibationAvalonia.Properties.Resources.AccountsViewModelLibationCouldNotRemoveThatAccountIts)));
		source.Changed += Source_Changed;
		RefreshAccounts();
	}

	public ObservableCollection<AccountCardViewModel> Accounts { get; } = new();
	public bool HasAccounts => Accounts.Count > 0;
	public bool IsScanning => source.IsScanning;
	public string ScanStateText => source.ScanStateText;
	public string AccountCountText => Accounts.Count == 1
		? global::LibationAvalonia.Properties.Resources.AccountsViewModel1ConfiguredAccount
		: string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.AccountsViewModel0ConfiguredAccounts, Accounts.Count.ToString("N0", CultureInfo.CurrentCulture));
	public LibationStatusKind AccountStatus => !HasAccounts
		? LibationStatusKind.NeedsAttention
		: Accounts.Any(account => account.AuthorizationStatus == LibationStatusKind.NeedsAttention)
			? LibationStatusKind.NeedsAttention
			: Accounts.Any(account => account.AuthorizationStatus == LibationStatusKind.DownloadPending)
				? LibationStatusKind.DownloadPending
				: LibationStatusKind.Connected;
	public string EmptyStateTitle => HasError ? global::LibationAvalonia.Properties.Resources.AccountsViewAccountsUnavailable : global::LibationAvalonia.Properties.Resources.AccountsViewModelNoAudibleAccountsConfigured;
	public string EmptyStateExplanation => HasError
		? global::LibationAvalonia.Properties.Resources.AccountsViewModelLibationCouldNotSafelyReadTheAccount
		: global::LibationAvalonia.Properties.Resources.AccountsViewModelAddAnAudibleAccountToScanIts;

	public ICommand AddAccountCommand { get; }
	public ICommand ManageAccountsCommand { get; }
	public ICommand ScanAccountCommand { get; }
	public ICommand EditMarketplacesCommand { get; }
	public ICommand ReauthenticateCommand { get; }
	public ICommand RemoveAccountCommand { get; }
	public string RouteEyebrow => global::LibationAvalonia.Properties.Resources.AccountsViewModelAudibleAccess;
	public string RouteTitle => global::LibationAvalonia.Properties.Resources.RouteAccountsLabel;
	public string RouteSubtitle => global::LibationAvalonia.Properties.Resources.AccountsViewModelReviewEachAccountSLocalAuthorizationMarketplaces;
	public RouteCommandPresentation RoutePrimaryCommand => new(global::LibationAvalonia.Properties.Resources.AccountsViewAddAccount, AddAccountCommand);
	public IReadOnlyList<RouteCommandPresentation> RouteSecondaryCommands =>
	[
		new(global::LibationAvalonia.Properties.Resources.OnboardingViewManageAccounts, ManageAccountsCommand),
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
				global::LibationAvalonia.Properties.Resources.AccountsViewModelReadAccountPresentation,
				global::LibationAvalonia.Properties.Resources.AccountsViewModelLibationCouldNotSafelyReadTheAccount2);
			Serilog.Log.Logger.Error(
				global::LibationAvalonia.Properties.Resources.AccountsViewModelTheContemporaryAccountsDestinationCouldNotLoad,
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
