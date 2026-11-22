using System.Collections.Generic;

namespace HalawaHub.App.BigPicture;

public enum BpRowKind { Toggle, Segmented, Button, Info }

/// <summary>سطر في نافذة إعدادات الشاشة الكبيرة.</summary>
public sealed class BpSettingRow : BpItem
{
    private bool _on;
    private int _selected;

    public required string Id { get; init; }
    public required BpRowKind Kind { get; init; }
    public required string Label { get; init; }
    public string Note { get; init; } = "";
    public IReadOnlyList<string> Options { get; init; } = [];
    public IReadOnlyList<string> OptionValues { get; init; } = [];

    public bool IsOn { get => _on; set { if (_on != value) { _on = value; Raise(); } } }
    public int Selected { get => _selected; set { if (_selected != value) { _selected = value; Raise(); } } }

    public bool IsToggle => Kind == BpRowKind.Toggle;
    public bool IsSegmented => Kind == BpRowKind.Segmented;
    public bool IsButton => Kind == BpRowKind.Button;
    public bool IsInfo => Kind == BpRowKind.Info;
    public bool IsControl => Kind != BpRowKind.Info;
}

/// <summary>إشعار في اللوحة، مع حالة الخروج (يُخفى ثم يُحذف).</summary>
public sealed class BpNotifItem : BpItem
{
    private bool _leaving;
    private bool _unread;

    public required BpNotification Model { get; init; }
    public int Index { get; init; }
    public bool HasAction => Model.ActionLabel != null;
    public bool IsNew { get; init; }
    public bool Unread { get => _unread; set { if (_unread != value) { _unread = value; Raise(); } } }
    public bool Leaving { get => _leaving; set { if (_leaving != value) { _leaving = value; Raise(); } } }
}

/// <summary>منصة في شريط فلترة المكتبة.</summary>
public sealed class BpFilterItem : BpItem
{
    private bool _on;

    public required string Id { get; init; }
    public required string Label { get; init; }
    public int Count { get; init; }
    public bool IsOn { get => _on; set { if (_on != value) { _on = value; Raise(); } } }
}

/// <summary>
/// صف المكتبة بسبع خانات ثابتة: عند التمرير يُعاد استعمال بطاقات الصف بربطها بألعاب أخرى
/// بدل بناء سبع بطاقات جديدة مع كل خطوة (كان ذلك نحو 200ms لكل ضغطة).
/// </summary>
/// <summary>خانة صف في نافذة المكتبة الظاهرة؛ Row يتبدل عند التمرير.</summary>
public sealed class BpRowSlot : System.ComponentModel.INotifyPropertyChanged
{
    private BpCardRow? _row;
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public BpCardRow? Row
    {
        get => _row;
        set
        {
            if (ReferenceEquals(_row, value)) return;
            _row = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(string.Empty));
        }
    }

    public BpCardItem? C0 => _row?.C0;
    public BpCardItem? C1 => _row?.C1;
    public BpCardItem? C2 => _row?.C2;
    public BpCardItem? C3 => _row?.C3;
    public BpCardItem? C4 => _row?.C4;
    public BpCardItem? C5 => _row?.C5;
    public BpCardItem? C6 => _row?.C6;
}

public sealed class BpCardRow
{
    public BpCardRow(IReadOnlyList<BpCardItem> items) => Items = items;
    public IReadOnlyList<BpCardItem> Items { get; }
    public BpCardItem? C0 => At(0);
    public BpCardItem? C1 => At(1);
    public BpCardItem? C2 => At(2);
    public BpCardItem? C3 => At(3);
    public BpCardItem? C4 => At(4);
    public BpCardItem? C5 => At(5);
    public BpCardItem? C6 => At(6);
    private BpCardItem? At(int i) => i < Items.Count ? Items[i] : null;
}
