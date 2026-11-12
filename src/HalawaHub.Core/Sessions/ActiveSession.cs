using System.Text.Json;
using HalawaHub.Core.Library;

namespace HalawaHub.Core.Sessions;

/// <summary>
/// جلسة اللعب الجارية كما تُحفظ في active-session.json، حتى لا يضيع وقت اللعب
/// إن أُغلق Halawa (أو انطفأ الجهاز) واللعبة تعمل.
/// </summary>
public sealed class ActiveSessionRecord
{
    public string Platform { get; set; } = "";
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";

    /// "steam" | "folder" | "process"
    public string Kind { get; set; } = "";
    public string InstallDir { get; set; } = "";
    public List<int> Pids { get; set; } = new();
    public DateTime StartedUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }

    public string? SteamLogPath { get; set; }
    public long SteamLogOffset { get; set; }
}

public static class ActiveSessionStore
{
    public static string DefaultPath => AppPaths.InData("active-session.json");

    public static ActiveSessionRecord? Load(string path) =>
        AtomicFile.ReadWithBackup(path, json => JsonSerializer.Deserialize<ActiveSessionRecord>(json), "active-session.json");

    public static void Save(string path, ActiveSessionRecord record)
    {
        try
        {
            AtomicFile.WriteAllText(path, JsonSerializer.Serialize(record));
        }
        catch (Exception ex)
        {
            Log.Error("active-session.json: save failed", ex);
        }
    }

    public static void Delete(string path)
    {
        foreach (var p in new[] { path, AtomicFile.BackupPathFor(path) })
        {
            try { if (File.Exists(p)) File.Delete(p); }
            catch (Exception ex) { Log.Error($"could not delete {p}", ex); }
        }
    }

    public static ActiveSessionRecord? FromTracker(IPlaySessionTracker tracker, string platform, string id, string name, string installDir)
    {
        if (tracker.StartedAtUtc is not { } started) return null;
        var record = new ActiveSessionRecord
        {
            Platform = platform,
            Id = id,
            Name = name,
            Kind = tracker.Kind,
            InstallDir = installDir,
            Pids = tracker.Pids.ToList(),
            StartedUtc = started,
            LastSeenUtc = tracker.LastSeenUtc ?? started
        };
        if (tracker is SteamSessionTracker steam)
        {
            record.SteamLogPath = steam.LogPath;
            record.SteamLogOffset = steam.LogOffset;
        }
        return record;
    }
}

/// نتيجة الاستعادة عند الإقلاع: إما نكمل التتبع، أو نُغلق الجلسة بمدة معروفة
public sealed record SessionRecovery(IPlaySessionTracker? Resumed, TimeSpan ClosedDuration);

public static class ActiveSessionRecovery
{
    /// لا نصدق جلسة "تعمل" منذ أكثر من هذا (ساعة الجهاز تغيّرت، أو ملف قديم جدًا)
    public static readonly TimeSpan MaxPlausibleSession = TimeSpan.FromHours(24);

    /// <summary>
    /// إن كانت اللعبة ما زالت تعمل نكمل تتبعها من وقت بدايتها الأصلي، وإلا تُغلق
    /// الجلسة عند آخر لحظة رأيناها فيها. رقم العملية قد يُعاد استخدامه بعد إعادة
    /// تشغيل الجهاز، فعملية بدأت بعد بداية الجلسة ليست عمليتنا.
    /// </summary>
    public static SessionRecovery Recover(ActiveSessionRecord record, IProcessScanner? scanner = null, Func<DateTime>? utcNow = null)
    {
        scanner ??= SystemProcessScanner.Instance;
        var now = (utcNow ?? (() => DateTime.UtcNow))();

        var closed = record.LastSeenUtc - record.StartedUtc;
        if (closed < TimeSpan.Zero || closed > MaxPlausibleSession) closed = TimeSpan.Zero;

        if (now - record.LastSeenUtc > MaxPlausibleSession)
            return new SessionRecovery(null, closed);

        bool PidIsOurs(int pid)
        {
            if (!scanner.IsAlive(pid)) return false;
            var started = scanner.StartTimeUtc(pid);
            return started == null || started <= record.StartedUtc.AddMinutes(5);
        }

        switch (record.Kind)
        {
            case "folder" when FolderSessionTracker.IsRunningIn(record.InstallDir, scanner):
                return new SessionRecovery(
                    FolderSessionTracker.Resume(record.InstallDir, record.StartedUtc, now, scanner, utcNow), TimeSpan.Zero);

            case "process" when record.Pids.Count == 1 && PidIsOurs(record.Pids[0]):
                return new SessionRecovery(
                    new ProcessSessionTracker(record.Pids[0], scanner, utcNow, record.StartedUtc), TimeSpan.Zero);

            case "steam" when !string.IsNullOrEmpty(record.SteamLogPath) && File.Exists(record.SteamLogPath):
            {
                var steam = SteamSessionTracker.Resume(record.SteamLogPath!, record.Id, record.StartedUtc, record.Pids,
                    record.SteamLogOffset, record.InstallDir, PidIsOurs);

                // سطر النهاية قد يكون كُتب وHalawa مغلق — يعطينا المدة الدقيقة
                if (steam.Poll() == SteamSessionState.Ended)
                    return new SessionRecovery(null, steam.Elapsed());
                if (steam.AnyAlive())
                    return new SessionRecovery(steam, TimeSpan.Zero);
                return new SessionRecovery(null, closed);
            }
        }

        return new SessionRecovery(null, closed);
    }
}
