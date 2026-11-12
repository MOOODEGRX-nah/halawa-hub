using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FluentAssertions;
using HalawaHub.App.Controls;
using HalawaHub.App.Services;
using HalawaHub.App.Views;
using Xunit;

namespace HalawaHub.Tests.Ui;

/// <summary>
/// نافذة الإعدادات ونافذة اللعبة كانتا بحجم ثابت فتبقيان صغيرتين عند تكبير البرنامج،
/// وعند أصغر حجم للبرنامج كان أسفل نافذة الإعدادات يُقص. الآن: 80% من البرنامج، لا أصغر
/// من حجم التصميم ولا أكبر من 1400×900، وفي المنتصف ودائمًا كاملة داخله.
/// </summary>
public class DialogSizingTests
{
    private static readonly Size Max = new(1400, 900);

    [Theory]
    // الحجم الافتراضي للبرنامج: 80% أكبر من حجم التصميم
    [InlineData(1280, 860, 900, 540, 1024, 688)]
    // مكبّر على 1080p: العرض عند حده الأقصى، والارتفاع 80%
    [InlineData(1920, 1052, 900, 540, 1400, 841)]
    [InlineData(1920, 1080, 760, 500, 1400, 864)]
    // شاشة كبيرة جدًا: الحد الأقصى في الاتجاهين
    [InlineData(2560, 1440, 900, 540, 1400, 900)]
    [InlineData(3840, 2160, 760, 500, 1400, 900)]
    // أصغر حجم للبرنامج: حجم التصميم، وكان 110+540 يتجاوز ارتفاع 620
    [InlineData(980, 620, 900, 540, 900, 540)]
    [InlineData(980, 620, 760, 500, 784, 500)]
    // أصغر من ذلك (شاشة صغيرة أو مدير نوافذ يتجاهل الحد الأدنى): تصغر لتبقى داخل البرنامج
    [InlineData(800, 500, 900, 540, 760, 460)]
    [InlineData(30, 30, 900, 540, 0, 0)]
    public void FrameSize_FollowsTheWindow_WithinLimits(double w, double h, double minW, double minH, double expectedW, double expectedH)
    {
        DialogFrame.FrameSize(new Size(w, h), 0.8, 0.8, new Size(minW, minH), Max, 20)
            .Should().Be(new Size(expectedW, expectedH));
    }

    [Fact]
    public void FrameSize_WithoutALimit_KeepsTheDesignSize()
    {
        DialogFrame.FrameSize(Size.Infinity, 0.8, 0.8, new Size(900, 540), Max, 20).Should().Be(new Size(900, 540));
    }

    public static IEnumerable<object[]> Cases()
    {
        foreach (var language in new[] { Loc.Arabic, Loc.English })
        foreach (var dialog in new[] { "settings", "game" })
            yield return new object[] { language, dialog };
    }

    private static (UserControl Dialog, Border Box, double SideWidth, Size Min) Open(UiHarness ui, string dialog)
    {
        if (dialog == "settings")
        {
            ui.Vm.OpenSettingsCommand.Execute(null);
            ui.Vm.SelectSettingsSectionCommand.Execute("0");
            ui.Settle();
            var settings = ui.Window.GetVisualDescendants().OfType<SettingsDialog>().Single();
            return (settings, BoxOf(settings), 230, new Size(900, 540));
        }

        ui.Vm.OpenDetailsCommand.Execute(ui.Vm.Games.First());
        ui.Settle();
        var game = ui.Window.GetVisualDescendants().OfType<GameDialog>().Single();
        return (game, BoxOf(game), 210, new Size(760, 500));
    }

    private static Border BoxOf(UserControl dialog) =>
        dialog.GetVisualDescendants().OfType<Border>().First(b => b.Name == "DialogBox");

