using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using HalawaHub.App.Services;
using HalawaHub.Core;
using HalawaHub.Core.Store;

namespace HalawaHub.App.ViewModels;

public sealed class NotificationItem : System.ComponentModel.INotifyPropertyChanged
{
    public required string Category { get; init; }
    /// يُحسب عند العرض، فالإشعار يتبع لغة الواجهة الحالية لا لغة لحظة إنشائه
    public required Func<string> Render { get; init; }
    public string Text => Render();
    public DateTime At { get; init; } = DateTime.Now;
    public string TimeText => At.ToString("HH:mm", CultureInfo.InvariantCulture);
    /// ما يفتحه الضغط على الإشعار (صفحة العروض مثلًا)، أو لا شيء
    public Action? Open { get; init; }
    /// اللعبة المعنية (تحديث، فشل تشغيل) إن وُجدت: الشاشة الكبيرة تعرض لها زر إجراء
    public GameCardViewModel? Game { get; init; }
    public bool CanOpen => Open != null;

    // ===== بطاقة الإشعار في النافذة العادية (.panel.nt في التصميم) =====

    /// upd / free / scan / fail / app: لون الأيقونة وشكلها
    public string Kind => Category switch
    {
        "update" => "upd",
        "free" => "free",
        "launch" => "fail",
        "appupdate" => "app",
        _ => "scan"
    };

    /// مسارات الأيقونات من NT_PATH في التصميم (شبكة 24×24)
    public Avalonia.Media.Geometry Icon => Avalonia.Media.Geometry.Parse(Kind switch
    {
        "upd" => "M12 4v12M6 11l6 6 6-6M5 20h14",
        "free" => "M20 12v9H4v-9M2 7h20v5H2zM12 21V7M12 7H8a2.5 2.5 0 0 1 0-5c3 0 4 5 4 5zM12 7h4a2.5 2.5 0 0 0 0-5c-3 0-4 5-4 5z",
        "fail" => "M12 8v5M12 16.5v.5M10.3 3.9L2.4 18a2 2 0 0 0 1.7 3h15.8a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0z",
        "app" => "M21 12a9 9 0 1 1-3-6.7L21 8M21 3v5h-5",
        _ => "M5 12.5l4.5 4.5L19 7.5"
    });

    /// العنوان والنص وزر الإجراء يحسبها MainViewModel.Describe بلغة الواجهة الحالية
    public string Title { get; internal set; } = "";
    public string Body { get; internal set; } = "";
    public bool HasBody => Body.Length > 0;
    public string RelativeTimeText { get; internal set; } = "";
    public string? ActionLabel { get; internal set; }
    public bool HasAction => ActionLabel != null && Act != null;
    internal Action? Act { get; set; }

    private bool _isUnread;
    /// غير مقروء: نقطة حمراء حتى تُغلق قائمة الإشعارات بعد رؤيته
    public bool IsUnread
    {
        get => _isUnread;
        internal set
        {
            if (_isUnread == value) return;
            _isUnread = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsUnread)));
        }
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}

public sealed class SubscriptionRow
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string CostText { get; init; }
    public required string RenewalText { get; init; }
}

public sealed class FreeGameItem
{
    public required string Title { get; init; }
    public required string Url { get; init; }
    public string? ImageUrl { get; init; }
    public required string EndsText { get; init; }
    public bool HasImage => !string.IsNullOrEmpty(ImageUrl);
}

/// المرحلة 6: مركز الإشعارات، معالج أول تشغيل، الاشتراكات، ومجانيات Epic
public partial class MainViewModel
{
    // ===== مركز الإشعارات (في الذاكرة لهذه الجلسة) =====

    private const int MaxNotifications = 50;
    private int _scanCount;
    private readonly HashSet<string> _notifiedOnce = new();

    public ObservableCollection<NotificationItem> Notifications { get; } = new();

    private int _unreadNotifications;
    public int UnreadNotifications
    {
        get => _unreadNotifications;
        private set
        {
            if (_unreadNotifications == value) return;
            _unreadNotifications = value;
            OnPropertyChanged(nameof(UnreadNotifications));
            OnPropertyChanged(nameof(HasUnreadNotifications));
        }
    }

    public bool HasUnreadNotifications => _unreadNotifications > 0;
    public bool HasNoNotifications => Notifications.Count == 0;

