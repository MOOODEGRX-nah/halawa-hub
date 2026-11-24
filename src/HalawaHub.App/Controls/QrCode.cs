using System;
using System.Collections;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using QRCoder;

namespace HalawaHub.App.Controls;

/// <summary>
/// رمز QR يُرسم مربعات بنفسه: QRCoder (MIT) يحسب المصفوفة فقط، بلا System.Drawing،
/// فيعمل كما هو على ويندوز ولينكس وفي النسخة المستقلة.
/// </summary>
public class QrCode : Control
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<QrCode, string?>(nameof(Text));

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    private List<BitArray>? _matrix;

    static QrCode()
    {
        AffectsRender<QrCode>(TextProperty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty) _matrix = Encode(Text);
    }

    /// المصفوفة مع هامش المسح الأبيض (QRCoder يضيفه افتراضيًا)
    internal static List<BitArray>? Encode(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        return data.ModuleMatrix;
    }

    public override void Render(DrawingContext context)
    {
        var b = Bounds;
        context.FillRectangle(Brushes.White, new Rect(b.Size));
        if (_matrix is not { Count: > 0 } m) return;
        var n = m.Count;
        var cell = Math.Floor(Math.Min(b.Width, b.Height) / n);
        if (cell < 1) cell = Math.Min(b.Width, b.Height) / n;
        var ox = (b.Width - cell * n) / 2;
        var oy = (b.Height - cell * n) / 2;
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            for (var y = 0; y < n; y++)
                for (var x = 0; x < n; x++)
                {
                    if (!m[y][x]) continue;
                    var px = ox + x * cell;
                    var py = oy + y * cell;
                    g.BeginFigure(new Point(px, py), true);
                    g.LineTo(new Point(px + cell, py));
                    g.LineTo(new Point(px + cell, py + cell));
                    g.LineTo(new Point(px, py + cell));
                    g.EndFigure(true);
                }
        }
        context.DrawGeometry(Brushes.Black, null, geometry);
    }
}
