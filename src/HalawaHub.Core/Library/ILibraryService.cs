using HalawaHub.Core.Models;
using HalawaHub.Core.Plugins;

namespace HalawaHub.Core.Library;

/// <summary>
/// نتيجة فحص المكتبات — data فقط، بدون UI state.
/// </summary>
public class LibraryScanResult
{
    public List<GameInfo> Games { get; set; } = new();
    public Dictionary<string, int> PlatformCounts { get; set; } = new();
}

/// <summary>
/// خدمة فحص المكتبات — منطق نقي بدون اعتماد على UI.
/// </summary>
public interface ILibraryService
{
    /// <summary>
    /// فحص كل المنصات المتاحة وإرجاع الألعاب + أعداد المنصات.
    /// </summary>
    LibraryScanResult ScanAllLibraries(
        IEnumerable<IGameLibraryProvider> providers,
        IEnumerable<IGameTool> tools);
}
