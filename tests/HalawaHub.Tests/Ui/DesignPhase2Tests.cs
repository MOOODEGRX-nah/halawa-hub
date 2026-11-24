using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FluentAssertions;
using HalawaHub.App.Controls;
using HalawaHub.App.Services;
using HalawaHub.App.ViewModels;
using HalawaHub.App.Views;
using Xunit;

namespace HalawaHub.Tests.Ui;

/// <summary>تحديث التصميم، المرحلة 2: الشريط العائم وشعار M وبطاقة الإشعارات تحت الجرس.</summary>
public class DesignPhase2Tests
{
    [AvaloniaFact]
    public void TopBar_IsAFloatingCapsule_WithTheVectorMark()
    {
        using var ui = new UiHarness(Loc.English);
        var bar = ui.All<TopBar>().Single();
        var capsule = bar.FindControl<Border>("TopCapsule")!;
        capsule.CornerRadius.TopLeft.Should().Be(22);
        capsule.Margin.Top.Should().Be(12);
        bar.GetVisualDescendants().OfType<HalawaMark>().Should().ContainSingle();
        bar.GetVisualDescendants().OfType<Image>().Should().BeEmpty("the square PNG logo is gone");
    }

    [AvaloniaFact]
    public void SettingsHeader_UsesTheSameMark()
    {
        using var ui = new UiHarness(Loc.English);
        ui.Vm.OpenSettingsCommand.Execute(null);
        ui.Settle();
        ui.All<SettingsDialog>().Single().GetVisualDescendants().OfType<HalawaMark>().Should().ContainSingle();
    }

    [AvaloniaFact]
    public void UpdateNotification_HasTitleBodyAndUpdateInLauncherAction()
    {
        using var ui = new UiHarness(Loc.English);
        ui.Vm.ClearNotificationsCommand.Execute(null);
        var elden = ui.Vm.Games.First(g => g.Name == "Elden Ring");
        ui.Vm.Notify("update", "x", elden);

        var n = ui.Vm.Notifications[0];
        n.Kind.Should().Be("upd");
        n.Title.Should().Be("Elden Ring: update available");
        n.Body.Should().Be("Steam downloads it before the game starts.");
        n.ActionLabel.Should().Be("Update in Steam");
        n.RelativeTimeText.Should().Be("Now");
        n.IsUnread.Should().BeTrue();
        ui.Vm.NotificationCount.Should().Be(1);
    }

    [AvaloniaFact]
    public void AppUpdateNotification_OpensTheUpdatesSection()
    {
        using var ui = new UiHarness(Loc.English);
        ui.Vm.ClearNotificationsCommand.Execute(null);
        ui.Vm.NotifyKey("appupdate", "ntAppUpdate");
        var n = ui.Vm.Notifications[0];
        n.Kind.Should().Be("app");
        n.Title.Should().Be("A new version of Halawa Hub");
        n.Body.Should().Be("Install it from Settings › Updates.");
        n.ActionLabel.Should().Be("Open Updates");

        ui.Vm.ToggleNotificationsCommand.Execute(null);
        ui.Vm.RunNotificationActionCommand.Execute(n);
        ui.Vm.IsNotificationsOpen.Should().BeFalse();
        ui.Vm.IsSettingsVisible.Should().BeTrue();
        ui.Vm.SettingsSection.Should().Be(MainViewModel.SectionUpdates);
    }

    [AvaloniaFact]
    public void FailedLaunch_OffersTryAgain_AndScanOffersShow()
    {
        using var ui = new UiHarness(Loc.Arabic);
        ui.Vm.ClearNotificationsCommand.Execute(null);
        var valorant = ui.Vm.Games.First(g => g.Name == "Valorant");
        ui.Vm.Notify("launch", "Riot Client لا يعمل.", valorant);
        ui.Vm.NotifyKey("scan", "ntScan", 6, 1);

        var fail = ui.Vm.Notifications.Single(x => x.Kind == "fail");
        fail.Title.Should().Be("تعذّر تشغيل Valorant");
        fail.Body.Should().Be("Riot Client لا يعمل.");
        fail.ActionLabel.Should().Be("حاول مجددًا");
        ui.Vm.Notifications.Single(x => x.Kind == "scan").ActionLabel.Should().Be("عرض");
    }

    [AvaloniaFact]
    public void UnreadDots_StayWhileOpen_AndClearOnClose()
    {
        using var ui = new UiHarness(Loc.English);
        ui.Vm.ClearNotificationsCommand.Execute(null);
        ui.Vm.NotifyKey("scan", "ntScan", 6, 1);
        ui.Vm.HasUnreadNotifications.Should().BeTrue();

        ui.Vm.ToggleNotificationsCommand.Execute(null);
        ui.Vm.HasUnreadNotifications.Should().BeFalse("the bell dot goes once the card is open");
        ui.Vm.Notifications[0].IsUnread.Should().BeTrue("the item dot shows what is new while the card is open");

        ui.Vm.ToggleNotificationsCommand.Execute(null);
        ui.Vm.Notifications[0].IsUnread.Should().BeFalse();
    }

    [AvaloniaFact]
    public void NotificationCard_HangsUnderTheBell_NotInTheMiddle()
    {
        using var ui = new UiHarness(Loc.English);
        ui.Vm.ClearNotificationsCommand.Execute(null);
        ui.Vm.NotifyKey("scan", "ntScan", 6, 1);
        ui.Vm.ToggleNotificationsCommand.Execute(null);
        ui.Settle();

        var dialog = ui.All<NotificationsDialog>().Single();
        var card = dialog.FindControl<Border>("Card")!;
        var bell = ui.All<Button>().First(b => b.Name == "BellButton");
        var bellBottom = bell.TranslatePoint(new Avalonia.Point(0, bell.Bounds.Height), dialog)!.Value.Y;

        card.VerticalAlignment.Should().Be(Avalonia.Layout.VerticalAlignment.Top);
        card.Bounds.Top.Should().BeApproximately(bellBottom + 10, 1);
        card.Bounds.Right.Should().BeApproximately(dialog.Bounds.Width - 28, 1, "it sits at the page edge, not centred");
    }
}
