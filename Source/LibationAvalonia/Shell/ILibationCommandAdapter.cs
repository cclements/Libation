using LibationAvalonia.ViewModels;
using System.Collections.Generic;
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
	Task ShowSettingsAsync();
	Task ShowTrashAsync();
	Task ShowAboutAsync();
	Task ShowQualityScanAsync();
	Task LocateAudiobooksAsync();
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
	Task ApplyFilterAsync(string filter);
	Task EditQuickFiltersAsync();
	Task StartWalkthroughAsync();
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
	public Task ShowSettingsAsync() => Main.ShowSettingsAsync();
	public Task ShowTrashAsync() => Main.ShowTrashBinAsync();
	public Task ShowAboutAsync() => Main.ShowAboutAsync();
	public Task ShowQualityScanAsync() => Main.ShowFindBetterQualityBooksAsync();
	public Task LocateAudiobooksAsync() => Main.LocateAudiobooksAsync();
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
	public Task ApplyFilterAsync(string filter) => Main.FilterBtn(filter);
	public Task EditQuickFiltersAsync() => Main.EditQuickFiltersAsync();
	public Task StartWalkthroughAsync() => Main.StartWalkthroughAsync();
	public void AddCurrentFilter() => Main.AddQuickFilterBtn();
	public void OpenFilterHelp() => Main.FilterHelpBtn();
	public void ProcessVisible() => Main.LiberateVisible();
	public void ToggleAutomaticScan() => Main.ToggleAutoScan();
}
