using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using System.Collections;

namespace LibationAvalonia.DesignSystem.Components;

public partial class ToastHost : UserControl
{
	public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty = AvaloniaProperty.Register<ToastHost, IEnumerable?>(nameof(ItemsSource));
	public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty = AvaloniaProperty.Register<ToastHost, IDataTemplate?>(nameof(ItemTemplate));

	public ToastHost() => InitializeComponent();

	public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
	public IDataTemplate? ItemTemplate { get => GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }
}
