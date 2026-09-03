using DataLayer;
using LibationAvalonia.DesignSystem.Components;
using LibationAvalonia.ViewModels;
using ReactiveUI;
using System;
using System.Globalization;
using System.Linq;

namespace LibationAvalonia.Features.Trash;

internal readonly record struct TrashItemKey(string ProductId)
{
	internal static TrashItemKey From(LibraryBook libraryBook)
		=> new(libraryBook.Book.AudibleProductId.ToUpperInvariant());
}

/// <summary>
/// Presentation over one existing trash-query row. Podcast parent rows remain
/// visible context, but can never enter a restore or permanent-delete request.
/// </summary>
public sealed class TrashItemViewModel : ViewModelBase
{
	private string searchText = string.Empty;

	internal TrashItemViewModel(LibraryBook libraryBook, string? parentTitle = null, string? relatedSearchText = null)
	{
		Update(libraryBook, parentTitle, relatedSearchText);
	}

	internal void Update(LibraryBook libraryBook, string? parentTitle = null, string? relatedSearchText = null)
	{
		ArgumentNullException.ThrowIfNull(libraryBook);
		var nextKey = TrashItemKey.From(libraryBook);
		if (LibraryBook is not null && nextKey != Key)
			throw new InvalidOperationException(global::LibationAvalonia.Properties.Resources.TrashModelsATrashRowSStableIdentityCannot);

		LibraryBook = libraryBook;
		Key = nextKey;
		Title = libraryBook.Book.TitleWithSubtitle;
		IsContextOnly = libraryBook.Book.ContentType == ContentType.Parent;
		CanSelect = libraryBook.IsDeleted && !IsContextOnly;
		string authors = string.Join(", ", libraryBook.Book.Authors.Select(author => author.Name).Where(name => !string.IsNullOrWhiteSpace(name)));
		string kind = libraryBook.Book.ContentType switch
		{
			ContentType.Parent => global::LibationAvalonia.Properties.Resources.TrashModelsPodcastSeriesContext,
			ContentType.Episode => global::LibationAvalonia.Properties.Resources.TrashModelsPodcastEpisode,
			_ => global::LibationAvalonia.Properties.Resources.TrashModelsAudiobook,
		};
		string added = libraryBook.DateAdded == default
			? global::LibationAvalonia.Properties.Resources.TrashModelsAddedDateUnavailable
			: string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.TrashModelsAdded0, libraryBook.DateAdded.ToString("d", CultureInfo.CurrentCulture));
		Detail = IsContextOnly
			? global::LibationAvalonia.Properties.Resources.TrashModelsSeriesContextForRemovedEpisodesThisRow
			: string.IsNullOrWhiteSpace(parentTitle)
				? $"{kind} · {added}"
				: string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.TrashModels0In12, kind, parentTitle, added);
		CreatorText = string.IsNullOrWhiteSpace(authors) ? null : authors;
		StatusText = IsContextOnly ? global::LibationAvalonia.Properties.Resources.TrashModelsSeriesContext : libraryBook.IsAudiblePlus ? global::LibationAvalonia.Properties.Resources.TrashModelsRemovedAudiblePlus : global::LibationAvalonia.Properties.Resources.TrashModelsRemoved;
		Status = IsContextOnly ? LibationStatusKind.Unavailable : LibationStatusKind.NeedsAttention;
		AccessibleName = IsContextOnly
			? $"{Title}. {Detail}"
			: string.Format(global::System.Globalization.CultureInfo.CurrentCulture, global::LibationAvalonia.Properties.Resources.TrashModelsSelect0ForRestoreOrPermanentDeletion, Title, Detail);
		searchText = string.Join(" ", new[]
		{
			Title,
			CreatorText,
			Detail,
			StatusText,
			libraryBook.Book.AudibleProductId,
			parentTitle,
			relatedSearchText,
		}.Where(value => !string.IsNullOrWhiteSpace(value)));
		if (!CanSelect && IsSelected)
			IsSelected = false;

		foreach (var property in new[]
		{
			nameof(Title), nameof(CreatorText), nameof(Detail), nameof(StatusText), nameof(Status),
			nameof(AccessibleName), nameof(IsContextOnly), nameof(CanSelect),
		})
			this.RaisePropertyChanged(property);
	}

	internal TrashItemKey Key { get; private set; }
	internal LibraryBook LibraryBook { get; private set; } = null!;
	public string Title { get; private set; } = string.Empty;
	public string? CreatorText { get; private set; }
	public string Detail { get; private set; } = string.Empty;
	public string StatusText { get; private set; } = string.Empty;
	public LibationStatusKind Status { get; private set; }
	public string AccessibleName { get; private set; } = string.Empty;
	public bool IsContextOnly { get; private set; }
	public bool CanSelect { get; private set; }
	public bool IsSelected
	{
		get => field;
		set
		{
			bool next = CanSelect && value;
			if (field == next)
				return;
			this.RaiseAndSetIfChanged(ref field, next);
			SelectionChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	internal event EventHandler? SelectionChanged;
	internal bool Matches(string query) => searchText.Contains(query, StringComparison.CurrentCultureIgnoreCase);
}
