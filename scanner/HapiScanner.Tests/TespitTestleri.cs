using System.IO.Compression;
using System.Text;
using HapiScanner.Core.Dosya;
using HapiScanner.Core.Tespit;

namespace HapiScanner.Tests;

public class AdaySuzgeciTestleri
{
    private readonly AdaySuzgeci _suzgec = new();

    private static DosyaBilgisi Dosya(string ad, long boyut)
        => new($"/kok/{ad}", ad, boyut, DateTimeOffset.UtcNow);

    [Theory]
    [InlineData("hile.jar")]
    [InlineData("yardimci.exe")]
    [InlineData("kutuphane.dll")]
    [InlineData("eski.litemod")]
    public void IlgiliUzantilarAday(string ad) => Assert.True(_suzgec.AdayMi(Dosya(ad, 1024)));

    [Theory]
    [InlineData("tatil.mp4")]
    [InlineData("windows.iso")]
    [InlineData("yedek.bak")]
    [InlineData("foto.jpg")]
    [InlineData("belge.pdf")]
    public void BuyukVeAlakasizDosyalarAdayDegil(string ad)
        => Assert.False(_suzgec.AdayMi(Dosya(ad, 50L * 1024 * 1024)));

    [Fact]
    public void CokBuyukJarAdayDegil()
        => Assert.False(_suzgec.AdayMi(Dosya("sisirilmis.jar", 400L * 1024 * 1024)));

    [Fact]
    public void BosDosyaAdayDegil() => Assert.False(_suzgec.AdayMi(Dosya("bos.jar", 0)));

    [Fact]
    public void UzantisiDegistirilmisDosyaAday()
    {

        Assert.True(_suzgec.AdayMi(Dosya("odev.txt", 2 * 1024 * 1024)));
        Assert.True(_suzgec.AdayMi(Dosya("uzantisiz", 2 * 1024 * 1024)));
    }

    [Fact]
    public void VideoDosyasiHashlenmez()
        => Assert.False(_suzgec.HashlenebilirMi(Dosya("film.mkv", 2L * 1024 * 1024 * 1024)));
}

public class KuralMotoruTestleri
{
    private static KuralPaketi Paket(params Kural[] kurallar)
        => new("2026.11.03.1", kurallar.ToList());

    private static Kural Kural1(int asgari, params Gosterge[] gostergeler)
        => new("TEST_0001", "TestKurali", "KNOWN_CLIENT", "HIGH", asgari, gostergeler.ToList());

    private static Gosterge G(string tip, params string[] degerler)
        => new(tip, degerler.ToList());

    [Fact]
    public void HashVePaketEslesirseYuksekGuven()
    {
        var motor = new KuralMotoru(Paket(Kural1(2,
            G(GostergeTipi.Sha256, "abc"),
            G(GostergeTipi.PaketAdi, "ornek.hile"))));

        var set = new GostergeSeti();
        set.Ekle(GostergeTipi.Sha256, "abc");
        set.Ekle(GostergeTipi.PaketAdi, "ornek.hile.Ana");

        var eslesme = Assert.Single(motor.Degerlendir(set));
        Assert.Equal(Guven.Yuksek, eslesme.Guven);
        Assert.True(eslesme.IsaretlenirMi);
    }

    [Fact]
    public void C_DosyaAdiDegisseBileHashYakalar()
    {
        var motor = new KuralMotoru(Paket(Kural1(1, G(GostergeTipi.Sha256, "abc"))));
        var set = new GostergeSeti();
        set.Ekle(GostergeTipi.DosyaAdi, "odev.jar");
        set.Ekle(GostergeTipi.Sha256, "abc");

        var eslesme = Assert.Single(motor.Degerlendir(set));
        Assert.Equal(Guven.Yuksek, eslesme.Guven);
    }

    [Fact]
    public void C_DosyaAdiDegisseBileYapiYakalar()
    {
        var motor = new KuralMotoru(Paket(Kural1(2,
            G(GostergeTipi.SinifAdi, "ornek.hile.Ana"),
            G(GostergeTipi.ManifestIsareti, "OrnekIstemci"))));

        var set = new GostergeSeti();
        set.Ekle(GostergeTipi.DosyaAdi, "matematik.jar");
        set.Ekle(GostergeTipi.SinifAdi, "ornek.hile.Ana");
        set.Ekle(GostergeTipi.ManifestIsareti, "OrnekIstemci");

        Assert.NotEmpty(motor.Degerlendir(set));
    }

