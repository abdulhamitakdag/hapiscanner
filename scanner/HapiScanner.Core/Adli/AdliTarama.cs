namespace HapiScanner.Core.Adli;

public sealed record AdliTaramaSonucu(
    AdliKapsam Kapsam,
    IReadOnlyList<GecmisIz> Izler,
    IReadOnlyList<BirimDegerlendirmesi> Birimler);

public sealed class AdliTarama
{
    private readonly IBirimSaglayici _birimler;
    private readonly IMftSaglayici _mft;
    private readonly IUsnSaglayici _usn;
    private readonly IAyricalikSaglayici _ayricalik;
    private readonly AdliKapsamHesaplayici _hesaplayici;
    private readonly UsnGecmisAnalizi _gecmis;

    public AdliTarama(
        IBirimSaglayici birimler,
        IMftSaglayici mft,
        IUsnSaglayici usn,
        IAyricalikSaglayici ayricalik,
        AdliKapsamHesaplayici? hesaplayici = null,
        UsnGecmisAnalizi? gecmis = null)
    {
        _birimler = birimler;
        _mft = mft;
        _usn = usn;
        _ayricalik = ayricalik;
        _hesaplayici = hesaplayici ?? new AdliKapsamHesaplayici();
        _gecmis = gecmis ?? new UsnGecmisAnalizi();
    }

    public AdliTaramaSonucu Calistir(
        IReadOnlyDictionary<string, ulong>? oncekiJournalIdleri = null,
        ISet<string>? suAndaVarOlanAdlar = null,
        RuntimeErisimi runtime = RuntimeErisimi.Sinirli,
        DateTimeOffset? simdi = null)
    {
        var degerlendirmeler = new List<BirimDegerlendirmesi>();

        foreach (var birim in _birimler.Birimler())
        {
            var mft = birim.MftMumkunMu ? _mft.Oku(birim) : MftOkumaSonucu.Desteklenmiyor(birim.Kok);
            var usn = birim.UsnMumkunMu ? _usn.Oku(birim) : UsnOkumaSonucu.Desteklenmiyor(birim.Kok);

            ulong? onceki = null;
            if (oncekiJournalIdleri is not null &&
                oncekiJournalIdleri.TryGetValue(birim.Kok, out var deger)) onceki = deger;

            degerlendirmeler.Add(new BirimDegerlendirmesi(birim, mft, usn, onceki));
        }

        var kapsam = _hesaplayici.Hesapla(degerlendirmeler, _ayricalik.Durum(), runtime, simdi);

        var kayitlar = degerlendirmeler
            .Where(d => d.Birim.Ilgili)
            .SelectMany(d => d.Usn.KayitListesi);

        var izler = _gecmis.Analiz(kayitlar, suAndaVarOlanAdlar);
        return new AdliTaramaSonucu(kapsam, izler, degerlendirmeler);
    }
}