    private bool _isNotificationsOpen;
    public bool IsNotificationsOpen
    {
        get => _isNotificationsOpen;
        private set { if (_isNotificationsOpen == value) return; _isNotificationsOpen = value; OnPropertyChanged(nameof(IsNotificationsOpen)); }
    }

    private RelayCommand? _toggleNotificationsCommand, _dismissNotificationCommand, _clearNotificationsCommand, _openNotificationCommand, _runNotificationActionCommand;

    public RelayCommand OpenNotificationCommand => _openNotificationCommand ??= new RelayCommand(p =>
    {
        if (p is not NotificationItem { Open: { } open }) return;
        CloseNotifications();
        open();
    });

    /// زر الإجراء في بطاقة الإشعار: حدّث في Steam، عرض، حاول مجددًا، افتح التحديثات
    public RelayCommand RunNotificationActionCommand => _runNotificationActionCommand ??= new RelayCommand(p =>
    {
        if (p is not NotificationItem { Act: { } act }) return;
        CloseNotifications();
        act();
    });

    public RelayCommand ToggleNotificationsCommand => _toggleNotificationsCommand ??= new RelayCommand(_ =>
    {
        if (IsNotificationsOpen) { CloseNotifications(); return; }
        // الوقت النسبي ("قبل دقيقتين") يُحسب من جديد عند كل فتح
        RefreshNotificationTexts();
        IsNotificationsOpen = true;
        UnreadNotifications = 0;
    });

    public RelayCommand DismissNotificationCommand => _dismissNotificationCommand ??= new RelayCommand(p =>
    {
        if (p is NotificationItem n) Notifications.Remove(n);
        OnNotificationCountChanged();
    });

    public RelayCommand ClearNotificationsCommand => _clearNotificationsCommand ??= new RelayCommand(_ =>
    {
        Notifications.Clear();
        OnNotificationCountChanged();
    });

    /// عند الإغلاق تصير كل الإشعارات المعروضة مقروءة، فتختفي نقاطها في الفتح التالي
    public void CloseNotifications()
    {
        if (!IsNotificationsOpen) return;
        IsNotificationsOpen = false;
        foreach (var n in Notifications) n.IsUnread = false;
    }

    public int NotificationCount => Notifications.Count;

    private void OnNotificationCountChanged()
    {
        OnPropertyChanged(nameof(HasNoNotifications));
        OnPropertyChanged(nameof(NotificationCount));
    }

    /// عنوان الإشعار ونصه وزر إجرائه بلغة الواجهة الحالية (NT_ALL في Screen.dc.html)
    internal void Describe(NotificationItem n)
    {
        var L = Loc.Instance;
        var card = n.Game;
        n.RelativeTimeText = HalawaHub.App.BigPicture.BigPictureHost.RelativeTime(n.At, DateTime.Now);
        n.ActionLabel = null;
        n.Act = null;
        switch (n.Kind)
        {
            case "upd" when card != null:
                n.Title = L.Format("bpNtUpdT", card.Name);
                n.Body = L["bpNtUpdB"];
                n.ActionLabel = L.Format("updateInLauncher", HalawaHub.Core.Library.LauncherActions.LauncherName(card.Game.Platform));
                n.Act = () => OpenInLauncher(card);
                break;
            case "fail" when card != null:
                n.Title = L.Format("bpFailTitle", card.Name);
                n.Body = n.Text;
                n.ActionLabel = L["bpRetry"];
                n.Act = () => LaunchGame(card);
                break;
            case "free":
                n.Title = L["bpNtFreeT"];
                n.Body = FreeNotificationBody(n.Text);
                n.ActionLabel = n.Open != null ? L["bpNtShow"] : null;
                n.Act = n.Open;
                break;
            case "scan":
                n.Title = L["bpNtScanT"];
                n.Body = n.Text;
                n.ActionLabel = L["bpNtShow"];
                n.Act = () => { TopNavIndex = 0; SelectedNavItem = NavRecent; };
                break;
            case "app":
                n.Title = L["bpNtAppT"];
                n.Body = L["nAppB"];
                n.ActionLabel = L["nAppAct"];
                n.Act = () => { OpenSettings(); SettingsSection = SectionUpdates; };
                break;
            default:
                n.Title = n.Text;
                n.Body = "";
                n.ActionLabel = n.Open != null ? L["bpNtShow"] : null;
                n.Act = n.Open;
                break;
        }
    }

