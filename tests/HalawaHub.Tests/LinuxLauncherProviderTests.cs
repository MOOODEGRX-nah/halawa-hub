using FluentAssertions;
using HalawaHub.Core.Library;
using Microsoft.Data.Sqlite;
using Xunit;

namespace HalawaHub.Tests;

/// <summary>
/// ملفات Heroic وقاعدة Lutris مزيفة على القرص، بنفس الصيغ الموجودة في مصدر كل منهما
/// (Heroic v2.22.4 وLutris 0.5.22). تعمل على كل الأنظمة لأن القراءة لا تعتمد على النظام.
/// </summary>
public sealed class HeroicLibraryProviderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "HeroicTest-" + Guid.NewGuid().ToString("N"));
    private readonly string _gameDir;

    public HeroicLibraryProviderTests()
    {
        _gameDir = Path.Combine(_root, "games", "Rocket League");
        Directory.CreateDirectory(_gameDir);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void Write(string relative, string json)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
    }

    private static string Json(string text) => text.Replace("\\", "\\\\");

    private List<HalawaHub.Core.Models.GameInfo> Scan() =>
        new HeroicLibraryProvider(new[] { _root }).ScanLibrary().ToList();

    [Fact]
    public void Epic_IsReadFromLegendaryInstalledJson()
    {
        Write("legendaryConfig/legendary/installed.json", $$"""
            {
              "Sugar": { "app_name": "Sugar", "title": "Rocket League®", "install_path": "{{Json(_gameDir)}}", "is_dlc": false, "platform": "Windows", "executable": "Binaries/Win64/Launcher.exe" },
              "SomeDlc": { "app_name": "SomeDlc", "title": "A DLC", "install_path": "{{Json(_gameDir)}}", "is_dlc": true }
            }
            """);

        var game = Scan().Should().ContainSingle("DLC entries are not games").Subject;

        game.Id.Should().Be("legendary:Sugar");
        game.Name.Should().Be("Rocket League®");
        game.Platform.Should().Be("Heroic");
        game.InstallPath.Should().Be(_gameDir);
        game.ExecutablePath.Should().Be("heroic://launch/legendary/Sugar");
        game.IsInstalled.Should().BeTrue();
    }

    [Fact]
    public void Gog_TitleComesFromTheLibraryCache()
    {
        Write("gog_store/installed.json", $$"""
            { "installed": [ { "appName": "1207658924", "install_path": "{{Json(_gameDir)}}", "platform": "windows", "is_dlc": false } ] }
            """);
        Write("store_cache/gog_library.json", """
            { "games": [ { "app_name": "1207658924", "title": "Unreal Gold", "runner": "gog" } ], "__timestamp": {} }
            """);

        var game = Scan().Should().ContainSingle().Subject;

        game.Id.Should().Be("gog:1207658924");
        game.Name.Should().Be("Unreal Gold");
        game.ExecutablePath.Should().Be("heroic://launch/gog/1207658924");
    }

    [Fact]
    public void Gog_WithoutTheLibraryCache_FallsBackToTheFolderName()
    {
        Write("gog_store/installed.json", $$"""
            { "installed": [ { "appName": "1", "install_path": "{{Json(_gameDir)}}" } ] }
            """);

        Scan().Should().ContainSingle().Which.Name.Should().Be("Rocket League");
    }

    [Fact]
    public void Amazon_IsReadFromNileFiles()
    {
        Write("nile_config/nile/installed.json", $$"""
            [ { "id": "amzn1.adg.product.abc", "version": "1", "path": "{{Json(_gameDir)}}", "size": 100 } ]
            """);
        Write("nile_config/nile/library.json", """
            [ { "id": "x", "product": { "id": "amzn1.adg.product.abc", "title": "Amazon Game" } } ]
            """);

        var game = Scan().Should().ContainSingle().Subject;

        game.Id.Should().Be("nile:amzn1.adg.product.abc");
        game.Name.Should().Be("Amazon Game");
        game.ExecutablePath.Should().Be("heroic://launch/nile/amzn1.adg.product.abc");
    }

    [Fact]
    public void MissingInstallFolder_IsListedAsNotInstalled()
    {
        // قرص الألعاب غير مركب مثلًا: اللعبة موجودة لكن لا يمكن تشغيلها الآن
        Write("legendaryConfig/legendary/installed.json", """
            { "A": { "title": "Gone", "install_path": "/nonexistent/halawa/gone" } }
            """);

        Scan().Should().ContainSingle().Which.IsInstalled.Should().BeFalse();
    }

    [Fact]
    public void CorruptFile_DoesNotHideTheOtherStores()
    {
        Write("legendaryConfig/legendary/installed.json", "{ not json");
        Write("gog_store/installed.json", $$"""
            { "installed": [ { "appName": "7", "install_path": "{{Json(_gameDir)}}" } ] }
            """);

        Scan().Should().ContainSingle().Which.Id.Should().Be("gog:7");
    }

    [Fact]
    public void NothingInstalled_ReturnsEmpty_AndUnexpectedShapesAreIgnored()
    {
        Scan().Should().BeEmpty();

        Write("legendaryConfig/legendary/installed.json", "[]");
        Write("gog_store/installed.json", "{}");
        Write("nile_config/nile/installed.json", "{}");

        Scan().Should().BeEmpty();
    }

    [Fact]
    public void Roots_CoverNativeAndFlatpak_AndRespectXdgConfigHome()
    {
        HeroicLibraryProvider.DefaultConfigRoots(null, "/home/u").Should().Equal(
            Path.Combine("/home/u", ".config", "heroic"),
            Path.Combine("/home/u", ".var", "app", "com.heroicgameslauncher.hgl", "config", "heroic"));

        HeroicLibraryProvider.DefaultConfigRoots("/custom", "/home/u")[0].Should().Be(Path.Combine("/custom", "heroic"));
    }

    [Fact]
    public void Available_OnlyWhenAConfigFolderExists()
    {
        new HeroicLibraryProvider(new[] { _root }).IsAvailable().Should().BeTrue();
        new HeroicLibraryProvider(new[] { Path.Combine(_root, "absent") }).IsAvailable().Should().BeFalse();
    }
}

