using System.IO;
using HalawaHub.Core.Models;
using HalawaHub.Core.Plugins;
using Microsoft.Data.Sqlite;

namespace HalawaHub.Core.Library;

/// <summary>
/// ألعاب Lutris على لينكس من قاعدة بياناته pga.db (للقراءة فقط).
/// المسار والجدول من مصدر Lutris 0.5.22 (settings.py وdatabase/schema.py):
///   عادي:    $XDG_DATA_HOME/lutris/pga.db  (الافتراضي ~/.local/share/lutris/pga.db)
///   Flatpak: ~/.var/app/net.lutris.Lutris/data/lutris/pga.db
/// التشغيل عبر lutris:rungameid/&lt;id&gt; وهو البروتوكول الموثق في gui/application.py،
/// ويفتحه النظام عبر xdg-open للتثبيتين معًا.
/// </summary>
public class LutrisLibraryProvider : IGameLibraryProvider
{
    public const string Platform = "Lutris";

    private readonly IReadOnlyList<string>? _databases;

    public LutrisLibraryProvider()
    {
    }

    /// للاختبارات: قواعد بيانات مزيفة على القرص
    internal LutrisLibraryProvider(IEnumerable<string> databases)
    {
        _databases = databases.ToList();
    }

    public string PlatformName => Platform;

    public bool IsAvailable() =>
        (_databases != null || OperatingSystem.IsLinux()) && Databases().Any(File.Exists);

    public IEnumerable<GameInfo> ScanLibrary()
    {
        var games = new List<GameInfo>();
        foreach (var database in Databases().Where(File.Exists))
        {
            try
            {
                games.AddRange(ReadDatabase(database));
            }
            catch (Exception ex)
            {
                // قاعدة تالفة أو بجدول مختلف: نكمل بالبقية بدل كسر الفحص
                Log.Error($"Lutris: could not read {database}", ex);
            }
        }
        return games;
    }

    private IReadOnlyList<string> Databases() => _databases ?? DefaultDatabases(
        Environment.GetEnvironmentVariable("XDG_DATA_HOME"),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    internal static IReadOnlyList<string> DefaultDatabases(string? xdgDataHome, string home) => new[]
    {
        Path.Combine(string.IsNullOrWhiteSpace(xdgDataHome) ? Path.Combine(home, ".local", "share") : xdgDataHome, "lutris", "pga.db"),
        Path.Combine(home, ".var", "app", "net.lutris.Lutris", "data", "lutris", "pga.db")
    };

    private static List<GameInfo> ReadDatabase(string path)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString();

        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, directory, runner FROM games WHERE installed = 1";

        var games = new List<GameInfo>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var id = reader.IsDBNull(0) ? null : reader.GetValue(0).ToString();
            var name = reader.IsDBNull(1) ? null : reader.GetString(1);
            var directory = reader.IsDBNull(2) ? "" : reader.GetString(2);
            var runner = reader.IsDBNull(3) ? "" : reader.GetString(3);

            if (string.IsNullOrEmpty(id) || string.IsNullOrWhiteSpace(name)) continue;

            // بلا مشغّل لا يمكن تشغيلها، وألعاب مشغّل Steam تظهر أصلًا من مزود Steam
            if (string.IsNullOrEmpty(runner) || runner.Equals("steam", StringComparison.OrdinalIgnoreCase)) continue;

            games.Add(new GameInfo
            {
                Id = id,
                Name = name,
                InstallPath = directory,
                ExecutablePath = $"lutris:rungameid/{id}",
                Platform = Platform,
                // بعض الألعاب بلا مجلد (ملف تنفيذي فقط)، وLutris يعدّها مثبتة
                IsInstalled = directory.Length == 0 || Directory.Exists(directory)
            });
        }
        return games;
    }
}
