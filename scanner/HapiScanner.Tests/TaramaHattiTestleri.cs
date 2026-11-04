using HapiScanner.Core.Adli;
using HapiScanner.Core.Arayuz;
using HapiScanner.Core.Dosya;
using HapiScanner.Core.Runtime;
using HapiScanner.Core.Tarama;
using HapiScanner.Core.Tespit;

namespace HapiScanner.Tests;

internal sealed class SahteDosyaSistemi : IDosyaSistemi
{

    internal static string Yol(params string[] parcalar) => Path.Combine(parcalar);

    private readonly Dictionary<string, List<DosyaBilgisi>> _dosyalar = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _dizinler = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, byte[]> _icerik = new(StringComparer.OrdinalIgnoreCase);

    public List<string> AcilanYollar { get; } = new();

    public SahteDosyaSistemi Dizin(string yol)
    {
        _dosyalar.TryAdd(yol, new List<DosyaBilgisi>());
        _dizinler.TryAdd(yol, new List<string>());

        var ust = Path.GetDirectoryName(yol.TrimEnd(Path.DirectorySeparatorChar));
        if (!string.IsNullOrEmpty(ust))
        {
            Dizin(ust);
            if (!_dizinler[ust].Contains(yol)) _dizinler[ust].Add(yol);
        }
        return this;
    }

    public SahteDosyaSistemi DosyaEkle(string dizin, string ad, long boyut = 4096,
                                       byte[]? icerik = null)
    {
        Dizin(dizin);
        var yol = Path.Combine(dizin, ad);
        _dosyalar[dizin].Add(new DosyaBilgisi(
            yol, ad, boyut,
            new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero)));
        if (icerik is not null) _icerik[yol] = icerik;
        return this;
    }

    public bool DizinVarMi(string yol) => _dizinler.ContainsKey(yol);

    public IEnumerable<string> AltDizinler(string yol)
        => _dizinler.TryGetValue(yol, out var liste) ? liste : Array.Empty<string>();

    public IEnumerable<DosyaBilgisi> Dosyalar(string yol)
        => _dosyalar.TryGetValue(yol, out var liste) ? liste : Array.Empty<DosyaBilgisi>();

    public Stream? Ac(string yol)
    {
        AcilanYollar.Add(yol);
        return _icerik.TryGetValue(yol, out var veri) ? new MemoryStream(veri) : null;
    }
}

internal sealed class SahteBildirici : IAsamaBildirici
{
    public List<string> Asamalar { get; } = new();
    public List<int> Yuzdeler { get; } = new();
    public List<string> Notlar { get; } = new();

    public void Asamaya(Asama asama) => Asamalar.Add(asama.Protokol);
    public void Ilerleme(int yuzde) => Yuzdeler.Add(yuzde);
    public void Not(string satir) => Notlar.Add(satir);
}

public class DosyaKesfiTestleri
{
    private static SahteDosyaSistemi Ornek()
        => new SahteDosyaSistemi()
            .DosyaEkle(SahteDosyaSistemi.Yol("mc", "mods"), "hile.jar")
            .DosyaEkle(SahteDosyaSistemi.Yol("mc", "mods"), "optifine.jar")
            .DosyaEkle(SahteDosyaSistemi.Yol("mc", "mods"), "video.mp4", 900_000_000)
            .DosyaEkle(SahteDosyaSistemi.Yol("mc", "saves"), "dunya.dat")
            .DosyaEkle(SahteDosyaSistemi.Yol("indir"), "kurulum.exe");

    private static TaramaKoku Kok(string yol, int derinlik = 6)
        => new(yol, AdayNedeni.MinecraftModDizini, derinlik);

    [Fact]
    public void AdayDosyalarToplanir()
    {
        var sonuc = new DosyaKesfi(Ornek()).Calistir(new[] { Kok(SahteDosyaSistemi.Yol("mc")) });

        var adlar = sonuc.Adaylar.Select(a => a.Dosya.Ad).ToList();
        Assert.Contains("hile.jar", adlar);
        Assert.Contains("optifine.jar", adlar);
        Assert.DoesNotContain("video.mp4", adlar);
    }

