using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using HalawaHub.App.Controls;
using HalawaHub.App.Services;
using HalawaHub.App.ViewModels;
using Xunit;

namespace HalawaHub.Tests.Ui;

/// <summary>
/// الاختبارات الأخرى توقف الحركة لتصل لحالة نهائية ثابتة. هنا تعمل كل الحركات:
/// إصدار 0.0.10.9 انهار عند الفتح بسبب حركة RenderTransform خاطئة.
/// </summary>
public class MotionTests
{
    private static void Run(UiHarness ui, int milliseconds)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Thread.Sleep(10);
        }
    }

    [AvaloniaTheory]
    [InlineData(Loc.Arabic)]
    [InlineData(Loc.English)]
    public void AllAnimations_Run_WithoutCrashing_AndEndInTheRightState(string language)
    {
        using var ui = new UiHarness(language);
        var vm = ui.Vm;

        MotionSettings.Enabled = true;
        try
        {
            // انزلاق الصفحات والمؤشرات ودخول البطاقات
            ui.Click(ui.All<Button>().First(b => b.Classes.Contains("tstab") && (string?)b.Tag == "2"));
            Run(ui, 800);
            vm.LibraryTabIndex.Should().Be(2);
            ui.All<Image>().Any(i => i.Name == "PageSnapshot").Should().BeFalse("the snapshot is removed after the slide");

            ui.Click(ui.All<Button>().First(b => b.Classes.Contains("tstab") && (string?)b.Tag == "0"));
            Run(ui, 800);

            // البطاقة: قائمة تقفز ثم تُغلق
            var card = vm.Games.First();
            vm.ToggleCardMenu(card);
            Run(ui, 350);
            vm.CloseAllCardMenus();
            Run(ui, 100);

            // البحث: توسع وهالة واهتزاز عند عدم وجود نتائج
            vm.IsSearchOpen = true;
            Run(ui, 500);
            vm.SearchQuery = "zzz";
            Run(ui, 300);
            vm.CloseSearch();
            Run(ui, 400);

            // الإعدادات: دخول وتنقل وشارات الحالة وخروج
            vm.OpenSettingsCommand.Execute(null);
            Run(ui, 500);
            foreach (var section in new[] { "1", "2", "3", "4", "5", "0" })
            {
                vm.SelectSettingsSectionCommand.Execute(section);
                Run(ui, 120);
            }

            vm.SelectSettingsSectionCommand.Execute("2");
            vm.ApiKeyStatus = ApiKeyCheckState.Checking;
            Run(ui, 200);
            vm.ApiKeyStatus = ApiKeyCheckState.Valid;
            Run(ui, 300);
            vm.ApiKeyStatus = ApiKeyCheckState.Invalid;
            Run(ui, 600);

            vm.SetLanguageCommand.Execute(language == Loc.Arabic ? Loc.English : Loc.Arabic);
            Run(ui, 450);
            vm.SetLanguageCommand.Execute(language);
            Run(ui, 100);

            vm.CloseSettingsCommand.Execute(null);
            Run(ui, 400);
            vm.IsSettingsVisible.Should().BeFalse("the dialog leaves after the closing animation");

            // نافذة اللعبة
            vm.OpenDetailsCommand.Execute(card);
            Run(ui, 450);
            foreach (var section in new[] { "1", "2", "3", "0" })
            {
                vm.SelectGameDialogSectionCommand.Execute(section);
                Run(ui, 120);
            }
            vm.CloseDetailsCommand.Execute(null);
            Run(ui, 400);
            vm.DetailsGame.Should().BeNull();

            // صفحة قريبًا: الشريط غير المحدد
            vm.GoDeveloperCommand.Execute(null);
            Run(ui, 300);
            vm.GoLibraryCommand.Execute(null);
            Run(ui, 200);

            // زر التحديث يدور ثم ✓
            vm.RefreshCommand.Execute(null);
            Run(ui, 300);
            ui.WaitFor(() => !vm.IsRefreshing);
            vm.IsRefreshDone.Should().BeTrue();

            ui.All<GameCard_Probe>().Should().BeEmpty();
        }
        finally
        {
            MotionSettings.Enabled = false;
        }
    }

    // نوع وهمي يتأكد أن All<T> لا يرمي بعد كل الحركات
    private sealed class GameCard_Probe : Control { }
}
