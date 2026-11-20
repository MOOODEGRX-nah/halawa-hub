using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;

namespace HalawaHub.App.BigPicture;

public sealed partial class BigPictureViewModel
{
    // ===== الإعدادات =====

    private static readonly string[] SectionIds = { "general", "controller", "display", "language", "exit" };
    private int _section;
    private bool _sectionForward = true;
    private int _setCol;   // 0 الأقسام، 1 الأسطر
    private int _setRow;

    public ObservableCollection<BpButtonItem> SettingSections { get; } = new();
    public ObservableCollection<BpSettingRow> SettingRows { get; } = new();
    public int SectionIndex => _section;
    public bool SectionForward => _sectionForward;
    public int SectionVersion { get; private set; }
    public string SectionTitle => SettingSections.Count > _section ? SettingSections[_section].Label : "";
    /// المؤشر الجانبي يخفت حين يكون التركيز على الأسطر
    public bool SectionPillDim => _screen == BpScreen.Settings && _overlay == BpOverlay.None && _setCol == 1;

    private void BuildSettings()
    {
        SettingSections.Clear();
        for (var i = 0; i < SectionIds.Length; i++)
            SettingSections.Add(new BpButtonItem { FocusKey = "sec." + i, Id = SectionIds[i], IsOn = i == _section, IsDanger = false });
        RefreshSectionLabels();
        BuildRows();
    }

    public void RefreshSectionLabels()
    {
        string[] keys = { "bpSecGeneral", "bpSecController", "bpSecDisplay", "bpSecLanguage", "bpSecExit" };
        for (var i = 0; i < keys.Length; i++) SettingSections[i].Label = T(keys[i]);
    }

    private BpSettingRow Toggle(string id, string label, string note, bool on, int index) => new()
    {
        FocusKey = "row." + index, Id = id, Kind = BpRowKind.Toggle, Label = T(label), Note = T(note), IsOn = on,
    };

    private BpSettingRow Seg(string id, string label, string note, string[] values, string[] labels, string current, int index) => new()
    {
        FocusKey = "row." + index, Id = id, Kind = BpRowKind.Segmented, Label = T(label), Note = T(note),
        OptionValues = values, Options = labels, Selected = Math.Max(0, Array.IndexOf(values, current)),
    };

    private void BuildRows()
    {
        SettingRows.Clear();
        var s = Settings;
        switch (SectionIds[_section])
        {
            case "general":
                SettingRows.Add(Toggle("open", "bpAutoBp", "bpAutoBpN", s.OpenOnController, 0));
                SettingRows.Add(Toggle("start", "bpStartBp", "bpStartBpN", s.StartInBigPicture, 1));
                SettingRows.Add(Toggle("exit", "bpExitDisc", "bpExitDiscN", s.ExitOnDisconnect, 2));
                break;
            case "controller":
                SettingRows.Add(Seg("glyphs", "bpGlyphs", "bpGlyphsN", new[] { "auto", "xbox", "ps" },
                    new[] { T("bpGAuto"), "Xbox", "PlayStation" }, s.Glyphs, 0));
                SettingRows.Add(Toggle("swap", "bpSwapAB", "bpSwapABN", s.SwapConfirmBack, 1));
                break;
            case "display":
                SettingRows.Add(Seg("text", "bpTextSize", "bpTextSizeN", new[] { "normal", "large" },
                    new[] { T("bpTNormal"), T("bpTLarge") }, s.TextSize, 0));
                SettingRows.Add(Toggle("clock", "bpShowClock", "bpShowClockN", s.ShowClock, 1));
                SettingRows.Add(new BpSettingRow { FocusKey = "info", Id = "motion", Kind = BpRowKind.Info, Label = T("bpMotionInfo") });
                break;
            case "language":
                SettingRows.Add(Seg("lang", "bpSecLanguage", "bpLangNote", new[] { "ar", "en" },
                    new[] { "عربي", "English" }, IsRtl ? "ar" : "en", 0));
                break;
            default:
                SettingRows.Add(new BpSettingRow { FocusKey = "row.0", Id = "exitbp", Kind = BpRowKind.Button, Label = T("bpExitRow"), Note = T("bpExitRowN") });
                break;
        }
        SectionVersion++;
    }

