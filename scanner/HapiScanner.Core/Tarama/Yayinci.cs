namespace HapiScanner.Core.Tarama;

public static class Yayinci
{
    public static IReadOnlyList<string> PublicKeyler { get; } = new[]
    {

        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEWg7zzHIAqVC+3vYp/BmIwpWrHR2p9fe1BNwC" +
        "ZvDXGdsxTnKOKKy5fVKkJoJQkPZcmc30fmwz8gnI3f1JtuMGiQ==",
    };

    public const string VarsayilanSunucu = "https://verify.hapicraft.com.tr/";

    public const string PaketDosyasi = "hapicraft-kurallar.json";
    public const string ImzaDosyasi = "hapicraft-kurallar.json.sig";
}

public sealed class DosyaKuralPaketiKaynagi : IKuralPaketiKaynagi
{
    private readonly string _paketYolu;
    private readonly string _imzaYolu;

    public DosyaKuralPaketiKaynagi(string dizin)
    {
        _paketYolu = Path.Combine(dizin, Yayinci.PaketDosyasi);
        _imzaYolu = Path.Combine(dizin, Yayinci.ImzaDosyasi);
    }

    public bool VarMi => File.Exists(_paketYolu) && File.Exists(_imzaYolu);

    public async Task<(string Paket, string? ImzaBase64)> GetirAsync(CancellationToken iptal)
    {
        if (!VarMi)
            throw new FileNotFoundException(
                "Kural paketi bulunamadı. Scanner'ı yeniden indir.", _paketYolu);

        var paket = await File.ReadAllTextAsync(_paketYolu, iptal).ConfigureAwait(false);
        var imza = (await File.ReadAllTextAsync(_imzaYolu, iptal).ConfigureAwait(false)).Trim();
        return (paket, imza);
    }
}
