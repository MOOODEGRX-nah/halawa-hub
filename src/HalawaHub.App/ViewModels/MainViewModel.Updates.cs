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

        _config.LastSeenVersion = AppInfo.Version;
        ConfigService.Save(_config);

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
        catch { }
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
        var result = await _updateService.CheckForUpdateAsync();

        if (!result.HasUpdate)
        {
            if (manualCheck) StatusMessage = result.Message;
            return;
        }

        UpdateMessage = result.Message;
        _updateDownloadUrl = result.DownloadUrl;
        _updateSha256 = result.Sha256;
        InstallUpdateCommand.RaiseCanExecuteChanged();
    }

    private async Task InstallUpdateAsync()
    {
        if (string.IsNullOrEmpty(_updateDownloadUrl) || _isUpdating) return;

        _isUpdating = true;
        InstallUpdateCommand.RaiseCanExecuteChanged();
        UpdateMessage = "جاري تحميل التحديث...";

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
            InstallUpdateCommand.RaiseCanExecuteChanged();
        }
    }
}
