using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using FluentAssertions;
using HalawaHub.App.BigPicture;
using HalawaHub.App.BigPicture.Input;
using HalawaHub.App.Services;
using Xunit;

namespace HalawaHub.Tests;

/// <summary>تحديث التصميم 5.2 و5.3: العمود الجانبي في الرئيسي، و«لعبك هذا الأسبوع»، وسجل الأيام.</summary>
public class BigPictureHomeSideTests : IDisposable
{
    public void Dispose()
    {
        Loc.Instance.Language = Loc.Arabic;
        MotionSettings.Enabled = true;
    }

    private static BigPictureViewModel Vm(FakeBpHost host) => new(host, _ => Task.CompletedTask);

    [AvaloniaFact]
    public void Screenshots_cycle_only_while_home_shows_and_stop_under_a_panel()
    {
        Loc.Instance.Language = Loc.English;
        var host = FakeBpHost.WithGames(4);
        host.ShotList.AddRange(new[] { "https://s/1.jpg", "https://s/2.jpg", "https://s/3.jpg", "https://s/4.jpg" });
        var vm = Vm(host);
        vm.Press(BpButton.Right);    // اللعبة في الأعلى تتغير فتُحمَّل لقطاتها

        vm.HasShots.Should().BeTrue();
        vm.ShotCaption.Should().Be("Screenshot 1 / 4");
        vm.ShotDots.Should().Equal(true, false, false, false);
        vm.IsShotTimerRunning.Should().BeTrue();

        vm.NextShot();
        vm.ShotCaption.Should().Be("Screenshot 2 / 4");
        vm.ShotDots.Should().Equal(false, true, false, false);

        vm.Press(BpButton.Menu);
        vm.IsShotTimerRunning.Should().BeFalse("the guide is over home");
        vm.Press(BpButton.B);
        vm.IsShotTimerRunning.Should().BeTrue();
        vm.GoTab(BpScreen.Library);
        vm.IsShotTimerRunning.Should().BeFalse("home is not visible");
        vm.GoTab(BpScreen.Home);
        MotionSettings.Enabled = false;
        vm.Press(BpButton.Right);
        vm.IsShotTimerRunning.Should().BeFalse("reduce motion keeps the screenshot still");
    }

    [AvaloniaFact]
    public void Activity_shows_the_streak_and_the_last_session()
    {
        Loc.Instance.Language = Loc.English;
        var host = FakeBpHost.WithGames(2);
        host.ActivityValue = new BpActivity(4, DateTime.Now.AddDays(-1), TimeSpan.FromHours(2));
        var vm = Vm(host);
        vm.StreakText.Should().Be("4 days in a row");
        vm.LastSessionText.Should().Be("Last session: 2 h · Yesterday");

        Loc.Instance.Language = Loc.Arabic;
        vm.StreakText.Should().Be("4 أيام متتالية");
        vm.LastSessionText.Should().StartWith("آخر جلسة: ساعتان · ");
    }

    [AvaloniaFact]
    public void Your_week_has_seven_bars_ending_today_and_a_total()
    {
        Loc.Instance.Language = Loc.English;
        var host = FakeBpHost.WithGames(2);
        host.Week = new[] { 1.0, 0, 2, 0.5, 3, 1, 2.1 };
        var vm = Vm(host);
        vm.WeekBars.Should().HaveCount(7);
        vm.WeekBars.Last().IsToday.Should().BeTrue();
        vm.WeekBars.Last().Day.Should().Be(DateTime.Now.ToString("ddd", System.Globalization.CultureInfo.GetCultureInfo("en-US")));
        vm.WeekBars[4].Px.Should().Be(62, "the longest day fills the bar");
        vm.WeekBars[1].IsZero.Should().BeTrue();
        vm.WeekTotalText.Should().Be("9.6 h");
    }

    [AvaloniaFact]
    public void Game_page_shows_two_news_items()
    {
        var vm = Vm(FakeBpHost.WithGames(2));
        vm.SetNews(Enumerable.Range(1, 5).Select(i => ($"N{i}", "2026-11-0" + i, "https://n/" + i)));
        vm.NewsItems.Should().HaveCount(2);
    }

    [AvaloniaFact]
    public void Play_days_split_at_midnight_and_count_the_streak()
    {
        PlayDaysService.ResetForTests();
        var today = DateTime.Today;
        PlayDaysService.Record("Steam", "1", today.AddDays(-2).AddHours(20), 3600);
        PlayDaysService.Record("Steam", "1", today.AddDays(-1).AddMinutes(30), 3600);   // 23:30 → 00:30
        PlayDaysService.Record("Steam", "1", today.AddHours(10), 1800);

        var week = PlayDaysService.Week("Steam", "1", today);
        week[6].Should().BeApproximately(0.5, 0.001, "today");
        week[5].Should().BeApproximately(0.5, 0.001, "yesterday after midnight");
        week[4].Should().BeApproximately(1.5, 0.001, "the day before: 1 h plus the half hour before midnight");
        PlayDaysService.Streak("Steam", "1", today).Should().Be(3);
        PlayDaysService.LastSession("Steam", "1")!.Value.Length.Should().Be(TimeSpan.FromMinutes(30));
        PlayDaysService.Streak("Steam", "2", today).Should().Be(0);
        PlayDaysService.ResetForTests();
    }
}
