namespace HapiScanner.Core.Adli;

public enum DosyaSistemi
{
    Bilinmeyen,
    Ntfs,
    ReFS,
    ExFat,
    Fat32,
    Diger,
}

public static class DosyaSistemiCozumleyici
{
    public static DosyaSistemi Coz(string? ad) => (ad ?? string.Empty).Trim().ToUpperInvariant() switch
    {
        "" => DosyaSistemi.Bilinmeyen,
        "NTFS" => DosyaSistemi.Ntfs,
        "REFS" => DosyaSistemi.ReFS,
        "EXFAT" => DosyaSistemi.ExFat,
        "FAT32" or "FAT" or "FAT16" or "MSDOS" => DosyaSistemi.Fat32,
        _ => DosyaSistemi.Diger,
    };
}

public sealed record BirimBilgisi(
    string Kok,
    string DosyaSistemiAdi,
    bool Ilgili = true,
    bool CikarilabilirMi = false)
{
    public DosyaSistemi Sistem => DosyaSistemiCozumleyici.Coz(DosyaSistemiAdi);

    public bool MftMumkunMu => Sistem == DosyaSistemi.Ntfs;

    public bool UsnMumkunMu => Sistem is DosyaSistemi.Ntfs or DosyaSistemi.ReFS;
}

public interface IBirimSaglayici
{
    IReadOnlyList<BirimBilgisi> Birimler();
}
