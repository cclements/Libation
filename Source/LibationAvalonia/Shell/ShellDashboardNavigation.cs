using DataLayer;
using LibationAvalonia.Features.Library;
using LibationAvalonia.Features.Overview;
using System;
using System.Threading.Tasks;

namespace LibationAvalonia.Shell;

/// <summary>Routes dashboard actions without moving route ownership into the feature.</summary>
public sealed class ShellDashboardNavigation(
	NavigationService navigation,
	LibraryViewModel library) : IDashboardNavigation
{
	public Task OpenBookAsync(LibraryBook book)
	{
		if (!library.TryOpenBook(book))
			return Task.FromException(new InvalidOperationException(
				"The selected title no longer has a valid library identity."));
		navigation.Navigate(AppRouteId.Library);
		return Task.CompletedTask;
	}

	public Task OpenLibraryAsync()
	{
		navigation.Navigate(AppRouteId.Library);
		return Task.CompletedTask;
	}

	public Task OpenProcessingAsync()
	{
		navigation.Navigate(AppRouteId.Processing);
		return Task.CompletedTask;
	}
}
