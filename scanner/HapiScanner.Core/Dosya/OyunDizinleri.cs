namespace HapiScanner.Core.Dosya;

public static class OyunDizinleri
{

    private static readonly string[] AppDataLauncherlari =
    {
        ".minecraft", ".technic", ".feedthebeast", "PrismLauncher", "ATLauncher",
        "ModrinthApp", "gdlauncher", "SKlauncher",
    };

    private static readonly string[] ProfilLauncherlari =
    {
        @"curseforge\minecraft", "MultiMC", "PrismLauncher", "Documents\\MultiMC",
    };

    private static readonly string[] OyunAltDizinleri =
    {
        "mods", "versions", "libraries", "resourcepacks", "config",
    };

    public static IReadOnlyList<TaramaKoku> Kokler(
        IReadOnlyDictionary<string, string?> ortam, IDosyaSistemi? ds = null)
    {
        var kokler = new List<TaramaKoku>();
        var eklenen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Ekle(string? yol, AdayNedeni neden, int derinlik = 6)
        {
            if (string.IsNullOrWhiteSpace(yol)) return;
            if (!eklenen.Add(yol)) return;
            if (ds is not null && !ds.DizinVarMi(yol)) return;
            kokler.Add(new TaramaKoku(yol, neden, derinlik));
        }

        string? Birlestir(string? taban, string parca)
            => string.IsNullOrWhiteSpace(taban) ? null : Path.Combine(taban, parca);

        var appData = Deger(ortam, "APPDATA");
        var profil = Deger(ortam, "USERPROFILE");
        var yerelAppData = Deger(ortam, "LOCALAPPDATA");
        var temp = Deger(ortam, "TEMP") ?? Birlestir(yerelAppData, "Temp");

        foreach (var launcher in AppDataLauncherlari)
        {
            var kok = Birlestir(appData, launcher);
            Ekle(kok, AdayNedeni.MinecraftDizini);

            if (kok is null) continue;
            foreach (var alt in OyunAltDizinleri)
                Ekle(Path.Combine(kok, alt), AdayNedeni.MinecraftModDizini, 8);
        }

        foreach (var launcher in ProfilLauncherlari)
            Ekle(Birlestir(profil, launcher), AdayNedeni.LauncherDizini);

        Ekle(Birlestir(profil, "Downloads"), AdayNedeni.Indirilenler, 3);
        Ekle(Birlestir(profil, "Desktop"), AdayNedeni.Masaustu, 3);
        Ekle(temp, AdayNedeni.GeciciDizin, 3);

        return kokler;
    }

    public static IReadOnlyList<TaramaKoku> Kokler(IDosyaSistemi ds)
        => Kokler(new Dictionary<string, string?>
        {
            ["APPDATA"] = Environment.GetEnvironmentVariable("APPDATA"),
            ["LOCALAPPDATA"] = Environment.GetEnvironmentVariable("LOCALAPPDATA"),
            ["USERPROFILE"] = Environment.GetEnvironmentVariable("USERPROFILE"),
            ["TEMP"] = Environment.GetEnvironmentVariable("TEMP"),
        }, ds);

    private static string? Deger(IReadOnlyDictionary<string, string?> ortam, string ad)
        => ortam.TryGetValue(ad, out var deger) ? deger : null;
}
