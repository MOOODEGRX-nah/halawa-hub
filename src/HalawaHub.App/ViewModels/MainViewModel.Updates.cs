using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HalawaHub.App.Services;
using HalawaHub.Core;
using HalawaHub.Core.Covers;
using HalawaHub.Core.Library;
using HalawaHub.Core.Models;
using HalawaHub.Core.News;
using HalawaHub.Core.Plugins;
using HalawaHub.Core.Updates;

namespace HalawaHub.App.ViewModels;

public partial class MainViewModel
{
    private async Task InitializeBackgroundTasksAsync()
    {
        await CheckForUpdateAsync();
        _ = LoadNewsAsync();
        _ = CheckChangelogAsync();
    }

    private async Task LoadNewsAsync()
    {
        NewsEntries.Clear();

        // خبر تحديث البرنامج نفسه، لو متوفر فعليًا
        if (HasUpdateMessage)
            NewsEntries.Add(new NewsItem("Halawa-Hub", UpdateMessage ?? "", "", DateTime.UtcNow));

        // آخر خبر لأول لعبتين Steam بمكتبتك (API عام مجاني، بدون مفتاح)
        var steamGames = Games.Where(c => c.Platform == "Steam" && !string.IsNullOrEmpty(c.Game.Id))
                               .Take(2).ToList();

        foreach (var g in steamGames)
        {
            var items = await _newsClient.GetNewsForAppAsync(g.Name, g.Game.Id, count: 1);
            foreach (var item in items) NewsEntries.Add(item);
        }

        OnPropertyChanged(nameof(HasNews));
    }

    private void UpdateHomeViewCollections()
    {
        // استمر من حيث توقفت — آخر لعبة شغّلتها فعليًا (فاضي لين تشغّل أول لعبة)
        ContinuePlayingGame = Games.Where(c => c.LastPlayed != null)
                                    .OrderByDescending(c => c.LastPlayed)
                                    .FirstOrDefault();

        // تم تحميله حديثًا — أحدث 3 حسب تاريخ تعديل مجلد التثبيت
        RecentlyInstalled.Clear();
        foreach (var c in Games.OrderByDescending(c => GetInstallTimestamp(c.Game)).Take(3))
            RecentlyInstalled.Add(c);
        OnPropertyChanged(nameof(HasRecentlyInstalled));

        // تشغيل سريع — المفضلة (لحد 4)
        FavoriteQuickLaunch.Clear();
        foreach (var c in Games.Where(c => c.IsFavorite).Take(4))
            FavoriteQuickLaunch.Add(c);
        OnPropertyChanged(nameof(HasFavoriteQuickLaunch));

        // آخر الألعاب اللي لعبتها — كل المكتبة، مرتبة حسب آخر تشغيل (اللي ما
        // تشغّل أبدًا ينزل لآخر الترتيب بدل ما يختفي)
        RecentlyPlayed.Clear();
        foreach (var c in Games.OrderByDescending(c => c.LastPlayed ?? DateTime.MinValue))
            RecentlyPlayed.Add(c);
        OnPropertyChanged(nameof(HasRecentlyPlayed));
    }

    private async Task CheckChangelogAsync()
    {
        if (_config.LastSeenVersion == AppInfo.Version) return;

        var changes = await ChangelogService.GetChangesForVersionAsync(AppInfo.GitHubOwner, AppInfo.GitHubRepo, AppInfo.Version);

        _config.LastSeenVersion = AppInfo.Version;
        ConfigService.Save(_config);

        // ما فيه ملاحظات مسجّلة لهذا الإصدار (أو أول تشغيل بالأساس) — لا نزعج المستخدم
        if (changes.Count == 0) return;

        ChangelogEntries.Clear();
        foreach (var change in changes) ChangelogEntries.Add(change);
        IsChangelogOpen = true;
    }

    private void OpenUrl(object? param)
    {
        if (param is not string url || string.IsNullOrEmpty(url)) return;

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // تجاهل، ما يستاهل مقاطعة المستخدم بخطأ لمجرد فشل فتح رابط
        }
    }

    private async Task VerifyApiKeyAsync()
    {
        if (string.IsNullOrWhiteSpace(SteamGridDbApiKey))
        {
            ApiKeyStatus = ApiKeyCheckState.Invalid;
            return;
        }

        ApiKeyStatus = ApiKeyCheckState.Checking;

        var client = new SteamGridDbClient(SteamGridDbApiKey);
        var valid = await client.ValidateApiKeyAsync();

        ApiKeyStatus = valid ? ApiKeyCheckState.Valid : ApiKeyCheckState.Invalid;
    }

    private async Task CheckForUpdateAsync(bool manualCheck = false)
    {
        var update = await _updateChecker.CheckForUpdateAsync();

        if (update is not { IsNewer: true })
        {
            if (manualCheck) StatusMessage = "البرنامج محدّث لآخر إصدار.";
            return;
        }

        Log.Info("تحديث جديد متوفر: v" + update.LatestVersion);
            UpdateMessage = $"يتوفر إصدار جديد: v{update.LatestVersion} (لديك v{AppInfo.Version})";
        _updateDownloadUrl = update.DownloadUrl;
            _updateSha256 = update.Sha256;
        InstallUpdateCommand.RaiseCanExecuteChanged();
    }

    private async Task InstallUpdateAsync()
    {
        if (string.IsNullOrEmpty(_updateDownloadUrl) || _isUpdating) return;

        _isUpdating = true;
        InstallUpdateCommand.RaiseCanExecuteChanged();
        UpdateMessage = "جاري تحميل التحديث...";

        var success = await SelfUpdater.DownloadAndApplyAsync(_updateDownloadUrl, status => UpdateMessage = status, _updateSha256);

        if (success)
        {
            UpdateMessage = "التحديث جاهز، البرنامج بيعيد التشغيل الآن...";
            await Task.Delay(1200);
            Environment.Exit(0);
        }
        else
        {
            Log.Error("فشل التحديث التلقائي");
                UpdateMessage = "فشل التحديث التلقائي. جرّب لاحقًا أو حمّل من صفحة الإصدارات على GitHub يدويًا.";
            _isUpdating = false;
            InstallUpdateCommand.RaiseCanExecuteChanged();
        }
    }

}
