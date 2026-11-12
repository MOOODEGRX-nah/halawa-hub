using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FluentAssertions;
using HalawaHub.App.Services;
using HalawaHub.App.ViewModels;
using HalawaHub.App.Views;
using Xunit;

namespace HalawaHub.Tests.Ui;

public class ConfirmDialogTests
{
    [AvaloniaTheory]
    [InlineData(Loc.Arabic)]
    [InlineData(Loc.English)]
    public void Requests_AreShownOneAfterAnother_AndEscPicksTheLastChoice(string language)
    {
        using var ui = new UiHarness(language: language);
        var picked = "";

        ConfirmRequest Request(string title) => new()
        {
            Title = title,
            Message = "m",
            Details = new[] { @"C:\Plugins\x.dll" },
            Choices = new[]
            {
                new ConfirmChoice { Label = "yes", IsPrimary = true, Run = () => picked += title + ":yes;" },
                new ConfirmChoice { Label = "no", Run = () => picked += title + ":no;" }
            }
        };

        ui.Vm.RequestConfirm(Request("A"));
        ui.Vm.RequestConfirm(Request("B"));
        ui.Settle();

        ui.All<ConfirmDialog>().Should().ContainSingle();
        ui.Vm.Confirm!.Title.Should().Be("A");

        ui.Click("yes");
        ui.Vm.Confirm!.Title.Should().Be("B");

        ui.Window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        ui.Settle();

        ui.Vm.IsConfirmOpen.Should().BeFalse();
        picked.Should().Be("A:yes;B:no;");
    }
}