    [Fact]
    public void AtlanacakDizinlereGirilmez()
    {

        var sonuc = new DosyaKesfi(Ornek()).Calistir(new[] { Kok(SahteDosyaSistemi.Yol("mc")) });
        Assert.DoesNotContain(sonuc.Adaylar, a => a.Dosya.Yol.Contains("saves"));
    }

    [Fact]
    public void OlmayanKokAtlanirVeRaporlanir()
    {
        var sonuc = new DosyaKesfi(Ornek()).Calistir(new[] { Kok(SahteDosyaSistemi.Yol("yok")) });
        Assert.Empty(sonuc.Adaylar);
        Assert.Equal(new[] { SahteDosyaSistemi.Yol("yok") }, sonuc.AtlananKokler);
        Assert.False(sonuc.KapsamVarMi);
    }

    [Fact]
    public void AyniDosyaIkiKoktenGelirseBirKezSayilir()
    {
        var sonuc = new DosyaKesfi(Ornek()).Calistir(new[]
        {
            Kok(SahteDosyaSistemi.Yol("mc")), Kok(SahteDosyaSistemi.Yol("mc", "mods")),
        });

        Assert.Equal(sonuc.Adaylar.Count, sonuc.Adaylar.Select(a => a.Dosya.Yol).Distinct().Count());
    }

    [Fact]
    public void DerinlikSiniriAsilmaz()
    {
        var ds = new SahteDosyaSistemi().DosyaEkle(SahteDosyaSistemi.Yol("a", "b", "c", "d"), "derin.jar");
        var sonuc = new DosyaKesfi(ds).Calistir(new[] { Kok(SahteDosyaSistemi.Yol("a"), derinlik: 1) });
        Assert.Empty(sonuc.Adaylar);

        var genis = new DosyaKesfi(ds).Calistir(new[] { Kok(SahteDosyaSistemi.Yol("a"), derinlik: 5) });
        Assert.Single(genis.Adaylar);
    }

    [Fact]
    public void AdaySayisiTavanaVurduguAndaDurulur()
    {
        var ds = new SahteDosyaSistemi();
        for (var i = 0; i < 50; i++) ds.DosyaEkle(SahteDosyaSistemi.Yol("cok"), $"dosya{i}.jar");

        var sonuc = new DosyaKesfi(ds) { AzamiAday = 10 }.Calistir(new[] { Kok(SahteDosyaSistemi.Yol("cok")) });
        Assert.Equal(10, sonuc.Adaylar.Count);
    }

    [Fact]
    public void IptalEdilebilir()
    {
        var ds = new SahteDosyaSistemi().DosyaEkle(SahteDosyaSistemi.Yol("mc"), "a.jar");
        using var iptal = new CancellationTokenSource();
        iptal.Cancel();

        Assert.Throws<OperationCanceledException>(
            () => new DosyaKesfi(ds).Calistir(new[] { Kok(SahteDosyaSistemi.Yol("mc")) }, iptal: iptal.Token));
    }
}

public class OyunDizinleriTestleri
{
    private static readonly string Profil = Path.Combine("Users", "oyuncu");
    private static readonly string AppData = Path.Combine(Profil, "AppData", "Roaming");

    private static readonly Dictionary<string, string?> Ortam = new()
    {
        ["APPDATA"] = AppData,
        ["LOCALAPPDATA"] = Path.Combine(Profil, "AppData", "Local"),
        ["USERPROFILE"] = Profil,
        ["TEMP"] = Path.Combine(Profil, "AppData", "Local", "Temp"),
    };

