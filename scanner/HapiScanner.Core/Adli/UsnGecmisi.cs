using HapiScanner.Core.Dosya;

namespace HapiScanner.Core.Adli;

public static class UsnKanitTuru
{
    public const string Olusturma = "USN_CREATE";
    public const string Silme = "USN_DELETE";
    public const string YenidenAdlandirma = "USN_RENAME";
    public const string Degisim = "USN_MODIFY";
}

public sealed record GecmisIz(
    string Ad,
    string OrtukKimlik,
    DateTimeOffset IlkGorulme,
    DateTimeOffset SonGorulme,
    bool SuAndaVarMi,
    UsnOlayi Olaylar,
    long KayitSayisi)
{
    public bool SilinmeGorulduMu => Olaylar.HasFlag(UsnOlayi.Silme);
    public bool YenidenAdlandirmaGorulduMu =>
        Olaylar.HasFlag(UsnOlayi.YenidenAdlandirmaEski) || Olaylar.HasFlag(UsnOlayi.YenidenAdlandirmaYeni);

    public IReadOnlyList<string> KanitTurleri()
    {
        var liste = new List<string>();
        if (Olaylar.HasFlag(UsnOlayi.Olusturma)) liste.Add(UsnKanitTuru.Olusturma);
        if (SilinmeGorulduMu) liste.Add(UsnKanitTuru.Silme);
        if (YenidenAdlandirmaGorulduMu) liste.Add(UsnKanitTuru.YenidenAdlandirma);
        if (liste.Count == 0 && Olaylar.HasFlag(UsnOlayi.VeriDegisimi)) liste.Add(UsnKanitTuru.Degisim);
        return liste;
    }
}

public sealed class UsnGecmisAnalizi
{
    private readonly Func<UsnKaydi, bool> _ilgiliMi;

    public UsnGecmisAnalizi(Func<UsnKaydi, bool>? ilgiliMi = null)
        => _ilgiliMi = ilgiliMi ?? VarsayilanIlgiSuzgeci;

    public static bool VarsayilanIlgiSuzgeci(UsnKaydi kayit)
        => kayit.Uzanti is ".jar" or ".exe" or ".dll" or ".litemod" or ".zip" or ".sys";

    public IReadOnlyList<GecmisIz> Analiz(
        IEnumerable<UsnKaydi> kayitlar,
        ISet<string>? suAndaVarOlanAdlar = null)
    {
        var gruplar = new Dictionary<string, List<UsnKaydi>>(StringComparer.OrdinalIgnoreCase);

        foreach (var kayit in kayitlar)
        {
            if (string.IsNullOrWhiteSpace(kayit.Ad)) continue;
            if (!_ilgiliMi(kayit)) continue;

            var anahtar = kayit.DosyaReferansi != 0
                ? $"ref:{kayit.DosyaReferansi}"
                : $"ad:{kayit.Ad.ToLowerInvariant()}";

            if (!gruplar.TryGetValue(anahtar, out var liste))
            {
                liste = new List<UsnKaydi>();
                gruplar[anahtar] = liste;
            }
            liste.Add(kayit);
        }

        var izler = new List<GecmisIz>();
        foreach (var liste in gruplar.Values)
        {
            var sirali = liste.OrderBy(k => k.Usn).ToList();
            var olaylar = UsnOlayi.Yok;
            foreach (var k in sirali) olaylar |= k.Olay;

            var sonAd = sirali[^1].Ad;
            var varMi = suAndaVarOlanAdlar is not null &&
                        sirali.Any(k => suAndaVarOlanAdlar.Contains(k.Ad));

            izler.Add(new GecmisIz(
                sonAd,
                Hashleme.MetinOzeti(sonAd.ToLowerInvariant())[..16],
                sirali.Min(k => k.Zaman),
                sirali.Max(k => k.Zaman),
                varMi,
                olaylar,
                sirali.Count));
        }

        return izler.OrderBy(i => i.IlkGorulme).ToList();
    }
}
