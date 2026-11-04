using HapiScanner.Core.Arayuz;

namespace HapiScanner.Core.Model3B;

public enum Esya
{
    Yok,
    Kilit,
    Buyutec,
    Dosya,
    Kalkan,
    Sunucu,
    Onay,
    Uyari,
}

public enum AnimasyonTuru
{
    Karsilama,
    Gizlilik,
    Kod,
    Baglaniyor,
    AsamaHazirlik,
    AsamaKuralPaketi,
    AsamaBaglanti,
    AsamaDosyaSistemi,
    AsamaAdli,
    AsamaRuntime,
    AsamaRapor,
    SonucIyi,
    SonucUyari,
    SonucKotu,
    Hata,
}

public sealed record AnimasyonTanimi(
    AnimasyonTuru Tur,
    Esya Esya,
    double Hiz,
    string Aciklama,

    double Yakinlik = 0);

public static class Animasyonlar
{

    public const double GecisSuresi = 0.45;

    private static readonly Dictionary<AnimasyonTuru, AnimasyonTanimi> Tanimlar = new()
    {
        [AnimasyonTuru.Karsilama] = new(AnimasyonTuru.Karsilama, Esya.Yok, 1.0, "el sallıyor"),
        [AnimasyonTuru.Gizlilik] = new(AnimasyonTuru.Gizlilik, Esya.Kilit, 0.8, "gizlilik metnini tutuyor"),
        [AnimasyonTuru.Kod] = new(AnimasyonTuru.Kod, Esya.Yok, 1.6, "kod yazıyor", 0.35),
        [AnimasyonTuru.Baglaniyor] = new(AnimasyonTuru.Baglaniyor, Esya.Sunucu, 0.9, "sunucuya bağlanıyor"),
        [AnimasyonTuru.AsamaHazirlik] = new(AnimasyonTuru.AsamaHazirlik, Esya.Yok, 1.2, "hazırlanıyor"),
        [AnimasyonTuru.AsamaKuralPaketi] = new(AnimasyonTuru.AsamaKuralPaketi, Esya.Kalkan, 1.0, "kural paketi imzasını kontrol ediyor"),
        [AnimasyonTuru.AsamaBaglanti] = new(AnimasyonTuru.AsamaBaglanti, Esya.Sunucu, 0.9, "oyun bağlantısını işaret ediyor"),
        [AnimasyonTuru.AsamaDosyaSistemi] = new(AnimasyonTuru.AsamaDosyaSistemi, Esya.Buyutec, 1.5, "dosyaları tarıyor"),
        [AnimasyonTuru.AsamaAdli] = new(AnimasyonTuru.AsamaAdli, Esya.Dosya, 0.7, "silinmiş izleri okuyor"),
        [AnimasyonTuru.AsamaRuntime] = new(AnimasyonTuru.AsamaRuntime, Esya.Kalkan, 1.0, "çalışan modülleri inceliyor"),
        [AnimasyonTuru.AsamaRapor] = new(AnimasyonTuru.AsamaRapor, Esya.Onay, 1.3, "raporu imzalıyor"),
        [AnimasyonTuru.SonucIyi] = new(AnimasyonTuru.SonucIyi, Esya.Onay, 1.1, "kutluyor"),
        [AnimasyonTuru.SonucUyari] = new(AnimasyonTuru.SonucUyari, Esya.Uyari, 0.8, "sonucu gösteriyor"),
        [AnimasyonTuru.SonucKotu] = new(AnimasyonTuru.SonucKotu, Esya.Uyari, 0.6, "durgun"),
        [AnimasyonTuru.Hata] = new(AnimasyonTuru.Hata, Esya.Yok, 0.6, "omuzları düşük"),
    };

    public static AnimasyonTanimi Tanim(AnimasyonTuru tur) => Tanimlar[tur];