    /// نص المجانيات: أسماء الألعاب وموعد انتهائها كما في التصميم، أو النص الأصلي إن لم تُحمّل القائمة
    internal string FreeNotificationBody(string fallback)
    {
        var games = FreeGames;
        if (games.Count == 0) return fallback;
        var sep = Loc.Instance.IsRtl ? "، " : ", ";
        var ends = games[0].EndsText;
        if (!Loc.Instance.IsRtl && ends.Length > 0) ends = char.ToLowerInvariant(ends[0]) + ends[1..];
        return string.Join(sep, games.Select(g => g.Title)) + sep + ends + ".";
    }

    private bool CategoryEnabled(string category) => category switch
    {
        "update" => _config.NotifyGameUpdates,
        "launch" => _config.NotifyLaunchFailed,
        "scan" => _config.NotifyScan,
        "appupdate" => _config.NotifyAppUpdate,
        _ => true
    };

    /// الفئات: update (تحديث لعبة)، launch (فشل تشغيل)، scan (انتهى الفحص)، appupdate (تحديث Halawa)
    internal void Notify(string category, string text, GameCardViewModel? game = null) => Notify(category, () => text, game: game);

    /// نص بمفتاح ترجمة: يُعاد حسابه إن تغيّرت اللغة
    internal void NotifyKey(string category, string key, params object[] args) =>
        Notify(category, () => Loc.Instance.Format(key, args));

    internal void Notify(string category, Func<string> render, Action? open = null, GameCardViewModel? game = null)
    {
        if (!CategoryEnabled(category) || string.IsNullOrWhiteSpace(render())) return;
        var item = new NotificationItem { Category = category, Render = render, Open = open, Game = game, IsUnread = true };
        Describe(item);
        Notifications.Insert(0, item);
        while (Notifications.Count > MaxNotifications) Notifications.RemoveAt(Notifications.Count - 1);
        if (!IsNotificationsOpen) UnreadNotifications++;
        OnNotificationCountChanged();
    }

    /// مرة واحدة في الجلسة لكل مفتاح (لعبة لها تحديث تُفحص حالتها مرارًا)
    private void NotifyOnce(string category, string key, string locKey, GameCardViewModel? game, params object[] args)
    {
        if (_notifiedOnce.Add(category + ":" + key)) Notify(category, () => Loc.Instance.Format(locKey, args), game: game);
    }

    /// بعد تغيير اللغة: نعيد عرض الإشعارات بنصوص اللغة الجديدة
    private void RefreshNotificationTexts()
    {
        var items = Notifications.ToList();
        Notifications.Clear();
        foreach (var n in items) { Describe(n); Notifications.Add(n); }
    }

    public bool NotifyGameUpdates { get => _config.NotifyGameUpdates; set { _config.NotifyGameUpdates = value; SaveConfig(); OnPropertyChanged(nameof(NotifyGameUpdates)); } }
    public bool NotifyLaunchFailed { get => _config.NotifyLaunchFailed; set { _config.NotifyLaunchFailed = value; SaveConfig(); OnPropertyChanged(nameof(NotifyLaunchFailed)); } }
    public bool NotifyScan { get => _config.NotifyScan; set { _config.NotifyScan = value; SaveConfig(); OnPropertyChanged(nameof(NotifyScan)); } }
    public bool NotifyAppUpdate { get => _config.NotifyAppUpdate; set { _config.NotifyAppUpdate = value; SaveConfig(); OnPropertyChanged(nameof(NotifyAppUpdate)); } }

    // ===== معالج أول تشغيل: اللغة ← المنصات ← مفتاح SteamGridDB (اختياري) =====

    private bool _isWizardOpen;
    public bool IsWizardOpen
    {
        get => _isWizardOpen;
        private set { if (_isWizardOpen == value) return; _isWizardOpen = value; OnPropertyChanged(nameof(IsWizardOpen)); }
    }

    private int _wizardStep;
    public int WizardStep
    {
        get => _wizardStep;
        private set
        {
            _wizardStep = Math.Clamp(value, 0, 2);
            OnPropertyChanged(nameof(WizardStep));
            OnPropertyChanged(nameof(WizardNextLabel));
            OnPropertyChanged(nameof(WizardCanGoBack));
            OnPropertyChanged(nameof(WizardStepText));
            if (_wizardStep == 1) RefreshPlatformRows();
        }
    }

    public bool WizardCanGoBack => _wizardStep > 0;
    public string WizardNextLabel => Loc.Instance[_wizardStep == 2 ? "wizFinish" : "wizNext"];
    public string WizardStepText => $"{_wizardStep + 1} / 3";

