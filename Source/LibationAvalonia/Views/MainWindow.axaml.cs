using AudibleUtilities;
using AppScaffolding;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using DataLayer;
using FileManager;
using LibationAvalonia.Dialogs;
using LibationAvalonia.DesignSystem;
using LibationAvalonia.DesignSystem.Components;
using LibationAvalonia.Features.Onboarding;
using LibationAvalonia.Shell;
using LibationAvalonia.ViewModels;
using LibationFileManager;
using LibationUiBase.Forms;
using LibationUiBase.GridView;
using ReactiveUI;
using ReactiveUI.Avalonia;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace LibationAvalonia.Views;

public partial class MainWindow : ReactiveWindow<MainVM>
{
	private readonly Control classicContent;
	private readonly double classicMinWidth;
	private readonly double classicMinHeight;
	private readonly ExperienceManager? experienceManagerOverride;
	private readonly Func<AppShellView>? contemporaryShellFactory;
	private AppShellView? contemporaryShell;
	private AppShellViewModel? contemporaryShellViewModel;
	private OnboardingViewModel? onboardingViewModel;
	private List<LibraryBook>? loadedLibrary;
	private bool isOpened;
	private bool contemporaryShellFailureNoticePending;

	public MainWindow() : this(null, null) { }

	internal MainWindow(
		ExperienceManager? experienceManagerOverride,
		Func<AppShellView>? contemporaryShellFactory)
	{
		this.experienceManagerOverride = experienceManagerOverride;
		this.contemporaryShellFactory = contemporaryShellFactory;
		if (Design.IsDesignMode)
			Configuration.CreateMockInstance();

		ApiExtended.LoginChoiceFactory = account => Dispatcher.UIThread.Invoke(() => new Dialogs.Login.AvaloniaLoginChoiceEager(account));

		AudibleApiStorage.LoadError += AudibleApiStorage_LoadError;
		InitializeComponent();
		classicContent = (Control)(Content ?? throw new InvalidOperationException("MainWindow did not load its classic content."));
		classicMinWidth = MinWidth;
		classicMinHeight = MinHeight;
		DataContext = new MainVM(this);
		Configure_Upgrade();

		Opened += MainWindow_Opened;
		Activated += (_, _) => App.ExperienceManager?.RefreshSystemPreferences();
		Closing += MainWindow_Closing;

		KeyBindings.Add(new KeyBinding { Command = ReactiveCommand.Create(selectAndFocusSearchBox), Gesture = new KeyGesture(Key.F, KeyGestureHelper.CommandModifier) });
		KeyBindings.Add(new KeyBinding { Command = ReactiveCommand.Create(selectAndFocusSearchBox), Gesture = new KeyGesture(Key.K, KeyGestureHelper.CommandModifier) });
		KeyBindings.Add(new KeyBinding { Command = ReactiveCommand.Create(closeContemporaryTransientSurface), Gesture = new KeyGesture(Key.Escape) });
		KeyBindings.Add(new KeyBinding { Command = ReactiveCommand.Create(processContemporaryFlight), Gesture = new KeyGesture(Key.Enter, KeyGestureHelper.CommandModifier) });
		KeyBindings.Add(new KeyBinding { Command = ReactiveCommand.Create(cycleContemporaryFocusRegion), Gesture = new KeyGesture(Key.F6) });
		KeyBindings.Add(new KeyBinding { Command = ReactiveCommand.Create(() => NavigateContemporary(AppRouteId.Overview)), Gesture = new KeyGesture(Key.D1, KeyGestureHelper.CommandModifier | KeyModifiers.Shift) });
		KeyBindings.Add(new KeyBinding { Command = ReactiveCommand.Create(() => NavigateContemporary(AppRouteId.Library)), Gesture = new KeyGesture(Key.D2, KeyGestureHelper.CommandModifier | KeyModifiers.Shift) });
		KeyBindings.Add(new KeyBinding { Command = ReactiveCommand.Create(() => NavigateContemporary(AppRouteId.Downloads)), Gesture = new KeyGesture(Key.D3, KeyGestureHelper.CommandModifier | KeyModifiers.Shift) });
		KeyBindings.Add(new KeyBinding { Command = ReactiveCommand.Create(() => NavigateContemporary(AppRouteId.Processing)), Gesture = new KeyGesture(Key.D4, KeyGestureHelper.CommandModifier | KeyModifiers.Shift) });
		KeyBindings.Add(new KeyBinding { Command = ReactiveCommand.Create(() => NavigateContemporary(AppRouteId.History)), Gesture = new KeyGesture(Key.D5, KeyGestureHelper.CommandModifier | KeyModifiers.Shift) });

		if (!Configuration.IsMacOs && ViewModel is MainVM vm)
		{
			KeyBindings.Add(new KeyBinding { Command = ReactiveCommand.Create(vm.ShowSettingsAsync), Gesture = new KeyGesture(Key.P, KeyGestureHelper.CommandModifier) });
			KeyBindings.Add(new KeyBinding { Command = ReactiveCommand.Create(vm.ShowAccountsAsync), Gesture = new KeyGesture(Key.A, KeyGestureHelper.CommandModifier | KeyModifiers.Shift) });
			KeyBindings.Add(new KeyBinding { Command = ReactiveCommand.Create(vm.ExportLibraryAsync), Gesture = new KeyGesture(Key.S, KeyGestureHelper.CommandModifier) });
		}

		Configuration.Instance.PropertyChanged += Settings_PropertyChanged;
		Configuration.Instance.PropertyChanged += ShellSettings_PropertyChanged;
		Settings_PropertyChanged(this, null);
		ApplyShellMode();
#if DEBUG
		Configure_DebugMenu();
#endif
	}

