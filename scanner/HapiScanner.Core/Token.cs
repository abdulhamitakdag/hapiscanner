using System.Text.RegularExpressions;

namespace HapiScanner.Core;

public static class Token
{

    private static readonly Regex Bicim =
        new("^HAPI-[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{4}-[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{4}$",
            RegexOptions.Compiled);

    public static string Normalize(string girdi)
    {
        if (girdi is null) return string.Empty;
        var temiz = girdi.Trim().ToUpperInvariant().Replace(" ", "").Replace("\t", "");
        if (temiz.Length > 0 && !temiz.StartsWith("HAPI", StringComparison.Ordinal))
            temiz = "HAPI-" + temiz;

        if (Regex.IsMatch(temiz, "^HAPI[A-Z0-9]{8}$"))
            temiz = $"HAPI-{temiz.Substring(4, 4)}-{temiz.Substring(8, 4)}";
        return temiz;
    }

    public static bool GecerliMi(string girdi) => Bicim.IsMatch(Normalize(girdi));
}
