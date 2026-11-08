using System;
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
/// إظهار نافذتها ثم تخرج.
///
/// Mutex للكشف، وNamed Pipe للإشارة لأن EventWaitHandle المسمى لا يعمل على لينكس.
/// لا نمتلك الـ Mutex (initiallyOwned=false): يكفي وجوده، وهكذا لا يوجد ارتباط بخيط
/// محدد. إذا انتهت النسخة الأولى أو قتلها سكربت التحديث يزول الـ Mutex تلقائيًا.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    public const string DefaultId = "HalawaHub";

    private readonly Mutex _mutex;
    private readonly string _pipeName;
    private CancellationTokenSource? _listenCts;

    public bool IsPrimary { get; }

    private SingleInstanceGuard(Mutex mutex, bool isPrimary, string pipeName)
    {
        _mutex = mutex;
        IsPrimary = isPrimary;
        _pipeName = pipeName;
    }

    public static SingleInstanceGuard Acquire(string id = DefaultId)
    {
        var suffix = UserScopedSuffix();
        var mutex = new Mutex(false, $"{id}-SingleInstance-{suffix}", out var createdNew);
        return new SingleInstanceGuard(mutex, createdNew, $"{id}-Activate-{suffix}");
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
        _mutex.Dispose();
    }
}
