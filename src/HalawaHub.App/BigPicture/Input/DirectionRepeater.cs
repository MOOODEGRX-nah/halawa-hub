using System;
using System.Collections.Generic;

namespace HalawaHub.App.BigPicture.Input;

/// <summary>
/// الأسهم واتجاه العصا: ضغطة واحدة عند البدء، ثم تكرار بعد مهلة ما دام الاتجاه
/// مضغوطًا. لا يعرف شيئًا عن SDL: يأخذ الوقت بالمللي ثانية فيُختبر بلا جهاز.
/// </summary>
public sealed class DirectionRepeater
{
    /// العصا تُحسب اتجاهًا بعد نصف مداها، وتُترك تحت الثلث (حتى لا ترتجف عند الحد)
    public const short PressThreshold = 16000;
    public const short ReleaseThreshold = 10000;

    public int DelayMs { get; init; } = 380;
    public int RateMs { get; init; } = 95;

    private BpButton? _dpad;
    private BpButton? _stick;
    private BpButton? _held;
    private long _nextAt;
    private short _x, _y;

    /// ضغط زر اتجاه في لوحة الاتجاهات
    public IEnumerable<BpButton> DpadDown(BpButton dir, long now)
    {
        _dpad = dir;
        return Update(now);
    }

    public IEnumerable<BpButton> DpadUp(BpButton dir, long now)
    {
        if (_dpad == dir) _dpad = null;
        return Update(now);
    }

    /// حركة العصا اليسرى (قيم SDL من -32768 إلى 32767، والأسفل موجب)
    public IEnumerable<BpButton> Stick(short x, short y, long now)
    {
        _x = x;
        _y = y;
        _stick = StickDirection(_stick, x, y);
        return Update(now);
    }

    /// يُستدعى دوريًا: يعيد التكرارات التي حان وقتها
    public IEnumerable<BpButton> Tick(long now)
    {
        if (_held is { } dir && now >= _nextAt)
        {
            _nextAt = now + RateMs;
            return new[] { dir };
        }
        return Array.Empty<BpButton>();
    }

    public bool IsHolding => _held != null;

    public void Reset()
    {
        _dpad = _stick = _held = null;
        _x = _y = 0;
    }

    private IEnumerable<BpButton> Update(long now)
    {
        var dir = _dpad ?? _stick;
        if (dir == _held) return Array.Empty<BpButton>();
        _held = dir;
        if (dir is not { } d) return Array.Empty<BpButton>();
        _nextAt = now + DelayMs;
        return new[] { d };
    }

    /// المحور الأقوى يحدد الاتجاه؛ الاتجاه الحالي يبقى حتى ينزل تحت حد الترك
    internal static BpButton? StickDirection(BpButton? current, short x, short y)
    {
        int ax = Math.Abs((int)x), ay = Math.Abs((int)y);

        if (current is { } cur)
        {
            var along = cur is BpButton.Left or BpButton.Right ? ax : ay;
            var sameSide = cur switch
            {
                BpButton.Left => x < 0,
                BpButton.Right => x > 0,
                BpButton.Up => y < 0,
                _ => y > 0,
            };
            if (sameSide && along >= ReleaseThreshold && along >= Math.Max(ax, ay) * 0.6) return cur;
        }

        if (Math.Max(ax, ay) < PressThreshold) return null;
        return ax >= ay
            ? (x < 0 ? BpButton.Left : BpButton.Right)
            : (y < 0 ? BpButton.Up : BpButton.Down);
    }
}
