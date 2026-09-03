using DataLayer;
using LibationAvalonia.DesignSystem.Components;
using LibationAvalonia.Features.Flight;
using LibationAvalonia.ViewModels;
using LibationFileManager;
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

public sealed class GalleryRowViewModel : ViewModelBase
{
	private IReadOnlyList<LibraryBookItemViewModel> items;

	public GalleryRowViewModel(int index, IReadOnlyList<LibraryBookItemViewModel> items)
	{
		Index = index;
		this.items = items;
	}

	public int Index { get; }
	public IReadOnlyList<LibraryBookItemViewModel> Items => items;

	internal void ReplaceItems(IReadOnlyList<LibraryBookItemViewModel> replacement)
	{
		if (items.SequenceEqual(replacement))
			return;
		items = replacement;
		this.RaisePropertyChanged(nameof(Items));
	}
}

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
	public string Title => entry.Title ?? global::LibationAvalonia.Properties.Resources.DownloadsModelsUntitledAudiobook;
	public string Author => entry.Authors ?? string.Empty;
	public string Narrator => entry.Narrators ?? string.Empty;
	public string Duration => entry.Length ?? string.Empty;
	public string Series => entry.Series ?? string.Empty;
	public string Description => entry.Description ?? string.Empty;
	public string Account => entry.Account ?? string.Empty;
	public string Category => entry.Category ?? string.Empty;
	public string Tags => entry.BookTags ?? string.Empty;
	public string PurchaseDate => entry.PurchaseDate ?? string.Empty;
	public string DateAdded => entry.DateAdded == default
		? string.Empty
		: entry.DateAdded.ToString("d", CultureInfo.CurrentCulture);
	public string RatingText => entry.MyRating?.ToString() ?? global::LibationAvalonia.Properties.Resources.LibraryModelsNotRated;
	public string MarketplaceText => string.IsNullOrWhiteSpace(LibraryBook.Book.Locale)
		? global::LibationAvalonia.Properties.Resources.LibraryModelsNotRecorded
		: LibraryBook.Book.Locale;
	public string ReleaseDateText => LibraryBook.Book.DatePublished?.ToString("d", CultureInfo.CurrentCulture) ?? global::LibationAvalonia.Properties.Resources.LibraryModelsNotRecorded;
	public string QualityVersionText => BuildQualityVersionText(entry.LastDownload);
	public string OutputPathText => AudibleFileStorage.Audio.GetPath(ProductId)?.ShortPathName ?? global::LibationAvalonia.Properties.Resources.LibraryModelsNotCreatedYet;
	public string LocalStateText => LibraryBook.Book.AudioExists ? global::LibationAvalonia.Properties.Resources.LibraryModelsLocalAudioAvailable : global::LibationAvalonia.Properties.Resources.LibraryModelsLocalAudioNotCreated;
	public string PdfStateText => LibraryBook.Book.HasPdf ? global::LibationAvalonia.Properties.Resources.LibraryModelsPDFAvailableFromAudible : global::LibationAvalonia.Properties.Resources.LibraryModelsNoPDFIsAssociatedWithThisTitle;
	public bool CanDownload => new GridContextMenu([entry], '_').DownloadBookEnabled;
	public bool CanReveal => LibraryBook.Book.AudioExists;
	public bool CanViewSeries => LibraryBook.Book.SeriesLink.Any();
	public LibationStatusKind Status => LibraryBook.AbsentFromLastScan
		? LibationStatusKind.Unavailable
		: LibraryBook.Book.UserDefinedItem.BookStatus switch
		{
			LiberatedStatus.Liberated => LibationStatusKind.Completed,
			LiberatedStatus.PartialDownload => LibationStatusKind.Downloaded,
			LiberatedStatus.Error => LibationStatusKind.Failed,
			_ => LibationStatusKind.DownloadPending,
		};
	public string StatusText => Status switch
	{
		LibationStatusKind.Unavailable => global::LibationAvalonia.Properties.Resources.LibraryModelsUnavailableAfterTheLatestScan,
		LibationStatusKind.Completed => global::LibationAvalonia.Properties.Resources.CellarOverviewViewCompleted,
		LibationStatusKind.Downloaded => global::LibationAvalonia.Properties.Resources.LibraryModelsDownloadedProcessingPending,
		LibationStatusKind.Failed => global::LibationAvalonia.Properties.Resources.DownloadsModelsNeedsAttention,
		_ => global::LibationAvalonia.Properties.Resources.DownloadsModelsDownloadPending,
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

	public string SelectionText => IsSelected ? global::LibationAvalonia.Properties.Resources.LibraryModelsSelected : string.Empty;
	public string AccessibleName
		=> string.Join(", ", new[] { Title, Author, StatusText, IsSelected ? global::LibationAvalonia.Properties.Resources.LibraryModelsSelected2 : null, IsFocused ? global::LibationAvalonia.Properties.Resources.LibraryModelsFocused : null }
			.Where(value => !string.IsNullOrWhiteSpace(value)));

	private static string BuildQualityVersionText(LastDownloadStatus? download)
	{
		if (download?.IsValid is not true)
			return global::LibationAvalonia.Properties.Resources.LibraryModelsNoCompletedDownloadRecorded;
		var format = download.LastDownloadedFormat?.ToString() ?? global::LibationAvalonia.Properties.Resources.LibraryModelsAudio;
		var fileVersion = string.IsNullOrWhiteSpace(download.LastDownloadedFileVersion)
			? null
			: string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.LibraryModelsFileV0, download.LastDownloadedFileVersion);
		return string.Join(" · ", new[] { format, fileVersion, string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.LibraryModelsLibation0, download.LastDownloadedVersion) }
			.Where(value => !string.IsNullOrWhiteSpace(value)));
	}

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
		this.RaisePropertyChanged(nameof(MarketplaceText));
		this.RaisePropertyChanged(nameof(ReleaseDateText));
		this.RaisePropertyChanged(nameof(QualityVersionText));
		this.RaisePropertyChanged(nameof(OutputPathText));
		this.RaisePropertyChanged(nameof(LocalStateText));
		this.RaisePropertyChanged(nameof(PdfStateText));
		this.RaisePropertyChanged(nameof(CanDownload));
		this.RaisePropertyChanged(nameof(CanReveal));
		this.RaisePropertyChanged(nameof(CanViewSeries));
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
