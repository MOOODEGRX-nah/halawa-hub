using System.IO;

namespace HalawaHub.Core;

/// <summary>
/// سجل ملفات بسيط: يكتب سطرًا لكل حدث في
/// %LocalAppData%\HalawaHub\logs\app-YYYY-MM-DD.log
/// ويحتفظ فقط بملفات آخر 7 أيام. أي فشل بالكتابة يُبتلع بصمت.
/// </summary>
public static class Log
{
    private static readonly object Lock = new();
    private static string? _currentDay;
    private static string? _currentPath;

    public static string LogDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HalawaHub", "logs");

    public static void Info(string message) => Write("INFO", message);

    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex == null
            ? message
            : $"{message} :: {ex.GetType().Name}: {ex.Message}");

    private static void Write(string level, string message)
    {
        try
        {
            lock (Lock)
            {
                var day = DateTime.Now.ToString("yyyy-MM-dd");
                if (day != _currentDay)
                {
                    _currentDay = day;
                    Directory.CreateDirectory(LogDir);
                    _currentPath = Path.Combine(LogDir, $"app-{day}.log");
                    CleanupOldLogs();
                }

                var line = $"[{DateTime.Now:HH:mm:ss}] [{level}] {message}";
                File.AppendAllText(_currentPath!, line + Environment.NewLine);
            }
        }
        catch
        {
            // التسجيل أداة تشخيص — ما يصير أبدًا سبب كراش
        }
    }

    private static void CleanupOldLogs()
    {
        try
        {
            foreach (var file in Directory.GetFiles(LogDir, "app-*.log"))
            {
                if (DateTime.Now - File.GetLastWriteTime(file) > TimeSpan.FromDays(7))
                    File.Delete(file);
            }
        }
        catch
        {
            // تجاهل
        }
    }
}
