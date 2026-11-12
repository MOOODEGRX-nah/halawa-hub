using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using HalawaHub.App.Services;

namespace HalawaHub.App.ViewModels;

/// خيار في قائمة منسدلة (حقل، مقارنة، منصة)
public sealed record Choice(string Key, string Label)
{
    public override string ToString() => Label;
}

/// مجموعة كما تظهر في شريط المكتبة وفي الإعدادات
public sealed class CollectionItemViewModel : INotifyPropertyChanged
{
    public CollectionItemViewModel(GameCollection model) => Model = model;

    public GameCollection Model { get; }
    public string Id => Model.Id;
    public string Name => Model.Name;
    public string Icon => Model.Icon;
    public bool IsSmart => Model.IsSmart;
    public bool IsHidden => Model.Hidden;
    public string KindText => Loc.Instance[Model.IsSmart ? "colSmart" : "colManual"];
    public string HideLabel => Loc.Instance[Model.Hidden ? "colShow" : "colHide"];

    private int _count;
    public int Count { get => _count; set { _count = value; Raise(nameof(Count)); Raise(nameof(CountText)); } }
    public string CountText => Loc.Instance.Format("platGames", _count);

    private bool _isActive;
    public bool IsActive { get => _isActive; set { if (_isActive == value) return; _isActive = value; Raise(nameof(IsActive)); } }

