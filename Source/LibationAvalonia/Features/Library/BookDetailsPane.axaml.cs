using Avalonia.Controls;
using Avalonia.Interactivity;
using System;

namespace LibationAvalonia.Features.Library;

public partial class BookDetailsPane : UserControl
{
	public BookDetailsPane() => InitializeComponent();

	public event EventHandler<LibraryBookItemViewModel>? ProcessRequested;
	public event EventHandler<LibraryBookItemViewModel>? DownloadRequested;
	public event EventHandler<LibraryBookItemViewModel>? RevealRequested;
	public event EventHandler<LibraryBookItemViewModel>? EditTagsRequested;
	public event EventHandler<LibraryBookItemViewModel>? ViewSeriesRequested;

	private void Process_Click(object? sender, RoutedEventArgs e) => Raise(sender, ProcessRequested);
	private void Download_Click(object? sender, RoutedEventArgs e) => Raise(sender, DownloadRequested);
	private void Reveal_Click(object? sender, RoutedEventArgs e) => Raise(sender, RevealRequested);
	private void EditTags_Click(object? sender, RoutedEventArgs e) => Raise(sender, EditTagsRequested);
	private void ViewSeries_Click(object? sender, RoutedEventArgs e) => Raise(sender, ViewSeriesRequested);

	private void Raise(object? sender, EventHandler<LibraryBookItemViewModel>? handler)
	{
		if (sender is Control { DataContext: LibraryBookItemViewModel item })
			handler?.Invoke(this, item);
	}
}
