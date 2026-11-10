using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using HalawaHub.App.Controls;
using HalawaHub.App.Services;
using HalawaHub.App.ViewModels;
using HalawaHub.App.Views;
using Xunit;
using Path = Avalonia.Controls.Shapes.Path;

namespace HalawaHub.Tests.Ui;

/// <summary>
/// كل حركة تُقاس أثناء حدوثها: قيمة تمر بمراحل وسطى ثم تصل لنهايتها. الاختبار الذي
/// يقرأ الحالة النهائية فقط ينجح حتى لو قفز العنصر بلا أي حركة، وهذا ما أخفى توقف
/// الحركات على جهاز خيار "تأثيرات الحركة" فيه معطل.
/// </summary>
public class MotionTests
{
    private static List<double> Sample(UiHarness ui, int milliseconds, Func<double> read)
    {
        var values = new List<double>();

        // أول دفعة تعالج كل ما تراكم على خيط الواجهة، وقد تأخذ أطول من النافذة كلها على
        // جهاز بطيء. لو بدأ العد قبلها لخرجنا بعينة واحدة وظننا أن الحركة لم تعمل.
        PumpOnce();
        values.Add(read());

        var deadline = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (DateTime.UtcNow < deadline)
        {
            PumpOnce();
            values.Add(read());
            Thread.Sleep(8);
        }
        return values;
    }

