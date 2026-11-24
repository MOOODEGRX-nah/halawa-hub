using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace HalawaHub.App.BigPicture;

/// عرض واحد كما يصل من البرنامج (مجانية Epic أو تخفيض Steam/GOG)
public sealed record BpDealData(string Title, string Line, string Badge, string? ImageUrl, string Url, bool IsFree);

/// صف عروض متجر: العنوان، العروض، ورابط صفحة المتجر الكاملة لبطاقة «عرض الكل»
public sealed record BpDealSource(string TitleKey, IReadOnlyList<BpDealData> Items, string MoreUrl);

public sealed class BpDealItem : BpItem
{
    public BpDealData? Deal { get; init; }
    public bool IsMore => Deal == null;
    public required string Url { get; init; }
    public int Row { get; init; }
    public int Index { get; init; }
    public string Title => Deal?.Title ?? "";
    public string Line => Deal?.Line ?? "";
    public string Badge => Deal?.Badge ?? "";
    public bool HasBadge => Badge.Length > 0;
    public bool IsFree => Deal?.IsFree == true;
    public string? ImageUrl => Deal?.ImageUrl;
    public string Letter => Title.Trim().Length > 0 ? char.ToUpperInvariant(Title.Trim()[0]).ToString() : "";
}

public sealed class BpDealRow : System.ComponentModel.INotifyPropertyChanged
{
    /// المسافة بين بطاقتين (العرض 280 + الفراغ 22)
    public const double Pitch = 302;
    private int _offset;
    /// كم بطاقة تزاح للبداية حتى تبقى المركَّزة ظاهرة
    public int Offset
    {
        get => _offset;
        set { if (_offset == value) return; _offset = value; PropertyChanged?.Invoke(this, new(nameof(Offset))); PropertyChanged?.Invoke(this, new(nameof(ShiftOps))); }
    }
    public double ShiftX => -_offset * Pitch;
    public Avalonia.Media.Transformation.TransformOperations ShiftOps =>
        Avalonia.Media.Transformation.TransformOperations.Parse(FormattableString.Invariant($"translateX({ShiftX}px)"));
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public required string TitleKey { get; init; }
    public string Title => BigPictureViewModel.T(TitleKey);
    public required int Count { get; init; }
    public ObservableCollection<BpDealItem> Items { get; } = new();
    public string MoreLabel => BigPictureViewModel.T("bpShowAll");
    public string MoreNote { get; init; } = "";
}

/// <summary>تبويب العروض (الرابع): مجانيات Epic، تخفيضات Steam، متاجر أخرى، ولكل صف بطاقة «عرض الكل» في آخره.</summary>
public sealed partial class BigPictureViewModel
{
    public ObservableCollection<BpDealRow> DealRows { get; } = new();

    private int _dealRow;
    private int _dealCol;

    public int DealRowIndex => _dealRow;
    public int DealCol => _dealCol;
    public bool HasDeals => DealRows.Count > 0;
    public string DealsUpdatedText => _host.DealsUpdatedText;
    public bool HasDealsUpdated => DealsUpdatedText.Length > 0;

    /// الصف الثالث لا يتسع تحت الأول والثاني: تصعد الصفوف حين يُركَّز عليه (trackCls r2 في التصميم)
    public const double DealRowHeight = 300;
    public double DealsShiftY => _dealRow >= 2 ? -DealRowHeight : 0;
    public Avalonia.Media.Transformation.TransformOperations DealsShiftOps =>
        Avalonia.Media.Transformation.TransformOperations.Parse(FormattableString.Invariant($"translateY({DealsShiftY}px)"));

    /// البطاقة المركَّزة تبقى ثالثة من البداية ما أمكن؛ الصفوف الأخرى ترجع لأولها
    private void UpdateDealShifts()
    {
        for (var i = 0; i < DealRows.Count; i++)
            DealRows[i].Offset = i == _dealRow ? Math.Clamp(_dealCol - 2, 0, Math.Max(0, DealRows[i].Items.Count - 5)) : 0;
    }

    private void BuildDeals()
    {
        DealRows.Clear();
        var r = 0;
        foreach (var src in _host.Deals.Where(s => s.Items.Count > 0))
        {
            var row = new BpDealRow { TitleKey = src.TitleKey, Count = src.Items.Count, MoreNote = StoreName(src.MoreUrl) };
            for (var i = 0; i < src.Items.Count; i++)
                row.Items.Add(new BpDealItem { FocusKey = $"deal.{r}.{i}", Deal = src.Items[i], Url = src.Items[i].Url, Row = r, Index = i });
            row.Items.Add(new BpDealItem { FocusKey = $"deal.{r}.{src.Items.Count}", Url = src.MoreUrl, Row = r, Index = src.Items.Count });
            DealRows.Add(row);
            r++;
        }
        _dealRow = Math.Clamp(_dealRow, 0, Math.Max(0, DealRows.Count - 1));
        _dealCol = DealRows.Count == 0 ? 0 : Math.Clamp(_dealCol, 0, DealRows[_dealRow].Items.Count - 1);
        UpdateDealShifts();
    }

    private static string StoreName(string url) =>
        url.Contains("epicgames", StringComparison.Ordinal) ? "Epic Games Store"
        : url.Contains("steampowered", StringComparison.Ordinal) ? "Steam"
        : url.Contains("gog.com", StringComparison.Ordinal) ? "GOG" : "";

    private void NavDeals(int dx, int dy)
    {
        if (DealRows.Count == 0) return;
        if (dy != 0)
        {
            _dealRow = Math.Clamp(_dealRow + dy, 0, DealRows.Count - 1);
            _dealCol = Math.Min(_dealCol, DealRows[_dealRow].Items.Count - 1);
            UpdateDealShifts();
            return;
        }
        _dealCol = Math.Clamp(_dealCol + dx, 0, DealRows[_dealRow].Items.Count - 1);
        UpdateDealShifts();
    }

    /// A: يفتح العرض أو صفحة المتجر في المتصفح، ورسالة قصيرة تقول ذلك
    private void ActDeals()
    {
        if (DealRows.Count == 0) return;
        var item = DealRows[_dealRow].Items[_dealCol];
        _host.OpenUrl(item.Url);
        ShowToast(T("bpOpensBrowser"));
    }

    public void ClickDeal(int row, int index)
    {
        if (row < 0 || row >= DealRows.Count) return;
        var same = _dealRow == row && _dealCol == index;
        _dealRow = row;
        _dealCol = Math.Clamp(index, 0, DealRows[row].Items.Count - 1);
        UpdateDealShifts();
        if (same) ActDeals();
        SyncFocus();
    }

    private string? DealsFocusKey() => DealRows.Count == 0 ? null : $"deal.{_dealRow}.{_dealCol}";

    private IEnumerable<BpItem> DealItems() => DealRows.SelectMany(r => r.Items);
}
