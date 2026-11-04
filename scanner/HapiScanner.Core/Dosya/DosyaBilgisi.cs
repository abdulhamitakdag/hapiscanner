namespace HapiScanner.Core.Dosya;

public sealed record DosyaBilgisi(
    string Yol,
    string Ad,
    long Boyut,
    DateTimeOffset Degisim,
    DateTimeOffset? Olusturma = null)
{
    public string Uzanti => Path.GetExtension(Ad).ToLowerInvariant();

    public string OrtukKimlik() => Hashleme.MetinOzeti(Yol.ToLowerInvariant())[..16];
}

public enum AdayNedeni
{
    MinecraftModDizini,
    MinecraftDizini,
    Indirilenler,
    Masaustu,
    GeciciDizin,
    LauncherDizini,
    Diger,
}

public sealed record Aday(DosyaBilgisi Dosya, AdayNedeni Neden);
