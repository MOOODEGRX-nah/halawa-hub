using System;
using System.Collections.Generic;
using System.Linq;

namespace HalawaHub.App.BigPicture;

/// ما يعرضه قسم «إعدادات اللعبة» عن اللعبة (يُقرأ من جديد بعد كل تغيير)
public sealed record BpGameInfo
{
    public string Name { get; init; } = "";
    public string OriginalName { get; init; } = "";
    public string StatusText { get; init; } = "";
    public string StatusNote { get; init; } = "";
    public bool HasUpdate { get; init; }
    /// «افتح في Steam» أو «حدّث في Steam»، أو null إن لم يكن للمنصة مشغّل يُفتح
    public string? LauncherAction { get; init; }
    public bool IsHidden { get; init; }
    public string InstallPath { get; init; } = "";
    public string SizeText { get; init; } = "";
    public bool CanVerify { get; init; }
    public string VerifyNote { get; init; } = "";
    public bool CanUninstall { get; init; }
    public string UninstallLabel { get; init; } = "";
    public bool SupportsLaunchArgs { get; init; } = true;
    public string LaunchArgs { get; init; } = "";
    /// orig / own / sgdb
    public string CoverSource { get; init; } = "orig";
    public string CoverSourceText { get; init; } = "";
}

/// عضوية اللعبة في مجموعة يدوية
public sealed record BpCollectionToggle(string Id, string Name, int Count, bool IsMember);

/// <summary>
/// تحديث التصميم 5.4: إعدادات اللعبة داخل الشاشة الكبيرة بنفس نافذة الإعدادات وأقسام نافذة اللعبة العادية:
/// عام، المجموعات، الملفات، الغلاف، التشغيل، ثم «العودة إلى اللعبة». الاسم ومعاملات التشغيل بلوحة المفاتيح على الشاشة.
/// </summary>
public sealed partial class BigPictureViewModel
{
    private static readonly string[] GameSectionIds = { "g.general", "g.coll", "g.files", "g.cover", "g.launch", "g.back" };
    private static readonly string[] GameSectionKeys = { "bpGsGeneral", "bpGsColl", "bpGsFiles", "bpGsCover", "bpGsLaunch", "bpGsBack" };

    private BpGame? _gameSet;

    /// النافذة تعرض إعدادات لعبة (لا إعدادات الشاشة الكبيرة)
    public bool IsGameSettings => _gameSet != null;
    public BpGame? GameSettingsGame => _gameSet;
    public int SectionCount => SettingSections.Count;

    private string[] CurrentSectionIds => _gameSet != null ? GameSectionIds : SectionIds;

    /// يُستدعى بزر «إعدادات اللعبة» في صفحة اللعبة
    public void OpenGameSettings(BpGame game)
    {
        CloseOverlayQuiet();
        _gameSet = game;
        _section = 0;
        _setCol = 0;
        _setRow = 0;
        _screen = BpScreen.Settings;
        BuildSettings();
        Changed();
    }

    /// B من أعلى الأقسام أو «العودة إلى اللعبة»
    private void CloseGameSettings()
    {
        var game = _gameSet;
        _gameSet = null;
        _section = 0;
        BuildSettings();
        _screen = BpScreen.Game;
        if (game != null) Hero = _host.Games.FirstOrDefault(g => g.Key == game.Key) ?? game;
        BuildGameButtons();
        Changed();
    }

    private BpSettingRow Action(string id, string label, string note, int index, string value = "", bool danger = false, string chip = "") => new()
    {
        FocusKey = "row." + index, Id = id, Kind = BpRowKind.Button, Label = label, Note = note, Value = value, IsDanger = danger, Chip = chip,
    };

