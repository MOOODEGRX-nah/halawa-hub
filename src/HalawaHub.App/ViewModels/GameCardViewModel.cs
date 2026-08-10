using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using HalawaHub.Core.Models;
using HalawaHub.Core.Plugins;

namespace HalawaHub.App.ViewModels;

/// <summary>
/// يغلّف لعبة واحدة (GameInfo) مع قائمة الأدوات المنطبقة عليها تحديدًا،
/// عشان كل بطاقة بالواجهة تقدر تعرض زر "أدوات" لو فيه أي IGameTool يخصّها.
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
    }

    public string Name => Game.Name;
    public string Platform => Game.Platform;
    public string StatusText => Game.StatusText;
    public bool IsInstalled => Game.IsInstalled;
    public string? CoverImageUrl => Game.CoverImageUrl;
    public bool HasCoverImage => Game.HasCoverImage;
    public bool HasTools => Tools.Count > 0;

    /// تُستدعى لما يتوصّل رابط غلاف من مصدر خارجي (SteamGridDB) بعد التحميل الأولي
    public void SetCoverUrl(string url)
    {
        Game.CoverImageUrl = url;
        OnPropertyChanged(nameof(CoverImageUrl));
        OnPropertyChanged(nameof(HasCoverImage));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
