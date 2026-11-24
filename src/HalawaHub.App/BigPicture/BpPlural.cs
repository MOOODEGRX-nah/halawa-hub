using System;
using HalawaHub.App.Services;

namespace HalawaHub.App.BigPicture;

/// <summary>
/// العدد والمعدود: العربية تغيّر الكلمة مع العدد (ساعة واحدة، ساعتان، 3 ساعات، 11 ساعة)،
/// والإنجليزية تفرّق بين المفرد والجمع. الكسور تأخذ المفرد في العربية (2.5 ساعة).
/// </summary>
public static class BpPlural
{
    /// one: للعدد 1، two: للعدد 2، few: من 3 إلى 10 ({0} مكان العدد)، many: 0 و11 فأكثر والكسور
    public static string Ar(double n, string one, string two, string few, string many)
    {
        var whole = Math.Abs(n - Math.Round(n)) < 0.0001;
        var shown = BpGame.FormatHours(n);
        if (!whole) return string.Format(many, shown);
        var i = (long)Math.Round(n);
        return i switch
        {
            1 => one,
            2 => two,
            >= 3 and <= 10 => string.Format(few, shown),
            _ => string.Format(many, shown),
        };
    }

    private static bool Rtl => Loc.Instance.IsRtl;

    public static string HoursPlayed(double h) => Rtl
        ? Ar(h, "ساعة لعب واحدة", "ساعتا لعب", "{0} ساعات لعب", "{0} ساعة لعب")
        : $"{BpGame.FormatHours(h)} h played";

    public static string HoursShort(double h) => Rtl
        ? Ar(h, "ساعة واحدة", "ساعتان", "{0} ساعات", "{0} ساعة")
        : $"{BpGame.FormatHours(h)} h";

    public static string Games(int n) => Rtl
        ? Ar(n, "لعبة واحدة", "لعبتان", "{0} ألعاب", "{0} لعبة")
        : n == 1 ? "1 game" : $"{n} games";

    /// «4 أيام متتالية» / «4 days in a row»
    public static string DaysInARow(int d) => Rtl
        ? Ar(d, "يوم واحد", "يومان متتاليان", "{0} أيام متتالية", "{0} يومًا متتاليًا")
        : d == 1 ? "1 day" : $"{d} days in a row";

    /// مدة جلسة: «ساعتان»، «45 دقيقة» / «2 h»، «45 min»
    public static string Duration(TimeSpan t) => t.TotalMinutes < 60
        ? (Rtl ? Ar(Math.Max(1, (int)t.TotalMinutes), "دقيقة", "دقيقتان", "{0} دقائق", "{0} دقيقة") : $"{Math.Max(1, (int)t.TotalMinutes)} min")
        : (Rtl ? HoursShort(Math.Round(t.TotalHours, t.TotalHours >= 10 ? 0 : 1)) : $"{Math.Round(t.TotalHours, t.TotalHours >= 10 ? 0 : 1)} h");

    public static string DaysAgo(int d) => Rtl
        ? Ar(d, "أمس", "قبل يومين", "قبل {0} أيام", "قبل {0} يومًا")
        : d == 1 ? "Yesterday" : $"{d} days ago";

    public static string WeeksAgo(int w) => Rtl
        ? Ar(w, "الأسبوع الماضي", "قبل أسبوعين", "قبل {0} أسابيع", "قبل {0} أسبوعًا")
        : w == 1 ? "Last week" : $"{w} weeks ago";

    public static string MonthsAgo(int m) => Rtl
        ? Ar(m, "الشهر الماضي", "قبل شهرين", "قبل {0} أشهر", "قبل {0} شهرًا")
        : m == 1 ? "Last month" : $"{m} months ago";

    public static string MinutesAgo(int m) => Rtl
        ? Ar(m, "قبل دقيقة", "قبل دقيقتين", "قبل {0} دقائق", "قبل {0} دقيقة")
        : $"{m} min ago";

    public static string HoursAgo(int h) => Rtl
        ? Ar(h, "قبل ساعة", "قبل ساعتين", "قبل {0} ساعات", "قبل {0} ساعة")
        : h == 1 ? "1 hour ago" : $"{h} hours ago";
}
