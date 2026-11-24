using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace HalawaHub.App.BigPicture;

public sealed partial class BigPictureViewModel
{
    public const int LibraryColumns = 7;
    private const int RowLimit = 15;
    private static readonly string[] StripIds = { "cont", "favs", "installed", "recent" };
    private static readonly string[] PlatformOrder = { "Steam", "Epic", "GOG", "Xbox", "Riot", "EA", "Ubisoft", "Rockstar" };

    // ===== الرئيسي =====

    private int _homeRow = 2;   // 0 أزرار البطل، 1 شريط التبويبات، 2 البطاقات
    private int _heroIndex;
    private int _strip;
    private bool _stripForward = true;
    private int _cardIndex;
    private BpGame? _hero;

    public ObservableCollection<BpButtonItem> HeroButtons { get; } = new();
    public ObservableCollection<BpButtonItem> StripTabs { get; } = new();
    public ObservableCollection<BpCardItem> RowCards { get; } = new();
    public BpButtonItem MoreCard { get; } = new() { FocusKey = "more", Id = "more" };

    public int HomeRow => _homeRow;
    public int StripIndex => _strip;
    public bool StripForward => _stripForward;
    public int CardIndex => _cardIndex;
    /// يزيد مع كل تغيير للعبة البطل، لتبديل حركة الدخول
    public int HeroVersion { get; private set; }
    /// يزيد مع تغيير قائمة البطاقات
    public int RowVersion { get; private set; }

    /// اللعبة المعروضة في الأعلى: البطاقة المركَّزة، أو آخر لعبة رُكّز عليها
    /// الحجم يُحسب في الخلفية؛ ربط Hero.SizeShown لا يتحدث لأن Hero نفس الكائن، فالنص هنا يتغير ويُعاد قراءته
    public string HeroSize => _hero?.SizeShown ?? "";

    public BpGame? Hero
    {
        get => _hero;
        private set
        {
            if (ReferenceEquals(_hero, value)) return;
            _hero = value;
            HeroVersion++;
            RefreshHeroLabels();
            OnHeroChangedForSide();
        }
    }

    public bool HasHero => _hero != null;
    /// صورة اللعبة الصغيرة في صفحة اللعبة تكرر الخلفية حين للعبة صورة عريضة؛ تظهر فقط حين لا توجد
    public bool ShowShowcase => _hero is { HeroUrl: null };
    public bool HeroHasNoArt => _hero is { HeroUrl: null } && _hero.CoverUrl != null;
    public bool HeroIsFavorite => _hero?.IsFavorite == true;

    /// إزاحة صف البطاقات: تبقى البطاقة المركَّزة رابعة من البداية ما أمكن
    public int RowOffsetCards => Math.Clamp(_cardIndex - 3, 0, Math.Max(0, RowCards.Count + 1 - 7));

    private void BuildHome()
    {
        // التصميم الجديد: لا أزرار في أعلى الرئيسي (التشغيل والمفضلة في صفحة اللعبة، وX على البطاقة يشغّلها)
        HeroButtons.Clear();

        StripTabs.Clear();
        for (var i = 0; i < StripIds.Length; i++)
            StripTabs.Add(new BpButtonItem { FocusKey = "strip." + i, Id = StripIds[i], IsOn = i == _strip });

        RefreshRow();
        Hero = RowCards.FirstOrDefault()?.Game ?? _host.Games.FirstOrDefault();
        RefreshHomeLabels();
    }

    /// النصوص تتبع اللغة
    public void RefreshHomeLabels()
    {
        StripTabs[0].Label = T("bpCont");
        StripTabs[1].Label = T("bpFavs");
        StripTabs[2].Label = T("bpInstalled");
        StripTabs[3].Label = T("bpRecent");
        MoreCard.Label = T("bpSeeAll");
        RefreshHeroLabels();
    }

