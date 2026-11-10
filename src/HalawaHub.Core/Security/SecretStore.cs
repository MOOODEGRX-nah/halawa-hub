using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace HalawaHub.Core.Security;

public enum SecretBackend
{
    /// ويندوز: مشفّر بـ DPAPI لحساب المستخدم الحالي، ويُحفظ المشفَّر في config.json
    WindowsDpapi,

    /// لينكس: مخزن مفاتيح النظام (KWallet أو GNOME Keyring) عبر Secret Service
    SecretService,

    /// لينكس بلا مخزن مفاتيح: ملف secrets.json يقرؤه المستخدم وحده (0600)
    PrivateFile
}

/// <summary>
/// حفظ الأسرار (مفتاح SteamGridDB وأي مفتاح لاحق) خارج النص الصريح في config.json.
/// القيمة المحفوظة في الإعدادات "مرجع":
///   dpapi:&lt;base64&gt;     النص المشفّر نفسه (ويندوز)
///   secret-service:&lt;name&gt; القيمة في مخزن مفاتيح النظام
///   file:&lt;name&gt;           القيمة في secrets.json بصلاحية 0600
/// متغير البيئة HALAWAHUB_SECRET_BACKEND=file يفرض الملف (للاختبارات، حتى لا تكتب في
/// مخزن المفاتيح الحقيقي للمطوّر).
/// </summary>
public static class SecretStore
{
    public const string BackendOverrideVariable = "HALAWAHUB_SECRET_BACKEND";
    private const string SecretToolApp = "halawa-hub";
    private static readonly TimeSpan ToolTimeout = TimeSpan.FromSeconds(15);
    private static readonly object FileLock = new();

    internal static string SecretsFilePath => AppPaths.InData("secrets.json");

    /// المخزن الذي سيُستخدم لحفظ جديد على هذا الجهاز
    public static SecretBackend PreferredBackend
    {
        get
        {
            if (OperatingSystem.IsWindows()) return SecretBackend.WindowsDpapi;
            if (Environment.GetEnvironmentVariable(BackendOverrideVariable) == "file") return SecretBackend.PrivateFile;
            return SecretToolAvailable() ? SecretBackend.SecretService : SecretBackend.PrivateFile;
        }
    }

    /// المخزن الذي يشير إليه مرجع محفوظ (لعرضه في الإعدادات)
    public static SecretBackend? BackendOf(string? reference)
    {
        if (string.IsNullOrEmpty(reference)) return null;
        if (reference.StartsWith("dpapi:", StringComparison.Ordinal)) return SecretBackend.WindowsDpapi;
        if (reference.StartsWith("secret-service:", StringComparison.Ordinal)) return SecretBackend.SecretService;
        if (reference.StartsWith("file:", StringComparison.Ordinal)) return SecretBackend.PrivateFile;
        return null;
    }

    /// <summary>
    /// يحفظ السر ويعيد المرجع الذي يُكتب في الإعدادات. قيمة فارغة تحذف السر وتعيد "".
    /// إن فشل مخزن المفاتيح على لينكس نرجع للملف الخاص بدل أن نضيّع المفتاح.
    /// </summary>
    public static string Save(string name, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            Delete(name);
            return "";
        }