    private static void PumpOnce()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// ينتظر شرطًا بدل وقت ثابت، فلا يهم كم يأخذ الجهاز. ساعة الحركة في Avalonia حقيقية:
    /// لو توقف الخيط طوال الحركة تضيع مراحلها الوسطى، فعند ثبات القيمة قبل تحقق الشرط
    /// نطلق الحركة من جديد (retrigger) ونقيس المحاولة الجديدة وحدها.
    /// </summary>
    private static List<double> SampleUntil(Func<double> read, Func<List<double>, bool> done, string what, Action? retrigger = null)
    {
        var values = new List<double>();
        var timeout = DateTime.UtcNow.AddSeconds(30);
        var lastChange = DateTime.UtcNow;
        var pumpsSinceChange = 0;

        while (DateTime.UtcNow < timeout)
        {
            PumpOnce();

            var value = read();
            if (values.Count > 0 && Math.Round(value, 3) == Math.Round(values[^1], 3))
            {
                pumpsSinceChange++;
            }
            else
            {
                pumpsSinceChange = 0;
                lastChange = DateTime.UtcNow;
            }

            values.Add(value);
            if (done(values)) return values;

            // العدّان معًا: بعد توقف طويل يكفي الوقت وحده رغم أن الحركة بدأت للتو
            if (retrigger != null && pumpsSinceChange >= 60 && DateTime.UtcNow - lastChange > TimeSpan.FromSeconds(1.5))
            {
                retrigger();
                values.Clear();
                pumpsSinceChange = 0;
                lastChange = DateTime.UtcNow;
            }

            Thread.Sleep(8);
        }

        throw new Xunit.Sdk.XunitException(
            $"{what}: condition not met in 30s (saw: {string.Join(" ", values.Select(v => v.ToString("0.###")))})");
    }

    private static int DistinctCount(List<double> values) => values.Select(v => Math.Round(v, 3)).Distinct().Count();

    private static void ShouldHaveMoved(List<double> values, string what, int minimumSteps = 3) =>
        values.Select(v => Math.Round(v, 3)).Distinct().Count().Should()
            .BeGreaterThanOrEqualTo(minimumSteps, $"{what} must pass through in-between values, not jump (saw: {string.Join(" ", values.Select(v => v.ToString("0.###")))})");

    private static double ScaleOf(Visual v) => v.RenderTransform?.Value.M11 ?? 1;
    private static double OffsetYOf(Visual v) => v.RenderTransform?.Value.M32 ?? 0;

    private static Point CenterOf(UiHarness ui, Visual v) =>
        v.TranslatePoint(new Point(v.Bounds.Width / 2, v.Bounds.Height / 2), ui.Window)!.Value;

    private static T Named<T>(UiHarness ui, string name) where T : Control =>
        ui.Window.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    private static Border DialogBoxOf<TDialog>(UiHarness ui) where TDialog : UserControl =>
        ui.Window.GetVisualDescendants().OfType<TDialog>().Single()
            .GetVisualDescendants().OfType<Border>().First(b => b.Name == "DialogBox");

    private static Control Pill(UiHarness ui, Func<SlidingPill, bool> which) =>
        ui.All<SlidingPill>().First(which).Child!;

    private static void MoveAwayAndRest(UiHarness ui)
    {
        ui.Window.MouseMove(new Point(1270, 850));
        Sample(ui, 450, () => 0);
    }

    [AvaloniaFact]
    public void DefaultsToOn_EvenWhenWindowsAnimationsAreOff()
    {
        // خيار ويندوز معطل على جهاز المالك، وكان يوقف كل الحركات بصمت
        typeof(MotionSettings).GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            .Select(m => m.Name).Should().NotContain("ReadSystemSetting");
    }

    [AvaloniaTheory]
    [InlineData(Loc.Arabic)]
    [InlineData(Loc.English)]
    public void HoverAndPress_OnButtons_AndCards(string language)
    {
        using var ui = new UiHarness(language, motion: true);
        Sample(ui, 200, () => 0);

        // البطاقة ترتفع 4 عند المرور
        var card = ui.All<GameCard>().First();
        var face = card.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("face"));
        ui.Window.MouseMove(CenterOf(ui, card));
        var lift = Sample(ui, 350, () => OffsetYOf(face));
        ShouldHaveMoved(lift, "card hover lift");
        lift.Last().Should().BeApproximately(-4, 0.01);

        // زر التشغيل: لون المرور، ثم انضغاط، ثم رجوع، مع وميض
        var play = card.GetVisualDescendants().OfType<Button>().First(b => b.Classes.Contains("play"));
        var ripple = play.GetVisualDescendants().OfType<Ellipse>().First(e => e.Name == "PART_Ripple");
        var playCenter = CenterOf(ui, play);
        ui.Window.MouseMove(playCenter);
        var fill = Sample(ui, 300, () => ((ISolidColorBrush)play.Background!).Color.R);
        ShouldHaveMoved(fill, "play hover colour");

        ui.Window.MouseDown(playCenter, MouseButton.Left);
        var press = Sample(ui, 350, () => ScaleOf(play));
        ShouldHaveMoved(press, "play press");
        press.Min().Should().BeLessThan(0.97);
        Math.Round(ripple.RenderTransform!.Value.M11, 2).Should().Be(0, "ripple collapses to the press point");

        ui.Vm.LaunchGameCommand.CanExecute(null);
        var releaseAt = new Point(playCenter.X, playCenter.Y);
        ui.Window.MouseMove(new Point(1270, 850));
        ui.Window.MouseUp(new Point(1270, 850), MouseButton.Left);
        var rippleOut = Sample(ui, 450, () => ripple.RenderTransform!.Value.M11);
        ShouldHaveMoved(rippleOut, "ripple expanding on release");
        releaseAt.X.Should().BeGreaterThan(0);

        MoveAwayAndRest(ui);
        OffsetYOf(face).Should().BeApproximately(0, 0.01, "the card settles back");
    }

    [AvaloniaTheory]
    [InlineData(Loc.Arabic)]
    [InlineData(Loc.English)]
    public void SlidingPills_Stretch_ThenSettle(string language)
    {
        using var ui = new UiHarness(language, motion: true);
        var vm = ui.Vm;
        Sample(ui, 200, () => 0);

        // المكتبة / المطورين
        var top = Pill(ui, p => p.Inset == 6);
        var restWidth = top.Bounds.Width;
        vm.GoDeveloperCommand.Execute(null);
        var topWidths = Sample(ui, 700, () => top.Bounds.Width);
        ShouldHaveMoved(topWidths, "top pill");
        topWidths.Max().Should().BeGreaterThan(restWidth * 1.1, "the pill stretches on its way");
        topWidths.Last().Should().BeApproximately(restWidth, 1);
        vm.GoLibraryCommand.Execute(null);
        Sample(ui, 700, () => 0);

        // خط التبويبات
        var underline = Pill(ui, p => p.Motion == PillMotion.Underline);
        var underlineWidth = underline.Bounds.Width;
        vm.SelectLibraryTab(2);
        var underlineWidths = Sample(ui, 600, () => underline.Bounds.Width);
        underlineWidths.Max().Should().BeGreaterThan(underlineWidth * 1.3, "the leading edge moves first, then the trailing edge follows");
        vm.SelectLibraryTab(0);
        Sample(ui, 1000, () => 0);

        // شريط المنصات
        var chipPill = Pill(ui, p => p.Inset == 4 && p.Motion == PillMotion.Spring);
        var chipLeft = chipPill.Bounds.Left;
        ui.Click(ui.All<Button>().First(b => b.Classes.Contains("chip") && ((PlatformChip)b.DataContext!).Key == "GOG"));
        ShouldHaveMoved(Sample(ui, 700, () => chipPill.Bounds.Left + chipPill.Bounds.Width), "platform chip pill");
        chipPill.Bounds.Left.Should().NotBe(chipLeft);
        vm.SelectedPlatform = null;
        Sample(ui, 800, () => 0);

        // القائمة الجانبية في الإعدادات
        vm.OpenSettingsCommand.Execute(null);
        Sample(ui, 500, () => 0);
        var settingsPill = Pill(ui, p => p.Orientation == Avalonia.Layout.Orientation.Vertical && p.ItemCount == 6);
        vm.SelectSettingsSectionCommand.Execute("3");
        var heights = Sample(ui, 700, () => settingsPill.Bounds.Height);
        heights.Max().Should().BeGreaterThan(44 * 1.5, "the side pill stretches vertically");
        heights.Last().Should().BeApproximately(44, 1);

        // زر اللغة
        vm.SelectSettingsSectionCommand.Execute("1");
        Sample(ui, 700, () => 0);
        var langPill = Pill(ui, p => p.Motion == PillMotion.Even);
        ShouldHaveMoved(CaptureLanguageSwitch(ui, langPill), "language pill");
        vm.SetLanguageCommand.Execute(language);
    }

    private static List<double> CaptureLanguageSwitch(UiHarness ui, Control pill)
    {
        var other = Loc.Instance.Language == Loc.Arabic ? Loc.English : Loc.Arabic;
        ui.Vm.SetLanguageCommand.Execute(other);
        return Sample(ui, 500, () => pill.Bounds.Left);
    }

    [AvaloniaTheory]
    [InlineData(Loc.Arabic)]
    [InlineData(Loc.English)]
    public void CardMenu_MorphsToX_AndPopsIn(string language)
    {
        using var ui = new UiHarness(language, motion: true);
        Sample(ui, 200, () => 0);

        var view = ui.All<GameCard>().First();
        var card = (GameCardViewModel)view.DataContext!;
        var icon = view.GetVisualDescendants().OfType<MenuCloseIcon>().First();
        var menu = view.GetVisualDescendants().OfType<Border>().First(b => b.Name == "Menu");

        ui.Vm.ToggleCardMenu(card);
        var samples = new List<(double Progress, double Scale)>();
        Sample(ui, 550, () => { samples.Add((icon.Progress, ScaleOf(menu))); return 0; });

        ShouldHaveMoved(samples.Select(s => s.Progress).ToList(), "menu icon morph");
        samples.Max(s => s.Progress).Should().BeGreaterThan(1, "the morph overshoots like the design's spring");
        icon.Progress.Should().BeApproximately(1, 0.001);
        ShouldHaveMoved(samples.Select(s => s.Scale).ToList(), "menu pop");
        samples.Max(s => s.Scale).Should().BeGreaterThan(1, "the pop overshoots");
        menu.Opacity.Should().BeApproximately(1, 0.01);

        // الأيقونة الصغيرة في القائمة تنزلق مقابضها عند المرور
        var sliders = view.GetVisualDescendants().OfType<SlidersIcon>().First();
        sliders.IsFlipped = true;
        var knob = Sample(ui, 700, () => sliders.FirstKnob);
        ShouldHaveMoved(knob, "menu sliders knob");
        knob.Last().Should().BeApproximately(-1, 0.001);
        sliders.IsFlipped = false;

        ui.Vm.CloseAllCardMenus();
        ShouldHaveMoved(Sample(ui, 550, () => icon.Progress), "menu icon morph back");
    }

    [AvaloniaFact]
    public void SettingsButton_SlidesKnobs_AndFadesGradient_OnRealHover()
    {
        using var ui = new UiHarness(Loc.English, motion: true);
        Sample(ui, 200, () => 0);

        var button = Named<Button>(ui, "SettingsButton");
        var sliders = Named<SlidersIcon>(ui, "Sliders");
        sliders.FirstKnob.Should().Be(1);

        ui.Window.MouseMove(CenterOf(ui, button));
        var knob = Sample(ui, 750, () => sliders.FirstKnob);
        ShouldHaveMoved(knob, "settings knobs");
        knob.Last().Should().BeApproximately(-1, 0.001);

        ui.Window.MouseMove(new Point(640, 700));
        ShouldHaveMoved(Sample(ui, 750, () => sliders.FirstKnob), "settings knobs returning");
        sliders.FirstKnob.Should().BeApproximately(1, 0.001);
    }

    [AvaloniaFact]
    public void Search_Expands_WithSpring_AndShowsItsParts()
    {
        using var ui = new UiHarness(Loc.English, motion: true);
        var vm = ui.Vm;
        Sample(ui, 200, () => 0);

        vm.RecentSearches.Clear();
        var box = Named<Panel>(ui, "Box");
        var input = Named<TextBox>(ui, "Input");
        box.Width.Should().Be(44);

        var drop = Named<Border>(ui, "Dropdown");
        var icon = Named<VectorIcon>(ui, "SearchIcon");

        // ضغطة الزر المغلق تنكمش
        var open = Named<Button>(ui, "OpenButton");
        var at = CenterOf(ui, open);
        ui.Window.MouseMove(at);
        ui.Window.MouseDown(at, MouseButton.Left);
        ShouldHaveMoved(SampleUntil(() => ScaleOf(box),
            v => DistinctCount(v) >= 3 && v[^1] < 0.95, "search press",
            retrigger: () =>
            {
                ui.Window.MouseUp(at, MouseButton.Left);
                Sample(ui, 200, () => 0);
                ui.Window.MouseDown(at, MouseButton.Left);
            }), "search press");
        box.RenderTransform!.Value.M11.Should().BeLessThan(0.95);
        ui.Window.MouseUp(at, MouseButton.Left);

        // إغلاق يمر عبر الزر نفسه كما يفعل المستخدم، ثم فتح جديد
        void ReopenSearch()
        {
            vm.IsSearchOpen = false;
            Sample(ui, 450, () => 0);
            vm.IsSearchOpen = true;
        }

        var widths = SampleUntil(() => box.Width,
            v => DistinctCount(v) >= 3 && v.Max() > 340 && v[^1] == 340, "search expanding", retrigger: ReopenSearch);
        ShouldHaveMoved(widths, "search expanding");
        widths.Max().Should().BeGreaterThan(340, "it overshoots like the design's spring");
        box.Width.Should().Be(340);
        vm.IsSearchOpen.Should().BeTrue();
        SampleUntil(() => input.Opacity, v => v[^1] == 1, "search input fade");

        vm.SearchQuery = "el";
        ShouldHaveMoved(SampleUntil(() => drop.Opacity,
            v => DistinctCount(v) >= 3 && v[^1] == 1, "search dropdown fade",
            retrigger: () =>
            {
                vm.SearchQuery = "";
                Sample(ui, 100, () => 0);
                vm.SearchQuery = "el";
            }), "search dropdown fade");

        // انحدار: توقف خيط الواجهة على جهاز CI كان يترك عينة واحدة خلال النافذة الثابتة،
        // فيفشل الاختبار والحركة سليمة. هذا التوقف يبقى هنا ليحرس الانتظار بالشرط.
        Dispatcher.UIThread.Post(() => Thread.Sleep(800));
        vm.SearchQuery = "zzz";
        var shake = SampleUntil(() => icon.RenderTransform?.Value.M31 ?? 0,
            v => DistinctCount(v) >= 3, "no-match shake",
            retrigger: () => vm.SearchQuery += "z");
        ShouldHaveMoved(shake, "no-match shake");
        var bubble = Named<Border>(ui, "Bubble");
        bubble.IsEffectivelyVisible.Should().BeTrue();

        vm.CloseSearch();
        var closing = SampleUntil(() => box.Width,
            v => DistinctCount(v) >= 3 && v[^1] == 44, "search closing",
            retrigger: () =>
            {
                vm.IsSearchOpen = true;
                Sample(ui, 500, () => 0);
                vm.CloseSearch();
            });
        ShouldHaveMoved(closing, "search closing");
        box.Width.Should().Be(44);
    }

    [AvaloniaFact]
    public void RefreshIcon_Spins_WhileScanning_ThenShowsCheck()
    {
        using var gate = new ManualResetEventSlim(true);
        using var ui = new UiHarness(Loc.English, motion: true, scanGate: gate);
        Sample(ui, 100, () => 0);

        gate.Reset();
        ui.Vm.RefreshCommand.Execute(null);

        var icon = Named<VectorIcon>(ui, "RefreshIcon");
        var angles = Sample(ui, 400, () => (icon.RenderTransform as RotateTransform)?.Angle ?? 0);
        ShouldHaveMoved(angles, "refresh spin", minimumSteps: 5);
        ui.Vm.IsRefreshing.Should().BeTrue();

        gate.Set();
        ui.WaitFor(() => !ui.Vm.IsRefreshing);

        var check = ui.Window.GetVisualDescendants().OfType<VectorIcon>().First(v => v.Classes.Contains("rf-b"));
        ShouldHaveMoved(Sample(ui, 350, () => check.Opacity), "check appearing", minimumSteps: 2);
        check.Opacity.Should().Be(1);
    }

    [AvaloniaTheory]
    [InlineData(Loc.Arabic)]
    [InlineData(Loc.English)]
    public void Dialogs_AnimateIn_AndOut(string language)
    {
        using var ui = new UiHarness(language, motion: true);
        var vm = ui.Vm;
        Sample(ui, 200, () => 0);

        // عناصر النافذة لا تُبنى قبل أول فتح، فنحضرها بعد الفتح
        Border Box<TDialog>() where TDialog : UserControl => DialogBoxOf<TDialog>(ui);

        vm.OpenSettingsCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        var settingsBox = Box<SettingsDialog>();
        var inSettings = Sample(ui, 480, () => ScaleOf(settingsBox));
        ShouldHaveMoved(inSettings, "settings opening", minimumSteps: 2);
        inSettings.Max().Should().BeGreaterThan(1, "the dialog overshoots like the design's spring");
        settingsBox.Opacity.Should().Be(1);

        vm.CloseSettingsCommand.Execute(null);
        vm.IsSettingsVisible.Should().BeTrue("it stays visible during the closing animation");
        var outSettings = Sample(ui, 200, () => settingsBox.Opacity);
        ShouldHaveMoved(outSettings, "settings closing", minimumSteps: 2);
        Sample(ui, 150, () => 0);
        vm.IsSettingsVisible.Should().BeFalse();

        vm.OpenDetailsCommand.Execute(vm.Games.First());
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        var gameBox = Box<GameDialog>();
        var inGame = Sample(ui, 450, () => ScaleOf(gameBox));
        ShouldHaveMoved(inGame, "game dialog opening", minimumSteps: 2);

        // أقسام النافذة تدخل بانزلاق
        vm.SelectGameDialogSectionCommand.Execute("2");
        var title = ui.Window.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "Title" && t.IsEffectivelyVisible);
        ShouldHaveMoved(Sample(ui, 300, () => title.Opacity), "section title", minimumSteps: 2);

        vm.CloseDetailsCommand.Execute(null);
        ShouldHaveMoved(Sample(ui, 200, () => gameBox.Opacity), "game dialog closing", minimumSteps: 2);
        Sample(ui, 150, () => 0);
        vm.DetailsGame.Should().BeNull();
    }

