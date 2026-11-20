using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HalawaHub.App.Services;
using HalawaHub.App.ViewModels;

namespace HalawaHub.App.BigPicture;

/// <summary>الشاشة الكبيرة فوق MainViewModel: نفس الألعاب والتشغيل والإشعارات والإعدادات.</summary>
public sealed class BigPictureHost : IBpHost
{
    private readonly MainViewModel _main;
    private readonly Action _exit;
    private readonly Action _closeApp;
    private List<BpGame>? _games;

    public BigPictureHost(MainViewModel main, Action exit, Action closeApp)
    {
        _main = main;
        _exit = exit;
        _closeApp = closeApp;
        main.Config.BigPicture ??= new BpSettings();
    }

    public BpSettings Settings => _main.Config.BigPicture;

    public IReadOnlyList<BpGame> Games => _games ??= _main.Games.Where(c => !c.IsHidden).Select(ToBp).ToList();

    /// تُعاد قراءة المكتبة بعد فحص جديد
    public void Invalidate() => _games = null;

    internal static BpGame ToBp(GameCardViewModel c) => new()
    {
        Key = c.Game.Platform + ":" + c.Game.Id,
        Name = c.Name,
        Platform = c.Game.Platform,
        PlatformShort = GameCardViewModel.ShortenPlatform(c.Game.Platform),
        Hours = c.PlayTimeTotal.TotalHours,
        LastPlayed = c.LastPlayed is { } lp ? (lp.Kind == DateTimeKind.Utc ? lp.ToLocalTime() : lp) : null,
        DateAdded = c.DateAdded,
        HasUpdate = c.HasUpdate,
        IsInstalled = c.IsInstalled,
        IsFavorite = c.IsFavorite,
        CoverUrl = c.CoverImageUrl,
        CoverFallbackUrl = c.CoverImageUrlFallback,
        SteamAppId = c.HasNews ? c.Game.Id : null,
        SizeLookup = () => { c.EnsureSizeRequested(); return c.SizeOnDiskText; },
        Source = c,
    };

    private static GameCardViewModel Card(BpGame g) => (GameCardViewModel)g.Source!;

    public void SaveSettings() => _main.SaveBigPictureSettings();

    public void SetLanguage(string language) => _main.Language = language;

    public void ToggleFavorite(BpGame game)
    {
        var card = Card(game);
        card.IsFavorite = !card.IsFavorite;
        game.IsFavorite = card.IsFavorite;
    }

    public async Task<BpLaunchResult> LaunchAsync(BpGame game, CancellationToken token)
    {
        var card = Card(game);
        var result = _main.LaunchFromBigPicture(card);
        if (!result.Ok) return result;

        // ننتظر أن تظهر اللعبة تعمل (يكتشفها متتبع الجلسة)، بحد أقصى 20 ثانية
        for (var i = 0; i < 40 && !card.IsRunning; i++)
            await Task.Delay(500, token);
        return result;
    }

    public void OpenUpdateInLauncher(BpGame game) => _main.OpenInLauncherFromBigPicture(Card(game));
    public void OpenLauncher(BpGame game) => _main.OpenInLauncherFromBigPicture(Card(game));

    public void OpenGameSettings(BpGame game)
    {
        // إعدادات اللعبة (الغلاف، معاملات التشغيل...) في نافذة اللعبة العادية
        _exit();
        _main.OpenDetailsCommand.Execute(Card(game));
    }

    public void OpenNews(BpGame game, int index) { /* المرحلة 2 */ }

    // ===== الإشعارات (المرحلة 4 تكمل الربط) =====

    public IReadOnlyList<BpNotification> Notifications
    {
        get
        {
            var unread = _main.UnreadNotifications;
            return _main.Notifications.Select((n, i) => new BpNotification
            {
                Id = n.At.Ticks + ":" + i,
                Kind = n.Category switch { "update" => "upd", "launch" => "fail", "scan" => "scan", "appupdate" => "app", _ => "free" },
                Title = n.Text,
                Time = n.TimeText,
                Unread = i < unread,
                ActionLabel = n.CanOpen ? Loc.Instance["bpNtShow"] : null,
                Action = n.Open,
                Source = n,
            }).ToList();
        }
    }

    public void DismissNotification(BpNotification item) => _main.DismissNotificationCommand.Execute(item.Source);
    public void ClearNotifications() => _main.ClearNotificationsCommand.Execute(null);
    public void MarkNotificationsRead() => _main.MarkNotificationsReadFromBigPicture();

    public void ExitBigPicture() => _exit();
    public void CloseApp() => _closeApp();
}
