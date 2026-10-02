using System;
using System.Collections.Generic;
using HalawaHub.App.ViewModels;

namespace HalawaHub.App.Services;

/// <summary>
/// خدمة الفلترة والفرز — منطق نقي بدون UI state.
/// ثوابت التنقل تُمرر من الـ VM لمنع أي انحراف بين التعريف والاستخدام.
/// </summary>
public interface IFilterService
{
    IEnumerable<GameCardViewModel> ApplyFilter(
        IEnumerable<GameCardViewModel> games,
        string searchQuery,
        string selectedNavItem,
        string sortMode,
        string navAll,
        string navFavorite,
        string navInstalled,
        string navRecent);
}
