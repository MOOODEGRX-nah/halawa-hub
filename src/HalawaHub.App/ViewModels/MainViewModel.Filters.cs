using HalawaHub.App.Services;

namespace HalawaHub.App.ViewModels;

public partial class MainViewModel
{
    /// <summary>
    /// يطبّق الفلتر عبر FilterService ويعبّي FilteredGames.
    /// </summary>
    private void ApplyFilter()
    {
        FilteredGames.Clear();
        var filtered = _filterService.ApplyFilter(Games, SearchQuery, SelectedNavItem, SortMode);
        foreach (var card in filtered)
            FilteredGames.Add(card);
    }
}
