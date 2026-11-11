using System;
using System.Threading.Tasks;
using HalawaHub.Core;

namespace HalawaHub.App.Services;

/// <summary>
/// معالجات النقر async void: أي استثناء فيها يصل لخيط الواجهة ويُسقط البرنامج
/// (الحافظة المشغولة على ويندوز ترمي COMException مثلًا). هنا يُسجَّل الخطأ ويكمل البرنامج.
/// </summary>
public static class SafeUi
{
    public static async void Run(Func<Task> action, string what)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Log.Error($"UI action failed: {what}", ex);
        }
    }
}