        switch (PreferredBackend)
        {
            case SecretBackend.WindowsDpapi:
                return "dpapi:" + Convert.ToBase64String(Dpapi.Protect(Encoding.UTF8.GetBytes(value)));

            case SecretBackend.SecretService:
                if (SecretToolStore(name, value))
                {
                    RemoveFromFile(name);
                    return "secret-service:" + name;
                }
                Log.Warn("Secret Service store failed — falling back to a private file");
                goto default;

            default:
                WriteToFile(name, value);
                return "file:" + name;
        }
    }

    /// يقرأ السر من مرجعه؛ null إن تعذّر (مخزن مقفل، ملف محذوف، حساب ويندوز آخر)
    public static string? Load(string? reference)
    {
        if (string.IsNullOrEmpty(reference)) return null;
        try
        {
            if (reference.StartsWith("dpapi:", StringComparison.Ordinal))
            {
                if (!OperatingSystem.IsWindows()) return null;
                var bytes = Dpapi.Unprotect(Convert.FromBase64String(reference["dpapi:".Length..]));
                return Encoding.UTF8.GetString(bytes);
            }
            if (reference.StartsWith("secret-service:", StringComparison.Ordinal))
                return SecretToolLookup(reference["secret-service:".Length..]);
            if (reference.StartsWith("file:", StringComparison.Ordinal))
                return ReadFromFile(reference["file:".Length..]);
        }
        catch (Exception ex)
        {
            Log.Error("could not read a stored secret", ex);
        }
        return null;
    }

    public static void Delete(string name)
    {
        RemoveFromFile(name);
        if (!OperatingSystem.IsWindows() && SecretToolAvailable())
            RunSecretTool(new[] { "clear", "application", SecretToolApp, "key", name }, null, out _);
    }

    // ===== الملف الخاص =====

    private static Dictionary<string, string> ReadFile()
    {
        var path = SecretsFilePath;
        if (!File.Exists(path)) return new();
        return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? new();
    }

    private static string? ReadFromFile(string name)
    {
        lock (FileLock)
            return ReadFile().TryGetValue(name, out var v) ? v : null;
    }

    private static void WriteToFile(string name, string value)
    {
        lock (FileLock)
        {
            var data = ReadFile();
            data[name] = value;
            WriteFile(data);
        }
    }

    private static void RemoveFromFile(string name)
    {
        lock (FileLock)
        {
            try
            {
                var data = ReadFile();
                if (!data.Remove(name)) return;
                if (data.Count == 0)
                {
                    File.Delete(SecretsFilePath);
                    try { File.Delete(AtomicFile.BackupPathFor(SecretsFilePath)); } catch { /* best effort */ }
                }
                else WriteFile(data);
            }
            catch (Exception ex)
            {
                Log.Error("could not update secrets.json", ex);
            }
        }
    }

    private static void WriteFile(Dictionary<string, string> data)
    {
        var path = SecretsFilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (!OperatingSystem.IsWindows())
        {
            // ننشئ الملف بصلاحية 0600 قبل كتابة أي سر فيه
            if (!File.Exists(path)) File.WriteAllText(path, "{}");
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        AtomicFile.WriteAllText(path, JsonSerializer.Serialize(data));
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            var bak = AtomicFile.BackupPathFor(path);
            if (File.Exists(bak)) File.SetUnixFileMode(bak, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    // ===== Secret Service عبر secret-tool (libsecret) =====

    private static bool? _secretToolAvailable;

    private static bool SecretToolAvailable()
    {
        if (OperatingSystem.IsWindows()) return false;
        if (_secretToolAvailable is { } cached) return cached;
        var onPath = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(':', StringSplitOptions.RemoveEmptyEntries)
            .Any(d => File.Exists(Path.Combine(d, "secret-tool")));
        var hasBus = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS"));
        _secretToolAvailable = onPath && hasBus;
        return _secretToolAvailable.Value;
    }

    private static bool SecretToolStore(string name, string value) =>
        RunSecretTool(new[] { "store", "--label=Halawa-Hub " + name, "application", SecretToolApp, "key", name }, value, out _);

    private static string? SecretToolLookup(string name) =>
        RunSecretTool(new[] { "lookup", "application", SecretToolApp, "key", name }, null, out var output) && output.Length > 0
            ? output
            : null;

    private static bool RunSecretTool(string[] args, string? stdin, out string stdout)
    {
        stdout = "";
        try
        {
            var psi = new ProcessStartInfo("secret-tool")
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            foreach (var a in args) psi.ArgumentList.Add(a);

            using var p = Process.Start(psi);
            if (p == null) return false;
            if (stdin != null) p.StandardInput.Write(stdin);
            p.StandardInput.Close();
            var output = p.StandardOutput.ReadToEndAsync();
            if (!p.WaitForExit((int)ToolTimeout.TotalMilliseconds))
            {
                try { p.Kill(entireProcessTree: true); } catch { /* best effort */ }
                Log.Error("secret-tool timed out (keyring locked?)");
                return false;
            }
            stdout = output.Result.TrimEnd(new[] { (char)13, (char)10 });
            return p.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Log.Error("secret-tool failed", ex);
            return false;
        }
    }

    // ===== DPAPI (ويندوز) =====

    private static class Dpapi
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Halawa-Hub secret v1");

        [StructLayout(LayoutKind.Sequential)]
        private struct DataBlob
        {
            public int Size;
            public IntPtr Data;
        }

        private const int CryptProtectUiForbidden = 0x1;

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptProtectData(ref DataBlob dataIn, string? description, ref DataBlob entropy,
            IntPtr reserved, IntPtr prompt, int flags, ref DataBlob dataOut);

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptUnprotectData(ref DataBlob dataIn, IntPtr description, ref DataBlob entropy,
            IntPtr reserved, IntPtr prompt, int flags, ref DataBlob dataOut);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr mem);

        public static byte[] Protect(byte[] data) => Run(data, protect: true);
        public static byte[] Unprotect(byte[] data) => Run(data, protect: false);

        private static byte[] Run(byte[] data, bool protect)
        {
            var input = Pin(data, out var inHandle);
            var entropy = Pin(Entropy, out var entHandle);
            var output = new DataBlob();
            try
            {
                var ok = protect
                    ? CryptProtectData(ref input, "Halawa-Hub", ref entropy, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, ref output)
                    : CryptUnprotectData(ref input, IntPtr.Zero, ref entropy, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, ref output);
                if (!ok) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

                var result = new byte[output.Size];
                Marshal.Copy(output.Data, result, 0, output.Size);
                return result;
            }
            finally
            {
                inHandle.Free();
                entHandle.Free();
                if (output.Data != IntPtr.Zero) LocalFree(output.Data);
            }
        }

        private static DataBlob Pin(byte[] bytes, out GCHandle handle)
        {
            handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            return new DataBlob { Size = bytes.Length, Data = handle.AddrOfPinnedObject() };
        }
    }
}
