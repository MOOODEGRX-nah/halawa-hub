using Avalonia;
using HalawaHub.App.Services;
using HalawaHub.App.ViewModels;
using Avalonia.Controls;
using Avalonia.Platform;
using System;

namespace HalawaHub.App;

/// <summary>
/// جزء منفصل من MainWindow مسؤول عن أيقونة الصينية وسلوك الإغلاق:
/// زر X يخفي النافذة للصينية بدل إنهاء البرنامج، والأيقونة نفسها
/// تعرض النافذة عند الضغط، وقائمة يمين فيها إظهار/خروج حقيقي.
/// </summary>
public partial class MainWindow
{
    private bool _allowExit;
    private bool _trayAvailable;

    /// الإخفاء مسموح فقط إذا كانت أيقونة الصينية موجودة فعلًا، وإلا تصبح
    /// النافذة مخفية بلا أي طريق للرجوع إليها
    internal static bool ShouldHideToTray(bool allowExit, bool trayAvailable) => !allowExit && trayAvailable;

    private void SetupTrayAndCloseBehavior()
    {
        Closing += (_, e) =>
        {
            (DataContext as MainViewModel)?.FlushPendingSettings();

            if (ShouldHideToTray(_allowExit, _trayAvailable))
            {
                e.Cancel = true;
                Hide();
            }
        };

        Activated += (_, _) => (DataContext as MainViewModel)?.OnWindowRegainedFocus();

        try
        {
            var icon = new TrayIcon
            {
                Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://Halawa-Hub/Assets/icon.ico"))),
                ToolTipText = "Halawa-Hub"
            };
            icon.Clicked += (_, _) => ShowAndFocus();

            var menu = new NativeMenu();
            var showItem = new NativeMenuItem(Loc.Instance["trayShow"]);
            showItem.Click += (_, _) => ShowAndFocus();
            var exitItem = new NativeMenuItem(Loc.Instance["trayExit"]);
            exitItem.Click += (_, _) => { _allowExit = true; Close(); };
            menu.Add(showItem);
            menu.Add(exitItem);
            icon.Menu = menu;

            Loc.Instance.LanguageChanged += (_, _) =>
            {
                showItem.Header = Loc.Instance["trayShow"];
                exitItem.Header = Loc.Instance["trayExit"];
            };

            TrayIcon.SetIcons(Application.Current!, new TrayIcons { icon });
            _trayAvailable = true;
        }
        catch (Exception ex)
        {
            // الصينية تحسين إضافي — ما يصير تكراش البرنامج لو ما اشتغلت بجهاز ما،
            // وزر الإغلاق حينها يغلق البرنامج فعليًا
            _trayAvailable = false;
            HalawaHub.Core.Log.Error("تعذر إنشاء أيقونة الصينية", ex);
        }
    }

    internal void ShowAndFocus()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }
}
