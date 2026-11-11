using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HalawaHub.App.Services;
using HalawaHub.Core;
using HalawaHub.Core.Library;
using HalawaHub.Core.Models;
using HalawaHub.Core.Store;

namespace HalawaHub.App.ViewModels;

/// <summary>
/// 7.3 ألعاب Steam المملوكة غير المثبتة (اختياري، يحتاج مفتاح Steam Web API)، و
/// 7.4 دمج اللعبة نفسها من أكثر من منصة في بطاقة واحدة مع "شغّل من".
/// </summary>
public partial class MainViewModel
{
    private static readonly TimeSpan OwnedMaxAge = TimeSpan.FromHours(6);
    private List<GameInfo> _ownedSteam = new();
    private Debouncer? _steamWebKeyDebouncer;

    /// للاختبارات: بدل الشبكة
    internal Func<string, string, Task<string?>>? FetchOwnedOverride { get; set; }

    public string SteamWebApiKey
    {
        get => _config.SteamWebApiKey;
        set
        {
            var v = (value ?? "").Trim();
            if (_config.SteamWebApiKey == v) return;
            _config.SteamWebApiKey = v;
            OnPropertyChanged(nameof(SteamWebApiKey));
            (_steamWebKeyDebouncer ??= new Debouncer(TimeSpan.FromMilliseconds(900), ApplySteamWebKeyChange)).Schedule();
        }
    }

    private string _ownedStatus = "";
    /// نتيجة آخر جلب (عدد الألعاب أو سبب الفشل)
    public string OwnedStatusText
    {
        get => _ownedStatus;
        private set { _ownedStatus = value; OnPropertyChanged(nameof(OwnedStatusText)); OnPropertyChanged(nameof(HasOwnedStatus)); }
    }
    public bool HasOwnedStatus => _ownedStatus.Length > 0;

    private void ApplySteamWebKeyChange()
    {
        SaveConfig();
        _ = RefreshOwnedGamesAsync(force: true);
    }

    /// ألعاب المسح + المملوكة غير المثبتة (ما ليس مثبتًا أصلًا)
    private List<GameInfo> WithOwnedGames(IEnumerable<GameInfo> scanned)
    {
        var list = scanned.ToList();
        if (_ownedSteam.Count == 0) return list;
        var installed = list.Where(g => g.Platform == "Steam").Select(g => g.Id).ToHashSet();
        list.AddRange(_ownedSteam.Where(g => !installed.Contains(g.Id)));
        return list;
    }

    internal async Task RefreshOwnedGamesAsync(bool force = false)
    {
        var key = _config.SteamWebApiKey;
        if (string.IsNullOrEmpty(key))
        {
            if (_ownedSteam.Count > 0) { _ownedSteam = new(); ReapplyLastScan(); }
            OwnedStatusText = "";
            return;
        }

        try
        {
            var pathProvider = SteamPathProvider;
            var chosen = _config.SteamAccountId;
            var steamId = FetchOwnedOverride != null ? "test" : await Task.Run(() =>
            {
                var steam = pathProvider?.Invoke();
                return string.IsNullOrEmpty(steam) ? null : SteamPlaytimeImport.PickAccount(SteamPlaytimeImport.ReadAccounts(steam), chosen)?.SteamId64;
            });
            if (string.IsNullOrEmpty(steamId))
            {
                OwnedStatusText = Loc.Instance["ownedNoAccount"];
                return;
            }

            var cachePath = AppPaths.InData("cache", "steam-owned-" + steamId + ".json");
            string? json = null;
            if (!force && FetchOwnedOverride == null && File.Exists(cachePath) && DateTime.UtcNow - File.GetLastWriteTimeUtc(cachePath) < OwnedMaxAge)
                json = await File.ReadAllTextAsync(cachePath);

            if (json == null)
            {
                json = await (FetchOwnedOverride ?? SteamOwnedGames.FetchJsonAsync)(key, steamId);
                if (json != null && FetchOwnedOverride == null && SteamOwnedGames.Parse(json).Count > 0)
                {
                    try { AtomicFile.WriteAllText(cachePath, json); } catch (Exception ex) { Log.Error("owned games cache save failed", ex); }
                }
            }

            if (json == null)
            {
                OwnedStatusText = Loc.Instance["ownedFailed"];
                return;
            }

            _ownedSteam = SteamOwnedGames.Parse(json);
            OwnedStatusText = Loc.Instance.Format("ownedCount", _ownedSteam.Count);
            ReapplyLastScan();
        }
        catch (Exception ex)
        {
            Log.Error("owned games refresh failed", ex);
            OwnedStatusText = Loc.Instance["ownedFailed"];
        }
    }

