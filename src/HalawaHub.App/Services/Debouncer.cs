using System;
using System.Threading;
using System.Threading.Tasks;

namespace HalawaHub.App.Services;

/// <summary>
/// يؤجل تنفيذ إجراء حتى يتوقف طلبه لفترة قصيرة، مثل حفظ مفتاح API بعد انتهاء
/// الكتابة بدل الحفظ عند كل حرف. Flush ينفذ الإجراء المعلق فورًا.
/// </summary>
public sealed class Debouncer
{
    private readonly TimeSpan _delay;
    private readonly Action _action;
    private readonly object _lock = new();
    private CancellationTokenSource? _pending;

    public Debouncer(TimeSpan delay, Action action)
    {
        _delay = delay;
        _action = action;
    }

    public bool HasPending
    {
        get { lock (_lock) return _pending != null; }
    }

    public void Schedule()
    {
        var cts = new CancellationTokenSource();
        lock (_lock)
        {
            _pending?.Cancel();
            _pending = cts;
        }
        _ = RunAfterDelayAsync(cts);
    }

    public void Flush()
    {
        lock (_lock)
        {
            if (_pending == null) return;
            _pending.Cancel();
            _pending = null;
        }
        _action();
    }

    private async Task RunAfterDelayAsync(CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(_delay, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        lock (_lock)
        {
            if (!ReferenceEquals(_pending, cts)) return;
            _pending = null;
        }
        _action();
    }
}