	private void NavigateContemporary(AppRouteId route)
	{
		if (!Configuration.Instance.UseContemporaryShell)
			return;
		EnsureContemporaryShell();
		contemporaryShellViewModel?.Navigation.Navigate(route);
	}

	private void closeContemporaryTransientSurface() => contemporaryShellViewModel?.CloseTransientSurface();
	private void processContemporaryFlight()
	{
		if (!Configuration.Instance.UseContemporaryShell || onboardingViewModel is not null)
			return;
		ICommand? command = contemporaryShellViewModel?.CurrentFlight.ProcessCommand;
		if (command?.CanExecute(null) == true)
			command.Execute(null);
	}
	private void cycleContemporaryFocusRegion()
	{
		if (Configuration.Instance.UseContemporaryShell && onboardingViewModel is null)
			contemporaryShell?.CycleFocusRegion();
	}

	private void ShellSettings_PropertyChanged(object? sender, Dinah.Core.PropertyChangedEventArgsEx e)
	{
		if (e.PropertyName != nameof(Configuration.UseContemporaryShell))
			return;
		// ExperienceManager subscribed before the window and commits the matching
		// resources on this dispatcher. Queue the content swap after that transaction
		// so neither opt-in nor rollback presents a one-frame mixed appearance.
		Dispatcher.UIThread.Post(() =>
		{
			// A manually reopened chooser must not mask an explicit opt-out made in
			// Settings. Automatic first-run onboarding is different: it intentionally
			// appears while the contemporary flag is still off and remains skippable.
			if (!Configuration.Instance.UseContemporaryShell
				&& onboardingViewModel?.IsManualReentry == true)
			{
				onboardingViewModel.ExitRequested -= Onboarding_ExitRequested;
				onboardingViewModel.Dispose();
				onboardingViewModel = null;
			}
			ApplyShellMode();
		}, DispatcherPriority.Normal);
	}

	private bool EnsureContemporaryShell()
	{
		if (contemporaryShell is not null)
			return true;
		if (ViewModel is not MainVM main || (experienceManagerOverride ?? App.ExperienceManager) is not { } manager)
			return false;

		AppShellViewModel? candidateViewModel = null;
		AppShellView? candidateShell = null;
		try
		{
			candidateViewModel = new(main, Configuration.Instance, manager);
			candidateViewModel.Settings.OnboardingRequested += Settings_OnboardingRequested;
			if (loadedLibrary is not null)
				candidateViewModel.Flight.ReconcileLibrary(loadedLibrary);
			candidateShell = contemporaryShellFactory?.Invoke() ?? new AppShellView();
			candidateShell.DataContext = candidateViewModel;
			contemporaryShellViewModel = candidateViewModel;
			contemporaryShell = candidateShell;
			return true;
		}
		catch (Exception ex)
		{
			if (candidateViewModel is not null)
			{
				candidateViewModel.Settings.OnboardingRequested -= Settings_OnboardingRequested;
				candidateShell?.ClearValue(DataContextProperty);
				try
				{
					candidateViewModel.Dispose();
				}
				catch (Exception disposeFailure)
				{
					StartupLog.Warning(disposeFailure, "The failed contemporary shell could not be fully released.");
				}
			}
			HandleContemporaryShellFailure(ex);
			return false;
		}
	}

