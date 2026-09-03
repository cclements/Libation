using DataLayer;
using LibationAvalonia.Dialogs;
using LibationAvalonia.ViewModels;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LibationAvalonia.Shell;

/// <summary>
/// Transitional bridge to the established MainVM command owners. Contemporary
/// presentation calls this interface; it does not copy domain operations.
/// </summary>
public interface ILibationCommandAdapter
{
	MainVM Main { get; }
	Task AddAccountAsync();
	Task ShowAccountsAsync();
	Task ScanLibraryAsync();
	Task ScanSelectedAccountsAsync();
	Task ScanAccountAsync(string accountId, string registeredMarketplace);
	Task EditAccountMarketplacesAsync(string accountId, string registeredMarketplace);
	Task ReauthenticateAccountAsync(string accountId, string registeredMarketplace);
	Task RemoveAccountAsync(string accountId, string registeredMarketplace, string consequenceText);
	Task ShowSettingsAsync();
	Task ShowSettingsAsync(SettingsDialogSection section);
	Task ShowTrashAsync();
	Task ShowAboutAsync();
	Task ShowQualityScanAsync();
	Task LocateAudiobooksAsync();
	Task LocateAudiobookAsync(LibraryBook book);
	Task LocateAudiobooksFromDropAsync(IReadOnlyList<string> paths);
	Task ExportLibraryAsync();
	Task DownloadPendingBooksAsync();
	Task DownloadPendingPdfsAsync();
	Task ConvertLibraryToMp3Async();
	Task ReplaceVisibleTagsAsync();
	Task SetVisibleBookStatusAsync();
	Task SetVisiblePdfStatusAsync();
	Task DetectVisibleStatusAsync();
	Task RemoveVisibleAsync();
	Task ApplyFilterAsync(string filter, CancellationToken cancellationToken = default);
	Task EditQuickFiltersAsync();
	Task StartWalkthroughAsync();
	Task ProcessVisibleConfirmedAsync();
	Task<IReadOnlyList<LibraryBook>> GetTrashItemsAsync(CancellationToken cancellationToken = default);
	Task<int> RestoreTrashBooksAsync(IReadOnlyList<LibraryBook> books);
	Task<int> PermanentlyDeleteTrashBooksConfirmedAsync(IReadOnlyList<LibraryBook> books);
	void AddCurrentFilter();
	void OpenFilterHelp();
	void ProcessVisible();
	void ToggleAutomaticScan();
}

public sealed class LibationCommandAdapter(MainVM main) : ILibationCommandAdapter
{
	public MainVM Main { get; } = main;

	public Task AddAccountAsync() => Main.AddAccountsAsync();
	public Task ShowAccountsAsync() => Main.ShowAccountsAsync();
	public Task ScanLibraryAsync() => Main.ScanAllAccountsAsync();
	public Task ScanSelectedAccountsAsync() => Main.ScanSomeAccountsAsync();
	public Task ScanAccountAsync(string accountId, string registeredMarketplace) => Main.ScanAccountAsync(accountId, registeredMarketplace);
	public Task EditAccountMarketplacesAsync(string accountId, string registeredMarketplace) => Main.EditAccountMarketplacesAsync(accountId, registeredMarketplace);
	public Task ReauthenticateAccountAsync(string accountId, string registeredMarketplace) => Main.ReauthenticateAccountAsync(accountId, registeredMarketplace);
	public Task RemoveAccountAsync(string accountId, string registeredMarketplace, string consequenceText) => Main.RemoveAccountAsync(accountId, registeredMarketplace, consequenceText);
	public Task ShowSettingsAsync() => Main.ShowSettingsAsync();
	public Task ShowSettingsAsync(SettingsDialogSection section) => Main.ShowSettingsAsync(section);
	public Task ShowTrashAsync() => Main.ShowTrashBinAsync();
	public Task ShowAboutAsync() => Main.ShowAboutAsync();
	public Task ShowQualityScanAsync() => Main.ShowFindBetterQualityBooksAsync();
	public Task LocateAudiobooksAsync() => Main.LocateAudiobooksAsync();
	public async Task LocateAudiobookAsync(LibraryBook book)
	{
		_ = await Main.LocateBookFileAsync(book);
	}
	public Task LocateAudiobooksFromDropAsync(IReadOnlyList<string> paths) => Main.LocateAudiobooksFromDropAsync(paths);
	public Task ExportLibraryAsync() => Main.ExportLibraryAsync();
	public Task DownloadPendingBooksAsync() => Main.BackupAllBooks();
	public Task DownloadPendingPdfsAsync() => Main.BackupAllPdfs();
	public Task ConvertLibraryToMp3Async() => Main.ConvertAllToMp3Async();
	public Task ReplaceVisibleTagsAsync() => Main.ReplaceTagsAsync();
	public Task SetVisibleBookStatusAsync() => Main.SetBookDownloadedAsync();
	public Task SetVisiblePdfStatusAsync() => Main.SetPdfDownloadedAsync();
	public Task DetectVisibleStatusAsync() => Main.SetDownloadedAutoAsync();
	public Task RemoveVisibleAsync() => Main.RemoveVisibleAsync();
	public Task ApplyFilterAsync(string filter, CancellationToken cancellationToken = default) => Main.FilterBtn(filter, cancellationToken);
	public Task EditQuickFiltersAsync() => Main.EditQuickFiltersAsync();
	public Task StartWalkthroughAsync() => Main.StartWalkthroughAsync();
	public Task ProcessVisibleConfirmedAsync() => Main.ProcessVisibleConfirmedAsync();
	public Task<IReadOnlyList<LibraryBook>> GetTrashItemsAsync(CancellationToken cancellationToken = default) => Main.GetTrashItemsAsync(cancellationToken);
	public Task<int> RestoreTrashBooksAsync(IReadOnlyList<LibraryBook> books) => Main.RestoreTrashBooksAsync(books);
	public Task<int> PermanentlyDeleteTrashBooksConfirmedAsync(IReadOnlyList<LibraryBook> books) => Main.PermanentlyDeleteTrashBooksConfirmedAsync(books);
	public void AddCurrentFilter() => Main.AddQuickFilterBtn();
	public void OpenFilterHelp() => Main.FilterHelpBtn();
	public void ProcessVisible() => Main.LiberateVisible();
	public void ToggleAutomaticScan() => Main.ToggleAutoScan();
}
