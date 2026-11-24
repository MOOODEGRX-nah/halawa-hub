using System;
using System.Globalization;
using System.Linq;

namespace HalawaHub.App.BigPicture;

public enum EntryTarget { Name, Args, Key }

/// <summary>
/// كتابة نص بلوحة المفاتيح على الشاشة فوق إعدادات اللعبة: اسم اللعبة، معاملات التشغيل، مفتاح SteamGridDB.
/// نفس لوحة البحث (نفس الحروف والتنقل)، و«تم» يحفظ، وB يلغي.
/// </summary>
public sealed partial class BigPictureViewModel
{
    private EntryTarget _entryTarget;
    private string _entryText = "";

    public bool IsEntryOpen => _overlay == BpOverlay.TextEntry;
    public string EntryText => _entryText;
    public bool HasEntryText => _entryText.Length > 0;
    /// المفتاح يظهر نقاطًا لا حروفًا
    public string EntryShown => _entryText.Length == 0 ? EntryPlaceholder
        : _entryTarget == EntryTarget.Key ? new string('•', _entryText.Length) : _entryText;
    public string EntryTitle => T(_entryTarget switch { EntryTarget.Name => "bpGsName", EntryTarget.Args => "bpGsArgs", _ => "bpGsKeyType" });
    private string EntryPlaceholder => T(_entryTarget switch { EntryTarget.Name => "bpGsNamePh", EntryTarget.Args => "bpGsNone", _ => "keyPh" });

    private void OpenTextEntry(EntryTarget target, string current)
    {
        _entryTarget = target;
        _entryText = current;
        // الاسم بلغة الواجهة، والمفتاح والمعاملات بالحروف الإنجليزية
        _kbLang = target == EntryTarget.Name && IsRtl ? "ar" : "en";
        BuildKeys();
        _kbRow = 0;
        _kbCol = 0;
        _overlay = BpOverlay.TextEntry;
        Changed();
    }

    private void NavEntry(int dx, int dy)
    {
        var flipped = (_kbLang == "ar") != IsRtl;
        if (dy != 0)
        {
            _kbRow = Math.Clamp(_kbRow + dy, 0, 3);
            _kbCol = Math.Clamp(_kbCol, 0, RowLength(_kbRow) - 1);
            return;
        }
        _kbCol = Math.Clamp(_kbCol + (flipped ? -dx : dx), 0, RowLength(_kbRow) - 1);
    }

    private void ActEntry() => TypeEntryKey(KeyRows[_kbRow][_kbCol].Id);

    public void ClickEntryKey(int row, int col)
    {
        _kbRow = row; _kbCol = col;
        TypeEntryKey(KeyRows[row][col].Id);
        SyncFocus();
    }

    private void TypeEntryKey(string id)
    {
        switch (id)
        {
            case "space": _entryText += " "; break;
            case "del": if (_entryText.Length > 0) _entryText = _entryText[..^1]; break;
            case "abc":
                _kbLang = _kbLang == "ar" ? "en" : "ar";
                BuildKeys();
                _kbRow = 3; _kbCol = 2;
                break;
            case "done": CommitEntry(); return;
            default:
                // اسم اللعبة: أول حرف في كل كلمة كبير؛ المفتاح والمعاملات بحروف صغيرة
                var startOfWord = _entryText.Length == 0 || _entryText[^1] == ' ';
                _entryText += _entryTarget == EntryTarget.Name && startOfWord
                    ? id.ToUpper(CultureInfo.InvariantCulture)
                    : id.ToLower(CultureInfo.InvariantCulture);
                break;
        }
        Changed();
    }

    private void CommitEntry()
    {
        var text = _entryText.Trim();
        _overlay = BpOverlay.None;
        _kbLang = IsRtl ? "ar" : "en";
        BuildKeys();
        switch (_entryTarget)
        {
            case EntryTarget.Name when _gameSet is { } g:
                _host.RenameGame(g, text);
                ShowToast(T("bpGsSaved"));
                RefreshGameRows();
                break;
            case EntryTarget.Args when _gameSet is { } g:
                _host.SetLaunchArgs(g, text);
                ShowToast(T("bpGsSaved"));
                RefreshGameRows();
                break;
            case EntryTarget.Key:
                _ = SaveKeyAsync(text);
                break;
        }
        Changed();
    }
}
