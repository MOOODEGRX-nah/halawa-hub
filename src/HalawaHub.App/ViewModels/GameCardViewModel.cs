using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using HalawaHub.App.Services;
using HalawaHub.Core.Library;
using HalawaHub.Core.Models;
using HalawaHub.Core.Plugins;

namespace HalawaHub.App.ViewModels;

/// <summary>
/// يغلّف لعبة واحدة (GameInfo) مع قائمة الأدوات المنطبقة عليها، حالة
/// المفضلة، معاملات التشغيل المخصصة، وغلاف مخصص اختياري — كل شي محتاجه
/// كرت اللعبة ونافذة إعداداتها.
/// </summary>
public class GameCardViewModel : INotifyPropertyChanged
{
    public GameInfo Game { get; }
    public ObservableCollection<GameToolViewModel> Tools { get; } = new();

    private readonly string? _originalCoverUrl;
    private readonly string? _originalCoverFallback;

    public GameCardViewModel(GameInfo game, IEnumerable<IGameTool> allTools)
    {
        Game = game;
        _originalCoverUrl = game.CoverImageUrl;
        _originalCoverFallback = game.CoverImageUrlFallback;

        foreach (var tool in allTools.Where(t => t.AppliesTo(game)))
            Tools.Add(new GameToolViewModel(tool, game));

        _isFavorite = FavoritesService.IsFavorite(game.Platform, game.Id);
        _launchParameters = LaunchParamsService.Get(game.Platform, game.Id);
        LastPlayed = PlayHistoryService.GetLastPlayed(game.Platform, game.Id);
        DateAdded = FirstSeenService.Get(game);
        _displayName = GameOverridesService.GetDisplayName(game.Platform, game.Id);
        _editableName = _displayName ?? game.Name;
        _isHidden = GameOverridesService.IsHidden(game.Platform, game.Id);

        var customCover = CustomCoverService.Get(game.Platform, game.Id);
        if (!string.IsNullOrEmpty(customCover))
        {
            try
            {
                Game.CoverImageUrl = new Uri(customCover).AbsoluteUri;
                Game.CoverImageUrlFallback = null;
                HasCustomCover = true;
            }
            catch
            {
                // مسار محفوظ غير صالح (مثلاً انتقل الملف)، نتجاهله ونستخدم الغلاف الأصلي
            }
        }
    }

    public DateTime? LastPlayed { get; private set; }

    /// أول مرة رآها Halawa (للترتيب حسب "تاريخ الإضافة")
    public DateTime DateAdded { get; }

    public TimeSpan PlayTimeTotal => PlayTimeService.GetTotal(Game.Platform, Game.Id);

    public string LastPlayedText
    {
        get
        {
            if (LastPlayed == null) return "";
            var span = DateTime.UtcNow - LastPlayed.Value;
            var l = Loc.Instance;
            if (span.TotalMinutes < 60) return l["lastPlayedMinutes"];
            if (span.TotalHours < 24) return l.Format("lastPlayedHours", (int)span.TotalHours);
            if (span.TotalDays < 2) return l["lastPlayedYesterday"];
            if (span.TotalDays < 7) return l.Format("lastPlayedDays", (int)span.TotalDays);
            return l.Format("lastPlayedWeeks", (int)(span.TotalDays / 7));
        }
    }

    public string PlayTimeText
    {
        get
        {
            var total = PlayTimeService.GetTotal(Game.Platform, Game.Id);
            var l = Loc.Instance;
            if (total.TotalSeconds < 5) return "";
            if (total.TotalMinutes < 1) return l["playedUnderMinute"];
            if (total.TotalHours < 1) return l.Format("playedMinutes", (int)total.TotalMinutes);
            if (total.TotalHours < 100) return l.Format("playedHours", (int)total.TotalHours);
            return l["playedLots"];
        }
    }

    public bool HasPlayTime => PlayTimeService.GetTotal(Game.Platform, Game.Id).TotalSeconds >= 5;

    // ===== الحالة: تحديث، إصلاح، تعمل، القرص غير موصول =====

    private GameStatus _status;

    public GameStatus Status
    {
        get => _status;
        set
        {
            if (_status == value) return;
            _status = value;
            NotifyStatusChanged();
        }
    }

