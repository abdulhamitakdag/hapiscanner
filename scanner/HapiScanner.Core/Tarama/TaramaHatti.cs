using HapiScanner.Core.Adli;
using HapiScanner.Core.Arayuz;
using HapiScanner.Core.Dosya;
using HapiScanner.Core.Runtime;
using HapiScanner.Core.Tespit;

namespace HapiScanner.Core.Tarama;

public sealed record TaramaCiktisi(
    string Sonuc,
    string AdliKapsam,
    string RuntimeKapsam,
    IReadOnlyList<KanitIstegi> Kanitlar,
    IReadOnlyList<Eslesme> Eslesmeler,
    int IncelenenAday,
    int GezilenDizin,
    IReadOnlyList<string> Notlar);

public sealed class TaramaHatti
{
    private readonly DosyaKesfi _kesif;
    private readonly IDosyaSistemi _dosyaSistemi;
    private readonly GostergeCikarici _cikarici;
    private readonly AdliTarama _adli;
    private readonly RuntimeAnalizi _runtime;
    private readonly KuralMotoru _motor;
    private readonly string _rulesetSurumu;

    public TaramaHatti(
        DosyaKesfi kesif,
        IDosyaSistemi dosyaSistemi,
        AdliTarama adli,
        RuntimeAnalizi runtime,
        KuralMotoru motor,
        string rulesetSurumu,
        GostergeCikarici? cikarici = null)
    {
        _kesif = kesif;
        _dosyaSistemi = dosyaSistemi;
        _adli = adli;
        _runtime = runtime;
        _motor = motor;
        _rulesetSurumu = rulesetSurumu;
        _cikarici = cikarici ?? new GostergeCikarici();
    }

    public TaramaCiktisi Calistir(
        IEnumerable<TaramaKoku> kokler,
        int? hedefProcessId,
        IAsamaBildirici bildirici,
        IReadOnlyDictionary<string, ulong>? oncekiJournalIdleri = null,
        DateTimeOffset? simdi = null,
        CancellationToken iptal = default)
    {
        var an = simdi ?? DateTimeOffset.UtcNow;
        var bulgular = new List<Bulgu>();
        var notlar = new List<string>();

        bildirici.Asamaya(Asama.DosyaSistemi);
        var kesif = _kesif.Calistir(kokler, iptal: iptal);
        bildirici.Not($"{kesif.GezilenDizin} dizin gezildi, {kesif.Adaylar.Count} aday dosya");
        if (kesif.AtlananKokler.Count > 0)
            bildirici.Not($"{kesif.AtlananKokler.Count} dizin bulunamadı, atlandı");

        var mevcutAdlar = new HashSet<string>(
            kesif.Adaylar.Select(a => a.Dosya.Ad), StringComparer.OrdinalIgnoreCase);

        var incelenen = 0;
        foreach (var aday in kesif.Adaylar)
        {
            iptal.ThrowIfCancellationRequested();
            incelenen++;

            if (incelenen % 50 == 0)
                bildirici.Ilerleme(Yuzde(Asama.DosyaSistemi, incelenen, kesif.Adaylar.Count));

            using var icerik = _dosyaSistemi.Ac(aday.Dosya.Yol);
            GostergeSeti set;
            try
            {
                set = _cikarici.Cikar(aday.Dosya, icerik);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {

                continue;
            }

            if (_motor.IzinliMi(set)) continue;

            foreach (var eslesme in _motor.Degerlendir(set))
                bulgular.Add(new Bulgu(eslesme, KanitTuru.MevcutDosya, SuAndaVar: true,
                                       IlkGorulme: aday.Dosya.Olusturma,
                                       SonGorulme: aday.Dosya.Degisim));
        }
        bildirici.Ilerleme(Asama.BitisYuzdesi(Asama.DosyaSistemi));

        bildirici.Asamaya(Asama.Adli);
        iptal.ThrowIfCancellationRequested();

        var runtimeSonucu = _runtime.Calistir(hedefProcessId);

        var adliSonuc = _adli.Calistir(oncekiJournalIdleri, mevcutAdlar,
                                       runtimeSonucu.Erisim, an);
        bildirici.Not($"{adliSonuc.Izler.Count} geçmiş iz, kapsam {adliSonuc.Kapsam.ProtokolDegeri}");
        foreach (var satir in adliSonuc.Kapsam.Ozet()) notlar.Add(satir);

        foreach (var set in AdliGostergeler.Setler(adliSonuc.Izler))
        {
            if (_motor.IzinliMi(set)) continue;
            var iz = adliSonuc.Izler.First(i => i.OrtukKimlik == set.OrtukKimlik);
            foreach (var eslesme in _motor.Degerlendir(set))
                bulgular.Add(new Bulgu(eslesme, KanitTuru.GecmisKayit, iz.SuAndaVarMi,
                                       iz.IlkGorulme, iz.SonGorulme));
        }
        bildirici.Ilerleme(Asama.BitisYuzdesi(Asama.Adli));

        bildirici.Asamaya(Asama.Runtime);
        iptal.ThrowIfCancellationRequested();

        bildirici.Not($"{runtimeSonucu.IncelenenModulSayisi} modül incelendi, " +
                      $"{runtimeSonucu.Anomaliler.Count} anomali");
        notlar.AddRange(runtimeSonucu.Notlar);

        var runtimeSeti = RuntimeGostergeleri.Doldur(runtimeSonucu);
        if (!_motor.IzinliMi(runtimeSeti))
            foreach (var eslesme in _motor.Degerlendir(runtimeSeti))
                bulgular.Add(new Bulgu(eslesme, KanitTuru.Runtime, SuAndaVar: true,
                                       SonGorulme: an));
        bildirici.Ilerleme(Asama.BitisYuzdesi(Asama.Runtime));

        var eslesmeler = bulgular.Select(b => b.Eslesme).ToList();
        var sonuc = SonucKarari.Belirle(eslesmeler, adliSonuc.Kapsam);

        if (!kesif.KapsamVarMi && sonuc == Sonuc.BilinenKanitYok)
        {
            sonuc = Sonuc.Kararsiz;
            notlar.Add("Taranacak dizin bulunamadı, dosya sistemi kapsamı yok.");
        }

        return new TaramaCiktisi(
            sonuc,
            adliSonuc.Kapsam.ProtokolDegeri,
            adliSonuc.Kapsam.RuntimeProtokolDegeri,
            KanitUretici.Uret(bulgular, _rulesetSurumu),
            eslesmeler,
            incelenen,
            kesif.GezilenDizin,
            notlar);
    }

    private static int Yuzde(Asama asama, int yapilan, int toplam)
    {
        if (toplam <= 0) return Asama.BitisYuzdesi(asama);
        var bitis = Asama.BitisYuzdesi(asama);
        var baslangic = bitis - asama.Agirlik;
        return baslangic + (int)(asama.Agirlik * Math.Clamp(yapilan / (double)toplam, 0, 1));
    }
}
