using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace LibationAvalonia.DesignSystem.Components;

public partial class DropZone : UserControl
{
	public static readonly StyledProperty<string> TitleProperty = AvaloniaProperty.Register<DropZone, string>(nameof(Title), "Drop files here");
	public static readonly StyledProperty<string?> HintProperty = AvaloniaProperty.Register<DropZone, string?>(nameof(Hint));
	public static readonly StyledProperty<string?> AcceptedTypesTextProperty = AvaloniaProperty.Register<DropZone, string?>(nameof(AcceptedTypesText));
	public static readonly StyledProperty<object?> IllustrationContentProperty = AvaloniaProperty.Register<DropZone, object?>(nameof(IllustrationContent));
	public static readonly StyledProperty<string> BrowseTextProperty = AvaloniaProperty.Register<DropZone, string>(nameof(BrowseText), "Browse");
	public static readonly StyledProperty<string?> ErrorTextProperty = AvaloniaProperty.Register<DropZone, string?>(nameof(ErrorText));
	public static readonly StyledProperty<bool> IsDragOverProperty = AvaloniaProperty.Register<DropZone, bool>(nameof(IsDragOver));
	public static readonly StyledProperty<ICommand?> BrowseCommandProperty = AvaloniaProperty.Register<DropZone, ICommand?>(nameof(BrowseCommand));
	public static readonly StyledProperty<object?> BrowseCommandParameterProperty = AvaloniaProperty.Register<DropZone, object?>(nameof(BrowseCommandParameter));
	public static readonly StyledProperty<ICommand?> DropCommandProperty = AvaloniaProperty.Register<DropZone, ICommand?>(nameof(DropCommand));

	public DropZone()
	{
		InitializeComponent();
		DragDrop.SetAllowDrop(this, true);
		DragDrop.AddDragEnterHandler(this, OnDragEnter);
		DragDrop.AddDragOverHandler(this, OnDragOver);
		DragDrop.AddDragLeaveHandler(this, OnDragLeave);
		DragDrop.AddDropHandler(this, OnDrop);
		UpdateDragState();
	}

	public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
	public string? Hint { get => GetValue(HintProperty); set => SetValue(HintProperty, value); }
	public string? AcceptedTypesText { get => GetValue(AcceptedTypesTextProperty); set => SetValue(AcceptedTypesTextProperty, value); }
	public object? IllustrationContent { get => GetValue(IllustrationContentProperty); set => SetValue(IllustrationContentProperty, value); }
	public string BrowseText { get => GetValue(BrowseTextProperty); set => SetValue(BrowseTextProperty, value); }
	public string? ErrorText { get => GetValue(ErrorTextProperty); set => SetValue(ErrorTextProperty, value); }
	public bool IsDragOver { get => GetValue(IsDragOverProperty); set => SetValue(IsDragOverProperty, value); }
	public ICommand? BrowseCommand { get => GetValue(BrowseCommandProperty); set => SetValue(BrowseCommandProperty, value); }
	public object? BrowseCommandParameter { get => GetValue(BrowseCommandParameterProperty); set => SetValue(BrowseCommandParameterProperty, value); }
	public ICommand? DropCommand { get => GetValue(DropCommandProperty); set => SetValue(DropCommandProperty, value); }

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);
		if (change.Property == IsDragOverProperty)
			UpdateDragState();
	}

	private void UpdateDragState() => PseudoClasses.Set(":drag-over", IsDragOver);

	private void OnDragEnter(object? sender, DragEventArgs e) => UpdateDragFeedback(e);

	private void OnDragOver(object? sender, DragEventArgs e) => UpdateDragFeedback(e);

	private void OnDragLeave(object? sender, DragEventArgs e)
	{
		IsDragOver = false;
		e.Handled = true;
	}

	private void OnDrop(object? sender, DragEventArgs e)
	{
		var paths = GetLocalPaths(e);
		IsDragOver = false;
		e.DragEffects = paths.Count > 0 ? DragDropEffects.Copy : DragDropEffects.None;
		e.Handled = true;

		if (paths.Count == 0)
		{
			ErrorText = "Libation could not read a local file or folder path from that drop. Use Browse instead.";
			return;
		}

		if (DropCommand?.CanExecute(paths) == true)
		{
			ErrorText = null;
			DropCommand.Execute(paths);
		}
		else
		{
			ErrorText = "Dropped files are not available for this action. Use Browse instead.";
		}
	}

	private void UpdateDragFeedback(DragEventArgs e)
	{
		bool canAccept = GetLocalPaths(e).Count > 0;
		IsDragOver = canAccept;
		e.DragEffects = canAccept ? DragDropEffects.Copy : DragDropEffects.None;
		e.Handled = true;
	}

	private static IReadOnlyList<string> GetLocalPaths(DragEventArgs e)
		=> (e.DataTransfer.TryGetFiles() ?? [])
			.Select(item => item.TryGetLocalPath())
			.Where(path => !string.IsNullOrWhiteSpace(path))
			.Select(path => path!)
			.Distinct(StringComparer.Ordinal)
			.ToArray();
}
