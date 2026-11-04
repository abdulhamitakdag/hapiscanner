namespace HapiScanner.Core.Tespit;

public static class Severity
{
    public const string Dusuk = "LOW";
    public const string Orta = "MEDIUM";
    public const string Yuksek = "HIGH";
    public const string Kritik = "CRITICAL";

    private static readonly string[] Sirali = { Dusuk, Orta, Yuksek, Kritik };

    public static bool GecerliMi(string? severity)
        => severity is not null && Sirali.Contains(severity, StringComparer.Ordinal);

    public static int Siralama(string? severity)
    {
        var yer = Array.IndexOf(Sirali, severity);
        return yer < 0 ? 0 : yer;
    }

    public static string EnAgir(string? a, string? b)
        => Siralama(a) >= Siralama(b) ? (a ?? Orta) : (b ?? Orta);
}

public static class GuvenAdi
{
    public static string Protokol(Guven guven) => guven switch
    {
        Guven.Yuksek => "HIGH",
        Guven.Orta => "MEDIUM",
        Guven.Dusuk => "LOW",
        _ => "NONE",
    };
}
