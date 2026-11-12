using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HalawaHub.App.Services;
using HalawaHub.Core;
using HalawaHub.Core.Library;
using HalawaHub.Core.Models;
using HalawaHub.Core.Sessions;

namespace HalawaHub.App.ViewModels;

/// <summary>
/// جلسات اللعب. لكل لعبة متتبع حقيقي حيث أمكن:
///   Steam  → سجل Steam (gameprocess_log.txt)، وإلا مجلد اللعبة
///   غيرها → العمليات التي ملفها داخل مجلد اللعبة (Epic, GOG, Xbox, Riot, Heroic, Lutris)
///   ملف تنفيذي مباشر بلا مجلد صالح → العملية نفسها
/// فقط حين لا يوجد أي متتبع يبقى الاحتياط القديم: الجلسة تنتهي بعودة التركيز لنافذة Halawa.
/// الجلسة الجارية تُحفظ في active-session.json لتُستأنف أو تُغلق عند الإقلاع التالي.
/// </summary>
public partial class MainViewModel
{
    private static readonly TimeSpan SessionPollInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan SessionSaveInterval = TimeSpan.FromSeconds(30);

    private sealed class PlaySession
    {
        public required string Platform { get; init; }
        public required string Id { get; init; }
        public required string Name { get; init; }
        public required string InstallDir { get; init; }
        public DateTime StartUtc { get; init; } = DateTime.UtcNow;
        public IPlaySessionTracker? Tracker { get; init; }
        public bool Persisted { get; set; }

        /// Halawa هو من شغّل Steam لهذه اللعبة، والمستخدم طلب إغلاقه بعدها
        public bool CloseSteamAfter { get; set; }
    }

    private PlaySession? _session;
    private Avalonia.Threading.DispatcherTimer? _sessionTimer;
    private bool _sessionPolling;
    private DateTime _lastSessionSave;

    internal IProcessScanner ProcessScanner { get; set; } = SystemProcessScanner.Instance;
    internal LaunchPreflight Preflight { get; set; } = LaunchPreflight.System;
    internal string ActiveSessionPath { get; set; } = ActiveSessionStore.DefaultPath;

    internal bool HasActiveSession => _session != null;

    internal void BeginPlaySession(GameCardViewModel card, IPlaySessionTracker? tracker) =>
        BeginPlaySession(card.Game, tracker);

    private void BeginPlaySession(GameInfo game, IPlaySessionTracker? tracker, DateTime? startUtc = null)
    {
        EndPlaySession();
        _session = new PlaySession
        {
            Platform = game.Platform,
            Id = game.Id,
            Name = game.Name,
            InstallDir = game.InstallPath ?? "",
            Tracker = tracker,
            StartUtc = startUtc ?? DateTime.UtcNow
        };

        if (tracker != null)
        {
            if (tracker.State == PlaySessionState.NeverStarted)
            {
                HandleNeverStarted(tracker);
                return;
            }
            PersistSessionIfRunning(force: true);
            StartSessionPolling();
        }

        Log.Info($"بدأت جلسة لعب: {game.Name} ({tracker?.Kind ?? "focus"})");
        if (tracker?.State == PlaySessionState.Running) RefreshStatusOf(game.Platform, game.Id);
    }

    public void OnWindowRegainedFocus()
    {
        // احتياط قديم فقط للجلسات التي لا متتبع لها إطلاقًا
        RefreshStatusesOnFocus();

        if (_session is { Tracker: null })
            EndPlaySession();
    }

    private void StartSessionPolling()
    {
        if (_sessionTimer == null)
        {
            _sessionTimer = new Avalonia.Threading.DispatcherTimer { Interval = SessionPollInterval };
            _sessionTimer.Tick += (_, _) => OnSessionTimerTick();
        }
        _sessionTimer.Start();
    }

    private void StopSessionPolling() => _sessionTimer?.Stop();

    /// فحص العمليات قد يأخذ عشرات الميلي ثانية، فيجري على خيط خلفي
    private async void OnSessionTimerTick()
    {
        var session = _session;
        if (session?.Tracker is not { } tracker)
        {
            StopSessionPolling();
            return;
        }
        if (_sessionPolling) return;

        _sessionPolling = true;
        try
        {
            var state = await Task.Run(tracker.Poll);
            if (ReferenceEquals(_session, session)) HandleSessionState(tracker, state);
        }
        catch (Exception ex)
        {
            Log.Error("session poll failed", ex);
        }
        finally
        {
            _sessionPolling = false;
        }
    }