    private int ControlRowCount => SettingRows.Count(r => r.IsControl);

    private void PickSection(int index)
    {
        index = Math.Clamp(index, 0, SectionIds.Length - 1);
        if (index == _section) return;
        _sectionForward = index > _section;
        _section = index;
        for (var i = 0; i < SettingSections.Count; i++) SettingSections[i].IsOn = i == _section;
        _setRow = 0;
        BuildRows();
    }

    private void NavSettings(int dx, int dy)
    {
        if (_setCol == 0)
        {
            if (dx > 0) { _setCol = 1; _setRow = 0; return; }
            if (dy != 0) PickSection(_section + dy);
            return;
        }

        var row = _setRow < SettingRows.Count ? SettingRows[_setRow] : null;
        if (dx != 0 && row is { IsSegmented: true })
        {
            if (dx < 0 && row.Selected == 0) { _setCol = 0; return; }
            ChangeRow(row, dx);
            return;
        }
        if (dx < 0) { _setCol = 0; return; }
        if (dy != 0) _setRow = Math.Clamp(_setRow + dy, 0, Math.Max(0, ControlRowCount - 1));
    }

    private void ActSettings()
    {
        if (_setCol == 0) { _setCol = 1; _setRow = 0; return; }
        if (_setRow < SettingRows.Count) ChangeRow(SettingRows[_setRow], 1);
    }

    public void ClickSection(int index)
    {
        _setCol = 0;
        PickSection(index);
        SyncFocus();
    }

    public void ClickRow(int index)
    {
        if (index >= SettingRows.Count || !SettingRows[index].IsControl) return;
        _setCol = 1;
        _setRow = index;
        ChangeRow(SettingRows[index], 1);
        SyncFocus();
    }

    /// تبديل مفتاح، أو تحريك الاختيار في المجموعة (يدور)، أو الخروج
    private void ChangeRow(BpSettingRow row, int delta)
    {
        var s = Settings;
        switch (row.Kind)
        {
            case BpRowKind.Toggle:
                row.IsOn = !row.IsOn;
                switch (row.Id)
                {
                    case "open": s.OpenOnController = row.IsOn; break;
                    case "start": s.StartInBigPicture = row.IsOn; break;
                    case "exit": s.ExitOnDisconnect = row.IsOn; break;
                    case "swap": s.SwapConfirmBack = row.IsOn; break;
                    case "clock": s.ShowClock = row.IsOn; break;
                }
                _host.SaveSettings();
                break;
            case BpRowKind.Segmented:
                var n = row.OptionValues.Count;
                row.Selected = (row.Selected + (delta == 0 ? 1 : delta) + n) % n;
                var value = row.OptionValues[row.Selected];
                switch (row.Id)
                {
                    case "glyphs": s.Glyphs = value; _host.SaveSettings(); break;
                    case "text": s.TextSize = value; _host.SaveSettings(); break;
                    case "lang": _host.SetLanguage(value); OnLanguageChanged(); break;
                }
                break;
            case BpRowKind.Button:
                ShowToast(T("bpExiting"));
                _host.ExitBigPicture();
                break;
        }
    }

    /// تغيّرت اللغة: نعيد النصوص (العناصر نفسها تبقى)
    public void OnLanguageChanged()
    {
        RefreshHomeLabels();
        RefreshSectionLabels();
        RefreshGuideLabels();
        RefreshOverlayLabels();
        _kbLang = IsRtl ? "ar" : "en";
        BuildKeys();
        var row = _setRow;
        BuildRows();
        _setRow = Math.Min(row, Math.Max(0, ControlRowCount - 1));
        BuildLibraryKeepingFilter();
        SyncFocus();
    }

    private void BuildLibraryKeepingFilter()
    {
        var index = _libIndex;
        BuildLibrary();
        _libIndex = Math.Clamp(index, 0, Math.Max(0, LibraryCards.Count - 1));
    }

    // ===== القائمة الجانبية =====

