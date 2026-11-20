using Avalonia.Input;

namespace HalawaHub.App.BigPicture.Input;

/// <summary>أزرار الشاشة الكبيرة المنطقية: يد التحكم ولوحة المفاتيح تُترجم إليها.</summary>
public enum BpButton
{
    Up, Down, Left, Right,
    A, B, X, Y,
    LB, RB,
    Menu,
    Notifications,
}

/// <summary>نوع يد التحكم، لاختيار رسوم الأزرار.</summary>
public enum PadKind { Generic, Xbox, PlayStation, Nintendo }

/// <summary>يد التحكم الموصولة الآن. Battery من 0 إلى 100، أو -1 إن لم تُعرف (أو سلكية).</summary>
public sealed record PadInfo(string Name, PadKind Kind, int Battery);

/// <summary>لوحة المفاتيح بدل يد التحكم، كما في التصميم.</summary>
public static class KeyboardMap
{
    public static BpButton? Map(Key key) => key switch
    {
        Key.Up => BpButton.Up,
        Key.Down => BpButton.Down,
        Key.Left => BpButton.Left,
        Key.Right => BpButton.Right,
        Key.Enter or Key.Space => BpButton.A,
        Key.Escape or Key.Back => BpButton.B,
        Key.Q or Key.PageUp => BpButton.LB,
        Key.E or Key.PageDown => BpButton.RB,
        Key.X => BpButton.X,
        Key.Y => BpButton.Y,
        Key.G or Key.M or Key.Home => BpButton.Menu,
        Key.N => BpButton.Notifications,
        _ => null,
    };
}