    private void RefreshHeroLabels()
    {
        if (HeroButtons.Count < 3) return;
        HeroButtons[0].Label = T("bpPlay");
        HeroButtons[1].Label = T("bpDetails");
        HeroButtons[2].Label = T(HeroIsFavorite ? "bpInFav" : "bpAddFav");
        HeroButtons[2].IsOn = HeroIsFavorite;
        if (GameButtons.Count > 0) BuildGameButtons();
    }

    internal IReadOnlyList<BpGame> StripList(int strip)
    {
        var games = _host.Games;
        IEnumerable<BpGame> list = StripIds[strip] switch
        {
            "cont" => games.Where(g => g.LastPlayed != null).OrderByDescending(g => g.LastPlayed),
            "favs" => games.Where(g => g.IsFavorite).OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase),
            "installed" => games.Where(g => g.IsInstalled).OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase),
            _ => games.OrderByDescending(g => g.DateAdded),
        };
        return list.Take(RowLimit).ToList();
    }

    private void RefreshRow()
    {
        RowCards.Clear();
        var list = StripList(_strip);
        for (var i = 0; i < list.Count; i++)
            RowCards.Add(new BpCardItem { FocusKey = "card." + i, Game = list[i], Index = i, Delay = TimeSpan.FromMilliseconds(i * 45) });
        RowVersion++;
    }

    private void PickStrip(int index)
    {
        index = Math.Clamp(index, 0, StripIds.Length - 1);
        if (index == _strip) return;
        _stripForward = index > _strip;
        _strip = index;
        for (var i = 0; i < StripTabs.Count; i++) StripTabs[i].IsOn = i == _strip;
        _cardIndex = 0;
        RefreshRow();
    }

    private void NavHome(int dx, int dy)
    {
        if (dy != 0)
        {
            _homeRow = Math.Clamp(_homeRow + dy, 1, 2);
            return;
        }

        switch (_homeRow)
        {
            case 0: _heroIndex = Math.Clamp(_heroIndex + dx, 0, HeroButtons.Count - 1); break;
            case 1: PickStrip(_strip + dx); break;
            default:
                _cardIndex = Math.Clamp(_cardIndex + dx, 0, RowCards.Count);
                if (_cardIndex < RowCards.Count) Hero = RowCards[_cardIndex].Game;
                break;
        }
    }

    private void ActHome()
    {
        switch (_homeRow)
        {
            case 0: RunHeroButton(_heroIndex); break;
            case 1: _homeRow = 2; break;
            default:
                if (_cardIndex >= RowCards.Count) GoTab(BpScreen.Library);
                else OpenGame(RowCards[_cardIndex].Game);
                break;
        }
    }

    private void RunHeroButton(int index)
    {
        if (_hero is not { } g) return;
        switch (index)
        {
            case 0: Play(g); break;
            case 1: OpenGame(g); break;
            case 2: ToggleFavorite(g); break;
        }
    }

    /// النقر بالفأرة: يركّز ثم ينفذ كما في التصميم
    public void ClickHeroButton(int index)
    {
        _homeRow = 0;
        _heroIndex = index;
        RunHeroButton(index);
        SyncFocus();
    }

    public void ClickStrip(int index)
    {
        _homeRow = 1;
        PickStrip(index);
        SyncFocus();
    }

    public void ClickCard(int index)
    {
        if (_homeRow == 2 && _cardIndex == index)
        {
            if (index >= RowCards.Count) GoTab(BpScreen.Library);
            else OpenGame(RowCards[index].Game);
        }
        else
        {
            _homeRow = 2;
            _cardIndex = Math.Clamp(index, 0, RowCards.Count);
            if (_cardIndex < RowCards.Count) Hero = RowCards[_cardIndex].Game;
        }
        SyncFocus();
    }

    private void ToggleFavorite(BpGame g)
    {
        _host.ToggleFavorite(g);
        RefreshHeroLabels();
        if (StripIds[_strip] == "favs") { RefreshRow(); _cardIndex = Math.Min(_cardIndex, RowCards.Count); }
    }

    /// فحص جديد للمكتبة والشاشة مفتوحة: نعيد القوائم ونبقي مكان التركيز ما أمكن
    public void Reload()
    {
        var heroKey = _hero?.Key;
        RefreshRow();
        _cardIndex = Math.Min(_cardIndex, RowCards.Count);
        BuildLibraryKeepingFilter();
        RefreshResults();
        var hero = _host.Games.FirstOrDefault(g => g.Key == heroKey) ?? RowCards.FirstOrDefault()?.Game ?? _host.Games.FirstOrDefault();
        if (!ReferenceEquals(hero, _hero)) Hero = hero;
        Changed();
    }

    // ===== المكتبة =====

    private string _platform = "all";
    private bool _platformForward = true;
    private int _libIndex;

    public ObservableCollection<BpFilterItem> Filters { get; } = new();
    public ObservableCollection<BpCardItem> LibraryCards { get; } = new();
    /// صفوف من 7 بطاقات: قائمة الصفوف افتراضية، فالمكتبة الكبيرة لا تُبنى كلها
    public ObservableCollection<BpCardRow> LibraryRows { get; } = new();
    public int LibraryRow => _libIndex / LibraryColumns;
    /// الصفوف الأربعة الظاهرة، والصف الأول منها: الصف المركَّز يبقى ثاني الصفوف ما أمكن
    public BpRowSlot[] LibraryWindow { get; } = { new(), new(), new(), new() };
    public int LibraryFirstRow => Math.Max(0, Math.Min(LibraryRow - 1, Math.Max(0, LibraryRows.Count - 2)));

    private void UpdateLibraryWindow()
    {
        var first = LibraryFirstRow;
        for (var i = 0; i < LibraryWindow.Length; i++)
            LibraryWindow[i].Row = first + i < LibraryRows.Count ? LibraryRows[first + i] : null;
    }
    public int LibraryIndex => _libIndex;
    public int FilterIndex => Math.Max(0, Filters.ToList().FindIndex(f => f.Id == _platform));
    public bool FilterForward => _platformForward;
    public int LibraryVersion { get; private set; }
    public string LibraryCountText => BpPlural.Games(LibraryCards.Count);
    public BpGame? FocusedLibraryGame => _libIndex < LibraryCards.Count ? LibraryCards[_libIndex].Game : null;

    private void BuildLibrary()
    {
        Filters.Clear();
        var all = _host.Games;
        Filters.Add(new BpFilterItem { FocusKey = "flt.all", Id = "all", Label = T("bpAll"), Count = all.Count, IsOn = _platform == "all" });
        var groups = all.GroupBy(PlatformOf)
            .OrderBy(g => Array.IndexOf(PlatformOrder, g.Key) is var i && i >= 0 ? i : PlatformOrder.Length)
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase);
        foreach (var g in groups)
            Filters.Add(new BpFilterItem { FocusKey = "flt." + g.Key, Id = g.Key, Label = g.Key, Count = g.Count(), IsOn = _platform == g.Key });
        if (Filters.All(f => !f.IsOn)) { _platform = "all"; Filters[0].IsOn = true; }
        RefreshLibrary();
    }

    internal static string PlatformOf(BpGame g) => string.IsNullOrEmpty(g.PlatformShort) ? g.Platform : g.PlatformShort;

    private void RefreshLibrary()
    {
        LibraryCards.Clear();
        var list = _host.Games
            .Where(g => _platform == "all" || PlatformOf(g) == _platform)
            .OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        for (var i = 0; i < list.Count; i++)
            LibraryCards.Add(new BpCardItem { FocusKey = "lib." + i, Game = list[i], Index = i, Delay = TimeSpan.FromMilliseconds(Math.Min(i, 20) * 30) });
        LibraryRows.Clear();
        for (var r = 0; r < LibraryCards.Count; r += LibraryColumns)
            LibraryRows.Add(new BpCardRow(LibraryCards.Skip(r).Take(LibraryColumns).ToList()));
        _libIndex = Math.Clamp(_libIndex, 0, Math.Max(0, LibraryCards.Count - 1));
        LibraryVersion++;
    }

    public void SetPlatform(string id)
    {
        var next = Filters.ToList().FindIndex(f => f.Id == id);
        if (next < 0 || id == _platform) return;
        _platformForward = next > FilterIndex;
        _platform = id;
        foreach (var f in Filters) f.IsOn = f.Id == id;
        _libIndex = 0;
        RefreshLibrary();
        SyncFocus();
    }

    private void CyclePlatform()
    {
        if (Filters.Count == 0) return;
        SetPlatform(Filters[(FilterIndex + 1) % Filters.Count].Id);
    }

    /// التركيز على شريط المنصات فوق الشبكة (فوق من الصف الأول، كما في Steam)
    private bool _onFilters;
    public bool LibraryOnFilters => _onFilters;

    private void NavLibrary(int dx, int dy)
    {
        if (_onFilters)
        {
            if (dy > 0 && LibraryCards.Count > 0) { _onFilters = false; return; }
            if (dx != 0)
            {
                var f = Math.Clamp(FilterIndex + dx, 0, Filters.Count - 1);
                if (f != FilterIndex) SetPlatform(Filters[f].Id);
            }
            return;
        }

        var i = _libIndex + dx;
        if (dx != 0 && i / LibraryColumns != _libIndex / LibraryColumns) i = _libIndex;
        if (dx != 0 && i < 0) i = _libIndex;
        if (dy < 0 && _libIndex < LibraryColumns && Filters.Count > 1) { _onFilters = true; return; }
        if (dy != 0) i = _libIndex + dy * LibraryColumns;
        // نزولًا إلى صف أخير أقصر: آخر لعبة فيه
        if (dy > 0 && i >= LibraryCards.Count && _libIndex / LibraryColumns < (LibraryCards.Count - 1) / LibraryColumns) i = LibraryCards.Count - 1;
        if (i >= 0 && i < LibraryCards.Count) _libIndex = i;
    }

    public void ClickLibraryCard(int index)
    {
        if (index == _libIndex && FocusedLibraryGame is { } g) OpenGame(g);
        else _libIndex = Math.Clamp(index, 0, Math.Max(0, LibraryCards.Count - 1));
        SyncFocus();
    }

    // ===== صفحة اللعبة =====

    private int _gameCol;
    private int _gameIndex;

    public ObservableCollection<BpButtonItem> GameButtons { get; } = new();
    public ObservableCollection<BpButtonItem> NewsItems { get; } = new();

    private void BuildGameButtons()
    {
        if (_hero is not { } g) return;
        var keep = _gameIndex;
        GameButtons.Clear();
        var i = 0;
        GameButtons.Add(new BpButtonItem { FocusKey = "game." + i++, Id = "play", Label = T("bpPlay"), IsPrimary = true });
        if (g.HasUpdate) GameButtons.Add(new BpButtonItem { FocusKey = "game." + i++, Id = "update", Label = T("bpUpdNow") });
        GameButtons.Add(new BpButtonItem { FocusKey = "game." + i++, Id = "fav", Label = T(g.IsFavorite ? "bpInFav" : "bpAddFav"), IsOn = g.IsFavorite });
        GameButtons.Add(new BpButtonItem { FocusKey = "game." + i, Id = "settings", Label = T("bpGameSettings") });
        _gameIndex = Math.Clamp(keep, 0, GameButtons.Count - 1);
    }

    /// أخبار Steam للعبة المفتوحة (تملؤها الواجهة بعد تحميلها)
    public void SetNews(IEnumerable<(string Title, string Date, string Url)> news)
    {
        NewsItems.Clear();
        var i = 0;
        // صفحة اللعبة تعرض خبرين (التصميم)
        foreach (var (title, date, url) in news.Take(2))
            NewsItems.Add(new BpButtonItem { FocusKey = "news." + i++, Id = url, Label = title, Note = date });
        if (_gameCol == 1 && NewsItems.Count == 0) _gameCol = 0;
        SyncFocus();
    }

    private void NavGame(int dx, int dy)
    {
        if (dx != 0)
        {
            var col = dx > 0 ? 1 : 0;
            if (col == 1 && NewsItems.Count == 0) return;
            if (col != _gameCol) { _gameCol = col; _gameIndex = 0; }
            return;
        }
        var count = _gameCol == 0 ? GameButtons.Count : NewsItems.Count;
        _gameIndex = Math.Clamp(_gameIndex + dy, 0, Math.Max(0, count - 1));
    }

    private void ActGame()
    {
        if (_hero is not { } g) return;
        if (_gameCol == 1) { if (_gameIndex < NewsItems.Count) _host.OpenNews(g, NewsItems[_gameIndex].Id); ShowToast(T("bpNewsOpened")); return; }
        RunGameButton(_gameIndex);
    }

    private void RunGameButton(int index)
    {
        if (_hero is not { } g || index >= GameButtons.Count) return;
        switch (GameButtons[index].Id)
        {
            case "play": Play(g); break;
            case "update": _host.OpenUpdateInLauncher(g); ShowToast(F("bpOpenedSteam", g.Name)); break;
            case "fav": ToggleFavorite(g); break;
            case "settings": OpenGameSettings(g); break;
        }
    }

    public void ClickGameButton(int index)
    {
        _gameCol = 0;
        _gameIndex = index;
        RunGameButton(index);
        SyncFocus();
    }

    public void ClickNews(int index)
    {
        _gameCol = 1;
        _gameIndex = index;
        ActGame();
        SyncFocus();
    }

    // ===== البحث =====

    public static readonly string[][] KeysEn =
    {
        new[] { "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P" },
        new[] { "A", "S", "D", "F", "G", "H", "J", "K", "L" },
        new[] { "Z", "X", "C", "V", "B", "N", "M" },
    };
    public static readonly string[][] KeysAr =
    {
        new[] { "ض", "ص", "ث", "ق", "ف", "غ", "ع", "ه", "خ", "ح", "ج" },
        new[] { "ش", "س", "ي", "ب", "ل", "ا", "ت", "ن", "م", "ك", "ط" },
        new[] { "ئ", "ء", "ؤ", "ر", "ى", "ة", "و", "ز", "ظ", "د" },
    };
    private static readonly string[] SpecialKeys = { "space", "del", "abc", "done" };

    private string _query = "";
    private string _kbLang = "en";
    private int _kbZone;   // 0 لوحة المفاتيح، 1 النتائج
    private int _kbRow, _kbCol, _resIndex;

    public ObservableCollection<ObservableCollection<BpButtonItem>> KeyRows { get; } = new();
    public ObservableCollection<BpCardItem> Results { get; } = new();
    public string Query => _query;
    public bool HasQuery => _query.Length > 0;
    public string QueryShown => _query.Length > 0 ? _query : T("bpSearchPh");
    public Avalonia.Media.FlowDirection KeyboardDirection => _kbLang == "ar" ? Avalonia.Media.FlowDirection.RightToLeft : Avalonia.Media.FlowDirection.LeftToRight;
    public string KeyboardLanguage => _kbLang;
    public bool NoMatch => _query.Trim().Length > 0 && Results.Count == 0;
    public string NoMatchText => F("bpNoResFor", _query.Trim());
    public int ResultsVersion { get; private set; }

    private void BuildSearch()
    {
        _kbLang = IsRtl ? "ar" : "en";
        BuildKeys();
        RefreshResults();
    }

    private void BuildKeys()
    {
        KeyRows.Clear();
        var letters = _kbLang == "ar" ? KeysAr : KeysEn;
        for (var r = 0; r < letters.Length; r++)
        {
            var row = new ObservableCollection<BpButtonItem>();
            for (var c = 0; c < letters[r].Length; c++)
                row.Add(new BpButtonItem { FocusKey = $"kb.{r}.{c}", Id = letters[r][c], Label = letters[r][c] });
            KeyRows.Add(row);
        }
        var special = new ObservableCollection<BpButtonItem>();
        for (var c = 0; c < SpecialKeys.Length; c++)
            special.Add(new BpButtonItem { FocusKey = $"kb.3.{c}", Id = SpecialKeys[c], IsPrimary = SpecialKeys[c] == "done" });
        KeyRows.Add(special);
        RefreshKeyLabels();
    }

    public void RefreshKeyLabels()
    {
        var special = KeyRows[3];
        special[0].Label = T("bpSpace");
        special[1].Label = "⌫";
        special[2].Label = _kbLang == "ar" ? "EN" : "ع";
        special[3].Label = T("bpDone");
    }

    private int RowLength(int r) => KeyRows[r].Count;

    internal void SetQuery(string q)
    {
        _query = q;
        RefreshResults();
        Changed();
    }

    private void RefreshResults()
    {
        Results.Clear();
        var q = _query.Trim();
        IEnumerable<BpGame> list = q.Length > 0
            ? _host.Games.Where(g => g.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase)).OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).Take(5)
            : _host.Games.OrderByDescending(g => g.LastPlayed ?? DateTime.MinValue).Take(4);
        var i = 0;
        foreach (var g in list)
            Results.Add(new BpCardItem { FocusKey = "res." + i, Game = g, Index = i, Delay = TimeSpan.FromMilliseconds(50 + i++ * 70) });
        if (_kbZone == 1 && Results.Count == 0) _kbZone = 0;
        _resIndex = Math.Clamp(_resIndex, 0, Math.Max(0, Results.Count - 1));
        ResultsVersion++;
    }

    private void TypeKey(string id)
    {
        switch (id)
        {
            case "space": SetQuery(_query + " "); break;
            case "del": if (_query.Length > 0) SetQuery(_query[..^1]); break;
            case "abc":
                _kbLang = _kbLang == "ar" ? "en" : "ar";
                BuildKeys();
                _kbRow = 3; _kbCol = 2;
                break;
            case "done": if (Results.Count > 0) { _kbZone = 1; _resIndex = 0; } break;
            default: SetQuery(_query + id.ToLowerInvariant()); break;
        }
    }

    /// <summary>
    /// dx منطقي باتجاه الواجهة (موجب = نحو النتائج). لوحة المفاتيح لها اتجاه حروفها: حروف
    /// إنجليزية داخل الواجهة العربية تُرسم من اليسار، فالسهم الأيسر ينقص العمود لا يزيده.
    /// </summary>
    private void NavSearch(int dx, int dy)
    {
        var flipped = (_kbLang == "ar") != IsRtl;
        if (_kbZone == 1)
        {
            if (dx < 0) { _kbZone = 0; _kbCol = flipped ? 0 : RowLength(_kbRow) - 1; return; }
            _resIndex = Math.Clamp(_resIndex + dy, 0, Math.Max(0, Results.Count - 1));
            return;
        }
        if (dy != 0)
        {
            _kbRow = Math.Clamp(_kbRow + dy, 0, 3);
            _kbCol = Math.Clamp(_kbCol, 0, RowLength(_kbRow) - 1);
            return;
        }
        var c = _kbCol + (flipped ? -dx : dx);
        if (c < 0 || c > RowLength(_kbRow) - 1)
        {
            if (dx > 0 && Results.Count > 0) { _kbZone = 1; _resIndex = 0; }
            return;
        }
        _kbCol = c;
    }

    private void ActSearch()
    {
        if (_kbZone == 1) { if (_resIndex < Results.Count) OpenGame(Results[_resIndex].Game); return; }
        TypeKey(KeyRows[_kbRow][_kbCol].Id);
    }

    public void ClickKey(int row, int col)
    {
        _kbZone = 0; _kbRow = row; _kbCol = col;
        TypeKey(KeyRows[row][col].Id);
        SyncFocus();
    }

    public void ClickResult(int index)
    {
        if (index < Results.Count) OpenGame(Results[index].Game);
        SyncFocus();
    }
}
