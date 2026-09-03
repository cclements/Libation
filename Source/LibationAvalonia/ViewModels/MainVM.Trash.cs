using ApplicationServices;
using DataLayer;
using LibationUiBase.Forms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LibationAvalonia.ViewModels;

partial class MainVM
{
	public Task<IReadOnlyList<LibraryBook>> GetTrashItemsAsync(CancellationToken cancellationToken = default)
		=> Task.Run<IReadOnlyList<LibraryBook>>(
			() => DbContexts.GetDeletedLibraryBooks(),
			cancellationToken);

	public Task<int> RestoreTrashBooksAsync(IReadOnlyList<LibraryBook> books)
	{
		ArgumentNullException.ThrowIfNull(books);
		return books.Where(book => book.IsDeleted).WithoutParents().RestoreBooksAsync();
	}

	public async Task<int> PermanentlyDeleteTrashBooksConfirmedAsync(
		IReadOnlyList<LibraryBook> books,
		Avalonia.Controls.Window? confirmationOwner = null)
	{
		ArgumentNullException.ThrowIfNull(books);
		var eligible = books.Where(book => book.IsDeleted).WithoutParents().ToArray();
		if (eligible.Length == 0)
			return 0;

		var result = await MessageBox.Show(
			confirmationOwner ?? MainWindow,
			$"Permanently remove {eligible.Length} title record(s) from Libation? Existing audiobook files are not deleted. Owned titles may return the next time their Audible account is scanned.",
			"Permanently delete from Libation?",
			MessageBoxButtons.YesNo,
			MessageBoxIcon.Warning,
			MessageBoxDefaultButton.Button2);
		return result == DialogResult.Yes
			? await eligible.PermanentlyDeleteBooksAsync()
			: 0;
	}
}