public sealed class LutrisLibraryProviderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "LutrisTest-" + Guid.NewGuid().ToString("N"));
    private readonly string _database;
    private readonly string _gameDir;

    public LutrisLibraryProviderTests()
    {
        _database = Path.Combine(_root, "pga.db");
        _gameDir = Path.Combine(_root, "game");
        Directory.CreateDirectory(_gameDir);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    /// نفس أعمدة جدول games في lutris/database/schema.py
    private void CreateDatabase(params (long Id, string? Name, string? Directory, string? Runner, long Installed)[] games)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _database, Pooling = false }.ToString());
        connection.Open();

        using (var create = connection.CreateCommand())
        {
            create.CommandText = """
                CREATE TABLE games (id INTEGER PRIMARY KEY, name TEXT, sortname TEXT, slug TEXT, installer_slug TEXT,
                  parent_slug TEXT, platform TEXT, runner TEXT, executable TEXT, directory TEXT, updated DATETIME,
                  lastplayed INTEGER, installed INTEGER, installed_at INTEGER, year INTEGER, configpath TEXT,
                  has_custom_banner INTEGER, has_custom_icon INTEGER, has_custom_coverart_big INTEGER, playtime REAL,
                  service TEXT, service_id TEXT, discord_id TEXT)
                """;
            create.ExecuteNonQuery();
        }

        foreach (var game in games)
        {
            using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO games (id, name, directory, runner, installed) VALUES ($id, $name, $directory, $runner, $installed)";
            insert.Parameters.AddWithValue("$id", game.Id);
            insert.Parameters.AddWithValue("$name", (object?)game.Name ?? DBNull.Value);
            insert.Parameters.AddWithValue("$directory", (object?)game.Directory ?? DBNull.Value);
            insert.Parameters.AddWithValue("$runner", (object?)game.Runner ?? DBNull.Value);
            insert.Parameters.AddWithValue("$installed", game.Installed);
            insert.ExecuteNonQuery();
        }
    }

    private List<HalawaHub.Core.Models.GameInfo> Scan() =>
        new LutrisLibraryProvider(new[] { _database }).ScanLibrary().ToList();

    [Fact]
    public void InstalledGame_IsListed_WithTheRunGameIdProtocol()
    {
        CreateDatabase((12, "Celeste", _gameDir, "linux", 1));

        var game = Scan().Should().ContainSingle().Subject;

        game.Id.Should().Be("12");
        game.Name.Should().Be("Celeste");
        game.Platform.Should().Be("Lutris");
        game.InstallPath.Should().Be(_gameDir);
        game.ExecutablePath.Should().Be("lutris:rungameid/12");
        game.IsInstalled.Should().BeTrue();
    }

    [Fact]
    public void NotInstalled_NoRunner_AndSteamRunner_AreSkipped()
    {
        CreateDatabase(
            (1, "Library Only", null, "linux", 0),
            (2, "No Runner", _gameDir, null, 1),
            (3, "Duplicate Of Steam", _gameDir, "steam", 1),
            (4, "Wine Game", _gameDir, "wine", 1));

        Scan().Should().ContainSingle().Which.Name.Should().Be("Wine Game");
    }

    [Fact]
    public void GameWithoutFolder_IsStillInstalled_ButMissingFolderIsNot()
    {
        CreateDatabase(
            (1, "No Folder", null, "linux", 1),
            (2, "Missing Folder", "/nonexistent/halawa/gone", "linux", 1));

        var games = Scan();

        games.Single(g => g.Id == "1").IsInstalled.Should().BeTrue();
        games.Single(g => g.Id == "2").IsInstalled.Should().BeFalse();
    }

    [Fact]
    public void CorruptDatabase_ReturnsEmpty_InsteadOfThrowing()
    {
        File.WriteAllText(_database, "this is not a database");

        Scan().Should().BeEmpty();
    }

    [Fact]
    public void Scan_DoesNotModifyTheDatabase()
    {
        CreateDatabase((1, "A", _gameDir, "linux", 1));
        var before = File.ReadAllBytes(_database);

        Scan();

        File.ReadAllBytes(_database).Should().Equal(before, "Lutris owns this file; we only read it");
    }

    [Fact]
    public void Databases_CoverNativeAndFlatpak_AndRespectXdgDataHome()
    {
        LutrisLibraryProvider.DefaultDatabases(null, "/home/u").Should().Equal(
            Path.Combine("/home/u", ".local", "share", "lutris", "pga.db"),
            Path.Combine("/home/u", ".var", "app", "net.lutris.Lutris", "data", "lutris", "pga.db"));

        LutrisLibraryProvider.DefaultDatabases("/custom", "/home/u")[0].Should().Be(Path.Combine("/custom", "lutris", "pga.db"));
    }

    [Fact]
    public void Available_OnlyWhenADatabaseExists()
    {
        new LutrisLibraryProvider(new[] { _database }).IsAvailable().Should().BeFalse();

        CreateDatabase();

        new LutrisLibraryProvider(new[] { _database }).IsAvailable().Should().BeTrue();
    }
}

public class WindowsOnlyProviderGuardTests
{
    [Fact]
    public void WindowsProviders_ReportUnavailable_OutsideWindows()
    {
        if (OperatingSystem.IsWindows()) return;

        new GogLibraryProvider().IsAvailable().Should().BeFalse();
        new GogLibraryProvider().ScanLibrary().Should().BeEmpty();
        new EpicLibraryProvider().IsAvailable().Should().BeFalse();
        new EpicLibraryProvider().ScanLibrary().Should().BeEmpty();
        new RiotLibraryProvider().IsAvailable().Should().BeFalse();
        new RiotLibraryProvider().ScanLibrary().Should().BeEmpty();
        new XboxLibraryProvider().IsAvailable().Should().BeFalse();
    }

    [Fact]
    public void LinuxProviders_ReportUnavailable_OnWindows()
    {
        if (!OperatingSystem.IsWindows()) return;

        new HeroicLibraryProvider().IsAvailable().Should().BeFalse();
        new LutrisLibraryProvider().IsAvailable().Should().BeFalse();
    }
}
