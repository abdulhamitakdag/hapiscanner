using System.Buffers.Binary;
using System.Runtime.Versioning;
using HapiScanner.Core.Adli;

namespace HapiScanner.Windows;

public sealed class MftSaglayici : IMftSaglayici
{

    public int AzamiKayit { get; init; } = 200_000;

    public List<string> GorulenAdlar { get; } = new();

    public MftOkumaSonucu Oku(BirimBilgisi birim)
    {
        if (!birim.MftMumkunMu) return MftOkumaSonucu.Desteklenmiyor(birim.Kok);
        if (!OperatingSystem.IsWindows())
            return new MftOkumaSonucu(MftSonucTuru.Desteklenmiyor,
                Aciklama: "MFT yalnizca Windows uzerinde okunabilir");

        try
        {
            return WindowsOku(birim);
        }
        catch (Exception hata)
        {
            return new MftOkumaSonucu(MftSonucTuru.Hata,
                Aciklama: $"{birim.Kok}: MFT okunamadi ({hata.GetType().Name})");
        }
    }

    [SupportedOSPlatform("windows")]
    private MftOkumaSonucu WindowsOku(BirimBilgisi birim)
    {
        using var handle = Yerel.BirimAc(birim.Kok);
        if (handle.IsInvalid)
        {
            var hata = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            return hata == Yerel.ERROR_ACCESS_DENIED
                ? MftOkumaSonucu.ErisimYok(birim.Kok)
                : new MftOkumaSonucu(MftSonucTuru.Hata,
                    Aciklama: $"{birim.Kok}: birim acilamadi (Win32 {hata})");
        }

        var giris = new byte[24];
        var cikis = new byte[1 << 16];
        ulong sonrakiRef = 0;
        long sayac = 0;

        while (sayac < AzamiKayit)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(giris.AsSpan(0), sonrakiRef);
            BinaryPrimitives.WriteInt64LittleEndian(giris.AsSpan(8), 0);
            BinaryPrimitives.WriteInt64LittleEndian(giris.AsSpan(16), long.MaxValue);

            if (!Yerel.Ioctl(handle, Yerel.FSCTL_ENUM_USN_DATA, giris, cikis, out var donen, out var kod))
            {
                if (kod == Yerel.ERROR_HANDLE_EOF) break;
                if (sayac > 0) break;
                return kod switch
                {
                    Yerel.ERROR_ACCESS_DENIED => MftOkumaSonucu.ErisimYok(birim.Kok),
                    Yerel.ERROR_INVALID_FUNCTION or Yerel.ERROR_NOT_SUPPORTED =>
                        MftOkumaSonucu.Desteklenmiyor(birim.Kok),
                    _ => new MftOkumaSonucu(MftSonucTuru.Hata,
                        Aciklama: $"{birim.Kok}: MFT numaralandirma basarisiz (Win32 {kod})"),
                };
            }

            if (donen <= 8) break;

            sonrakiRef = BinaryPrimitives.ReadUInt64LittleEndian(cikis.AsSpan(0));
            var kayitlar = UsnKayitCozumleyici.Coz(cikis.AsSpan(8, donen - 8), AzamiKayit - (int)sayac);
            if (kayitlar.Count == 0) break;

            sayac += kayitlar.Count;
            foreach (var kayit in kayitlar)
            {
                if (UsnGecmisAnalizi.VarsayilanIlgiSuzgeci(kayit)) GorulenAdlar.Add(kayit.Ad);
            }
        }

        return sayac > 0
            ? new MftOkumaSonucu(MftSonucTuru.Okundu, sayac)
            : new MftOkumaSonucu(MftSonucTuru.Hata, Aciklama: $"{birim.Kok}: MFT kaydi okunamadi");
    }
}
