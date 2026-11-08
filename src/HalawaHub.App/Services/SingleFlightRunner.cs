using System;
using System.Threading;
using System.Threading.Tasks;

namespace HalawaHub.App.Services;

/// <summary>
/// يشغّل عملًا ثقيلًا على خيط خلفي، ويتجاهل أي طلب جديد ما دام العمل السابق
/// مستمرًا. يستخدمه فحص المكتبات: مزود Xbox قد يأخذ حتى 60 ثانية، فلا يجوز أن
/// يجمّد الواجهة ولا أن تتراكم عدة عمليات فحص.
/// </summary>
public sealed class SingleFlightRunner
{
    private int _running;

    public bool IsRunning => Volatile.Read(ref _running) == 1;

    /// <returns>Ran=false يعني أن الطلب تُجوهل لأن عملًا آخر ما زال يعمل.</returns>
    public async Task<(bool Ran, T? Result)> RunAsync<T>(Func<T> work)
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
            return (false, default);

        try
        {
            var result = await Task.Run(work);
            return (true, result);
        }
        finally
        {
            Volatile.Write(ref _running, 0);
        }
    }
}
