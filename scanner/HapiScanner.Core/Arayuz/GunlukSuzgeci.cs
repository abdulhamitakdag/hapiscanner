using System.Text.RegularExpressions;

namespace HapiScanner.Core.Arayuz;

public static class GunlukSuzgeci
{
    private static readonly Regex WindowsYolu = new(
        @"[A-Za-z]:\\[^\s""]*", RegexOptions.Compiled);

    private static readonly Regex UncYolu = new(
        @"\\\\[^\s""]+", RegexOptions.Compiled);

    private static readonly Regex UnixYolu = new(
        @"(?<![\w.])/(?:home|Users|root|mnt|media)/[^\s""]*", RegexOptions.Compiled);

    private static readonly Regex Token = new(
        @"HAPI-[A-Za-z0-9]{4}-[A-Za-z0-9]{4}", RegexOptions.Compiled);

    public const string YolMaskesi = "[yol gizlendi]";
    public const string TokenMaskesi = "HAPI-****-****";

    public static string Temizle(string? satir)
    {
        if (string.IsNullOrWhiteSpace(satir)) return string.Empty;
        var temiz = satir.Trim();
        temiz = UncYolu.Replace(temiz, YolMaskesi);
        temiz = WindowsYolu.Replace(temiz, YolMaskesi);
        temiz = UnixYolu.Replace(temiz, YolMaskesi);
        temiz = Token.Replace(temiz, TokenMaskesi);
        return temiz.Length > 300 ? temiz[..300] + "..." : temiz;
    }
}
