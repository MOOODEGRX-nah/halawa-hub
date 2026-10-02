using HalawaHub.App.ViewModels;

namespace HalawaHub.App.Services;

/// <summary>
/// خدمة الفلترة والفرز — منطق نقي بدون UI state.
/// </summary>
public interface IFilterService
{
    /// <summary>
    /// يفلتر ويرتب الألعاب حسب المعطيات، ويرجع IEnumerable جديد.
    /// </summary>
    IEnumerable<GameCardViewModel> ApplyFilter(
        IEnumerable<GameCardViewModel> games,
        string searchQuery,
        string selectedNavItem,
        string sortMode);
}
