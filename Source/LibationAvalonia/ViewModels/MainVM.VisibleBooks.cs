using ApplicationServices;
using Avalonia.Threading;
using DataLayer;
using LibationAvalonia.Dialogs;
using LibationUiBase.Forms;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LibationAvalonia.ViewModels;

partial class MainVM
{
	private int _visibleNotLiberated = 0;
	private int _visibleCount = 0;

	/// <summary> The Bottom-right visible book count status text </summary>
	public string VisibleCountText => $"Visible: {_visibleCount}";
	/// <summary> The Visible Books menu item header text </summary>
	public string VisibleCountMenuItemText => menufyText($"Visible Books {_visibleCount}");
	/// <summary> Indicates if any of the books visible in the Products Display haven't been liberated </summary>
	public bool AnyVisibleNotLiberated => _visibleNotLiberated > 0;
	/// <summary> The "Liberate Visible Books" menu item header text (submenu item of the "Liberate Menu" menu item) </summary>
	public string LiberateVisibleToolStripText { get; private set; } = "Liberate _Visible Books: 0";
	/// <summary> The "Liberate" menu item header text (submenu item of the "Visible Books" menu item) </summary>
	public string LiberateVisibleToolStripText_2 { get; private set; } = menufyText("Liberate: 0");

	private void Configure_VisibleBooks()
	{
		LibraryCommands.BookUserDefinedItemCommitted += setLiberatedVisibleMenuItemAsync;
		ProductsDisplay.VisibleCountChanged += ProductsDisplay_VisibleCountChanged;
	}

	private void setVisibleCount(int visibleCount)
	{
		_visibleCount = visibleCount;
		this.RaisePropertyChanged(nameof(VisibleCountText));
		this.RaisePropertyChanged(nameof(VisibleCountMenuItemText));
		updateNoMatchesVisible();
	}

	/// <summary>
	/// An empty grid only needs explaining when a filter emptied it. Mutually exclusive with the
	/// getting-started panel, which stands down while a filter is applied.
	/// </summary>
	private void updateNoMatchesVisible()
		=> NoMatchesVisible = _visibleCount == 0 && HasActiveFilter;

	private void setVisibleNotLiberatedCount(int visibleNotLiberated)
	{
		_visibleNotLiberated = visibleNotLiberated;

		LiberateVisibleToolStripText
			= AnyVisibleNotLiberated
			? "Liberate " + menufyText($"Visible Books: {visibleNotLiberated}")
			: "All visible books are liberated";

		LiberateVisibleToolStripText_2
			= AnyVisibleNotLiberated
			? menufyText($"Liberate: {visibleNotLiberated}")
			: "All visible books are liberated";

		this.RaisePropertyChanged(nameof(AnyVisibleNotLiberated));
		this.RaisePropertyChanged(nameof(LiberateVisibleToolStripText));
		this.RaisePropertyChanged(nameof(LiberateVisibleToolStripText_2));
	}

	public async void ProductsDisplay_VisibleCountChanged(object? sender, int qty)
	{
		setVisibleCount(qty);
		await setLiberatedVisibleMenuItemAsync();
	}

	private async void setLiberatedVisibleMenuItemAsync(object? _, object __)
		=> await setLiberatedVisibleMenuItemAsync();


	public async void LiberateVisible() => await ProcessVisibleAsync();

	/// <summary>
	/// Awaitable owner operation for contemporary surfaces. The legacy menu keeps
	/// its <see cref="LiberateVisible"/> event-handler wrapper.
	/// </summary>
	public async Task ProcessVisibleAsync()
	{
		try
		{
			if (await ProcessQueue.QueueDownloadDecryptAsync(ProductsDisplay.GetVisibleBookEntries().UnLiberated().ToArray()))
				setQueueCollapseState(false);
		}
		catch (Exception ex)
		{
			Serilog.Log.Logger.Error(ex, "An error occurred while backing up visible library books");
		}
	}

