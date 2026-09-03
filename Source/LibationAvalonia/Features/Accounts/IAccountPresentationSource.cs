using LibationAvalonia.DesignSystem.Components;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LibationAvalonia.Features.Accounts;

/// <summary>
/// Coarse local credential state. This deliberately does not claim that Audible
/// has accepted the stored authorization in the current session.
/// </summary>
public enum AccountAuthorizationState
{
	StoredAuthorizationReady,
	StoredSessionNeedsRenewal,
	SignInRequired,
}

/// <summary>
/// Immutable, presentation-safe account facts. No login, credential, token,
/// cookie, activation byte, or domain Account crosses this boundary.
/// </summary>
public sealed class AccountPresentationSnapshot
{
	internal AccountPresentationSnapshot(
		string presentationId,
		string displayName,
		IEnumerable<string> marketplaces,
		int? titleCount,
		AccountAuthorizationState authorizationState,
		bool includedInLibraryScans,
		bool actionsAvailable)
	{
		PresentationId = presentationId;
		DisplayName = displayName;
		Marketplaces = new List<string>(marketplaces).AsReadOnly();
		TitleCount = titleCount;
		AuthorizationState = authorizationState;
		IncludedInLibraryScans = includedInLibraryScans;
		ActionsAvailable = actionsAvailable;
	}

	internal string PresentationId { get; }
	public string DisplayName { get; }
	public IReadOnlyList<string> Marketplaces { get; }
	public int? TitleCount { get; }
	public AccountAuthorizationState AuthorizationState { get; }
	public bool IncludedInLibraryScans { get; }
	public bool ActionsAvailable { get; }
	public string AuthorizationText => AuthorizationState switch
	{
		AccountAuthorizationState.StoredAuthorizationReady => "Stored authorization is ready",
		AccountAuthorizationState.StoredSessionNeedsRenewal => "Stored session needs renewal",
		_ => "Sign-in required",
	};
	public LibationStatusKind AuthorizationStatus => AuthorizationState switch
	{
		AccountAuthorizationState.StoredAuthorizationReady => LibationStatusKind.Connected,
		AccountAuthorizationState.StoredSessionNeedsRenewal => LibationStatusKind.DownloadPending,
		_ => LibationStatusKind.NeedsAttention,
	};
}

/// <summary>
/// Read-only account projection plus delegation to established account owners.
/// Implementations may inspect account storage, but view models receive safe
/// snapshots only and never mutate account persistence directly.
/// </summary>
public interface IAccountPresentationSource : IDisposable
{
	event EventHandler? Changed;
	bool IsScanning { get; }
	string ScanStateText { get; }
	IReadOnlyList<AccountPresentationSnapshot> GetAccounts();
	Task AddAccountAsync();
	Task ManageAccountsAsync();
	Task ScanNowAsync(AccountPresentationSnapshot account);
	Task EditMarketplacesAsync(AccountPresentationSnapshot account);
	Task ReauthenticateAsync(AccountPresentationSnapshot account);
	Task RemoveAsync(AccountPresentationSnapshot account);
}