    /// متزامن: للاختبارات
    internal void PollSession()
    {
        if (_session?.Tracker is not { } tracker)
        {
            StopSessionPolling();
            return;
        }
        HandleSessionState(tracker, tracker.Poll());
    }

    private void HandleSessionState(IPlaySessionTracker tracker, PlaySessionState state)
    {
        switch (state)
        {
            case PlaySessionState.Running:
                if (_session is { Persisted: false } first) RefreshStatusOf(first.Platform, first.Id);
                PersistSessionIfRunning(force: false);
                break;
            case PlaySessionState.Ended:
                EndPlaySession();
                break;
            case PlaySessionState.NeverStarted:
                HandleNeverStarted(tracker);
                break;
        }
    }

    private void HandleNeverStarted(IPlaySessionTracker tracker)
    {
        var name = _session?.Name ?? "";
        CancelPlaySession();

        var key = tracker.Failure == SessionFailure.AlreadyRunning ? "launchAlreadyRunning" : "launchTimeout";
        Log.Info($"{name}: {key} — لا يُحتسب وقت لعب");
        var message = Loc.Instance.Format(key, name);
        if (key == "launchTimeout" && _lastLaunchHint is { } hint) message += " " + hint;
        ShowStatus(message);
        Notify("launch", message);
    }

    private string? _lastLaunchHint;

    /// إلغاء بلا احتساب (لم تبدأ اللعبة، أو فشل المشغّل)
    private void CancelPlaySession()
    {
        StopSessionPolling();
        if (_session == null) return;
        _session = null;
        ActiveSessionStore.Delete(ActiveSessionPath);
    }

    private void PersistSessionIfRunning(bool force)
    {
        if (_session is not { Tracker: { State: PlaySessionState.Running } tracker } session) return;
        if (!force && session.Persisted && DateTime.UtcNow - _lastSessionSave < SessionSaveInterval) return;

        var record = ActiveSessionStore.FromTracker(tracker, session.Platform, session.Id, session.Name, session.InstallDir);
        if (record == null) return;
        ActiveSessionStore.Save(ActiveSessionPath, record);
        session.Persisted = true;
        _lastSessionSave = DateTime.UtcNow;
    }

    /// عند إغلاق النافذة: آخر لحظة رأينا فيها اللعبة تُحفظ فورًا
    internal void PersistActiveSession() => PersistSessionIfRunning(force: true);

    private void EndPlaySession()
    {
        var session = _session;
        StopSessionPolling();
        if (session == null) return;
        _session = null;
        ActiveSessionStore.Delete(ActiveSessionPath);

        var secs = session.Tracker != null
            ? session.Tracker.Elapsed().TotalSeconds
            : (DateTime.UtcNow - session.StartUtc).TotalSeconds;
        CreditPlayTime(session.Platform, session.Id, secs);
        if (session.CloseSteamAfter) ExitSteam();
        RefreshStatusOf(session.Platform, session.Id);
        Log.Info($"انتهت جلسة لعب {session.Name}: {(int)secs / 60} دقيقة");
    }

    private void CreditPlayTime(string platform, string id, double secs)
    {
        PlayTimeService.AddSeconds(platform, id, secs);
        // البطاقات تُبنى من جديد مع كل فحص، فنبحث عن الحالية لا عن التي بدأت الجلسة
        foreach (var card in Games.Where(c => c.Game.Platform == platform && c.Game.Id == id))
            card.RefreshPlayTime();
        UpdateHomeViewCollections();
    }

    /// <summary>
    /// عند الإقلاع: جلسة محفوظة من تشغيل سابق. إن كانت اللعبة ما زالت تعمل نكمل
    /// تتبعها، وإلا نحتسب الوقت حتى آخر لحظة رأيناها فيها.
    /// </summary>
    internal async Task RecoverActiveSessionAsync()
    {
        try
        {
            var path = ActiveSessionPath;
            var scanner = ProcessScanner;
            var record = await Task.Run(() => ActiveSessionStore.Load(path));
            if (record == null) return;

            var recovery = await Task.Run(() => ActiveSessionRecovery.Recover(record, scanner));
            if (recovery.Resumed != null && _session == null)
            {
                var game = new GameInfo { Platform = record.Platform, Id = record.Id, Name = record.Name, InstallPath = record.InstallDir };
                BeginPlaySession(game, recovery.Resumed, record.StartedUtc);
                Log.Info($"استؤنفت جلسة {record.Name} من التشغيل السابق");
                return;
            }

            ActiveSessionStore.Delete(path);
            CreditPlayTime(record.Platform, record.Id, recovery.ClosedDuration.TotalSeconds);
            Log.Info($"أُغلقت جلسة {record.Name} المحفوظة: {(int)recovery.ClosedDuration.TotalMinutes} دقيقة");
        }
        catch (Exception ex)
        {
            Log.Error("active session recovery failed", ex);
        }
    }