	private void ApplyShellMode()
	{
		// The chooser is a first-class main-window surface so its established owner
		// dialogs remain usable. Profile commits can raise shell changes before the
		// chooser has emitted ExitRequested; keep it attached until that transaction
		// is complete.
		if (onboardingViewModel is not null)
			return;

		if (Configuration.Instance.UseContemporaryShell)
		{
			if (EnsureContemporaryShell() && contemporaryShell is not null)
			{
				try
				{
					Content = contemporaryShell;
					// Plan §8 defines this minimum for the contemporary desktop shell.
					MinWidth = 720;
					MinHeight = 560;
				}
				catch (Exception ex)
				{
					HandleContemporaryShellFailure(ex);
				}
			}
		}
		else
		{
			Content = classicContent;
			MinWidth = classicMinWidth;
			MinHeight = classicMinHeight;
		}
	}

	private void HandleContemporaryShellFailure(Exception failure)
	{
		StartupLog.Error(failure, "The contemporary shell failed to initialize. Libation restored the current interface.");
		DiscardContemporaryShell();
		contemporaryShellFailureNoticePending = true;
		if (Configuration.Instance.UseContemporaryShell)
			Configuration.Instance.UseContemporaryShell = false;

		try
		{
			Content = classicContent;
			MinWidth = classicMinWidth;
			MinHeight = classicMinHeight;
		}
		catch (Exception restoreFailure)
		{
			StartupLog.Error(restoreFailure, "The current interface could not be restored after the contemporary shell failed.");
		}

		if (isOpened)
			Dispatcher.UIThread.Post(() => _ = ShowContemporaryShellFailureAsync(), DispatcherPriority.Normal);
	}

	private void DiscardContemporaryShell()
	{
		var shell = contemporaryShell;
		var viewModel = contemporaryShellViewModel;
		contemporaryShell = null;
		contemporaryShellViewModel = null;
		if (viewModel is null)
			return;

		viewModel.Settings.OnboardingRequested -= Settings_OnboardingRequested;
		shell?.ClearValue(DataContextProperty);
		try
		{
			viewModel.Dispose();
		}
		catch (Exception ex)
		{
			StartupLog.Warning(ex, "The failed contemporary shell could not be fully released.");
		}
	}

	private async Task ShowContemporaryShellFailureAsync()
	{
		if (!contemporaryShellFailureNoticePending)
			return;
		contemporaryShellFailureNoticePending = false;
		try
		{
			await MessageBox.Show(
				this,
				"Libation could not start the redesigned interface, so it restored the current interface. "
					+ "Your library data was not changed. The failure was written to the Libation log.",
				"Redesigned interface could not start",
				MessageBoxButtons.OK,
				MessageBoxIcon.Warning);
		}
		catch (Exception ex)
		{
			StartupLog.Warning(ex, "Libation could not show the contemporary-shell fallback notice.");
		}
	}

	private void Settings_OnboardingRequested(object? sender, EventArgs e) => ShowOnboarding(isManualReentry: true);
	public bool TryShowContemporaryOnboarding()
	{
		if (!Configuration.Instance.UseContemporaryShell)
			return false;
		ShowOnboarding(isManualReentry: true);
		return onboardingViewModel is not null;
	}

	internal void ShowOnboarding(bool isManualReentry)
	{
		if (onboardingViewModel is not null || ViewModel is not MainVM main)
			return;

		OnboardingViewModel? candidate = null;
		try
		{
			candidate = new OnboardingViewModel(
				new LibationCommandAdapter(main),
				isManualReentry,
				Configuration.Instance);
			if (!isManualReentry && !candidate.ShouldOfferAutomatically)
			{
				candidate.Dispose();
				return;
			}

			onboardingViewModel = candidate;
			candidate.ExitRequested += Onboarding_ExitRequested;
			Content = new OnboardingView { DataContext = candidate };
			// Plan section 8 owns the contemporary desktop minimum.
			MinWidth = 720;
			MinHeight = 560;
		}
		catch (Exception ex)
		{
			if (candidate is not null)
			{
				candidate.ExitRequested -= Onboarding_ExitRequested;
				if (ReferenceEquals(onboardingViewModel, candidate))
					onboardingViewModel = null;
				try
				{
					candidate.Dispose();
				}
				catch (Exception disposeFailure)
				{
					StartupLog.Warning(disposeFailure, "The failed contemporary onboarding surface could not be fully released.");
				}
			}
			HandleContemporaryShellFailure(ex);
		}
	}

