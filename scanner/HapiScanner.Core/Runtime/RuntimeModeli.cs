using HapiScanner.Core.Dosya;

namespace HapiScanner.Core.Runtime;

public sealed record RuntimeProcess(
    int ProcessId,
    string Ad,
    string? Yol = null,
    string? KomutSatiri = null,
    DateTimeOffset? Baslangic = null)
{
    public string OrtukKimlik() => Hashleme.MetinOzeti($"{Ad.ToLowerInvariant()}|{ProcessId}")[..16];
}

public sealed record YuklenenModul(
    string Ad,
    string? Yol = null,
    string? ImzaSahibi = null,
    bool ImzaGecerliMi = false,
    long Boyut = 0)
{
    public string OrtukKimlik() => Hashleme.MetinOzeti(Ad.ToLowerInvariant())[..16];
}

public sealed record JvmMetadata(
    IReadOnlyList<string> Argumanlar,
    IReadOnlyList<string> ClasspathGirdileri,
    IReadOnlyList<string> JavaAjanlari,
    IReadOnlyList<string> NativeAjanlar,
    bool AttachMekanizmasiKapali = false)
{
    public static JvmMetadata Bos() => new(
        Array.Empty<string>(), Array.Empty<string>(),
        Array.Empty<string>(), Array.Empty<string>());

    public bool AjanVarMi => JavaAjanlari.Count > 0 || NativeAjanlar.Count > 0;
}

public enum AnomaliTuru
{

    JavaAjani,

    NativeAjan,

    KullaniciYazilabilirYoldanModul,

    ImzasizModul,

    SiradisiClasspath,

    AttachMekanizmasiKapali,

    HariciProcessIliskisi,
}

public sealed record RuntimeAnomali(
    AnomaliTuru Tur,
    string Ad,
    string OrtukKimlik,
    string Aciklama)
{
    public static RuntimeAnomali Olustur(AnomaliTuru tur, string ad, string aciklama)
        => new(tur, ad, Hashleme.MetinOzeti(ad.ToLowerInvariant())[..16], aciklama);
}

public interface IProcessSaglayici
{
    IReadOnlyList<RuntimeProcess> Processler();

    IReadOnlyList<YuklenenModul>? Moduller(int processId);
}
