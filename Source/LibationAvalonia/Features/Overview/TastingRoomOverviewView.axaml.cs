using Avalonia;
using Avalonia.Controls;
using LibationAvalonia.Features.Library;

namespace LibationAvalonia.Features.Overview;

public partial class TastingRoomOverviewView : OverviewViewBase
{
	public static readonly StyledProperty<LibraryViewModel?> LibraryProperty =
		AvaloniaProperty.Register<TastingRoomOverviewView, LibraryViewModel?>(nameof(Library));

	public TastingRoomOverviewView() => InitializeComponent();
	public LibraryViewModel? Library { get => GetValue(LibraryProperty); set => SetValue(LibraryProperty, value); }
	internal ContentControl DecanterHost => DecanterSurfaceHost;
}
