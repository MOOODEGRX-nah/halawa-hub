namespace HalawaHub.Core;

/// <summary>
/// المصدر الوحيد لمجلد بيانات المستخدم: %LOCALAPPDATA%\HalawaHub على ويندوز
/// و ~/.local/share/HalawaHub على لينكس (نفس المسارات القديمة بالضبط).
///
/// متغير البيئة HALAWAHUB_DATA_DIR يحوّل المجلد لمكان آخر، وتستخدمه الاختبارات
/// حتى لا تكتب أبدًا في بيانات المستخدم الحقيقية.
/// </summary>
public static class AppPaths
{
    public const string DataDirOverrideVariable = "HALAWAHUB_DATA_DIR";

    public static string DataDirectory { get; } = ResolveDataDirectory(
        Environment.GetEnvironmentVariable(DataDirOverrideVariable));

    public static string InData(params string[] parts) =>
        Path.Combine(new[] { DataDirectory }.Concat(parts).ToArray());

    internal static string ResolveDataDirectory(string? overrideDir) =>
        !string.IsNullOrWhiteSpace(overrideDir)
            ? overrideDir
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HalawaHub");
}
