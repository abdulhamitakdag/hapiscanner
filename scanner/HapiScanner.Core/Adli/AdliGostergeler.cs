using HapiScanner.Core.Tespit;

namespace HapiScanner.Core.Adli;

public static class AdliGostergeler
{
    public static GostergeSeti Doldur(GecmisIz iz, GostergeSeti? mevcut = null)
    {
        var set = mevcut ?? new GostergeSeti { OrtukKimlik = iz.OrtukKimlik };

        set.Ekle(GostergeTipi.DosyaAdi, iz.Ad);

        foreach (var tur in iz.KanitTurleri()) set.Ekle(GostergeTipi.UsnOlayi, tur);

        if (!iz.SuAndaVarMi && iz.SilinmeGorulduMu)
            set.Ekle(GostergeTipi.MftKaniti, "DELETED_BUT_RECORDED");

        if (iz.YenidenAdlandirmaGorulduMu)
            set.Ekle(GostergeTipi.MftKaniti, "RENAMED");

        return set;
    }

    public static IReadOnlyList<GostergeSeti> Setler(IEnumerable<GecmisIz> izler)
        => izler.Select(iz => Doldur(iz)).ToList();
}
