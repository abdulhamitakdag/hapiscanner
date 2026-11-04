using HapiScanner.Core.Tespit;

namespace HapiScanner.Core.Runtime;

public static class RuntimeGostergeleri
{
    public static GostergeSeti Doldur(RuntimeAnalizSonucu sonuc, GostergeSeti? mevcut = null)
    {
        var set = mevcut ?? new GostergeSeti
        {
            OrtukKimlik = sonuc.Hedef?.OrtukKimlik() ?? string.Empty,
        };

        foreach (var anomali in sonuc.Anomaliler)
        {
            set.Ekle(GostergeTipi.RuntimeAnomalisi, anomali.Tur.ToString());

            switch (anomali.Tur)
            {
                case AnomaliTuru.JavaAjani:
                case AnomaliTuru.NativeAjan:
                    set.Ekle(GostergeTipi.JvmAjani, anomali.Ad);
                    break;

                case AnomaliTuru.HariciProcessIliskisi:
                    set.Ekle(GostergeTipi.ProcessIliskisi, anomali.Ad);
                    break;

                case AnomaliTuru.KullaniciYazilabilirYoldanModul:
                case AnomaliTuru.ImzasizModul:
                    set.Ekle(GostergeTipi.RuntimeModul, anomali.Ad);
                    break;
            }
        }

        return set;
    }
}