    [Fact]
    public void MinecraftVeModsDizinleriListeyeGirer()
    {
        var kokler = OyunDizinleri.Kokler(Ortam);
        var yollar = kokler.Select(k => k.Yol).ToList();

        Assert.Contains(Path.Combine(AppData, ".minecraft"), yollar);
        Assert.Contains(Path.Combine(AppData, ".minecraft", "mods"), yollar);
        Assert.Contains(Path.Combine(Profil, "Downloads"), yollar);
        Assert.Contains(Path.Combine(Profil, "Desktop"), yollar);
    }

    [Fact]
    public void ModsDizinidahaDerinTaranir()
    {
        var kokler = OyunDizinleri.Kokler(Ortam);
        var mods = kokler.First(k => k.Yol.EndsWith("mods"));
        var minecraft = kokler.First(k => k.Yol.EndsWith(".minecraft"));
        Assert.True(mods.AzamiDerinlik > minecraft.AzamiDerinlik);
    }

    [Fact]
    public void IndirilenlerSigTaranir()
    {

        var kokler = OyunDizinleri.Kokler(Ortam);
        var indir = kokler.First(k => k.Yol.EndsWith("Downloads"));
        Assert.True(indir.AzamiDerinlik <= 3);
        Assert.Equal(AdayNedeni.Indirilenler, indir.Neden);
    }

    [Fact]
    public void OrtamDegiskeniYoksaKokUretilmez()
    {
        var kokler = OyunDizinleri.Kokler(new Dictionary<string, string?>());
        Assert.Empty(kokler);
    }

    [Fact]
    public void AyniYolIkiKezEklenmez()
    {
        var kokler = OyunDizinleri.Kokler(Ortam);
        Assert.Equal(kokler.Count, kokler.Select(k => k.Yol).Distinct().Count());
    }

    [Fact]
    public void DosyaSistemiVerilirseOlmayanDizinElenir()
    {
        var ds = new SahteDosyaSistemi().Dizin(Path.Combine(AppData, ".minecraft"));
        var kokler = OyunDizinleri.Kokler(Ortam, ds);
        Assert.Single(kokler);
        Assert.EndsWith(".minecraft", kokler[0].Yol);
    }

    [Fact]
    public void BelgelenmemisDizineBakilmaz()
    {

        var yollar = OyunDizinleri.Kokler(Ortam).Select(k => k.Yol.ToLowerInvariant()).ToList();
        foreach (var yasak in new[] { "documents", "pictures", "videos", "music" })
            Assert.DoesNotContain(yollar, y => Path.GetFileName(y) == yasak);
    }
}

public class TaramaHattiTestleri
{

    private static readonly byte[] HileIcerigi = System.Text.Encoding.UTF8.GetBytes(
        new string('x', 256));

    private static string BilinenHash => Convert.ToHexString(
        System.Security.Cryptography.SHA256.HashData(HileIcerigi)).ToLowerInvariant();

    private static KuralMotoru Motor(params Kural[] kurallar)
        => new(new KuralPaketi("2026.11.03.1", kurallar.ToList()));

    private static Kural DosyaAdiKurali() => new(
        "R-AD", "supheli dosya adi", "CHEAT_CLIENT", "HIGH", 1,
        new List<Gosterge> { new(GostergeTipi.DosyaAdi, new List<string> { "hile" }) });

    private static Kural BilinenHashKurali() => new(
        "R-HASH", "bilinen hile istemcisi", "CHEAT_CLIENT", "CRITICAL", 1,
        new List<Gosterge> { new(GostergeTipi.Sha256, new List<string> { BilinenHash }) });

    private static AdliTarama TemizAdli()
        => new(new SahteBirimSaglayici(AdliYardimci.Ntfs()),
               new SahteMft(_ => AdliYardimci.MftOkundu()),
               new SahteUsn(_ => AdliYardimci.Okundu(120)),
               new SabitAyricalik(AyricalikDurumu.Yonetici));

