using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HalawaHub.App.BigPicture;

public enum BpScreen { Home, Library, Search, Game, Settings }
public enum BpOverlay { None, Guide, Notifications, Update, Launch }
public enum BpLaunchState { Starting, Done, Failed }

/// <summary>لعبة كما تراها الشاشة الكبيرة. Source هي بطاقة المكتبة الأصلية (أو null في الاختبارات).</summary>
public sealed class BpGame
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required string Platform { get; init; }
    public string PlatformShort { get; init; } = "";
    public double Hours { get; init; }
    public DateTime? LastPlayed { get; init; }
    public DateTime DateAdded { get; init; }
    public bool HasUpdate { get; init; }
    public bool IsInstalled { get; init; } = true;
    public bool IsFavorite { get; set; }
    public string SizeText { get; init; } = "";
    public string? CoverUrl { get; init; }
    public string? CoverFallbackUrl { get; init; }
    public string? HeroUrl { get; init; }
    public string? SteamAppId { get; init; }
    /// الحجم على القرص يُحسب عند الحاجة (للعبة المعروضة فقط)
    public Func<string>? SizeLookup { get; init; }
    public object? Source { get; init; }

    public string Letter => Name.Trim().Length > 0 ? char.ToUpperInvariant(Name.Trim()[0]).ToString() : "?";
    public string ShownPlatform => string.IsNullOrEmpty(PlatformShort) ? Platform : PlatformShort;

    // النصوص تُقرأ باللغة الحالية؛ تغيير اللغة يعيد بناء العناصر
    public string HoursText => Services.Loc.Instance.Format("bpHours", FormatHours(Hours));
    public string HoursShortText => Services.Loc.Instance.Format("bpHoursShort", FormatHours(Hours));
    public string LastShortText => RelativeDay(LastPlayed, DateTime.Now);
    public string LastLongText => LastPlayed == null
        ? Services.Loc.Instance["bpNever"]
        : Services.Loc.Instance.Format("bpLastLong", LastShortText);
    public string SizeShown => SizeLookup?.Invoke() is { Length: > 0 } s ? s : string.IsNullOrEmpty(SizeText) ? "—" : SizeText;

    internal static string FormatHours(double hours) =>
        hours >= 10 || hours == 0 ? Math.Round(hours).ToString("0") : hours.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

    /// "أمس" و"قبل 3 أيام"... كما في التصميم
    internal static string RelativeDay(DateTime? when, DateTime now)
    {
        var L = Services.Loc.Instance;
        if (when is not { } w) return L["bpNever"];
        var days = (now.Date - w.Date).Days;
        return days switch
        {
            <= 0 => L["bpToday"],
            1 => L["bpYesterday"],
            < 7 => L.Format("bpDaysAgo", days),
            < 14 => L["bpLastWeek"],
            < 21 => L["bp2Weeks"],
            < 30 => L.Format("bpWeeksAgo", days / 7),
            < 60 => L["bpLastMonth"],
            < 90 => L["bp2Months"],
            < 365 => L.Format("bpMonthsAgo", days / 30),
            _ => L["bpLongAgo"],
        };
    }
}

/// <summary>إشعار في لوحة الشاشة الكبيرة.</summary>
public sealed class BpNotification
{
    public required string Id { get; init; }
    /// upd, free, scan, fail, app
    public required string Kind { get; init; }
    public required string Title { get; init; }
    public string Body { get; init; } = "";
    public string Time { get; init; } = "";
    public bool Unread { get; set; }
    /// نص زر الإجراء، أو null بلا زر
    public string? ActionLabel { get; init; }
    public Action? Action { get; init; }
    public object? Source { get; init; }
}

/// <summary>أساس العناصر القابلة للتركيز: IsFocused تتغير بلا إعادة بناء العنصر.</summary>
public abstract class BpItem : INotifyPropertyChanged
{
    private bool _focused;

    public required string FocusKey { get; init; }

    public bool IsFocused
    {
        get => _focused;
        set { if (_focused != value) { _focused = value; Raise(); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class BpCardItem : BpItem
{
    public required BpGame Game { get; init; }
    public int Index { get; init; }
    /// تأخير دخول البطاقة (متدرج)
    public TimeSpan Delay { get; init; }
}

public sealed class BpButtonItem : BpItem
{
    private string _label = "";
    private bool _on;

    public required string Id { get; init; }
    public string Label { get => _label; set { if (_label != value) { _label = value; Raise(); } } }
    /// للأزرار ذات الحالة (المفضلة) والأقسام المختارة
    public bool IsOn { get => _on; set { if (_on != value) { _on = value; Raise(); } } }
    public bool IsPrimary { get; init; }
    public bool IsDanger { get; init; }
    public string Badge { get; set; } = "";
}
