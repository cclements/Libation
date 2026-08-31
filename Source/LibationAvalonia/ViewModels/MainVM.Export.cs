using ApplicationServices;
using Avalonia.Platform.Storage;
using DataLayer;
using FileManager;
using LibationFileManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LibationAvalonia.ViewModels;

partial class MainVM
{
	private void Configure_Export() { }

	public async Task ExportLibraryAsync()
		=> _ = await ExportBooksCoreAsync(null, "Library", $"Libation Library Export {DateTime.Now:yyyy-MM-dd}", showErrorDialog: true);

	public async Task<UserActionResult> ExportSelectedBooksAsync(IReadOnlyList<LibraryBook> books)
	{
		ArgumentNullException.ThrowIfNull(books);
		if (books.Count == 0)
			return new(UserActionOutcome.NoChange, "Select at least one Current Flight title before exporting metadata.");

		return await ExportBooksCoreAsync(
			books,
			"Current Flight metadata",
			$"Libation Current Flight Export {DateTime.Now:yyyy-MM-dd}",
			showErrorDialog: false);
	}

	private async Task<UserActionResult> ExportBooksCoreAsync(
		IEnumerable<LibraryBook>? books,
		string scope,
		string suggestedFileName,
		bool showErrorDialog)
	{
		try
		{
			var selectedBooks = books?.ToArray();
			var startFolder = Configuration.Instance.Books?.PathWithoutPrefix;
			var options = new FilePickerSaveOptions
			{
				Title = $"Where to export {scope}",
				SuggestedStartLocation = startFolder is null ? null : await MainWindow.StorageProvider.TryGetFolderFromPathAsync(startFolder),
				SuggestedFileName = suggestedFileName,
				DefaultExtension = "xlsx",
				ShowOverwritePrompt = true,
				FileTypeChoices = new FilePickerFileType[]
				{
					new("Excel Workbook (*.xlsx)")
					{
						Patterns = new[] { "*.xlsx" },
						//https://gist.github.com/RhetTbull/7221ef3cfd9d746f34b2550d4419a8c2
						AppleUniformTypeIdentifiers = new[] { "org.openxmlformats.spreadsheetml.sheet" }
					},
					new("CSV files (*.csv)")
					{
						Patterns = new[] { "*.csv" },
						AppleUniformTypeIdentifiers = new[] { "public.comma-separated-values-text" }
					},
					new("JSON files (*.json)")
					{
						Patterns = new[] { "*.json" },
						AppleUniformTypeIdentifiers = new[] { "public.json" }
					},
					new("All files (*.*)") { Patterns = new[] { "*" } }
					}
			};

			var selectedFile = (await MainWindow.StorageProvider.SaveFilePickerAsync(options))?.TryGetLocalPath();

			if (selectedFile is null)
				return new(UserActionOutcome.Cancelled, $"{scope} export cancelled. No file was written.");

			var ext = FileUtility.GetStandardizedExtension(System.IO.Path.GetExtension(selectedFile));
			switch (ext)
			{
				case ".xlsx": // xlsx
				default:
					LibraryExporter.ToXlsx(selectedFile, selectedBooks);
					break;
				case ".csv": // csv
					LibraryExporter.ToCsv(selectedFile, selectedBooks);
					break;
				case ".json": // json
					LibraryExporter.ToJson(selectedFile, selectedBooks);
					break;
			}

			await MessageBox.Show($"{scope} exported to:\r\n" + selectedFile, "Metadata Exported");
			return new(
				UserActionOutcome.Completed,
				selectedBooks is null
					? "Library metadata exported."
					: $"Exported metadata for {selectedBooks.Length} Current Flight title(s).");
		}
		catch (Exception ex)
		{
			if (!showErrorDialog)
				throw;
			await MessageBox.ShowAdminAlert(MainWindow, $"Error attempting to export {scope}.", "Error exporting", ex);
			return new(UserActionOutcome.NoChange, $"Libation could not export {scope}. Review the error details and try again.");
		}
	}
}
