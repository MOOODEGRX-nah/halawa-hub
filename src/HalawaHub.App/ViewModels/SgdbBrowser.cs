using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using HalawaHub.App.Services;
using HalawaHub.Core;
using HalawaHub.Core.Covers;

namespace HalawaHub.App.ViewModels;

/// صورة في شبكة النتائج: الشكل والتقييم وشارة GIF للمتحرك
public sealed class SgdbTile
{
    public required SgdbImage Image { get; init; }
    public required SgdbArtType Type { get; init; }
    public string ThumbUrl => Image.Thumb;
    public string StyleText => SgdbBrowser.StyleLabel(Image.Style);
    public string ScoreText => Image.Score.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public bool IsAnimated => Image.IsAnimated;
    /// مقاس البلاطة حسب النوع: عمودي للأغلفة، عريض للبقية، مربع للأيقونات
    public double TileWidth => Type switch { SgdbArtType.Cover => 104, SgdbArtType.Icon => 104, SgdbArtType.Background => 214, _ => 160 };
    public double TileHeight => Type switch { SgdbArtType.Cover => 156, SgdbArtType.Icon => 104, SgdbArtType.Background => 70, _ => 75 };
}

/// زر شكل (الكل، بديلة، مموّهة...)
public sealed class SgdbChip
{
    public string? Key { get; init; }
    public required string Label { get; init; }
    public bool IsOn { get; init; }
}

/// <summary>
/// متصفح SteamGridDB داخل نافذة اللعبة (والشاشة الكبيرة): بحث بالاسم، خمسة أنواع، أشكال كل نوع،
/// «المتحركة فقط»، وشبكة تحمّل الصفحة التالية عند النزول بلا حد ثابت.
/// بدون «المتحركة فقط» تظهر الصور الثابتة أولًا ثم المتحركة بعد انتهائها، فشارة GIF دقيقة دائمًا
/// (SteamGridDB يعيد الثابتة فقط ما لم تُطلب المتحركة صراحة).
/// </summary>
public sealed class SgdbBrowser : INotifyPropertyChanged
{
    private readonly Func<SteamGridDbClient> _client;

    public SgdbBrowser(Func<SteamGridDbClient> client) => _client = client;

    public ObservableCollection<SgdbTile> Tiles { get; } = new();

    private sealed class Cursor
    {
        public required bool Animated { get; init; }
        public int NextPage;
        public int Total;
        public int Loaded;
        public bool Done;
        public List<SgdbImage>? Buffered;
    }

    private readonly List<Cursor> _cursors = new();
    private int _gameId;
    private int _generation;

    public string GameName { get; private set; } = "";
    public string Title => Loc.Instance.Format("sgTitle", GameName);

    private string _query = "";
    public string Query { get => _query; set { _query = value ?? ""; Raise(nameof(Query)); } }

    private SgdbArtType _type = SgdbArtType.Cover;
    public SgdbArtType Type => _type;
    public int TypeIndex => (int)_type;

    private string? _style;
    public string? Style => _style;

    private bool _animatedOnly;
    public bool AnimatedOnly => _animatedOnly;

    private int _total;
    public int Total { get => _total; private set { _total = value; Raise(nameof(Total)); Raise(nameof(CountText)); } }
    public string CountText => Loc.Instance.Format("sgCount", _total);

    private bool _isLoading;
    public bool IsLoading { get => _isLoading; private set { _isLoading = value; Raise(nameof(IsLoading)); Raise(nameof(IsEmpty)); } }

    /// مفتاح نص الخطأ (لا النص نفسه) حتى يتبع لغة الواجهة
    private string? _errorKey;
    private string? ErrorKey { get => _errorKey; set { _errorKey = value; Raise(nameof(Error)); Raise(nameof(HasError)); Raise(nameof(IsEmpty)); } }
    public string? Error => _errorKey == null ? null : Loc.Instance[_errorKey];
    public bool HasError => _errorKey != null;

    public bool HasMore => !HasError && _cursors.Any(c => !c.Done);
    public bool IsEmpty => !IsLoading && !HasError && Tiles.Count == 0 && _searched;
    private bool _searched;

    /// الأنواع الخمسة؛ Key هو رقم النوع
    public IReadOnlyList<SgdbChip> TypeChips => new[] { "sgGrid", "sgWide", "sgHero", "sgLogo", "sgIcon" }
        .Select((k, i) => new SgdbChip { Key = i.ToString(System.Globalization.CultureInfo.InvariantCulture), Label = Loc.Instance[k], IsOn = i == (int)_type })
        .ToList();

    public IReadOnlyList<SgdbChip> StyleChips =>
        new[] { new SgdbChip { Key = null, Label = Loc.Instance["sgAll"], IsOn = _style == null } }
            .Concat(SgdbQuery.StylesFor(_type).Select(s => new SgdbChip { Key = s, Label = StyleLabel(s), IsOn = _style == s }))
            .ToList();

    public static string StyleLabel(string style) => Loc.Instance[style switch
    {
        "alternate" => "sgAlt",
        "blurred" => "sgBlur",
        "white_logo" => "sgWhite",
        "no_logo" => "sgNoLogo",
        "material" => "sgMat",
        "official" => "sgOfficial",
        "white" => "sgWhiteL",
        "black" => "sgBlack",
        "custom" => "sgCustom",
        _ => "sgAll"
    }];