    private static readonly string[] GuideIds = { "resume", "home", "library", "notifications", "settings", "exitbp", "close" };
    private int _guideIndex;
    private bool _guideForward = true;

    public ObservableCollection<BpButtonItem> GuideItems { get; } = new();
    public int GuideIndex => _guideIndex;
    public bool GuideForward => _guideForward;

    private void BuildGuide()
    {
        GuideItems.Clear();
        for (var i = 0; i < GuideIds.Length; i++)
            GuideItems.Add(new BpButtonItem { FocusKey = "guide." + i, Id = GuideIds[i], IsDanger = GuideIds[i] == "close" });
        RefreshGuideLabels();
    }

    private void RefreshGuideLabels()
    {
        string[] keys = { "bpResume", "bpHome", "bpLibrary", "bpNotifications", "bpSettings", "bpExitBp", "bpCloseApp" };
        for (var i = 0; i < keys.Length; i++) GuideItems[i].Label = T(keys[i]);
    }

    private void RefreshGuideBadge()
    {
        var unread = UnreadCount;
        GuideItems[3].Badge = unread > 0 ? unread.ToString() : "";
        for (var i = 0; i < GuideItems.Count; i++) GuideItems[i].IsOn = i == _guideIndex;
    }

    private void NavGuide(int dy)
    {
        var i = Math.Clamp(_guideIndex + dy, 0, GuideItems.Count - 1);
        if (i == _guideIndex) return;
        _guideForward = i > _guideIndex;
        _guideIndex = i;
        for (var k = 0; k < GuideItems.Count; k++) GuideItems[k].IsOn = k == _guideIndex;
    }

    private void RunGuide(int index)
    {
        switch (GuideIds[index])
        {
            case "resume": CloseOverlay(); break;
            case "home": GoTab(BpScreen.Home); break;
            case "library": GoTab(BpScreen.Library); break;
            case "notifications": OpenNotifications(); break;
            case "settings": OpenSettings(); break;
            case "exitbp": CloseOverlay(); ShowToast(T("bpExiting")); _host.ExitBigPicture(); break;
            case "close": CloseOverlay(); _host.CloseApp(); break;
        }
    }

    public void ClickGuide(int index)
    {
        _guideForward = index > _guideIndex;
        _guideIndex = index;
        RunGuide(index);
        SyncFocus();
    }

    // ===== نافذة التحديث قبل التشغيل =====

    private int _updIndex;
    public ObservableCollection<BpButtonItem> UpdateButtons { get; } = new()
    {
        new BpButtonItem { FocusKey = "upd.0", Id = "update", IsPrimary = true },
        new BpButtonItem { FocusKey = "upd.1", Id = "anyway" },
        new BpButtonItem { FocusKey = "upd.2", Id = "cancel" },
    };
    public string UpdateTitle => _hero == null ? "" : F("bpUpdTitle", _hero.Name);

    /// تشغيل: لعبة لها تحديث تسأل أولًا
    public void Play(BpGame game)
    {
        Hero = game;
        if (game.HasUpdate)
        {
            _overlay = BpOverlay.Update;
            _updIndex = 0;
            RefreshOverlayLabels();
            Changed();
            return;
        }
        StartLaunch(game);
    }

    private void RunUpdateButton(int index)
    {
        if (_hero is not { } g) return;
        switch (index)
        {
            case 0: CloseOverlay(); _host.OpenUpdateInLauncher(g); ShowToast(F("bpOpenedSteam", g.Name)); break;
            case 1: StartLaunch(g); break;
            default: CloseOverlay(); break;
        }
    }

    public void ClickUpdateButton(int index)
    {
        _updIndex = index;
        RunUpdateButton(index);
        SyncFocus();
    }

    // ===== لوحة التشغيل =====

    private CancellationTokenSource? _launchCts;
    private int _failIndex;
    private BpLaunchResult? _failure;

    public BpLaunchState LaunchState { get; private set; }
    public ObservableCollection<BpButtonItem> FailButtons { get; } = new()
    {
        new BpButtonItem { FocusKey = "fail.0", Id = "launcher", IsPrimary = true },
        new BpButtonItem { FocusKey = "fail.1", Id = "retry" },
        new BpButtonItem { FocusKey = "fail.2", Id = "back" },
    };