    [Fact]
    public void AsgariGostergeSayisiAltindaEslesmeYok()
    {
        var motor = new KuralMotoru(Paket(Kural1(2,
            G(GostergeTipi.Sha256, "abc"),
            G(GostergeTipi.PaketAdi, "ornek.hile"))));

        var set = new GostergeSeti();
        set.Ekle(GostergeTipi.Sha256, "abc");

        Assert.Empty(motor.Degerlendir(set));
    }

    [Fact]
    public void YalnizDosyaAdiEslesmesiIsaretlenmez()
    {

        var motor = new KuralMotoru(Paket(Kural1(1, G(GostergeTipi.DosyaAdi, "hile"))));
        var set = new GostergeSeti();
        set.Ekle(GostergeTipi.DosyaAdi, "hile-indirildi.jar");

        var eslesmeler = motor.Degerlendir(set);
        Assert.All(eslesmeler, e => Assert.False(e.IsaretlenirMi));
        Assert.All(eslesmeler, e => Assert.NotEqual(Guven.Yuksek, e.Guven));
    }

    [Fact]
    public void SadeceZayifGostergelerYuksekGuvenUretemez()
    {
        var motor = new KuralMotoru(Paket(Kural1(2,
            G(GostergeTipi.DosyaAdi, "hile"),
            G(GostergeTipi.GenelMetin, "bypass"))));

        var set = new GostergeSeti();
        set.Ekle(GostergeTipi.DosyaAdi, "hile.jar");
        set.Ekle(GostergeTipi.GenelMetin, "bypass");

        Assert.All(motor.Degerlendir(set), e => Assert.NotEqual(Guven.Yuksek, e.Guven));
    }

    [Fact]
    public void CokSayidaZayifGostergeBileIsaretlemeUretemez()
    {

        var gostergeler = Enumerable.Range(0, 12)
            .Select(i => G(GostergeTipi.DosyaAdi, $"zayif{i}"))
            .ToArray();
        var motor = new KuralMotoru(Paket(Kural1(12, gostergeler)));

        var set = new GostergeSeti();
        for (var i = 0; i < 12; i++) set.Ekle(GostergeTipi.DosyaAdi, $"zayif{i}-dosya.jar");

        var eslesme = Assert.Single(motor.Degerlendir(set));
        Assert.True(eslesme.Puan >= 100, "bu test ancak puan esigi asilinca anlamli");
        Assert.NotEqual(Guven.Yuksek, eslesme.Guven);
        Assert.False(eslesme.IsaretlenirMi);
    }

    [Fact]
    public void TekGucluGostergeEklenirseIsaretlenebilir()
    {

        var motor = new KuralMotoru(Paket(Kural1(2,
            G(GostergeTipi.Sha256, "abc"),
            G(GostergeTipi.DosyaAdi, "zayif"))));

        var set = new GostergeSeti();
        set.Ekle(GostergeTipi.Sha256, "abc");
        set.Ekle(GostergeTipi.DosyaAdi, "zayif.jar");

        Assert.Equal(Guven.Yuksek, Assert.Single(motor.Degerlendir(set)).Guven);
    }

    [Fact]
    public void IzinListesiMesruModuKorur()
    {
        var paket = new KuralPaketi("2026.11.03.1",
            new List<Kural> { Kural1(1, G(GostergeTipi.YapisalIsaret, "IS_MOD")) },
            new List<Gosterge> { G(GostergeTipi.ModMetadata, "sodium") });
        var motor = new KuralMotoru(paket);

        var set = new GostergeSeti();
        set.Ekle(GostergeTipi.YapisalIsaret, "IS_MOD");
        set.Ekle(GostergeTipi.ModMetadata, "sodium");

        Assert.True(motor.IzinliMi(set));
        Assert.Empty(motor.Degerlendir(set));
    }

    [Fact]
    public void ManifestAnahtarliEslesme()
    {
        var motor = new KuralMotoru(Paket(Kural1(1,
            new Gosterge(GostergeTipi.ManifestIsareti,
                new List<string> { "OrnekIstemci" }, "Implementation-Title"))));

        var set = new GostergeSeti();
        set.Ekle(GostergeTipi.ManifestIsareti, "Implementation-Title=OrnekIstemci");

        Assert.NotEmpty(motor.Degerlendir(set));
    }

