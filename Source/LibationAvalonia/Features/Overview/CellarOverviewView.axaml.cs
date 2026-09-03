using Avalonia;
using Avalonia.Controls;
using LibationAvalonia.Features.Library;
using LibationFileManager;
using System.Collections.Generic;

namespace LibationAvalonia.Features.Overview;

public partial class CellarOverviewView : OverviewViewBase
{
	public static readonly StyledProperty<LibraryViewModel?> LibraryProperty =
		AvaloniaProperty.Register<CellarOverviewView, LibraryViewModel?>(nameof(Library));

	public CellarOverviewView() => InitializeComponent();

	public LibraryViewModel? Library { get => GetValue(LibraryProperty); set => SetValue(LibraryProperty, value); }
	public IReadOnlyList<LibraryViewMode> ViewModes { get; } = [LibraryViewMode.Gallery, LibraryViewMode.Details];
	internal ContentControl FlightHost => FlightSurfaceHost;
}
