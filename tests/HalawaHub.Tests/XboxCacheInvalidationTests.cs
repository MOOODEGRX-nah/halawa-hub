using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using HalawaHub.Core.Library;
using Xunit;

namespace HalawaHub.Tests;

/// <summary>
/// اختبارات انحدار لـ B06: إبطال كاش Xbox + قاعدة "cache successes only".
/// ملاحظة: هذه الاختبارات لا تشغّل PowerShell (تحتاج Windows CI) — تختبر فقط
/// منطق الكاش عبر reflection للوصول للحقول الساكنة الخاصة.
/// </summary>
public class XboxCacheInvalidationTests : IDisposable
{
    public XboxCacheInvalidationTests()
    {
        // نظّف الكاش قبل كل اختبار
        XboxLibraryProvider.InvalidateCache();
    }

    public void Dispose()
    {
        // نظّف الكاش بعد كل اختبار
        XboxLibraryProvider.InvalidateCache();
    }

    [Fact]
    public void InvalidateCache_ClearsCachedEntries()
    {
        // Arrange: املأ الكاش بقيمة وهمية
        SetCachedEntries(new List<object> { new { Name = "Fake Game" } });

        // Act
        XboxLibraryProvider.InvalidateCache();

        // Assert: الكاش أصبح null
        GetCachedEntries().Should().BeNull();
    }

    [Fact]
    public void InvalidateCache_ResetsLastScan()
    {
        // Arrange
        SetCachedEntries(new List<object> { new { Name = "Fake" } });
        SetLastScan(DateTime.UtcNow);

        // Act
        XboxLibraryProvider.InvalidateCache();

        // Assert
        GetLastScan().Should().Be(DateTime.MinValue);
    }

    [Fact]
    public void CacheFields_AreStatic()
    {
        // sanity: نتأكد أن الحقول ساكنة (وإلا منطق الـ InvalidateCache يفشل)
        var entriesField = typeof(XboxLibraryProvider).GetField(
            "_cachedEntries", BindingFlags.NonPublic | BindingFlags.Static);
        var lastScanField = typeof(XboxLibraryProvider).GetField(
            "_lastScan", BindingFlags.NonPublic | BindingFlags.Static);

        entriesField.Should().NotBeNull("_cachedEntries must exist as static");
        lastScanField.Should().NotBeNull("_lastScan must exist as static");
        entriesField!.IsStatic.Should().BeTrue();
        lastScanField!.IsStatic.Should().BeTrue();
    }

    [Fact]
    public void CacheLock_IsStatic()
    {
        var lockField = typeof(XboxLibraryProvider).GetField(
            "CacheLock", BindingFlags.NonPublic | BindingFlags.Static);
        lockField.Should().NotBeNull("CacheLock must exist for thread-safe cache access");
        lockField!.IsStatic.Should().BeTrue();
    }

    private static object? GetCachedEntries()
    {
        var field = typeof(XboxLibraryProvider).GetField(
            "_cachedEntries", BindingFlags.NonPublic | BindingFlags.Static);
        return field?.GetValue(null);
    }

    private static void SetCachedEntries(object? value)
    {
        var field = typeof(XboxLibraryProvider).GetField(
            "_cachedEntries", BindingFlags.NonPublic | BindingFlags.Static);
        field?.SetValue(null, value);
    }

    private static DateTime GetLastScan()
    {
        var field = typeof(XboxLibraryProvider).GetField(
            "_lastScan", BindingFlags.NonPublic | BindingFlags.Static);
        return (DateTime)(field?.GetValue(null) ?? DateTime.MinValue);
    }

    private static void SetLastScan(DateTime value)
    {
        var field = typeof(XboxLibraryProvider).GetField(
            "_lastScan", BindingFlags.NonPublic | BindingFlags.Static);
        field?.SetValue(null, value);
    }
}