	/// <summary>Contemporary Tools entry point with an explicit, cancel-default scope confirmation.</summary>
	public async Task ProcessVisibleConfirmedAsync()
	{
		var eligible = ProductsDisplay.GetVisibleBookEntries().UnLiberated().ToArray();
		if (eligible.Length == 0)
			return;

		var result = await MessageBox.Show(
			MainWindow,
			$"Queue {eligible.Length} visible title(s) that still need processing? This can download Audible files, decrypt audiobooks, and write output into the configured Books location.",
			"Process visible titles?",
			MessageBoxButtons.YesNo,
			MessageBoxIcon.Question,
			MessageBoxDefaultButton.Button2);
		if (result != DialogResult.Yes)
			return;

		try
		{
			if (await ProcessQueue.QueueDownloadDecryptAsync(eligible))
				setQueueCollapseState(false);
		}
		catch (Exception ex)
		{
			Serilog.Log.Logger.Error(ex, "An error occurred while backing up visible library books");
			await MessageBox.ShowAdminAlert(
				MainWindow,
				"Libation could not queue the visible titles for processing.",
				"Could not process visible titles",
				ex);
		}
	}

	public async Task ReplaceTagsAsync()
	{
		var dialog = new TagsBatchDialog();
		var result = await dialog.ShowDialog<DialogResult>(MainWindow);
		if (result != DialogResult.OK)
			return;

		var visibleLibraryBooks = ProductsDisplay.GetVisibleBookEntries();

		var confirmationResult = await MessageBox.ShowConfirmationDialog(
			MainWindow,
			visibleLibraryBooks,
			// do not use `$` string interpolation. See impl.
			"Are you sure you want to replace tags in {0}?",
			"Replace tags?");

		if (confirmationResult != DialogResult.Yes)
			return;

		await visibleLibraryBooks.UpdateTagsAsync(dialog.NewTags);
	}

	public Task<UserActionResult> AddTagsToBooksAsync(IReadOnlyList<LibraryBook> books)
		=> UpdateTagsForBooksAsync(books, addTags: true);

	public Task<UserActionResult> ReplaceTagsForBooksAsync(IReadOnlyList<LibraryBook> books)
		=> UpdateTagsForBooksAsync(books, addTags: false);

	private async Task<UserActionResult> UpdateTagsForBooksAsync(IReadOnlyList<LibraryBook> books, bool addTags)
	{
		ArgumentNullException.ThrowIfNull(books);
		var selectedBooks = books.Where(book => book?.Book is not null).ToArray();
		if (selectedBooks.Length == 0)
			return new(UserActionOutcome.NoChange, "Select at least one Current Flight title before changing tags.");

		var dialog = new TagsBatchDialog(addTags);
		var result = await dialog.ShowDialog<DialogResult>(MainWindow);
		if (result != DialogResult.OK)
			return new(UserActionOutcome.Cancelled, addTags ? "Add tags cancelled. No metadata was changed." : "Replace tags cancelled. No metadata was changed.");

		var incoming = (dialog.NewTags ?? string.Empty)
			.Split(null as char[], StringSplitOptions.RemoveEmptyEntries)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();
		if (addTags && incoming.Length == 0)
			return new(UserActionOutcome.NoChange, "No tags were entered, so no metadata was changed.");

		var confirmationResult = await MessageBox.ShowConfirmationDialog(
			MainWindow,
			selectedBooks,
			addTags
				? "Are you sure you want to add tags to {0}? Existing tags will be kept."
				: "Are you sure you want to replace tags in {0}?",
			addTags ? "Add tags?" : "Replace tags?");
		if (confirmationResult != DialogResult.Yes)
			return new(UserActionOutcome.Cancelled, addTags ? "Add tags cancelled. No metadata was changed." : "Replace tags cancelled. No metadata was changed.");

		int changed = addTags
			? await selectedBooks.UpdateUserDefinedItemAsync(udi =>
				udi.Tags = string.Join(" ", udi.TagsEnumerated.Concat(incoming).Distinct(StringComparer.OrdinalIgnoreCase)))
			: await selectedBooks.UpdateTagsAsync(dialog.NewTags);

		if (changed == 0)
			return new(UserActionOutcome.NoChange, "The selected tags already matched, so no metadata was changed.");

		return new(
			UserActionOutcome.Completed,
			addTags
				? $"Added tags to {changed} Current Flight title(s)."
				: $"Replaced tags in {changed} Current Flight title(s).");
	}

