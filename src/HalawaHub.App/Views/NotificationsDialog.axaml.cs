using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HalawaHub.App.Services;
using HalawaHub.App.ViewModels;

namespace HalawaHub.App.Views;

/// <summary>
/// الإشعارات: بطاقة زجاجية عائمة تحت الجرس (.panel.nt في التصميم)، لا نافذة في وسط الشاشة.
/// تلتصق بطرف الصفحة (يمين في الإنجليزية، يسار في العربية) ويبدأ أعلاها تحت الجرس.
/// </summary>
public partial class NotificationsDialog : UserControl
{
    /// المسافة بين الجرس والبطاقة، وطرف البطاقة من حافة النافذة (padding: 128px 28px في التصميم)
    private const double GapBelowBell = 10, EdgeMargin = 28, BottomMargin = 20, FallbackTop = 128;

    public NotificationsDialog()
    {
        InitializeComponent();
        PropertyChanged += (_, e) =>
        {
            if (e.Property == IsVisibleProperty && IsVisible) Show();
        };
    }

    private void Show()
    {
        Card.Margin = new Thickness(0, TopUnderBell(), EdgeMargin, BottomMargin);
        if (!MotionSettings.Enabled)
        {
            Card.Opacity = 1;
            Card.RenderTransform = null;
            return;
        }
        // تبدأ أعلى قليلًا وأصغر ثم تستقر (setDlgIn)
        var transitions = Card.Transitions;
        Card.Transitions = null;
        Card.Opacity = 0;
        Card.RenderTransform = TransformOperations.Parse("translateY(-8px) scale(0.97)");
        Card.Transitions = transitions;
        Dispatcher.UIThread.Post(() =>
        {
            Card.Opacity = 1;
            Card.RenderTransform = TransformOperations.Parse("translateY(0px) scale(1)");
        }, DispatcherPriority.Render);
    }

    /// أسفل زر الجرس داخل هذه الطبقة. المحور الرأسي لا يتأثر باتجاه الواجهة، فلا حاجة لحساب العكس.
    internal double TopUnderBell()
    {
        var bell = (this.GetVisualRoot() as Visual)?.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(b => b.Name == "BellButton" && b.IsEffectivelyVisible);
        if (bell == null) return FallbackTop;
        var bottom = bell.TranslatePoint(new Point(0, bell.Bounds.Height), this);
        return bottom is { } p ? p.Y + GapBelowBell : FallbackTop;
    }

    private void Scrim_PointerPressed(object? sender, PointerPressedEventArgs e) =>
        (DataContext as MainViewModel)?.CloseNotifications();
}
