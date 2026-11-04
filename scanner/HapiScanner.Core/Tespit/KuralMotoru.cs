using System.Text.Json;

namespace HapiScanner.Core.Tespit;

public sealed class GostergeSeti
{
    private readonly Dictionary<string, HashSet<string>> _degerler = new(StringComparer.Ordinal);

    public string OrtukKimlik { get; init; } = string.Empty;

    public void Ekle(string tip, string? deger)
    {
        if (string.IsNullOrWhiteSpace(deger)) return;
        if (!_degerler.TryGetValue(tip, out var kume))
        {
            kume = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _degerler[tip] = kume;
        }
        kume.Add(deger.Trim());
    }

    public void Ekle(string tip, IEnumerable<string> degerler)
    {
        foreach (var deger in degerler) Ekle(tip, deger);
    }

    public bool Icerir(string tip, string deger)
        => _degerler.TryGetValue(tip, out var kume) && kume.Contains(deger);

    public bool IcerirVeyaOnekler(string tip, string deger)
    {
        if (!_degerler.TryGetValue(tip, out var kume)) return false;
        if (kume.Contains(deger)) return true;
        return kume.Any(v => v.StartsWith(deger, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlySet<string> Degerler(string tip)
        => _degerler.TryGetValue(tip, out var kume)
            ? kume
            : (IReadOnlySet<string>)new HashSet<string>();

    public IEnumerable<string> Tipler => _degerler.Keys;
}

public sealed class KuralMotoru
{
    private readonly KuralPaketi _paket;
    private readonly Agirliklar _agirliklar;

    public KuralMotoru(KuralPaketi paket, Agirliklar? agirliklar = null)
    {
        _paket = paket;
        _agirliklar = agirliklar ?? new Agirliklar();
    }

    public string RulesetSurumu => _paket.RulesetSurumu;

    public static KuralPaketi PaketCoz(string json)
    {
        var paket = JsonSerializer.Deserialize<KuralPaketi>(json)
                    ?? throw new InvalidDataException("kural paketi okunamadi");
        if (string.IsNullOrWhiteSpace(paket.RulesetSurumu))
            throw new InvalidDataException("ruleset_version zorunlu");
        foreach (var kural in paket.Kurallar)
        {
            if (kural.AsgariGosterge < 1)
                throw new InvalidDataException($"{kural.RuleId}: min_indicators en az 1 olmali");
            if (kural.Gostergeler.Count == 0)
                throw new InvalidDataException($"{kural.RuleId}: gosterge yok");
        }
        return paket;
    }

    public bool IzinliMi(GostergeSeti set)
    {
        if (_paket.IzinListesi is null) return false;
        return _paket.IzinListesi.Any(g => GostergeTutuyorMu(set, g));
    }

    public IReadOnlyList<Eslesme> Degerlendir(GostergeSeti set)
    {
        if (IzinliMi(set)) return Array.Empty<Eslesme>();

        var sonuclar = new List<Eslesme>();
        foreach (var kural in _paket.Kurallar)
        {
            var eslesenler = kural.Gostergeler
                .Where(g => GostergeTutuyorMu(set, g))
                .ToList();

            if (eslesenler.Count < kural.AsgariGosterge) continue;

            var puan = eslesenler.Sum(g => _agirliklar.Agirlik(g.Tip));
            var guclu = eslesenler.Any(g => !_agirliklar.ZayifMi(g.Tip));

            var guven = Guvenle(puan, guclu);
            if (guven == Guven.Yok) continue;

            sonuclar.Add(new Eslesme(kural.RuleId, kural.Ad, kural.Kategori,
                kural.Severity, guven, puan,
                eslesenler.Select(g => g.Tip).Distinct().ToList()));
        }

        return sonuclar.OrderByDescending(e => e.Puan).ToList();
    }

    private Guven Guvenle(int puan, bool gucluGostergeVar)
    {

        if (!gucluGostergeVar) return puan >= _agirliklar.SuphelilikEsigi
            ? Guven.Dusuk : Guven.Yok;

        if (puan >= _agirliklar.IsaretlemeEsigi) return Guven.Yuksek;
        if (puan >= _agirliklar.SuphelilikEsigi) return Guven.Orta;
        return Guven.Dusuk;
    }

    private static bool GostergeTutuyorMu(GostergeSeti set, Gosterge gosterge)
    {
        foreach (var deger in gosterge.Degerler)
        {
            switch (gosterge.Tip)
            {
                case GostergeTipi.PaketAdi:
                case GostergeTipi.SinifAdi:
                    if (set.IcerirVeyaOnekler(gosterge.Tip, deger)) return true;
                    break;

                case GostergeTipi.ManifestIsareti:

                    var hedef = gosterge.Anahtar is null ? deger : $"{gosterge.Anahtar}={deger}";
                    if (set.Icerir(gosterge.Tip, hedef)) return true;
                    break;

                case GostergeTipi.DosyaAdi:
                case GostergeTipi.GenelMetin:

                    if (set.Degerler(gosterge.Tip)
                           .Any(v => v.Contains(deger, StringComparison.OrdinalIgnoreCase)))
                        return true;
                    break;

                default:
                    if (set.Icerir(gosterge.Tip, deger)) return true;
                    break;
            }
        }
        return false;
    }
}
