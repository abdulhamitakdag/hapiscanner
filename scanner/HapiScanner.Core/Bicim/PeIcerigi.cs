using System.Buffers.Binary;
using System.Text;

namespace HapiScanner.Core.Bicim;

public sealed record PeIcerigi(
    bool GecerliPe,
    bool Dll,
    bool Yonetilen,
    ushort MakineTipi,
    DateTimeOffset? DerlemeZamani,
    IReadOnlyList<string> BolumAdlari,
    string? Hata = null)
{
    public static PeIcerigi Bozuk(string hata) => new(
        false, false, false, 0, null, Array.Empty<string>(), hata);

    public bool X64 => MakineTipi == 0x8664;
    public bool X86 => MakineTipi == 0x014c;
}

public static class PeOkuyucu
{
    private const int AzamiBolum = 96;

    public static PeIcerigi Oku(string yol)
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
            return PeIcerigi.Bozuk(hata.GetType().Name);
        }
    }

    public static PeIcerigi Oku(Stream akis)
    {
        try
        {

            var basliklar = new byte[0x400];
            akis.Position = 0;
            var okunan = Doldur(akis, basliklar);
            if (okunan < 0x40) return PeIcerigi.Bozuk("cok kisa");

            if (basliklar[0] != 0x4D || basliklar[1] != 0x5A)
                return PeIcerigi.Bozuk("MZ imzasi yok");

            var peOfseti = BinaryPrimitives.ReadInt32LittleEndian(basliklar.AsSpan(0x3C, 4));
            if (peOfseti <= 0 || peOfseti > okunan - 24)
                return PeIcerigi.Bozuk("gecersiz e_lfanew");

            if (basliklar[peOfseti] != 0x50 || basliklar[peOfseti + 1] != 0x45
                || basliklar[peOfseti + 2] != 0 || basliklar[peOfseti + 3] != 0)
                return PeIcerigi.Bozuk("PE imzasi yok");

            var coff = peOfseti + 4;
            var makine = BinaryPrimitives.ReadUInt16LittleEndian(basliklar.AsSpan(coff, 2));
            var bolumSayisi = BinaryPrimitives.ReadUInt16LittleEndian(basliklar.AsSpan(coff + 2, 2));
            var zamanDamgasi = BinaryPrimitives.ReadUInt32LittleEndian(basliklar.AsSpan(coff + 4, 4));
            var opsiyonelBoyut = BinaryPrimitives.ReadUInt16LittleEndian(basliklar.AsSpan(coff + 16, 2));
            var ozellikler = BinaryPrimitives.ReadUInt16LittleEndian(basliklar.AsSpan(coff + 18, 2));

            if (bolumSayisi > AzamiBolum) bolumSayisi = AzamiBolum;

            var dll = (ozellikler & 0x2000) != 0;

            var yonetilen = false;
            var opsiyonel = coff + 20;
            if (opsiyonelBoyut >= 0xE0 && opsiyonel + opsiyonelBoyut <= okunan)
            {
                var sihirli = BinaryPrimitives.ReadUInt16LittleEndian(basliklar.AsSpan(opsiyonel, 2));

                var cliOfseti = sihirli == 0x20b ? opsiyonel + 0xE8 : opsiyonel + 0xD8;
                if (cliOfseti + 8 <= okunan)
                {
                    var adres = BinaryPrimitives.ReadUInt32LittleEndian(basliklar.AsSpan(cliOfseti, 4));
                    yonetilen = adres != 0;
                }
            }

            var bolumler = new List<string>();
            var bolumBasi = opsiyonel + opsiyonelBoyut;
            for (var i = 0; i < bolumSayisi; i++)
            {
                var ofset = bolumBasi + i * 40;
                if (ofset + 8 > okunan) break;
                var ad = Encoding.ASCII.GetString(basliklar, ofset, 8).TrimEnd('\0', ' ');
                if (ad.Length > 0) bolumler.Add(ad);
            }

            DateTimeOffset? derleme = null;

            if (zamanDamgasi > 631152000 && zamanDamgasi < 4102444800)
                derleme = DateTimeOffset.FromUnixTimeSeconds(zamanDamgasi);

            return new PeIcerigi(true, dll, yonetilen, makine, derleme, bolumler);
        }
        catch (Exception hata) when (hata is IOException or NotSupportedException
                                     or ArgumentException or OutOfMemoryException)
        {
            return PeIcerigi.Bozuk(hata.GetType().Name);
        }
    }

    private static int Doldur(Stream akis, byte[] tampon)
    {
        var toplam = 0;
        while (toplam < tampon.Length)
        {
            var okunan = akis.Read(tampon, toplam, tampon.Length - toplam);
            if (okunan == 0) break;
            toplam += okunan;
        }
        return toplam;
    }
}
