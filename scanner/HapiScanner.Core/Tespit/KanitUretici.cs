using System.Globalization;

namespace HapiScanner.Core.Tespit;

public static class KanitTuru
{

    public const string MevcutDosya = "FILE_PRESENT";

    public const string GecmisKayit = "HISTORICAL_USN";

    public const string Runtime = "RUNTIME";
}

public sealed record Bulgu(
    Eslesme Eslesme,
    string KanitTuru,
    bool SuAndaVar,
    DateTimeOffset? IlkGorulme = null,
    DateTimeOffset? SonGorulme = null);

public static class KanitUretici
{

    public static List<KanitIstegi> Uret(IEnumerable<Bulgu> bulgular, string rulesetSurumu)
    {
        var gruplar = bulgular
            .GroupBy(b => (b.Eslesme.RuleId, b.KanitTuru), StringTupleKarsilastirici.Ornek);

        var kanitlar = new List<KanitIstegi>();
        foreach (var grup in gruplar)
        {
            var enIyi = grup.OrderByDescending(b => b.Eslesme.Guven)
                            .ThenByDescending(b => Severity.Siralama(b.Eslesme.Severity))
                            .First();

            var severity = grup.Aggregate((string?)null,
                (agir, b) => Severity.EnAgir(agir, b.Eslesme.Severity)) ?? Severity.Orta;

            kanitlar.Add(new KanitIstegi(
                RuleId: enIyi.Eslesme.RuleId,
                EvidenceType: grup.Key.Item2,
                ArtifactCategory: enIyi.Eslesme.Kategori,
                Confidence: GuvenAdi.Protokol(enIyi.Eslesme.Guven),
                Severity: severity,
                CurrentlyPresent: grup.Any(b => b.SuAndaVar),
                RuleVersion: rulesetSurumu,
                FirstSeen: Zaman(grup.Select(b => b.IlkGorulme).Where(z => z is not null).Min()),
                LastSeen: Zaman(grup.Select(b => b.SonGorulme).Where(z => z is not null).Max())));
        }

        return kanitlar
            .OrderByDescending(k => Severity.Siralama(k.Severity))
            .ToList();
    }

    private static string? Zaman(DateTimeOffset? an)
        => an?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
}

internal sealed class StringTupleKarsilastirici : IEqualityComparer<(string, string)>
{
    internal static readonly StringTupleKarsilastirici Ornek = new();

    public bool Equals((string, string) a, (string, string) b)
        => string.Equals(a.Item1, b.Item1, StringComparison.Ordinal)
           && string.Equals(a.Item2, b.Item2, StringComparison.Ordinal);

    public int GetHashCode((string, string) deger)
        => HashCode.Combine(deger.Item1, deger.Item2);
}
