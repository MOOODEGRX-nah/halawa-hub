using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using HalawaHub.App;
using HalawaHub.Core.Updates;
using Xunit;

namespace HalawaHub.Tests;

/// <summary>اختيار ملف التحديث لكل نظام وصيغة، بردود GitHub مزيفة.</summary>
public class UpdateAssetSelectionTests
{
    private const string ReleasePage = "https://github.com/o/r/releases/tag/v9.0.0.0";

    private static string Release(params (string Name, string? Digest)[] assets)
    {
        var items = string.Join(",", assets.Select(a =>
            $$"""{"name":"{{a.Name}}","browser_download_url":"https://x/{{a.Name}}"{{(a.Digest == null ? "" : $",\"digest\":\"sha256:{a.Digest}\"")}}}"""));
        return $$"""{"tag_name":"v9.0.0.0","html_url":"{{ReleasePage}}","assets":[{{items}}]}""";
    }

    private static readonly string FullRelease = Release(
        ("SHA256SUMS.txt", "s"),
        (InstallInfo.AppImageAssetName, "a"),
        (InstallInfo.TarballAssetName, "t"),
        (UpdateChecker.WindowsAssetName, "w"));

    [Theory]
    [InlineData(InstallKind.Windows, "Halawa-Hub.zip", "w")]
    [InlineData(InstallKind.LinuxTarball, "Halawa-Hub-linux-x64.tar.gz", "t")]
    [InlineData(InstallKind.LinuxAppImage, "Halawa-Hub-x86_64.AppImage", "a")]
    public void EachInstallKind_GetsItsOwnAsset_AndItsOwnDigest(InstallKind kind, string asset, string digest)
    {
        var info = UpdateChecker.ParseRelease(FullRelease, "1.0.0.0", InstallInfo.AssetNameFor(kind));

        info!.DownloadUrl.Should().Be("https://x/" + asset);
        info.Sha256.Should().Be(digest);
        info.IsReleasePage.Should().BeFalse();
    }

    [Fact]
    public void NotifyOnly_AlwaysOpensTheReleasePage_EvenWithAssetsAvailable()
    {
        var info = UpdateChecker.ParseRelease(FullRelease, "1.0.0.0", InstallInfo.AssetNameFor(InstallKind.NotifyOnly));

        info!.IsReleasePage.Should().BeTrue();
        info.DownloadUrl.Should().Be(ReleasePage);
        info.IsNewer.Should().BeTrue("the user is still told about the new version");
    }

    [Theory]
    [InlineData(InstallKind.LinuxTarball)]
    [InlineData(InstallKind.LinuxAppImage)]
    public void LinuxAssetMissing_FallsBackToNotifyOnly(InstallKind kind)
    {
        // إصدار نُشر لويندوز فقط، مثل كل الإصدارات قبل v0.0.10.63
        var json = Release(("SHA256SUMS.txt", "s"), (UpdateChecker.WindowsAssetName, "w"));

        var info = UpdateChecker.ParseRelease(json, "1.0.0.0", InstallInfo.AssetNameFor(kind));

        info!.IsReleasePage.Should().BeTrue();
        info.DownloadUrl.Should().Be(ReleasePage);
        info.Sha256.Should().BeNull();
    }

    [Fact]
    public void Windows_IsNotConfusedByLinuxAssets()
    {
        var json = Release((InstallInfo.TarballAssetName, "t"), (InstallInfo.AppImageAssetName, "a"));

        UpdateChecker.ParseRelease(json, "1.0.0.0", UpdateChecker.WindowsAssetName)!
            .IsReleasePage.Should().BeTrue("Windows must never download a Linux file");
    }

    [Fact]
    public void ChecksumsUrl_IsRemembered()
    {
        UpdateChecker.ParseRelease(FullRelease, "1.0.0.0", InstallInfo.TarballAssetName)!
            .ChecksumsUrl.Should().Be("https://x/SHA256SUMS.txt");
    }