    private RelayCommand? _wizardNextCommand, _wizardBackCommand, _wizardSkipCommand;
    public RelayCommand WizardNextCommand => _wizardNextCommand ??= new RelayCommand(_ => { if (_wizardStep == 2) FinishWizard(); else WizardStep++; });
    public RelayCommand WizardBackCommand => _wizardBackCommand ??= new RelayCommand(_ => WizardStep--);
    public RelayCommand WizardSkipCommand => _wizardSkipCommand ??= new RelayCommand(_ => FinishWizard());

    /// يظهر فقط لمن أُنشئ ملف إعداداته للتو (ShowOnboarding). المستخدم الحالي ملفه بلا هذا الحقل.
    internal void StartWizardIfFirstRun()
    {
        if (!_config.ShowOnboarding) return;
        WizardStep = 0;
        IsWizardOpen = true;
    }

    private void FinishWizard()
    {
        IsWizardOpen = false;
        if (!_config.ShowOnboarding) return;
        _config.ShowOnboarding = false;
        SaveConfig();
    }

    // ===== الاشتراكات (محلية فقط) =====

    internal string SubscriptionsPath { get; set; } = SubscriptionsService.DefaultPath;
    private List<Subscription> _subscriptions = new();

    public ObservableCollection<SubscriptionRow> Subscriptions { get; } = new();
    public bool HasNoSubscriptions => Subscriptions.Count == 0;
    public string SubscriptionsTotalText => Loc.Instance.Format("subTotal", SubscriptionsService.MonthlyTotal(_subscriptions).ToString("0.##", CultureInfo.InvariantCulture));

    private string _newSubName = "", _newSubCost = "", _newSubDate = "";
    private bool _newSubYearly;

    public string NewSubName { get => _newSubName; set { _newSubName = value ?? ""; OnPropertyChanged(nameof(NewSubName)); SubFormChanged(); } }
    public string NewSubCost { get => _newSubCost; set { _newSubCost = value ?? ""; OnPropertyChanged(nameof(NewSubCost)); SubFormChanged(); } }
    public string NewSubDate { get => _newSubDate; set { _newSubDate = value ?? ""; OnPropertyChanged(nameof(NewSubDate)); SubFormChanged(); } }
    public int NewSubCycleIndex => _newSubYearly ? 1 : 0;

    private RelayCommand? _addSubscriptionCommand, _removeSubscriptionCommand, _setSubCycleCommand;
    public RelayCommand AddSubscriptionCommand => _addSubscriptionCommand ??= new RelayCommand(_ => AddSubscription(), _ => TryBuildSubscription(out Subscription _));
    public RelayCommand RemoveSubscriptionCommand => _removeSubscriptionCommand ??= new RelayCommand(p =>
    {
        if (p is not SubscriptionRow row) return;
        _subscriptions.RemoveAll(s => s.Id == row.Id);
        SubscriptionsService.Save(SubscriptionsPath, _subscriptions);
        RebuildSubscriptionRows();
    });
    public RelayCommand SetSubCycleCommand => _setSubCycleCommand ??= new RelayCommand(p => { _newSubYearly = p as string == "yearly"; OnPropertyChanged(nameof(NewSubCycleIndex)); });

    private void SubFormChanged() => AddSubscriptionCommand.RaiseCanExecuteChanged();

    internal void LoadSubscriptions()
    {
        _subscriptions = SubscriptionsService.Load(SubscriptionsPath);
        RebuildSubscriptionRows();
    }

