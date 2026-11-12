using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using HalawaHub.Core;
using HalawaHub.Core.News;

namespace HalawaHub.App.ViewModels;

/// <summary>
/// تبويب "الأخبار" في نافذة لعبة Steam: آخر أخبارها من ISteamNews (بلا مفتاح).
/// تُحفظ على القرص (cache/news/&lt;appid&gt;.json) وتُعاد بعد 6 ساعات؛ بلا إنترنت تُعرض
/// آخر نسخة محفوظة بصمت.
/// </summary>
public partial class MainViewModel
{
    public const int NewsSection = 4;
    public const int ToolsSection = 5;
    private static readonly TimeSpan NewsMaxAge = TimeSpan.FromHours(6);

    public ObservableCollection<NewsItem> GameNews { get; } = new();

    private bool _isGameNewsLoading;
    public bool IsGameNewsLoading
    {
        get => _isGameNewsLoading;
        private set
        {
            if (_isGameNewsLoading == value) return;
            _isGameNewsLoading = value;
            OnPropertyChanged(nameof(IsGameNewsLoading));
            OnPropertyChanged(nameof(HasNoGameNews));
        }
    }

    public bool HasNoGameNews => !_isGameNewsLoading && GameNews.Count == 0;

    /// للاختبارات: بدل الشبكة
    internal Func<string, string, Task<List<NewsItem>>>? FetchNewsOverride { get; set; }

    private string? _newsLoadedFor;

    private sealed class NewsCache
    {
        public DateTime SavedUtc { get; set; }
        public List<NewsItem> Items { get; set; } = new();
    }

    private static string NewsCachePath(string appId) => AppPaths.InData("cache", "news", appId + ".json");

    internal async Task LoadGameNewsAsync(GameCardViewModel card)
    {
        if (!card.HasNews || _newsLoadedFor == card.Game.Id) return;
        var appId = card.Game.Id;
        _newsLoadedFor = appId;
        GameNews.Clear();
        IsGameNewsLoading = true;

        try
        {
            var cache = await Task.Run(() => ReadNewsCache(appId));
            if (cache != null) Fill(cache.Items);

            if (cache == null || DateTime.UtcNow - cache.SavedUtc > NewsMaxAge)
            {
                var fetch = FetchNewsOverride ?? ((name, id) => _newsClient.GetNewsForAppAsync(name, id, count: 10));
                var fresh = await fetch(card.Name, appId);
                if (fresh.Count > 0)
                {
                    await Task.Run(() => WriteNewsCache(appId, fresh));
                    if (_newsLoadedFor == appId) Fill(fresh);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error($"news for {appId} failed", ex);
        }
        finally
        {
            if (_newsLoadedFor == appId) IsGameNewsLoading = false;
        }

        void Fill(List<NewsItem> items)
        {
            if (_newsLoadedFor != appId) return;
            GameNews.Clear();
            foreach (var item in items) GameNews.Add(item);
            OnPropertyChanged(nameof(HasNoGameNews));
        }
    }

    private static NewsCache? ReadNewsCache(string appId)
    {
        try
        {
            var path = NewsCachePath(appId);
            return File.Exists(path) ? JsonSerializer.Deserialize<NewsCache>(File.ReadAllText(path)) : null;
        }
        catch
        {
            return null;
        }
    }

    private static void WriteNewsCache(string appId, List<NewsItem> items)
    {
        try
        {
            AtomicFile.WriteAllText(NewsCachePath(appId), JsonSerializer.Serialize(new NewsCache { SavedUtc = DateTime.UtcNow, Items = items }));
        }
        catch (Exception ex)
        {
            Log.Error("news cache save failed", ex);
        }
    }
}