    public static AnimasyonTuru Sec(Ekran ekran, Asama? asama, string? sonucTonu)
        => ekran switch
        {
            Ekran.Karsilama => AnimasyonTuru.Karsilama,
            Ekran.Gizlilik => AnimasyonTuru.Gizlilik,
            Ekran.Kod => AnimasyonTuru.Kod,
            Ekran.Baglaniyor => AnimasyonTuru.Baglaniyor,
            Ekran.Tarama => AsamaAnimasyonu(asama),
            Ekran.Sonuc => sonucTonu switch
            {
                "iyi" => AnimasyonTuru.SonucIyi,
                "kotu" => AnimasyonTuru.SonucKotu,
                "uyari" => AnimasyonTuru.SonucUyari,
                _ => AnimasyonTuru.SonucUyari,
            },
            Ekran.Hata => AnimasyonTuru.Hata,
            _ => AnimasyonTuru.Karsilama,
        };

    private static AnimasyonTuru AsamaAnimasyonu(Asama? asama) => asama?.Protokol switch
    {
        "init" => AnimasyonTuru.AsamaHazirlik,
        "ruleset" => AnimasyonTuru.AsamaKuralPaketi,
        "connection" => AnimasyonTuru.AsamaBaglanti,
        "filesystem" => AnimasyonTuru.AsamaDosyaSistemi,
        "forensics" => AnimasyonTuru.AsamaAdli,
        "runtime" => AnimasyonTuru.AsamaRuntime,
        "report" => AnimasyonTuru.AsamaRapor,
        _ => AnimasyonTuru.Baglaniyor,
    };

    private static double Sin(double t, double periyot, double faz = 0)
        => Math.Sin((t / periyot + faz) * 2 * Math.PI);

    private static double Nefes(double t) => Sin(t, 4.0) * 0.18;

    private static double Gidis(double t, double periyot, double faz = 0)
        => (1 - Math.Cos((t / periyot + faz) * 2 * Math.PI)) / 2;

    // Selamlama birkac saniye surer, sonra kol yavasca yana iner.
    private const double SelamSuresi = 2.7;
    private const double SelamSonusu = 1.1;

    private static double SelamAgirligi(double t)
    {
        if (t <= SelamSuresi) return 1;
        var oran = Math.Clamp((t - SelamSuresi) / SelamSonusu, 0, 1);
        return (1 + Math.Cos(oran * Math.PI)) / 2;
    }

