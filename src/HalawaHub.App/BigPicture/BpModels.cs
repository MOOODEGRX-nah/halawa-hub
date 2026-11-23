using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HalawaHub.App.BigPicture;

public enum BpScreen { Home, Library, Search, Game, Settings }
public enum BpOverlay { None, Guide, Notifications, Update, Launch, GameOptions }
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
    /// صورة عريضة صغيرة لنتائج البحث (header.jpg في Steam)
    public string? HeaderUrl { get; init; }
    /// الصورة العريضة في صفحة اللعبة
    public string? ShowcaseUrl => HeroUrl ?? HeaderUrl;
    public string? SteamAppId { get; init; }
    /// الحجم على القرص يُحسب عند الحاجة (للعبة المعروضة فقط)
    public Func<string>? SizeLookup { get; init; }
    public object? Source { get; init; }

    public string Letter => Name.Trim().Length > 0 ? char.ToUpperInvariant(Name.Trim()[0]).ToString() : "?";
    public string ShownPlatform => string.IsNullOrEmpty(PlatformShort) ? Platform : PlatformShort;

    // النصوص تُقرأ باللغة الحالية؛ تغيير اللغة يعيد بناء العناصر
    public string HoursText => BpPlural.HoursPlayed(Math.Round(Hours, Hours >= 10 || Hours == 0 ? 0 : 1));
    public string HoursShortText => BpPlural.HoursShort(Math.Round(Hours, Hours >= 10 || Hours == 0 ? 0 : 1));
    public string LastShortText => RelativeDay(LastPlayed, DateTime.Now);
    public string LastLongText => LastPlayed == null
        ? Services.Loc.Instance["bpNever"]
        : Services.Loc.Instance.Format("bpLastLong", Services.Loc.Instance.IsRtl ? LastShortText : LowerFirst(LastShortText));

    private static string LowerFirst(string s) => s.Length > 0 && char.IsUpper(s[0]) ? char.ToLowerInvariant(s[0]) + s[1..] : s;
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
            < 7 => BpPlural.DaysAgo(days),
            < 30 => BpPlural.WeeksAgo(days / 7),
            < 365 => BpPlural.MonthsAgo(Math.Max(1, days / 30)),
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
    /// update (حدّث من Steam)، retry (حاول مجددًا)، show (المكتبة)، open (يفتح في النافذة العادية)
    public string? ActionId { get; init; }
    public BpGame? Game { get; init; }
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
    public string Note { get; set; } = "";
    public string Badge { get; set; } = "";
}