    [Fact]
    public void PaketAdiOnekEslesmesi()
    {
        var motor = new KuralMotoru(Paket(Kural1(1, G(GostergeTipi.PaketAdi, "ornek.hile"))));
        var set = new GostergeSeti();
        set.Ekle(GostergeTipi.PaketAdi, "ornek.hile.modul.alt");
        Assert.NotEmpty(motor.Degerlendir(set));
    }

    [Fact]
    public void AlakasizDosyaEslesmez()
    {
        var motor = new KuralMotoru(Paket(Kural1(1, G(GostergeTipi.Sha256, "abc"))));
        var set = new GostergeSeti();
        set.Ekle(GostergeTipi.Sha256, "bambaska");
        Assert.Empty(motor.Degerlendir(set));
    }

    [Fact]
    public void EslesmelerPuanaGoreSirali()
    {
        var paket = new KuralPaketi("2026.11.03.1", new List<Kural>
        {
            new("ZAYIF", "Zayif", "X", "LOW", 1,
                new List<Gosterge> { G(GostergeTipi.DosyaAdi, "hile") }),
            new("GUCLU", "Guclu", "X", "HIGH", 1,
                new List<Gosterge> { G(GostergeTipi.Sha256, "abc") }),
        });
        var motor = new KuralMotoru(paket);

        var set = new GostergeSeti();
        set.Ekle(GostergeTipi.DosyaAdi, "hile.jar");
        set.Ekle(GostergeTipi.Sha256, "abc");

        var eslesmeler = motor.Degerlendir(set);
        Assert.Equal("GUCLU", eslesmeler[0].RuleId);
    }

    [Fact]
    public void OrnekKuralPaketiCozulur()
    {
        var yol = Path.GetFullPath("../../../../../rules/ornek-kurallar.json");
        Assert.True(File.Exists(yol), $"kural dosyasi yok: {yol}");

        var paket = KuralMotoru.PaketCoz(File.ReadAllText(yol));
        Assert.Equal("2026.11.03.1", paket.RulesetSurumu);
        Assert.NotEmpty(paket.Kurallar);
        Assert.All(paket.Kurallar, k => Assert.True(k.AsgariGosterge >= 1));
    }

    [Fact]
    public void SurumsuzPaketReddedilir()
    {
        Assert.Throws<InvalidDataException>(() =>
            KuralMotoru.PaketCoz("""{"ruleset_version":"","rules":[]}"""));
    }

    [Fact]
    public void AsgariGostergeSifirOlanKuralReddedilir()
    {
        Assert.Throws<InvalidDataException>(() => KuralMotoru.PaketCoz("""
        {"ruleset_version":"1","rules":[{"rule_id":"X","name":"X","category":"X",
         "severity":"LOW","min_indicators":0,"indicators":[{"type":"SHA256","values":["a"]}]}]}
        """));
    }

    [Fact]
    public void GostergesizKuralReddedilir()
    {
        Assert.Throws<InvalidDataException>(() => KuralMotoru.PaketCoz("""
        {"ruleset_version":"1","rules":[{"rule_id":"X","name":"X","category":"X",
         "severity":"LOW","min_indicators":1,"indicators":[]}]}
        """));
    }

    [Fact]
    public void BozukJsonPaketCokmeyeYolAcmaz()
    {
        Assert.ThrowsAny<Exception>(() => KuralMotoru.PaketCoz("{bozuk"));
    }
}

public class GostergeCikariciTestleri
{
    private readonly GostergeCikarici _cikarici = new();

    private static DosyaBilgisi Dosya(string ad, long boyut = 4096)
        => new($"/kok/{ad}", ad, boyut, DateTimeOffset.UtcNow);

    private static MemoryStream JarAkisi(params (string Ad, string Icerik)[] girdiler)
    {
        var bellek = new MemoryStream();
        using (var arsiv = new ZipArchive(bellek, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (ad, icerik) in girdiler)
            {
                var girdi = arsiv.CreateEntry(ad);
                using var akis = girdi.Open();
                using var yazici = new StreamWriter(akis);
                yazici.Write(icerik);
            }
        }
        bellek.Position = 0;
        return bellek;
    }

