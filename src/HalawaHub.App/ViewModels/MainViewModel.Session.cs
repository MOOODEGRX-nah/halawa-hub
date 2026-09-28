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
        StatusMessage = null;

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

            card.NotifyPlayed();
            UpdateHomeViewCollections();
        }
        catch (Exception ex)
        {
            StatusMessage = $"تعذر تشغيل {game.Name}: {ex.Message}";
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
                StatusMessage = $"تعذر فتح نافذة الحذف: {ex.Message}";
            }
        }
        else
        {
            StatusMessage = "الحذف المباشر مو مدعوم بعد لهذي المنصة — احذفها من اللانشر الرسمي.";
        }

        DetailsGame = null;
    }

}
