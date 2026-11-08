using System;
using System.IO;
using System.Linq;
using System.Text;

namespace HalawaHub.App.Services;

/// <summary>
/// ملفات .desktop على لينكس (Autostart وقائمة التطبيقات) حسب
/// Desktop Entry Specification، خصوصًا قواعد الاقتباس في مفتاح Exec.
/// </summary>
internal static class LinuxDesktopEntry
{
    public const string IconName = "halawa-hub";
    public const string FileName = "halawa-hub.desktop";

    /// اسم نافذة X11 في Avalonia هو اسم الملف التنفيذي، وبه يربط سطح المكتب النافذة بالأيقونة
    private const string WindowClass = "Halawa-Hub";

    public static string Build(string executablePath) => new StringBuilder()
        .Append("[Desktop Entry]\n")
        .Append("Type=Application\n")
        .Append("Name=Halawa-Hub\n")
        .Append("Comment=Game library launcher\n")
        .Append("Comment[ar]=مشغّل مكتبة الألعاب\n")
        .Append("Exec=").Append(QuoteExec(executablePath)).Append('\n')
        .Append("Icon=").Append(IconName).Append('\n')
        .Append("Terminal=false\n")
        .Append("Categories=Game;\n")
        .Append("StartupWMClass=").Append(WindowClass).Append('\n')
        .ToString();

    /// <summary>
    /// مفتاح Exec: داخل علامتي الاقتباس تُهرّب " و ` و $ و \ بشرطة مائلة، ثم تُكتب
    /// الشرطة المائلة مضاعفة لأن قيم النص نفسها تمر بتهريب منفصل. و% تُكتب %%.
    /// </summary>
    internal static string QuoteExec(string path)
    {
        var quoted = new StringBuilder("\"");
        foreach (var c in path)
        {
            switch (c)
            {
                case '"':
                case '`':
                case '$':
                    quoted.Append("\\\\").Append(c);
                    break;
                case '\\':
                    quoted.Append("\\\\\\\\");
                    break;
                case '%':
                    quoted.Append("%%");
                    break;
                default:
                    quoted.Append(c);
                    break;
            }
        }
        return quoted.Append('"').ToString();
    }

    /// <summary>
    /// عكس QuoteExec: المسار المكتوب في مفتاح Exec، أو null إذا لم يوجد.
    /// قيمة بلا اقتباس (ملف عدّله المستخدم) تُؤخذ حتى أول مسافة.
    /// </summary>
    internal static string? ReadExecPath(string entry)
    {
        var line = entry.Split('\n').FirstOrDefault(l => l.StartsWith("Exec=", StringComparison.Ordinal));
        if (line == null) return null;

        var value = line["Exec=".Length..].Trim();
        if (value.Length < 2 || value[0] != '"' || value[^1] != '"')
            return value.Split(' ', 2)[0] is { Length: > 0 } plain ? plain : null;

        return value[1..^1]
            .Replace("\\\\\\\\", "\u0000")
            .Replace("\\\\", "")
            .Replace("\u0000", "\\")
            .Replace("%%", "%");
    }

    /// كتابة ذرية: لا يبقى سطح المكتب أمام ملف نصفه مكتوب
    public static void WriteAtomically(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(temporary, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(temporary, path, overwrite: true);
    }

    public static string ConfigHome(string? xdgConfigHome, string home) =>
        string.IsNullOrWhiteSpace(xdgConfigHome) ? Path.Combine(home, ".config") : xdgConfigHome;

    public static string DataHome(string? xdgDataHome, string home) =>
        string.IsNullOrWhiteSpace(xdgDataHome) ? Path.Combine(home, ".local", "share") : xdgDataHome;

    public static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
}
