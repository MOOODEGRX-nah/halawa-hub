using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using HalawaHub.Core;
using SDL;
using static SDL.SDL3;

namespace HalawaHub.App.BigPicture.Input;

/// <summary>مصدر يد التحكم. الأحداث تأتي من خيط خلفي؛ المستهلك ينقلها لخيط الواجهة.</summary>
public interface IGamepadSource : IDisposable
{
    event Action<BpButton>? ButtonPressed;
    /// تغيّرت اليد الموصولة (null = لا يد)
    event Action<PadInfo?>? PadChanged;
    PadInfo? Current { get; }
    /// true أثناء فتح الشاشة الكبيرة: قراءة سريعة. false: نراقب التوصيل فقط، ببطء
    void SetActive(bool active);
}

/// <summary>
/// يد التحكم عبر SDL3 على خيط واحد خاص: التهيئة والقراءة والإغلاق كلها عليه.
/// لا نستعمل SDL_WaitEventTimeout لأنه بلا نظام عرض يدور كل مللي ثانية؛
/// نقرأ كل 16 مللي ثانية والشاشة الكبيرة مفتوحة ويد موصولة، وكل ربع ثانية بلا يد،
/// وكل ثانية والشاشة الكبيرة مغلقة (نراقب التوصيل فقط).
/// أي فشل في تحميل المكتبة يترك البرنامج يعمل بلوحة المفاتيح فقط.
/// </summary>
public sealed unsafe class SdlGamepadSource : IGamepadSource
{
    private const int ActivePollMs = 16;
    private const int NoPadPollMs = 250;
    private const int IdlePollMs = 1000;
    private const long BatteryEveryMs = 30_000;

    private readonly Thread _thread;
    private readonly AutoResetEvent _wake = new(false);
    private readonly DirectionRepeater _repeat = new();
    private readonly Dictionary<SDL_JoystickID, IntPtr> _open = new();
    private volatile bool _stop;
    private volatile bool _active;
    private SDL_JoystickID _currentId;
    private long _batteryAt;

    public event Action<BpButton>? ButtonPressed;
    public event Action<PadInfo?>? PadChanged;
    public PadInfo? Current { get; private set; }

