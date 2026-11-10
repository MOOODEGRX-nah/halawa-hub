namespace HalawaHub.Core.Sessions;

public enum PlaySessionState
{
    WaitingForStart,
    Running,
    Ended,
    NeverStarted
}

/// لماذا لم تبدأ الجلسة — يحدد الرسالة التي يراها المستخدم
public enum SessionFailure
{
    None,
    StartTimeout,
    AlreadyRunning
}

/// <summary>
/// متتبع جلسة لعب واحدة. يُنشأ قبل التشغيل ثم يُستدعى Poll دوريًا (من خيط خلفي:
/// المتتبعات لا تلمس الواجهة). كل الأوقات UTC.
/// </summary>
public interface IPlaySessionTracker
{
    /// "steam" أو "folder" أو "process" — يُحفظ في active-session.json
    string Kind { get; }

    PlaySessionState State { get; }
    SessionFailure Failure { get; }
    DateTime? StartedAtUtc { get; }

    /// آخر لحظة رأينا فيها اللعبة تعمل؛ إن أُغلق Halawa فجأة تُغلق الجلسة عندها
    DateTime? LastSeenUtc { get; }

    IReadOnlyCollection<int> Pids { get; }

    PlaySessionState Poll();

    /// مدة اللعب حتى الآن (أو حتى النهاية). صفر إن لم تبدأ اللعبة.
    TimeSpan Elapsed();
}

/// <summary>
/// تتبع بمجلد اللعبة (Epic, GOG, Xbox, Riot على ويندوز؛ Heroic, Lutris على لينكس):
/// اللعبة "تعمل" ما دامت هناك عملية ملفها التنفيذي داخل مجلد تثبيتها.
/// - لقطة قبل التشغيل: إن كانت اللعبة تعمل مسبقًا لا نحتسب شيئًا (لا نعرف متى بدأت).
/// - إن لم تظهر عملية خلال مهلة البدء (180 ث) لا يُحتسب شيء.
/// - النهاية عند آخر مرة رأيناها، بعد مهلة سماح قصيرة (مشغّل يعيد تشغيل اللعبة مثلًا).
/// </summary>
public sealed class FolderSessionTracker : IPlaySessionTracker
{
    public static readonly TimeSpan DefaultStartupTimeout = TimeSpan.FromSeconds(180);
    public static readonly TimeSpan DefaultEndGrace = TimeSpan.FromSeconds(8);

    private readonly string _dir;
    private readonly IProcessScanner _scanner;
    private readonly Func<DateTime> _utcNow;
    private readonly TimeSpan _startupTimeout;
    private readonly TimeSpan _endGrace;
    private readonly bool _windows;
    private readonly DateTime _createdAt;
    private DateTime? _endedAt;
    private int[] _pids = Array.Empty<int>();