	private void Onboarding_ExitRequested(object? sender, OnboardingExitEventArgs e)
	{
		if (sender is not OnboardingViewModel viewModel || !ReferenceEquals(viewModel, onboardingViewModel))
			return;

		viewModel.ExitRequested -= Onboarding_ExitRequested;
		onboardingViewModel = null;
		viewModel.Dispose();
		// Saving the selected profile schedules its resource transaction first.
		// Queue the shell swap behind that commit so a new visual never inherits
		// the previous profile's resource values while it is being attached.
		Dispatcher.UIThread.Post(ApplyShellMode, DispatcherPriority.Normal);
	}

#if DEBUG
	private void Configure_DebugMenu()
	{
		var galleryItem = new MenuItem { Header = Properties.Resources.MenuComponentGalleryHeader };
		galleryItem.Click += (_, _) => ComponentGallery.ShowWindow(this);
		var simulateItem = new MenuItem { Header = "Simulate bad book failures (test dialog)..." };
		simulateItem.Click += async (_, _) =>
		{
			if (ViewModel is MainVM vm)
				await vm.SimulateBadBookFailuresAsync();
		};

		// Insert before Tour; the Separator above Tour in axaml already provides the divider.
		var items = settingsToolStripMenuItem.Items;
		var insertIndex = -1;
		for (var i = 0; i < items.Count; i++)
		{
			if (items[i] is MenuItem menuItem
				&& menuItem.Header?.ToString()?.Contains("Tour", StringComparison.OrdinalIgnoreCase) == true)
			{
				insertIndex = i;
				break;
			}
		}

		if (insertIndex < 0)
			insertIndex = items.Count;

		items.Insert(insertIndex++, galleryItem);
		items.Insert(insertIndex, simulateItem);
	}
#endif

	[Dinah.Core.PropertyChangeFilter(nameof(Configuration.Books))]
	private void Settings_PropertyChanged(object? sender, Dinah.Core.PropertyChangedEventArgsEx? e)
	{
		if (!Configuration.IsWindows)
		{
			//The books directory does not support filenames with windows' invalid characters.
			//Tell the ReplacementCharacters configuration to treat those characters as invalid.
			ReplacementCharacters.AdditionalInvalidFilenameCharacters
				= Configuration.Instance.BooksCanWriteWindowsInvalidChars ? []
				: FileSystemTest.AdditionalInvalidWindowsFilenameCharacters.ToArray();
		}
	}

	private void AudibleApiStorage_LoadError(object? sender, AccountSettingsLoadErrorEventArgs e)
	{
		try
		{
			//Backup AccountSettings.json and create a new, empty file.
			var backupFile =
				FileUtility.SaferMoveToValidPath(
					e.SettingsFilePath,
					e.SettingsFilePath,
					Configuration.Instance.ReplacementCharacters,
					"bak");
			AudibleApiStorage.EnsureAccountsSettingsFileExists();
			e.Handled = true;

			showAccountSettingsRecoveredMessage(backupFile);
		}
		catch
		{
			showAccountSettingsUnrecoveredMessage();
		}

		async void showAccountSettingsRecoveredMessage(LongPath backupFile)
		{
			var ex = e.GetException();
			var body = AccountSettingsDecryptFailure.TryFindInTree(ex, out _)
				? AccountSettingsDecryptFailure.GetRecoveredDialogBody(ex, backupFile.PathWithoutPrefix)
				: $"""
					Libation could not load your account settings, so it had created a new, empty account settings file.

					You will need to re-add you Audible account(s) before scanning or downloading.

					The old account settings file has been archived at '{backupFile.PathWithoutPrefix}'

					{ex}
					""";

			await MessageBox.Show(
				this,
				body,
				AccountSettingsDecryptFailure.LoadErrorCaption,
				MessageBoxButtons.OK,
				MessageBoxIcon.Warning);
		}

		void showAccountSettingsUnrecoveredMessage()
		{
			var messageBoxWindow = MessageBox.Show(this, $"""
			Libation could not load your account settings. The file may be corrupted, but Libation is unable to delete it.

			Please move or delete the account settings file '{e.SettingsFilePath}'

			{e.GetException().ToString()}
			""",
			"Error Loading Account Settings",
			MessageBoxButtons.OK);

			//Force the message box to show synchronously because we're not handling the exception
			//and libation will crash after the event handler returns
			var frame = new DispatcherFrame();
			_ = messageBoxWindow.ContinueWith(static (_, s) => (s as DispatcherFrame)?.Continue = false, frame);
			Dispatcher.UIThread.PushFrame(frame);
			messageBoxWindow.GetAwaiter().GetResult();
		}
	}

