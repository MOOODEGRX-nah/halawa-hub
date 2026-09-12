using System.ComponentModel;
using HalawaHub.Core.Models;
using HalawaHub.Core.Plugins;

namespace HalawaHub.App.ViewModels;

/// <summary>
/// يمثّل أداة واحدة (IGameTool) مربوطة بلعبة محددة، مع زر تطبيق/إزالة
/// جاهز للربط المباشر بالواجهة.
/// </summary>
public class GameToolViewModel : INotifyPropertyChanged
{
    private readonly IGameTool _tool;
    private readonly GameInfo _game;

    public string Name => _tool.Name;
    public string Description => _tool.Description;

    private bool _isApplied;
    public bool IsApplied
    {
        get => _isApplied;
        private set
        {
            _isApplied = value;
            OnPropertyChanged(nameof(IsApplied));
            OnPropertyChanged(nameof(ActionLabel));
        }
    }

    public string ActionLabel => IsApplied ? "إزالة" : "تطبيق";

    public RelayCommand ToggleCommand { get; }

    public GameToolViewModel(IGameTool tool, GameInfo game)
    {
        _tool = tool;
        _game = game;
        _isApplied = tool.IsApplied(game);
        ToggleCommand = new RelayCommand(_ => Toggle());
    }

    private void Toggle()
    {
        if (IsApplied) _tool.Remove(_game);
        else _tool.Apply(_game);

        IsApplied = _tool.IsApplied(_game);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
