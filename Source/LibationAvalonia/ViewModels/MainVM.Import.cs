using ApplicationServices;
using AudibleUtilities;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using DataLayer;
using LibationFileManager;
using LibationUiBase;
using LibationUiBase.Forms;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LibationAvalonia.ViewModels;

public partial class MainVM
{
	private int _numAccountsScanning = 2;
	public string LocateAudiobooksTip => Configuration.GetHelpText("LocateAudiobooks");

	/// <summary> Auto scanning accounts is enables </summary>
	public bool AutoScanChecked { get => field; set => Configuration.Instance.AutoScan = this.RaiseAndSetIfChanged(ref field, value); } = Configuration.Instance.AutoScan;
	/// <summary> Display text for the "Remove # Books from Libation" button </summary>
	public string RemoveBooksButtonText { get => field; set => this.RaiseAndSetIfChanged(ref field, value); } = "Remove # Books from Libation";
	/// <summary> Indicates if the "Remove # Books from Libation" button is enabled </summary>
	public bool RemoveBooksButtonEnabled { get => field; set { this.RaiseAndSetIfChanged(ref field, value); } } = Design.IsDesignMode;
	/// <summary> Indicates if the "Remove # Books from Libation" and "Done Removing" buttons should be visible </summary>
	public bool RemoveButtonsVisible
	{
		get => field;
		set
		{
			this.RaiseAndSetIfChanged(ref field, value);
			this.RaisePropertyChanged(nameof(RemoveMenuItemsEnabled));
		}
	} = Design.IsDesignMode;
	/// <summary> Indicates if Libation is currently scanning account(s) </summary>
	public bool ActivelyScanning => _numAccountsScanning > 0;
	/// <summary> Indicates if the "Remove Books" menu items are enabled</summary>
	public bool RemoveMenuItemsEnabled => !RemoveButtonsVisible && !ActivelyScanning;
	/// <summary> The library scanning status text </summary>
	public string ScanningText => _numAccountsScanning == 1 ? "Scanning..." : $"Scanning {_numAccountsScanning} accounts...";
	/// <summary> There is at least one Audible account </summary>
	public bool AnyAccounts => AccountsCount > 0;
	/// <summary> There is exactly one Audible account </summary>
	public bool OneAccount => AccountsCount == 1;
	/// <summary> There are more than 1 Audible accounts </summary>
	public bool MultipleAccounts => AccountsCount > 1;
	/// <summary> The number of accounts added to Libation </summary>
	public int AccountsCount
	{
		get => field;
		set
		{
			this.RaiseAndSetIfChanged(ref field, value);
			this.RaisePropertyChanged(nameof(AnyAccounts));
			this.RaisePropertyChanged(nameof(OneAccount));
			this.RaisePropertyChanged(nameof(MultipleAccounts));
			RaiseGettingStartedChanged();
		}
	}


	public void Configure_Import()
	{
		MainWindow.Loaded += (_, _) =>
		{
			refreshImportMenu();
			AccountsSettingsPersister.Saved += (_, _)
			=> Avalonia.Threading.Dispatcher.UIThread.Invoke(refreshImportMenu);
		};

		AutoScanChecked = Configuration.Instance.AutoScan;

		setyNumScanningAccounts(0);
		LibraryCommands.ScanBegin += (_, accountsLength) => Dispatcher.UIThread.Post(() => setyNumScanningAccounts(accountsLength));
		LibraryCommands.ScanEnd += (_, _) => Dispatcher.UIThread.Post(() => setyNumScanningAccounts(0));

		if (!Design.IsDesignMode)
			RemoveButtonsVisible = false;
	}

	public void ToggleAutoScan() => AutoScanChecked = !AutoScanChecked;

	public async Task AddAccountsAsync()
	{
		await MessageBox.Show("To load your Audible library, come back here to the Import menu after adding your account");
		await new LibationAvalonia.Dialogs.AccountsDialog().ShowDialog(MainWindow);
	}

	public async Task ScanAccountAsync()
	{
		using var persister = AudibleApiStorage.GetAccountsSettingsPersister();
		var firstAccount = persister.AccountsSettings.GetAll().FirstOrDefault();
		if (firstAccount != null)
			await scanLibrariesAsync(firstAccount);
	}