    [Theory]
    [InlineData("abc  Halawa-Hub-linux-x64.tar.gz\n", "abc")]
    [InlineData("zzz  Halawa-Hub.zip\nabc *Halawa-Hub-linux-x64.tar.gz\r\n", "abc")]
    [InlineData("zzz  Halawa-Hub.zip\n", null)]
    [InlineData("garbage\n\n", null)]
    [InlineData(null, null)]
    public void FindChecksum_ReadsSha256SumFormat(string? file, string? expected)
    {
        UpdateChecker.FindChecksum(file, InstallInfo.TarballAssetName).Should().Be(expected);
    }

    [Fact]
    public void DigestMissing_UsesTheCombinedChecksumsFile()
    {
        var json = Release(("SHA256SUMS.txt", null), (InstallInfo.TarballAssetName, null));
        var info = UpdateChecker.ParseRelease(json, "1.0.0.0", InstallInfo.TarballAssetName)!;

        var resolved = UpdateChecker.ApplyChecksums(info, "abc  Halawa-Hub-linux-x64.tar.gz\n", InstallInfo.TarballAssetName);

        resolved.Sha256.Should().Be("abc");
        resolved.IsReleasePage.Should().BeFalse();
        resolved.DownloadUrl.Should().Be("https://x/" + InstallInfo.TarballAssetName);
    }

    [Fact]
    public void NoChecksumAtAll_FallsBackToTheReleasePage()
    {
        var json = Release((InstallInfo.TarballAssetName, null));
        var info = UpdateChecker.ParseRelease(json, "1.0.0.0", InstallInfo.TarballAssetName)!;

        var resolved = UpdateChecker.ApplyChecksums(info, null, InstallInfo.TarballAssetName);

        resolved.IsReleasePage.Should().BeTrue("a file we cannot verify must not replace a working install");
        resolved.DownloadUrl.Should().Be(ReleasePage);
    }
}

public class LinuxCommandLineTests
{
    [Theory]
    [InlineData(new[] { "--wait-for-pid", "42" }, true, 42)]
    [InlineData(new[] { "x", "--wait-for-pid", "7", "y" }, true, 7)]
    [InlineData(new[] { "--wait-for-pid" }, false, 0)]
    [InlineData(new[] { "--wait-for-pid", "abc" }, false, 0)]
    [InlineData(new string[0], false, 0)]
    public void WaitForPid_IsParsedSafely(string[] args, bool found, int pid)
    {
        Program.TryReadWaitForPid(args, out var value).Should().Be(found);
        value.Should().Be(pid);
    }
}