    [Fact]
    public void JardanSinifVeHashCikarilir()
    {
        using var jar = JarAkisi(("com/ornek/Ana.class", "x"));
        var set = _cikarici.Cikar(Dosya("mod.jar", jar.Length), jar);

        Assert.True(set.Icerir(GostergeTipi.SinifAdi, "com.ornek.Ana"));
        Assert.True(set.Icerir(GostergeTipi.JarMetadata, "VALID_ARCHIVE"));
        Assert.Single(set.Degerler(GostergeTipi.Sha256));
    }

    [Fact]
    public void UzantiUyusmazligiYakalanir()
    {

        using var jar = JarAkisi(("com/ornek/Ana.class", "x"));
        var set = _cikarici.Cikar(Dosya("odev.txt", jar.Length), jar);
        Assert.True(set.Icerir(GostergeTipi.YapisalIsaret, "EXTENSION_MISMATCH"));
    }

    [Fact]
    public void JavaAgentYapisalIsaretOlarakGecer()
    {
        using var jar = JarAkisi(("META-INF/MANIFEST.MF", "Premain-Class: a.B\r\n"));
        var set = _cikarici.Cikar(Dosya("ajan.jar", jar.Length), jar);
        Assert.True(set.Icerir(GostergeTipi.YapisalIsaret, "JAVA_AGENT"));
    }

    [Fact]
    public void GomuluNativeKutuphaneIsaretlenir()
    {
        using var jar = JarAkisi(("native/yardimci.dll", "MZ"));
        var set = _cikarici.Cikar(Dosya("mod.jar", jar.Length), jar);
        Assert.True(set.Icerir(GostergeTipi.YapisalIsaret, "EMBEDDED_NATIVE"));
    }

    [Fact]
    public void GostergeSetindeDosyaYoluYok()
    {
        using var jar = JarAkisi(("com/ornek/Ana.class", "x"));
        var dosya = new DosyaBilgisi("/home/ahmet/Belgeler/gizli/mod.jar", "mod.jar",
                                     jar.Length, DateTimeOffset.UtcNow);
        var set = _cikarici.Cikar(dosya, jar);

        foreach (var tip in set.Tipler)
            foreach (var deger in set.Degerler(tip))
            {
                Assert.DoesNotContain("/home/ahmet", deger);
                Assert.DoesNotContain("Belgeler", deger);
            }

        Assert.Equal(16, set.OrtukKimlik.Length);
        Assert.DoesNotContain("ahmet", set.OrtukKimlik);
    }

    [Fact]
    public void OrtukKimlikAyniYolIcinKararli()
    {
        var a = new DosyaBilgisi("/kok/x.jar", "x.jar", 10, DateTimeOffset.UtcNow);
        var b = new DosyaBilgisi("/KOK/X.JAR", "x.jar", 10, DateTimeOffset.UtcNow);
        Assert.Equal(a.OrtukKimlik(), b.OrtukKimlik());
    }

    [Fact]
    public void BozukDosyaCikarmayiCokertmez()
    {
        using var cop = new MemoryStream(Encoding.UTF8.GetBytes("ne zip ne pe"));
        var set = _cikarici.Cikar(Dosya("belirsiz.bin", cop.Length), cop);

        Assert.True(set.Icerir(GostergeTipi.DosyaAdi, "belirsiz.bin"));
    }

    [Fact]
    public void TamHatBastanSona()
    {

        using var jar = JarAkisi(
            ("com/ornek/hile/Ana.class", "x"),
            ("META-INF/MANIFEST.MF", "Implementation-Title: OrnekIstemci\r\n"));

        var dosya = Dosya("odev.jar", jar.Length);
        Assert.True(new AdaySuzgeci().AdayMi(dosya));

        var set = _cikarici.Cikar(dosya, jar);

        var motor = new KuralMotoru(new KuralPaketi("2026.11.03.1", new List<Kural>
        {
            new("TEST_0001", "OrnekIstemci", "KNOWN_CLIENT", "HIGH", 2, new List<Gosterge>
            {
                new(GostergeTipi.PaketAdi, new List<string> { "com.ornek.hile" }),
                new(GostergeTipi.ManifestIsareti, new List<string> { "OrnekIstemci" }),
            }),
        }));

        var eslesme = Assert.Single(motor.Degerlendir(set));
        Assert.Equal("TEST_0001", eslesme.RuleId);
        Assert.True(eslesme.IsaretlenirMi);
    }
}
