using System.Text;

namespace HalawaHub.Core.Library;

/// حساب Steam سجّل الدخول على هذا الجهاز (config/loginusers.vdf)
public sealed record SteamAccount(string SteamId64, string PersonaName, bool MostRecent, long Timestamp)
{
    /// اسم مجلد userdata: رقم الحساب 32 بت = SteamID64 − 76561197960265728
    public string AccountId =>
        ulong.TryParse(SteamId64, out var id) && id > SteamPlaytimeImport.SteamId64Base
            ? (id - SteamPlaytimeImport.SteamId64Base).ToString()
            : "";
}

/// <summary>
/// وقت اللعب الذي يحفظه Steam نفسه محليًا: userdata/&lt;account&gt;/config/localconfig.vdf →
/// UserLocalConfigStore/Software/Valve/Steam/Apps/&lt;appid&gt;/Playtime (بالدقائق) و LastPlayed (unix).
/// الصيغة مؤكدة من ملفات حقيقية (Steam نوفمبر 2026). قراءة فقط، بلا شبكة ولا مفتاح.
/// </summary>
public static class SteamPlaytimeImport
{
    public const ulong SteamId64Base = 76561197960265728UL;

    public static List<SteamAccount> ReadAccounts(string steamPath)
    {
        try
        {
            var path = Path.Combine(steamPath, "config", "loginusers.vdf");
            return File.Exists(path) ? ParseAccounts(File.ReadAllText(path)) : new();
        }
        catch (Exception ex)
        {
            Log.Error("Steam: loginusers.vdf unreadable", ex);
            return new();
        }
    }

    internal static List<SteamAccount> ParseAccounts(string vdf)
    {
        var result = new List<SteamAccount>();
        if (Vdf.Parse(vdf) is not { } root || Vdf.Child(root, "users") is not { } users) return result;
        foreach (var (id, value) in users)
        {
            if (value is not Dictionary<string, object> u) continue;
            long.TryParse(Vdf.Text(u, "timestamp"), out var ts);
            result.Add(new SteamAccount(id, Vdf.Text(u, "PersonaName") ?? Vdf.Text(u, "AccountName") ?? id,
                Vdf.Text(u, "MostRecent") == "1", ts));
        }
        return result;
    }

    /// الحساب المختار، وإلا MostRecent، وإلا صاحب أحدث دخول (بعض إصدارات Steam لا تكتب MostRecent)
    public static SteamAccount? PickAccount(IReadOnlyList<SteamAccount> accounts, string? chosenSteamId64)
    {
        if (accounts.Count == 0) return null;
        return accounts.FirstOrDefault(a => a.SteamId64 == chosenSteamId64)
               ?? accounts.FirstOrDefault(a => a.MostRecent)
               ?? accounts.OrderByDescending(a => a.Timestamp).First();
    }

    /// appid → (ثواني اللعب، آخر تشغيل UTC أو null)
    public static Dictionary<string, (long Seconds, DateTime? LastPlayedUtc)> ReadPlaytimes(string steamPath, SteamAccount account)
    {
        try
        {
            if (account.AccountId.Length == 0) return new();
            var path = Path.Combine(steamPath, "userdata", account.AccountId, "config", "localconfig.vdf");
            if (!File.Exists(path)) return new();
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(fs, Encoding.UTF8);
            return ParsePlaytimes(reader.ReadToEnd());
        }
        catch (Exception ex)
        {
            Log.Error("Steam: localconfig.vdf unreadable", ex);
            return new();
        }
    }

    internal static Dictionary<string, (long Seconds, DateTime? LastPlayedUtc)> ParsePlaytimes(string vdf)
    {
        var result = new Dictionary<string, (long, DateTime?)>();
        var node = Vdf.Parse(vdf);
        foreach (var key in new[] { "UserLocalConfigStore", "Software", "Valve", "Steam", "Apps" })
        {
            node = node == null ? null : Vdf.Child(node, key);
        }
        if (node == null) return result;

        foreach (var (appId, value) in node)
        {
            if (value is not Dictionary<string, object> app) continue;
            if (!long.TryParse(Vdf.Text(app, "Playtime"), out var minutes) || minutes <= 0) continue;
            DateTime? last = long.TryParse(Vdf.Text(app, "LastPlayed"), out var unix) && unix > 0
                ? DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime
                : null;
            result[appId] = (minutes * 60, last);
        }
        return result;
    }
}

/// <summary>
/// محلّل صغير لصيغة VDF النصية (KeyValues): "مفتاح" "قيمة" أو "مفتاح" { … }.
/// المفاتيح بلا حساسية لحالة الأحرف (Steam يكتب "Apps" و"apps" في إصدارات مختلفة).
/// </summary>
internal static class Vdf
{
    public static Dictionary<string, object>? Parse(string text)
    {
        try
        {
            var pos = 0;
            return ParseObject(text, ref pos, topLevel: true);
        }
        catch (Exception ex)
        {
            Log.Error("VDF parse failed", ex);
            return null;
        }
    }

    public static Dictionary<string, object>? Child(Dictionary<string, object> node, string key) =>
        node.TryGetValue(key, out var v) ? v as Dictionary<string, object> : null;

    public static string? Text(Dictionary<string, object> node, string key) =>
        node.TryGetValue(key, out var v) ? v as string : null;

    private static Dictionary<string, object> ParseObject(string s, ref int pos, bool topLevel)
    {
        var obj = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        while (true)
        {
            var key = NextToken(s, ref pos);
            if (key == null || key == "}")
            {
                if (key == null && !topLevel) throw new FormatException("unexpected end of VDF");
                return obj;
            }
            if (key == "{") throw new FormatException("unexpected { in VDF");

            var value = NextToken(s, ref pos) ?? throw new FormatException("missing value in VDF");
            if (value == "{") obj[key] = ParseObject(s, ref pos, topLevel: false);
            else if (value == "}") throw new FormatException("unexpected } in VDF");
            else obj[key] = value;
        }
    }

    /// نص بين علامتي تنصيص (مع \" و \\)، أو { أو }؛ null عند النهاية. التعليقات // تُتخطى.
    private static string? NextToken(string s, ref int pos)
    {
        while (pos < s.Length)
        {
            var c = s[pos];
            if (char.IsWhiteSpace(c)) { pos++; continue; }
            if (c == '/' && pos + 1 < s.Length && s[pos + 1] == '/')
            {
                while (pos < s.Length && s[pos] != '\n') pos++;
                continue;
            }
            break;
        }
        if (pos >= s.Length) return null;

        var ch = s[pos];
        if (ch == '{' || ch == '}') { pos++; return ch.ToString(); }

        if (ch == '"')
        {
            pos++;
            var sb = new StringBuilder();
            while (pos < s.Length && s[pos] != '"')
            {
                if (s[pos] == '\\' && pos + 1 < s.Length)
                {
                    pos++;
                    sb.Append(s[pos] switch { 'n' => '\n', 't' => '\t', var o => o });
                }
                else sb.Append(s[pos]);
                pos++;
            }
            pos++; // علامة الإغلاق
            return sb.ToString();
        }

        // مفتاح بلا تنصيص (نادر)
        var start = pos;
        while (pos < s.Length && !char.IsWhiteSpace(s[pos]) && s[pos] is not ('{' or '}' or '"')) pos++;
        return s[start..pos];
    }
}