    /// <summary>
    /// النافذتان صار حجمهما يتبع البرنامج: الظهور والإغلاق والحبة المنزلقة يجب أن تبقى
    /// متحركة بعد تكبير البرنامج، وبعد تغيير حجمه والنافذة مفتوحة.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(Loc.Arabic)]
    [InlineData(Loc.English)]
    public void Dialogs_StillAnimate_AfterWindowResize(string language)
    {
        using var ui = new UiHarness(language, motion: true);
        var vm = ui.Vm;
        ui.Window.Width = 1920;
        ui.Window.Height = 1080;
        Sample(ui, 200, () => 0);

        vm.OpenSettingsCommand.Execute(null);
        PumpOnce();
        var box = DialogBoxOf<SettingsDialog>(ui);
        var opening = Sample(ui, 480, () => ScaleOf(box));
        ShouldHaveMoved(opening, "settings opening on a big window", minimumSteps: 2);
        opening.Max().Should().BeGreaterThan(1, "the dialog keeps its spring at the new size");
        box.Bounds.Width.Should().Be(1400);

        // تصغير البرنامج والنافذة مفتوحة، ثم التنقل بين الأقسام
        ui.Window.Width = 1100;
        ui.Window.Height = 700;
        Sample(ui, 100, () => 0);
        box.Bounds.Width.Should().Be(900, "80% of 1100 is below the design size, so the design size wins");

        var pill = Pill(ui, p => p.Orientation == Avalonia.Layout.Orientation.Vertical && p.ItemCount == 6);
        vm.SelectSettingsSectionCommand.Execute("4");
        var heights = Sample(ui, 700, () => pill.Bounds.Height);
        heights.Max().Should().BeGreaterThan(44 * 1.5, "the side pill still stretches on its way");
        heights.Last().Should().BeApproximately(44, 1);
        var selected = ui.All<Button>().First(b => b.Classes.Contains("si") && b.Classes.Contains("on"));
        CenterOf(ui, pill).Y.Should().BeApproximately(CenterOf(ui, selected).Y, 1, "the pill lands on the selected item");

        vm.CloseSettingsCommand.Execute(null);
        ShouldHaveMoved(Sample(ui, 200, () => box.Opacity), "settings closing after resize", minimumSteps: 2);
        Sample(ui, 150, () => 0);
        vm.IsSettingsVisible.Should().BeFalse();

        // نافذة اللعبة: تُفتح بعد تكبير البرنامج مرة أخرى
        ui.Window.Width = 2560;
        ui.Window.Height = 1440;
        Sample(ui, 100, () => 0);
        vm.OpenDetailsCommand.Execute(vm.Games.First());
        PumpOnce();
        var gameBox = DialogBoxOf<GameDialog>(ui);
        ShouldHaveMoved(Sample(ui, 450, () => ScaleOf(gameBox)), "game dialog opening on a big window", minimumSteps: 2);
        gameBox.Bounds.Size.Should().Be(new Size(1400, 900), "capped so text does not stretch on very large screens");

        var gamePill = Pill(ui, p => p.Orientation == Avalonia.Layout.Orientation.Vertical && p.ItemCount == 4);
        vm.SelectGameDialogSectionCommand.Execute("3");
        ShouldHaveMoved(Sample(ui, 700, () => gamePill.Bounds.Height), "game dialog pill");

        vm.CloseDetailsCommand.Execute(null);
        ShouldHaveMoved(Sample(ui, 200, () => gameBox.Opacity), "game dialog closing", minimumSteps: 2);
        Sample(ui, 150, () => 0);
        vm.DetailsGame.Should().BeNull();
    }