    // ===== 7.4 دمج المكرر =====

    public bool MergeDuplicates
    {
        get => _config.MergeDuplicates;
        set
        {
            if (_config.MergeDuplicates == value) return;
            _config.MergeDuplicates = value;
            SaveConfig();
            OnPropertyChanged(nameof(MergeDuplicates));
            ApplyFilter();
        }
    }

    /// مفتاح المطابقة: الاسم بلا رموز ولا علامات تجارية وبأحرف صغيرة
    internal static string MergeKey(string name)
    {
        var sb = new StringBuilder();
        foreach (var c in name.ToLowerInvariant())
            if (char.IsLetterOrDigit(c)) sb.Append(c);
        return sb.ToString();
    }

    /// <summary>
    /// بطاقة واحدة لكل لعبة موجودة على أكثر من منصة: الأساسية هي المثبتة ثم الأحدث تشغيلًا،
    /// والبقية تصير "بدائل" تُعرض عند التشغيل. لا يُدمج شيء حين يختار المستخدم منصة بعينها.
    /// </summary>
    internal List<GameCardViewModel> MergeCards(IEnumerable<GameCardViewModel> cards)
    {
        var list = cards.ToList();
        foreach (var c in list) c.SetAlternates(Array.Empty<GameCardViewModel>());
        if (!_config.MergeDuplicates || SelectedPlatform != null) return list;

        var result = new List<GameCardViewModel>();
        var groups = list.GroupBy(c => MergeKey(c.Game.Name)).ToDictionary(g => g.Key, g => g.ToList());
        var done = new HashSet<string>();
        foreach (var card in list)
        {
            var key = MergeKey(card.Game.Name);
            if (key.Length == 0 || groups[key].Select(c => c.Game.Platform).Distinct().Count() < 2)
            {
                result.Add(card);
                continue;
            }
            if (!done.Add(key)) continue;

            // نسخة واحدة لكل منصة، والأساسية: مثبتة أولًا ثم الأحدث تشغيلًا
            var perPlatform = groups[key].GroupBy(c => c.Game.Platform).Select(g => g.First())
                .OrderByDescending(c => c.IsInstalled).ThenByDescending(c => c.LastPlayed ?? DateTime.MinValue).ToList();
            var primary = perPlatform[0];
            primary.SetAlternates(perPlatform.Skip(1).ToList());
            result.Add(primary);
        }
        return result;
    }

    /// لعبة على أكثر من منصة: نسأل من أيها تُشغَّل
    private bool AskLaunchFrom(GameCardViewModel card)
    {
        if (card.Alternates.Count == 0) return false;
        var l = Loc.Instance;
        var choices = new[] { card }.Concat(card.Alternates)
            .Select((c, i) => new ConfirmChoice
            {
                Label = c.PlatformShort + (c.IsInstalled ? "" : " · " + l["installGame"]),
                IsPrimary = i == 0,
                Run = () => LaunchGameDirect(c)
            }).ToList();
        choices.Add(new ConfirmChoice { Label = l["cancel"] });
        RequestConfirm(new ConfirmRequest { Title = l.Format("launchFromTitle", card.Name), Message = l["launchFromBody"], Choices = choices });
        return true;
    }
}