    public bool HasUpdate => _status.HasFlag(GameStatus.UpdateAvailable);
    public bool IsUpdating => _status.HasFlag(GameStatus.Updating);
    public bool NeedsRepair => _status.HasFlag(GameStatus.NeedsRepair);
    public bool IsRunning => _status.HasFlag(GameStatus.Running);
    public bool IsDriveMissing => _status.HasFlag(GameStatus.DriveNotConnected);

    /// شارة واحدة على البطاقة، بالأولوية: القرص، تعمل، إصلاح، تحديث
    public string StatusBadgeText => Loc.Instance[
        !Game.IsInstalled ? "statusNotInstalled" : IsDriveMissing ? "stDrive" : IsRunning ? "stRunning" : NeedsRepair ? "stRepair" : IsUpdating ? "stUpdating" : HasUpdate ? "stUpdate" : "stNone"];

    public bool HasStatusBadge => _status != GameStatus.None || !Game.IsInstalled;

    /// كل الحالات للنافذة، مفصولة بنقطة
    public string StatusDetailsText
    {
        get
        {
            var l = Loc.Instance;
            var parts = new System.Collections.Generic.List<string>();
            if (IsDriveMissing) parts.Add(l["stDrive"]);
            if (IsRunning) parts.Add(l["stRunning"]);
            if (NeedsRepair) parts.Add(l["stRepair"]);
            if (IsUpdating) parts.Add(l["stUpdating"]);
            if (HasUpdate) parts.Add(l["stUpdate"]);
            return parts.Count == 0 ? l["stOk"] : string.Join(" · ", parts);
        }
    }

    /// لون الشارة: 0 رمادي (القرص)، 1 أخضر (تعمل)، 2 أحمر (إصلاح)، 3 كهرماني (تحديث)
    public int StatusTone => !Game.IsInstalled || IsDriveMissing ? 0 : IsRunning ? 1 : NeedsRepair ? 2 : HasStatusBadge ? 3 : 4;

    public bool HasStatusActions => CanOpenInLauncher || CanVerify;

    /// ما يعرفه Halawa عن تحديثات هذه المنصة، بصدق
    public string UpdateNoteText => Loc.Instance.Format(Game.Platform switch
    {
        "Steam" => "updNoteSteam",
        "Epic Games" => "updNoteEpic",
        ManualGames.Platform => "updNoteManual",
        _ => CanOpenInLauncher ? "updNoteOther" : "updNoteNone"
    }, LauncherActions.LauncherName(Game.Platform));

    public bool CanOpenInLauncher => LauncherActions.System.OpenInLauncher(Game) != null;
    public bool CanVerify => LauncherActions.System.Verify(Game) != null;
    public string OpenInLauncherLabel => Loc.Instance.Format(Game.Platform == "Steam" && HasUpdate ? "updateInLauncher" : "openInLauncher", LauncherActions.LauncherName(Game.Platform));

    private void NotifyStatusChanged()
    {
        foreach (var name in new[] { nameof(Status), nameof(HasUpdate), nameof(IsUpdating), nameof(NeedsRepair), nameof(IsRunning),
                     nameof(IsDriveMissing), nameof(StatusBadgeText), nameof(HasStatusBadge), nameof(StatusDetailsText), nameof(StatusTone),
                     nameof(OpenInLauncherLabel), nameof(UninstallLabel), nameof(UpdateNoteText) })
            OnPropertyChanged(name);
    }

    private bool _showStats = true;

    /// إعداد "وقت اللعب على البطاقات" (تضبطه MainViewModel)
    public bool ShowStats
    {
        get => _showStats;
        set
        {
            if (_showStats == value) return;
            _showStats = value;
            OnPropertyChanged(nameof(ShowStats));
            OnPropertyChanged(nameof(HasStatsLine));
        }
    }

    /// سطر البطاقة: "12 ساعة · قبل 3 أيام"
    public string StatsText => string.Join(" · ", new[] { PlayTimeText, LastPlayedText }.Where(s => !string.IsNullOrEmpty(s)));

    public bool HasStatsLine => _showStats && StatsText.Length > 0;

    public bool HasLastPlayed => LastPlayed != null;

