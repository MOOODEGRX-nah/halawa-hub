using System;
using System.Threading.Tasks;
using HalawaHub.Core;
using Tmds.DBus.Protocol;

namespace HalawaHub.App.Services;

/// <summary>
/// هل يوجد فعلًا من يعرض أيقونة الصينية؟ على لينكس لا يرمي TrayIcon في Avalonia أي
/// استثناء إذا لم يوجد مضيف StatusNotifier (GNOME بدون إضافة)، فكان زر الإغلاق يخفي
/// النافذة بلا أي طريق للرجوع إليها. نسأل D-Bus مباشرة عند كل إغلاق لأن لوحة سطح
/// المكتب قد تبدأ بعد البرنامج في جلسة التشغيل التلقائي.
/// </summary>
internal static class LinuxTrayProbe
{
    private const string WatcherName = "org.kde.StatusNotifierWatcher";
    private const string WatcherPath = "/StatusNotifierWatcher";

    public static bool IsHostAvailable(TimeSpan timeout)
    {
        try
        {
            var probe = Task.Run(ProbeAsync);
            return probe.Wait(timeout) && probe.Result;
        }
        catch (Exception ex)
        {
            Log.Error("tray probe failed, treating the tray as unavailable", ex);
            return false;
        }
    }

    private static async Task<bool> ProbeAsync()
    {
        var address = Address.Session;
        if (string.IsNullOrEmpty(address)) return false;

        using var connection = new Connection(address);
        await connection.ConnectAsync();

        if (!await NameHasOwnerAsync(connection, WatcherName)) return false;

        return await IsHostRegisteredAsync(connection);
    }

    private static Task<bool> NameHasOwnerAsync(Connection connection, string name)
    {
        MessageBuffer message;
        using (var writer = connection.GetMessageWriter())
        {
            writer.WriteMethodCallHeader(
                destination: "org.freedesktop.DBus",
                path: "/org/freedesktop/DBus",
                @interface: "org.freedesktop.DBus",
                member: "NameHasOwner",
                signature: "s");
            writer.WriteString(name);
            message = writer.CreateMessage();
        }

        return connection.CallMethodAsync(message, static (Message reply, object? _) => reply.GetBodyReader().ReadBool());
    }

    private static Task<bool> IsHostRegisteredAsync(Connection connection)
    {
        MessageBuffer message;
        using (var writer = connection.GetMessageWriter())
        {
            writer.WriteMethodCallHeader(
                destination: WatcherName,
                path: WatcherPath,
                @interface: "org.freedesktop.DBus.Properties",
                member: "Get",
                signature: "ss");
            writer.WriteString(WatcherName);
            writer.WriteString("IsStatusNotifierHostRegistered");
            message = writer.CreateMessage();
        }

        return connection.CallMethodAsync(message, static (Message reply, object? _) =>
        {
            var reader = reply.GetBodyReader();
            reader.ReadSignature("b");
            return reader.ReadBool();
        });
    }
}