    private static RuntimeAnalizi TemizRuntime()
        => new(new SahteProcessSaglayici(
            new[] { RuntimeYardimci.Hedef() }, RuntimeYardimci.MesruModuller()));

    private static TaramaHatti Hat(IDosyaSistemi ds, KuralMotoru motor,
                                   AdliTarama? adli = null, RuntimeAnalizi? runtime = null)
        => new(new DosyaKesfi(ds), ds, adli ?? TemizAdli(), runtime ?? TemizRuntime(),
               motor, "2026.11.03.1");

    private static IReadOnlyList<TaramaKoku> Kokler()
        => new[] { new TaramaKoku(SahteDosyaSistemi.Yol("mc", "mods"), AdayNedeni.MinecraftModDizini) };

    [Fact]
    public void TemizMakinedeBilinenKanitYok()
    {
        var ds = new SahteDosyaSistemi().DosyaEkle(SahteDosyaSistemi.Yol("mc", "mods"), "optifine.jar");
        var bildirici = new SahteBildirici();

        var cikti = Hat(ds, Motor(DosyaAdiKurali())).Calistir(Kokler(), null, bildirici);

        Assert.Equal(Sonuc.BilinenKanitYok, cikti.Sonuc);
        Assert.Empty(cikti.Kanitlar);
        Assert.Equal("GOOD", cikti.AdliKapsam);
    }

    [Fact]
    public void UcAsamaSirasiylaBildirilir()
    {
        var ds = new SahteDosyaSistemi().DosyaEkle(SahteDosyaSistemi.Yol("mc", "mods"), "a.jar");
        var bildirici = new SahteBildirici();

        Hat(ds, Motor()).Calistir(Kokler(), null, bildirici);

        Assert.Equal(new[] { "filesystem", "forensics", "runtime" }, bildirici.Asamalar);
    }

    [Fact]
    public void IlerlemeGeriGitmez()
    {
        var ds = new SahteDosyaSistemi();
        for (var i = 0; i < 120; i++) ds.DosyaEkle(SahteDosyaSistemi.Yol("mc", "mods"), $"m{i}.jar");
        var bildirici = new SahteBildirici();

        Hat(ds, Motor()).Calistir(Kokler(), null, bildirici);

        var yuzdeler = bildirici.Yuzdeler;
        Assert.Equal(yuzdeler.OrderBy(y => y), yuzdeler);
        Assert.All(yuzdeler, y => Assert.InRange(y, 0, 100));
    }

    [Fact]
    public void BilinenHashKanitUretir()
    {
        var ds = new SahteDosyaSistemi().DosyaEkle(
            SahteDosyaSistemi.Yol("mc", "mods"), "hileclient.jar",
            HileIcerigi.Length, HileIcerigi);

        var cikti = Hat(ds, Motor(BilinenHashKurali()))
            .Calistir(Kokler(), null, new SahteBildirici());

        var kanit = Assert.Single(cikti.Kanitlar);
        Assert.Equal("R-HASH", kanit.RuleId);
        Assert.Equal(KanitTuru.MevcutDosya, kanit.EvidenceType);
        Assert.True(kanit.CurrentlyPresent);
        Assert.Equal(Sonuc.Isaretli, cikti.Sonuc);
    }

    [Fact]
    public void DosyaAdiTekBasinaKanitUretmez()
    {

        var ds = new SahteDosyaSistemi().DosyaEkle(
            SahteDosyaSistemi.Yol("mc", "mods"), "hileclient.jar");

        var cikti = Hat(ds, Motor(DosyaAdiKurali()))
            .Calistir(Kokler(), null, new SahteBildirici());

        Assert.Empty(cikti.Kanitlar);
        Assert.Equal(Sonuc.BilinenKanitYok, cikti.Sonuc);
    }