    private bool TryBuildSubscription(out Subscription sub)
    {
        sub = new Subscription();
        if (string.IsNullOrWhiteSpace(_newSubName)) return false;
        if (!decimal.TryParse(_newSubCost.Trim().Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var cost) || cost < 0) return false;
        if (!DateTime.TryParseExact(_newSubDate.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return false;
        sub = new Subscription { Name = _newSubName.Trim(), Cost = cost, Cycle = _newSubYearly ? "yearly" : "monthly", Renewal = date };
        return true;
    }

    private void AddSubscription()
    {
        if (!TryBuildSubscription(out var sub)) return;
        _subscriptions.Add(sub);
        SubscriptionsService.Save(SubscriptionsPath, _subscriptions);
        NewSubName = "";
        NewSubCost = "";
        NewSubDate = "";
        RebuildSubscriptionRows();
    }

    private void RebuildSubscriptionRows()
    {
        var l = Loc.Instance;
        var today = DateTime.Today;
        Subscriptions.Clear();
        foreach (var s in _subscriptions.OrderBy(s => SubscriptionsService.NextRenewal(s, today)))
        {
            var next = SubscriptionsService.NextRenewal(s, today);
            var days = (int)(next - today).TotalDays;
            Subscriptions.Add(new SubscriptionRow
            {
                Id = s.Id,
                Name = s.Name,
                CostText = s.Cost.ToString("0.##", CultureInfo.InvariantCulture) + " / " + l[s.Cycle == "yearly" ? "subYear" : "subMonth"],
                RenewalText = days == 0 ? l["subToday"] : l.Format("subRenews", next.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), days)
            });
        }
        OnPropertyChanged(nameof(HasNoSubscriptions));
        OnPropertyChanged(nameof(SubscriptionsTotalText));
    }

    // ===== مجانية هذا الأسبوع (Epic) =====

    private static readonly TimeSpan FreeGamesMaxAge = TimeSpan.FromHours(6);

    public ObservableCollection<FreeGameItem> FreeGames { get; } = new();

    /// صفحة العروض: المجانيات هنا بدل شريط فوق المكتبة
    public bool HasFreeGames => FreeGames.Count > 0;
    public bool HasNoDeals => !HasFreeGames && !HasSteamDeals && !HasOtherDeals;
    public string DealsEmptyText => Loc.Instance[_config.ShowFreeGames ? "dealsEmpty" : "dealsOff"];

    private void RaiseDeals()
    {
        OnPropertyChanged(nameof(HasFreeGames));
        OnPropertyChanged(nameof(HasNoDeals));
        OnPropertyChanged(nameof(HasSteamDeals));
        OnPropertyChanged(nameof(HasOtherDeals));
        OnPropertyChanged(nameof(DealsEmptyText));
    }

    /// إشعار واحد عند ظهور مجانيات لم يرها المستخدم من قبل، والضغط عليه يفتح صفحة العروض
    private void NotifyNewFreeGames()
    {
        var urls = FreeGames.Select(g => g.Url).ToList();
        var fresh = urls.Count(u => !_config.SeenFreeGames.Contains(u));
        if (fresh == 0) return;
        _config.SeenFreeGames = urls;
        SaveConfig();
        Notify("free", () => Loc.Instance.Format("ntFree", fresh), () => GoDealsCommand.Execute(null));
    }


    public bool ShowFreeGames
    {
        get => _config.ShowFreeGames;
        set
        {
            if (_config.ShowFreeGames == value) return;
            _config.ShowFreeGames = value;
            SaveConfig();
            OnPropertyChanged(nameof(ShowFreeGames));
            if (!value) FreeGames.Clear();
            RaiseDeals();
            if (value && FreeGames.Count == 0) _ = LoadFreeGamesAsync();
            _ = LoadStoreDealsAsync();
        }
    }

    /// للاختبارات: بدل الشبكة
    internal Func<Task<string?>>? FetchFreeGamesOverride { get; set; }

    internal async Task LoadFreeGamesAsync()
    {
        if (!_config.ShowFreeGames) return;
        try
        {
            var cachePath = AppPaths.InData("cache", "epic-free.json");
            string? json = null;
            if (FetchFreeGamesOverride == null && File.Exists(cachePath) && DateTime.UtcNow - File.GetLastWriteTimeUtc(cachePath) < FreeGamesMaxAge)
                json = await File.ReadAllTextAsync(cachePath);

            if (json == null)
            {
                json = await (FetchFreeGamesOverride ?? EpicFreeGames.FetchJsonAsync)();
                if (json != null && FetchFreeGamesOverride == null && EpicFreeGames.Parse(json, DateTime.UtcNow).Count > 0)
                {
                    try { AtomicFile.WriteAllText(cachePath, json); } catch (Exception ex) { Log.Error("free games cache save failed", ex); }
                }
            }

            var games = EpicFreeGames.Parse(json, DateTime.UtcNow);
            FreeGames.Clear();
            foreach (var g in games)
                FreeGames.Add(new FreeGameItem
                {
                    Title = g.Title, Url = g.Url, ImageUrl = g.ImageUrl,
                    EndsText = Loc.Instance.Format("freeUntil", g.EndsUtc.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                });
        }
        catch (Exception ex)
        {
            Log.Error("free games load failed", ex);
            FreeGames.Clear();
        }
        RaiseDeals();
        NotifyNewFreeGames();
    }
}
