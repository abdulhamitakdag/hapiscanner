using System.IO;
using System.Net.Http;
using HapiScanner.Core;
using HapiScanner.Core.Adli;
using HapiScanner.Core.Arayuz;
using HapiScanner.Core.Dosya;
using HapiScanner.Core.Runtime;
using HapiScanner.Core.Tarama;
using HapiScanner.Core.Tespit;
using HapiScanner.Windows;

namespace HapiScanner.App.Akis;

public static class AkisKurucu
{

    public static string SunucuAdresi(string[] argumanlar)
    {
        for (var i = 0; i < argumanlar.Length - 1; i++)
            if (argumanlar[i].Equals("--sunucu", StringComparison.OrdinalIgnoreCase))
                return argumanlar[i + 1];
        return Yayinci.VarsayilanSunucu;
    }

    public static ITaramaAkisi Kur(string[] argumanlar, out string? uyari)
    {
        uyari = null;

        var dizin = AppContext.BaseDirectory;
        var paketKaynagi = new DosyaKuralPaketiKaynagi(dizin);
        if (!paketKaynagi.VarMi)
        {
            uyari = "Kural paketi bulunamadı, tarama başlatılamaz.";
            return new HazirDegilAkisi();
        }

        if (!Uri.TryCreate(SunucuAdresi(argumanlar), UriKind.Absolute, out var adres))
        {
            uyari = "Sunucu adresi geçersiz.";
            return new HazirDegilAkisi();
        }

        var http = new HttpClient
        {
            BaseAddress = adres,
            Timeout = TimeSpan.FromSeconds(30),
        };

        var anahtarDeposu = new KaliciAnahtarDeposu(
            Path.Combine(VeriDizini(), "cihaz.key"), new DpapiKoruyucu());
        var anahtar = anahtarDeposu.Ac();

        var dosyaSistemi = new YerelDosyaSistemi();
        var kokler = OyunDizinleri.Kokler(dosyaSistemi);
        if (kokler.Count == 0)
            uyari = "Minecraft dizini bulunamadı; tarama kapsamı düşük olacak.";

        var istemci = new DogrulamaIstemcisi(http, anahtar, OkunanSurum(paketKaynagi));
        var yukleyici = new KuralPaketiYukleyici(
            Yayinci.PublicKeyler,
            new DosyaSurumDeposu(Path.Combine(VeriDizini(), "ruleset.surum")));

        return new GercekTaramaAkisi(
            istemci,
            paketKaynagi,
            yukleyici,
            new WindowsBaglantiBulucu(),
            (motor, surum) => new TaramaHatti(
                new DosyaKesfi(dosyaSistemi),
                dosyaSistemi,
                new AdliTarama(
                    new WindowsBirimSaglayici(),
                    new MftSaglayici(),
                    new UsnJournalSaglayici(),
                    new WindowsAyricalikSaglayici()),
                new RuntimeAnalizi(new WindowsProcessSaglayici()),
                motor,
                surum),
            kokler);
    }

    private static string VeriDizini() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HapiScanner");

    private static string OkunanSurum(DosyaKuralPaketiKaynagi kaynak)
    {
        try
        {
            var (paket, _) = kaynak.GetirAsync(CancellationToken.None).GetAwaiter().GetResult();
            return KuralMotoru.PaketCoz(paket).RulesetSurumu;
        }
        catch (Exception e) when (e is IOException or InvalidDataException
                                       or System.Text.Json.JsonException)
        {
            return string.Empty;
        }
    }
}
