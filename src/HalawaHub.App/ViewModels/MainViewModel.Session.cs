using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HalawaHub.App.Services;
using HalawaHub.Core;
using HalawaHub.Core.Covers;
using HalawaHub.Core.Library;
using HalawaHub.Core.Models;
using HalawaHub.Core.News;
using HalawaHub.Core.Plugins;
using HalawaHub.Core.Updates;

namespace HalawaHub.App.ViewModels;

public partial class MainViewModel
{
    private static readonly TimeSpan SteamPollInterval = TimeSpan.FromSeconds(2);

    private GameCardViewModel? _sessionGame;
    private DateTime _sessionStart;
    private Process? _sessionProcess;
    private SteamSessionTracker? _steamTracker;
    private Avalonia.Threading.DispatcherTimer? _steamPollTimer;

    /// <summary>
    /// ألعاب Steam تُشغَّل عبر steam:// فلا نملك عمليتها؛ إن وُجد سجل Steam
    /// نتتبع الجلسة منه (steamTracker) بدل تركيز النافذة الذي كان ينهيها بعد ثوانٍ.
    /// </summary>
    internal void BeginPlaySession(GameCardViewModel card, Process? proc, SteamSessionTracker? steamTracker = null)
    {
        EndPlaySession();
        _sessionGame = card;
        _sessionStart = DateTime.UtcNow;

        if (steamTracker != null)
        {
            _steamTracker = steamTracker;
            StartSteamPolling();
        }
        else if (proc != null && File.Exists(card.Game.ExecutablePath))
        {
            try
            {
                _sessionProcess = proc;
                proc.EnableRaisingEvents = true;
                // عملية لعبة سابقة قد تخرج بعد بدء جلسة جديدة — لا تُنهِ إلا جلستها
                proc.Exited += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (_sessionGame == card) EndPlaySession();
                });
            }
            catch
            {
                _sessionProcess = null;
            }
        }

        Log.Info($"بدأت جلسة لعب: {card.Name}");
    }

    public void OnWindowRegainedFocus()
    {
        // احتياط قديم للجلسات التي لا نملك لها عملية ولا سجلًا (Epic/Xbox...)
        if (_sessionGame != null && _sessionProcess == null && _steamTracker == null)
            EndPlaySession();
    }

    private void StartSteamPolling()
    {
        _steamPollTimer ??= CreateSteamPollTimer();
        _steamPollTimer.Start();
    }

    private Avalonia.Threading.DispatcherTimer CreateSteamPollTimer()
    {
        var timer = new Avalonia.Threading.DispatcherTimer { Interval = SteamPollInterval };
        timer.Tick += (_, _) => PollSteamSession();
        return timer;
    }

    private void StopSteamPolling()
    {
        _steamPollTimer?.Stop();
        _steamTracker = null;
    }

    internal void PollSteamSession()
    {
        var tracker = _steamTracker;
        if (tracker == null || _sessionGame == null)
        {
            // الجلسة أُلغيت من مكان آخر (مثل فشل xdg-open على لينكس)
            StopSteamPolling();
            return;
        }

        switch (tracker.Poll())
        {
            case SteamSessionState.Ended:
                EndPlaySession();
                break;

            case SteamSessionState.NeverStarted:
                Log.Info($"Steam: لم تبدأ {_sessionGame.Name} خلال المهلة — لا يُحتسب وقت لعب");
                _sessionGame = null;
                StopSteamPolling();
                break;
        }
    }

    private void EndPlaySession()
    {
        var card = _sessionGame;
        var tracker = _steamTracker;
        StopSteamPolling();
        _sessionProcess = null;
        if (card == null) return;
        _sessionGame = null;

        var secs = tracker != null
            ? tracker.Elapsed(DateTime.Now).TotalSeconds
            : (DateTime.UtcNow - _sessionStart).TotalSeconds;
        PlayTimeService.AddSeconds(card.Game.Platform, card.Game.Id, secs);
        card.RefreshPlayTime();
        UpdateHomeViewCollections();
        Log.Info($"انتهت جلسة لعب {card.Name}: {(int)secs / 60} دقيقة");
    }

    private void LaunchGame(GameCardViewModel? card)
    {
        if (card == null) return;

        var game = card.Game;
        ClearStatus();

        try
        {
            var psi = new ProcessStartInfo(game.ExecutablePath) { UseShellExecute = true };

            var args = game.LaunchArguments ?? "";
            if (!string.IsNullOrWhiteSpace(card.LaunchParameters))
                args = string.IsNullOrEmpty(args) ? card.LaunchParameters : $"{args} {card.LaunchParameters}";

            if (!string.IsNullOrEmpty(args))
                psi.Arguments = args;

            // قبل التشغيل: يحفظ طول سجل Steam الحالي حتى لا يفوته سطر بدء اللعبة
            var steamTracker = game.Platform == "Steam" ? SteamSessionTracker.TryCreate(game.Id) : null;

            var proc = Process.Start(psi);
            BeginPlaySession(card, proc, steamTracker);

            if (OperatingSystem.IsLinux() && proc != null && GameCardViewModel.IsUrlLaunch(game.ExecutablePath))
                _ = WatchLinuxOpenerAsync(card, proc);

            card.NotifyPlayed();
            UpdateHomeViewCollections();
        }
        catch (Exception ex)
        {
            ShowStatus(Loc.Instance.Format("launchFailed", card.Name, ex.Message));
        }
    }

    /// <summary>
    /// xdg-open ينجح في البدء دائمًا، ويخبر عن المشغّل غير المثبت برمز خروج فقط.
    /// بدون هذا يضغط المستخدم "تشغيل" ولا يحدث شيء، وتُحسب جلسة لعب وهمية.
    /// </summary>
    private async Task WatchLinuxOpenerAsync(GameCardViewModel card, Process opener)
    {
        try
        {
            var exited = opener.WaitForExitAsync();
            if (await Task.WhenAny(exited, Task.Delay(TimeSpan.FromSeconds(5))) != exited) return;
            if (opener.ExitCode == 0) return;

            Log.Error($"launcher opener exited with {opener.ExitCode} for {card.Game.ExecutablePath}");
            if (_sessionGame == card) _sessionGame = null;
            ShowStatus(Loc.Instance.Format("launchFailed", card.Name, $"xdg-open: {opener.ExitCode}"));
        }
        catch (Exception ex)
        {
            Log.Error("could not watch the launcher opener", ex);
        }
    }

    private void DeleteGame()
    {
        if (DetailsGame == null) return;
        var game = DetailsGame.Game;

        // نعتمد فقط على أدوات الحذف الرسمية للمنصة نفسها (تفتح تأكيدها الخاص) —
        // ما نحذف أي ملفات مباشرة من هنا تفاديًا لأي خطر على بيانات المستخدم
        if (game.Platform == "Steam" && !string.IsNullOrEmpty(game.Id))
        {
            try
            {
                Process.Start(new ProcessStartInfo($"steam://uninstall/{game.Id}") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                ShowStatus(Loc.Instance.Format("uninstallFailed", ex.Message));
            }
        }
        else
        {
            ShowStatus(Loc.Instance["uninstallUnsupported"]);
        }

        CloseDetails();
    }

}
