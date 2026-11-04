namespace HapiScanner.Core.Dosya;

public sealed class AdaySuzgeci
{

    private static readonly HashSet<string> IlgiliUzantilar = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jar", ".exe", ".dll", ".zip", ".litemod", ".pak", ".sys", ".bat", ".ps1",
    };

    private static readonly HashSet<string> AlakasizUzantilar = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".avi", ".mov", ".webm", ".iso", ".img", ".vhd", ".vhdx",
        ".bak", ".7z", ".rar", ".tar", ".gz", ".wim", ".vmdk", ".mp3", ".flac",
        ".psd", ".raw", ".png", ".jpg", ".jpeg", ".gif", ".pdf", ".docx", ".xlsx",
    };

    public long AzamiAdayBoyutu { get; init; } = 256L * 1024 * 1024;

    public long AsgariAdayBoyutu { get; init; } = 64;

    public bool AdayMi(DosyaBilgisi dosya)
    {
        if (dosya.Boyut < AsgariAdayBoyutu) return false;
        if (dosya.Boyut > AzamiAdayBoyutu) return false;

        var uzanti = dosya.Uzanti;
        if (AlakasizUzantilar.Contains(uzanti)) return false;
        if (IlgiliUzantilar.Contains(uzanti)) return true;

        if (uzanti.Length == 0 || !AlakasizUzantilar.Contains(uzanti))
            return dosya.Boyut <= 64L * 1024 * 1024;

        return false;
    }

    public bool HashlenebilirMi(DosyaBilgisi dosya)
        => dosya.Boyut <= Hashleme.AzamiHashBoyutu && AdayMi(dosya);
}
