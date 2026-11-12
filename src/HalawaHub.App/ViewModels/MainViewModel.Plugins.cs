using System.Collections.ObjectModel;
using System.Linq;
using HalawaHub.App.Services;
using HalawaHub.Core;
using HalawaHub.Core.Plugins;

namespace HalawaHub.App.ViewModels;

/// الإضافات: موافقة أول مرة، وتشغيل/إيقاف من الإعدادات
public partial class MainViewModel
{
    public ObservableCollection<PluginItemViewModel> PluginItems { get; } = new();

    public bool HasPlugins => PluginItems.Count > 0;

    internal PluginLoader PluginLoaderForTests => _pluginLoader;

    private void BuildPluginItems()
    {
        PluginItems.Clear();
        foreach (var entry in _pluginLoader.Plugins)
            PluginItems.Add(new PluginItemViewModel(entry, SetPluginEnabled));
        OnPropertyChanged(nameof(HasPlugins));
    }

    /// يسأل عن كل إضافة جديدة أو تغيّر ملفها، واحدة تلو الأخرى
    internal void AskAboutPendingPlugins()
    {
        foreach (var entry in _pluginLoader.PendingApproval.ToList())
        {
            var l = Loc.Instance;
            RequestConfirm(new ConfirmRequest
            {
                Title = l[entry.Changed ? "pluginAskChangedTitle" : "pluginAskTitle"],
                Message = l.Format("pluginAskBody", entry.FileName),
                Details = new[] { entry.Path, "SHA256 " + entry.Sha256 },
                Choices = new[]
                {
                    new ConfirmChoice { Label = l["pluginAllow"], IsPrimary = true, Run = () => SetPluginEnabled(entry, true) },
                    new ConfirmChoice { Label = l["pluginDeny"], Run = () => SetPluginEnabled(entry, false) }
                }
            });
        }
    }

    private void SetPluginEnabled(PluginEntry entry, bool enabled)
    {
        if (enabled)
        {
            if (!_pluginLoader.Enable(entry))
                ShowStatus(Loc.Instance.Format("pluginLoadFailed", entry.FileName));
            else
                Log.Info($"plugin enabled by the user: {entry.FileName}");
        }
        else
        {
            _pluginLoader.Disable(entry);
            Log.Info($"plugin disabled by the user: {entry.FileName}");
        }

        foreach (var item in PluginItems.Where(i => ReferenceEquals(i.Entry, entry))) item.Refresh();

        // المزودات والأدوات تغيرت: نعيد بناء المكتبة لتظهر/تختفي ألعابها وأدواتها
        _ = RefreshLibraryAsync();
    }

    private void RefreshPluginTexts()
    {
        foreach (var item in PluginItems) item.Refresh();
    }
}