public sealed class InstallKindDetectionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "InstallKind-" + Guid.NewGuid().ToString("N"));
    private readonly string _install;

    public InstallKindDetectionTests()
    {
        _install = Path.Combine(_root, "halawa-hub");
        Directory.CreateDirectory(_install);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void AddMarker() => File.WriteAllText(Path.Combine(_install, InstallInfo.TarballMarkerFile), "");

    [Fact]
    public void Tarball_WithMarkerAndWritableFolders_SelfUpdates()
    {
        AddMarker();

        InstallInfo.DetectLinux(null, _install + "/", _ => true).Should().Be(InstallKind.LinuxTarball);
    }

    [Fact]
    public void WithoutMarker_NotifyOnly_EvenIfWritable()
    {
        // بناء مطور أو حزمة AUR: لا يجوز أن يستبدل نفسه
        InstallInfo.DetectLinux(null, _install, _ => true).Should().Be(InstallKind.NotifyOnly);
    }

    [Fact]
    public void ReadOnlyInstallFolder_NotifyOnly()
    {
        AddMarker();

        InstallInfo.DetectLinux(null, _install, _ => false).Should().Be(InstallKind.NotifyOnly);
    }

    [Fact]
    public void ReadOnlyParentFolder_NotifyOnly_BecauseTheFolderIsRenamed()
    {
        AddMarker();

        InstallInfo.DetectLinux(null, _install, dir => dir == _install).Should().Be(InstallKind.NotifyOnly);
    }

    [Fact]
    public void AppImage_WritableFile_SelfUpdates_ElseNotifyOnly()
    {
        var appImage = Path.Combine(_root, "Halawa-Hub.AppImage");
        File.WriteAllText(appImage, "");

        InstallInfo.DetectLinux(appImage, "/tmp/.mount_x/usr/lib/halawa-hub/", _ => true).Should().Be(InstallKind.LinuxAppImage);
        InstallInfo.DetectLinux(appImage, "/tmp/.mount_x/usr/lib/halawa-hub/", _ => false).Should().Be(InstallKind.NotifyOnly);
        InstallInfo.DetectLinux(appImage + ".gone", "/tmp/.mount_x/usr/lib/halawa-hub/", _ => true).Should().Be(InstallKind.NotifyOnly);
    }

    [Fact]
    public void AppImageVariablesFromAnotherApp_AreNotOurs()
    {
        // تطبيق AppImage آخر شغّلنا: لو صدّقنا متغيراته لاستبدل التحديث ملفه هو
        InstallInfo.ReadOwnAppImagePath("/apps/other.AppImage", "/tmp/.mount_other", "/home/u/.local/opt/halawa-hub/")
            .Should().BeNull();

        InstallInfo.ReadOwnAppImagePath("/apps/hub.AppImage", "/tmp/.mount_hub", "/tmp/.mount_hub/usr/lib/halawa-hub/")
            .Should().Be("/apps/hub.AppImage");

        InstallInfo.ReadOwnAppImagePath("/apps/hub.AppImage", "/tmp/.mount_h", "/tmp/.mount_hub/usr/lib/halawa-hub/")
            .Should().BeNull("a shared name prefix is not the same mount");

        InstallInfo.ReadOwnAppImagePath(null, null, "/x/").Should().BeNull();
    }

    [Fact]
    public void CanWriteToDirectory_ChecksForReal_AndLeavesNothingBehind()
    {
        InstallInfo.CanWriteToDirectory(_install).Should().BeTrue();
        InstallInfo.CanWriteToDirectory(Path.Combine(_root, "absent")).Should().BeFalse();
        Directory.GetFileSystemEntries(_install).Should().BeEmpty();
    }
}