	public async Task ScanAccountAsync(string accountId, string registeredMarketplace)
	{
		using var persister = AudibleApiStorage.GetAccountsSettingsPersister();
		var account = FindAccount(persister.AccountsSettings, accountId, registeredMarketplace);
		if (account is null)
		{
			await MessageBox.Show(MainWindow, "That Audible account is no longer available. Refresh Accounts and try again.", "Account not found");
			return;
		}
		await scanLibrariesAsync(account);
	}

	public async Task ReauthenticateAccountAsync(string accountId, string registeredMarketplace)
	{
		try
		{
			using var persister = AudibleApiStorage.GetAccountsSettingsPersister();
			var account = FindAccount(persister.AccountsSettings, accountId, registeredMarketplace);
			if (account is null)
			{
				await MessageBox.Show(MainWindow, "That Audible account is no longer available. Refresh Accounts and try again.", "Account not found");
				return;
			}

			await ApiExtended.ReauthenticateAsync(account);
			await MessageBox.Show(MainWindow, "Audible authorization was refreshed for this account.", "Account reauthenticated");
		}
		catch (OperationCanceledException)
		{
			Serilog.Log.Information("Audible reauthentication was cancelled by the user");
		}
		catch (Exception ex)
		{
			await MessageBox.ShowAdminAlert(
				MainWindow,
				"Libation could not refresh this account's Audible authorization.",
				"Reauthentication failed",
				ex);
		}
	}

	public async Task ScanAllAccountsAsync()
	{
		using var persister = AudibleApiStorage.GetAccountsSettingsPersister();
		await scanLibrariesAsync(persister.AccountsSettings.GetAll().ToArray());
	}

	public async Task ScanSomeAccountsAsync()
	{
		var scanAccountsDialog = new LibationAvalonia.Dialogs.ScanAccountsDialog();

		if (await scanAccountsDialog.ShowDialog<DialogResult>(MainWindow) != DialogResult.OK)
			return;

		if (!scanAccountsDialog.CheckedAccounts.Any())
			return;

		await scanLibrariesAsync(scanAccountsDialog.CheckedAccounts.ToArray());
	}

	/// <summary>
	/// Associates one existing library title with a file selected by the user.
	/// Both Classic and contemporary presentations delegate to this owner.
	/// </summary>
	public async Task<bool> LocateBookFileAsync(LibraryBook libraryBook)
	{
		ArgumentNullException.ThrowIfNull(libraryBook);
		try
		{
			var options = new FilePickerOpenOptions
			{
				Title = $"Locate the audiobook file for {libraryBook.Book.Title}",
				AllowMultiple = false,
				FileTypeFilter = [new("All files (*.*)") { Patterns = ["*"] }],
			};
			var booksPath = Configuration.Instance.Books?.PathWithoutPrefix;
			if (!string.IsNullOrWhiteSpace(booksPath))
				options.SuggestedStartLocation = await MainWindow.StorageProvider.TryGetFolderFromPathAsync(booksPath);

			var selectedFile = (await MainWindow.StorageProvider.OpenFilePickerAsync(options))
				.SingleOrDefault()?.TryGetLocalPath();
			if (selectedFile is null)
				return false;

			FilePathCache.Insert(libraryBook.Book.AudibleProductId, selectedFile);
			return true;
		}
		catch (Exception ex)
		{
			await MessageBox.ShowAdminAlert(
				MainWindow,
				"Libation could not associate the selected file with this title.",
				"Could not locate audiobook file",
				ex);
			return false;
		}
	}

	public async Task RemoveBooksAsync()
	{
		// if 0 accounts, this will not be visible
		// if 1 account, run scanLibrariesRemovedBooks() on this account
		// if multiple accounts, another menu set will open. do not run scanLibrariesRemovedBooks()
		using var persister = AudibleApiStorage.GetAccountsSettingsPersister();
		var accounts = persister.AccountsSettings.GetAll();

		if (accounts.Count != 1)
			return;

		var firstAccount = accounts.Single();
		await scanLibrariesRemovedBooks(firstAccount);
	}

