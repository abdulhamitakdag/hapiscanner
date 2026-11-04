using System.Security.Cryptography;
using System.Text;

namespace HapiScanner.Core.Dosya;

public static class Hashleme
{

    public const long AzamiHashBoyutu = 512L * 1024 * 1024;

    public static string MetinOzeti(string metin)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(metin))).ToLowerInvariant();

    public static string BaytOzeti(byte[] ham)
        => Convert.ToHexString(SHA256.HashData(ham)).ToLowerInvariant();

    public static string AkisOzeti(Stream akis)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(akis)).ToLowerInvariant();
    }

    public static string? DosyaOzeti(string yol, long azamiBoyut = AzamiHashBoyutu)
    {
        try
        {
            var bilgi = new FileInfo(yol);
            if (!bilgi.Exists || bilgi.Length > azamiBoyut) return null;
            using var akis = new FileStream(yol, FileMode.Open, FileAccess.Read,
                                            FileShare.ReadWrite | FileShare.Delete);
            return AkisOzeti(akis);
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException
                                     or NotSupportedException or ArgumentException)
        {
            return null;
        }
    }
}
