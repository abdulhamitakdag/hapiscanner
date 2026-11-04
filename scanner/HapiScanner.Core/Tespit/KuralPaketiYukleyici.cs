using System.Security.Cryptography;
using HapiScanner.Core.Dosya;

namespace HapiScanner.Core.Tespit;

public enum PaketRedNedeni
{
    Yok,

    ImzaEksik,

    ImzaGecersiz,

    SurumBicimiGecersiz,

    SurumCokEski,

    SurumGeriAlinmis,

    IcerikGecersiz,
}

public sealed record PaketYuklemeSonucu(
    KuralPaketi? Paket,
    PaketRedNedeni Neden,
    string? Aciklama = null,
    string? PaketOzeti = null)
{
    public bool Kabul => Paket is not null && Neden == PaketRedNedeni.Yok;

    public static PaketYuklemeSonucu Red(PaketRedNedeni neden, string aciklama)
        => new(null, neden, aciklama);
}

public interface ISurumDeposu
{
    string? SonGorulenSurum();
    void Kaydet(string surum);
}

public sealed class BellekSurumDeposu : ISurumDeposu
{
    private string? _surum;
    public string? SonGorulenSurum() => _surum;
    public void Kaydet(string surum) => _surum = surum;
}

public sealed class DosyaSurumDeposu : ISurumDeposu
{
    private readonly string _yol;
    public DosyaSurumDeposu(string yol) => _yol = yol;

    public string? SonGorulenSurum()
    {
        try
        {
            if (!File.Exists(_yol)) return null;
            var icerik = File.ReadAllText(_yol).Trim();
            return RulesetSurumu.GecerliMi(icerik) ? icerik : null;
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Kaydet(string surum)
    {
        try
        {
            var dizin = Path.GetDirectoryName(_yol);
            if (!string.IsNullOrEmpty(dizin)) Directory.CreateDirectory(dizin);
            File.WriteAllText(_yol, surum);
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
        {

        }
    }
}

public sealed class KuralPaketiYukleyici
{
    private readonly IReadOnlyList<string> _yayinciAnahtarlari;
    private readonly ISurumDeposu _surumDeposu;

    public KuralPaketiYukleyici(
        IReadOnlyList<string> yayinciAnahtarlari,
        ISurumDeposu? surumDeposu = null)
    {
        _yayinciAnahtarlari = yayinciAnahtarlari;
        _surumDeposu = surumDeposu ?? new BellekSurumDeposu();
    }

    public PaketYuklemeSonucu Yukle(string paketJson, string? imzaBase64, string? asgariSurum = null)
    {
        if (string.IsNullOrWhiteSpace(imzaBase64))
            return PaketYuklemeSonucu.Red(PaketRedNedeni.ImzaEksik,
                "Kural paketi imzasiz. Imzasiz paket KULLANILMAZ.");

        KuralPaketi paket;
        try
        {
            paket = KuralMotoru.PaketCoz(paketJson);
        }
        catch (Exception hata)
        {
            return PaketYuklemeSonucu.Red(PaketRedNedeni.IcerikGecersiz,
                $"Kural paketi okunamadi: {hata.Message}");
        }

        if (!RulesetSurumu.GecerliMi(paket.RulesetSurumu))
            return PaketYuklemeSonucu.Red(PaketRedNedeni.SurumBicimiGecersiz,
                $"ruleset_version bicimi gecersiz: {paket.RulesetSurumu}");

        var ozet = Hashleme.BaytOzeti(System.Text.Encoding.UTF8.GetBytes(paketJson));

        var metin = Imza.KuralPaketiMetni(ozet, paket.RulesetSurumu);
        if (!ImzaTutuyorMu(metin, imzaBase64))
            return PaketYuklemeSonucu.Red(PaketRedNedeni.ImzaGecersiz,
                "Kural paketi imzasi bilinen yayinci anahtarlariyla dogrulanamadi.");

        if (asgariSurum is not null && RulesetSurumu.KucukMu(paket.RulesetSurumu, asgariSurum))
            return PaketYuklemeSonucu.Red(PaketRedNedeni.SurumCokEski,
                $"Kural paketi surumu {paket.RulesetSurumu}, sunucunun istedigi taban {asgariSurum}.");

        var sonGorulen = _surumDeposu.SonGorulenSurum();
        if (sonGorulen is not null && RulesetSurumu.KucukMu(paket.RulesetSurumu, sonGorulen))
            return PaketYuklemeSonucu.Red(PaketRedNedeni.SurumGeriAlinmis,
                $"Daha once {sonGorulen} surumu gorulmus; {paket.RulesetSurumu} surumune " +
                "donus downgrade sayilir.");

        var dogrulama = Dogrula(paket);
        if (dogrulama is not null)
            return PaketYuklemeSonucu.Red(PaketRedNedeni.IcerikGecersiz, dogrulama);

        _surumDeposu.Kaydet(paket.RulesetSurumu);
        return new PaketYuklemeSonucu(paket, PaketRedNedeni.Yok, PaketOzeti: ozet);
    }

    private bool ImzaTutuyorMu(byte[] metin, string imzaBase64)
    {
        byte[] imza;
        try
        {
            imza = Convert.FromBase64String(imzaBase64);
        }
        catch (FormatException)
        {
            return false;
        }

        foreach (var anahtar in _yayinciAnahtarlari)
        {
            try
            {
                using var ecdsa = ECDsa.Create();
                ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(anahtar), out _);
                if (ecdsa.VerifyData(metin, imza, HashAlgorithmName.SHA256,
                        DSASignatureFormat.Rfc3279DerSequence))
                    return true;
            }
            catch (Exception hata) when (hata is CryptographicException or FormatException)
            {

            }
        }

        return false;
    }

    public static string? Dogrula(KuralPaketi paket)
    {
        if (paket.Kurallar.Count == 0) return "kural paketi bos";

        var gorulenIdler = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kural in paket.Kurallar)
        {
            if (string.IsNullOrWhiteSpace(kural.RuleId)) return "rule_id bos";
            if (!gorulenIdler.Add(kural.RuleId)) return $"{kural.RuleId}: rule_id tekrar ediyor";
            if (!Severity.GecerliMi(kural.Severity))
                return $"{kural.RuleId}: gecersiz severity '{kural.Severity}'";
            if (kural.AsgariGosterge > kural.Gostergeler.Count)
                return $"{kural.RuleId}: min_indicators gosterge sayisindan buyuk";

            foreach (var gosterge in kural.Gostergeler)
            {
                if (!GostergeTipi.TanimliMi(gosterge.Tip))
                    return $"{kural.RuleId}: tanimsiz gosterge tipi '{gosterge.Tip}'";
                if (gosterge.Degerler.Count == 0)
                    return $"{kural.RuleId}: '{gosterge.Tip}' gostergesinde deger yok";
            }
        }

        foreach (var gosterge in paket.IzinListesi ?? new List<Gosterge>())
        {
            if (!GostergeTipi.TanimliMi(gosterge.Tip))
                return $"izin listesi: tanimsiz gosterge tipi '{gosterge.Tip}'";
        }

        return null;
    }
}