	// selectively remove books from all accounts
	public async Task RemoveBooksAllAsync()
	{
		using var persister = AudibleApiStorage.GetAccountsSettingsPersister();
		var allAccounts = persister.AccountsSettings.GetAll();
		await scanLibrariesRemovedBooks(allAccounts.ToArray());
	}

	public async Task RemoveBooksBtn()
	{
		RemoveBooksButtonEnabled = false;
		await ProductsDisplay.RemoveCheckedBooksAsync();
		RemoveBooksButtonEnabled = true;
	}

	public async Task DoneRemovingBtn()
	{
		RemoveButtonsVisible = false;

		ProductsDisplay.DoneRemovingBooks();

		//Restore the filter
		await PerformFilter(lastGoodFilter);
	}

	// selectively remove books from some accounts
	public async Task RemoveBooksSomeAsync()
	{
		var scanAccountsDialog = new LibationAvalonia.Dialogs.ScanAccountsDialog();

		if (await scanAccountsDialog.ShowDialog<DialogResult>(MainWindow) != DialogResult.OK)
			return;

		if (!scanAccountsDialog.CheckedAccounts.Any())
			return;

		await scanLibrariesRemovedBooks(scanAccountsDialog.CheckedAccounts.ToArray());
	}

	public async Task LocateAudiobooksAsync()
	{
		var result = await MessageBox.Show(
			MainWindow,
			Configuration.GetHelpText(nameof(LibationAvalonia.Dialogs.LocateAudiobooksDialog)),
			"Locate Previously-Liberated Audiobook Files",
			MessageBoxButtons.OKCancel,
			MessageBoxIcon.Information,
			MessageBoxDefaultButton.Button1);

		if (result is DialogResult.OK)
		{
			var locateDialog = new LibationAvalonia.Dialogs.LocateAudiobooksDialog();
			await locateDialog.ShowDialog(MainWindow);
		}
	}