	public async Task SetBookDownloadedAsync()
	{
		var dialog = new LiberatedStatusBatchManualDialog();
		var result = await dialog.ShowDialog<DialogResult>(MainWindow);
		if (result != DialogResult.OK)
			return;

		var visibleLibraryBooks = ProductsDisplay.GetVisibleBookEntries();

		var confirmationResult = await MessageBox.ShowConfirmationDialog(
			MainWindow,
			visibleLibraryBooks,
			// do not use `$` string interpolation. See impl.
			"Are you sure you want to replace book downloaded status in {0}?",
			"Replace downloaded status?");

		if (confirmationResult != DialogResult.Yes)
			return;

		await visibleLibraryBooks.UpdateBookStatusAsync(dialog.BookLiberatedStatus);
	}

	public async Task SetPdfDownloadedAsync()
	{
		var dialog = new LiberatedStatusBatchManualDialog(isPdf: true);
		var result = await dialog.ShowDialog<DialogResult>(MainWindow);
		if (result != DialogResult.OK)
			return;

		var visibleLibraryBooks = ProductsDisplay.GetVisibleBookEntries();

		var confirmationResult = await MessageBox.ShowConfirmationDialog(
			MainWindow,
			visibleLibraryBooks,
			// do not use `$` string interpolation. See impl.
			"Are you sure you want to replace PDF downloaded status in {0}?",
			"Replace downloaded status?");

		if (confirmationResult != DialogResult.Yes)
			return;

		await visibleLibraryBooks.UpdatePdfStatusAsync(dialog.BookLiberatedStatus);
	}

	public async Task SetDownloadedAutoAsync()
	{
		var dialog = new LiberatedStatusBatchAutoDialog();
		var result = await dialog.ShowDialog<DialogResult>(MainWindow);
		if (result != DialogResult.OK)
			return;

		var bulkSetStatus = new BulkSetDownloadStatus(ProductsDisplay.GetVisibleBookEntries(), dialog.SetDownloaded, dialog.SetNotDownloaded);
		var count = await Task.Run(bulkSetStatus.Discover);

		if (count == 0)
			return;

		var confirmationResult = await MessageBox.Show(
			bulkSetStatus.AggregateMessage,
			"Replace downloaded status?",
			MessageBoxButtons.YesNo,
			MessageBoxIcon.Question,
			MessageBoxDefaultButton.Button1);

		if (confirmationResult != DialogResult.Yes)
			return;

		await bulkSetStatus.ExecuteAsync();
	}

	public async Task RemoveVisibleAsync()
	{
		var visibleLibraryBooks = ProductsDisplay.GetVisibleBookEntries();

		var confirmationResult = await MessageBox.ShowConfirmationDialog(
			MainWindow,
			visibleLibraryBooks,
			// do not use `$` string interpolation. See impl.
			"Are you sure you want to remove {0} from Libation's library?",
			"Remove books from Libation?",
			MessageBoxDefaultButton.Button2);

		if (confirmationResult is DialogResult.Yes)
			await visibleLibraryBooks.RemoveBooksAsync();
	}

	private async Task setLiberatedVisibleMenuItemAsync()
	{
		try
		{
			var visible = ProductsDisplay.GetVisibleBookEntries();
			var libraryStats = await Task.Run(() => LibraryCommands.GetCounts(visible));
			await Dispatcher.UIThread.InvokeAsync(() => setVisibleNotLiberatedCount(libraryStats.PendingBooks));
		}
		// Every caller is an async void event handler, where a failure is not a faulted task anyone awaits but
		// an unhandled exception that closes Libation. Counting these books reads the file system, which can
		// fail at any moment for reasons that have nothing to do with the app - a Books folder on a drive that
		// was just unplugged is enough. A stale number above a menu item is not worth the session.
		catch (Exception ex)
		{
			Serilog.Log.Logger.Error(ex, "Error counting the visible books which are not yet liberated");
		}
	}
}
