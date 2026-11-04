using System.Buffers.Binary;
using System.Text;

namespace HapiScanner.Core.Adli;

public static class UsnKayitCozumleyici
{

    private const uint DataOverwrite = 0x00000001;
    private const uint DataExtend = 0x00000002;
    private const uint DataTruncation = 0x00000004;
    private const uint FileCreate = 0x00000100;
    private const uint FileDelete = 0x00000200;
    private const uint RenameOldName = 0x00001000;
    private const uint RenameNewName = 0x00002000;

    public static UsnOlayi OlayCoz(uint reason)
    {
        var olay = UsnOlayi.Yok;
        if ((reason & FileCreate) != 0) olay |= UsnOlayi.Olusturma;
        if ((reason & FileDelete) != 0) olay |= UsnOlayi.Silme;
        if ((reason & RenameOldName) != 0) olay |= UsnOlayi.YenidenAdlandirmaEski;
        if ((reason & RenameNewName) != 0) olay |= UsnOlayi.YenidenAdlandirmaYeni;
        if ((reason & (DataOverwrite | DataExtend | DataTruncation)) != 0) olay |= UsnOlayi.VeriDegisimi;
        if (olay == UsnOlayi.Yok && reason != 0) olay = UsnOlayi.Diger;
        return olay;
    }

    public static IReadOnlyList<UsnKaydi> Coz(ReadOnlySpan<byte> tampon, int azamiKayit = 500_000)
    {
        var kayitlar = new List<UsnKaydi>();
        var konum = 0;

        while (konum + 60 <= tampon.Length && kayitlar.Count < azamiKayit)
        {
            var kayitUzunlugu = (int)BinaryPrimitives.ReadUInt32LittleEndian(tampon[konum..]);
            if (kayitUzunlugu < 60 || konum + kayitUzunlugu > tampon.Length) break;

            var kayit = tampon.Slice(konum, kayitUzunlugu);
            var surum = BinaryPrimitives.ReadUInt16LittleEndian(kayit[4..]);

            var cozulen = surum switch
            {
                2 => V2Coz(kayit),
                3 => V3Coz(kayit),
                _ => null,
            };
            if (cozulen is not null) kayitlar.Add(cozulen);

            konum += kayitUzunlugu;
        }

        return kayitlar;
    }

    private static UsnKaydi? V2Coz(ReadOnlySpan<byte> k)
    {
        if (k.Length < 60) return null;
        var dosyaRef = BinaryPrimitives.ReadUInt64LittleEndian(k[8..]);
        var ustRef = BinaryPrimitives.ReadUInt64LittleEndian(k[16..]);
        var usn = BinaryPrimitives.ReadInt64LittleEndian(k[24..]);
        var zaman = BinaryPrimitives.ReadInt64LittleEndian(k[32..]);
        var reason = BinaryPrimitives.ReadUInt32LittleEndian(k[40..]);
        var adUzunlugu = BinaryPrimitives.ReadUInt16LittleEndian(k[56..]);
        var adKonumu = BinaryPrimitives.ReadUInt16LittleEndian(k[58..]);
        return Olustur(k, usn, zaman, reason, dosyaRef, ustRef, adKonumu, adUzunlugu);
    }

    private static UsnKaydi? V3Coz(ReadOnlySpan<byte> k)
    {
        if (k.Length < 76) return null;

        var dosyaRef = BinaryPrimitives.ReadUInt64LittleEndian(k[8..]);
        var ustRef = BinaryPrimitives.ReadUInt64LittleEndian(k[24..]);
        var usn = BinaryPrimitives.ReadInt64LittleEndian(k[40..]);
        var zaman = BinaryPrimitives.ReadInt64LittleEndian(k[48..]);
        var reason = BinaryPrimitives.ReadUInt32LittleEndian(k[56..]);
        var adUzunlugu = BinaryPrimitives.ReadUInt16LittleEndian(k[72..]);
        var adKonumu = BinaryPrimitives.ReadUInt16LittleEndian(k[74..]);
        return Olustur(k, usn, zaman, reason, dosyaRef, ustRef, adKonumu, adUzunlugu);
    }

    private static UsnKaydi? Olustur(
        ReadOnlySpan<byte> kayit, long usn, long filetime, uint reason,
        ulong dosyaRef, ulong ustRef, int adKonumu, int adUzunlugu)
    {
        if (adKonumu < 0 || adUzunlugu < 0) return null;
        if (adKonumu + adUzunlugu > kayit.Length) return null;

        var ad = adUzunlugu == 0
            ? string.Empty
            : Encoding.Unicode.GetString(kayit.Slice(adKonumu, adUzunlugu));

        return new UsnKaydi(usn, ad, OlayCoz(reason), ZamanCoz(filetime), dosyaRef, ustRef);
    }

    public static DateTimeOffset ZamanCoz(long filetime)
    {
        if (filetime <= 0) return DateTimeOffset.UnixEpoch;
        try
        {
            return DateTimeOffset.FromFileTime(filetime);
        }
        catch (ArgumentOutOfRangeException)
        {
            return DateTimeOffset.UnixEpoch;
        }
    }
}
