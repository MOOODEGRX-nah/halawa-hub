using System;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using System.Threading.Tasks;
using FluentAssertions;
using HalawaHub.App.Services;
using HalawaHub.Core.Plugins;
using HalawaHub.Core.Security;
using HalawaHub.Core.Updates;
using Xunit;

namespace HalawaHub.Tests;

public class UpdateChecksumRuleTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("abc", false)]
    [InlineData("zz9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a", false)]
    [InlineData("9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08", true)]
    [InlineData("9F86D081884C7D659A2FEAA0C55AD015A3BF4F1B2B0B822CD15D6C15B0F00A08", true)]
    public void OnlyARealSha256IsAccepted(string? sha, bool ok)
    {
        SelfUpdater.IsValidSha256(sha).Should().Be(ok);
    }

    [Fact]
    public async Task WindowsUpdateWithoutChecksum_IsRefusedBeforeDownloading()
    {
        if (!OperatingSystem.IsWindows()) return; // على لينكس يرفض قبل ذلك (نوع تثبيت غير مدعوم)

        string? status = null;
        var ok = await SelfUpdater.DownloadAndApplyAsync("http://127.0.0.1:1/never", s => status = s, expectedSha256: null);

        ok.Should().BeFalse();
        status.Should().Be(SelfUpdater.StatusChecksumMissing);
    }

    [Fact]
    public void ReleaseWithoutAnyChecksum_BecomesAReleasePageOnly()
    {
        var info = new UpdateInfo("9.9.9.9", "https://x/Halawa-Hub.zip", true, ReleasePageUrl: "https://x/release");
        var resolved = UpdateChecker.ApplyChecksums(info, sumsFile: null, assetName: UpdateChecker.WindowsAssetName);

        resolved.IsReleasePage.Should().BeTrue();
        resolved.DownloadUrl.Should().Be("https://x/release");
    }

    [Fact]
    public void ChecksumMissingMessage_ExistsInBothLanguages()
    {
        Loc.Lookup(Loc.Arabic, "updChecksumMissing").Should().NotBeNullOrEmpty();
        Loc.Lookup(Loc.English, "updChecksumMissing").Should().NotBeNullOrEmpty();
        UpdateService.LocalizeStatus(SelfUpdater.StatusChecksumMissing).Should().NotBe(SelfUpdater.StatusChecksumMissing);
    }
}

public class SecretStoreTests
{
    [Fact]
    public void Secret_RoundTrips_AndIsNotStoredInPlainText()
    {
        var name = "test-secret-" + Guid.NewGuid().ToString("N")[..8];
        const string value = "sgdb-key-1234567890abcdef";

        var reference = SecretStore.Save(name, value);
        try
        {
            reference.Should().NotContain(value);
            SecretStore.Load(reference).Should().Be(value);

            if (OperatingSystem.IsWindows())
            {
                reference.Should().StartWith("dpapi:");
            }
            else
            {
                reference.Should().StartWith("file:", "tests force the private-file backend");
                var mode = File.GetUnixFileMode(SecretStore.SecretsFilePath);
                mode.Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }
        finally
        {
            SecretStore.Delete(name);
        }
    }

    [Fact]
    public void EmptyValue_DeletesTheSecret()
    {
        SecretStore.Save("test-empty", "").Should().BeEmpty();
        SecretStore.Load("").Should().BeNull();
        SecretStore.Load(null).Should().BeNull();
    }

    [Fact]
    public void BackendOf_ReadsTheReferenceKind()
    {
        SecretStore.BackendOf("dpapi:AAAA").Should().Be(SecretBackend.WindowsDpapi);
        SecretStore.BackendOf("secret-service:x").Should().Be(SecretBackend.SecretService);
        SecretStore.BackendOf("file:x").Should().Be(SecretBackend.PrivateFile);
        SecretStore.BackendOf("plain").Should().BeNull();
    }
}

public class ConfigSecretMigrationTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "halawahub-cfgsecret-" + Guid.NewGuid().ToString("N"));

    public ConfigSecretMigrationTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        SecretStore.Delete(ConfigService.SteamGridDbSecretName);
        try { Directory.Delete(_dir, true); } catch { /* best effort */ }
    }

    [Fact]
    public void OldPlainKey_IsMigrated_AndRemovedFromTheFile()
    {
        var path = Path.Combine(_dir, "config.json");
        File.WriteAllText(path, "{\"SteamGridDbApiKey\":\"plain-old-key-42\",\"Language\":\"en\"}");

        var cfg = ConfigService.LoadFrom(path);
        cfg.SteamGridDbApiKey.Should().Be("plain-old-key-42");
        cfg.Language.Should().Be("en");

        var onDisk = File.ReadAllText(path);
        onDisk.Should().NotContain("plain-old-key-42");
        onDisk.Should().Contain("SteamGridDbApiKeyRef");

        ConfigService.LoadFrom(path).SteamGridDbApiKey.Should().Be("plain-old-key-42");
    }

    [Fact]
    public void ClearingTheKey_RemovesTheReference()
    {
        var path = Path.Combine(_dir, "config.json");
        var cfg = new AppConfig { SteamGridDbApiKey = "k-1" };
        ConfigService.SaveTo(path, cfg);
        File.ReadAllText(path).Should().Contain("SteamGridDbApiKeyRef").And.NotContain("k-1");

        cfg.SteamGridDbApiKey = "";
        ConfigService.SaveTo(path, cfg);
        File.ReadAllText(path).Should().NotContain("SteamGridDbApiKeyRef");
        ConfigService.LoadFrom(path).SteamGridDbApiKey.Should().BeEmpty();
    }

    [Fact]
    public void ConfigWithoutAKey_StillLoads()
    {
        var path = Path.Combine(_dir, "config.json");
        File.WriteAllText(path, "{\"Language\":\"ar\"}");
        ConfigService.LoadFrom(path).SteamGridDbApiKey.Should().BeEmpty();
    }
}

