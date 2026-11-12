using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using HalawaHub.Core;

namespace HalawaHub.App.Services;

/// <summary>
/// "تقليل الحركة": off (الافتراضي، الحركة جزء من التصميم وتعمل)، on (بلا حركة)،
/// auto (يتبع النظام). الافتراضي off عمدًا: ربط الحركة بخيار النظام كان يوقفها بصمت
/// عند من عطّل تأثيرات ويندوز لتسريع جهازه — الآن هو اختيار ظاهر في الإعدادات.
/// المصادر في وضع auto:
///   ويندوز: SystemParametersInfo(SPI_GETCLIENTAREAANIMATION) = "تأثيرات الحركة"
///   KDE: AnimationDurationFactor=0 في ~/.config/kdeglobals
///   GNOME: gsettings org.gnome.desktop.interface enable-animations = false
/// </summary>
public static class MotionPreferences
{
    public const string Off = "off";
    public const string On = "on";
    public const string Auto = "auto";

    public static string Current { get; private set; } = Off;

    /// هل تقليل الحركة مفعّل فعليًا الآن (بعد حل auto)
    public static bool Reduced => !MotionSettings.Enabled;

    public static void Apply(string? mode)
    {
        Current = mode is On or Auto ? mode : Off;
        var reduce = Current switch
        {
            On => true,
            Auto => SystemPrefersReducedMotion() ?? false,
            _ => false
        };
        MotionSettings.Enabled = !reduce;
    }

    /// null = لا نعرف (نعامله كـ "لا")
    public static bool? SystemPrefersReducedMotion()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var enabled = 0;
                return SystemParametersInfo(SpiGetClientAreaAnimation, 0, ref enabled, 0) ? enabled == 0 : null;
            }
            if (OperatingSystem.IsLinux())
                return KdePrefersReduced(ReadKdeGlobals()) ?? GnomePrefersReduced();
        }
        catch (Exception ex)
        {
            Log.Error("reduce-motion detection failed", ex);
        }
        return null;
    }

    private const uint SpiGetClientAreaAnimation = 0x1042;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint action, uint param, ref int value, uint winIni);

    private static string? ReadKdeGlobals()
    {
        var config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var path = Path.Combine(string.IsNullOrEmpty(config) ? Path.Combine(home, ".config") : config, "kdeglobals");
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    /// قسم [KDE] فيه AnimationDurationFactor؛ 0 = بلا حركة
    internal static bool? KdePrefersReduced(string? kdeglobals)
    {
        if (string.IsNullOrEmpty(kdeglobals)) return null;
        var section = Regex.Match(kdeglobals, @"^\[KDE\]\s*$(?<body>(?:(?!^\[).)*)", RegexOptions.Multiline | RegexOptions.Singleline);
        if (!section.Success) return null;
        var m = Regex.Match(section.Groups["body"].Value, @"^AnimationDurationFactor\s*=\s*(?<v>[0-9.]+)", RegexOptions.Multiline);
        if (!m.Success) return null;
        return double.TryParse(m.Groups["v"].Value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var factor) ? factor == 0 : null;
    }

    private static bool? GnomePrefersReduced()
    {
        try
        {
            var psi = new ProcessStartInfo("gsettings") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            foreach (var a in new[] { "get", "org.gnome.desktop.interface", "enable-animations" }) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi);
            if (p == null) return null;
            var output = p.StandardOutput.ReadToEnd().Trim();
            if (!p.WaitForExit(2000)) return null;
            return output switch { "false" => true, "true" => false, _ => null };
        }
        catch
        {
            return null; // gsettings غير موجود (ليس GNOME)
        }
    }
}
