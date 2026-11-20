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
