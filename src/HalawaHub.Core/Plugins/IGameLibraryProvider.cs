using HalawaHub.Core.Models;

namespace HalawaHub.Core.Plugins;

/// <summary>
/// أي مصدر مكتبة ألعاب (Steam, GOG, Epic, Xbox...) يطبّق هذا الـ Interface.
/// يسمح بإضافة منصات جديدة كـ Plugin بدون تعديل النواة.
/// </summary>
public interface IGameLibraryProvider
{
    /// اسم المنصة كما يظهر في الواجهة
    string PlatformName { get; }

    /// هل المنصة مثبتة/متوفرة على هذا الجهاز أصلاً؟
    bool IsAvailable();

    /// يفحص الجهاز ويرجع كل الألعاب الموجودة لهذه المنصة
    IEnumerable<GameInfo> ScanLibrary();
}