	private async void MainWindow_Opened(object? sender, EventArgs e)
	{
		isOpened = true;
		await MessageBox.VerboseLoggingWarning_ShowIfTrue();
		await ShowContemporaryShellFailureAsync();

		if (AudibleFileStorage.BooksDirectory is null)
		{
			var result = await MessageBox.Show(
				this,
				"Please set a valid Books location in the settings dialog.",
				"Books Directory Not Set",
				MessageBoxButtons.OKCancel,
				MessageBoxIcon.Warning,
				MessageBoxDefaultButton.Button1);

			if (result is DialogResult.OK)
				await new SettingsDialog().ShowDialog(this);
		}

		ShowOnboarding(isManualReentry: false);
		await RunCapturePlanIfRequestedAsync();
	}

	private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
	{
		productsDisplay?.CloseImageDisplay();
		contemporaryShell?.CloseImageDisplay();
		if (onboardingViewModel is not null)
		{
			onboardingViewModel.ExitRequested -= Onboarding_ExitRequested;
			onboardingViewModel.Dispose();
			onboardingViewModel = null;
		}
		if (contemporaryShellViewModel is not null)
			contemporaryShellViewModel.Settings.OnboardingRequested -= Settings_OnboardingRequested;
		contemporaryShellViewModel?.Dispose();
		Configuration.Instance.PropertyChanged -= Settings_PropertyChanged;
		Configuration.Instance.PropertyChanged -= ShellSettings_PropertyChanged;
		this.SaveSizeAndLocation(Configuration.Instance);
		//This is double firing with 11.3.9
		Closing -= MainWindow_Closing;
	}

	private void selectAndFocusSearchBox()
	{
		if (Content == contemporaryShell && contemporaryShell is not null)
			contemporaryShell.SelectAndFocusSearch();
		else
		{
			filterSearchTb.SelectAll();
			filterSearchTb.Focus();
		}
	}

	public async Task OnLibraryLoadedAsync(List<LibraryBook> initialLibrary)
	{
		loadedLibrary = initialLibrary;
		//Get the ViewModel before crossing the await boundary
		if (ViewModel is not MainVM vm)
			return;

		if (QuickFilters.UseDefault)
			await vm.PerformFilter(QuickFilters.Filters.FirstOrDefault());

		vm.BindToGridTask = Task.WhenAll(
			vm.SetBackupCountsAsync(initialLibrary),
			vm.RefreshBooksInTrashAsync(),
			Task.Run(() => vm.ProductsDisplay.BindToGridAsync(initialLibrary)));

		await vm.BindToGridTask;
		contemporaryShellViewModel?.Flight.ReconcileLibrary(initialLibrary);
	}

	public void ProductsDisplay_LiberateClicked(object _, IList<LibraryBook> libraryBook, Configuration config) => ViewModel?.LiberateClicked(libraryBook, config);
	public void ProductsDisplay_LiberateSeriesClicked(object _, SeriesEntry series) => ViewModel?.LiberateSeriesClicked(series);
	public void ProductsDisplay_ConvertToMp3Clicked(object _, LibraryBook[] libraryBook) => ViewModel?.ConvertToMp3Clicked(libraryBook);

	BookDetailsDialog? bookDetailsForm;
	public void ProductsDisplay_TagsButtonClicked(object _, LibraryBook libraryBook)
	{
		if (bookDetailsForm is null || !bookDetailsForm.IsVisible)
		{
			bookDetailsForm = new BookDetailsDialog(libraryBook);
			bookDetailsForm.Show(this);
		}
		else
			bookDetailsForm.LibraryBook = libraryBook;
	}

