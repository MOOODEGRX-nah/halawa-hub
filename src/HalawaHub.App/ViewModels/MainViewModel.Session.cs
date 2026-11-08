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
    private GameCardViewModel? _sessionGame;
    private DateTime _sessionStart;
    private Process? _sessionProcess;

    private void BeginPlaySession(GameCardViewModel card, Process? proc)
    {
        EndPlaySession();
        _sessionGame = card;
        _sessionStart = DateTime.UtcNow;

        if (proc != null && File.Exists(card.Game.ExecutablePath))
        {
            try
            {
                _sessionProcess = proc;
                proc.EnableRaisingEvents = true;
                proc.Exited += (_, _) =>
                    Avalonia.Threading.Dispatcher.UIThread.Post(EndPlaySession);
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
        if (_sessionGame != null && _sessionProcess == null)
            EndPlaySession();
    }

    private void EndPlaySession()
    {
        var card = _sessionGame;
        if (card == null) return;
        _sessionGame = null;

        var secs = (DateTime.UtcNow - _sessionStart).TotalSeconds;
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

            var proc = Process.Start(psi);
            BeginPlaySession(card, proc);

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
