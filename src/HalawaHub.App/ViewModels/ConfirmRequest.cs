using System;
using System.Collections.Generic;

namespace HalawaHub.App.ViewModels;

/// زر في نافذة التأكيد. Run يُنفَّذ بعد إغلاق النافذة.
public sealed class ConfirmChoice
{
    public required string Label { get; init; }
    public bool IsPrimary { get; init; }
    public Action? Run { get; init; }
}

/// <summary>
/// طلب تأكيد واحد (إضافة جديدة، لعبة لها تحديث، حذف البيانات...). الطلبات تُعرض
/// واحدًا تلو الآخر بنفس تصميم نافذة "ما الجديد". Esc أو ✕ = آخر خيار (الإلغاء).
/// </summary>
public sealed class ConfirmRequest
{
    public required string Title { get; init; }
    public required string Message { get; init; }

    /// سطور تفاصيل تظهر بخط أصغر (مسار، هاش...)؛ تُعرض كما هي بلا ترجمة
    public IReadOnlyList<string> Details { get; init; } = Array.Empty<string>();

    public required IReadOnlyList<ConfirmChoice> Choices { get; init; }

    public bool HasDetails => Details.Count > 0;
}
