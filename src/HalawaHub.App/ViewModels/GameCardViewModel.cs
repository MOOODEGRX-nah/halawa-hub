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
/// كرت اللعبة وصفحة تفاصيلها.
/// </summary>
public class GameCardViewModel : INotifyPropertyChanged
{
    public GameInfo Game { get; }
    public ObservableCollection<GameToolViewModel> Tools { get; } = new();

    public GameCardViewModel(GameInfo game, IEnumerable<IGameTool> allTools)
    {
        Game = game;

        foreach (var tool in allTools.Where(t => t.AppliesTo(game)))
            Tools.Add(new GameToolViewModel(tool, game));

        _isFavorite = FavoritesService.IsFavorite(game.Platform, game.Id);
        _launchParameters = LaunchParamsService.Get(game.Platform, game.Id);

        var customCover = CustomCoverService.Get(game.Platform, game.Id);
        if (!string.IsNullOrEmpty(customCover))
        {
            try
            {
                Game.CoverImageUrl = new Uri(customCover).AbsoluteUri;
                Game.CoverImageUrlFallback = null;
            }
            catch
            {
                // مسار محفوظ غير صالح (مثلاً انتقل الملف)، نتجاهل ونستخدم الغلاف الأصلي
            }
        }
    }

    public string Name => Game.Name;
    public string Platform => Game.Platform;
    public string StatusText => Game.StatusText;
    public bool IsInstalled => Game.IsInstalled;

    /// الألعاب المثبتة تُحتسب "مملوكة" تلقائيًا
    public bool IsOwned => Game.IsInstalled;
    public string OwnedText => IsOwned ? "نعم ✅" : "لا";

    public string? CoverImageUrl => Game.CoverImageUrl;
    public string? CoverImageUrlFallback => Game.CoverImageUrlFallback;
    public bool HasCoverImage => Game.HasCoverImage;
    public bool HasTools => Tools.Count > 0;

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
        }
    }

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

    /// تُستدعى لما يتوصّل رابط غلاف من مصدر خارجي (SteamGridDB) بعد التحميل الأولي
    public void SetCoverUrl(string url)
    {
        Game.CoverImageUrl = url;
        OnPropertyChanged(nameof(CoverImageUrl));
        OnPropertyChanged(nameof(HasCoverImage));
    }

    /// تُستدعى لما يختار المستخدم صورة غلاف مخصصة من جهازه
    public void SetCustomCover(string localFilePath)
    {
        CustomCoverService.Set(Game.Platform, Game.Id, localFilePath);

        Game.CoverImageUrl = new Uri(localFilePath).AbsoluteUri;
        Game.CoverImageUrlFallback = null;

        OnPropertyChanged(nameof(CoverImageUrl));
        OnPropertyChanged(nameof(CoverImageUrlFallback));
        OnPropertyChanged(nameof(HasCoverImage));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