    /// يفتح المتصفح لاسم اللعبة (الاسم الأصلي من المتجر) ويبدأ البحث
    public Task OpenAsync(string gameName, SgdbArtType type = SgdbArtType.Cover)
    {
        GameName = gameName;
        Raise(nameof(Title));
        _query = gameName;
        Raise(nameof(Query));
        _type = type;
        _style = null;
        _animatedOnly = false;
        RaiseFilters();
        return SearchAsync();
    }

    public Task SetTypeAsync(int index)
    {
        var type = (SgdbArtType)Math.Clamp(index, 0, 4);
        if (type == _type) return Task.CompletedTask;
        _type = type;
        _style = null;
        RaiseFilters();
        return ReloadAsync();
    }

    public Task SetStyleAsync(string? style)
    {
        if (style == _style) return Task.CompletedTask;
        _style = style;
        RaiseFilters();
        return ReloadAsync();
    }

    public Task ToggleAnimatedAsync()
    {
        _animatedOnly = !_animatedOnly;
        RaiseFilters();
        return ReloadAsync();
    }

    /// بحث جديد بالاسم المكتوب (حين تكون اللعبة المطابقة خاطئة)
    public async Task SearchAsync()
    {
        var gen = ++_generation;
        Reset();
        IsLoading = true;
        try
        {
            var game = await _client().FindGameAsync(_query);
            if (gen != _generation) return;
            _gameId = game?.Id ?? 0;
            if (game == null) { Finish(gen); return; }
            await LoadFirstAsync(gen);
        }
        catch (Exception ex) when (gen == _generation) { Fail(ex); }
    }

    /// النوع أو الشكل تغيّر: نفس اللعبة، نتائج جديدة
    private async Task ReloadAsync()
    {
        if (_gameId == 0) { await SearchAsync(); return; }
        var gen = ++_generation;
        Reset();
        IsLoading = true;
        try { await LoadFirstAsync(gen); }
        catch (Exception ex) when (gen == _generation) { Fail(ex); }
    }

    private async Task LoadFirstAsync(int gen)
    {
        var client = _client();
        if (!_animatedOnly) _cursors.Add(new Cursor { Animated = false });
        _cursors.Add(new Cursor { Animated = true });

        // الصفحة الأولى من كل قسم معًا: العدد الكلي من البداية، والمتحركة جاهزة بعد الثابتة
        var pages = await Task.WhenAll(_cursors.Select(c => client.GetImagesAsync(_gameId, _type, _style, c.Animated, 0)));
        if (gen != _generation) return;
        for (var i = 0; i < _cursors.Count; i++)
        {
            _cursors[i].Total = pages[i].Total;
            _cursors[i].NextPage = 1;
            _cursors[i].Buffered = pages[i].Items.ToList();
            if (pages[i].Items.Count == 0) _cursors[i].Done = true;
        }
        Total = _cursors.Sum(c => c.Total);
        AppendNext();
        Finish(gen);
    }

    /// تُستدعى عند الاقتراب من أسفل الشبكة
    public async Task LoadMoreAsync()
    {
        if (IsLoading || HasError || !HasMore) return;
        var gen = _generation;
        if (AppendNext()) { Raise(nameof(HasMore)); return; }

        var cursor = _cursors.First(c => !c.Done);
        IsLoading = true;
        try
        {
            var page = await _client().GetImagesAsync(_gameId, _type, _style, cursor.Animated, cursor.NextPage);
            if (gen != _generation) return;
            cursor.NextPage++;
            if (page.Items.Count == 0) cursor.Done = true;
            else cursor.Buffered = page.Items.ToList();
            AppendNext();
            Finish(gen);
        }
        catch (Exception ex) when (gen == _generation) { Fail(ex); }
    }

    /// يضيف صفحة محمّلة مسبقًا إن وُجدت (الثابتة أولًا ثم المتحركة)
    private bool AppendNext()
    {
        foreach (var c in _cursors)
        {
            if (c.Buffered is { Count: > 0 } items)
            {
                foreach (var img in items) Tiles.Add(new SgdbTile { Image = img, Type = _type });
                c.Loaded += items.Count;
                c.Buffered = null;
                if (c.Loaded >= c.Total) c.Done = true;
                return true;
            }
            if (!c.Done) return false;
        }
        return false;
    }

    private void Reset()
    {
        _cursors.Clear();
        Tiles.Clear();
        ErrorKey = null;
        Total = 0;
        _searched = false;
    }

    private void Finish(int gen)
    {
        if (gen != _generation) return;
        _searched = true;
        IsLoading = false;
        Raise(nameof(HasMore));
    }

    private void Fail(Exception ex)
    {
        Log.Error("SteamGridDB browse failed", ex);
        _searched = true;
        ErrorKey = ex is HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden } ? "sgKeyBad" : "sgNoNet";
        IsLoading = false;
        Raise(nameof(HasMore));
    }

    /// بعد تغيير اللغة
    public void RefreshTexts()
    {
        Raise(nameof(Title));
        Raise(nameof(CountText));
        RaiseFilters();
        var items = Tiles.ToList();
        Tiles.Clear();
        foreach (var t in items) Tiles.Add(t);
        Raise(nameof(Error));
    }

    private void RaiseFilters()
    {
        Raise(nameof(Type));
        Raise(nameof(TypeIndex));
        Raise(nameof(Style));
        Raise(nameof(AnimatedOnly));
        Raise(nameof(StyleChips));
        Raise(nameof(TypeChips));
    }

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    public event PropertyChangedEventHandler? PropertyChanged;
}
