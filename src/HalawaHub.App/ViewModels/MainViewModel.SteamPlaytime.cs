using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using HalawaHub.App.Services;
using HalawaHub.Core;
using HalawaHub.Core.Library;

namespace HalawaHub.App.ViewModels;

/// حساب Steam في الإعدادات > المنصات
public sealed class SteamAccountChoice
{
    public required string SteamId64 { get; init; }
    public required string Name { get; init; }
    public required bool IsSelected { get; init; }
}

/// استيراد وقت اللعب من ملفات Steam المحلية: يُعرض الأكبر بين وقت Halawa ووقت Steam
public partial class MainViewModel
{
    /// null في الاختبارات: لا قراءة لملفات Steam الحقيقية
    internal System.Func<string?>? SteamPathProvider { get; set; }

    public ObservableCollection<SteamAccountChoice> SteamAccounts { get; } = new();
    public bool HasSteamAccounts => SteamAccounts.Count > 0;
    public bool HasSeveralSteamAccounts => SteamAccounts.Count > 1;

    public bool ImportSteamPlaytime
    {
        get => _config.ImportSteamPlaytime;
        set
        {
            if (_config.ImportSteamPlaytime == value) return;
            _config.ImportSteamPlaytime = value;
            SaveConfig();
            OnPropertyChanged(nameof(ImportSteamPlaytime));
            _ = RefreshSteamPlaytimeAsync();
        }
    }

    private RelayCommand? _selectSteamAccountCommand;
    public RelayCommand SelectSteamAccountCommand => _selectSteamAccountCommand ??= new RelayCommand(p =>
    {
        if (p is not string id || _config.SteamAccountId == id) return;
        _config.SteamAccountId = id;
        SaveConfig();
        _ = RefreshSteamPlaytimeAsync();
    });

    internal async Task RefreshSteamPlaytimeAsync()
    {
        var pathProvider = SteamPathProvider;
        if (pathProvider == null) return;
        var enabled = _config.ImportSteamPlaytime;
        var chosen = _config.SteamAccountId;

        try
        {
            var (accounts, picked, times) = await Task.Run(() =>
            {
                var steam = pathProvider();
                if (string.IsNullOrEmpty(steam))
                    return (new List<SteamAccount>(), (SteamAccount?)null, new Dictionary<string, long>());
                var list = SteamPlaytimeImport.ReadAccounts(steam);
                var pick = SteamPlaytimeImport.PickAccount(list, chosen);
                var map = new Dictionary<string, long>();
                if (enabled && pick != null)
                    foreach (var (appId, v) in SteamPlaytimeImport.ReadPlaytimes(steam, pick))
                        map["Steam|" + appId] = v.Seconds;
                return (list, pick, map);
            });

            PlayTimeService.SetExternal(times);
            SteamAccounts.Clear();
            foreach (var a in accounts)
                SteamAccounts.Add(new SteamAccountChoice { SteamId64 = a.SteamId64, Name = a.PersonaName, IsSelected = a.SteamId64 == picked?.SteamId64 });
            OnPropertyChanged(nameof(HasSteamAccounts));
            OnPropertyChanged(nameof(HasSeveralSteamAccounts));

            foreach (var card in Games.Where(c => c.Game.Platform == "Steam")) card.RefreshPlayTime();
            if (SortMode == "playtime") ApplyFilter();
            UpdateHomeViewCollections();
        }
        catch (System.Exception ex)
        {
            Log.Error("Steam play time import failed", ex);
        }
    }
}