    [Fact]
    public void RaporaDosyaYoluVeAdiSizmaz()
    {

        var ds = new SahteDosyaSistemi().DosyaEkle(
            SahteDosyaSistemi.Yol("mc", "mods"), "hileclient.jar",
            HileIcerigi.Length, HileIcerigi);
        var cikti = Hat(ds, Motor(BilinenHashKurali()))
            .Calistir(Kokler(), null, new SahteBildirici());

        Assert.NotEmpty(cikti.Kanitlar);
        var metin = string.Join("|", cikti.Kanitlar.Select(k =>
            $"{k.RuleId}{k.EvidenceType}{k.ArtifactCategory}{k.Confidence}{k.Severity}"));

        Assert.DoesNotContain("hileclient", metin, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(SahteDosyaSistemi.Yol("mc"), metin);
    }

    [Fact]
    public void AdliKapsamDusukseTemizSonucVerilmez()
    {
        var ds = new SahteDosyaSistemi().DosyaEkle(SahteDosyaSistemi.Yol("mc", "mods"), "optifine.jar");
        var adli = new AdliTarama(
            new SahteBirimSaglayici(AdliYardimci.Ntfs()),
            new SahteMft(_ => AdliYardimci.MftOkundu()),
            new SahteUsn(_ => AdliYardimci.Okundu(10)),
            new SabitAyricalik(AyricalikDurumu.Yonetici));

        var cikti = Hat(ds, Motor(), adli).Calistir(Kokler(), null, new SahteBildirici());

        Assert.NotEqual(Sonuc.BilinenKanitYok, cikti.Sonuc);
    }

    [Fact]
    public void TaranacakDizinYoksaSonucKararsiz()
    {

        var ds = new SahteDosyaSistemi();
        var cikti = Hat(ds, Motor()).Calistir(
            new[] { new TaramaKoku(SahteDosyaSistemi.Yol("yok"), AdayNedeni.Diger) }, null, new SahteBildirici());

        Assert.Equal(Sonuc.Kararsiz, cikti.Sonuc);
    }

    [Fact]
    public void HalenDiskteDuranDosyaSilinmisSayilmaz()
    {

        var ds = new SahteDosyaSistemi().DosyaEkle(SahteDosyaSistemi.Yol("mc", "mods"), "hile.jar");
        var adli = new AdliTarama(
            new SahteBirimSaglayici(AdliYardimci.Ntfs()),
            new SahteMft(_ => AdliYardimci.MftOkundu()),
            new SahteUsn(_ => AdliYardimci.Okundu(120,
                AdliYardimci.Kayit("hile.jar", UsnOlayi.Silme, 5))),
            new SabitAyricalik(AyricalikDurumu.Yonetici));

        var cikti = Hat(ds, Motor(DosyaAdiKurali()), adli)
            .Calistir(Kokler(), null, new SahteBildirici());

        var gecmis = cikti.Kanitlar.Where(k => k.EvidenceType == KanitTuru.GecmisKayit);
        Assert.All(gecmis, k => Assert.True(k.CurrentlyPresent));
    }

    [Fact]
    public void IptalHattiDurdurur()
    {
        var ds = new SahteDosyaSistemi().DosyaEkle(SahteDosyaSistemi.Yol("mc", "mods"), "a.jar");
        using var iptal = new CancellationTokenSource();
        iptal.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            Hat(ds, Motor()).Calistir(Kokler(), null, new SahteBildirici(),
                                      iptal: iptal.Token));
    }

    [Fact]
    public void KapsamDegerleriProtokolSemasinaUyar()
    {
        var ds = new SahteDosyaSistemi().DosyaEkle(SahteDosyaSistemi.Yol("mc", "mods"), "a.jar");
        var cikti = Hat(ds, Motor()).Calistir(Kokler(), null, new SahteBildirici());

        Assert.Contains(cikti.AdliKapsam, new[] { "GOOD", "LIMITED", "UNAVAILABLE" });
        Assert.Contains(cikti.RuntimeKapsam, new[] { "GOOD", "LIMITED", "UNAVAILABLE" });
    }
}
