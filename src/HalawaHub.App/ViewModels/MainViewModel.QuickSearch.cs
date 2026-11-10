using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace HalawaHub.App.ViewModels;

/// <summary>
/// Ctrl+K: بحث سريع من أي صفحة. الأسهم تنقل بين النتائج، Enter يفتح نافذة اللعبة،
/// Esc يغلق. الأقرب أولًا: ما يبدأ بالنص، ثم ما يحتويه.
/// </summary>
public partial class MainViewModel
{
    private const int QuickSearchLimit = 8;

    private bool _isQuickSearchOpen;
    private string _quickSearchQuery = "";
    private int _quickSearchIndex;

    public ObservableCollection<GameCardViewModel> QuickSearchResults { get; } = new();

    public bool IsQuickSearchOpen
    {
        get => _isQuickSearchOpen;
        private set
        {
            if (_isQuickSearchOpen == value) return;
            _isQuickSearchOpen = value;
            OnPropertyChanged(nameof(IsQuickSearchOpen));
        }
    }

    public string QuickSearchQuery
    {
        get => _quickSearchQuery;
        set
        {
            var v = value ?? "";
            if (_quickSearchQuery == v) return;
            _quickSearchQuery = v;
            OnPropertyChanged(nameof(QuickSearchQuery));
            UpdateQuickSearchResults();
        }
    }

    public int QuickSearchIndex
    {
        get => _quickSearchIndex;
        set
        {
            var v = QuickSearchResults.Count == 0 ? -1 : Math.Clamp(value, 0, QuickSearchResults.Count - 1);
            if (_quickSearchIndex == v) return;
            _quickSearchIndex = v;
            OnPropertyChanged(nameof(QuickSearchIndex));
        }
    }

    public bool HasNoQuickSearchResults => QuickSearchResults.Count == 0;

    public void OpenQuickSearch()
    {
        _quickSearchQuery = "";
        OnPropertyChanged(nameof(QuickSearchQuery));
        UpdateQuickSearchResults();
        IsQuickSearchOpen = true;
    }

    public void CloseQuickSearch() => IsQuickSearchOpen = false;

    public void MoveQuickSearch(int delta) => QuickSearchIndex = _quickSearchIndex + delta;

    /// Enter: نافذة اللعبة المختارة
    public void ConfirmQuickSearch()
    {
        if (_quickSearchIndex < 0 || _quickSearchIndex >= QuickSearchResults.Count) return;
        var card = QuickSearchResults[_quickSearchIndex];
        CloseQuickSearch();
        OpenDetailsCommand.Execute(card);
    }

    private void UpdateQuickSearchResults()
    {
        var q = _quickSearchQuery.Trim();
        var pool = Games.Where(c => ShowHiddenGames || !c.IsHidden);

        var results = q.Length == 0
            // بلا نص: آخر ما لُعب، ثم الباقي أبجديًا
            ? pool.OrderByDescending(c => c.LastPlayed ?? DateTime.MinValue).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            : pool.Where(c => c.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
                  .OrderBy(c => c.Name.StartsWith(q, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                  .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase);

        QuickSearchResults.Clear();
        foreach (var card in results.Take(QuickSearchLimit)) QuickSearchResults.Add(card);

        _quickSearchIndex = -2; // يفرض الإشعار
        QuickSearchIndex = 0;
        OnPropertyChanged(nameof(HasNoQuickSearchResults));
    }
}