    public string LaunchTitle => _hero == null ? "" : LaunchState switch
    {
        BpLaunchState.Failed => F("bpFailTitle", _hero.Name),
        BpLaunchState.Done => F("bpLaunched", _hero.Name),
        _ => F("bpStarting", _hero.Name),
    };

    public string LaunchSub => _hero == null ? "" : LaunchState switch
    {
        BpLaunchState.Failed => _failure?.Reason ?? "",
        BpLaunchState.Done => F("bpViaPlat", PlatformOf(_hero)),
        _ => F("bpWaitPlat", PlatformOf(_hero)),
    };

    public string FailText => _failure?.Detail ?? "";
    public bool IsLaunchFailed => LaunchState == BpLaunchState.Failed;
    public bool IsLaunchDone => LaunchState == BpLaunchState.Done;
    /// الدوّار يعمل فقط واللوحة ظاهرة: شارة مخفية في حالة الانتظار تبقى تدور وتستهلك المعالج
    public HalawaHub.App.Controls.BadgeState LaunchBadge => _overlay != BpOverlay.Launch ? HalawaHub.App.Controls.BadgeState.None : LaunchState switch
    {
        BpLaunchState.Done => HalawaHub.App.Controls.BadgeState.Done,
        BpLaunchState.Failed => HalawaHub.App.Controls.BadgeState.Error,
        _ => HalawaHub.App.Controls.BadgeState.Pending,
    };

    private void RefreshOverlayLabels()
    {
        UpdateButtons[0].Label = T("bpUpdNow");
        UpdateButtons[1].Label = T("bpPlayAnyway");
        UpdateButtons[2].Label = T("bpCancel");
        FailButtons[0].Label = _hero == null ? T("bpOpenLauncher") : F("bpOpenLauncherOf", PlatformOf(_hero));
        FailButtons[1].Label = T("bpRetry");
        FailButtons[2].Label = T("bpBack");
    }

    private async void StartLaunch(BpGame game)
    {
        CancelLaunch();
        var cts = _launchCts = new CancellationTokenSource();
        Hero = game;
        _overlay = BpOverlay.Launch;
        LaunchState = BpLaunchState.Starting;
        _failure = null;
        RefreshOverlayLabels();
        SyncFocus();

        BpLaunchResult result;
        try { result = await _host.LaunchAsync(game, cts.Token); }
        catch (OperationCanceledException) { return; }
        catch (Exception ex) { result = new BpLaunchResult(false, ex.Message); }
        if (cts.IsCancellationRequested || _overlay != BpOverlay.Launch) return;

        if (!result.Ok)
        {
            _failure = result;
            LaunchState = BpLaunchState.Failed;
            _failIndex = result.CanOpenLauncher ? 0 : 1;
            FailButtons[0].Label = F("bpOpenLauncherOf", PlatformOf(game));
            SyncFocus();
            return;
        }

        LaunchState = BpLaunchState.Done;
        SyncFocus();
        await _delay(TimeSpan.FromMilliseconds(1100));
        if (cts.IsCancellationRequested || _overlay != BpOverlay.Launch) return;
        _overlay = BpOverlay.None;
        SyncFocus();
        ShowToast(F("bpLaunched", game.Name));
    }

    private void CancelLaunch()
    {
        _launchCts?.Cancel();
        _launchCts = null;
    }

    private void RunFailButton(int index)
    {
        if (_hero is not { } g) return;
        switch (index)
        {
            case 0: CloseOverlay(); _host.OpenLauncher(g); ShowToast(F("bpLauncherOpening", PlatformOf(g))); break;
            case 1: StartLaunch(g); break;
            default: CloseOverlay(); break;
        }
    }

    public void ClickFailButton(int index)
    {
        _failIndex = index;
        RunFailButton(index);
        SyncFocus();
    }

    // ===== الإشعارات =====

    private int _ntIndex;
    public ObservableCollection<BpNotifItem> NotifItems { get; } = new();
    public int UnreadCount => _host.Notifications.Count(n => n.Unread);
    public bool HasNotifications => NotifItems.Count > 0;

