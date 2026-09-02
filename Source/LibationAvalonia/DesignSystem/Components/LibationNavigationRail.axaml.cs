using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using System.Collections;
using System.Linq;

namespace LibationAvalonia.DesignSystem.Components;

public partial class LibationNavigationRail : UserControl
{
	public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
		AvaloniaProperty.Register<LibationNavigationRail, IEnumerable?>(nameof(ItemsSource));
	public static readonly StyledProperty<IEnumerable?> UtilityItemsSourceProperty =
		AvaloniaProperty.Register<LibationNavigationRail, IEnumerable?>(nameof(UtilityItemsSource));
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
	public IEnumerable? UtilityItemsSource { get => GetValue(UtilityItemsSourceProperty); set => SetValue(UtilityItemsSourceProperty, value); }
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
		if (change.Property == SelectedItemProperty
			|| change.Property == ItemsSourceProperty
			|| change.Property == UtilityItemsSourceProperty)
			UpdateSelection();
	}

	private void UpdateCompactState() => PseudoClasses.Set(":compact", !IsExpanded);

	private bool synchronizingSelection;
	private void RouteList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (synchronizingSelection || sender is not ListBox list || list.SelectedItem is null)
			return;
		SetCurrentValue(SelectedItemProperty, list.SelectedItem);
		UpdateSelection();
	}

	private void UpdateSelection()
	{
		if (PrimaryRoutes is null || UtilityRoutes is null)
			return;
		try
		{
			synchronizingSelection = true;
			PrimaryRoutes.SelectedItem = Contains(ItemsSource, SelectedItem) ? SelectedItem : null;
			UtilityRoutes.SelectedItem = Contains(UtilityItemsSource, SelectedItem) ? SelectedItem : null;
		}
		finally
		{
			synchronizingSelection = false;
		}
	}

	private static bool Contains(IEnumerable? source, object? item)
		=> item is not null && source?.Cast<object>().Contains(item) == true;
}
