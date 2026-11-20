using System;
using Avalonia.Controls;
using HalawaHub.App.BigPicture.Input;
using HalawaHub.App.ViewModels;
using HalawaHub.Core;

namespace HalawaHub.App.BigPicture;

/// <summary>
/// يفتح الشاشة الكبيرة ويغلقها. النافذة العادية تُخفى أثناءها وتعود بنفس حالتها؛
/// البرنامج لا يُغلق لأن النافذة العادية مخفية لا مغلقة.
/// </summary>
public sealed class BigPictureController : IDisposable
{
    private readonly MainWindow _main;
    private readonly MainViewModel _vm;
    private readonly Func<IGamepadSource?> _createPads;
    private IGamepadSource? _pads;
    private BigPictureWindow? _window;
    private WindowState _mainState;

    public BigPictureController(MainWindow main, MainViewModel vm, Func<IGamepadSource?>? createPads = null)
    {
        _main = main;
        _vm = vm;
        _createPads = createPads ?? CreateSdl;
    }

    public bool IsOpen => _window != null;
    public BigPictureWindow? Window => _window;

    private static IGamepadSource? CreateSdl()
    {
        try { return new SdlGamepadSource(); }
        catch (Exception ex) { Log.Error("gamepad source failed", ex); return null; }
    }

    public void Open()
    {
        if (_window != null) { _window.Activate(); return; }

        _pads ??= _createPads();
        var host = new BigPictureHost(_vm, () => Exit(), CloseApp);
        var vm = new BigPictureViewModel(host);
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
        _pads?.Dispose();
        _pads = null;
    }
}
