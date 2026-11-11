using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using HalawaHub.App.Services;
using HalawaHub.Core;
using HalawaHub.Core.Library;

namespace HalawaHub.App.ViewModels;

/// <summary>
/// حالة الألعاب (تحديث، إصلاح، تعمل، القرص غير موصول) وإجراءاتها: افتح في المشغّل،
/// افحص الملفات، وسؤال قبل تشغيل لعبة لها تحديث. القراءات رخيصة (ملفات وسجل ويندوز)
/// وتُعاد عند تحديث المكتبة، وعند رجوع التركيز للنافذة، وبعد خروج اللعبة.
/// </summary>
public partial class MainViewModel
{
    private static readonly TimeSpan FocusStatusThrottle = TimeSpan.FromSeconds(10);

    /// null في الاختبارات حتى لا تقرأ حالة Steam الحقيقية على جهاز المطوّر
    internal GameStatusProbe? StatusProbe { get; set; }
    internal LauncherActions Launchers { get; set; } = LauncherActions.System;

    /// null في الاختبارات: لا ذاكرة مكتبة مشتركة بين الاختبارات
    internal string? LibraryCachePath { get; set; }

    private DateTime _lastStatusRefresh = DateTime.MinValue;
    private int _statusRefreshToken;

    private RelayCommand? _openInLauncherCommand;
    private RelayCommand? _verifyFilesCommand;

    public RelayCommand OpenInLauncherCommand =>
        _openInLauncherCommand ??= new RelayCommand(param => OpenInLauncher(param as GameCardViewModel ?? DetailsGame));

    public RelayCommand VerifyFilesCommand =>
        _verifyFilesCommand ??= new RelayCommand(param => VerifyFiles(param as GameCardViewModel ?? DetailsGame));

    internal async Task RefreshStatusesAsync(IReadOnlyList<GameCardViewModel>? only = null)
    {
        var probe = StatusProbe;
        if (probe == null) return;

        var cards = (only ?? Games.ToList()).ToList();
        var token = ++_statusRefreshToken;
        _lastStatusRefresh = DateTime.UtcNow;

        Dictionary<GameCardViewModel, GameStatus> results;
        try
        {
            results = await Task.Run(() =>
            {
                var r = new Dictionary<GameCardViewModel, GameStatus>();
                foreach (var card in cards)
                {
                    try { r[card] = probe.Probe(card.Game); }
                    catch (Exception ex) { Log.Error($"status probe failed for {card.Game.Name}", ex); }
                }
                return r;
            });
        }
        catch (Exception ex)
        {
            Log.Error("status refresh failed", ex);
            return;
        }

        // تحديث كامل أحدث بدأ بعدنا: نتائجه أصح
        if (only == null && token != _statusRefreshToken) return;

        foreach (var (card, status) in results)
        {
            card.Status = WithSession(card, status);
            if (card.HasUpdate) NotifyOnce("update", card.Game.Platform + "|" + card.Game.Id, Loc.Instance.Format("ntGameUpdate", card.Name));
        }
    }

    /// اللعبة التي يتتبعها Halawa الآن "تعمل" حتى لو لم يقل مشغّلها ذلك
    private GameStatus WithSession(GameCardViewModel card, GameStatus status) =>
        _session is { Tracker.State: Core.Sessions.PlaySessionState.Running } s
        && s.Platform == card.Game.Platform && s.Id == card.Game.Id
            ? status | GameStatus.Running
            : status;

    private void RefreshStatusesOnFocus()
    {
        if (StatusProbe == null || DateTime.UtcNow - _lastStatusRefresh < FocusStatusThrottle) return;
        _ = RefreshStatusesAsync();
    }

    private void RefreshStatusOf(string platform, string id)
    {
        var cards = Games.Where(c => c.Game.Platform == platform && c.Game.Id == id).ToList();
        if (cards.Count > 0) _ = RefreshStatusesAsync(cards);
    }

    private bool RunLauncherCommand(LauncherCommand? command, string failKey)
    {
        if (command == null) return false;
        try
        {
            var psi = new ProcessStartInfo(command.Target) { UseShellExecute = true };
            if (!string.IsNullOrEmpty(command.Arguments)) psi.Arguments = command.Arguments;
            Process.Start(psi)?.Dispose();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"launcher command failed: {command.Target}", ex);
            ShowStatus(Loc.Instance.Format(failKey, ex.Message));
            return false;
        }
    }

    private void OpenInLauncher(GameCardViewModel? card)
    {
        if (card == null) return;
        RunLauncherCommand(Launchers.OpenInLauncher(card.Game), "openLauncherFailed");
    }

    private void VerifyFiles(GameCardViewModel? card)
    {
        if (card == null) return;
        if (RunLauncherCommand(Launchers.Verify(card.Game), "openLauncherFailed"))
            ShowStatus(Loc.Instance.Format("verifyStarted", card.Name));
    }

    /// <summary>
    /// تشغيل لعبة لها تحديث معلّق: نسأل أولًا. Steam قد يفرض التحديث قبل التشغيل
    /// على أي حال، ولا يوجد أمر موثّق يبدأ التحديث من خارجه، فالزر يفتحها في Steam.
    /// </summary>
    private bool AskBeforePlayingWithUpdate(GameCardViewModel card)
    {
        if (!card.HasUpdate) return false;

        var l = Loc.Instance;
        var choices = new List<ConfirmChoice>();
        if (card.CanOpenInLauncher)
            choices.Add(new ConfirmChoice { Label = card.OpenInLauncherLabel, IsPrimary = true, Run = () => OpenInLauncher(card) });
        choices.Add(new ConfirmChoice { Label = l["playAnyway"], IsPrimary = choices.Count == 0, Run = () => LaunchGameNow(card) });
        choices.Add(new ConfirmChoice { Label = l["cancel"] });

        RequestConfirm(new ConfirmRequest
        {
            Title = l.Format("updatePendingTitle", card.Name),
            Message = l[card.Game.Platform == "Steam" ? "updatePendingSteam" : "updatePendingOther"],
            Choices = choices
        });
        return true;
    }
}