    [AvaloniaFact]
    public void StatusBadge_Pending_Done_Error()
    {
        using var ui = new UiHarness(Loc.English, motion: true);
        var vm = ui.Vm;
        vm.OpenSettingsCommand.Execute(null);
        vm.SelectSettingsSectionCommand.Execute("2");
        Sample(ui, 500, () => 0);

        vm.ApiKeyStatus = ApiKeyCheckState.Checking;
        Sample(ui, 50, () => 0);
        var badge = ui.All<StatusBadge>().First();
        var ring = badge.GetVisualChildren().OfType<Ellipse>().First(e => e.Stroke != null);
        var dot = badge.GetVisualChildren().OfType<Ellipse>().First(e => e.Fill != null);
        ShouldHaveMoved(Sample(ui, 300, () => ((RotateTransform)ring.RenderTransform!).Angle), "pending ring spin", 5);
        ShouldHaveMoved(Sample(ui, 300, () => dot.Opacity), "pending dot breathing", 5);

        vm.ApiKeyStatus = ApiKeyCheckState.Valid;
        var fill = badge.GetVisualChildren().OfType<Border>().First();
        var check = badge.GetVisualChildren().OfType<Path>().First(p => p.IsVisible);
        var samples = new List<(double Fill, double Dash)>();
        Sample(ui, 500, () => { samples.Add((fill.Opacity, check.StrokeDashOffset)); return 0; });
        ShouldHaveMoved(samples.Select(s => s.Fill).ToList(), "done badge pop");
        ShouldHaveMoved(samples.Select(s => s.Dash).ToList(), "check mark drawing");

        vm.ApiKeyStatus = ApiKeyCheckState.Invalid;
        var cross = badge.GetVisualChildren().OfType<Path>().First(p => p.IsVisible);
        var shake = new List<double>();
        var dash = new List<double>();
        Sample(ui, 600, () => { shake.Add(fill.RenderTransform?.Value.M31 ?? 0); dash.Add(cross.StrokeDashOffset); return 0; });
        ShouldHaveMoved(shake, "error shake");
        ShouldHaveMoved(dash, "x mark drawing");
    }

