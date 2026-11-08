using System;
using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HalawaHub.Core;

namespace HalawaHub.App.Services;

/// <summary>
/// يضمن نسخة واحدة من البرنامج لكل مستخدم. بما أن زر الإغلاق يخفي النافذة للصينية،
/// تشغيل الملف مرة ثانية كان يفتح نسخة ثانية؛ الآن النسخة الجديدة تطلب من الأولى
/// إظهار نافذتها ثم تخرج. الإشارة عبر Named Pipe لأن EventWaitHandle المسمى لا يعمل على لينكس.
///
/// الكشف على ويندوز Mutex: لا نمتلكه (initiallyOwned=false) فيكفي وجوده، وإذا انتهت
/// النسخة الأولى أو قتلها سكربت التحديث يزول تلقائيًا.
///
/// الكشف على لينكس قفل ملف (flock عبر FileShare.None) وليس Mutex: الـ Mutex المسمى في
/// .NET على لينكس محصور في جلسة العملية (setsid)، والتشغيل من قائمة التطبيقات ثم من
/// الطرفية جلستان مختلفتان، فكانت تفتح نسختان. النواة تحرر القفل عند موت العملية بأي
/// طريقة، فلا يوجد قفل عالق بعد crash أو kill -9.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    public const string DefaultId = "HalawaHub";

    private readonly IDisposable _instanceLock;
    private readonly string _pipeName;
    private CancellationTokenSource? _listenCts;

    public bool IsPrimary { get; }

    private SingleInstanceGuard(IDisposable instanceLock, bool isPrimary, string pipeName)
    {
        _instanceLock = instanceLock;
        IsPrimary = isPrimary;
        _pipeName = pipeName;
    }

    public static SingleInstanceGuard Acquire(string id = DefaultId)
    {
        var suffix = UserScopedSuffix();
        var pipeName = $"{id}-Activate-{suffix}";

        if (OperatingSystem.IsWindows())
        {
            var mutex = new Mutex(false, $"{id}-SingleInstance-{suffix}", out var createdNew);
            return new SingleInstanceGuard(mutex, createdNew, pipeName);
        }

        var lockFile = TryLockFile(LockFilePath(id, suffix, Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR")));
        return new SingleInstanceGuard(lockFile ?? (IDisposable)new NoLock(), lockFile != null, pipeName);
    }

    /// مجلد التشغيل الخاص بالمستخدم في الذاكرة، وإلا مجلد البيانات
    internal static string LockFilePath(string id, string suffix, string? runtimeDirectory) => Path.Combine(
        !string.IsNullOrWhiteSpace(runtimeDirectory) && Directory.Exists(runtimeDirectory)
            ? runtimeDirectory
            : AppPaths.DataDirectory,
        $"{id}-SingleInstance-{suffix}.lock");

    /// null يعني أن نسخة أخرى تمسك القفل
    private static FileStream? TryLockFile(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            // الملف يبقى في مكانه عمدًا: حذفه يفتح سباقًا بين نسختين تبدآن معًا
            return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            // لا نمنع المستخدم من فتح البرنامج بسبب مشكلة صلاحيات في ملف القفل
            Log.Error("single-instance: lock file is not accessible, continuing without it", ex);
            return new FileStream(Path.GetTempFileName(), FileMode.Open, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
        }
    }

    private sealed class NoLock : IDisposable
    {
        public void Dispose()
        {
        }
    }

    /// <summary>تستدعيها النسخة الأولى، و onActivate يعمل على خيط خلفي.</summary>
    public void StartListening(Action onActivate)
    {
        if (!IsPrimary || _listenCts != null) return;

        _listenCts = new CancellationTokenSource();
        var token = _listenCts.Token;
        _ = Task.Run(() => ListenLoopAsync(onActivate, token), token);
    }

    /// <summary>تستدعيها النسخة الثانية. ترجع false لو لم ترد النسخة الأولى خلال المهلة.</summary>
    public bool SignalPrimary(int timeoutMs = 3000)
    {
        try
        {
            using var client = new NamedPipeClientStream(
                ".", _pipeName, PipeDirection.Out, PipeOptions.CurrentUserOnly);
            client.Connect(timeoutMs);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("single-instance: could not signal the running instance", ex);
            return false;
        }
    }

    private async Task ListenLoopAsync(Action onActivate, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

                await server.WaitForConnectionAsync(token);
                onActivate();
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Log.Error("single-instance: activation listener failed", ex);
                try { await Task.Delay(1000, token); }
                catch (OperationCanceledException) { return; }
            }
        }
    }

    // ثابت لكل مستخدم، وآمن كاسم Mutex/Pipe مهما كانت أحرف اسم المستخدم
    private static string UserScopedSuffix()
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(Environment.UserName));
        return Convert.ToHexString(hash, 0, 8);
    }

    public void Dispose()
    {
        _listenCts?.Cancel();
        _instanceLock.Dispose();
    }
}
