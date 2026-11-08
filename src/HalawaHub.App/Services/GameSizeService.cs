using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using HalawaHub.Core;

namespace HalawaHub.App.Services;

/// <summary>
/// يحسب حجم مجلد اللعبة على خيط خلفي ويحفظ النتيجة طوال الجلسة، لأن المرور على
/// مجلد كبير قد يأخذ ثواني. المجلدات المحمية (WindowsApps) ترجع null.
/// </summary>
public static class GameSizeService
{
    private static readonly ConcurrentDictionary<string, Task<long?>> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static Task<long?> GetSizeAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return Task.FromResult<long?>(null);
        return Cache.GetOrAdd(path, p => Task.Run(() => Compute(p)));
    }

    internal static long? Compute(string path)
    {
        try
        {
            if (!Directory.Exists(path)) return null;

            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            };

            long total = 0;
            foreach (var file in new DirectoryInfo(path).EnumerateFiles("*", options))
                total += file.Length;
            return total;
        }
        catch (Exception ex)
        {
            Log.Error($"could not measure {path}", ex);
            return null;
        }
    }

    public static string Format(long? bytes)
    {
        if (bytes is null) return "—";

        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double value = bytes.Value;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        var format = unit == 0 ? "0" : "0.0";
        return value.ToString(format, CultureInfo.InvariantCulture) + " " + units[unit];
    }
}