    [AvaloniaTheory]
    [InlineData(Loc.Arabic)]
    [InlineData(Loc.English)]
    public void Tabs_SlidePages_AndCardsStaggerIn(string language)
    {
        using var ui = new UiHarness(language, motion: true);
        Sample(ui, 500, () => 0);

        var snapshot = Named<Image>(ui, "PageSnapshot");
        ui.Click(ui.All<Button>().First(b => b.Classes.Contains("tstab") && (string?)b.Tag == "2"));
        snapshot.IsVisible.Should().BeTrue("the old page is on screen while sliding out");
        var moves = Sample(ui, 500, () => snapshot.RenderTransform?.Value.M31 ?? 0);
        ShouldHaveMoved(moves, "old page sliding out");

        // البطاقات تدخل واحدة تلو الأخرى
        var cards = ui.Window.GetVisualDescendants().OfType<GameCard>().Select(c => (Visual)c.GetVisualParent()!).ToList();
        Sample(ui, 500, () => 0);
        snapshot.IsVisible.Should().BeFalse("removed when the slide ends");

        ui.Vm.SelectedPlatform = "Steam";
        Dispatcher.UIThread.RunJobs();
        ui.Vm.SelectedPlatform = null;
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        var last = ui.Window.GetVisualDescendants().OfType<GameCard>().Last();
        var container = (Visual)last.GetVisualParent()!;
        var entrance = Sample(ui, 800, () => container.Opacity);
        entrance.First().Should().BeLessThan(0.2, "the last card waits for its turn");
        ShouldHaveMoved(entrance, "staggered card entrance");
        container.Opacity.Should().Be(1);
        cards.Should().NotBeEmpty();
    }

