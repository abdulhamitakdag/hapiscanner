namespace HapiScanner.Core.Adli;

public enum AyricalikDurumu
{
    Bilinmeyen,
    Standart,
    Yonetici,
}

public interface IAyricalikSaglayici
{
    AyricalikDurumu Durum();
}

public sealed class SabitAyricalik : IAyricalikSaglayici
{
    private readonly AyricalikDurumu _durum;
    public SabitAyricalik(AyricalikDurumu durum) => _durum = durum;
    public AyricalikDurumu Durum() => _durum;
}

public static class AyricalikMesaji
{

    public const string YoneticiGerekli =
        "Tam dosya sistemi gecmisi taramasi icin yonetici (Administrator) yetkisi gerekir. " +
        "Tarama yonetici olmadan da devam eder, ancak raporun adli kapsami dusuk isaretlenir.";
}