    public void Refresh()
    {
        foreach (var n in new[] { nameof(Name), nameof(Icon), nameof(IsHidden), nameof(KindText), nameof(HideLabel), nameof(CountText) }) Raise(n);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

/// شرط قيد التحرير
public sealed class RuleEditorViewModel : INotifyPropertyChanged
{
    private readonly Action _changed;
    private readonly IReadOnlyList<Choice> _platforms;

    public RuleEditorViewModel(CollectionRule rule, IReadOnlyList<Choice> platforms, Action changed)
    {
        _platforms = platforms;
        _changed = changed;
        FieldOptions = CollectionsService.Fields.Select(f => new Choice(f, Loc.Instance["cf_" + f])).ToList();
        _field = FieldOptions.FirstOrDefault(c => c.Key == rule.Field) ?? FieldOptions[0];
        RebuildOps(rule.Op);
        _value = rule.Value;
        _platform = platforms.FirstOrDefault(p => string.Equals(p.Key, rule.Value, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<Choice> FieldOptions { get; }
    public IReadOnlyList<Choice> PlatformOptions => _platforms;
    public List<Choice> OpOptions { get; private set; } = new();

    private Choice _field;
    public Choice Field
    {
        get => _field;
        set
        {
            if (value == null || value == _field) return;
            _field = value;
            RebuildOps(null);
            _value = "";
            _platform = null;
            RaiseAll();
        }
    }

    private Choice _op = new("", "");
    public Choice Op
    {
        get => _op;
        set { if (value == null || value == _op) return; _op = value; RaiseAll(); }
    }

    private string _value = "";
    public string Value
    {
        get => _value;
        set { _value = value ?? ""; Raise(nameof(Value)); _changed(); }
    }

    private Choice? _platform;
    public Choice? Platform
    {
        get => _platform;
        set { _platform = value; Raise(nameof(Platform)); _changed(); }
    }

    public bool IsPlatform => _field.Key == "platform";
    public bool NeedsText => !IsPlatform && CollectionsService.NeedsValue(_field.Key, _op.Key);
    public string ValueHint => Loc.Instance[_field.Key switch { "playtime" => "cvHours", "name" => "cvText", _ => "cvDays" }];

    public CollectionRule ToRule() => new()
    {
        Field = _field.Key,
        Op = _op.Key,
        Value = IsPlatform ? _platform?.Key ?? "" : NeedsText ? _value.Trim() : ""
    };

    private void RebuildOps(string? keep)
    {
        OpOptions = CollectionsService.OpsFor(_field.Key).Select(o => new Choice(o, Loc.Instance["co_" + o])).ToList();
        _op = OpOptions.FirstOrDefault(o => o.Key == keep) ?? OpOptions[0];
    }

    private void RaiseAll()
    {
        foreach (var n in new[] { nameof(Field), nameof(OpOptions), nameof(Op), nameof(Value), nameof(Platform), nameof(IsPlatform), nameof(NeedsText), nameof(ValueHint) })
            Raise(n);
        _changed();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

/// عضوية لعبة في مجموعة يدوية (نافذة اللعبة)
public sealed class CollectionMembership : INotifyPropertyChanged
{
    private readonly Action<string, bool> _set;
    private bool _isMember;

    public CollectionMembership(string id, string name, bool isMember, Action<string, bool> set)
    {
        Id = id;
        Name = name;
        _isMember = isMember;
        _set = set;
    }

    public string Id { get; }
    public string Name { get; }

    public bool IsMember
    {
        get => _isMember;
        set
        {
            if (_isMember == value) return;
            _isMember = value;
            _set(Id, value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsMember)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// المجموعات: شريط في المكتبة يفلتر بالمجموعة المختارة، وإدارة كاملة في الإعدادات
/// (إنشاء، تسمية، أيقونة، سحب لإعادة الترتيب، إخفاء، حذف، وشروط المجموعة الذكية).
/// </summary>
public partial class MainViewModel
{
    public static readonly string[] CollectionIcons = { "star", "heart", "play", "sparkle", "wrench", "bell" };

    internal string CollectionsPath { get; set; } = CollectionsService.DefaultPath;

    private List<GameCollection> _collections = new();

    public ObservableCollection<CollectionItemViewModel> Collections { get; } = new();
    public ObservableCollection<CollectionItemViewModel> VisibleCollections { get; } = new();
    public bool HasVisibleCollections => VisibleCollections.Count > 0;
    public bool HasNoCollections => Collections.Count == 0;

    private string? _activeCollectionId;
    public string? ActiveCollectionId => _activeCollectionId;

    private RelayCommand? _selectCollectionCommand, _newManualCollectionCommand, _newSmartCollectionCommand, _editCollectionCommand,
        _deleteCollectionCommand, _toggleCollectionHiddenCommand, _saveCollectionCommand, _cancelCollectionEditCommand,
        _addRuleCommand, _removeRuleCommand, _pickCollectionIconCommand, _setMatchCommand;

    public RelayCommand SelectCollectionCommand => _selectCollectionCommand ??= new RelayCommand(p => SelectCollection(p as string));
    public RelayCommand NewManualCollectionCommand => _newManualCollectionCommand ??= new RelayCommand(_ => BeginEditCollection(new GameCollection()));
    public RelayCommand NewSmartCollectionCommand => _newSmartCollectionCommand ??= new RelayCommand(_ => BeginEditCollection(new GameCollection { IsSmart = true }));
    public RelayCommand EditCollectionCommand => _editCollectionCommand ??= new RelayCommand(p => { if (p is CollectionItemViewModel c) BeginEditCollection(c.Model); });
    public RelayCommand DeleteCollectionCommand => _deleteCollectionCommand ??= new RelayCommand(p => { if (p is CollectionItemViewModel c) AskDeleteCollection(c); });
    public RelayCommand ToggleCollectionHiddenCommand => _toggleCollectionHiddenCommand ??= new RelayCommand(p => { if (p is CollectionItemViewModel c) ToggleCollectionHidden(c); });
    public RelayCommand SaveCollectionCommand => _saveCollectionCommand ??= new RelayCommand(_ => SaveEditedCollection(), _ => CanSaveCollection);
    public RelayCommand CancelCollectionEditCommand => _cancelCollectionEditCommand ??= new RelayCommand(_ => EndEditCollection());
    public RelayCommand AddRuleCommand => _addRuleCommand ??= new RelayCommand(_ => AddRule(new CollectionRule()));
    public RelayCommand RemoveRuleCommand => _removeRuleCommand ??= new RelayCommand(p => { if (p is RuleEditorViewModel r) { EditRules.Remove(r); EditChanged(); } });
    public RelayCommand PickCollectionIconCommand => _pickCollectionIconCommand ??= new RelayCommand(p => { if (p is string k) { _editIcon = k; OnPropertyChanged(nameof(EditIcon)); } });
    public RelayCommand SetMatchCommand => _setMatchCommand ??= new RelayCommand(p => { _editMatchAll = p as string != "any"; OnPropertyChanged(nameof(EditMatchIndex)); });

    internal void LoadCollections()
    {
        _collections = CollectionsService.Load(CollectionsPath);
        RebuildCollectionItems();
    }

    private void RebuildCollectionItems()
    {
        Collections.Clear();
        foreach (var c in _collections) Collections.Add(new CollectionItemViewModel(c) { IsActive = c.Id == _activeCollectionId });
        if (_activeCollectionId != null && _collections.All(c => c.Id != _activeCollectionId || c.Hidden)) _activeCollectionId = null;
        RefreshCollectionCounts();
    }

    private static GameFacts FactsOf(GameCardViewModel c) => new(c.Game.Platform + "|" + c.Game.Id, c.Game.Platform, c.Name,
        c.PlayTimeTotal, c.LastPlayed, c.DateAdded, c.IsInstalled, c.IsFavorite);

    /// الأعداد وشريط المكتبة؛ تُستدعى بعد كل فحص وكل تغيير في المجموعات
    internal void RefreshCollectionCounts()
    {
        var now = DateTime.UtcNow;
        var facts = Games.Where(c => ShowHiddenGames || !c.IsHidden).Select(FactsOf).ToList();
        foreach (var item in Collections)
        {
            item.Count = facts.Count(f => CollectionsService.Contains(item.Model, f, now));
            item.IsActive = item.Id == _activeCollectionId;
            item.Refresh();
        }

        VisibleCollections.Clear();
        foreach (var item in Collections.Where(c => !c.IsHidden)) VisibleCollections.Add(item);
        OnPropertyChanged(nameof(HasVisibleCollections));
        OnPropertyChanged(nameof(HasNoCollections));
    }

    private void SelectCollection(string? id)
    {
        _activeCollectionId = _activeCollectionId == id ? null : id;
        foreach (var item in Collections) item.IsActive = item.Id == _activeCollectionId;
        OnPropertyChanged(nameof(ActiveCollectionId));
        ApplyFilter();
    }

    /// تُستدعى من ApplyFilter: هل اللعبة في المجموعة المختارة (أو لا مجموعة مختارة)؟
    private Func<GameCardViewModel, bool> ActiveCollectionFilter()
    {
        var active = _collections.FirstOrDefault(c => c.Id == _activeCollectionId);
        if (active == null) return _ => true;
        var now = DateTime.UtcNow;
        return card => CollectionsService.Contains(active, FactsOf(card), now);
    }

    private void PersistCollections()
    {
        CollectionsService.Save(CollectionsPath, _collections);
        RebuildCollectionItems();
        ApplyFilter();
    }

    /// سحب لإعادة الترتيب في الإعدادات
    public void MoveCollection(int from, int to)
    {
        if (from < 0 || from >= _collections.Count) return;
        to = Math.Clamp(to, 0, _collections.Count - 1);
        if (from == to) return;
        var item = _collections[from];
        _collections.RemoveAt(from);
        _collections.Insert(to, item);
        PersistCollections();
    }

    private void ToggleCollectionHidden(CollectionItemViewModel c)
    {
        c.Model.Hidden = !c.Model.Hidden;
        PersistCollections();
    }

    private void AskDeleteCollection(CollectionItemViewModel c)
    {
        var l = Loc.Instance;
        RequestConfirm(new ConfirmRequest
        {
            Title = l.Format("colDeleteTitle", c.Name),
            Message = l["colDeleteBody"],
            Choices = new[]
            {
                new ConfirmChoice { Label = l["colDelete"], IsPrimary = true, Run = () => { _collections.Remove(c.Model); PersistCollections(); } },
                new ConfirmChoice { Label = l["cancel"] }
            }
        });
    }

    // ===== المحرر =====

    private GameCollection? _editTarget;
    private bool _editIsNew;
    private string _editName = "";
    private string _editIcon = "star";
    private bool _editMatchAll = true;

    public ObservableCollection<RuleEditorViewModel> EditRules { get; } = new();
    public bool IsEditingCollection => _editTarget != null;
    public bool IsCollectionListVisible => _editTarget == null;
    public bool EditIsSmart => _editTarget?.IsSmart == true;
    public string EditTitle => Loc.Instance[_editIsNew ? (EditIsSmart ? "colNewSmart" : "colNewManual") : "colEdit"];
    public string EditIcon => _editIcon;
    public int EditMatchIndex => _editMatchAll ? 0 : 1;

    public string EditName
    {
        get => _editName;
        set { _editName = value ?? ""; OnPropertyChanged(nameof(EditName)); EditChanged(); }
    }

    public bool CanSaveCollection => _editTarget != null && CollectionsService.CanSave(BuildEdited());

    /// لماذا لا يمكن الحفظ (يظهر تحت الأزرار)
    public string EditProblem
    {
        get
        {
            if (_editTarget == null || CanSaveCollection) return "";
            if (string.IsNullOrWhiteSpace(_editName)) return Loc.Instance["colNeedName"];
            return Loc.Instance[EditRules.Count == 0 ? "colNeedRule" : "colBadRule"];
        }
    }

    private IReadOnlyList<Choice> PlatformChoices() =>
        Games.Select(g => g.Game.Platform).Distinct().OrderBy(p => p)
            .Select(p => new Choice(p, GameCardViewModel.ShortenPlatform(p))).ToList();

    private void BeginEditCollection(GameCollection target)
    {
        _editTarget = target;
        _editIsNew = _collections.All(c => c.Id != target.Id);
        _editName = target.Name;
        _editIcon = string.IsNullOrEmpty(target.Icon) ? "star" : target.Icon;
        _editMatchAll = target.MatchAll;
        EditRules.Clear();
        foreach (var r in target.Rules) AddRule(r);
        RaiseEditor();
    }

    private void AddRule(CollectionRule rule)
    {
        EditRules.Add(new RuleEditorViewModel(rule, PlatformChoices(), EditChanged));
        EditChanged();
    }

    private void EditChanged()
    {
        OnPropertyChanged(nameof(CanSaveCollection));
        OnPropertyChanged(nameof(EditProblem));
        SaveCollectionCommand.RaiseCanExecuteChanged();
    }

    private GameCollection BuildEdited() => new()
    {
        Id = _editTarget!.Id,
        Name = _editName.Trim(),
        Icon = _editIcon,
        IsSmart = _editTarget.IsSmart,
        Hidden = _editTarget.Hidden,
        Games = _editTarget.Games,
        MatchAll = _editMatchAll,
        Rules = EditRules.Select(r => r.ToRule()).ToList()
    };

    private void SaveEditedCollection()
    {
        if (!CanSaveCollection) return;
        var edited = BuildEdited();
        var index = _collections.FindIndex(c => c.Id == edited.Id);
        if (index >= 0) _collections[index] = edited;
        else _collections.Add(edited);
        EndEditCollection();
        PersistCollections();
    }

    private void EndEditCollection()
    {
        _editTarget = null;
        EditRules.Clear();
        RaiseEditor();
    }

    private void RaiseEditor()
    {
        foreach (var n in new[] { nameof(IsEditingCollection), nameof(IsCollectionListVisible), nameof(EditIsSmart), nameof(EditTitle),
                     nameof(EditName), nameof(EditIcon), nameof(EditMatchIndex) })
            OnPropertyChanged(n);
        EditChanged();
    }

    // ===== عضوية اللعبة (نافذة اللعبة) =====

    public ObservableCollection<CollectionMembership> GameMemberships { get; } = new();
    public bool HasManualCollections => GameMemberships.Count > 0;

    private void BuildGameMemberships(GameCardViewModel card)
    {
        var key = card.Game.Platform + "|" + card.Game.Id;
        GameMemberships.Clear();
        foreach (var c in _collections.Where(c => !c.IsSmart))
            GameMemberships.Add(new CollectionMembership(c.Id, c.Name, c.Games.Contains(key), (id, member) => SetMembership(id, key, member)));
        OnPropertyChanged(nameof(HasManualCollections));
    }

    private void SetMembership(string collectionId, string gameKey, bool member)
    {
        var c = _collections.FirstOrDefault(x => x.Id == collectionId);
        if (c == null) return;
        if (member && !c.Games.Contains(gameKey)) c.Games.Add(gameKey);
        if (!member) c.Games.Remove(gameKey);
        CollectionsService.Save(CollectionsPath, _collections);
        RefreshCollectionCounts();
        if (_activeCollectionId == collectionId) ApplyFilter();
    }
}
