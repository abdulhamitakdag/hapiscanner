namespace HapiScanner.Core.Adli;

[Flags]
public enum UsnOlayi
{
    Yok = 0,
    Olusturma = 1,
    Silme = 2,
    YenidenAdlandirmaEski = 4,
    YenidenAdlandirmaYeni = 8,
    VeriDegisimi = 16,
    Diger = 32,
}

public sealed record UsnKaydi(
    long Usn,
    string Ad,
    UsnOlayi Olay,
    DateTimeOffset Zaman,
    ulong DosyaReferansi = 0,
    ulong UstReferans = 0)
{
    public string Uzanti => Path.GetExtension(Ad).ToLowerInvariant();
}

public sealed record UsnJournalDurumu(
    ulong JournalId,
    long IlkUsn,
    long SonUsn,
    long AzamiBoyut,
    DateTimeOffset? EnEskiKayitZamani);

public enum UsnSonucTuru
{

    Okundu,

    Desteklenmiyor,

    JournalYok,

    ErisimReddedildi,

    Hata,
}

public sealed record UsnOkumaSonucu(
    UsnSonucTuru Tur,
    UsnJournalDurumu? Journal = null,
    IReadOnlyList<UsnKaydi>? Kayitlar = null,
    string? Aciklama = null)
{
    public IReadOnlyList<UsnKaydi> KayitListesi => Kayitlar ?? Array.Empty<UsnKaydi>();

    public static UsnOkumaSonucu Desteklenmiyor(string birim)
        => new(UsnSonucTuru.Desteklenmiyor, Aciklama: $"{birim}: USN destekleyen bir dosya sistemi degil");

    public static UsnOkumaSonucu ErisimYok(string birim)
        => new(UsnSonucTuru.ErisimReddedildi, Aciklama: $"{birim}: USN icin yonetici yetkisi gerekiyor");
}

public interface IUsnSaglayici
{
    UsnOkumaSonucu Oku(BirimBilgisi birim);
}

public enum MftSonucTuru
{
    Okundu,
    Desteklenmiyor,
    ErisimReddedildi,
    Hata,
}

public sealed record MftOkumaSonucu(
    MftSonucTuru Tur,
    long KayitSayisi = 0,
    string? Aciklama = null)
{
    public static MftOkumaSonucu Desteklenmiyor(string birim)
        => new(MftSonucTuru.Desteklenmiyor, Aciklama: $"{birim}: NTFS degil, MFT yok");

    public static MftOkumaSonucu ErisimYok(string birim)
        => new(MftSonucTuru.ErisimReddedildi, Aciklama: $"{birim}: MFT icin yonetici yetkisi gerekiyor");
}

public interface IMftSaglayici
{
    MftOkumaSonucu Oku(BirimBilgisi birim);
}
