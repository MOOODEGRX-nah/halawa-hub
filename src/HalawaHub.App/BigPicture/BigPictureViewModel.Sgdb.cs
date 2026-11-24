using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using HalawaHub.App.ViewModels;
using HalawaHub.Core.Covers;

namespace HalawaHub.App.BigPicture;

/// صورة في شبكة SteamGridDB بالشاشة الكبيرة
public sealed class BpSgdbItem : BpItem
{
    public required SgdbTile Tile { get; init; }
    public int Index { get; init; }
}

/// <summary>
/// صفحة SteamGridDB في الشاشة الكبيرة: نفس المتصفح المشترك مع النافذة العادية (الأنواع والأشكال والمتحركة،
/// شبكة بلا حد). LB/RB النوع، X الشكل، Y المتحركة فقط، A استعمل، B رجوع. بلا مفتاح: QR ولصق وكتابة.
/// </summary>
public sealed partial class BigPictureViewModel
{
    private SgdbBrowser? _sgdb;
    private BpGame? _sgdbGame;
    private int _sgIndex;
    private bool _coverKey;

    public ObservableCollection<BpSgdbItem> SgdbItems { get; } = new();
    public SgdbBrowser? Sgdb => _sgdb;
    public bool IsSgdbOpen => _screen == BpScreen.Sgdb;
    /// قسم الغلاف يعرض بطاقة المفتاح بدل البطاقات الثلاث
    public bool IsCoverKey => _coverKey && _gameSet != null;
    public string SgdbKeyUrl => _host.SgdbKeyUrl;

    /// أعمدة الشبكة حسب النوع: الأغلفة عمودية ضيقة، الخلفيات عريضة
    public int SgdbColumns => (_sgdb?.Type ?? SgdbArtType.Cover) switch
    {
        SgdbArtType.Cover => 9,
        SgdbArtType.Icon => 9,
        SgdbArtType.Background => 3,
        _ => 5,
    };

    public double SgdbTileWidth => (_sgdb?.Type ?? SgdbArtType.Cover) switch
    {
        SgdbArtType.Cover or SgdbArtType.Icon => 170,
        SgdbArtType.Background => 552,
        _ => 324,
    };

    public double SgdbTileHeight => (_sgdb?.Type ?? SgdbArtType.Cover) switch
    {
        SgdbArtType.Cover => 255,
        SgdbArtType.Icon => 170,
        SgdbArtType.Background => 178,
        SgdbArtType.Logo => 140,
        _ => 151,
    };

    /// الشبكة تصعد مع التركيز: الصف المركَّز يبقى ثاني صف ظاهر
    public double SgdbShiftY => -Math.Max(0, _sgIndex / SgdbColumns - 1) * (SgdbTileHeight + 16 + 26);
    public Avalonia.Media.Transformation.TransformOperations SgdbShiftOps =>
        Avalonia.Media.Transformation.TransformOperations.Parse(FormattableString.Invariant($"translateY({SgdbShiftY}px)"));

    private void OpenSgdb(BpGame g)
    {
        if (!_host.HasSgdbKey)
        {
            _coverKey = true;
            _setRow = 0;
            RefreshGameRows();
            return;
        }
        _coverKey = false;
        _sgdb = _host.Sgdb;
        if (_sgdb == null) return;
        _sgdbGame = g;
        _sgIndex = 0;
        _sgdb.Tiles.CollectionChanged -= OnSgdbTilesChanged;
        _sgdb.Tiles.CollectionChanged += OnSgdbTilesChanged;
        _sgdb.PropertyChanged -= OnSgdbChanged;
        _sgdb.PropertyChanged += OnSgdbChanged;
        _screen = BpScreen.Sgdb;
        RebuildSgdbItems();
        _ = _sgdb.OpenAsync(_host.GameInfo(g).OriginalName);
        Changed();
    }