    private void BuildGameRows()
    {
        if (_gameSet is not { } g) return;
        var info = _host.GameInfo(g);
        var i = 0;
        switch (GameSectionIds[_section])
        {
            case "g.general":
                SettingRows.Add(Action("g.name", T("bpGsName"), T("bpGsNameN"), i++, info.Name));
                SettingRows.Add(Action("g.status", T("statusLabel"), info.StatusNote, i++, chip: info.HasUpdate ? info.StatusText : "",
                    value: info.HasUpdate ? "" : info.StatusText));
                SettingRows.Add(new BpSettingRow { FocusKey = "row." + i++, Id = "g.fav", Kind = BpRowKind.Toggle, Label = T("inFav"), Note = T("bpGsFavN"), IsOn = g.IsFavorite });
                SettingRows.Add(new BpSettingRow { FocusKey = "row." + i++, Id = "g.hide", Kind = BpRowKind.Toggle, Label = T("hide"), Note = T("bpGsHideN"), IsOn = info.IsHidden });
                break;
            case "g.coll":
                foreach (var c in _host.GameCollections(g))
                    SettingRows.Add(new BpSettingRow { FocusKey = "row." + i++, Id = "g.coll:" + c.Id, Kind = BpRowKind.Toggle, Label = c.Name,
                        Note = BpPlural.Games(c.Count), IsOn = c.IsMember });
                SettingRows.Add(new BpSettingRow { FocusKey = "info", Id = "g.collnote", Kind = BpRowKind.Info, Label = T("bpGsCollN") });
                break;
            case "g.files":
                SettingRows.Add(Action("g.open", T("openLoc"), T("bpGsOpenN"), i++, info.SizeText));
                SettingRows.Add(Action("g.copy", T("copyPath"), T("bpGsCopyN"), i++));
                if (info.CanVerify) SettingRows.Add(Action("g.verify", T("verifyFiles"), info.VerifyNote, i++));
                SettingRows.Add(Action("g.shortcut", T("desktopShortcut"), T("bpGsShortcutN"), i++));
                if (info.CanUninstall) SettingRows.Add(Action("g.uninstall", info.UninstallLabel, T("bpGsUninstallN"), i++, danger: true));
                break;
            case "g.cover" when _coverKey:
                // لا مفتاح بعد: الشرح، ثم QR لصفحة المفتاح، ثم الصق أو اكتب
                SettingRows.Add(new BpSettingRow { FocusKey = "info", Id = "g.keyneed", Kind = BpRowKind.Info, Label = T("bpGsKeyNeed") });
                SettingRows.Add(new BpSettingRow { FocusKey = "qr", Id = "g.keyqr", Kind = BpRowKind.Qr, Label = T("bpGsKeyQr"), Note = _host.SgdbKeyUrl });
                SettingRows.Add(Action("g.keypaste", T("bpGsKeyPaste"), T("bpGsKeyPasteN"), i++));
                SettingRows.Add(Action("g.keytype", T("bpGsKeyType"), T("bpGsKeyTypeN"), i++));
                SettingRows.Add(Action("g.keyback", T("sgdbBack"), "", i++));
                break;
            case "g.cover":
                SettingRows.Add(new BpSettingRow
                {
                    FocusKey = "row." + i++, Id = "g.tiles", Kind = BpRowKind.Tiles, Label = T("curCover"), Note = info.CoverSourceText,
                    Options = new[] { T("optOwn"), T("optSg"), T("optOrig") }, OptionValues = new[] { "own", "sgdb", "orig" },
                    Selected = 1, Value = info.CoverSource,
                });
                break;
            case "g.launch":
                if (info.SupportsLaunchArgs)
                    SettingRows.Add(Action("g.args", T("bpGsArgs"), T("bpGsArgsN"), i++, info.LaunchArgs.Length > 0 ? info.LaunchArgs : T("bpGsNone")));
                SettingRows.Add(new BpSettingRow { FocusKey = "info", Id = "g.launchnote", Kind = BpRowKind.Info, Label = T("bpGsArgsInfo") });
                break;
            default:
                SettingRows.Add(Action("g.back", T("bpGsBack"), "", i));
                break;
        }
    }

