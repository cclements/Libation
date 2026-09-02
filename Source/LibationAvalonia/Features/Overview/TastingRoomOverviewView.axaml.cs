using Avalonia.Controls;

namespace LibationAvalonia.Features.Overview;

public partial class TastingRoomOverviewView : OverviewViewBase
{
	public TastingRoomOverviewView() => InitializeComponent();
	internal ContentControl DecanterHost => DecanterSurfaceHost;
}