    private void NotifyStats()
    {
        OnPropertyChanged(nameof(StatsText));
        OnPropertyChanged(nameof(HasStatsLine));
        OnPropertyChanged(nameof(HasLastPlayed));
    }

    public void RefreshPlayTime()
    {
        OnPropertyChanged(nameof(PlayTimeText));
        OnPropertyChanged(nameof(HasPlayTime));
        NotifyStats();
    }

    /// تُستدعى بعد ما تشتغل اللعبة فعليًا — تسجّل وقت التشغيل الحالي
    public void NotifyPlayed()
    {
        PlayHistoryService.RecordPlayed(Game.Platform, Game.Id);
        LastPlayed = DateTime.UtcNow;
        OnPropertyChanged(nameof(LastPlayed));
        OnPropertyChanged(nameof(LastPlayedText));
        NotifyStats();
    }

    /// النصوص المحسوبة تتغير مع اللغة، أما الروابط الثابتة فتتحدث وحدها عبر Loc
    public void NotifyLanguageChanged()
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(LastPlayedText));
        NotifyStats();
        OnPropertyChanged(nameof(PlayTimeText));
        OnPropertyChanged(nameof(FavoriteLabel));
        OnPropertyChanged(nameof(PlatformShort));
        OnPropertyChanged(nameof(PlayLabel));
        NotifyStatusChanged();
        foreach (var tool in Tools) tool.NotifyLanguageChanged();
    }

    // ===== الاسم =====

    private string? _displayName;
    private string _editableName;

    /// الاسم الظاهر في المكتبة: المخصص إن وُجد، وإلا الأصلي من المنصة
    public string Name => _displayName ?? Game.Name;
    public string OriginalName => Game.Name;

    /// حقل التعديل منفصل عن Name: أثناء الكتابة قد يكون فارغًا مؤقتًا
    public string EditableName
    {
        get => _editableName;
        set
        {
            _editableName = value ?? "";
            var trimmed = _editableName.Trim();
            _displayName = trimmed.Length == 0 || trimmed == Game.Name ? null : trimmed;
            GameOverridesService.SetDisplayName(Game.Platform, Game.Id, _displayName);
            OnPropertyChanged(nameof(EditableName));
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(Letter));
        }
    }

    /// الحرف الكبير في البطاقة بدون غلاف
    public string Letter
    {
        get
        {
            var name = Name.TrimStart();
            return name.Length == 0 ? "?" : char.ToUpperInvariant(name[0]).ToString();
        }
    }

    /// رقم تدرج البطاقة، ثابت لكل لعبة
    public int PaletteIndex
    {
        get
        {
            var hash = 0;
            foreach (var c in Game.Platform + "|" + Game.Id)
                hash = unchecked(hash * 31 + c);
            return Math.Abs(hash % 6);
        }
    }

    // ===== المنصة والحالة =====

    public string Platform => Game.Platform;

    /// اسم مختصر للشارة وشريط المنصات: "Xbox / Microsoft Store" يصير "Xbox"
    /// اسم المنصة، ومعه "+N" إن كانت اللعبة مدموجة من منصات أخرى
    public string PlatformShort => ShortenPlatform(Game.Platform) + (_alternates.Count > 0 ? " +" + _alternates.Count : "");

    private IReadOnlyList<GameCardViewModel> _alternates = Array.Empty<GameCardViewModel>();

    /// نسخ اللعبة نفسها على منصات أخرى (7.4)؛ تُعرض عند التشغيل
    public IReadOnlyList<GameCardViewModel> Alternates => _alternates;

    public void SetAlternates(IReadOnlyList<GameCardViewModel> alternates)
    {
        if (_alternates.Count == 0 && alternates.Count == 0) return;
        _alternates = alternates;
        OnPropertyChanged(nameof(Alternates));
        OnPropertyChanged(nameof(PlatformShort));
        OnPropertyChanged(nameof(PlayLabel));
    }

    /// "تشغيل"، أو "تثبيت" للعبة Steam مملوكة غير مثبتة
    public string PlayLabel => Loc.Instance[Game.IsInstalled ? "play" : "installGame"];

    public static string ShortenPlatform(string platform) => platform switch
    {
        "Epic Games" => "Epic",
        "Riot Games" => "Riot",
        "Xbox / Microsoft Store" => "Xbox",
        HalawaHub.Core.Library.ManualGames.Platform => Loc.Instance["platManual"],
        _ => platform
    };

    public string StatusText => Loc.Instance[Game.IsInstalled ? "statusInstalled" : "statusNotInstalled"];
    public bool IsInstalled => Game.IsInstalled;
    public bool HasTools => Tools.Count > 0;

    /// لعبة أضافها المستخدم: يمكن تغيير ملفها أو إزالتها من Halawa
    public bool IsManual => Game.Platform == ManualGames.Platform;

    /// تبويب الأخبار: ألعاب Steam فقط (ISteamNews)
    public bool HasNews => Game.Platform == "Steam" && !string.IsNullOrEmpty(Game.Id) && Game.Id.All(char.IsAsciiDigit);
    public string ManualTarget => Game.ExecutablePath;

    // ===== الملفات =====

    /// <summary>
    /// على لينكس نطلب إلغاء التثبيت من Steam فقط: Heroic وLutris لا يوفران بروتوكولًا
    /// له، فلا نعرض زرًا لا يعمل. سلوك ويندوز كما هو (رسالة توضيحية).
    /// </summary>
    public bool CanUninstall => Game.IsInstalled && LauncherActions.System.Uninstall(Game) != null;

    /// Epic: لا رابط للإزالة، فالزر يفتح المشغّل ويقول ذلك
    public string UninstallLabel => Loc.Instance[LauncherActions.System.Uninstall(Game) is { OpensLauncher: true } ? "uninstallInLauncher" : "uninstall"];

    /// <summary>
    /// الألعاب التي تبدأ برابط (steam:// وheroic:// وlutris:) لا تأخذ وسائط على لينكس:
    /// xdg-open يفتح الرابط فقط، فالحقل هنا لا أثر له.
    /// </summary>
    public bool SupportsLaunchParameters => !OperatingSystem.IsLinux() || !IsUrlLaunch(Game.ExecutablePath);

    internal static bool IsUrlLaunch(string executablePath) =>
        Uri.TryCreate(executablePath, UriKind.Absolute, out var uri) && !uri.IsFile;

    public string InstallPath => Game.InstallPath;
    public bool HasInstallPath => !string.IsNullOrWhiteSpace(Game.InstallPath);

    private string _sizeOnDiskText = "…";
    public string SizeOnDiskText
    {
        get => _sizeOnDiskText;
        private set
        {
            _sizeOnDiskText = value;
            OnPropertyChanged(nameof(SizeOnDiskText));
        }
    }

    private bool _sizeRequested;

    /// يبدأ حساب الحجم في الخلفية عند فتح قسم الملفات أول مرة
    public void EnsureSizeRequested() => _ = EnsureSizeAsync();

    /// يكتمل بعد حساب الحجم (عرض القائمة يحسب الأحجام واحدًا تلو الآخر)
    public async System.Threading.Tasks.Task EnsureSizeAsync()
    {
        if (_sizeRequested) return;
        _sizeRequested = true;
        // مجلد عام (مجلد Windows لاختصار يدوي مثلًا): حجمه ليس حجم اللعبة، وحسابه بطيء جدًا
        var dir = HalawaHub.Core.Sessions.GameFolderMatcher.NormalizeDir(Game.InstallPath);
        if (dir == null || HalawaHub.Core.Sessions.GameFolderMatcher.IsTooBroad(dir, OperatingSystem.IsWindows()))
        {
            SizeOnDiskText = "—";
            return;
        }
        SizeOnDiskText = GameSizeService.Format(await GameSizeService.GetSizeAsync(Game.InstallPath));
    }

    // ===== المفضلة والإخفاء =====

    private bool _isFavorite;
    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            if (_isFavorite == value) return;
            _isFavorite = value;
            FavoritesService.SetFavorite(Game.Platform, Game.Id, value);
            OnPropertyChanged(nameof(IsFavorite));
            OnPropertyChanged(nameof(FavoriteLabel));
            FavoriteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string FavoriteLabel => Loc.Instance[IsFavorite ? "remFav" : "addFav"];

    public event EventHandler? FavoriteChanged;

    private bool _isHidden;
    public bool IsHidden
    {
        get => _isHidden;
        set
        {
            if (_isHidden == value) return;
            _isHidden = value;
            GameOverridesService.SetHidden(Game.Platform, Game.Id, value);
            OnPropertyChanged(nameof(IsHidden));
            HiddenChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? HiddenChanged;

    // ===== قائمة البطاقة =====

    private bool _menuBuilt;

    /// محتوى قائمة البطاقة: null حتى تُفتح أول مرة، فلا تُبنى أزرارها لكل بطاقة في المكتبة
    public GameCardViewModel? MenuContent => _menuBuilt ? this : null;

    private bool _isMenuOpen;
    public bool IsMenuOpen
    {
        get => _isMenuOpen;
        set
        {
            if (_isMenuOpen == value) return;
            _isMenuOpen = value;
            if (value && !_menuBuilt)
            {
                _menuBuilt = true;
                OnPropertyChanged(nameof(MenuContent));
            }
            OnPropertyChanged(nameof(IsMenuOpen));
        }
    }

    // ===== معاملات التشغيل =====

    private string _launchParameters;
    public string LaunchParameters
    {
        get => _launchParameters;
        set
        {
            _launchParameters = value ?? "";
            LaunchParamsService.Set(Game.Platform, Game.Id, _launchParameters);
            OnPropertyChanged(nameof(LaunchParameters));
        }
    }

    // ===== الغلاف =====

    public string? CoverImageUrl => Game.CoverImageUrl;
    public string? CoverImageUrlFallback => Game.CoverImageUrlFallback;
    public bool HasCoverImage => Game.HasCoverImage;

    private bool _hasCustomCover;
    public bool HasCustomCover
    {
        get => _hasCustomCover;
        private set
        {
            _hasCustomCover = value;
            OnPropertyChanged(nameof(HasCustomCover));
        }
    }

    // هل الغلاف متحرك؟ (ملف محلي بامتداد gif/webp/apng)
    public bool IsAnimatedCover
    {
        get
        {
            if (string.IsNullOrEmpty(CoverImageUrl)) return false;
            if (!CoverImageUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase)) return false;
            try
            {
                var path = new Uri(CoverImageUrl).LocalPath;
                var ext = System.IO.Path.GetExtension(path)?.ToLowerInvariant();
                return ext is ".gif" or ".webp" or ".apng";
            }
            catch { return false; }
        }
    }

    // مصدر الصورة المتحركة (null لو مش متحركة)
    public string? AnimatedCoverSource => IsAnimatedCover ? CoverImageUrl : null;

    /// تُستدعى لما يتوصّل رابط غلاف من مصدر خارجي (SteamGridDB) بعد التحميل الأولي
    public void SetCoverUrl(string url)
    {
        Game.CoverImageUrl = url;
        NotifyCoverChanged();
    }

    /// تُستدعى لما يختار المستخدم صورة غلاف مخصصة من جهازه
    public void SetCustomCover(string localFilePath)
    {
        CustomCoverService.Set(Game.Platform, Game.Id, localFilePath);

        Game.CoverImageUrl = new Uri(localFilePath).AbsoluteUri;
        Game.CoverImageUrlFallback = null;
        HasCustomCover = true;
        NotifyCoverChanged();
    }

    /// يحذف الغلاف المخصص ويرجع غلاف المنصة (أو التدرج لو لا يوجد غلاف)
    public void RestoreOriginalCover()
    {
        CustomCoverService.Remove(Game.Platform, Game.Id);

        Game.CoverImageUrl = _originalCoverUrl;
        Game.CoverImageUrlFallback = _originalCoverFallback;
        HasCustomCover = false;
        NotifyCoverChanged();
    }

    private void NotifyCoverChanged()
    {
        OnPropertyChanged(nameof(CoverImageUrl));
        OnPropertyChanged(nameof(CoverImageUrlFallback));
        OnPropertyChanged(nameof(HasCoverImage));
        OnPropertyChanged(nameof(IsAnimatedCover));
        OnPropertyChanged(nameof(AnimatedCoverSource));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
