using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using HalawaHub.Core.Sessions;

namespace HalawaHub.Core.Library;

public enum SteamLogEventKind
{
    ProcessAdded,
    ProcessRemoved,
    AppStopped
}

public sealed record SteamLogEvent(DateTime Timestamp, string AppId, SteamLogEventKind Kind, int? Pid);

/// <summary>
/// محلّل سطور logs/gameprocess_log.txt — السجل الذي يكتب فيه Steam كل عملية
/// يتتبعها لكل لعبة. هو المصدر الوحيد الموثوق لوقت لعبة شُغّلت عبر steam://
/// لأن Process.Start لا يعيد لنا عملية اللعبة نفسها.
/// </summary>
public static class SteamGameProcessLog
{
    // [2026-11-10 11:10:45] AppID 1605230 adding PID 14724 as a tracked process "D:\...\Spaceship.exe"
    // [2026-11-10 11:13:17] AppID 1605230 no longer tracking PID 14724, exit code 0
    // [2026-11-10 11:13:17] Remove 1605230 from running list
    private static readonly Regex LinePattern = new(
        @"^\[(?<ts>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})\]\s+" +
        @"(?:AppID (?<app>\d+) (?:adding PID (?<added>\d+) as a tracked process|no longer tracking PID (?<removed>\d+))" +
        @"|Remove (?<stopped>\d+) from running list)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string LogPathFor(string steamPath) => Path.Combine(steamPath, "logs", "gameprocess_log.txt");

    public static SteamLogEvent? ParseLine(string line)
    {
        var m = LinePattern.Match(line);
        if (!m.Success) return null;

        // الطوابع الزمنية في السجل بالتوقيت المحلي بلا منطقة زمنية
        if (!DateTime.TryParseExact(m.Groups["ts"].Value, "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var ts))
            return null;

        if (m.Groups["stopped"].Success)
            return new SteamLogEvent(ts, m.Groups["stopped"].Value, SteamLogEventKind.AppStopped, null);

        var app = m.Groups["app"].Value;
        if (m.Groups["added"].Success)
            return new SteamLogEvent(ts, app, SteamLogEventKind.ProcessAdded, int.Parse(m.Groups["added"].Value, CultureInfo.InvariantCulture));

        return new SteamLogEvent(ts, app, SteamLogEventKind.ProcessRemoved, int.Parse(m.Groups["removed"].Value, CultureInfo.InvariantCulture));
    }
}

public enum SteamSessionState
{
    WaitingForStart,
    Running,
    Ended,
    NeverStarted
}

/// <summary>
/// يتتبع جلسة لعبة Steam واحدة من سجل Steam بدل تركيز نافذة Halawa:
/// يُنشأ قبل التشغيل (فيحفظ طول السجل الحالي ويتجاهل ما قبله)، ثم يُستدعى Poll دوريًا.
/// البداية = أول "adding PID" للعبة، والنهاية = "Remove … from running list".
/// احتياط: إن ماتت كل عمليات اللعبة ولم يكتب Steam سطر النهاية (Steam انهار مثلًا)
/// تنتهي الجلسة بعد مهلة سماح قصيرة. وإن لم تبدأ اللعبة خلال مهلة البدء لا يُحتسب شيء.
///
/// Steam Flatpak يعمل في فضاء PIDs خاص، فأرقام العمليات في سجله لا تطابق أرقام
/// النظام؛ عندها نسأل "هل اللعبة حية؟" بمجلد اللعبة بدل الأرقام.
/// </summary>
public sealed class SteamSessionTracker : IPlaySessionTracker
{
    public static readonly TimeSpan DefaultStartupTimeout = TimeSpan.FromMinutes(3);
    public static readonly TimeSpan DefaultDeadProcessGrace = TimeSpan.FromSeconds(15);

    private readonly string _logPath;
    private readonly string _appId;
    private readonly TimeSpan _startupTimeout;
    private readonly TimeSpan _deadProcessGrace;
    private readonly Func<int, bool> _isProcessAlive;
    private readonly Func<bool>? _gameAlive;
    private readonly Func<DateTime> _now;
    private readonly DateTime _createdAt;
    private readonly HashSet<int> _pids = new();
    private long _offset;
    private DateTime? _allDeadSince;

    public string Kind => "steam";
    public string LogPath => _logPath;
    public string AppId => _appId;
    public long LogOffset => _offset;
    public SteamSessionState State { get; private set; } = SteamSessionState.WaitingForStart;
    public SessionFailure Failure { get; private set; }

    /// بالتوقيت المحلي، كما يكتبه Steam
    public DateTime? StartedAt { get; private set; }
    public DateTime? EndedAt { get; private set; }
    public DateTime? LastSeenUtc { get; private set; }
    public DateTime? StartedAtUtc => StartedAt?.ToUniversalTime();
    public IReadOnlyCollection<int> Pids => _pids;

    internal SteamSessionTracker(
        string logPath,
        string appId,
        TimeSpan? startupTimeout = null,
        TimeSpan? deadProcessGrace = null,
        Func<int, bool>? isProcessAlive = null,
        Func<DateTime>? now = null,
        Func<bool>? gameAlive = null)
    {
        _logPath = logPath;
        _appId = appId;
        _startupTimeout = startupTimeout ?? DefaultStartupTimeout;
        _deadProcessGrace = deadProcessGrace ?? DefaultDeadProcessGrace;
        _isProcessAlive = isProcessAlive ?? SystemProcessScanner.Instance.IsAlive;
        _gameAlive = gameAlive;
        _now = now ?? (() => DateTime.Now);
        _createdAt = _now();

        try
        {
            _offset = File.Exists(logPath) ? new FileInfo(logPath).Length : 0;
        }
        catch (IOException)
        {
            _offset = 0;
        }
    }

    /// <summary>
    /// null إذا لم نجد Steam أو سجله — عندها يتتبع المستدعي بمجلد اللعبة.
    /// يجب استدعاؤه قبل Process.Start حتى لا يفوته سطر البدء.
    /// </summary>
    public static SteamSessionTracker? TryCreate(string appId, string? installDir = null)
    {
        if (!IsValidAppId(appId)) return null;

        var steamPath = SteamLibraryProvider.GetSteamPath();
        if (string.IsNullOrEmpty(steamPath)) return null;

        var logPath = SteamGameProcessLog.LogPathFor(steamPath);
        if (!File.Exists(logPath)) return null;
        return new SteamSessionTracker(logPath, appId, gameAlive: FlatpakAliveCheck(steamPath, installDir));
    }

    /// استئناف جلسة محفوظة من موضعها في السجل؛ سطر النهاية إن كُتب بعدها يُقرأ في أول Poll
    public static SteamSessionTracker Resume(string logPath, string appId, DateTime startedUtc, IEnumerable<int> pids,
        long offset, string? installDir = null, Func<int, bool>? isProcessAlive = null, Func<DateTime>? now = null)
    {
        var steamPath = Path.GetDirectoryName(Path.GetDirectoryName(logPath)) ?? "";
        var t = new SteamSessionTracker(logPath, appId, isProcessAlive: isProcessAlive, now: now,
            gameAlive: FlatpakAliveCheck(steamPath, installDir));
        t._offset = offset;
        t.State = SteamSessionState.Running;
        t.StartedAt = startedUtc.ToLocalTime();
        t.LastSeenUtc = DateTime.UtcNow;
        foreach (var pid in pids) t._pids.Add(pid);
        return t;
    }

    internal static bool IsValidAppId(string? appId) =>
        !string.IsNullOrEmpty(appId) && appId.All(char.IsAsciiDigit);

    internal static bool IsFlatpakSteam(string steamPath) =>
        steamPath.Replace('\\', '/').Contains("/.var/app/com.valvesoftware.Steam/", StringComparison.Ordinal);

    private static Func<bool>? FlatpakAliveCheck(string steamPath, string? installDir)
    {
        if (!IsFlatpakSteam(steamPath) || string.IsNullOrEmpty(installDir)) return null;
        return () => FolderSessionTracker.IsRunningIn(installDir);
    }

    /// هل عملية من عمليات الجلسة حية؟ (للاستئناف بعد إعادة تشغيل Halawa)
    public bool AnyAlive() => _gameAlive?.Invoke() ?? _pids.Any(_isProcessAlive);

    PlaySessionState IPlaySessionTracker.State => Map(State);
    PlaySessionState IPlaySessionTracker.Poll() => Map(Poll());

    private static PlaySessionState Map(SteamSessionState s) => s switch
    {
        SteamSessionState.Running => PlaySessionState.Running,
        SteamSessionState.Ended => PlaySessionState.Ended,
        SteamSessionState.NeverStarted => PlaySessionState.NeverStarted,
        _ => PlaySessionState.WaitingForStart
    };

    public SteamSessionState Poll()
    {
        if (State is SteamSessionState.Ended or SteamSessionState.NeverStarted) return State;

        foreach (var line in ReadNewLines())
        {
            var ev = SteamGameProcessLog.ParseLine(line);
            if (ev != null && ev.AppId == _appId) Apply(ev);
            if (State == SteamSessionState.Ended) return State;
        }

        var now = _now();
        if (State == SteamSessionState.Running)
            CheckForDeadProcesses(now);
        else if (State == SteamSessionState.WaitingForStart && now - _createdAt > _startupTimeout)
        {
            State = SteamSessionState.NeverStarted;
            Failure = SessionFailure.StartTimeout;
        }

        return State;
    }

    public TimeSpan Elapsed() => Elapsed(_now());

    /// مدة اللعب حتى الآن (أو حتى النهاية إن انتهت). صفر إن لم تبدأ اللعبة.
    public TimeSpan Elapsed(DateTime now)
    {
        if (StartedAt is not { } start) return TimeSpan.Zero;
        var span = (EndedAt ?? now) - start;
        return span < TimeSpan.Zero ? TimeSpan.Zero : span;
    }

    private void Apply(SteamLogEvent ev)
    {
        switch (ev.Kind)
        {
            case SteamLogEventKind.ProcessAdded:
                if (State == SteamSessionState.WaitingForStart)
                {
                    State = SteamSessionState.Running;
                    StartedAt = ev.Timestamp;
                    LastSeenUtc = DateTime.UtcNow;
                }
                if (ev.Pid is { } added) _pids.Add(added);
                _allDeadSince = null;
                break;

            case SteamLogEventKind.ProcessRemoved:
                if (ev.Pid is { } removed) _pids.Remove(removed);
                break;

            case SteamLogEventKind.AppStopped:
                if (State == SteamSessionState.Running) End(ev.Timestamp);
                break;
        }
    }

    private void CheckForDeadProcesses(DateTime now)
    {
        // مشغّل وسيط قد يخرج قبل أن يضيف Steam عملية اللعبة الحقيقية، لذا ننتظر مهلة سماح
        if (AnyAlive())
        {
            _allDeadSince = null;
            LastSeenUtc = now.ToUniversalTime();
            return;
        }

        _allDeadSince ??= now;
        if (now - _allDeadSince.Value >= _deadProcessGrace)
            End(_allDeadSince.Value);
    }

    private void End(DateTime at)
    {
        EndedAt = at;
        State = SteamSessionState.Ended;
    }

    /// <summary>
    /// يقرأ فقط ما أُضيف بعد آخر قراءة، ويتوقف عند آخر سطر مكتمل
    /// (السطر الناقص يُقرأ كاملًا في المرة التالية).
    /// </summary>
    private List<string> ReadNewLines()
    {
        var lines = new List<string>();
        try
        {
            using var fs = new FileStream(_logPath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);

            // Steam أعاد التشغيل وبدأ سجلًا جديدًا (ينقل القديم إلى gameprocess_log.previous.txt)
            if (fs.Length < _offset) _offset = 0;
            if (fs.Length == _offset) return lines;

            fs.Seek(_offset, SeekOrigin.Begin);
            var buffer = new byte[fs.Length - _offset];
            var read = 0;
            while (read < buffer.Length)
            {
                var n = fs.Read(buffer, read, buffer.Length - read);
                if (n == 0) break;
                read += n;
            }
            if (read == 0) return lines;

            var lastNewline = Array.LastIndexOf(buffer, (byte)'\n', read - 1);
            if (lastNewline < 0) return lines;

            _offset += lastNewline + 1;
            var text = Encoding.UTF8.GetString(buffer, 0, lastNewline + 1);
            foreach (var line in text.Split('\n'))
            {
                var trimmed = line.TrimEnd('\r');
                if (trimmed.Length > 0) lines.Add(trimmed);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // السجل مقفل أو حُذف مؤقتًا — نحاول في الاستدعاء التالي
        }
        return lines;
    }
}
