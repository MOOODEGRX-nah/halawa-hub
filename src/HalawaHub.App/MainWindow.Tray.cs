using Avalonia;
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

    private void SetupTrayAndCloseBehavior()
    {
        Closing += (_, e) =>
        {
            if (!_allowExit)
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
            var showItem = new NativeMenuItem("إظهار Halawa-Hub");
            showItem.Click += (_, _) => ShowAndFocus();
            var exitItem = new NativeMenuItem("خروج");
            exitItem.Click += (_, _) => { _allowExit = true; Close(); };
            menu.Add(showItem);
            menu.Add(exitItem);
            icon.Menu = menu;

            TrayIcon.SetIcons(Application.Current!, new TrayIcons { icon });
        }
        catch
        {
            // الصينية تحسين إضافي — ما يصير تكراش البرنامج لو ما اشتغلت بجهاز ما
        }
    }

    private void ShowAndFocus()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }
}