    public void OpenNotifications()
    {
        Incoming = null;
        CloseOverlayQuiet();
        _overlay = BpOverlay.Notifications;
        _ntIndex = 0;
        RebuildNotifications();
        SyncFocus();
        _ = MarkReadLaterAsync();
    }

    private async System.Threading.Tasks.Task MarkReadLaterAsync()
    {
        await _delay(TimeSpan.FromMilliseconds(1600));
        if (_overlay != BpOverlay.Notifications) return;
        _host.MarkNotificationsRead();
        foreach (var n in NotifItems) n.Unread = false;
        Changed();
    }

    public void RebuildNotifications()
    {
        NotifItems.Clear();
        var i = 0;
        foreach (var n in _host.Notifications)
            NotifItems.Add(new BpNotifItem { FocusKey = "nt." + i, Model = n, Index = i++, Unread = n.Unread, IsNew = n.Id == _newId });
        _ntIndex = Math.Clamp(_ntIndex, 0, Math.Max(0, NotifItems.Count - 1));
    }

    private void NavNotifications(int dy) =>
        _ntIndex = Math.Clamp(_ntIndex + dy, 0, Math.Max(0, NotifItems.Count - 1));

    private void RunNotificationAction()
    {
        if (_ntIndex >= NotifItems.Count) return;
        var n = NotifItems[_ntIndex].Model;
        switch (n.ActionId)
        {
            case "update" when n.Game != null:
                CloseOverlay();
                _host.OpenUpdateInLauncher(n.Game);
                ShowToast(F("bpOpenedSteam", n.Game.Name));
                break;
            case "retry" when n.Game != null:
                CloseOverlay();
                StartLaunch(n.Game);
                break;
            case "show":
                GoTab(BpScreen.Library);
                break;
            default:
                n.Action?.Invoke();
                break;
        }
    }

    // ===== إشعار يصل والشاشة مفتوحة =====

    private string? _newId;

    /// الإشعار القادم (نافذة صغيرة 5 ثوانٍ)، أو null
    public BpNotification? Incoming { get; private set; }
    public int IncomingVersion { get; private set; }
    /// يزيد مع كل وصول: الجرس يهتز
    public int BellVersion { get; private set; }

    private async void OnNotificationArrived(BpNotification n)
    {
        BellVersion++;
        _newId = n.Id;
        if (_overlay == BpOverlay.Notifications)
        {
            RebuildNotifications();
            Changed();
            return;
        }
        Incoming = n;
        var v = ++IncomingVersion;
        Changed();
        await _delay(TimeSpan.FromSeconds(5));
        if (v != IncomingVersion) return;
        Incoming = null;
        Changed();
    }

    /// الضغط على الإشعار القادم يفتح اللوحة
    public void OpenIncoming()
    {
        Incoming = null;
        IncomingVersion++;
        OpenNotifications();
    }

    private async void DismissFocusedNotification()
    {
        if (_ntIndex >= NotifItems.Count) return;
        var item = NotifItems[_ntIndex];
        if (item.Leaving) return;
        item.Leaving = true;
        await _delay(TimeSpan.FromMilliseconds(380));
        _host.DismissNotification(item.Model);
        RebuildNotifications();
        SyncFocus();
    }

    private async void ClearNotifications()
    {
        if (NotifItems.Count == 0) return;
        foreach (var n in NotifItems) n.Leaving = true;
        await _delay(TimeSpan.FromMilliseconds(380));
        _host.ClearNotifications();
        RebuildNotifications();
        SyncFocus();
    }

    public void ClickNotification(int index)
    {
        _ntIndex = Math.Clamp(index, 0, Math.Max(0, NotifItems.Count - 1));
        SyncFocus();
    }

    public void ClickNotificationAction(int index) { _ntIndex = index; RunNotificationAction(); SyncFocus(); }
    public void ClickNotificationDismiss(int index) { _ntIndex = index; DismissFocusedNotification(); SyncFocus(); }
    public void ClickClearNotifications() => ClearNotifications();
}