    [AvaloniaFact]
    public void Switch_CheckSpinner_ComingSoonBar_AndLanguageFade()
    {
        using var ui = new UiHarness(Loc.English, motion: true);
        var vm = ui.Vm;
        vm.OpenDetailsCommand.Execute(vm.Games.First());
        Sample(ui, 500, () => 0);

        var toggle = ui.All<ToggleButton_>().Any();
        toggle.Should().BeFalse();
        var sw = ui.All<Avalonia.Controls.Primitives.ToggleButton>().First();
        var thumb = sw.GetVisualDescendants().OfType<Ellipse>().First(e => e.Name == "PART_Thumb");
        sw.IsChecked = !sw.IsChecked;
        ShouldHaveMoved(Sample(ui, 500, () => thumb.Margin.Left), "switch thumb");
        sw.IsChecked = !sw.IsChecked;
        Sample(ui, 500, () => 0);
        vm.CloseDetailsCommand.Execute(null);
        Sample(ui, 300, () => 0);

        vm.GoDeveloperCommand.Execute(null);
        Sample(ui, 100, () => 0);
        var bar = Named<Border>(ui, "BarFill");
        ShouldHaveMoved(Sample(ui, 400, () => bar.RenderTransform?.Value.M31 ?? 0), "indeterminate bar", 5);
        vm.GoLibraryCommand.Execute(null);
        Sample(ui, 100, () => 0);

        var page = Named<Grid>(ui, "PageContent");
        vm.SetLanguageCommand.Execute(Loc.Arabic);
        ShouldHaveMoved(Sample(ui, 400, () => page.Opacity), "language fade");
        page.Opacity.Should().Be(1);
    }

