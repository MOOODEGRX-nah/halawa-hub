using System.Reflection;

namespace HalawaHub.Core;

public static class AppInfo
{
    // نسخة التجميع رباعية دائمًا (من Directory.Build.props)
    public static Version Current { get; } =
        typeof(AppInfo).Assembly.GetName().Version ?? new Version(0, 0, 0, 0);
    
    public static string Version => Current.ToString(4);
    
    // معلومات إضافية
    public static string AppName => "Halawa-Hub";
    public static string GitHubOwner => "MOOODEGRX-nah";
    public static string GitHubRepo => "halawa-hub";
}