    /// مكان العنصر في البرنامج. في العربي الرسم معكوس، فالزاوية (0,0) تقع يمينًا
    private static Rect InWindow(UiHarness ui, Visual v)
    {
        var a = v.TranslatePoint(new Point(0, 0), ui.Window)!.Value;
        var b = v.TranslatePoint(new Point(v.Bounds.Width, v.Bounds.Height), ui.Window)!.Value;
        return new Rect(new Point(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y)), new Point(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y)));
    }

    private static void Resize(UiHarness ui, double width, double height)
    {
        ui.Window.Width = width;
        ui.Window.Height = height;
        ui.Settle();
    }

    /// النافذة بالحجم المتوقع، في المنتصف، وكاملة داخل البرنامج؛ والقائمة الجانبية بعرضها الثابت
    private static void ShouldFit(UiHarness ui, Border box, double sideWidth, Size min)
    {
        var window = new Size(ui.Window.ClientSize.Width, ui.Window.ClientSize.Height);
        var expected = DialogFrame.FrameSize(window, 0.8, 0.8, min, Max, 20);
        var rect = InWindow(ui, box);

        rect.Size.Should().Be(expected);
        rect.Left.Should().BeGreaterThanOrEqualTo(20);
        rect.Top.Should().BeGreaterThanOrEqualTo(20);
        rect.Right.Should().BeLessThanOrEqualTo(window.Width - 20);
        rect.Bottom.Should().BeLessThanOrEqualTo(window.Height - 20);
        (rect.Left + rect.Width / 2).Should().BeApproximately(window.Width / 2, 1);
        (rect.Top + rect.Height / 2).Should().BeApproximately(window.Height / 2, 1);

        var sideBar = box.GetVisualDescendants().OfType<Grid>().First(g => g.ColumnDefinitions.Count == 2);
        sideBar.ColumnDefinitions[0].ActualWidth.Should().Be(sideWidth, "the side list keeps its width; the content takes the rest");
        sideBar.ColumnDefinitions[1].ActualWidth.Should().Be(expected.Width - 2 - sideWidth, "content stretches with the dialog");
    }

    /// لا شيء يُقص: كل ما خارج منطقة التمرير داخل النافذة، ومنطقة التمرير نفسها داخلها
    private static void NothingClipped(UiHarness ui, Border box)
    {
        var boxRect = InWindow(ui, box).Inflate(0.5);
        var body = box.GetVisualDescendants().OfType<ScrollViewer>().First(s => s.Name == "Body");
        boxRect.Contains(InWindow(ui, body)).Should().BeTrue("the scrolling area is inside the dialog");

        foreach (var c in box.GetVisualDescendants().OfType<Control>()
                     .Where(c => c is TextBlock or Button && c.IsEffectivelyVisible && c.Bounds.Width > 0)
                     .Where(c => !c.GetVisualAncestors().Contains(body))
                     // قائمة الأقسام الجانبية منطقة تمرير أيضًا (10 أقسام لا تتسع في أصغر نافذة)
                     .Where(c => !c.GetVisualAncestors().Any(a => a is ScrollViewer { Name: "NavScroll" })))
            boxRect.Contains(InWindow(ui, c)).Should().BeTrue($"'{(c as TextBlock)?.Text ?? c.Name ?? c.GetType().Name}' is cut off");
    }

    [AvaloniaTheory]
    [MemberData(nameof(Cases))]
    public void Dialog_GrowsWithTheWindow_AndStaysInsideIt(string language, string dialog)
    {
        using var ui = new UiHarness(language);
        foreach (var (w, h) in new[] { (1280, 860), (1920, 1080), (2560, 1440), (980, 620) })
        {
            Resize(ui, w, h);
            var (_, box, side, min) = Open(ui, dialog);
            ShouldFit(ui, box, side, min);
            NothingClipped(ui, box);

            if (dialog == "settings") ui.Vm.CloseSettingsCommand.Execute(null);
            else ui.Vm.CloseDetailsCommand.Execute(null);
            ui.Settle();
        }
    }

    [AvaloniaTheory]
    [MemberData(nameof(Cases))]
    public void Dialog_FollowsResize_WhileOpen(string language, string dialog)
    {
        using var ui = new UiHarness(language);
        var (_, box, side, min) = Open(ui, dialog);
        ShouldFit(ui, box, side, min);

        foreach (var (w, h) in new[] { (1920, 1080), (1100, 700), (980, 620), (1600, 1000) })
        {
            Resize(ui, w, h);
            ShouldFit(ui, box, side, min);
            NothingClipped(ui, box);
        }
    }

    [AvaloniaTheory]
    [InlineData(Loc.Arabic)]
    [InlineData(Loc.English)]
    public void ScrollBar_AppearsOnlyWhenTheContentDoesNotFit(string language)
    {
        using var ui = new UiHarness(language);
        var (_, box, _, _) = Open(ui, "settings");
        var body = box.GetVisualDescendants().OfType<ScrollViewer>().First(s => s.Name == "Body");

        // على النافذة المكبّرة كل الأقسام تتسع
        Resize(ui, 1920, 1080);
        for (var i = 0; i < 6; i++)
        {
            ui.Vm.SelectSettingsSectionCommand.Execute(i.ToString());
            ui.Settle();
            body.Extent.Height.Should().BeLessThanOrEqualTo(body.Viewport.Height, $"section {i} fits, so no scroll bar");
            body.VerticalScrollBarVisibility.Should().Be(Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);
        }
        body.HorizontalScrollBarVisibility.Should().Be(Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            "content wraps to the new width instead of scrolling sideways");
    }

    [AvaloniaTheory]
    [InlineData(Loc.Arabic)]
    [InlineData(Loc.English)]
    public void CoverResults_UseTheWiderDialog_AndScrollOnlyWhenTheyOverflow(string language)
    {
        using var ui = new UiHarness(language);
        var (_, box, _, _) = Open(ui, "game");
        ui.Vm.SelectGameDialogSectionCommand.Execute("2");
        // نتائج بحث جاهزة بدل طلب شبكة حقيقي إلى SteamGridDB
        typeof(HalawaHub.App.ViewModels.MainViewModel).GetProperty(nameof(ui.Vm.IsCoverSearchOpen))!.SetValue(ui.Vm, true);
        for (var i = 0; i < 24; i++)
            ui.Vm.CoverCandidates.Add(new HalawaHub.Core.Covers.CoverCandidate("", ""));
        ui.Settle();
        var body = box.GetVisualDescendants().OfType<ScrollViewer>().First(s => s.Name == "Body");

        int PerRow()
        {
            var thumbs = box.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("thumb")).ToList();
            thumbs.Should().HaveCount(24);
            var firstRow = InWindow(ui, thumbs[0]).Top;
            return thumbs.Count(t => Math.Abs(InWindow(ui, t).Top - firstRow) < 1);
        }

        Resize(ui, 980, 620);
        var narrow = PerRow();
        body.Extent.Height.Should().BeGreaterThan(body.Viewport.Height, "24 covers do not fit the smallest dialog, so it scrolls");

        Resize(ui, 1920, 1080);
        PerRow().Should().BeGreaterThan(narrow * 2, "the wider dialog fits more covers per row");
        body.Extent.Height.Should().BeLessThanOrEqualTo(body.Viewport.Height, "they all fit now, so there is no scroll bar");

        ui.Vm.CloseCoverSearchCommand.Execute(null);
        ui.Vm.CloseDetailsCommand.Execute(null);
        ui.Settle();
    }

    [AvaloniaTheory]
    [InlineData(Loc.Arabic)]
    [InlineData(Loc.English)]
    public void SwitchRows_StayNextToTheirLabels_OnABigWindow(string language)
    {
        using var ui = new UiHarness(language);
        Resize(ui, 2560, 1440);
        var (_, box, _, _) = Open(ui, "settings");
        ui.Vm.SelectSettingsSectionCommand.Execute("4");
        ui.Settle();

        var title = box.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "Title");
        var row = box.GetVisualDescendants().OfType<CappedWidth>().First(c => c.IsEffectivelyVisible).Child!;
        row.Bounds.Width.Should().Be(480);

        // يبدأ من نفس جهة العنوان بدل أن يطفو في منتصف النافذة
        var rowRect = InWindow(ui, row);
        var titleRect = InWindow(ui, title);
        if (language == Loc.Arabic) rowRect.Right.Should().BeApproximately(titleRect.Right, 1);
        else rowRect.Left.Should().BeApproximately(titleRect.Left, 1);
    }

    [AvaloniaFact]
    public void Backdrop_AroundTheBiggerDialog_StillCloses()
    {
        using var ui = new UiHarness(Loc.English);
        Resize(ui, 1920, 1080);
        var (_, box, _, _) = Open(ui, "settings");
        var rect = InWindow(ui, box);

        // نقطة على الخلفية بين حافة البرنامج والنافذة
        var outside = new Point(rect.Left / 2, rect.Top + rect.Height / 2);
        ui.Window.MouseDown(outside, Avalonia.Input.MouseButton.Left);
        ui.Window.MouseUp(outside, Avalonia.Input.MouseButton.Left);
        ui.Settle();
        ui.Vm.IsSettingsOpen.Should().BeFalse();
    }
}