public class PluginTrustTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "halawahub-plugins-" + Guid.NewGuid().ToString("N"));
    private readonly string _plugins;
    private readonly string _trust;

    public PluginTrustTests()
    {
        _plugins = Path.Combine(_dir, "Plugins");
        Directory.CreateDirectory(_plugins);
        _trust = Path.Combine(_dir, "plugins.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* DLL may stay loaded */ }
    }

    /// إضافة المثال المبنية مع الحل — إضافة حقيقية فيها IGameTool
    private static string SamplePluginPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "HalawaHub.sln"))) dir = dir.Parent;
        dir.Should().NotBeNull();
        foreach (var cfg in new[] { "Release", "Debug" })
        {
            var p = Path.Combine(dir!.FullName, "src", "HalawaHub.Plugins.Sample", "bin", cfg, "net8.0", PluginLoader.BundledPluginFile);
            if (File.Exists(p)) return p;
        }
        throw new FileNotFoundException("build the solution first: sample plugin missing");
    }

    private string CopyAs(string name)
    {
        var target = Path.Combine(_plugins, name);
        File.Copy(SamplePluginPath(), target, overwrite: true);
        return target;
    }

    private static bool IsLoaded(string path) =>
        AssemblyLoadContext.All.SelectMany(c => c.Assemblies)
            .Any(a => !a.IsDynamic && string.Equals(a.Location, path, StringComparison.OrdinalIgnoreCase));

    [Fact]
    public void NewDll_IsNotLoadedUntilApproved()
    {
        var path = CopyAs("ThirdParty.dll");
        var loader = new PluginLoader(_plugins, _trust, "1.0.0.0");
        loader.LoadPlugins();

        var entry = loader.Plugins.Single();
        entry.State.Should().Be(PluginState.NeedsApproval);
        entry.Sha256.Should().HaveLength(64);
        loader.GameTools.Should().BeEmpty();
        IsLoaded(path).Should().BeFalse("nothing from an unapproved DLL may run");

        loader.Enable(entry).Should().BeTrue();
        loader.GameTools.Should().NotBeEmpty();

        // المرة القادمة: موافق عليها
        var next = new PluginLoader(_plugins, _trust, "1.0.0.0");
        next.LoadPlugins();
        next.Plugins.Single().State.Should().Be(PluginState.Enabled);
    }

    [Fact]
    public void ChangedDll_AsksAgain()
    {
        var path = CopyAs("ThirdParty.dll");
        var loader = new PluginLoader(_plugins, _trust, "1.0.0.0");
        loader.LoadPlugins();
        // موافقة سابقة على هذه البصمة (بلا تحميل، حتى لا يُقفل الملف على ويندوز)
        loader.Disable(loader.Plugins.Single());

        File.AppendAllText(path, "tampered");

        var next = new PluginLoader(_plugins, _trust, "1.0.0.0");
        next.LoadPlugins();
        var entry = next.Plugins.Single();
        entry.State.Should().Be(PluginState.NeedsApproval);
        entry.Changed.Should().BeTrue();
        next.GameTools.Should().BeEmpty();
    }

    [Fact]
    public void DeniedDll_StaysOff_AndIsNotAskedAgain()
    {
        CopyAs("ThirdParty.dll");
        var loader = new PluginLoader(_plugins, _trust, "1.0.0.0");
        loader.LoadPlugins();
        loader.Disable(loader.Plugins.Single());

        var next = new PluginLoader(_plugins, _trust, "1.0.0.0");
        next.LoadPlugins();
        next.Plugins.Single().State.Should().Be(PluginState.Disabled);
        next.PendingApproval.Should().BeEmpty();
    }

    [Fact]
    public void FileChangedAfterThePrompt_IsNotLoaded()
    {
        var path = CopyAs("ThirdParty.dll");
        var loader = new PluginLoader(_plugins, _trust, "1.0.0.0");
        loader.LoadPlugins();
        var entry = loader.Plugins.Single();

        File.AppendAllText(path, "swapped");
        loader.Enable(entry).Should().BeFalse();
        loader.GameTools.Should().BeEmpty();
    }

    [Fact]
    public void BundledSample_FromTheSameBuild_IsTrustedAutomatically()
    {
        CopyAs(PluginLoader.BundledPluginFile);
        var sampleVersion = System.Reflection.AssemblyName.GetAssemblyName(SamplePluginPath()).Version!.ToString(4);

        var loader = new PluginLoader(_plugins, _trust, sampleVersion);
        loader.LoadPlugins();
        var entry = loader.Plugins.Single();
        entry.Bundled.Should().BeTrue();
        entry.State.Should().Be(PluginState.Enabled);

        // من بناء آخر (إصدار مختلف): يُسأل عنه كأي إضافة
        var other = new PluginLoader(Path.Combine(_dir, "Plugins"), Path.Combine(_dir, "other-trust.json"), "0.0.0.1");
        other.LoadPlugins();
        other.Plugins.Single().State.Should().Be(PluginState.NeedsApproval);
    }
}