	public async void filterSearchTb_KeyPress(object _, KeyEventArgs e)
	{
		if (e.Key == Key.Return && ViewModel is not null)
		{
			await ViewModel.FilterBtn(filterSearchTb.Text ?? string.Empty);

			// silence the 'ding'
			e.Handled = true;
		}
	}

	private async void ClearFilterButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
	{
		if (ViewModel is null)
			return;
		await ViewModel.FilterBtn(string.Empty);
		// Typed text lives only in the TextBox (OneWay binding). If the VM filter was already empty,
		// PerformFilter does not refresh the binding, so clear the control explicitly (WinForms sets Text in performFilter).
		filterSearchTb.Text = string.Empty;
	}

	private void Configure_Upgrade()
	{
		setProgressVisible(false);
#pragma warning disable CS8321 // Local function is declared but never used
		async Task upgradeAvailable(LibationUiBase.UpgradeEventArgs e)
		{
			if (ViewModel is not null)
				ViewModel.ApplicationUpdateState = $"Version {e.UpgradeProperties.LatestRelease:3} is available.";
			var notificationResult = await new UpgradeNotificationDialog(e.UpgradeProperties, e.CapUpgrade, e.UpgradeUnavailableReason).ShowDialogAsync(this);

			e.Ignore = notificationResult == DialogResult.Ignore;
			e.InstallUpgrade = notificationResult == DialogResult.OK;
		}
#pragma warning restore CS8321 // Local function is declared but never used

		var upgrader = new LibationUiBase.Upgrader();
		upgrader.DownloadProgress += async (_, e) => await Dispatcher.UIThread.InvokeAsync(() => ViewModel?.DownloadProgress = e.ProgressPercentage);
		upgrader.DownloadBegin += async (_, _) => await Dispatcher.UIThread.InvokeAsync(() =>
		{
			setProgressVisible(true);
			if (ViewModel is not null)
				ViewModel.ApplicationUpdateState = "Downloading the selected application update.";
		});
		upgrader.DownloadCompleted += async (_, _) => await Dispatcher.UIThread.InvokeAsync(() =>
		{
			setProgressVisible(false);
			if (ViewModel is not null)
				ViewModel.ApplicationUpdateState = "The application update download completed.";
		});
		upgrader.UpgradeFailed += async (_, message) => await Dispatcher.UIThread.InvokeAsync(() =>
		{
			setProgressVisible(false);
			if (ViewModel is not null)
				ViewModel.ApplicationUpdateState = "The application update check or install failed. Open About to try again.";
			MessageBox.Show(this, message, "Upgrade Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
		});

#if !DEBUG
		Opened += async (_, _) =>
		{
			await upgrader.CheckForUpgradeAtStartupAsync(upgradeAvailable);
			if (Configuration.Instance.CheckForUpgradesAtStartup
				&& ViewModel?.ApplicationUpdateState == "Update status has not been checked in this session.")
				ViewModel.ApplicationUpdateState = string.Empty;
		};
#endif
	}

	private void setProgressVisible(bool visible) => ViewModel?.DownloadProgress = visible ? 0 : null;

	public SearchSyntaxDialog ShowSearchSyntaxDialog()
	{
		var dialog = new SearchSyntaxDialog();
		dialog.TagDoubleClicked += Dialog_TagDoubleClicked;
		dialog.Closed += Dialog_Closed;
		if (Content == contemporaryShell && contemporaryShell is not null)
			contemporaryShell.SetFilterHelpEnabled(false);
		else
			filterHelpBtn.IsEnabled = false;
		dialog.Show(this);
		return dialog;

		void Dialog_Closed(object? sender, EventArgs e)
		{
			dialog.TagDoubleClicked -= Dialog_TagDoubleClicked;
			if (Content == contemporaryShell && contemporaryShell is not null)
				contemporaryShell.SetFilterHelpEnabled(true);
			else
				filterHelpBtn.IsEnabled = true;
		}
		void Dialog_TagDoubleClicked(object? sender, string tag)
		{
			if (Content == contemporaryShell && contemporaryShell is not null)
			{
				contemporaryShell.InsertSearchTag(tag);
				return;
			}
			var text = filterSearchTb.Text;
			filterSearchTb.Text = text?.Insert(Math.Min(Math.Max(0, filterSearchTb.CaretIndex), text.Length), tag);
			filterSearchTb.CaretIndex += tag.Length;
			filterSearchTb.Focus();
		}
	}
}
