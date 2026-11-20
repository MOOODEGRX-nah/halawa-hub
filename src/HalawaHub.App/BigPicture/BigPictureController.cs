using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Threading;
using HalawaHub.App.BigPicture.Input;
using HalawaHub.App.ViewModels;
using HalawaHub.Core;

namespace HalawaHub.App.BigPicture;

/// <summary>
/// يفتح الشاشة الكبيرة ويغلقها. النافذة العادية تُخفى أثناءها وتعود بنفس حالتها؛
/// البرنامج لا يُغلق لأن النافذة العادية مخفية لا مغلقة. يراقب يد التحكم في الخلفية
/// (قراءة كل ثانية) فقط إن كان "افتح عند توصيل يد تحكم" مفعّلًا.
/// </summary>
public sealed class BigPictureController : IDisposable
{
    /// الأيادي الموصولة أصلًا عند بدء المراقبة تصل كأحداث توصيل: لا تفتح الشاشة الكبيرة
    internal static readonly TimeSpan StartupGrace = TimeSpan.FromSeconds(3);
    /// "يعود للنافذة العادية بعد ثوانٍ": مهلة لإعادة التوصيل (بطارية، كابل)
    internal static readonly TimeSpan DisconnectGrace = TimeSpan.FromSeconds(4);

    private readonly MainWindow _main;
    private readonly MainViewModel _vm;
    private readonly Func<IGamepadSource?> _createPads;
    private readonly Stopwatch _since = new();
    private IGamepadSource? _pads;
    private BigPictureWindow? _window;
    private WindowState _mainState;
    private PadInfo? _lastPad;
    /// للاختبارات: مهلة تجاهل الأيادي الموصولة عند البدء
    internal TimeSpan Grace { get; set; } = StartupGrace;
    private DispatcherTimer? _disconnectTimer;

    public BigPictureController(MainWindow main, MainViewModel vm, Func<IGamepadSource?>? createPads = null)
    {
        _main = main;
        _vm = vm;
        _createPads = createPads ?? CreateSdl;
        vm.BigPictureRequested += (_, _) => Open();
        vm.BigPictureSettingsChanged += (_, _) => EnsureWatching();
        // "صغّر Halawa عند بدء لعبة" يشمل الشاشة الكبيرة
        vm.MinimizeRequested += (_, _) => { if (_window != null) _window.WindowState = WindowState.Minimized; };
    }

    public bool IsOpen => _window != null;
    public BigPictureWindow? Window => _window;
    private BpSettings Settings => _vm.Config.BigPicture ??= new BpSettings();

    private static IGamepadSource? CreateSdl()
    {
        try { return new SdlGamepadSource(); }
        catch (Exception ex) { Log.Error("gamepad source failed", ex); return null; }
    }

    /// عند بدء البرنامج: الشاشة الكبيرة مباشرة إن طُلبت، ومراقبة اليد إن لزمت
    public void Start(bool forceBigPicture)
    {
        if (forceBigPicture || Settings.StartInBigPicture) Open();
        else EnsureWatching();
    }

    /// المراقبة تعمل فقط حين يحتاجها إعداد (أو والشاشة الكبيرة مفتوحة)
    public void EnsureWatching(bool force = false)
    {
        if (_pads != null || !(force || Settings.OpenOnController || IsOpen)) return;
        _pads = _createPads();
        if (_pads == null) return;
        _since.Restart();
        _lastPad = _pads.Current;
        _pads.PadChanged += pad => Dispatcher.UIThread.Post(() => OnPadChanged(pad));
    }

    internal void OnPadChanged(PadInfo? pad)
    {
        var connected = _lastPad == null && pad != null;
        var disconnected = _lastPad != null && pad == null;
        _lastPad = pad;

        if (connected)
        {
            _disconnectTimer?.Stop();
            if (!IsOpen && Settings.OpenOnController && _since.Elapsed >= Grace)
            {
                Log.Info("controller connected: opening Big Picture");
                Open();
            }
        }
        else if (disconnected && IsOpen && Settings.ExitOnDisconnect)
        {
            _disconnectTimer?.Stop();
            _disconnectTimer = new DispatcherTimer { Interval = DisconnectGrace };
            _disconnectTimer.Tick += (_, _) =>
            {
                _disconnectTimer?.Stop();
                if (_lastPad == null && IsOpen)
                {
                    Log.Info("controller disconnected: leaving Big Picture");
                    Exit();
                }
            };
            _disconnectTimer.Start();
        }
    }

    public void Open()
    {
        if (_window != null) { _window.Activate(); return; }

        var host = new BigPictureHost(_vm, () => Exit(), CloseApp);
        var vm = new BigPictureViewModel(host);
        EnsureWatching(force: true);
        var window = new BigPictureWindow(vm, _pads);
        _window = window;
        window.Closed += (_, _) => { if (_window == window) Exit(); };

        _mainState = _main.WindowState == WindowState.Minimized ? WindowState.Normal : _main.WindowState;
        Log.Info("Big Picture opened");
        window.ShowOn(_main.IsVisible ? _main : null);
        _main.Hide();
    }

    /// العودة إلى النافذة العادية بنفس حالتها
    public void Exit()
    {
        var window = _window;
        if (window == null) return;
        _window = null;
        _disconnectTimer?.Stop();
        Log.Info("Big Picture closed");

        // "تُغلق الشاشة الكبيرة وتفتح نافذة Halawa العادية" حتى لو كانت في الصينية قبلها
        _main.Show();
        _main.WindowState = _mainState;
        _main.Activate();
        window.Close();
    }

    private void CloseApp()
    {
        var window = _window;
        _window = null;
        window?.Close();
        _main.ExitForReal();
    }

    public void Dispose()
    {
        _disconnectTimer?.Stop();
        _pads?.Dispose();
        _pads = null;
    }
}
