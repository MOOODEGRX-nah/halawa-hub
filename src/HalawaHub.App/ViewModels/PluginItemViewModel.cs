using System;
using System.ComponentModel;
using HalawaHub.App.Services;
using HalawaHub.Core.Plugins;

namespace HalawaHub.App.ViewModels;

/// سطر إضافة في الإعدادات: الاسم، المسار، البصمة، ومفتاح تشغيل/إيقاف
public sealed class PluginItemViewModel : INotifyPropertyChanged
{
    private readonly PluginEntry _entry;
    private readonly Action<PluginEntry, bool> _setEnabled;

    public PluginItemViewModel(PluginEntry entry, Action<PluginEntry, bool> setEnabled)
    {
        _entry = entry;
        _setEnabled = setEnabled;
    }

    internal PluginEntry Entry => _entry;
    public string FileName => _entry.FileName;
    public string Path => _entry.Path;
    public string HashText => "SHA256 " + _entry.Sha256;

    public string StateText
    {
        get
        {
            var l = Loc.Instance;
            var state = _entry.State switch
            {
                PluginState.Enabled => l["pluginOn"],
                PluginState.Disabled => l["pluginOff"],
                PluginState.NeedsApproval => l[_entry.Changed ? "pluginChangedWaiting" : "pluginWaiting"],
                _ => l.Format("pluginFailed", _entry.Error ?? "")
            };
            return _entry.Bundled ? state + " · " + l["pluginBundled"] : state;
        }
    }

    public bool IsEnabled
    {
        get => _entry.State == PluginState.Enabled;
        set
        {
            if (value == IsEnabled) return;
            _setEnabled(_entry, value);
            Refresh();
        }
    }

    public void Refresh()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StateText)));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