    [AvaloniaFact]
    public void Rotations_DoNotMirror_InArabic()
    {
        // التدوير يتبع FlowDirection، والتصميم يدور مع عقارب الساعة باللغتين:
        // القياس بالماوس الحقيقي وجد دائرة الانتظار تدور بالعكس في العربية
        using var ui = new UiHarness(Loc.Arabic, motion: true);
        var vm = ui.Vm;

        vm.OpenSettingsCommand.Execute(null);
        Sample(ui, 100, () => 0);
        var ring = ui.Window.GetVisualDescendants().OfType<Panel>().First(p => p.Name == "SpinRing");
        ring.FlowDirection.Should().Be(FlowDirection.LeftToRight);
        vm.CloseSettingsCommand.Execute(null);
        Sample(ui, 350, () => 0);

        Named<VectorIcon>(ui, "RefreshIcon").FlowDirection.Should().Be(FlowDirection.LeftToRight);

        vm.OpenDetailsCommand.Execute(vm.Games.First());
        Sample(ui, 100, () => 0);
        vm.SelectGameDialogSectionCommand.Execute("2");
        Sample(ui, 100, () => 0);
        ui.All<Border>().First(b => b.Classes.Contains("cover")).FlowDirection.Should().Be(FlowDirection.LeftToRight);
    }

    [AvaloniaFact]
    public void SmallerDetails_AllMove()
    {
        var pending = new TaskCompletionSource<UpdateCheckResult>();
        var calls = 0;
        var updates = new Moq.Mock<IUpdateService>();
        updates.Setup(u => u.CheckForUpdateAsync()).Returns(() =>
            ++calls == 1
                ? Task.FromResult(new UpdateCheckResult(false, null, null, null, "ok"))
                : pending.Task);

        using var ui = new UiHarness(Loc.English, updates.Object, motion: true);
        var vm = ui.Vm;
        Sample(ui, 200, () => 0);

        // هالة زر البحث تنبض باستمرار
        var halo = ui.Window.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("halo"));
        ShouldHaveMoved(Sample(ui, 300, () => ScaleOf(halo)), "search halo");