    public static Poz Hesapla(AnimasyonTuru tur, double saniye, double ilerleme = 0)
    {
        var t = saniye * Tanim(tur).Hiz;
        var nefes = Nefes(t);
        var oran = Math.Clamp(ilerleme / 100.0, 0, 1);

        var poz = tur switch
        {

            AnimasyonTuru.Karsilama => new Poz(
                Bas: new Donus(X: -4 + Sin(t, 3.0) * 3, Y: Sin(t, 5.0) * 7),
                Govde: new Donus(Y: Sin(t, 5.0) * 3),
                SagKol: new Donus(
                    Z: -4 + (-111 - Sin(t, 0.9) * 22) * SelamAgirligi(t),
                    X: -6 * SelamAgirligi(t) + Sin(t, 3.0) * 4 * (1 - SelamAgirligi(t))),
                SolKol: new Donus(X: Sin(t, 3.0) * 4, Z: 4),
                SagBacak: new Donus(X: Sin(t, 3.0) * 2),
                SolBacak: new Donus(X: -Sin(t, 3.0) * 2),
                Pelerin: new Donus(X: 8 + Sin(t, 3.0) * 3),
                Yukseklik: nefes,
                ModelDonusu: -14,
                EsyaDonusu: 0),

            AnimasyonTuru.Gizlilik => new Poz(
                Bas: new Donus(X: 22, Y: Sin(t, 2.4) * 9),
                Govde: new Donus(X: 4),
                SagKol: new Donus(X: -72, Z: -12),
                SolKol: new Donus(X: -72, Z: 12),
                Pelerin: new Donus(X: 6),
                Yukseklik: nefes,
                ModelDonusu: -10,
                EsyaDonusu: Sin(t, 6.0) * 4),

            AnimasyonTuru.Kod => new Poz(
                Bas: new Donus(X: 16 - Gidis(t, 9.0) * 16, Y: Sin(t, 7.0) * 5),
                Govde: new Donus(X: 6),
                SagKol: new Donus(X: -82 + Sin(t, 0.4) * 7, Z: -16),
                SolKol: new Donus(X: -82 + Sin(t, 0.4, 0.5) * 7, Z: 16),
                Yukseklik: nefes * 0.5,
                ModelDonusu: -8,
                EsyaGorunurlugu: 0),

            AnimasyonTuru.Baglaniyor => new Poz(
                Bas: new Donus(Y: Sin(t, 4.0) * 24, X: -3),
                Govde: new Donus(Y: Sin(t, 4.0) * 6, Z: Sin(t, 6.0) * 2),
                SagKol: new Donus(X: Sin(t, 4.0) * 7, Z: -5),
                SolKol: new Donus(X: -Sin(t, 4.0) * 7, Z: 5),
                Pelerin: new Donus(X: 5),
                Yukseklik: nefes,
                ModelDonusu: Sin(t, 8.0) * 12,
                EsyaDonusu: t * 40),

            AnimasyonTuru.AsamaHazirlik => new Poz(
                Bas: new Donus(X: -8 + Sin(t, 1.4) * 4),
                Govde: new Donus(X: -2),
                SagKol: new Donus(X: -30 - Gidis(t, 1.4) * 120, Z: -8),
                SolKol: new Donus(X: -30 - Gidis(t, 1.4, 0.5) * 120, Z: 8),
                SagBacak: new Donus(X: 2),
                SolBacak: new Donus(X: -2),
                Yukseklik: nefes + Gidis(t, 1.4) * 0.5,
                ModelDonusu: -12,
                EsyaGorunurlugu: 0),

            AnimasyonTuru.AsamaKuralPaketi => new Poz(
                Bas: new Donus(X: 14, Y: Sin(t, 2.0) * 6),
                SagKol: new Donus(X: -95 + Sin(t, 1.2) * 18, Z: -22),
                SolKol: new Donus(X: -78, Z: 20),
                Govde: new Donus(X: 3, Y: 6),
                Pelerin: new Donus(X: 6),
                Yukseklik: nefes,
                ModelDonusu: -16,
                EsyaDonusu: Sin(t, 1.2) * 12),

            AnimasyonTuru.AsamaBaglanti => new Poz(
                Bas: new Donus(Y: Sin(t, 3.2) * 28),
                Govde: new Donus(Y: Sin(t, 3.2) * 8),
                SagKol: new Donus(X: -92, Z: -14 + Sin(t, 3.2) * 10),
                SolKol: new Donus(X: Sin(t, 3.2) * 5, Z: 6),
                Pelerin: new Donus(X: 5),
                Yukseklik: nefes,
                ModelDonusu: -10,
                EsyaDonusu: Sin(t, 3.2) * 18),

            AnimasyonTuru.AsamaDosyaSistemi => new Poz(
                Bas: new Donus(X: 26, Y: Sin(t * (1 + oran * 0.6), 1.1) * 12),
                Govde: new Donus(X: 12, Y: 8),
                SagKol: new Donus(
                    X: -60 - Gidis(t * (1 + oran * 0.6), 0.75) * 55,
                    Z: -18),
                SolKol: new Donus(X: -20, Z: 14),
                SagBacak: new Donus(X: -4),
                SolBacak: new Donus(X: 4),
                Pelerin: new Donus(X: 14),
                Yukseklik: nefes * 0.6,
                ModelDonusu: -18,
                EsyaDonusu: Sin(t * (1 + oran * 0.6), 0.75) * 14),

            AnimasyonTuru.AsamaAdli => new Poz(
                Bas: new Donus(X: 34, Y: Sin(t, 5.0) * 16),
                Govde: new Donus(X: 26, Y: Sin(t, 5.0) * 4),
                SagKol: new Donus(X: -46 + Sin(t, 5.0) * 10, Z: -24),
                SolKol: new Donus(X: -18, Z: 16),
                SagBacak: new Donus(X: -6),
                SolBacak: new Donus(X: 6),
                Pelerin: new Donus(X: 26),
                Yukseklik: nefes * 0.5 - 0.6,
                ModelDonusu: -22,
                EsyaDonusu: Sin(t, 5.0) * 8),

            AnimasyonTuru.AsamaRuntime => new Poz(
                Bas: new Donus(X: 8, Y: Sin(t, 2.6) * 18),
                Govde: new Donus(X: 2),
                SagKol: new Donus(X: -100, Z: -10 + Sin(t, 2.6) * 8),
                SolKol: new Donus(X: -62, Z: 18),
                Pelerin: new Donus(X: 7),
                Yukseklik: nefes,
                ModelDonusu: Sin(t, 12.0) * 32,
                EsyaDonusu: t * 55),

            AnimasyonTuru.AsamaRapor => new Poz(
                Bas: new Donus(X: 18, Y: Sin(t, 1.6) * 8),
                Govde: new Donus(X: 5, Y: 4),
                SagKol: new Donus(
                    X: -88 + Sin(t, 1.6) * 12,
                    Z: -14 + Sin(t, 0.8) * 14),
                SolKol: new Donus(X: -70, Z: 18),
                Pelerin: new Donus(X: 6),
                Yukseklik: nefes,
                ModelDonusu: -14,
                EsyaDonusu: Sin(t, 0.8) * 20),

            AnimasyonTuru.SonucIyi => new Poz(
                Bas: new Donus(X: -12 + Sin(t, 1.3) * 5),
                Govde: new Donus(Y: Sin(t, 2.6) * 5),
                SagKol: new Donus(Z: -150 - Gidis(t, 1.3) * 25),
                SolKol: new Donus(Z: 150 + Gidis(t, 1.3) * 25),
                SagBacak: new Donus(X: -Gidis(t, 1.3) * 14),
                SolBacak: new Donus(X: Gidis(t, 1.3) * 14),
                Pelerin: new Donus(X: 10 + Gidis(t, 1.3) * 16),
                Yukseklik: Math.Max(0, Math.Sin((t / 1.3) * 2 * Math.PI)) * 2.4 + nefes,
                ModelDonusu: -10,
                EsyaDonusu: Sin(t, 1.3) * 25),

            AnimasyonTuru.SonucUyari => new Poz(
                Bas: new Donus(X: 6, Z: 10, Y: Sin(t, 5.0) * 8),
                Govde: new Donus(Z: Sin(t, 5.0) * 2),
                SagKol: new Donus(X: -42, Z: -34 + Sin(t, 2.5) * 8),
                SolKol: new Donus(X: -42, Z: 34 - Sin(t, 2.5) * 8),
                Pelerin: new Donus(X: 6),
                Yukseklik: nefes,
                ModelDonusu: -12,
                EsyaDonusu: Sin(t, 2.5) * 10),

            AnimasyonTuru.SonucKotu => new Poz(
                Bas: new Donus(X: 20, Y: 6),
                Govde: new Donus(X: 6),
                SagKol: new Donus(X: -8, Z: -3),
                SolKol: new Donus(X: -8, Z: 3),
                Pelerin: new Donus(X: 4),
                Yukseklik: nefes * 0.4,
                ModelDonusu: -8,
                EsyaDonusu: 0),

            AnimasyonTuru.Hata => new Poz(
                Bas: new Donus(X: 24, Z: -8, Y: Sin(t, 6.0) * 5),
                Govde: new Donus(X: 8, Z: Sin(t, 6.0) * 1.5),
                SagKol: new Donus(X: 10, Z: -2),
                SolKol: new Donus(X: 10, Z: 2),
                Pelerin: new Donus(X: 3),
                Yukseklik: nefes * 0.4 - 0.4,
                ModelDonusu: -6,
                EsyaGorunurlugu: 0),

            _ => Poz.Durus,
        };

        return Tanim(tur).Esya == Esya.Yok ? poz with { EsyaGorunurlugu = 0 } : poz;
    }
}
