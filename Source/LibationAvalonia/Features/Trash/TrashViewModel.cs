using LibationAvalonia.DesignSystem.Components;
using LibationAvalonia.Features.Tools;
using LibationAvalonia.Shell;
using LibationAvalonia.ViewModels;
using ReactiveUI;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;

namespace LibationAvalonia.Features.Trash;

/// <summary>
/// Gateway to the existing searchable restore/permanent-delete workflow. No direct
/// destructive command is surfaced on the destination itself.
/// </summary>
public sealed class TrashViewModel : SecondaryDestinationViewModel, IRoutePresentation
{
	private readonly MainVM main;

	public TrashViewModel(ILibationCommandAdapter commands)
	{
		ArgumentNullException.ThrowIfNull(commands);
		main = commands.Main;
		OpenTrashCommand = CreateOwnerCommand(
			commands.ShowTrashAsync,
			"open Trash",
			"Libation could not open Trash. No library records were changed.");
		RefreshCommand = CreateOwnerCommand(
			main.RefreshBooksInTrashAsync,
			"refresh the Trash count",
			"Libation could not refresh the Trash count. No library records were changed.");
		main.PropertyChanged += Main_PropertyChanged;
	}

	public int Count => main.BooksInTrash;
	public bool HasItems => Count > 0;
	public string CountText => Count == 1 ? "1 title in Trash" : $"{Count.ToString("N0", CultureInfo.CurrentCulture)} titles in Trash";
	public string StatusText => HasItems ? "Restore or review removed library records" : "Trash is empty";
	public LibationStatusKind Status => HasItems ? LibationStatusKind.NeedsAttention : LibationStatusKind.Completed;
	public ICommand OpenTrashCommand { get; }
	public ICommand RefreshCommand { get; }
	public string RouteEyebrow => "Removed library records";
	public string RouteTitle => "Trash";
	public string RouteSubtitle => "Review titles hidden from the Library before restoring or permanently deleting records.";
	public RouteCommandPresentation RoutePrimaryCommand => new("Open protected Trash workflow", OpenTrashCommand);
	public System.Collections.Generic.IReadOnlyList<RouteCommandPresentation> RouteSecondaryCommands =>
	[
		new("Refresh count", RefreshCommand),
	];
	public RouteStatusPresentation RouteStatusBadge => new(CountText, Status);

	private void Main_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (!string.IsNullOrEmpty(e.PropertyName) && e.PropertyName != nameof(MainVM.BooksInTrash))
			return;
		this.RaisePropertyChanged(nameof(Count));
		this.RaisePropertyChanged(nameof(HasItems));
		this.RaisePropertyChanged(nameof(CountText));
		this.RaisePropertyChanged(nameof(StatusText));
		this.RaisePropertyChanged(nameof(Status));
		this.RaisePropertyChanged(nameof(RouteStatusBadge));
	}

	protected override void DisposeCore() => main.PropertyChanged -= Main_PropertyChanged;
}
