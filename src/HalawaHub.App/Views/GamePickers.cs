using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using HalawaHub.App.Services;
using HalawaHub.Core.Library;

namespace HalawaHub.App.Views;

/// منتقي ملف لعبة (إضافة يدوية أو تغيير ملفها): الأنواع المناسبة لكل نظام
internal static class GamePickers
{
    public static async Task<string?> PickGameFileAsync(Control owner)
    {
        var storage = TopLevel.GetTopLevel(owner)?.StorageProvider;
        if (storage == null) return null;

        var filter = OperatingSystem.IsWindows()
            ? new FilePickerFileType(Loc.Instance["gameFiles"]) { Patterns = ManualGames.WindowsExtensions }
            : new FilePickerFileType(Loc.Instance["gameFiles"]) { Patterns = ManualGames.LinuxExtensions };

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Loc.Instance["addGame"],
            AllowMultiple = false,
            FileTypeFilter = new[] { filter }
        });
        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }
}