	public async Task LocateAudiobooksFromDropAsync(IReadOnlyList<string> paths)
	{
		ArgumentNullException.ThrowIfNull(paths);
		var folder = paths
			.Select(path => Directory.Exists(path)
				? path
				: File.Exists(path) ? Path.GetDirectoryName(path) : null)
			.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));

		if (folder is null)
		{
			await MessageBox.Show(
				MainWindow,
				"Libation could not access a local folder from the dropped items. Use Browse and choose a folder instead.",
				"Dropped Location Unavailable");
			return;
		}

		var locateDialog = new LibationAvalonia.Dialogs.LocateAudiobooksDialog(folder);
		await locateDialog.ShowDialog(MainWindow);
	}

	private void setyNumScanningAccounts(int numScanning)
	{
		_numAccountsScanning = numScanning;
		this.RaisePropertyChanged(nameof(ActivelyScanning));
		this.RaisePropertyChanged(nameof(RemoveMenuItemsEnabled));
		this.RaisePropertyChanged(nameof(ScanningText));
		RaiseGettingStartedChanged();
	}

	private async Task scanLibrariesRemovedBooks(params Account[] accounts)
	{
		//This action is meant to operate on the entire library.
		//For removing books within a filter set, use
		//Visible Books > Remove from library

		await ProductsDisplay.Filter(null);

		RemoveBooksButtonEnabled = true;
		RemoveButtonsVisible = true;

		await ProductsDisplay.ScanAndRemoveBooksAsync(accounts);
	}

	private async Task scanLibrariesAsync(params Account[]? accounts)
	{
		try
		{
			var (totalProcessed, newAdded) = await Task.Run(() => LibraryCommands.ImportAccountAsync(accounts));
			LastSuccessfulScan = DateTimeOffset.Now;
			autoScanRunner?.OnManualScanSucceeded();

			// this is here instead of ScanEnd so that the following is only possible when it's user-initiated, not automatic loop
			if (Configuration.Instance.ShowImportedStats && newAdded > 0)
				await MessageBox.Show($"Total processed: {totalProcessed}\r\nNew: {newAdded}");
		}
		catch (OperationCanceledException)
		{
			Serilog.Log.Information("Audible login attempt cancelled by user");
		}
		catch (Exception ex)
		{
			if (WebView2LoginErrorMessage.TryFindInTree(ex, out var webViewEx) && webViewEx is not null)
			{
				await MessageBox.ShowAdminAlert(
					MainWindow,
					WebView2LoginErrorMessage.ExplainerBody,
					WebView2LoginErrorMessage.Caption,
					webViewEx);
			}
			else if (NonJsonResponseExceptionExtensions.TryFindInTree(ex, out var htmlEx) && htmlEx is not null)
			{
				await MessageBox.ShowAdminAlert(
					MainWindow,
					htmlEx.GetExplainerBody(),
					NonJsonResponseExceptionExtensions.LibraryScanFailedCaption,
					htmlEx);
			}
			else
			{
				await MessageBox.ShowAdminAlert(
					MainWindow,
					"Error importing library. Please try again. If this still happens after 2 or 3 tries, stop and contact administrator",
					"Error importing library",
					ex);
			}
		}
	}

	private static Account? FindAccount(AccountsSettings settings, string accountId, string registeredMarketplace)
		=> settings.Accounts.FirstOrDefault(account =>
			string.Equals(account.AccountId, accountId, StringComparison.OrdinalIgnoreCase)
			&& string.Equals(account.Locale?.Name, registeredMarketplace, StringComparison.OrdinalIgnoreCase));

	private void refreshImportMenu()
	{
		using var persister = AudibleApiStorage.GetAccountsSettingsPersister();
		AccountsCount = persister.AccountsSettings.Accounts.Count;


		if (NativeMenu.GetMenu(MainWindow)?.Items[0] is not NativeMenuItem ss ||
			ss.Menu is not NativeMenu importMenuItem)
		{
			Serilog.Log.Logger.Error($"Unable to find {nameof(importMenuItem)}");
			return;
		}


		for (int i = importMenuItem.Items.Count - 1; i >= 2; i--)
			importMenuItem.Items.RemoveAt(i);

		if (AccountsCount < 1)
		{
			importMenuItem.Items.Add(new NativeMenuItem { Header = "No accounts yet. Add Account...", Command = ReactiveCommand.Create(AddAccountsAsync) });
		}
		else if (AccountsCount == 1)
		{
			importMenuItem.Items.Add(new NativeMenuItem { Header = "Scan Library", Command = ReactiveCommand.Create(ScanAccountAsync), Gesture = new KeyGesture(Key.S, KeyGestureHelper.MenuModifier) });
			importMenuItem.Items.Add(new NativeMenuItemSeparator());
			importMenuItem.Items.Add(new NativeMenuItem { Header = "Remove Library Books", Command = ReactiveCommand.Create(RemoveBooksAsync), Gesture = new KeyGesture(Key.R, KeyGestureHelper.MenuModifier) });
		}
		else
		{
			importMenuItem.Items.Add(new NativeMenuItem { Header = "Scan Library of All Accounts", Command = ReactiveCommand.Create(ScanAllAccountsAsync), Gesture = new KeyGesture(Key.S, KeyGestureHelper.MenuModifier) });
			importMenuItem.Items.Add(new NativeMenuItem { Header = "Scan Library of Some Accounts", Command = ReactiveCommand.Create(ScanSomeAccountsAsync), Gesture = new KeyGesture(Key.S, KeyGestureHelper.MenuModifier | KeyModifiers.Shift) });
			importMenuItem.Items.Add(new NativeMenuItemSeparator());
			importMenuItem.Items.Add(new NativeMenuItem { Header = "Remove Books from All Accounts", Command = ReactiveCommand.Create(RemoveBooksAllAsync), Gesture = new KeyGesture(Key.R, KeyGestureHelper.MenuModifier) });
			importMenuItem.Items.Add(new NativeMenuItem { Header = "Remove Books from Some Accounts", Command = ReactiveCommand.Create(RemoveBooksSomeAsync), Gesture = new KeyGesture(Key.R, KeyGestureHelper.MenuModifier | KeyModifiers.Shift) });
		}

		importMenuItem.Items.Add(new NativeMenuItemSeparator());
		importMenuItem.Items.Add(new NativeMenuItem { Header = "Locate Audiobooks...", Command = ReactiveCommand.Create(LocateAudiobooksAsync) });
	}
}