public sealed class LinuxSelfUpdaterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "LinuxUpdater-" + Guid.NewGuid().ToString("N"));

    public LinuxSelfUpdaterTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string BuildArchive(params (string Path, string Content)[] files)
    {
        var archive = Path.Combine(_root, $"archive-{Guid.NewGuid():N}.tar.gz");
        using var output = File.Create(archive);
        using var gzip = new GZipStream(output, CompressionMode.Compress);
        using var tar = new TarWriter(gzip);
        foreach (var (path, content) in files)
        {
            var entry = new PaxTarEntry(TarEntryType.RegularFile, path)
            {
                DataStream = new MemoryStream(Encoding.UTF8.GetBytes(content))
            };
            tar.WriteEntry(entry);
        }
        return archive;
    }

    private string MakeDir(string name, params string[] files)
    {
        var dir = Path.Combine(_root, name);
        Directory.CreateDirectory(dir);
        foreach (var file in files)
        {
            var path = Path.Combine(dir, file);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, name);
        }
        return dir;
    }

    [Fact]
    public void Extract_FindsTheExecutableInTheTopLevelFolder_AndMakesItExecutable()
    {
        if (OperatingSystem.IsWindows()) return;

        var archive = BuildArchive(("Halawa-Hub/Halawa-Hub", "new"), ("Halawa-Hub/libSkiaSharp.so", "lib"));

        var root = LinuxSelfUpdater.ExtractTarball(archive, Path.Combine(_root, "out"));

        root.Should().Be(Path.Combine(_root, "out", "Halawa-Hub"));
        var mode = File.GetUnixFileMode(Path.Combine(root, "Halawa-Hub"));
        mode.Should().HaveFlag(UnixFileMode.UserExecute);
        File.Exists(Path.Combine(root, "libSkiaSharp.so")).Should().BeTrue();
    }

    [Fact]
    public void Extract_ArchiveWithoutTheExecutable_IsRejected()
    {
        var archive = BuildArchive(("Halawa-Hub/readme.txt", "x"));

        var act = () => LinuxSelfUpdater.ExtractTarball(archive, Path.Combine(_root, "out"));

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Extract_PathTraversalEntry_IsRejected()
    {
        var archive = BuildArchive(("../escaped.txt", "evil"), ("Halawa-Hub/Halawa-Hub", "x"));

        var act = () => LinuxSelfUpdater.ExtractTarball(archive, Path.Combine(_root, "out"));

        act.Should().Throw<IOException>();
        File.Exists(Path.Combine(_root, "escaped.txt")).Should().BeFalse();
    }

    [Fact]
    public void Swap_ReplacesTheFolder_AndKeepsTheOldOneAsBackup()
    {
        var install = MakeDir("install", "Halawa-Hub");
        var fresh = MakeDir("fresh", "Halawa-Hub");
        var backup = Path.Combine(_root, "backup");

        LinuxSelfUpdater.SwapDirectories(install, fresh, backup);

        File.ReadAllText(Path.Combine(install, "Halawa-Hub")).Should().Be("fresh");
        File.ReadAllText(Path.Combine(backup, "Halawa-Hub")).Should().Be("install");
    }

    [Fact]
    public void Swap_WhenTheNewFolderIsGone_RestoresTheOldOne()
    {
        var install = MakeDir("install", "Halawa-Hub");
        var backup = Path.Combine(_root, "backup");

        var act = () => LinuxSelfUpdater.SwapDirectories(install, Path.Combine(_root, "absent"), backup);

        act.Should().Throw<IOException>();
        File.ReadAllText(Path.Combine(install, "Halawa-Hub")).Should().Be("install", "the app must never be left without its folder");
    }

    [Fact]
    public void UserPlugins_SurviveTheUpdate_ButShippedPluginsAreNotOverwritten()
    {
        var install = MakeDir("install", "Plugins/Mine.dll", "Plugins/HalawaHub.Plugins.Sample.dll");
        var fresh = MakeDir("fresh", "Plugins/HalawaHub.Plugins.Sample.dll");

        LinuxSelfUpdater.KeepUserPlugins(install, fresh);

        File.ReadAllText(Path.Combine(fresh, "Plugins", "Mine.dll")).Should().Be("install");
        File.ReadAllText(Path.Combine(fresh, "Plugins", "HalawaHub.Plugins.Sample.dll")).Should().Be("fresh");
    }

    [Fact]
    public void CleanLeftovers_RemovesOnlyOurOwnSiblings()
    {
        var install = MakeDir("halawa-hub", "Halawa-Hub");
        MakeDir("halawa-hub.old-123", "x");
        MakeDir("halawa-hub.update-456", "x");
        var unrelated = MakeDir("halawa-hub-saves", "x");

        LinuxSelfUpdater.CleanLeftovers(install);

        Directory.Exists(Path.Combine(_root, "halawa-hub.old-123")).Should().BeFalse();
        Directory.Exists(Path.Combine(_root, "halawa-hub.update-456")).Should().BeFalse();
        Directory.Exists(install).Should().BeTrue();
        Directory.Exists(unrelated).Should().BeTrue();
    }

    [Fact]
    public void AppImage_BrokenDownload_NeverReplacesTheWorkingFile()
    {
        if (OperatingSystem.IsWindows()) return;

        var target = Path.Combine(_root, "Hub.AppImage");
        var staged = target + ".update-1";
        File.WriteAllText(target, "working");
        File.WriteAllText(staged, "broken");

        var act = () => LinuxSelfUpdater.ReplaceAppImage(staged, target, _ => throw new InvalidDataException("bad build"));

        act.Should().Throw<InvalidDataException>();
        File.ReadAllText(target).Should().Be("working");
    }

    [Fact]
    public void AppImage_GoodDownload_ReplacesTheFile_AndStaysExecutable()
    {
        if (OperatingSystem.IsWindows()) return;

        var target = Path.Combine(_root, "Hub.AppImage");
        var staged = target + ".update-1";
        File.WriteAllText(target, "old");
        File.WriteAllText(staged, "new");

        LinuxSelfUpdater.ReplaceAppImage(staged, target, _ => { });

        File.ReadAllText(target).Should().Be("new");
        File.GetUnixFileMode(target).Should().HaveFlag(UnixFileMode.UserExecute);
        File.Exists(staged).Should().BeFalse();
    }

    [Theory]
    [InlineData(0, "1.2.3.5\n", true)]
    [InlineData(0, "1.2.3.4\n", false)] // نفس النسخة: حلقة تحديث لا تنتهي (E09)
    [InlineData(0, "1.0.0.0", false)]
    [InlineData(1, "1.2.3.5", false)]
    [InlineData(0, "not a version", false)]
    [InlineData(0, "", false)]
    public void NewBuild_MustRun_AndBeNewer(int exitCode, string output, bool expected)
    {
        LinuxSelfUpdater.IsNewerBuild(exitCode, output, new Version(1, 2, 3, 4)).Should().Be(expected);
    }

    [Fact]
    public async Task Download_VerifiesTheHash_AndDeletesAMismatch()
    {
        var payload = Encoding.UTF8.GetBytes("update payload");
        var hash = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();

        using var server = new OneFileServer(payload);
        var good = Path.Combine(_root, "good");
        var bad = Path.Combine(_root, "bad");

        (await LinuxSelfUpdater.DownloadVerifiedAsync(server.Url, good, hash)).Should().BeTrue();
        File.ReadAllBytes(good).Should().Equal(payload);

        (await LinuxSelfUpdater.DownloadVerifiedAsync(server.Url, bad, new string('0', 64))).Should().BeFalse();
        File.Exists(bad).Should().BeFalse("a file that failed verification must not stay around");
    }

    [Fact]
    public async Task Apply_WithoutAChecksum_RefusesBeforeDownloading()
    {
        string? status = null;

        var ok = await LinuxSelfUpdater.ApplyAsync(InstallKind.LinuxTarball, "http://127.0.0.1:1/never", null, s => status = s);

        ok.Should().BeFalse();
        status.Should().Be(SelfUpdater.StatusChecksumFailed);
    }

    private sealed class OneFileServer : IDisposable
    {
        private readonly TcpServerShim _shim;

        public OneFileServer(byte[] payload) => _shim = new TcpServerShim(payload);

        public string Url => _shim.Url;

        public void Dispose() => _shim.Dispose();
    }

    /// خادم HTTP صغير على منفذ عشوائي، بدون صلاحيات HttpListener الخاصة بويندوز
    private sealed class TcpServerShim : IDisposable
    {
        private readonly System.Net.Sockets.TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cts = new();

        public TcpServerShim(byte[] payload)
        {
            _listener.Start();
            _ = Task.Run(async () =>
            {
                try
                {
                    while (!_cts.IsCancellationRequested)
                    {
                        using var client = await _listener.AcceptTcpClientAsync(_cts.Token);
                        await using var stream = client.GetStream();
                        var buffer = new byte[4096];
                        _ = await stream.ReadAsync(buffer, _cts.Token);
                        var header = Encoding.ASCII.GetBytes(
                            $"HTTP/1.1 200 OK\r\nContent-Length: {payload.Length}\r\nConnection: close\r\n\r\n");
                        await stream.WriteAsync(header, _cts.Token);
                        await stream.WriteAsync(payload, _cts.Token);
                    }
                }
                catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or IOException or System.Net.Sockets.SocketException)
                {
                    // إغلاق الخادم عند نهاية الاختبار
                }
            });
        }

        public string Url => $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/file";

        public void Dispose()
        {
            _cts.Cancel();
            _listener.Stop();
        }
    }
}