    /// A على سطر في إعدادات اللعبة
    private void ChangeGameRow(BpSettingRow row, int delta)
    {
        if (_gameSet is not { } g) return;
        switch (row.Id)
        {
            case "g.name":
                OpenTextEntry(EntryTarget.Name, _host.GameInfo(g).Name);
                return;
            case "g.status":
                if (_host.GameInfo(g).HasUpdate) { _host.OpenUpdateInLauncher(g); ShowToast(F("bpOpenedSteam", g.Name)); }
                else { _host.OpenLauncher(g); }
                return;
            case "g.fav":
                _host.ToggleFavorite(g);
                row.IsOn = g.IsFavorite;
                return;
            case "g.hide":
                // الإخفاء يخرج اللعبة من القوائم: نعود للتبويب ونقول ذلك
                _gameSet = null;
                _section = 0;
                BuildSettings();
                GoTab(_from);
                _host.HideGame(g);
                ShowToast(F("bpHiddenToast", g.Name));
                return;
            case "g.open": _host.OpenFileLocation(g); ShowToast(T("bpGsOpened")); return;
            case "g.copy": CopyRequested?.Invoke(_host.GameInfo(g).InstallPath); ShowToast(T("bpGsCopied")); return;
            case "g.verify": _host.VerifyFiles(g); ShowToast(F("bpGsVerifying", g.Name)); return;
            case "g.shortcut": ShowToast(_host.CreateShortcut(g) ? T("bpGsShortcutDone") : T("bpGsShortcutFail")); return;
            case "g.uninstall": _host.Uninstall(g); ShowToast(F("bpGsUninstallOpened", g.ShownPlatform)); return;
            case "g.keypaste": PasteKeyRequested?.Invoke(); return;
            case "g.keytype": OpenTextEntry(EntryTarget.Key, ""); return;
            case "g.keyback": _coverKey = false; _setRow = 0; RefreshGameRows(); return;
            case "g.args": OpenTextEntry(EntryTarget.Args, _host.GameInfo(g).LaunchArgs); return;
            case "g.back": CloseGameSettings(); return;
            case "g.tiles": RunCoverTile(row); return;
        }
        if (row.Id.StartsWith("g.coll:", StringComparison.Ordinal))
        {
            row.IsOn = !row.IsOn;
            _host.SetCollection(g, row.Id["g.coll:".Length..], row.IsOn);
        }
    }

    /// يمين ويسار على صف بطاقات الغلاف
    private bool MoveTiles(BpSettingRow row, int dx)
    {
        if (row.Kind != BpRowKind.Tiles) return false;
        var next = row.Selected + dx;
        if (next < 0) return false;
        row.Selected = Math.Min(next, row.Options.Count - 1);
        return true;
    }

    private void RunCoverTile(BpSettingRow row)
    {
        if (_gameSet is not { } g) return;
        switch (row.OptionValues[row.Selected])
        {
            case "own": PickCoverFileRequested?.Invoke(g); break;
            case "sgdb": OpenSgdb(g); break;
            default:
                _host.RestoreCover(g);
                ShowToast(T("bpGsCoverSet"));
                RefreshGameRows();
                break;
        }
    }

    /// بعد تغيير من خارج الصفوف (اسم، غلاف، مفتاح): نعيد بناء القسم بالقيم الجديدة مع إبقاء التركيز
    internal void RefreshGameRows()
    {
        if (_gameSet == null) return;
        var keepRow = _setRow;
        var keepTile = SettingRows.FirstOrDefault(r => r.Kind == BpRowKind.Tiles)?.Selected;
        BuildRows();
        _setRow = Math.Min(keepRow, Math.Max(0, ControlRowCount - 1));
        if (keepTile is { } t && SettingRows.FirstOrDefault(r => r.Kind == BpRowKind.Tiles) is { } tiles) tiles.Selected = t;
        Changed();
    }

    /// نسخ المسار (الحافظة في النافذة)
    public event Action<string>? CopyRequested;
    /// «غيّرها يدوياً»: منتقي الملفات في النافذة
    public event Action<BpGame>? PickCoverFileRequested;

    /// النافذة اختارت صورة غلاف من الجهاز
    public void SetOwnCover(BpGame game, string path)
    {
        _host.SetOwnCover(game, path);
        ShowToast(T("bpGsCoverSet"));
        RefreshGameRows();
    }
}
