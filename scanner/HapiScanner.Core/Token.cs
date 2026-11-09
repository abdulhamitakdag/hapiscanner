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

    private const string Alfabe = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string Bicimle(string girdi)
    {
        if (string.IsNullOrEmpty(girdi)) return string.Empty;

        var harfler = new System.Text.StringBuilder();
        foreach (var karakter in girdi.ToUpperInvariant())
            if (char.IsLetterOrDigit(karakter)) harfler.Append(karakter);

        var kalan = harfler.ToString();
        while (kalan.StartsWith("HAPI", StringComparison.Ordinal)) kalan = kalan[4..];

        var govde = new System.Text.StringBuilder();
        foreach (var karakter in kalan)
            if (Alfabe.IndexOf(karakter) >= 0) govde.Append(karakter);

        var metin = govde.ToString();
        foreach (var artik in new[] { "HAP", "HA", "H" })
            if (metin.Length > 8 && metin.StartsWith(artik, StringComparison.Ordinal))
            {
                metin = metin[artik.Length..];
                break;
            }
        if (metin.Length > 8) metin = metin[..8];
        if (metin.Length == 0) return string.Empty;

        return metin.Length <= 4
            ? "HAPI-" + metin
            : $"HAPI-{metin[..4]}-{metin[4..]}";
    }
}
