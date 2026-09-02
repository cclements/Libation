using Avalonia;
using Avalonia.Controls;
using LibationAvalonia.Features.Library;

namespace LibationAvalonia.Features.Overview;

public partial class CellarOverviewView : OverviewViewBase
{
	public static readonly StyledProperty<LibraryViewModel?> LibraryProperty =
		AvaloniaProperty.Register<CellarOverviewView, LibraryViewModel?>(nameof(Library));

	public CellarOverviewView() => InitializeComponent();

	public LibraryViewModel? Library { get => GetValue(LibraryProperty); set => SetValue(LibraryProperty, value); }
	internal ContentControl FlightHost => FlightSurfaceHost;
}
