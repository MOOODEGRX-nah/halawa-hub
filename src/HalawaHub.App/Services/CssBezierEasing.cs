using System;
using Avalonia.Animation.Easings;

namespace HalawaHub.App.Services;

/// <summary>
/// cubic-bezier من CSS بالضبط. SplineEasing في Avalonia يقص قيمة Y عند 1، فيضيع
/// الارتداد النابضي في منحنيات مثل (.34,1.56,.64,1) التي يقوم عليها إحساس التصميم.
/// </summary>
public sealed class CssBezierEasing : Easing
{
    private readonly double _x1, _y1, _x2, _y2;

    public CssBezierEasing(double x1, double y1, double x2, double y2)
    {
        _x1 = x1;
        _y1 = y1;
        _x2 = x2;
        _y2 = y2;
    }

    public override double Ease(double progress)
    {
        if (progress <= 0) return 0;
        if (progress >= 1) return 1;

        return Bezier(_y1, _y2, SolveT(progress));
    }

    private static double Bezier(double p1, double p2, double t)
    {
        var u = 1 - t;
        return 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t;
    }

    private static double BezierDerivative(double p1, double p2, double t)
    {
        var u = 1 - t;
        return 3 * u * u * p1 + 6 * u * t * (p2 - p1) + 3 * t * t * (1 - p2);
    }

    /// يجد t الذي يعطي x المطلوب: Newton، ثم تنصيف إذا كان الميل صغيرًا
    private double SolveT(double x)
    {
        var t = x;
        for (var i = 0; i < 8; i++)
        {
            var error = Bezier(_x1, _x2, t) - x;
            if (Math.Abs(error) < 1e-5) return t;

            var slope = BezierDerivative(_x1, _x2, t);
            if (Math.Abs(slope) < 1e-6) break;
            t -= error / slope;
        }

        double low = 0, high = 1;
        t = x;
        for (var i = 0; i < 30; i++)
        {
            var value = Bezier(_x1, _x2, t);
            if (Math.Abs(value - x) < 1e-5) break;
            if (value < x) low = t;
            else high = t;
            t = (low + high) / 2;
        }
        return t;
    }
}

/// <summary>منحنيات التصميم، لتستخدم من XAML بـ {x:Static}.</summary>
public static class Springs
{
    /// .btn و .pl عند الضغط والرجوع
    public static Easing Press { get; } = new CssBezierEasing(0.3, 1.8, 0.5, 1);

    /// ظهور القوائم، ✓ و↻، وتحول زر القائمة
    public static Easing Pop { get; } = new CssBezierEasing(0.34, 1.56, 0.64, 1);

    /// مقبض مفتاح التشغيل، ودوران زر الإغلاق
    public static Easing Switch { get; } = new CssBezierEasing(0.34, 1.6, 0.64, 1);

    /// ميلان الغلاف في نافذة اللعبة
    public static Easing Tilt { get; } = new CssBezierEasing(0.34, 1.5, 0.64, 1);
}

/// <summary>الشفافية لا تتجاوز 0..1 حتى لو ارتد المنحنى: تجاوزها قد يظهر وميضًا عند الرسم.</summary>
public sealed class ClampedEasing : Easing
{
    private readonly Easing _inner;

    public ClampedEasing(Easing inner) => _inner = inner;

    public override double Ease(double progress) => Math.Clamp(_inner.Ease(progress), 0, 1);
}
