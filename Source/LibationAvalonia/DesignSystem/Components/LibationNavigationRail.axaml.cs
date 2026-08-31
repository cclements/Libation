using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using System.Collections;

namespace LibationAvalonia.DesignSystem.Components;

public partial class LibationNavigationRail : UserControl
{
	public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
		AvaloniaProperty.Register<LibationNavigationRail, IEnumerable?>(nameof(ItemsSource));
	public static readonly StyledProperty<object?> SelectedItemProperty =
		AvaloniaProperty.Register<LibationNavigationRail, object?>(nameof(SelectedItem), defaultBindingMode: BindingMode.TwoWay);
	public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty =
		AvaloniaProperty.Register<LibationNavigationRail, IDataTemplate?>(nameof(ItemTemplate));
	public static readonly StyledProperty<bool> IsExpandedProperty =
		AvaloniaProperty.Register<LibationNavigationRail, bool>(nameof(IsExpanded), true);
	public static readonly StyledProperty<object?> BrandContentProperty =
		AvaloniaProperty.Register<LibationNavigationRail, object?>(nameof(BrandContent));
	public static readonly StyledProperty<object?> FooterContentProperty =
		AvaloniaProperty.Register<LibationNavigationRail, object?>(nameof(FooterContent));
	public static readonly StyledProperty<string> AccessibleNameProperty =
		AvaloniaProperty.Register<LibationNavigationRail, string>(nameof(AccessibleName), "Primary navigation");

	public LibationNavigationRail()
	{
		InitializeComponent();
		UpdateCompactState();
	}

	public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
	public object? SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }
	public IDataTemplate? ItemTemplate { get => GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }
	public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
	public object? BrandContent { get => GetValue(BrandContentProperty); set => SetValue(BrandContentProperty, value); }
	public object? FooterContent { get => GetValue(FooterContentProperty); set => SetValue(FooterContentProperty, value); }
	public string AccessibleName { get => GetValue(AccessibleNameProperty); set => SetValue(AccessibleNameProperty, value); }

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);
		if (change.Property == IsExpandedProperty)
			UpdateCompactState();
	}

	private void UpdateCompactState() => PseudoClasses.Set(":compact", !IsExpanded);
}
