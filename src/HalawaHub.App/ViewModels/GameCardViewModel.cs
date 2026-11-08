using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using HalawaHub.App.Services;
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

    public void RefreshPlayTime()
    {
        OnPropertyChanged(nameof(PlayTimeText));
        OnPropertyChanged(nameof(HasPlayTime));
    }

    /// تُستدعى بعد ما تشتغل اللعبة فعليًا — تسجّل وقت التشغيل الحالي
    public void NotifyPlayed()
    {
        PlayHistoryService.RecordPlayed(Game.Platform, Game.Id);
        LastPlayed = DateTime.UtcNow;
        OnPropertyChanged(nameof(LastPlayed));
        OnPropertyChanged(nameof(LastPlayedText));
    }

    /// النصوص المحسوبة تتغير مع اللغة، أما الروابط الثابتة فتتحدث وحدها عبر Loc
    public void NotifyLanguageChanged()
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(LastPlayedText));
        OnPropertyChanged(nameof(PlayTimeText));
        OnPropertyChanged(nameof(FavoriteLabel));
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
    public string PlatformShort => ShortenPlatform(Game.Platform);

    public static string ShortenPlatform(string platform) => platform switch
    {
        "Epic Games" => "Epic",
        "Riot Games" => "Riot",
        "Xbox / Microsoft Store" => "Xbox",
        _ => platform
    };

    public string StatusText => Loc.Instance[Game.IsInstalled ? "statusInstalled" : "statusNotInstalled"];
    public bool IsInstalled => Game.IsInstalled;
    public bool HasTools => Tools.Count > 0;

    // ===== الملفات =====

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
    public async void EnsureSizeRequested()
    {
        if (_sizeRequested) return;
        _sizeRequested = true;
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

    private bool _isMenuOpen;
    public bool IsMenuOpen
    {
        get => _isMenuOpen;
        set
        {
            if (_isMenuOpen == value) return;
            _isMenuOpen = value;
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