    public string Kind => "folder";
    public string InstallDir => _dir;
    public PlaySessionState State { get; private set; } = PlaySessionState.WaitingForStart;
    public SessionFailure Failure { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? LastSeenUtc { get; private set; }
    public IReadOnlyCollection<int> Pids => _pids;

    private FolderSessionTracker(string normalizedDir, IProcessScanner scanner, Func<DateTime>? utcNow,
        TimeSpan? startupTimeout, TimeSpan? endGrace, bool? windows)
    {
        _dir = normalizedDir;
        _scanner = scanner;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
        _startupTimeout = startupTimeout ?? DefaultStartupTimeout;
        _endGrace = endGrace ?? DefaultEndGrace;
        _windows = windows ?? OperatingSystem.IsWindows();
        _createdAt = _utcNow();
    }

    /// <summary>
    /// null إذا كان المجلد غير صالح للتتبع (فارغ، غير موجود، أو واسع جدًا).
    /// يأخذ لقطة العمليات فورًا، لذا يُستدعى قبل تشغيل اللعبة.
    /// </summary>
    public static FolderSessionTracker? TryCreate(string? installDir, IProcessScanner? scanner = null,
        Func<DateTime>? utcNow = null, TimeSpan? startupTimeout = null, TimeSpan? endGrace = null,
        bool? windows = null, Func<string, bool>? dirExists = null)
    {
        var win = windows ?? OperatingSystem.IsWindows();
        var dir = GameFolderMatcher.NormalizeDir(installDir, win);
        if (dir == null || GameFolderMatcher.IsTooBroad(dir, win)) return null;
        if (!(dirExists ?? Directory.Exists)(dir)) return null;

        var tracker = new FolderSessionTracker(dir, scanner ?? SystemProcessScanner.Instance, utcNow, startupTimeout, endGrace, win);
        if (tracker.Matching().Length > 0)
        {
            tracker.State = PlaySessionState.NeverStarted;
            tracker.Failure = SessionFailure.AlreadyRunning;
        }
        return tracker;
    }

    /// استئناف جلسة محفوظة (Halawa أُغلق واللعبة ما زالت تعمل)
    public static FolderSessionTracker Resume(string installDir, DateTime startedUtc, DateTime lastSeenUtc,
        IProcessScanner? scanner = null, Func<DateTime>? utcNow = null, bool? windows = null)
    {
        var dir = GameFolderMatcher.NormalizeDir(installDir, windows) ?? installDir;
        return new FolderSessionTracker(dir, scanner ?? SystemProcessScanner.Instance, utcNow, null, null, windows)
        {
            State = PlaySessionState.Running,
            StartedAtUtc = startedUtc,
            LastSeenUtc = lastSeenUtc
        };
    }

    /// هل تعمل عملية من هذا المجلد الآن؟ (للاستئناف بعد إعادة تشغيل Halawa)
    public static bool IsRunningIn(string installDir, IProcessScanner? scanner = null, bool? windows = null)
    {
        var dir = GameFolderMatcher.NormalizeDir(installDir, windows);
        if (dir == null) return false;
        var win = windows ?? OperatingSystem.IsWindows();
        return (scanner ?? SystemProcessScanner.Instance).Snapshot()
            .Any(p => !GameFolderMatcher.IsIgnoredHelper(p.Name) && GameFolderMatcher.IsInside(p, dir, win));
    }

    private int[] Matching() =>
        _scanner.Snapshot()
            .Where(p => !GameFolderMatcher.IsIgnoredHelper(p.Name) && GameFolderMatcher.IsInside(p, _dir, _windows))
            .Select(p => p.Pid)
            .ToArray();

    public PlaySessionState Poll()
    {
        if (State is PlaySessionState.Ended or PlaySessionState.NeverStarted) return State;

        var now = _utcNow();
        var pids = Matching();

        if (State == PlaySessionState.WaitingForStart)
        {
            if (pids.Length > 0)
            {
                State = PlaySessionState.Running;
                StartedAtUtc = now;
                LastSeenUtc = now;
                _pids = pids;
            }
            else if (now - _createdAt > _startupTimeout)
            {
                State = PlaySessionState.NeverStarted;
                Failure = SessionFailure.StartTimeout;
            }
            return State;
        }

        if (pids.Length > 0)
        {
            LastSeenUtc = now;
            _pids = pids;
        }
        else if (now - (LastSeenUtc ?? now) >= _endGrace)
        {
            _endedAt = LastSeenUtc;
            State = PlaySessionState.Ended;
        }
        return State;
    }

    public TimeSpan Elapsed()
    {
        if (StartedAtUtc is not { } start) return TimeSpan.Zero;
        var end = _endedAt ?? (State == PlaySessionState.Running ? _utcNow() : LastSeenUtc ?? start);
        var span = end - start;
        return span < TimeSpan.Zero ? TimeSpan.Zero : span;
    }
}

/// <summary>
/// تتبع بعملية واحدة نعرفها (لعبة شُغّلت مباشرة من ملفها، ولا مجلد صالحًا لها).
/// تبدأ فورًا وتنتهي عند خروج العملية.
/// </summary>
public sealed class ProcessSessionTracker : IPlaySessionTracker
{
    private readonly int _pid;
    private readonly IProcessScanner _scanner;
    private readonly Func<DateTime> _utcNow;
    private DateTime? _endedAt;

    public string Kind => "process";
    public PlaySessionState State { get; private set; } = PlaySessionState.Running;
    public SessionFailure Failure => SessionFailure.None;
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? LastSeenUtc { get; private set; }
    public IReadOnlyCollection<int> Pids => new[] { _pid };

    public ProcessSessionTracker(int pid, IProcessScanner? scanner = null, Func<DateTime>? utcNow = null, DateTime? startedUtc = null)
    {
        _pid = pid;
        _scanner = scanner ?? SystemProcessScanner.Instance;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
        StartedAtUtc = startedUtc ?? _utcNow();
        LastSeenUtc = _utcNow();
    }

    public PlaySessionState Poll()
    {
        if (State != PlaySessionState.Running) return State;
        if (_scanner.IsAlive(_pid))
        {
            LastSeenUtc = _utcNow();
        }
        else
        {
            _endedAt = _utcNow();
            State = PlaySessionState.Ended;
        }
        return State;
    }

    public TimeSpan Elapsed()
    {
        var end = _endedAt ?? _utcNow();
        var span = end - StartedAtUtc!.Value;
        return span < TimeSpan.Zero ? TimeSpan.Zero : span;
    }
}
