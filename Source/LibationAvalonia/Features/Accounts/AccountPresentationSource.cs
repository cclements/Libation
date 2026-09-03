using AudibleUtilities;
using Avalonia.Threading;
using Dinah.Core;
using LibationAvalonia.Shell;
using LibationAvalonia.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace LibationAvalonia.Features.Accounts;

/// <summary>
/// Copies account and library facts into safe snapshots, then delegates every
/// action to the existing application command owner.
/// </summary>
public sealed class AccountPresentationSource : IAccountPresentationSource
{
	public static readonly string RemovalConsequenceText =
		global::LibationAvalonia.Properties.Resources.AccountPresentationSourceRemovingThisAccountDeletesItsSavedSign
		+ global::LibationAvalonia.Properties.Resources.AccountPresentationSourceExistingLibraryRecordsAndLocalAudiobookFiles;

	private readonly ILibationCommandAdapter commands;
	private readonly MainVM main;
	private readonly object sync = new();
	private readonly Dictionary<string, string> presentationIds = new(StringComparer.Ordinal);
	private Dictionary<string, AccountLocator> locators = new(StringComparer.Ordinal);
	private bool disposed;

	private sealed record AccountLocator(string AccountId, string RegisteredMarketplace);

	public AccountPresentationSource(ILibationCommandAdapter commands)
	{
		ArgumentNullException.ThrowIfNull(commands);
		this.commands = commands;
		main = commands.Main;
		main.PropertyChanged += Main_PropertyChanged;
		AccountsSettingsPersister.Saved += AccountsSettingsPersister_Saved;
	}

	public event EventHandler? Changed;
	public bool IsScanning => main.ActivelyScanning;
	public string ScanStateText => IsScanning ? main.ScanningText : global::LibationAvalonia.Properties.Resources.AccountPresentationSourceNoAccountScanIsRunning;

	public IReadOnlyList<AccountPresentationSnapshot> GetAccounts()
	{
		ObjectDisposedException.ThrowIf(disposed, this);

		using var persister = AudibleApiStorage.GetAccountsSettingsPersister();
		var settings = persister.AccountsSettings;
		var accounts = settings.GetAll().ToArray();
		var library = main.LibraryStats?.LibraryBooks?.ToArray();
		var titleCounts = library is null
			? null
			: accounts.ToDictionary(AccountKey, _ => 0, StringComparer.Ordinal);

		foreach (var libraryBook in library ?? [])
		{
			var owner = settings.GetAccount(libraryBook.Account, libraryBook.Book.Locale);
			if (owner is not null && titleCounts is not null && titleCounts.ContainsKey(AccountKey(owner)))
				titleCounts[AccountKey(owner)]++;
		}

		var nextLocators = new Dictionary<string, AccountLocator>(StringComparer.Ordinal);
		var snapshots = new List<AccountPresentationSnapshot>(accounts.Length);
		foreach (var account in accounts)
		{
			var registeredMarketplace = account.Locale?.Name ?? string.Empty;
			var key = AccountKey(account);
			string presentationId;
			lock (sync)
			{
				if (!presentationIds.TryGetValue(key, out presentationId!))
					presentationIds[key] = presentationId = Guid.NewGuid().ToString("N");
			}

			nextLocators[presentationId] = new(account.AccountId, registeredMarketplace);
			var authorization = account.IdentityTokens?.IsValid == true
				? AccountAuthorizationState.StoredAuthorizationReady
				: AccountCredentialStatus.LooksLikeMissingCredentials(account)
					? AccountAuthorizationState.SignInRequired
					: AccountAuthorizationState.StoredSessionNeedsRenewal;
			var marketplaces = account.ScanLocales
				.Select(locale => locale.Name)
				.Where(name => !string.IsNullOrWhiteSpace(name))
				.Distinct(StringComparer.CurrentCultureIgnoreCase)
				.ToArray();

			snapshots.Add(new(
				presentationId,
				SafeDisplayName(account),
				marketplaces,
				titleCounts?.GetValueOrDefault(key),
				authorization,
				account.LibraryScan,
				!string.IsNullOrWhiteSpace(registeredMarketplace)));
		}

		lock (sync)
			locators = nextLocators;
		return snapshots.AsReadOnly();
	}

	public Task AddAccountAsync() => commands.AddAccountAsync();
	public Task ManageAccountsAsync() => commands.ShowAccountsAsync();

	public Task ScanNowAsync(AccountPresentationSnapshot account)
	{
		var locator = Resolve(account);
		return commands.ScanAccountAsync(locator.AccountId, locator.RegisteredMarketplace);
	}

	public Task EditMarketplacesAsync(AccountPresentationSnapshot account)
	{
		var locator = Resolve(account);
		return commands.EditAccountMarketplacesAsync(locator.AccountId, locator.RegisteredMarketplace);
	}

	public async Task ReauthenticateAsync(AccountPresentationSnapshot account)
	{
		var locator = Resolve(account);
		await commands.ReauthenticateAccountAsync(locator.AccountId, locator.RegisteredMarketplace);
		RaiseChanged();
	}

	public Task RemoveAsync(AccountPresentationSnapshot account)
	{
		var locator = Resolve(account);
		return commands.RemoveAccountAsync(locator.AccountId, locator.RegisteredMarketplace, RemovalConsequenceText);
	}

	private AccountLocator Resolve(AccountPresentationSnapshot account)
	{
		ArgumentNullException.ThrowIfNull(account);
		ObjectDisposedException.ThrowIf(disposed, this);
		lock (sync)
		{
			if (locators.TryGetValue(account.PresentationId, out var locator))
				return locator;
		}
		throw new InvalidOperationException(global::LibationAvalonia.Properties.Resources.AccountPresentationSourceThatAccountCardIsNoLongerCurrent);
	}

	private static string AccountKey(Account account)
		=> $"{account.AccountId.Trim().ToUpperInvariant()}\u001f{account.Locale?.Name ?? string.Empty}";

	internal static string SafeDisplayName(Account account)
	{
		var name = account.AccountName?.Trim();
		if (string.IsNullOrWhiteSpace(name))
			return account.AccountId.ToMask();
		var generatedPrefix = $"{account.AccountId} - ";
		var isGenerated = string.Equals(name, account.AccountId, StringComparison.OrdinalIgnoreCase)
			|| name.StartsWith(generatedPrefix, StringComparison.OrdinalIgnoreCase);
		if (isGenerated)
			return account.AccountId.ToMask();
		return name.Replace(account.AccountId, account.AccountId.ToMask(), StringComparison.OrdinalIgnoreCase);
	}

	private void AccountsSettingsPersister_Saved(object? sender, EventArgs e) => RaiseChanged();

	private void Main_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (!string.IsNullOrEmpty(e.PropertyName)
			&& e.PropertyName is not nameof(MainVM.LibraryStats)
				and not nameof(MainVM.ActivelyScanning)
				and not nameof(MainVM.ScanningText)
				and not nameof(MainVM.AccountsCount))
			return;
		RaiseChanged();
	}

	private void RaiseChanged()
	{
		if (disposed)
			return;
		if (!Dispatcher.UIThread.CheckAccess())
		{
			Dispatcher.UIThread.Post(RaiseChanged);
			return;
		}
		Changed?.Invoke(this, EventArgs.Empty);
	}

	public void Dispose()
	{
		if (disposed)
			return;
		disposed = true;
		main.PropertyChanged -= Main_PropertyChanged;
		AccountsSettingsPersister.Saved -= AccountsSettingsPersister_Saved;
		Changed = null;
		lock (sync)
		{
			locators.Clear();
			presentationIds.Clear();
		}
	}
}
