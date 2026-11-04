using System.Text.Json.Serialization;

namespace HapiScanner.Core.Tespit;

public static class GostergeTipi
{
    public const string DosyaAdi = "FILENAME";
    public const string Sha256 = "SHA256";
    public const string PeMetadata = "PE_METADATA";
    public const string JarMetadata = "JAR_METADATA";
    public const string ManifestIsareti = "MANIFEST_MARKER";
    public const string ModMetadata = "MOD_METADATA";
    public const string PaketAdi = "PACKAGE_NAME";
    public const string SinifAdi = "CLASS_NAME";
    public const string BilinenKaynak = "KNOWN_RESOURCE";
    public const string YapisalIsaret = "STRUCTURAL_MARKER";
    public const string GenelMetin = "GENERIC_STRING";

    public const string RuntimeModul = "RUNTIME_MODULE";
    public const string JvmAjani = "JVM_AGENT";
    public const string ProcessIliskisi = "PROCESS_RELATION";
    public const string RuntimeAnomalisi = "RUNTIME_ANOMALY";

    public const string UsnOlayi = "USN_EVENT";
    public const string MftKaniti = "MFT_EVIDENCE";
    public const string CalistirmaIzi = "EXECUTION_ARTIFACT";

    private static readonly HashSet<string> Tanimlilar = new(StringComparer.Ordinal)
    {
        DosyaAdi, Sha256, PeMetadata, JarMetadata, ManifestIsareti, ModMetadata,
        PaketAdi, SinifAdi, BilinenKaynak, YapisalIsaret, GenelMetin,
        RuntimeModul, JvmAjani, ProcessIliskisi, RuntimeAnomalisi,
        UsnOlayi, MftKaniti, CalistirmaIzi,
    };

    public static bool TanimliMi(string? tip) => tip is not null && Tanimlilar.Contains(tip);
}

public sealed record Gosterge(
    [property: JsonPropertyName("type")] string Tip,
    [property: JsonPropertyName("values")] List<string> Degerler,
    [property: JsonPropertyName("key")] string? Anahtar = null);

public sealed record Kural(
    [property: JsonPropertyName("rule_id")] string RuleId,
    [property: JsonPropertyName("name")] string Ad,
    [property: JsonPropertyName("category")] string Kategori,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("min_indicators")] int AsgariGosterge,
    [property: JsonPropertyName("indicators")] List<Gosterge> Gostergeler);

public sealed record KuralPaketi(
    [property: JsonPropertyName("ruleset_version")] string RulesetSurumu,
    [property: JsonPropertyName("rules")] List<Kural> Kurallar,
    [property: JsonPropertyName("allowlist")] List<Gosterge>? IzinListesi = null);

public sealed class Agirliklar
{
    private static readonly Dictionary<string, int> Varsayilan = new(StringComparer.Ordinal)
    {
        [GostergeTipi.Sha256] = 100,
        [GostergeTipi.ManifestIsareti] = 55,
        [GostergeTipi.ModMetadata] = 55,
        [GostergeTipi.PaketAdi] = 50,
        [GostergeTipi.SinifAdi] = 50,
        [GostergeTipi.BilinenKaynak] = 50,
        [GostergeTipi.JarMetadata] = 35,
        [GostergeTipi.YapisalIsaret] = 35,
        [GostergeTipi.PeMetadata] = 30,
        [GostergeTipi.RuntimeModul] = 50,
        [GostergeTipi.JvmAjani] = 35,
        [GostergeTipi.ProcessIliskisi] = 35,
        [GostergeTipi.DosyaAdi] = 10,
        [GostergeTipi.GenelMetin] = 10,
        [GostergeTipi.RuntimeAnomalisi] = 10,
        [GostergeTipi.UsnOlayi] = 35,
        [GostergeTipi.MftKaniti] = 30,
        [GostergeTipi.CalistirmaIzi] = 30,
    };

    public IReadOnlyDictionary<string, int> GostergeAgirligi { get; init; } = Varsayilan;

    public int IsaretlemeEsigi { get; init; } = 100;

    public int SuphelilikEsigi { get; init; } = 45;

    public int Agirlik(string gostergeTipi)
        => GostergeAgirligi.TryGetValue(gostergeTipi, out var deger) ? deger : 10;

    public bool ZayifMi(string gostergeTipi) => Agirlik(gostergeTipi) <= 10;
}

public enum Guven
{
    Yok,
    Dusuk,
    Orta,
    Yuksek,
}

public sealed record Eslesme(
    string RuleId,
    string Ad,
    string Kategori,
    string Severity,
    Guven Guven,
    int Puan,
    IReadOnlyList<string> EslesenGostergeler)
{
    public bool IsaretlenirMi => Guven == Guven.Yuksek;
}