    /// <summary>
    /// يُنشأ قبل Process.Start (يلتقط حالة السجل والعمليات قبل التشغيل).
    /// ambiguous = مجلد اللعبة يتداخل مع مجلد لعبة أخرى، فلا نعرف أي لعبة تعمل: لا نحتسب.
    /// </summary>
    internal (IPlaySessionTracker? Tracker, bool Ambiguous) CreateTracker(GameInfo game)
    {
        if (game.Platform == "Steam")
        {
            var steam = SteamSessionTracker.TryCreate(game.Id, game.InstallPath);
            if (steam != null) return (steam, false);
        }

        if (SharesFolderWithAnotherGame(game)) return (null, true);
        return (FolderSessionTracker.TryCreate(game.InstallPath, ProcessScanner), false);
    }

    private bool SharesFolderWithAnotherGame(GameInfo game)
    {
        var mine = GameFolderMatcher.NormalizeDir(game.InstallPath);
        if (mine == null) return false;
        var windows = OperatingSystem.IsWindows();

        foreach (var other in Games.Select(c => c.Game))
        {
            if (ReferenceEquals(other, game) || (other.Platform == game.Platform && other.Id == game.Id)) continue;
            var theirs = GameFolderMatcher.NormalizeDir(other.InstallPath);
            if (theirs == null) continue;
            if (string.Equals(mine, theirs, windows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
                || GameFolderMatcher.PathIsInside(mine, theirs, windows)
                || GameFolderMatcher.PathIsInside(theirs, mine, windows))
                return true;
        }
        return false;
    }

    internal static string? LaunchProblemMessage(LaunchProblem problem, string gameName, string detail) => problem switch
    {
        LaunchProblem.DriveNotConnected => Loc.Instance.Format("launchDriveMissing", gameName, detail),
        LaunchProblem.GameFilesMissing => Loc.Instance.Format("launchFilesMissing", gameName, detail),
        LaunchProblem.LauncherNotFound => Loc.Instance.Format("launchLauncherMissing", gameName, detail),
        LaunchProblem.SteamNotSignedIn => Loc.Instance.Format("launchSteamSignedOut", gameName),
        _ => null
    };

    private void LaunchGame(GameCardViewModel? card)
    {
        if (card == null || AskLaunchFrom(card)) return;
        LaunchGameDirect(card);
    }

    /// تشغيل هذه النسخة بعينها (بعد اختيار المنصة إن كانت اللعبة على أكثر من واحدة)
    private void LaunchGameDirect(GameCardViewModel card)
    {
        if (AskBeforePlayingWithUpdate(card)) return;
        LaunchGameNow(card);
    }

    private void LaunchGameNow(GameCardViewModel? card)
    {
        if (card == null) return;

        var game = card.Game;
        ClearStatus();
        _lastLaunchHint = null;

        // لعبة Steam مملوكة غير مثبتة: نطلب من Steam تثبيتها، ولا جلسة لعب
        if (!game.IsInstalled && game.Platform == "Steam" && game.ExecutablePath.StartsWith("steam://install/", StringComparison.Ordinal))
        {
            if (RunLauncherCommand(new LauncherCommand(game.ExecutablePath), "openLauncherFailed"))
                ShowStatus(Loc.Instance.Format("installAsked", card.Name));
            return;
        }

        var (problem, detail) = Preflight.Check(game);
        var problemMessage = LaunchProblemMessage(problem, card.Name, detail);
        if (LaunchPreflight.BlocksLaunch(problem))
        {
            Log.Info($"launch blocked for {game.Name}: {problem} ({detail})");
            ShowStatus(problemMessage!);
            Notify("launch", problemMessage!);
            return;
        }

        try
        {
            var psi = new ProcessStartInfo(game.ExecutablePath) { UseShellExecute = true };

            var args = game.LaunchArguments ?? "";
            if (!string.IsNullOrWhiteSpace(card.LaunchParameters))
                args = string.IsNullOrEmpty(args) ? card.LaunchParameters : $"{args} {card.LaunchParameters}";

            if (!string.IsNullOrEmpty(args))
                psi.Arguments = args;

            // هل كان Steam يعمل قبلنا؟ (لإغلاقه بعد اللعبة فقط إن كنا نحن من شغّله)
            var steamWasRunning = game.Platform == "Steam" && IsSteamRunning();

            // قبل التشغيل: طول سجل Steam ولقطة العمليات الحالية
            var (tracker, ambiguous) = CreateTracker(game);

            var proc = Process.Start(psi);

            // لعبة شُغّلت من ملفها مباشرة ولا مجلد صالح لها: نتتبع عمليتها
            if (tracker == null && !ambiguous && proc != null && File.Exists(game.ExecutablePath))
            {
                try { tracker = new ProcessSessionTracker(proc.Id, ProcessScanner); }
                catch (InvalidOperationException) { /* انتهت فورًا */ }
            }

            if (ambiguous)
            {
                Log.Info($"{game.Name}: مجلدها يتداخل مع لعبة أخرى — لا يُحتسب وقت لعب");
                EndPlaySession();
            }
            else
            {
                BeginPlaySession(card, tracker);
                if (_session != null) _session.CloseSteamAfter = CloseSteamAfterGame && game.Platform == "Steam" && !steamWasRunning;
            }

            if (problem == LaunchProblem.SteamNotSignedIn)
            {
                _lastLaunchHint = problemMessage;
                ShowStatus(problemMessage!);
            }

            if (OperatingSystem.IsLinux() && proc != null && GameCardViewModel.IsUrlLaunch(game.ExecutablePath))
                _ = WatchLinuxOpenerAsync(game, proc);
            else
                proc?.Dispose();

            if (MinimizeOnGameStart) MinimizeRequested?.Invoke(this, EventArgs.Empty);

            card.NotifyPlayed();
            UpdateHomeViewCollections();
        }
        catch (Exception ex)
        {
            Log.Error($"launch failed for {game.Name}", ex);
            var failed = Loc.Instance.Format("launchFailed", card.Name, ex.Message);
            ShowStatus(failed);
            Notify("launch", failed);
        }
    }

    /// <summary>
    /// xdg-open ينجح في البدء دائمًا، ويخبر عن المشغّل غير المثبت برمز خروج فقط.
    /// بدون هذا يضغط المستخدم "تشغيل" ولا يحدث شيء، وتُحسب جلسة لعب وهمية.
    /// </summary>
    private async Task WatchLinuxOpenerAsync(GameInfo game, Process opener)
    {
        using (opener)
        {
            try
            {
                var exited = opener.WaitForExitAsync();
                if (await Task.WhenAny(exited, Task.Delay(TimeSpan.FromSeconds(5))) != exited) return;
                if (opener.ExitCode == 0) return;

                Log.Error($"launcher opener exited with {opener.ExitCode} for {game.ExecutablePath}");
                if (_session != null && _session.Platform == game.Platform && _session.Id == game.Id) CancelPlaySession();
                var scheme = LaunchPreflight.UrlScheme(game.ExecutablePath) ?? game.Platform;
                ShowStatus(Loc.Instance.Format("launchLauncherMissing", game.Name, scheme));
            }
            catch (Exception ex)
            {
                Log.Error("could not watch the launcher opener", ex);
            }
        }
    }

    private void DeleteGame()
    {
        if (DetailsGame == null) return;
        var game = DetailsGame.Game;

        // نعتمد فقط على أدوات الحذف الرسمية للمنصة نفسها (تفتح تأكيدها الخاص) —
        // ما نحذف أي ملفات مباشرة من هنا تفاديًا لأي خطر على بيانات المستخدم
        if (Launchers.Uninstall(game) is { } uninstall)
            RunLauncherCommand(uninstall.Command, "uninstallFailed");
        else
            ShowStatus(Loc.Instance["uninstallUnsupported"]);

        CloseDetails();
    }

    private static bool IsSteamRunning()
    {
        try
        {
            var procs = Process.GetProcessesByName("steam");
            foreach (var p in procs) p.Dispose();
            return procs.Length > 0;
        }
        catch
        {
            return true; // لا نعرف: لا نغلقه
        }
    }

    /// steam://ExitSteam موثّق في steam(6): يغلق Steam بشكل طبيعي
    private void ExitSteam()
    {
        try
        {
            Log.Info("closing Steam after the game (Halawa started it)");
            Process.Start(new ProcessStartInfo("steam://ExitSteam") { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Error("could not close Steam", ex);
        }
    }
}
