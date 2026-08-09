using System.IO;
using System.Diagnostics;
using RiftClone.Core.Models;
using RiftClone.Core.Plugins;

namespace RiftClone.Plugins.Sample;

/// <summary>
/// مثال توضيحي بسيط وآمن على IGameTool: يفتح مجلد تثبيت اللعبة في المستكشف.
///
/// هذا بالضبط نفس النمط اللي بتتبعه أي أداة مستقبلية أقوى (مثل أداة تدير
/// ملفات رفع الدقة/Frame Generation لكل لعبة): تطبّق IGameTool، وتوضع
/// كـ DLL في مجلد Plugins، والنواة تكتشفها وتعرضها في الواجهة تلقائيًا
/// بدون أي تعديل على باقي البرنامج.
/// </summary>
public class OpenFolderTool : IGameTool
{
    public string Name => "فتح مجلد اللعبة";
    public string Description => "يفتح مجلد تثبيت اللعبة في مستكشف الملفات";

    public bool AppliesTo(GameInfo game) => game.IsInstalled;

    // أداة فورية (تنفذ فعل واحد)، لذا لا تحتفظ بحالة "مُفعّلة"
    public bool IsApplied(GameInfo game) => false;

    public void Apply(GameInfo game)
    {
        if (Directory.Exists(game.InstallPath))
            Process.Start("explorer.exe", game.InstallPath);
    }

    public void Remove(GameInfo game)
    {
        // لا يوجد شيء للتراجع عنه في هذه الأداة التوضيحية
    }
}
