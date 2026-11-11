using System;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using HalawaHub.App.Controls;
using HalawaHub.App.Services;
using HalawaHub.App.ViewModels;

namespace HalawaHub.App.Views;

public partial class GameCard : UserControl
{
    private GameCardViewModel? _card;

    public GameCard()
    {
        InitializeComponent();

        // الاشتراك في أحداث اللعبة فقط والبطاقة داخل النافذة. اللعبة (GameCardViewModel) تعيش طوال
        // عمر المكتبة، والبطاقة المرسومة تُنشأ من جديد مع كل فلترة أو ترتيب؛ اشتراك لا يُلغى عند
        // الإزالة كان يُبقي كل البطاقات القديمة في الذاكرة (تسريب يكبر مع كل تبديل تبويب).
        DataContextChanged += (_, _) => Rebind();
        AttachedToVisualTree += (_, _) => { _attached = true; Rebind(); };
        DetachedFromVisualTree += (_, _) => { _attached = false; Rebind(); };

    }

    private bool _attached;

    private void Rebind()
    {
        var target = _attached ? DataContext as GameCardViewModel : null;
        if (ReferenceEquals(target, _card)) return;
        if (_card != null) _card.PropertyChanged -= OnCardChanged;
        _card = target;
        if (_card != null) _card.PropertyChanged += OnCardChanged;
    }

    private MainViewModel? MainVm => TopLevel.GetTopLevel(this)?.DataContext as MainViewModel;

    private void OnCardChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(GameCardViewModel.IsMenuOpen) || _card == null) return;

        // البطاقة المفتوحة قائمتها فوق جاراتها
        if (this.GetVisualParent() is Control container)
            container.ZIndex = _card.IsMenuOpen ? 20 : 0;

        if (_card.IsMenuOpen)
        {
            var menu = this.FindControl<Border>("Menu")!;
            _ = MotionSettings.AnimateAsync(menu, TimeSpan.FromSeconds(0.24),
                new CssBezierEasing(0.34, 1.56, 0.64, 1), 0, 0.88, -4);
        }
    }

    private void Play_Click(object? sender, RoutedEventArgs e) =>
        MainVm?.LaunchGameCommand.Execute(_card);

    private void Menu_Click(object? sender, RoutedEventArgs e)
    {
        if (_card != null) MainVm?.ToggleCardMenu(_card);
    }

    private void Favorite_Click(object? sender, RoutedEventArgs e)
    {
        if (_card != null) MainVm?.ToggleFavoriteFromMenu(_card);
    }

    private void Settings_Click(object? sender, RoutedEventArgs e) =>
        MainVm?.OpenDetailsCommand.Execute(_card);

    private void Shortcut_Click(object? sender, RoutedEventArgs e)
    {
        if (_card != null) MainVm?.CreateDesktopShortcut(_card);
    }

    private void CopyId_Click(object? sender, RoutedEventArgs e)
    {
        var card = _card;
        if (card == null) return;
        SafeUi.Run(async () =>
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null) await clipboard.SetTextAsync(card.Game.Id);
            MainVm?.OnGameIdCopied(card);
        }, "copy game id");
    }

    // القائمة تُبنى عند أول فتح، فعناصرها غير موجودة وقت إنشاء البطاقة: نصل لأيقونة المنزلقات من الزر نفسه
    private void SettingsItem_PointerEntered(object? sender, Avalonia.Input.PointerEventArgs e) => FlipSliders(sender, true);
    private void SettingsItem_PointerExited(object? sender, Avalonia.Input.PointerEventArgs e) => FlipSliders(sender, false);

    private static void FlipSliders(object? sender, bool flipped)
    {
        if ((sender as Control)?.GetVisualDescendants().OfType<SlidersIcon>().FirstOrDefault() is { } sliders)
            sliders.IsFlipped = flipped;
    }
}
