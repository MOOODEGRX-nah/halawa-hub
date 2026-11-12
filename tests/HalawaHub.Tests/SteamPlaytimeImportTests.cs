using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using HalawaHub.App.Services;
using HalawaHub.Core.Library;
using Xunit;

namespace HalawaHub.Tests;

/// الصيغ منسوخة من ملفات Steam حقيقية (الأسماء والأرقام مستبدلة)
public class SteamPlaytimeImportTests
{
    private const string LoginUsers =
        "\"users\"\n{\n\t\"76561198000000001\"\n\t{\n\t\t\"AccountName\"\t\t\"first\"\n\t\t\"PersonaName\"\t\t\"First \\\"One\\\"\"\n\t\t\"AutoLogin\"\t\t\"1\"\n\t\t\"timestamp\"\t\t\"1794297825\"\n\t}\n" +
        "\t\"76561198000000002\"\n\t{\n\t\t\"AccountName\"\t\t\"second\"\n\t\t\"PersonaName\"\t\t\"Second\"\n\t\t\"timestamp\"\t\t\"1700000000\"\n\t}\n}\n";

    private const string LocalConfig =
        "\"UserLocalConfigStore\"\n{\n\t\"Software\"\n\t{\n\t\t\"Valve\"\n\t\t{\n\t\t\t\"Steam\"\n\t\t\t{\n\t\t\t\t\"Apps\"\n\t\t\t\t{\n" +
        "\t\t\t\t\t\"480\"\n\t\t\t\t\t{\n\t\t\t\t\t\t\"LastPlayed\"\t\t\"1741391241\"\n\t\t\t\t\t\t\"Playtime\"\t\t\"2661\"\n\t\t\t\t\t\t\"cloud\"\n\t\t\t\t\t\t{\n\t\t\t\t\t\t\t\"last_sync_state\"\t\t\"synchronized\"\n\t\t\t\t\t\t}\n\t\t\t\t\t}\n" +
        "\t\t\t\t\t\"570\"\n\t\t\t\t\t{\n\t\t\t\t\t\t\"LastPlayed\"\t\t\"0\"\n\t\t\t\t\t}\n" +
        "\t\t\t\t}\n\t\t\t}\n\t\t}\n\t}\n}\n";

    [Fact]
    public void Accounts_AreRead_WithEscapedNames()
    {
        var accounts = SteamPlaytimeImport.ParseAccounts(LoginUsers);
        accounts.Should().HaveCount(2);
        accounts[0].PersonaName.Should().Be("First \"One\"");
        accounts[0].AccountId.Should().Be((76561198000000001UL - SteamPlaytimeImport.SteamId64Base).ToString());
    }

    [Fact]
    public void WithoutMostRecent_TheLatestLoginIsPicked_UnlessTheUserChose()
    {
        var accounts = SteamPlaytimeImport.ParseAccounts(LoginUsers);
        SteamPlaytimeImport.PickAccount(accounts, null)!.SteamId64.Should().Be("76561198000000001");
        SteamPlaytimeImport.PickAccount(accounts, "76561198000000002")!.PersonaName.Should().Be("Second");
        SteamPlaytimeImport.PickAccount(accounts, "gone")!.SteamId64.Should().Be("76561198000000001");

        var withRecent = new List<SteamAccount> { new("1", "a", false, 999), new("2", "b", true, 1) };
        SteamPlaytimeImport.PickAccount(withRecent, "")!.SteamId64.Should().Be("2");
        SteamPlaytimeImport.PickAccount(new List<SteamAccount>(), null).Should().BeNull();
    }

    [Fact]
    public void Playtime_IsMinutes_AndGamesWithoutItAreSkipped()
    {
        var times = SteamPlaytimeImport.ParsePlaytimes(LocalConfig);
        times.Keys.Should().Equal("480");
        times["480"].Seconds.Should().Be(2661 * 60);
        times["480"].LastPlayedUtc.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1741391241).UtcDateTime);
    }

    [Fact]
    public void BrokenFiles_GiveNothing_InsteadOfThrowing()
    {
        SteamPlaytimeImport.ParsePlaytimes("\"UserLocalConfigStore\" { \"Software\" {").Should().BeEmpty();
        SteamPlaytimeImport.ParsePlaytimes("").Should().BeEmpty();
        SteamPlaytimeImport.ParseAccounts("garbage").Should().BeEmpty();
    }

    [Fact]
    public void TheLargerOfHalawaAndSteam_IsShown_AndSteamTimeIsNotWrittenToOurFile()
    {
        var id = "imp" + Guid.NewGuid().ToString("N")[..6];
        try
        {
            PlayTimeService.AddSeconds("Steam", id, 600);
            PlayTimeService.SetExternal(new Dictionary<string, long> { ["Steam|" + id] = 7200 });
            PlayTimeService.GetTotal("Steam", id).Should().Be(TimeSpan.FromSeconds(7200));

            PlayTimeService.SetExternal(new Dictionary<string, long> { ["Steam|" + id] = 60 });
            PlayTimeService.GetTotal("Steam", id).Should().Be(TimeSpan.FromSeconds(600));
        }
        finally
        {
            PlayTimeService.SetExternal(new Dictionary<string, long>());
        }
        PlayTimeService.GetTotal("Steam", id).Should().Be(TimeSpan.FromSeconds(600));
    }
}
