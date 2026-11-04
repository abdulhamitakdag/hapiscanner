using System.IO.Compression;
using System.Text;

namespace HapiScanner.Core.Bicim;

public sealed record JarIcerigi(
    bool GecerliArsiv,
    IReadOnlyList<string> SinifAdlari,
    IReadOnlyList<string> PaketAdlari,
    IReadOnlyDictionary<string, string> Manifest,
    IReadOnlyList<string> ModKimlikleri,
    IReadOnlyList<string> Kaynaklar,
    int GirdiSayisi,
    string? Hata = null)
{
    public static JarIcerigi Bozuk(string hata) => new(
        false, Array.Empty<string>(), Array.Empty<string>(),
        new Dictionary<string, string>(), Array.Empty<string>(),
        Array.Empty<string>(), 0, hata);
}

public static class JarOkuyucu
{

    private const int AzamiGirdi = 50_000;
    private const long AzamiManifestBaytı = 1024 * 1024;
    private const int AzamiSinifKaydi = 20_000;

    public static JarIcerigi Oku(string yol)
    {
        try
        {
            using var akis = new FileStream(yol, FileMode.Open, FileAccess.Read,
                                            FileShare.ReadWrite | FileShare.Delete);
            return Oku(akis);
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException
                                     or ArgumentException or NotSupportedException)
        {
            return JarIcerigi.Bozuk(hata.GetType().Name);
        }
    }

    public static JarIcerigi Oku(Stream akis)
    {
        try
        {
            using var arsiv = new ZipArchive(akis, ZipArchiveMode.Read, leaveOpen: true);

            var siniflar = new List<string>();
            var paketler = new HashSet<string>(StringComparer.Ordinal);
            var kaynaklar = new List<string>();
            var manifest = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var modlar = new List<string>();
            var sayac = 0;

            foreach (var girdi in arsiv.Entries)
            {
                if (++sayac > AzamiGirdi) break;
                var ad = girdi.FullName;

                if (ad.EndsWith(".class", StringComparison.OrdinalIgnoreCase))
                {
                    if (siniflar.Count < AzamiSinifKaydi)
                    {
                        var sinif = ad[..^6].Replace('/', '.');
                        siniflar.Add(sinif);
                        var son = sinif.LastIndexOf('.');
                        if (son > 0) paketler.Add(sinif[..son]);
                    }
                    continue;
                }

                if (ad.Equals("META-INF/MANIFEST.MF", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var (anahtar, deger) in ManifestCoz(girdi))
                        manifest[anahtar] = deger;
                    continue;
                }

                if (ad.Equals("fabric.mod.json", StringComparison.OrdinalIgnoreCase)
                    || ad.Equals("quilt.mod.json", StringComparison.OrdinalIgnoreCase)
                    || ad.Equals("META-INF/mods.toml", StringComparison.OrdinalIgnoreCase)
                    || ad.Equals("mcmod.info", StringComparison.OrdinalIgnoreCase))
                {
                    var kimlik = ModKimligiCoz(girdi, ad);
                    if (kimlik is not null) modlar.Add(kimlik);
                    kaynaklar.Add(ad);
                    continue;
                }

                if (!ad.EndsWith('/')) kaynaklar.Add(ad);
            }

            return new JarIcerigi(true, siniflar, paketler.ToList(), manifest,
                                  modlar, kaynaklar, sayac);
        }
        catch (InvalidDataException)
        {

            return JarIcerigi.Bozuk("InvalidDataException");
        }
        catch (Exception hata) when (hata is IOException or NotSupportedException
                                     or ArgumentException or OutOfMemoryException)
        {
            return JarIcerigi.Bozuk(hata.GetType().Name);
        }
    }

    private static IEnumerable<(string, string)> ManifestCoz(ZipArchiveEntry girdi)
    {
        string metin;
        try
        {
            if (girdi.Length > AzamiManifestBaytı) yield break;
            using var akis = girdi.Open();
            using var okuyucu = new StreamReader(akis, Encoding.UTF8);
            metin = okuyucu.ReadToEnd();
        }
        catch (Exception hata) when (hata is InvalidDataException or IOException
                                     or NotSupportedException)
        {
            yield break;
        }

        foreach (var satir in metin.Split('\n'))
        {
            var temiz = satir.TrimEnd('\r');
            if (temiz.Length == 0 || temiz.StartsWith(' ')) continue;
            var ayirac = temiz.IndexOf(':');
            if (ayirac <= 0) continue;
            yield return (temiz[..ayirac].Trim(), temiz[(ayirac + 1)..].Trim());
        }
    }

    private static string? ModKimligiCoz(ZipArchiveEntry girdi, string ad)
    {
        try
        {
            if (girdi.Length > AzamiManifestBaytı) return null;
            using var akis = girdi.Open();
            using var okuyucu = new StreamReader(akis);
            var metin = okuyucu.ReadToEnd();

            if (ad.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                using var belge = System.Text.Json.JsonDocument.Parse(metin);
                if (belge.RootElement.TryGetProperty("id", out var kimlik))
                    return kimlik.GetString();
                return null;
            }

            foreach (var satir in metin.Split('\n'))
            {
                var temiz = satir.Trim();
                if (!temiz.StartsWith("modId", StringComparison.OrdinalIgnoreCase)) continue;
                var esit = temiz.IndexOf('=');
                if (esit < 0) continue;
                return temiz[(esit + 1)..].Trim().Trim('"', '\'', ',');
            }
            return null;
        }
        catch (Exception hata) when (hata is System.Text.Json.JsonException
                                     or InvalidDataException or IOException
                                     or NotSupportedException or ArgumentException)
        {
            return null;
        }
    }
}
