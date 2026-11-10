using HalawaHub.App.Services;
using HalawaHub.Core;
using HalawaHub.Core.Library;

namespace HalawaHub.App.ViewModels;

/// عناصر قائمة البطاقة الإضافية: اختصار سطح المكتب ونسخ معرّف اللعبة
public partial class MainViewModel
{
    internal string? DesktopDirOverride { get; set; }

    public void CreateDesktopShortcut(GameCardViewModel card)
    {
        CloseAllCardMenus();
        try
        {
            var path = DesktopShortcut.Create(card.Game, card.Name, DesktopDirOverride);
            Log.Info($"desktop shortcut created: {path}");
            ShowStatus(Loc.Instance.Format("shortcutCreated", card.Name));
        }
        catch (System.Exception ex)
        {
            Log.Error($"desktop shortcut failed for {card.Name}", ex);
            ShowStatus(Loc.Instance.Format("shortcutFailed", ex.Message));
        }
    }

    public void OnGameIdCopied(GameCardViewModel card)
    {
        CloseAllCardMenus();
        ShowStatus(Loc.Instance.Format("idCopied", card.Game.Id));
    }
}
