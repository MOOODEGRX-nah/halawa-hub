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
        _ = CheckChangelogAsync();

    }

    private async Task LoadNewsAsync()
    {
        NewsEntries.Clear();

        if (HasUpdateMessage)
            NewsEntries.Add(new NewsItem("Halawa-Hub", UpdateMessage ?? "", "", DateTime.UtcNow));

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
        ContinuePlayingGame = Games.Where(c => c.LastPlayed != null)
                                    .OrderByDescending(c => c.LastPlayed)
                                    .FirstOrDefault();

        RecentlyInstalled.Clear();
        foreach (var c in Games.OrderByDescending(c => GetInstallTimestamp(c.Game)).Take(3))
            RecentlyInstalled.Add(c);
        OnPropertyChanged(nameof(HasRecentlyInstalled));

        FavoriteQuickLaunch.Clear();
        foreach (var c in Games.Where(c => c.IsFavorite).Take(4))
            FavoriteQuickLaunch.Add(c);
        OnPropertyChanged(nameof(HasFavoriteQuickLaunch));

        RecentlyPlayed.Clear();
        foreach (var c in Games.OrderByDescending(c => c.LastPlayed ?? DateTime.MinValue))
            RecentlyPlayed.Add(c);
        OnPropertyChanged(nameof(HasRecentlyPlayed));
    }

    private async Task CheckChangelogAsync()
    {
        if (_config.LastSeenVersion == AppInfo.Version) return;

        var changes = await ChangelogService.GetChangesForVersionAsync(AppInfo.GitHubOwner, AppInfo.GitHubRepo, AppInfo.Version);

        // B09a: لا نحفظ LastSeenVersion إلا بعد التأكد من أن المحتوى موجود وعُرض فعليًا
        // (كان يُحفظ قبل ذلك حتى لو فشل الجلب → المستخدم لا يرى "ما الجديد" أبدًا)
        if (changes.Count == 0) return;

        ChangelogEntries.Clear();
        foreach (var change in changes) ChangelogEntries.Add(change);
        IsChangelogOpen = true;

        // الآن فقط نحفظ أن المستخدم شاهد هذا الإصدار
        _config.LastSeenVersion = AppInfo.Version;
        ConfigService.Save(_config);
    }

    private void OpenUrl(object? param)
    {
        if (param is not string url || string.IsNullOrEmpty(url)) return;

        // روابط من خدمات خارجية (أخبار Steam مثلًا): http/https فقط، لا ملفات ولا بروتوكولات أخرى
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            Log.Error($"refused to open a non-web link: {url}");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
        }
        catch { }
    }

    private async Task VerifyApiKeyAsync()
    {
        if (string.IsNullOrWhiteSpace(SteamGridDbApiKey))
        {
            ApiKeyStatus = ApiKeyCheckState.Invalid;
            return;
        }

        FlushPendingSettings();

        var key = SteamGridDbApiKey;
        ApiKeyStatus = ApiKeyCheckState.Checking;

        var client = new SteamGridDbClient(key);
        var valid = await client.ValidateApiKeyAsync();

        // المستخدم عدّل المفتاح أثناء التحقق: النتيجة تخص مفتاحًا قديمًا
        if (key != SteamGridDbApiKey) return;

        ApiKeyStatus = valid ? ApiKeyCheckState.Valid : ApiKeyCheckState.Invalid;

        if (valid)
        {
            // المفتاح الجديد يعمل مباشرة بدون إعادة تشغيل
            _coverClient = client;
            _ = LoadMissingCoversAsync();
        }
    }

    private bool _isCheckingUpdates;

    private async Task CheckForUpdateAsync(bool manualCheck = false)
    {
        if (_isCheckingUpdates) return;
        _isCheckingUpdates = true;
        UpdateKind = UpdateCheckKind.Checking;

        var result = await _updateService.CheckForUpdateAsync();
        _isCheckingUpdates = false;

        if (!result.HasUpdate)
        {
            UpdateKind = result.Failed ? UpdateCheckKind.Failed : UpdateCheckKind.UpToDate;
            return;
        }

        UpdateMessage = result.Message;
        _latestVersion = result.LatestVersion;
        _updateDownloadUrl = result.DownloadUrl;
        _updateSha256 = result.Sha256;
        _updateIsReleasePage = result.IsReleasePage;
        UpdateKind = UpdateCheckKind.Available;
        InstallUpdateCommand.RaiseCanExecuteChanged();

        // كان جرس الإشعارات يخبر بالتحديث: رسالة قصيرة ونقطة على زر الإعدادات
        if (!manualCheck) ShowStatus(UpdateStatusText);
    }

    private bool _isInstallingUpdate;
    public bool IsInstallingUpdate
    {
        get => _isInstallingUpdate;
        private set
        {
            _isInstallingUpdate = value;
            OnPropertyChanged(nameof(IsInstallingUpdate));
        }
    }

    private async Task InstallUpdateAsync()
    {
        if (string.IsNullOrEmpty(_updateDownloadUrl) || _isUpdating) return;

        // لا يوجد ملف تثبيت لهذا النظام: نفتح صفحة الإصدار بدل تنزيل صفحة HTML
        if (_updateIsReleasePage)
        {
            OpenUrl(_updateDownloadUrl);
            return;
        }

        _isUpdating = true;
        IsInstallingUpdate = true;
        InstallUpdateCommand.RaiseCanExecuteChanged();
        UpdateMessage = Loc.Instance["updDownloading"];

        var result = await _updateService.InstallUpdateAsync(
            _updateDownloadUrl,
            _updateSha256,
            status => UpdateMessage = status);

        if (result.Success)
        {
            UpdateMessage = result.Message;
            await Task.Delay(1200);
            Environment.Exit(0);
        }
        else
        {
            UpdateMessage = result.Message;
            _isUpdating = false;
            IsInstallingUpdate = false;
            ShowStatus(result.Message);
            InstallUpdateCommand.RaiseCanExecuteChanged();
        }
    }
}