    private void OnSgdbTilesChanged(object? sender, NotifyCollectionChangedEventArgs e) { RebuildSgdbItems(); Changed(); }
    private void OnSgdbChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => Changed();

    private void RebuildSgdbItems()
    {
        if (_sgdb == null) return;
        // الإضافة عند النزول تضيف في الآخر فقط: لا نعيد بناء ما هو موجود
        if (SgdbItems.Count > _sgdb.Tiles.Count || (SgdbItems.Count > 0 && !ReferenceEquals(SgdbItems[0].Tile, _sgdb.Tiles.FirstOrDefault())))
            SgdbItems.Clear();
        for (var i = SgdbItems.Count; i < _sgdb.Tiles.Count; i++)
            SgdbItems.Add(new BpSgdbItem { FocusKey = "sg." + i, Tile = _sgdb.Tiles[i], Index = i });
        _sgIndex = Math.Clamp(_sgIndex, 0, Math.Max(0, SgdbItems.Count - 1));
    }

    private void CloseSgdb()
    {
        _screen = BpScreen.Settings;
        _sgdbGame = null;
        RefreshGameRows();
    }

    private void NavSgdb(int dx, int dy)
    {
        if (SgdbItems.Count == 0) return;
        var cols = SgdbColumns;
        var next = _sgIndex + dx + dy * cols;
        if (dx != 0 && (next / cols != _sgIndex / cols)) return;   // لا يلف إلى الصف التالي
        _sgIndex = Math.Clamp(next, 0, SgdbItems.Count - 1);
        // قريب من الأسفل: الصفحة التالية
        if (_sgIndex >= SgdbItems.Count - cols * 2) _ = _sgdb?.LoadMoreAsync();
    }

    private async void ActSgdb()
    {
        if (_sgdbGame is not { } g || _sgIndex >= SgdbItems.Count) return;
        var tile = SgdbItems[_sgIndex].Tile;
        var ok = await _host.PickSgdbAsync(g, tile);
        ShowToast(T(ok ? "bpGsCoverSet" : "sgdbFailed"));
        if (ok) CloseSgdb();
    }

    public void ClickSgdb(int index)
    {
        var same = index == _sgIndex;
        _sgIndex = Math.Clamp(index, 0, Math.Max(0, SgdbItems.Count - 1));
        if (same) ActSgdb();
        SyncFocus();
    }

    private void CycleSgdbType(int delta)
    {
        if (_sgdb == null) return;
        _sgIndex = 0;
        _ = _sgdb.SetTypeAsync(((int)_sgdb.Type + delta + 5) % 5);
    }

    private void CycleSgdbStyle()
    {
        if (_sgdb == null) return;
        var chips = _sgdb.StyleChips;
        var i = chips.ToList().FindIndex(c => c.IsOn);
        _sgIndex = 0;
        _ = _sgdb.SetStyleAsync(chips[(i + 1) % chips.Count].Key);
    }

    private void ToggleSgdbAnimated()
    {
        _sgIndex = 0;
        _ = _sgdb?.ToggleAnimatedAsync();
    }

    private string? SgdbFocusKey() => SgdbItems.Count > 0 ? "sg." + _sgIndex : null;

    // ===== بلا مفتاح: QR، الصق، اكتب =====

    /// «الصق المفتاح»: تقرأ النافذة الحافظة ثم تمرره هنا
    public event Action? PasteKeyRequested;

    public void PasteKey(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) { ShowToast(T("bpGsKeyEmpty")); return; }
        _ = SaveKeyAsync(text.Trim());
    }

    private async Task SaveKeyAsync(string key)
    {
        if (key.Length == 0) return;
        ShowToast(T("verifying"));
        var ok = await _host.SaveSgdbKeyAsync(key);
        ShowToast(T(ok ? "bpGsKeyOk" : "bpGsKeyBad"));
        if (ok && _gameSet is { } g)
        {
            _coverKey = false;
            OpenSgdb(g);   // المفتاح صالح: النتائج مباشرة
        }
    }
}
