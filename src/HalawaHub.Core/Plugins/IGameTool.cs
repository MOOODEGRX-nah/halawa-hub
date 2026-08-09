using HalawaHub.Core.Models;

namespace HalawaHub.Core.Plugins;

/// <summary>
/// أداة تُطبَّق على لعبة معينة (مثال مستقبلي: حقن ملفات OptiScaler، تعديل إعدادات، إلخ).
/// أي أداة مستقبلية تبنيها تلتزم بهذا الـ Interface فقط، والنواة تكتشفها تلقائيًا
/// من مجلد Plugins بدون أي تعديل على باقي البرنامج.
/// </summary>
public interface IGameTool
{
    /// اسم الأداة كما يظهر في الواجهة
    string Name { get; }

    /// وصف مختصر لما تفعله الأداة
    string Description { get; }

    /// هل هذه الأداة قابلة للتطبيق على هذه اللعبة تحديدًا؟
    /// (مثال: أداة رفع الدقة تُطبَّق فقط على ألعاب DirectX 11/12)
    bool AppliesTo(GameInfo game);

    /// هل الأداة مُفعّلة حاليًا على هذه اللعبة؟
    bool IsApplied(GameInfo game);

    /// تطبيق الأداة (نسخ ملفات، تعديل إعدادات...)
    void Apply(GameInfo game);

    /// التراجع/الإزالة (يرجع اللعبة لحالتها الأصلية)
    void Remove(GameInfo game);
}
