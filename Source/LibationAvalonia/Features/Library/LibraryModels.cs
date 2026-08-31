using DataLayer;
using LibationAvalonia.DesignSystem.Components;
using LibationAvalonia.Features.Flight;
using LibationAvalonia.ViewModels;
using LibationUiBase.GridView;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace LibationAvalonia.Features.Library;

public sealed record LibrarySortOption(
	string Label,
	string? MemberName,
	ListSortDirection Direction);

public sealed record GalleryRowViewModel(
	int Index,
	IReadOnlyList<LibraryBookItemViewModel> Items);

/// <summary>
/// Presentation wrapper over the existing GridEntry. It owns no metadata or selection;
/// changes continue to originate from GridEntry/LibraryBook and the shell-scoped FlightService.
/// </summary>
public sealed class LibraryBookItemViewModel : ViewModelBase, IDisposable
{
	private LibraryBookEntry entry;
	private bool isSelected;
	private bool isFocused;
	private bool disposed;

	internal LibraryBookItemViewModel(LibraryBookEntry entry, LibraryViewModel owner)
	{
		this.entry = entry;
		Owner = owner;
		entry.PropertyChanged += Entry_PropertyChanged;
	}

	internal LibraryViewModel Owner { get; }
	public LibraryBookEntry Entry => entry;
	public LibraryBook LibraryBook => entry.LibraryBook;
	public FlightItemId Id => FlightItemId.From(LibraryBook);
	public string ProductId => entry.AudibleProductId;
	public string Title => entry.Title ?? "Untitled audiobook";
	public string Author => entry.Authors ?? string.Empty;
	public string Narrator => entry.Narrators ?? string.Empty;
	public string Duration => string.IsNullOrWhiteSpace(entry.Length) ? "Duration unavailable" : entry.Length;
	public string Series => entry.Series ?? string.Empty;
	public string Description => entry.Description ?? string.Empty;
	public string Account => entry.Account ?? "Account unavailable";
	public string Category => entry.Category ?? string.Empty;
	public string Tags => entry.BookTags ?? string.Empty;
	public string PurchaseDate => entry.PurchaseDate ?? string.Empty;
	public string DateAdded => entry.DateAdded == default
		? "Date added unavailable"
		: entry.DateAdded.ToString("d", CultureInfo.CurrentCulture);
	public string RatingText => entry.MyRating?.ToString() ?? "Not rated";
	public string LocalStateText => LibraryBook.Book.AudioExists ? "Local audio available" : "Local audio not created";
	public string PdfStateText => LibraryBook.Book.HasPdf ? "PDF available from Audible" : "No PDF is associated with this title";
	public LibationStatusKind Status => LibraryBook.AbsentFromLastScan
		? LibationStatusKind.Unavailable
		: LibraryBook.Book.UserDefinedItem.BookStatus switch
		{
			LiberatedStatus.Liberated => LibationStatusKind.Completed,
			LiberatedStatus.Error => LibationStatusKind.Failed,
			_ => LibationStatusKind.DownloadPending,
		};
	public string StatusText => Status switch
	{
		LibationStatusKind.Unavailable => "Unavailable after the latest scan",
		LibationStatusKind.Completed => "Completed",
		LibationStatusKind.Failed => "Needs attention",
		_ => "Download pending",
	};

	public bool IsSelected
	{
		get => isSelected;
		internal set
		{
			if (isSelected == value)
				return;
			this.RaiseAndSetIfChanged(ref isSelected, value);
			this.RaisePropertyChanged(nameof(SelectionText));
			this.RaisePropertyChanged(nameof(AccessibleName));
		}
	}

	public bool IsFocused
	{
		get => isFocused;
		internal set
		{
			if (isFocused == value)
				return;
			this.RaiseAndSetIfChanged(ref isFocused, value);
			this.RaisePropertyChanged(nameof(AccessibleName));
		}
	}

	public string SelectionText => IsSelected ? "Selected" : string.Empty;
	public string AccessibleName
		=> string.Join(", ", new[] { Title, Author, StatusText, IsSelected ? "selected" : null, IsFocused ? "focused" : null }
			.Where(value => !string.IsNullOrWhiteSpace(value)));

	internal void ReplaceEntry(LibraryBookEntry replacement)
	{
		if (ReferenceEquals(entry, replacement))
			return;
		entry.PropertyChanged -= Entry_PropertyChanged;
		entry = replacement;
		entry.PropertyChanged += Entry_PropertyChanged;
		RaiseAllMetadataChanged();
	}

	private void Entry_PropertyChanged(object? sender, PropertyChangedEventArgs e) => RaiseAllMetadataChanged();

	private void RaiseAllMetadataChanged()
	{
		this.RaisePropertyChanged(nameof(Entry));
		this.RaisePropertyChanged(nameof(LibraryBook));
		this.RaisePropertyChanged(nameof(Title));
		this.RaisePropertyChanged(nameof(Author));
		this.RaisePropertyChanged(nameof(Narrator));
		this.RaisePropertyChanged(nameof(Duration));
		this.RaisePropertyChanged(nameof(Series));
		this.RaisePropertyChanged(nameof(Description));
		this.RaisePropertyChanged(nameof(Account));
		this.RaisePropertyChanged(nameof(Category));
		this.RaisePropertyChanged(nameof(Tags));
		this.RaisePropertyChanged(nameof(PurchaseDate));
		this.RaisePropertyChanged(nameof(DateAdded));
		this.RaisePropertyChanged(nameof(RatingText));
		this.RaisePropertyChanged(nameof(LocalStateText));
		this.RaisePropertyChanged(nameof(PdfStateText));
		this.RaisePropertyChanged(nameof(Status));
		this.RaisePropertyChanged(nameof(StatusText));
		this.RaisePropertyChanged(nameof(AccessibleName));
	}

	public void Dispose()
	{
		if (disposed)
			return;
		disposed = true;
		entry.PropertyChanged -= Entry_PropertyChanged;
	}
}

public sealed class LibrarySelectionProjectionChangedEventArgs(
	IReadOnlySet<string> selectedProductIds,
	string? focusedProductId) : EventArgs
{
	public IReadOnlySet<string> SelectedProductIds { get; } = selectedProductIds;
	public string? FocusedProductId { get; } = focusedProductId;
}