        // زر القائمة: الأيقونة تنكمش عند الضغط
        var eb = ui.All<Button>().First(b => b.Classes.Contains("eb"));
        var mt = eb.GetVisualDescendants().OfType<Panel>().First(p => p.Classes.Contains("mt"));
        var ebAt = CenterOf(ui, eb);
        ui.Window.MouseMove(ebAt);
        ui.Window.MouseDown(ebAt, MouseButton.Left);
        ShouldHaveMoved(Sample(ui, 200, () => ScaleOf(mt)), "menu icon press");
        ui.Window.MouseUp(ebAt, MouseButton.Left);
        vm.CloseAllCardMenus();
        MoveAwayAndRest(ui);

        // اقتراحات البحث تنزل واحدًا تلو الآخر، والعلامة ? تقفز
        vm.IsSearchOpen = true;
        Sample(ui, 100, () => 0);
        vm.SearchQuery = "a";
        var list = Named<ItemsControl>(ui, "SuggestionList");
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        var row = (Visual)list.GetVisualDescendants().OfType<Button>().First().GetVisualParent()!;
        ShouldHaveMoved(Sample(ui, 450, () => OffsetYOf(row)), "suggestion row");

        vm.SearchQuery = "zzz";
        Dispatcher.UIThread.RunJobs();
        var bubble = Named<Border>(ui, "Bubble");
        ShouldHaveMoved(Sample(ui, 600, () => ScaleOf(bubble)), "question bubble pop");
        var bob = bubble.GetVisualDescendants().OfType<TextBlock>().First();
        ShouldHaveMoved(Sample(ui, 300, () => OffsetYOf(bob)), "question bubble bob");
        vm.CloseSearch();
        Sample(ui, 400, () => 0);

        // دائرة الانتظار في زر الفحص، ثم الشارة، ثم النص يقفز
        vm.OpenSettingsCommand.Execute(null);
        Sample(ui, 500, () => 0);
        vm.CheckForUpdatesNowCommand.Execute(null);
        var spin = ui.Window.GetVisualDescendants().OfType<Panel>().First(p => p.Classes.Contains("spin"));
        ShouldHaveMoved(Sample(ui, 350, () => spin.Bounds.Width), "check button spinner opening");
        spin.Bounds.Width.Should().BeApproximately(16, 0.5);

        var ring = spin.GetVisualDescendants().OfType<Panel>().First(p => p.Classes.Contains("spin-ring"));
        ShouldHaveMoved(Sample(ui, 250, () => (ring.RenderTransform as RotateTransform)?.Angle ?? 0), "check button spinner", 4);

        var label = Named<TextBlock>(ui, "UpdateStatusLabel");
        pending.SetResult(new UpdateCheckResult(false, null, null, null, "ok"));
        Dispatcher.UIThread.RunJobs();
        ShouldHaveMoved(Sample(ui, 500, () => ScaleOf(label)), "result text pop");

        // زر الإغلاق يدور عند المرور
        var close = ui.All<Button>().First(b => b.Classes.Contains("x"));
        var closeIcon = close.GetVisualDescendants().OfType<VectorIcon>().First();
        ui.Window.MouseMove(CenterOf(ui, close));
        var rotate = Sample(ui, 450, () => closeIcon.RenderTransform?.Value.M12 ?? 0);
        ShouldHaveMoved(rotate, "close button rotation");
        vm.CloseSettingsCommand.Execute(null);
        Sample(ui, 350, () => 0);

        // غلاف نافذة اللعبة يميل عند المرور
        vm.OpenDetailsCommand.Execute(vm.Games.First());
        Sample(ui, 450, () => 0);
        vm.SelectGameDialogSectionCommand.Execute("2");
        Sample(ui, 450, () => 0);
        var cover = ui.All<Border>().First(b => b.Classes.Contains("cover"));
        ui.Window.MouseMove(CenterOf(ui, cover));
        var tilt = Sample(ui, 500, () => OffsetYOf(cover));
        ShouldHaveMoved(tilt, "cover tilt");
        tilt.Last().Should().BeLessThan(-3);
    }

    private sealed class ToggleButton_ : Control { }
}
