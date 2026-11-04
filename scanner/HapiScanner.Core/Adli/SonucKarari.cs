using HapiScanner.Core.Tespit;

namespace HapiScanner.Core.Adli;

public static class Sonuc
{
    public const string BilinenKanitYok = "NO_KNOWN_ARTIFACTS";
    public const string Isaretli = "FLAGGED";
    public const string Supheli = "SUSPICIOUS";
    public const string Kararsiz = "INCONCLUSIVE";
    public const string AdliBozulma = "FORENSICS_DEGRADED";
    public const string SinirliGecmis = "LIMITED_HISTORICAL_COVERAGE";
    public const string TaramaBasarisiz = "SCAN_FAILED";
}

public static class SonucKarari
{

    public static string Belirle(IReadOnlyList<Eslesme> eslesmeler, AdliKapsam kapsam)
    {
        if (eslesmeler.Any(e => e.IsaretlenirMi)) return Sonuc.Isaretli;
        if (eslesmeler.Count > 0) return Sonuc.Supheli;

        if (kapsam.Genel == Kapsam.Yok) return Sonuc.Kararsiz;
        if (kapsam.AdliBozulmaVar) return Sonuc.AdliBozulma;
        if (!kapsam.GecmisGuvenilirMi) return Sonuc.SinirliGecmis;

        return Sonuc.BilinenKanitYok;
    }

    public static string Aciklama(string sonuc, DateTimeOffset an) => sonuc switch
    {
        Sonuc.BilinenKanitYok =>
            $"{an:yyyy-MM-dd HH:mm} UTC itibariyla bilinen hile kaniti bulunamadi.",
        Sonuc.Isaretli =>
            "Guclu kanit bulundu, yetkili incelemesi gerekiyor.",
        Sonuc.Supheli =>
            "Zayif veya orta duzeyde kanit bulundu, yetkili incelemesi gerekiyor.",
        Sonuc.SinirliGecmis =>
            "Bilinen hile kaniti bulunamadi, ancak dosya sistemi gecmisi tam incelenemedi; " +
            "gecmiste hile kullanilmadigi sonucu cikarilamaz.",
        Sonuc.AdliBozulma =>
            "Dosya sistemi gecmis kaydi (USN journal) beklenmedik bicimde eksik veya sifirlanmis. " +
            "Bu tek basina hile kaniti degildir, ancak gecmis incelemesi guvenilir degildir.",
        Sonuc.Kararsiz =>
            "Tarama kapsami sonuc cikarmaya yetmedi.",
        _ => "Tarama tamamlanamadi.",
    };
}
