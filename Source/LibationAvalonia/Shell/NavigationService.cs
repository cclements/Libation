using LibationAvalonia.DesignSystem;
using LibationAvalonia.ViewModels;
using LibationFileManager;
using Avalonia.Media;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace LibationAvalonia.Shell;

public interface INavigationService
{
	AppRoute CurrentRoute { get; }
	ReadOnlyObservableCollection<ShellNavigationItemViewModel> PrimaryItems { get; }
	ReadOnlyObservableCollection<ShellNavigationItemViewModel> UtilityItems { get; }
	event EventHandler<AppRouteChangedEventArgs>? RouteChanged;
	void Navigate(AppRouteId destination);
}

public sealed class AppRouteChangedEventArgs(AppRoute previous, AppRoute current) : EventArgs
{
	public AppRoute Previous { get; } = previous;
	public AppRoute Current { get; } = current;
}

public sealed class ShellNavigationItemViewModel(AppRoute route) : ViewModelBase
{
	private bool isSelected;

	public AppRoute Route { get; } = route;
	public AppRouteId Id => Route.Id;
	public string Label => Route.Label;
	public string GlyphResourceKey => Route.GlyphResourceKey;
	public Geometry? IconData
		=> App.Current.TryGetResource(GlyphResourceKey, App.Current.ActualThemeVariant, out var value)
			? value as Geometry
			: null;
	public string AccessibleDescription => Route.AccessibleDescription;
	public IBrush SelectionBackground
		=> IsSelected
			&& App.Current.TryGetResource("Libation.Brush.AccentPrimary", App.Current.ActualThemeVariant, out var value)
			&& value is IBrush brush
				? brush
				: Brushes.Transparent;
	public bool IsSelected
	{
		get => isSelected;
		internal set
		{
			this.RaiseAndSetIfChanged(ref isSelected, value);
			this.RaisePropertyChanged(nameof(SelectionBackground));
		}
	}
}

/// <summary>
/// Owns only presentation navigation. Destination state is stable across profile
/// changes and persisted independently of the selected experience.
/// </summary>
public sealed class NavigationService : ViewModelBase, INavigationService
{
	private static readonly AppRouteId[] PrimaryOrder =
	[
		AppRouteId.Overview,
		AppRouteId.Library,
		AppRouteId.Downloads,
		AppRouteId.Processing,
		AppRouteId.History,
	];
	private static readonly AppRouteId[] UtilityOrder =
	[
		AppRouteId.Accounts,
		AppRouteId.Settings,
		AppRouteId.Tools,
		AppRouteId.Trash,
		AppRouteId.About,
	];

	private readonly Configuration configuration;
	private readonly Dictionary<AppRouteId, ShellNavigationItemViewModel> items;
	private AppRoute currentRoute;

	public NavigationService(Configuration configuration, ExperienceProfile initialProfile)
	{
		this.configuration = configuration;
		items = AppRoutes.All.Values
			.Select(route => new ShellNavigationItemViewModel(route))
			.ToDictionary(item => item.Id);

		PrimaryItems = new(new ObservableCollection<ShellNavigationItemViewModel>(
			PrimaryOrder.Select(id => items[id])));
		UtilityItems = new(new ObservableCollection<ShellNavigationItemViewModel>(
			UtilityOrder.Select(id => items[id])));

		var initial = ResolveInitialRoute(configuration.ContemporaryLastRoute, initialProfile);
		currentRoute = AppRoutes.Get(initial);
		items[initial].IsSelected = true;
	}

	public AppRoute CurrentRoute
	{
		get => currentRoute;
		private set => this.RaiseAndSetIfChanged(ref currentRoute, value);
	}

	public ReadOnlyObservableCollection<ShellNavigationItemViewModel> PrimaryItems { get; }
	public ReadOnlyObservableCollection<ShellNavigationItemViewModel> UtilityItems { get; }
	public event EventHandler<AppRouteChangedEventArgs>? RouteChanged;

	public void Navigate(AppRouteId destination)
	{
		if (!items.TryGetValue(destination, out var next) || next.Route == CurrentRoute)
			return;

		var previous = CurrentRoute;
		items[previous.Id].IsSelected = false;
		next.IsSelected = true;
		CurrentRoute = next.Route;
		configuration.ContemporaryLastRoute = destination.ToString();
		RouteChanged?.Invoke(this, new(previous, next.Route));
	}

	private static AppRouteId ResolveInitialRoute(string persisted, ExperienceProfile profile)
	{
		if (Enum.TryParse<AppRouteId>(persisted, ignoreCase: false, out var route)
			&& AppRoutes.All.ContainsKey(route))
			return route;

		return profile.DashboardLayout == DashboardLayoutKind.Cellar
			? AppRouteId.Library
			: AppRouteId.Overview;
	}
}
