using Avalonia.Animation.Easings;
using HalawaHub.App.Services;

namespace HalawaHub.App.BigPicture;

/// <summary>منحنيات الشاشة الكبيرة، منسوخة من cubic-bezier في التصميم.</summary>
public static class BpSprings
{
    /// .fring: حلقة التركيز
    public static Easing Ring { get; } = new CssBezierEasing(0.3, 1.35, 0.5, 1);
    /// .ptrack: انزلاق الصفحات
    public static Easing Track { get; } = new CssBezierEasing(0.22, 1.18, 0.36, 1);
    /// .rt: إزاحة صف البطاقات، ودخول البطل
    public static Easing Glide { get; } = new CssBezierEasing(0.22, 1, 0.36, 1);
    /// .c: رفع البطاقة المركَّزة
    public static Easing Lift { get; } = new CssBezierEasing(0.34, 1.56, 0.64, 1);
    /// .cpl: زر التشغيل داخل البطاقة
    public static Easing Open { get; } = new CssBezierEasing(0.34, 1.4, 0.64, 1);
    /// .pl/.btn عند التركيز والضغط
    public static Easing Press { get; } = new CssBezierEasing(0.3, 1.8, 0.5, 1);
    /// .key: مفاتيح لوحة المفاتيح
    public static Easing Key { get; } = new CssBezierEasing(0.34, 1.56, 0.64, 1);
    /// .sw i: مقابض مفتاح التشغيل
    public static Easing Knob { get; } = new CssBezierEasing(0.34, 1.5, 0.5, 1);
    /// .segpill
    public static Easing Seg { get; } = new CssBezierEasing(0.3, 1.5, 0.5, 1);
}
