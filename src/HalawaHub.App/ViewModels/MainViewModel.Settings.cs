using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using HalawaHub.App.Services;
using HalawaHub.Core;

namespace HalawaHub.App.ViewModels;

/// <summary>
/// إعدادات المرحلة 5: عام، المظهر، المكتبة، الأدوات. ترتيب أقسام الإعدادات يختلف عن
/// أرقامها (الأرقام ثابتة لأن الاختبارات والكود يشيران إليها)، وSettingsPillIndex يحوّل
/// رقم القسم إلى موضعه في الشريط الجانبي.
/// </summary>
public partial class MainViewModel
{
    public const int SectionUpdates = 0, SectionLanguage = 1, SectionApi = 2, SectionAppearance = 3, SectionGeneral = 4,
        SectionAbout = 5, SectionPlugins = 6, SectionLibrary = 7, SectionPlatforms = 8, SectionTools = 9;

    /// ترتيب الظهور في الشريط الجانبي
    internal static readonly int[] SettingsOrder =
    {
        SectionGeneral, SectionLibrary, SectionAppearance, SectionPlatforms, SectionLanguage,
        SectionApi, SectionPlugins, SectionUpdates, SectionTools, SectionAbout
    };

    public int SettingsPillIndex => Math.Max(0, Array.IndexOf(SettingsOrder, SettingsSection));
    public int SettingsSectionCount => SettingsOrder.Length;

    private void SaveConfig() => ConfigService.Save(_config);

    // ===== عام =====

    public bool CloseToTray
    {
        get => _config.CloseAction != "exit";
        set
        {
            if (value == CloseToTray) return;
            _config.CloseAction = value ? "tray" : "exit";
            SaveConfig();
            OnPropertyChanged(nameof(CloseToTray));
            OnPropertyChanged(nameof(CloseActionIndex));
        }
    }

    /// 0 = صغّر للصينية، 1 = أغلق البرنامج
    public int CloseActionIndex => CloseToTray ? 0 : 1;

    public bool StartMinimized
    {
        get => _config.StartMinimized;
        set { if (_config.StartMinimized == value) return; _config.StartMinimized = value; SaveConfig(); OnPropertyChanged(nameof(StartMinimized)); }
    }

    /// تبويب المكتبة عند الفتح
    public int StartTab
    {
        get => Math.Clamp(_config.StartTab, 0, LibraryTabs.Length - 1);
        set
        {
            var v = Math.Clamp(value, 0, LibraryTabs.Length - 1);
            if (_config.StartTab == v) return;
            _config.StartTab = v;
            SaveConfig();
            OnPropertyChanged(nameof(StartTab));
        }
    }

    public bool MinimizeOnGameStart
    {
        get => _config.MinimizeOnGameStart;
        set { if (_config.MinimizeOnGameStart == value) return; _config.MinimizeOnGameStart = value; SaveConfig(); OnPropertyChanged(nameof(MinimizeOnGameStart)); }
    }

    public bool CloseSteamAfterGame
    {
        get => _config.CloseSteamAfterGame;
        set { if (_config.CloseSteamAfterGame == value) return; _config.CloseSteamAfterGame = value; SaveConfig(); OnPropertyChanged(nameof(CloseSteamAfterGame)); }
    }

    /// النافذة تستمع لهذا لتصغّر نفسها عند بدء لعبة
    public event EventHandler? MinimizeRequested;

    // ===== المظهر =====

    /// 0 داكن، 1 أسود OLED، 2 فاتح
    public int ThemeIndex => Math.Max(0, Array.IndexOf(ThemeManager.Themes, ThemeManager.Normalize(_config.Theme)));

    public string AccentKey => ThemeManager.NormalizeAccent(_config.Accent);

    public void SetTheme(string theme)
    {
        theme = ThemeManager.Normalize(theme);
        if (_config.Theme == theme) return;
        _config.Theme = theme;
        SaveConfig();
        if (Avalonia.Application.Current is { } app) ThemeManager.Apply(app, _config.Theme, _config.Accent);
        OnPropertyChanged(nameof(ThemeIndex));
    }

    public void SetAccent(string accent)
    {
        accent = ThemeManager.NormalizeAccent(accent);
        if (_config.Accent == accent) return;
        _config.Accent = accent;
        SaveConfig();
        if (Avalonia.Application.Current is { } app) ThemeManager.Apply(app, _config.Theme, _config.Accent);
        OnPropertyChanged(nameof(AccentKey));
    }

    /// 0 لا (الحركة تعمل)، 1 نعم، 2 تلقائي (يتبع النظام)
    public int ReduceMotionIndex => _config.ReduceMotion switch { MotionPreferences.On => 1, MotionPreferences.Auto => 2, _ => 0 };

    public void SetReduceMotion(string mode)
    {
        if (_config.ReduceMotion == mode) return;
        _config.ReduceMotion = mode;
        SaveConfig();
        MotionPreferences.Apply(mode);
        OnPropertyChanged(nameof(ReduceMotionIndex));
        OnPropertyChanged(nameof(IsMotionReduced));
        OnPropertyChanged(nameof(ReduceMotionStateText));
    }

    public bool IsMotionReduced => !MotionSettings.Enabled;

    /// في وضع "تلقائي" نقول ماذا اختار النظام
    public string ReduceMotionStateText => _config.ReduceMotion == MotionPreferences.Auto
        ? Loc.Instance[IsMotionReduced ? "motionAutoReduced" : "motionAutoFull"]
        : "";

