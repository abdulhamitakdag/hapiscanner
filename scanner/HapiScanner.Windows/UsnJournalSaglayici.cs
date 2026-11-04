using System.Buffers.Binary;
using System.Runtime.Versioning;
using HapiScanner.Core.Adli;

namespace HapiScanner.Windows;

public sealed class UsnJournalSaglayici : IUsnSaglayici
{

    public int AzamiKayit { get; init; } = 300_000;

    private const uint IstenenOlaylar =
        Yerel.USN_REASON_FILE_CREATE | Yerel.USN_REASON_FILE_DELETE |
        Yerel.USN_REASON_RENAME_OLD_NAME | Yerel.USN_REASON_RENAME_NEW_NAME;

    public UsnOkumaSonucu Oku(BirimBilgisi birim)
    {
        if (!birim.UsnMumkunMu) return UsnOkumaSonucu.Desteklenmiyor(birim.Kok);
        if (!OperatingSystem.IsWindows())
            return new UsnOkumaSonucu(UsnSonucTuru.Desteklenmiyor,
                Aciklama: "USN journal yalnizca Windows uzerinde okunabilir");

        try
        {
            return WindowsOku(birim);
        }
        catch (Exception hata)
        {

            return new UsnOkumaSonucu(UsnSonucTuru.Hata,
                Aciklama: $"{birim.Kok}: USN okunamadi ({hata.GetType().Name})");
        }
    }

    [SupportedOSPlatform("windows")]
    private UsnOkumaSonucu WindowsOku(BirimBilgisi birim)
    {
        using var handle = Yerel.BirimAc(birim.Kok);
        if (handle.IsInvalid)
        {
            var hata = Marshal_GetLastWin32Error();
            return hata == Yerel.ERROR_ACCESS_DENIED
                ? UsnOkumaSonucu.ErisimYok(birim.Kok)
                : new UsnOkumaSonucu(UsnSonucTuru.Hata,
                    Aciklama: $"{birim.Kok}: birim acilamadi (Win32 {hata})");
        }

        var sorgu = new byte[56];
        if (!Yerel.Ioctl(handle, Yerel.FSCTL_QUERY_USN_JOURNAL, null, sorgu, out var donen, out var kod))
            return SorguHatasi(birim, kod);
        if (donen < 56)
            return new UsnOkumaSonucu(UsnSonucTuru.Hata, Aciklama: $"{birim.Kok}: journal cevabi kisa");

        var journalId = BinaryPrimitives.ReadUInt64LittleEndian(sorgu);
        var ilkUsn = BinaryPrimitives.ReadInt64LittleEndian(sorgu.AsSpan(8));
        var sonrakiUsn = BinaryPrimitives.ReadInt64LittleEndian(sorgu.AsSpan(16));
        var gecerliEnKucukUsn = BinaryPrimitives.ReadInt64LittleEndian(sorgu.AsSpan(24));
        var azamiBoyut = (long)BinaryPrimitives.ReadUInt64LittleEndian(sorgu.AsSpan(40));

        var baslangic = Math.Max(ilkUsn, gecerliEnKucukUsn);
        var kayitlar = KayitlariOku(handle, journalId, baslangic, out var okumaHatasi);

        if (okumaHatasi is { } hataKodu && kayitlar.Count == 0)
            return SorguHatasi(birim, hataKodu);

        var enEski = kayitlar.Count > 0
            ? kayitlar.Where(k => k.Zaman > DateTimeOffset.UnixEpoch)
                      .Select(k => k.Zaman)
                      .DefaultIfEmpty(DateTimeOffset.UnixEpoch)
                      .Min()
            : (DateTimeOffset?)null;
        if (enEski == DateTimeOffset.UnixEpoch) enEski = null;

        var journal = new UsnJournalDurumu(journalId, baslangic, sonrakiUsn, azamiBoyut, enEski);
        return new UsnOkumaSonucu(UsnSonucTuru.Okundu, journal, kayitlar);
    }

    [SupportedOSPlatform("windows")]
    private List<UsnKaydi> KayitlariOku(
        Microsoft.Win32.SafeHandles.SafeFileHandle handle, ulong journalId, long baslangic,
        out int? hataKodu)
    {
        hataKodu = null;
        var kayitlar = new List<UsnKaydi>();
        var cikis = new byte[1 << 16];
        var giris = new byte[40];
        var sonrakiUsn = baslangic;

        while (kayitlar.Count < AzamiKayit)
        {
            BinaryPrimitives.WriteInt64LittleEndian(giris.AsSpan(0), sonrakiUsn);
            BinaryPrimitives.WriteUInt32LittleEndian(giris.AsSpan(8), IstenenOlaylar);
            BinaryPrimitives.WriteUInt32LittleEndian(giris.AsSpan(12), 0);
            BinaryPrimitives.WriteUInt64LittleEndian(giris.AsSpan(16), 0);
            BinaryPrimitives.WriteUInt64LittleEndian(giris.AsSpan(24), 0);
            BinaryPrimitives.WriteUInt64LittleEndian(giris.AsSpan(32), journalId);

            if (!Yerel.Ioctl(handle, Yerel.FSCTL_READ_USN_JOURNAL, giris, cikis, out var donen, out var kod))
            {
                hataKodu = kod;
                break;
            }

            if (donen <= 8) break;

            sonrakiUsn = BinaryPrimitives.ReadInt64LittleEndian(cikis.AsSpan(0));
            var parca = UsnKayitCozumleyici.Coz(cikis.AsSpan(8, donen - 8), AzamiKayit - kayitlar.Count);
            if (parca.Count == 0) break;

            kayitlar.AddRange(parca);
        }

        return kayitlar;
    }

    private static UsnOkumaSonucu SorguHatasi(BirimBilgisi birim, int kod) => kod switch
    {
        Yerel.ERROR_ACCESS_DENIED => UsnOkumaSonucu.ErisimYok(birim.Kok),
        Yerel.ERROR_JOURNAL_NOT_ACTIVE or Yerel.ERROR_JOURNAL_DELETE_IN_PROGRESS =>
            new UsnOkumaSonucu(UsnSonucTuru.JournalYok,
                Aciklama: $"{birim.Kok}: USN journal etkin degil veya siliniyor"),
        Yerel.ERROR_JOURNAL_ENTRY_DELETED =>
            new UsnOkumaSonucu(UsnSonucTuru.JournalYok,
                Aciklama: $"{birim.Kok}: istenen USN kayitlari dondurulmus"),
        Yerel.ERROR_INVALID_FUNCTION or Yerel.ERROR_NOT_SUPPORTED =>
            UsnOkumaSonucu.Desteklenmiyor(birim.Kok),
        _ => new UsnOkumaSonucu(UsnSonucTuru.Hata, Aciklama: $"{birim.Kok}: USN sorgusu basarisiz (Win32 {kod})"),
    };

    private static int Marshal_GetLastWin32Error() => System.Runtime.InteropServices.Marshal.GetLastWin32Error();
}
