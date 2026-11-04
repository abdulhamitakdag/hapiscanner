using HapiScanner.Core.Bicim;
using HapiScanner.Core.Dosya;

namespace HapiScanner.Core.Tespit;

public sealed class GostergeCikarici
{
    private readonly AdaySuzgeci _suzgec;

    public GostergeCikarici(AdaySuzgeci? suzgec = null) => _suzgec = suzgec ?? new AdaySuzgeci();

    public GostergeSeti Cikar(DosyaBilgisi dosya, Stream? icerik = null)
    {
        var set = new GostergeSeti { OrtukKimlik = dosya.OrtukKimlik() };

        set.Ekle(GostergeTipi.DosyaAdi, dosya.Ad);

        var ilkBaytlar = IlkBaytlar(dosya, icerik);
        var tur = TurBelirle(ilkBaytlar);

        switch (tur)
        {
            case DosyaTuru.Zip:
                JarIsle(set, dosya, icerik);
                break;
            case DosyaTuru.Pe:
                PeIsle(set, dosya, icerik);
                break;
        }

        if (_suzgec.HashlenebilirMi(dosya))
        {
            var ozet = IcerikOzeti(dosya, icerik);
            if (ozet is not null) set.Ekle(GostergeTipi.Sha256, ozet);
        }

        return set;
    }

    private enum DosyaTuru { Bilinmiyor, Zip, Pe }

    private static DosyaTuru TurBelirle(byte[] bas)
    {
        if (bas.Length >= 4 && bas[0] == 0x50 && bas[1] == 0x4B
            && (bas[2] == 0x03 || bas[2] == 0x05 || bas[2] == 0x07))
            return DosyaTuru.Zip;
        if (bas.Length >= 2 && bas[0] == 0x4D && bas[1] == 0x5A)
            return DosyaTuru.Pe;
        return DosyaTuru.Bilinmiyor;
    }

    private static byte[] IlkBaytlar(DosyaBilgisi dosya, Stream? icerik)
    {
        try
        {
            if (icerik is not null)
            {
                var tampon = new byte[4];
                icerik.Position = 0;
                var okunan = icerik.Read(tampon, 0, 4);
                icerik.Position = 0;
                return okunan < 4 ? tampon[..Math.Max(okunan, 0)] : tampon;
            }

            using var akis = new FileStream(dosya.Yol, FileMode.Open, FileAccess.Read,
                                            FileShare.ReadWrite | FileShare.Delete);
            var bas = new byte[4];
            var sayi = akis.Read(bas, 0, 4);
            return sayi < 4 ? bas[..Math.Max(sayi, 0)] : bas;
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException
                                     or ArgumentException or NotSupportedException)
        {
            return Array.Empty<byte>();
        }
    }

    private static void JarIsle(GostergeSeti set, DosyaBilgisi dosya, Stream? icerik)
    {
        var jar = icerik is not null ? JarOkuyucu.Oku(Sifirla(icerik)) : JarOkuyucu.Oku(dosya.Yol);
        if (!jar.GecerliArsiv) return;

        set.Ekle(GostergeTipi.JarMetadata, "VALID_ARCHIVE");
        set.Ekle(GostergeTipi.SinifAdi, jar.SinifAdlari);
        set.Ekle(GostergeTipi.PaketAdi, jar.PaketAdlari);
        set.Ekle(GostergeTipi.ModMetadata, jar.ModKimlikleri);
        set.Ekle(GostergeTipi.BilinenKaynak, jar.Kaynaklar);

        foreach (var (anahtar, deger) in jar.Manifest)
        {
            set.Ekle(GostergeTipi.ManifestIsareti, $"{anahtar}={deger}");
            set.Ekle(GostergeTipi.ManifestIsareti, deger);
        }

        if (jar.ModKimlikleri.Count > 0) set.Ekle(GostergeTipi.YapisalIsaret, "IS_MOD");
        if (jar.Manifest.ContainsKey("Premain-Class"))
            set.Ekle(GostergeTipi.YapisalIsaret, "JAVA_AGENT");
        if (jar.Manifest.ContainsKey("Agent-Class"))
            set.Ekle(GostergeTipi.YapisalIsaret, "JAVA_AGENT");
        if (jar.Kaynaklar.Any(k => k.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                                   || k.EndsWith(".so", StringComparison.OrdinalIgnoreCase)))
            set.Ekle(GostergeTipi.YapisalIsaret, "EMBEDDED_NATIVE");
        if (dosya.Uzanti is not (".jar" or ".zip" or ".litemod"))

            set.Ekle(GostergeTipi.YapisalIsaret, "EXTENSION_MISMATCH");
    }

    private static void PeIsle(GostergeSeti set, DosyaBilgisi dosya, Stream? icerik)
    {
        var pe = icerik is not null ? PeOkuyucu.Oku(Sifirla(icerik)) : PeOkuyucu.Oku(dosya.Yol);
        if (!pe.GecerliPe) return;

        set.Ekle(GostergeTipi.PeMetadata, pe.Dll ? "DLL" : "EXE");
        if (pe.Yonetilen) set.Ekle(GostergeTipi.PeMetadata, "MANAGED");
        if (pe.X64) set.Ekle(GostergeTipi.PeMetadata, "X64");
        if (pe.X86) set.Ekle(GostergeTipi.PeMetadata, "X86");
        set.Ekle(GostergeTipi.PeMetadata, pe.BolumAdlari.Select(b => $"SECTION:{b}"));

        if (dosya.Uzanti is not (".exe" or ".dll" or ".sys"))
            set.Ekle(GostergeTipi.YapisalIsaret, "EXTENSION_MISMATCH");

        if (pe.BolumAdlari.Any(b => b is "UPX0" or "UPX1" or ".vmp0" or ".themida"))
            set.Ekle(GostergeTipi.YapisalIsaret, "PACKED");
    }

    private static Stream Sifirla(Stream akis)
    {
        if (akis.CanSeek) akis.Position = 0;
        return akis;
    }

    private static string? IcerikOzeti(DosyaBilgisi dosya, Stream? icerik)
    {
        if (icerik is null) return Hashleme.DosyaOzeti(dosya.Yol);
        try
        {
            icerik.Position = 0;
            var ozet = Hashleme.AkisOzeti(icerik);
            if (icerik.CanSeek) icerik.Position = 0;
            return ozet;
        }
        catch (Exception hata) when (hata is IOException or NotSupportedException)
        {
            return null;
        }
    }
}