    public bool CoverHoverAnimation
    {
        get => _config.CoverHoverAnimation;
        set { if (_config.CoverHoverAnimation == value) return; _config.CoverHoverAnimation = value; SaveConfig(); OnPropertyChanged(nameof(CoverHoverAnimation)); }
    }

    // ===== المكتبة =====

    /// 0 شبكة، 1 قائمة
    public int LibraryLayoutIndex => _config.LibraryLayout == "list" ? 1 : 0;
    public bool IsListLayout => LibraryLayoutIndex == 1;
    public bool IsGridLayout => !IsListLayout;

    public void SetLibraryLayout(string layout)
    {
        layout = layout == "list" ? "list" : "grid";
        if (_config.LibraryLayout == layout) return;
        _config.LibraryLayout = layout;
        SaveConfig();
        OnPropertyChanged(nameof(LibraryLayoutIndex));
        OnPropertyChanged(nameof(IsListLayout));
        OnPropertyChanged(nameof(IsGridLayout));
        if (IsListLayout) RequestListSizes();
    }

    /// 0 صغير، 1 مريح، 2 كبير
    public int CardSizeIndex => _config.CardSize switch { "small" => 0, "large" => 2, _ => 1 };

    /// أقل عرض للبطاقة في AutoFillGrid
    public double CardMinWidth => CardSizeIndex switch { 0 => 140, 2 => 220, _ => 170 };

    public void SetCardSize(string size)
    {
        size = size is "small" or "large" ? size : "comfortable";
        if (_config.CardSize == size) return;
        _config.CardSize = size;
        SaveConfig();
        OnPropertyChanged(nameof(CardSizeIndex));
        OnPropertyChanged(nameof(CardMinWidth));
    }

    /// عمود الحجم في عرض القائمة: يُحسب في الخلفية لعبةً لعبة (بلا تجميد)
    private async void RequestListSizes()
    {
        foreach (var card in Games.ToList())
        {
            if (!IsListLayout) return;
            try { await card.EnsureSizeAsync(); }
            catch (Exception ex) { Log.Error($"size of {card.Name} failed", ex); }
        }
    }

    // ===== الأدوات =====

    private RelayCommand? _openLogFolderCommand, _openDataFolderCommand, _clearCoverCacheCommand, _deleteAllDataCommand;

    public RelayCommand OpenLogFolderCommand => _openLogFolderCommand ??= new RelayCommand(_ => OpenFolder(Log.LogDir));
    public RelayCommand OpenDataFolderCommand => _openDataFolderCommand ??= new RelayCommand(_ => OpenFolder(AppPaths.DataDirectory));
    public RelayCommand ClearCoverCacheCommand => _clearCoverCacheCommand ??= new RelayCommand(_ => ClearCoverCache());
    public RelayCommand DeleteAllDataCommand => _deleteAllDataCommand ??= new RelayCommand(_ => AskDeleteAllData());

    private void OpenFolder(string dir)
    {
        try
        {
            Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Error($"could not open {dir}", ex);
            ShowStatus(Loc.Instance.Format("openLocFailed", ex.Message));
        }
    }

    private void ClearCoverCache()
    {
        var (files, bytes) = CoverDiskCacheTools.Clear();
        ShowStatus(Loc.Instance.Format("coverCacheCleared", files, GameSizeService.Format(bytes)));
    }

    /// يُحذف عند الإقلاع التالي (قبل تحميل أي خدمة) حتى لا تعيد الخدمات المفتوحة كتابة الملفات
    private void AskDeleteAllData()
    {
        var l = Loc.Instance;
        RequestConfirm(new ConfirmRequest
        {
            Title = l["deleteAllTitle"],
            Message = l["deleteAllBody"],
            Details = new[] { l["deleteAllDeletes"], l["deleteAllKeeps"], AppPaths.DataDirectory },
            Choices = new[]
            {
                new ConfirmChoice { Label = l["deleteAllConfirm"], IsPrimary = true, Run = ScheduleDataWipe },
                new ConfirmChoice { Label = l["cancel"] }
            }
        });
    }

    /// عند الإغلاق بعد طلب المسح: لا حفظ لأي ملف بيانات
    internal static bool DataWipeScheduled { get; private set; }

    public event EventHandler? ExitRequested;

    private void ScheduleDataWipe()
    {
        try
        {
            DataWipe.Schedule();
            DataWipeScheduled = true;
            Log.Info("all user data will be deleted on the next start");
            ExitRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Log.Error("could not schedule the data wipe", ex);
            ShowStatus(Loc.Instance.Format("deleteAllFailed", ex.Message));
        }
    }

    private RelayCommand? _setSortModeCommand;
    public RelayCommand SetSortModeCommand => _setSortModeCommand ??= new RelayCommand(p => { if (p is string m) SetSortMode(m); });

    private RelayCommand? _settingChoiceCommand;

    /// أزرار الاختيار في الإعدادات: المعامل "نوع:قيمة" (theme:light، accent:blue، ...)
    public RelayCommand SettingChoiceCommand => _settingChoiceCommand ??= new RelayCommand(param =>
    {
        if (param is not string s || s.IndexOf(':') is var i && i <= 0) return;
        var (kind, value) = (s[..i], s[(i + 1)..]);
        switch (kind)
        {
            case "theme": SetTheme(value); break;
            case "accent": SetAccent(value); break;
            case "motion": SetReduceMotion(value); break;
            case "close": CloseToTray = value == "tray"; break;
            case "starttab": if (int.TryParse(value, out var t)) StartTab = t; break;
            case "layout": SetLibraryLayout(value); break;
            case "cardsize": SetCardSize(value); break;
        }
    });
}
