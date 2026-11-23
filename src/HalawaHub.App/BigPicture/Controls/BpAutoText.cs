using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using HalawaHub.App.Services;

namespace HalawaHub.App.BigPicture.Controls;

/// <summary>
/// نص باتجاه محتواه (dir="auto" في التصميم): اسم لعبة لاتيني داخل الواجهة العربية
/// يُكتب من اليسار ويُقص من نهايته، ويبقى ملاصقًا لبداية السطر في الواجهة.
/// </summary>
public class BpAutoText : TextBlock
{
    protected override Type StyleKeyOverride => typeof(TextBlock);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty) Apply();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Loc.Instance.LanguageChanged += OnLanguage;
        Apply();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Loc.Instance.LanguageChanged -= OnLanguage;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnLanguage(object? sender, EventArgs e) => Apply();

    private void Apply()
    {
        var textRtl = IsRtlText(Text);
        FlowDirection = textRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        TextAlignment = textRtl == Loc.Instance.IsRtl ? TextAlignment.Start : TextAlignment.End;
    }

    /// <summary>
    /// أي حرف عربي يجعل النص جملة عربية (من اليمين): "Elden Ring تعمل الآن" جملة عربية تبدأ
    /// باسم لعبة، وكانت تُعامل كإنجليزية لأن أول حرف لاتيني. نص بلا حروف أصلًا يتبع الواجهة.
    /// </summary>
    internal static bool IsRtlText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return Loc.Instance.IsRtl;
        var hasLetter = false;
        foreach (var ch in text)
        {
            if (ch is >= '֐' and <= 'ࣿ' or >= 'יִ' and <= '﷿' or >= 'ﹰ' and <= '﻿') return true;
            if (char.IsLetter(ch)) hasLetter = true;
        }
        return !hasLetter && Loc.Instance.IsRtl;
    }
}
