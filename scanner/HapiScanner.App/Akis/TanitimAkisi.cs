using HapiScanner.Core;
using HapiScanner.Core.Adli;
using HapiScanner.Core.Arayuz;

namespace HapiScanner.App.Akis;

public sealed class TanitimAkisi : ITaramaAkisi
{
    private static readonly (Asama Asama, int Sure, string[] Notlar)[] Senaryo =
    {
        (Asama.Hazirlik, 600, new[] { "scanner sürümü 0.1.6", "cihaz anahtarı hazır" }),
        (Asama.KuralPaketi, 900, new[] { "kural paketi imzası doğrulandı", "sürüm 2026.11.03.1" }),
        (Asama.Baglanti, 1100, new[] { "Minecraft process'i bulundu", "HapiCraft bağlantısı doğrulandı" }),
        (Asama.DosyaSistemi, 2600, new[]
        {
            "mods dizini taranıyor", "48 aday dosya incelendi", "12 jar yapısal olarak açıldı",
        }),
        (Asama.Adli, 2200, new[] { "USN journal okundu", "son 90 gün kapsanıyor" }),
        (Asama.Runtime, 1400, new[] { "7 native modül incelendi", "tanımsız modül yok" }),
        (Asama.Rapor, 700, new[] { "rapor imzalandı" }),
    };

    public async Task<AkisSonucu> CalistirAsync(
        string token, IAsamaBildirici bildirici, CancellationToken iptal)
    {
        bildirici.Not("TANITIM MODU — gerçek tarama yapılmıyor");

        foreach (var (asama, sure, notlar) in Senaryo)
        {
            iptal.ThrowIfCancellationRequested();
            bildirici.Asamaya(asama);

            var baslangic = Asama.BitisYuzdesi(asama) - asama.Agirlik;
            var adim = sure / Math.Max(notlar.Length, 1);

            for (var i = 0; i < notlar.Length; i++)
            {
                await Task.Delay(adim, iptal).ConfigureAwait(false);
                bildirici.Not(notlar[i]);
                var oran = (i + 1.0) / notlar.Length;
                bildirici.Ilerleme((int)(baslangic + asama.Agirlik * oran));
            }
        }

        return new AkisSonucu(
            Sonuc.BilinenKanitYok, "GOOD", "GOOD", Array.Empty<KanitIstegi>());
    }
}

public sealed class HazirDegilAkisi : ITaramaAkisi
{
    public const string Mesaj =
        "Tarama baslatilamadi. Scanner'in guncel surumunu yeniden indir.";

    private readonly string _mesaj;

    public HazirDegilAkisi(string? mesaj = null) => _mesaj = mesaj ?? Mesaj;

    public Task<AkisSonucu> CalistirAsync(
        string token, IAsamaBildirici bildirici, CancellationToken iptal)
        => Task.FromResult(new AkisSonucu(
            Sonuc.TaramaBasarisiz, "UNAVAILABLE", "UNAVAILABLE",
            Array.Empty<KanitIstegi>(), _mesaj));
}