    public SdlGamepadSource()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "Halawa gamepad" };
        _thread.Start();
    }

    public void SetActive(bool active)
    {
        _active = active;
        if (!active) _repeat.Reset();
        _wake.Set();
    }

    private void Run()
    {
        try
        {
            SDL_SetHint(SDL_HINT_JOYSTICK_ALLOW_BACKGROUND_EVENTS, "1");
            if (!SDL_Init(SDL_InitFlags.SDL_INIT_GAMEPAD))
            {
                Log.Error("SDL gamepad init failed: " + SDL_GetError());
                return;
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException or TypeInitializationException)
        {
            Log.Error("SDL3 could not be loaded; Big Picture uses the keyboard only", ex);
            return;
        }

        Log.Info("SDL gamepad ready");
        var clock = Stopwatch.StartNew();
        try
        {
            while (!_stop)
            {
                var now = clock.ElapsedMilliseconds;
                SDL_Event e;
                while (SDL_PollEvent(&e)) Handle(e, now);

                if (_active)
                {
                    foreach (var d in _repeat.Tick(now)) Raise(d);
                    if (now >= _batteryAt) { _batteryAt = now + BatteryEveryMs; RefreshBattery(); }
                }

                _wake.WaitOne(!_active ? IdlePollMs : _open.Count > 0 ? ActivePollMs : NoPadPollMs);
            }
        }
        catch (Exception ex)
        {
            Log.Error("gamepad thread stopped", ex);
        }
        finally
        {
            foreach (var pad in _open.Values) SDL_CloseGamepad((SDL_Gamepad*)pad);
            _open.Clear();
            SDL_Quit();
        }
    }

    private void Handle(in SDL_Event e, long now)
    {
        switch ((SDL_EventType)e.type)
        {
            case SDL_EventType.SDL_EVENT_GAMEPAD_ADDED:
                Open(e.gdevice.which);
                break;
            case SDL_EventType.SDL_EVENT_GAMEPAD_REMOVED:
                Close(e.gdevice.which);
                break;
            case SDL_EventType.SDL_EVENT_GAMEPAD_BUTTON_DOWN:
                if (!_active) break;
                MakeCurrent(e.gbutton.which);
                var down = MapButton((SDL_GamepadButton)e.gbutton.button);
                if (down is BpButton.Up or BpButton.Down or BpButton.Left or BpButton.Right)
                    foreach (var d in _repeat.DpadDown(down.Value, now)) Raise(d);
                else if (down is { } b) Raise(b);
                break;
            case SDL_EventType.SDL_EVENT_GAMEPAD_BUTTON_UP:
                var up = MapButton((SDL_GamepadButton)e.gbutton.button);
                if (up is BpButton.Up or BpButton.Down or BpButton.Left or BpButton.Right)
                    _repeat.DpadUp(up.Value, now);
                break;
            case SDL_EventType.SDL_EVENT_GAMEPAD_AXIS_MOTION:
                if (!_active) break;
                var axis = (SDL_GamepadAxis)e.gaxis.axis;
                if (axis is not (SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTX or SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTY)) break;
                if (!_open.TryGetValue(e.gaxis.which, out var p)) break;
                var pad = (SDL_Gamepad*)p;
                var x = SDL_GetGamepadAxis(pad, SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTX);
                var y = SDL_GetGamepadAxis(pad, SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTY);
                foreach (var d in _repeat.Stick(x, y, now)) Raise(d);
                break;
        }
    }

    private void Open(SDL_JoystickID id)
    {
        if (_open.ContainsKey(id)) return;
        var pad = SDL_OpenGamepad(id);
        if (pad == null)
        {
            Log.Error("could not open gamepad: " + SDL_GetError());
            return;
        }
        _open[id] = (IntPtr)pad;
        MakeCurrent(id, force: true);
        Log.Info($"gamepad connected: {Current?.Name} ({Current?.Kind}, battery {Current?.Battery})");
    }

    private void Close(SDL_JoystickID id)
    {
        if (!_open.Remove(id, out var pad)) return;
        SDL_CloseGamepad((SDL_Gamepad*)pad);
        Log.Info("gamepad disconnected");
        if (_currentId != id) return;

        _repeat.Reset();
        using var next = _open.Keys.GetEnumerator();
        if (next.MoveNext()) MakeCurrent(next.Current, force: true);
        else
        {
            _currentId = default;
            Current = null;
            PadChanged?.Invoke(null);
        }
    }

    private void MakeCurrent(SDL_JoystickID id, bool force = false)
    {
        if (!force && id == _currentId) return;
        if (!_open.TryGetValue(id, out var p)) return;
        _currentId = id;
        Current = Describe((SDL_Gamepad*)p);
        _batteryAt = 0;
        PadChanged?.Invoke(Current);
    }

    private void RefreshBattery()
    {
        if (!_open.TryGetValue(_currentId, out var p) || Current == null) return;
        var battery = ReadBattery((SDL_Gamepad*)p);
        if (battery == Current.Battery) return;
        Current = Current with { Battery = battery };
        PadChanged?.Invoke(Current);
    }

    private static PadInfo Describe(SDL_Gamepad* pad) =>
        new(SDL_GetGamepadName(pad) ?? "", KindOf(SDL_GetGamepadType(pad)), ReadBattery(pad));

    private static int ReadBattery(SDL_Gamepad* pad)
    {
        int percent;
        var state = SDL_GetGamepadPowerInfo(pad, &percent);
        return state is SDL_PowerState.SDL_POWERSTATE_ON_BATTERY or SDL_PowerState.SDL_POWERSTATE_CHARGING or SDL_PowerState.SDL_POWERSTATE_CHARGED
            ? Math.Clamp(percent, -1, 100)
            : -1;
    }

    internal static PadKind KindOf(SDL_GamepadType type) => type switch
    {
        SDL_GamepadType.SDL_GAMEPAD_TYPE_XBOX360 or SDL_GamepadType.SDL_GAMEPAD_TYPE_XBOXONE => PadKind.Xbox,
        SDL_GamepadType.SDL_GAMEPAD_TYPE_PS3 or SDL_GamepadType.SDL_GAMEPAD_TYPE_PS4 or SDL_GamepadType.SDL_GAMEPAD_TYPE_PS5 => PadKind.PlayStation,
        SDL_GamepadType.SDL_GAMEPAD_TYPE_NINTENDO_SWITCH_PRO or SDL_GamepadType.SDL_GAMEPAD_TYPE_NINTENDO_SWITCH_JOYCON_LEFT
            or SDL_GamepadType.SDL_GAMEPAD_TYPE_NINTENDO_SWITCH_JOYCON_RIGHT or SDL_GamepadType.SDL_GAMEPAD_TYPE_NINTENDO_SWITCH_JOYCON_PAIR
            or SDL_GamepadType.SDL_GAMEPAD_TYPE_GAMECUBE => PadKind.Nintendo,
        _ => PadKind.Generic,
    };

    /// الأزرار بمواقعها: السفلي A، الأيمن B، الأيسر X، العلوي Y
    internal static BpButton? MapButton(SDL_GamepadButton button) => button switch
    {
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_SOUTH => BpButton.A,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_EAST => BpButton.B,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_WEST => BpButton.X,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_NORTH => BpButton.Y,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_SHOULDER => BpButton.LB,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_SHOULDER => BpButton.RB,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_START or SDL_GamepadButton.SDL_GAMEPAD_BUTTON_GUIDE => BpButton.Menu,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_UP => BpButton.Up,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_DOWN => BpButton.Down,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_LEFT => BpButton.Left,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_RIGHT => BpButton.Right,
        _ => null,
    };

    /// HALAWAHUB_BP_INPUTLOG=1: كل زر يُكتب في السجل (لتجربة يد جديدة)
    private static readonly bool LogInput = Environment.GetEnvironmentVariable("HALAWAHUB_BP_INPUTLOG") == "1";

    private void Raise(BpButton button)
    {
        if (LogInput) Log.Info("gamepad button: " + button);
        try { ButtonPressed?.Invoke(button); }
        catch (Exception ex) { Log.Error("gamepad handler failed", ex); }
    }

    public void Dispose()
    {
        _stop = true;
        _wake.Set();
        if (Thread.CurrentThread != _thread) _thread.Join(TimeSpan.FromSeconds(2));
        _wake.Dispose();
    }
}
