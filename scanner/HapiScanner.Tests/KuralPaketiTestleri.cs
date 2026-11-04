using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HapiScanner.Core;
using HapiScanner.Core.Adli;
using HapiScanner.Core.Runtime;
using HapiScanner.Core.Tespit;

namespace HapiScanner.Tests;

internal sealed class SahteYayinci : IDisposable
{
    private readonly ECDsa _anahtar = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    internal string PublicKey => Convert.ToBase64String(_anahtar.ExportSubjectPublicKeyInfo());

    internal string Imzala(string paketJson, string? surumEtiketi = null)
    {
        var ozet = HapiScanner.Core.Dosya.Hashleme.BaytOzeti(Encoding.UTF8.GetBytes(paketJson));
        var surum = surumEtiketi ?? SurumOku(paketJson);
        var metin = Imza.KuralPaketiMetni(ozet, surum);
        return Convert.ToBase64String(_anahtar.SignData(metin, HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence));
    }

    private static string SurumOku(string json)
        => JsonDocument.Parse(json).RootElement.GetProperty("ruleset_version").GetString()!;

    public void Dispose() => _anahtar.Dispose();
}

internal static class PaketYardimci
{
    internal static string Json(string surum = "2026.11.03.1",
        string severity = "HIGH", string gostergeTipi = "SHA256", int asgari = 1)
        => $@"{{
  ""ruleset_version"": ""{surum}"",
  ""rules"": [
    {{
      ""rule_id"": ""R-1"",
      ""name"": ""ornek"",
      ""category"": ""KNOWN_CLIENT"",
      ""severity"": ""{severity}"",
      ""min_indicators"": {asgari},
      ""indicators"": [
        {{ ""type"": ""{gostergeTipi}"", ""values"": [""deadbeef""] }},
        {{ ""type"": ""PACKAGE_NAME"", ""values"": [""ornek.hile""] }}
      ]
    }}
  ]
}}";
}

public class RulesetSurumuTestleri
{
    [Theory]
    [InlineData("2026.11.03.1", true)]
    [InlineData("2026.11", true)]
    [InlineData("0.1.0-rc1", true)]
    [InlineData("latest", false)]
    [InlineData("2026", false)]
    [InlineData("", false)]
    [InlineData("2026.x.1", false)]
    public void BicimDogrulanir(string surum, bool gecerli)
        => Assert.Equal(gecerli, RulesetSurumu.GecerliMi(surum));

    [Theory]
    [InlineData("2026.11.03.1", "2026.11.03.2", true)]
    [InlineData("2026.11.03.2", "2026.11.03.1", false)]
    [InlineData("2026.11.03.1", "2026.11.03.1", false)]
    [InlineData("2026.09.30.9", "2026.11.01.1", true)]
    [InlineData("2027.01.01.1", "2026.12.31.9", false)]
    public void KucukMuBackendIleAyniDavranir(string surum, string asgari, bool beklenen)
        => Assert.Equal(beklenen, RulesetSurumu.KucukMu(surum, asgari));

    [Fact]
    public void EksikParcalarSifirSayilir()
        => Assert.False(RulesetSurumu.KucukMu("2026.11.03", "2026.11.03.0"));
}

public class KuralPaketiImzaTestleri
{
    [Fact]
    public void ImzaliPaketKabulEdilir()
    {
        using var yayinci = new SahteYayinci();
        var json = PaketYardimci.Json();
        var yukleyici = new KuralPaketiYukleyici(new[] { yayinci.PublicKey });

        var sonuc = yukleyici.Yukle(json, yayinci.Imzala(json));

        Assert.True(sonuc.Kabul);
        Assert.Equal("2026.11.03.1", sonuc.Paket!.RulesetSurumu);
        Assert.Equal(64, sonuc.PaketOzeti!.Length);
    }

    [Fact]
    public void ImzasizPaketKullanilmaz()
    {
        using var yayinci = new SahteYayinci();
        var sonuc = new KuralPaketiYukleyici(new[] { yayinci.PublicKey })
            .Yukle(PaketYardimci.Json(), imzaBase64: null);

        Assert.False(sonuc.Kabul);
        Assert.Equal(PaketRedNedeni.ImzaEksik, sonuc.Neden);
    }

    [Fact]
    public void BaskaAnahtarlaImzalananPaketReddedilir()
    {
        using var yayinci = new SahteYayinci();
        using var saldirgan = new SahteYayinci();
        var json = PaketYardimci.Json();

        var sonuc = new KuralPaketiYukleyici(new[] { yayinci.PublicKey })
            .Yukle(json, saldirgan.Imzala(json));

        Assert.Equal(PaketRedNedeni.ImzaGecersiz, sonuc.Neden);
    }

    [Fact]
    public void GovdeyeDokunulursaImzaDusur()
    {
        using var yayinci = new SahteYayinci();
        var json = PaketYardimci.Json();
        var imza = yayinci.Imzala(json);
        var oynanmis = json.Replace("deadbeef", "cafebabe");

        var sonuc = new KuralPaketiYukleyici(new[] { yayinci.PublicKey }).Yukle(oynanmis, imza);
        Assert.Equal(PaketRedNedeni.ImzaGecersiz, sonuc.Neden);
    }

    [Fact]
    public void ImzaBaskaSurumEtiketiyleYenidenKullanilamaz()
    {
        using var yayinci = new SahteYayinci();
        var eski = PaketYardimci.Json("2026.10.01.1");

        var imza = yayinci.Imzala(eski, surumEtiketi: "2026.12.01.1");

        var sonuc = new KuralPaketiYukleyici(new[] { yayinci.PublicKey }).Yukle(eski, imza);
        Assert.Equal(PaketRedNedeni.ImzaGecersiz, sonuc.Neden);
    }

    [Fact]
    public void AnahtarRotasyonuIcinBirdenFazlaYayinciAnahtariCalisir()
    {
        using var eski = new SahteYayinci();
        using var yeni = new SahteYayinci();
        var json = PaketYardimci.Json();

        var yukleyici = new KuralPaketiYukleyici(new[] { eski.PublicKey, yeni.PublicKey });
        Assert.True(yukleyici.Yukle(json, yeni.Imzala(json)).Kabul);
    }

    [Fact]
    public void BozukYayinciAnahtariDigerleriniEngellemez()
    {
        using var yayinci = new SahteYayinci();
        var json = PaketYardimci.Json();

        var yukleyici = new KuralPaketiYukleyici(new[] { "bu-base64-degil!!", yayinci.PublicKey });
        Assert.True(yukleyici.Yukle(json, yayinci.Imzala(json)).Kabul);
    }

    [Fact]
    public void BozukImzaBase64CokmezReddeder()
    {
        using var yayinci = new SahteYayinci();
        var sonuc = new KuralPaketiYukleyici(new[] { yayinci.PublicKey })
            .Yukle(PaketYardimci.Json(), "bu-base64-degil!!");

        Assert.Equal(PaketRedNedeni.ImzaGecersiz, sonuc.Neden);
    }
}

public class DowngradeKorumasiTestleri
{
    [Fact]
    public void SunucununIstedigiTabanAltindakiPaketReddedilir()
    {
        using var yayinci = new SahteYayinci();
        var json = PaketYardimci.Json("2026.10.01.1");

        var sonuc = new KuralPaketiYukleyici(new[] { yayinci.PublicKey })
            .Yukle(json, yayinci.Imzala(json), asgariSurum: "2026.11.01.1");

        Assert.Equal(PaketRedNedeni.SurumCokEski, sonuc.Neden);
    }

    [Fact]
    public void DahaOnceGorulenSurumunGerisineDonulemez()
    {
        using var yayinci = new SahteYayinci();
        var depo = new BellekSurumDeposu();
        var yukleyici = new KuralPaketiYukleyici(new[] { yayinci.PublicKey }, depo);

        var yeni = PaketYardimci.Json("2026.11.03.1");
        Assert.True(yukleyici.Yukle(yeni, yayinci.Imzala(yeni)).Kabul);

        var eski = PaketYardimci.Json("2026.09.01.1");
        var sonuc = yukleyici.Yukle(eski, yayinci.Imzala(eski));

        Assert.Equal(PaketRedNedeni.SurumGeriAlinmis, sonuc.Neden);
        Assert.Equal("2026.11.03.1", depo.SonGorulenSurum());
    }

    [Fact]
    public void AyniSurumYenidenYuklenebilir()
    {
        using var yayinci = new SahteYayinci();
        var depo = new BellekSurumDeposu();
        var yukleyici = new KuralPaketiYukleyici(new[] { yayinci.PublicKey }, depo);
        var json = PaketYardimci.Json();

        Assert.True(yukleyici.Yukle(json, yayinci.Imzala(json)).Kabul);
        Assert.True(yukleyici.Yukle(json, yayinci.Imzala(json)).Kabul);
    }

    [Fact]
    public void ReddedilenPaketSurumDeposunuKirletmez()
    {
        using var yayinci = new SahteYayinci();
        using var saldirgan = new SahteYayinci();
        var depo = new BellekSurumDeposu();
        var yukleyici = new KuralPaketiYukleyici(new[] { yayinci.PublicKey }, depo);

        var json = PaketYardimci.Json("2099.01.01.1");
        Assert.False(yukleyici.Yukle(json, saldirgan.Imzala(json)).Kabul);
        Assert.Null(depo.SonGorulenSurum());
    }

    [Fact]
    public void DosyaSurumDeposuSurumuKaliciTutar()
    {
        var yol = Path.Combine(Path.GetTempPath(), $"hapiscanner-surum-{Guid.NewGuid():N}");
        try
        {
            new DosyaSurumDeposu(yol).Kaydet("2026.11.03.1");
            Assert.Equal("2026.11.03.1", new DosyaSurumDeposu(yol).SonGorulenSurum());
        }
        finally
        {
            if (File.Exists(yol)) File.Delete(yol);
        }
    }

    [Fact]
    public void BozukSurumDosyasiHafizasizSayilir()
    {
        var yol = Path.Combine(Path.GetTempPath(), $"hapiscanner-surum-{Guid.NewGuid():N}");
        try
        {
            File.WriteAllText(yol, "latest");
            Assert.Null(new DosyaSurumDeposu(yol).SonGorulenSurum());
        }
        finally
        {
            if (File.Exists(yol)) File.Delete(yol);
        }
    }
}

public class PaketIcerikDogrulamaTestleri
{
    private static PaketYuklemeSonucu Yukle(string json)
    {
        using var yayinci = new SahteYayinci();
        return new KuralPaketiYukleyici(new[] { yayinci.PublicKey }).Yukle(json, yayinci.Imzala(json));
    }

    [Fact]
    public void TanimsizGostergeTipiReddedilir()
    {
        var sonuc = Yukle(PaketYardimci.Json(gostergeTipi: "UYDURMA_TIP"));
        Assert.Equal(PaketRedNedeni.IcerikGecersiz, sonuc.Neden);
        Assert.Contains("tanimsiz gosterge tipi", sonuc.Aciklama!);
    }

    [Fact]
    public void GecersizSeverityReddedilir()
    {
        var sonuc = Yukle(PaketYardimci.Json(severity: "FELAKET"));
        Assert.Equal(PaketRedNedeni.IcerikGecersiz, sonuc.Neden);
    }

    [Fact]
    public void GostergeSayisindanBuyukAsgariReddedilir()
    {
        var sonuc = Yukle(PaketYardimci.Json(asgari: 5));
        Assert.Equal(PaketRedNedeni.IcerikGecersiz, sonuc.Neden);
    }

    [Fact]
    public void TekrarEdenRuleIdReddedilir()
    {
        const string kural = """
            {
              "rule_id": "R-1", "name": "ornek", "category": "KNOWN_CLIENT",
              "severity": "HIGH", "min_indicators": 1,
              "indicators": [{ "type": "SHA256", "values": ["deadbeef"] }]
            }
            """;
        var json = $@"{{ ""ruleset_version"": ""2026.11.03.1"", ""rules"": [{kural},{kural}] }}";

        var sonuc = Yukle(json);
        Assert.Equal(PaketRedNedeni.IcerikGecersiz, sonuc.Neden);
        Assert.Contains("tekrar", sonuc.Aciklama!);
    }

    [Fact]
    public void GecersizSurumBicimiReddedilir()
    {
        var sonuc = Yukle(PaketYardimci.Json("latest"));
        Assert.Equal(PaketRedNedeni.SurumBicimiGecersiz, sonuc.Neden);
    }

    [Fact]
    public void BozukJsonCokmezReddeder()
    {
        using var yayinci = new SahteYayinci();
        var sonuc = new KuralPaketiYukleyici(new[] { yayinci.PublicKey })
            .Yukle("{bozuk", "AAAA");

        Assert.Equal(PaketRedNedeni.IcerikGecersiz, sonuc.Neden);
    }

    [Fact]
    public void DepodakiOrnekPaketGecerliBicimde()
    {
        var yol = Path.Combine(AppContext.BaseDirectory, "../../../../../rules/ornek-kurallar.json");
        if (!File.Exists(yol)) return;

        var paket = KuralMotoru.PaketCoz(File.ReadAllText(yol));
        Assert.Null(KuralPaketiYukleyici.Dogrula(paket));
    }
}

public class SeverityModeliTestleri
{
    [Theory]
    [InlineData("LOW", true)]
    [InlineData("MEDIUM", true)]
    [InlineData("HIGH", true)]
    [InlineData("CRITICAL", true)]
    [InlineData("low", false)]
    [InlineData("FELAKET", false)]
    public void SeverityDogrulanir(string deger, bool gecerli)
        => Assert.Equal(gecerli, Severity.GecerliMi(deger));

    [Fact]
    public void EnAgirSeverityKazanir()
    {
        Assert.Equal("CRITICAL", Severity.EnAgir("LOW", "CRITICAL"));
        Assert.Equal("HIGH", Severity.EnAgir("HIGH", "MEDIUM"));
        Assert.Equal("MEDIUM", Severity.EnAgir(null, "MEDIUM"));
    }

    [Fact]
    public void SeverityVeGuvenAyriEksen()
    {

        var eslesme = new Eslesme("R-1", "ornek", "KNOWN_CLIENT", Severity.Kritik,
            Guven.Dusuk, 20, new[] { GostergeTipi.DosyaAdi });

        Assert.False(eslesme.IsaretlenirMi);
        Assert.Equal("LOW", GuvenAdi.Protokol(eslesme.Guven));
    }
}

public class KanitUreticiTestleri
{
    private static Eslesme Eslesme(string ruleId, Guven guven, string severity = "HIGH")
        => new(ruleId, "ornek", "KNOWN_CLIENT", severity, guven, 100,
            new[] { GostergeTipi.Sha256 });

    [Fact]
    public void MevcutDosyaKanitiUretilir()
    {
        var kanit = Assert.Single(KanitUretici.Uret(new[]
        {
            new Bulgu(Eslesme("R-1", Guven.Yuksek), KanitTuru.MevcutDosya, SuAndaVar: true),
        }, "2026.11.03.1"));

        Assert.Equal("R-1", kanit.RuleId);
        Assert.Equal(KanitTuru.MevcutDosya, kanit.EvidenceType);
        Assert.Equal("HIGH", kanit.Confidence);
        Assert.True(kanit.CurrentlyPresent);
        Assert.Equal("2026.11.03.1", kanit.RuleVersion);
    }

    [Fact]
    public void GecmisKanitiZamanPenceresiTasir()
    {
        var ilk = new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero);
        var son = new DateTimeOffset(2026, 9, 15, 22, 30, 0, TimeSpan.Zero);

        var kanit = Assert.Single(KanitUretici.Uret(new[]
        {
            new Bulgu(Eslesme("R-1", Guven.Orta), KanitTuru.GecmisKayit, false, ilk, son),
        }, "2026.11.03.1"));

        Assert.Equal("2026-08-01T10:00:00Z", kanit.FirstSeen);
        Assert.Equal("2026-09-15T22:30:00Z", kanit.LastSeen);
        Assert.False(kanit.CurrentlyPresent);
    }

    [Fact]
    public void AyniKuralCokDosyadaTekKanitaBirlesir()
    {
        var kanitlar = KanitUretici.Uret(new[]
        {
            new Bulgu(Eslesme("R-1", Guven.Dusuk, "LOW"), KanitTuru.MevcutDosya, false),
            new Bulgu(Eslesme("R-1", Guven.Yuksek, "CRITICAL"), KanitTuru.MevcutDosya, true),
        }, "2026.11.03.1");

        var kanit = Assert.Single(kanitlar);
        Assert.Equal("HIGH", kanit.Confidence);
        Assert.Equal("CRITICAL", kanit.Severity);
        Assert.True(kanit.CurrentlyPresent);
    }

    [Fact]
    public void AyniKuralFarkliKanitTurundeAyriKalir()
    {
        var kanitlar = KanitUretici.Uret(new[]
        {
            new Bulgu(Eslesme("R-1", Guven.Yuksek), KanitTuru.MevcutDosya, true),
            new Bulgu(Eslesme("R-1", Guven.Orta), KanitTuru.GecmisKayit, false),
        }, "2026.11.03.1");

        Assert.Equal(2, kanitlar.Count);
    }

    [Fact]
    public void KanitlarSeveritySirasindaGelir()
    {
        var kanitlar = KanitUretici.Uret(new[]
        {
            new Bulgu(Eslesme("R-1", Guven.Orta, "LOW"), KanitTuru.MevcutDosya, true),
            new Bulgu(Eslesme("R-2", Guven.Orta, "CRITICAL"), KanitTuru.MevcutDosya, true),
        }, "2026.11.03.1");

        Assert.Equal("R-2", kanitlar[0].RuleId);
    }

    [Fact]
    public void KanitJsonundaYolVeyaDosyaAdiAlaniYok()
    {
        var kanit = Assert.Single(KanitUretici.Uret(new[]
        {
            new Bulgu(Eslesme("R-1", Guven.Yuksek), KanitTuru.MevcutDosya, true),
        }, "2026.11.03.1"));

        var json = JsonSerializer.Serialize(kanit);
        foreach (var yasak in new[] { "path", "file_name", "filename", "content", "yol" })
            Assert.DoesNotContain(yasak, json, StringComparison.OrdinalIgnoreCase);
    }
}

public class AdliGostergeBaglamaTestleri
{
    private static GecmisIz Iz(bool silinmis, bool yenidenAdlandirma = false, bool mevcut = false)
    {
        var olaylar = UsnOlayi.Olusturma;
        if (silinmis) olaylar |= UsnOlayi.Silme;
        if (yenidenAdlandirma) olaylar |= UsnOlayi.YenidenAdlandirmaYeni;

        return new GecmisIz("hile.jar", "0123456789abcdef",
            DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow.AddDays(-2),
            mevcut, olaylar, 2);
    }

    [Fact]
    public void SilinmisDosyaGecmisGostergesiUretir()
    {
        var set = AdliGostergeler.Doldur(Iz(silinmis: true));

        Assert.True(set.Icerir(GostergeTipi.UsnOlayi, UsnKanitTuru.Silme));
        Assert.True(set.Icerir(GostergeTipi.MftKaniti, "DELETED_BUT_RECORDED"));
        Assert.True(set.Icerir(GostergeTipi.DosyaAdi, "hile.jar"));
    }

    [Fact]
    public void HalenVarOlanDosyaSilinmeKanitiUretmez()
    {
        var set = AdliGostergeler.Doldur(Iz(silinmis: true, mevcut: true));
        Assert.False(set.Icerir(GostergeTipi.MftKaniti, "DELETED_BUT_RECORDED"));
    }

    [Fact]
    public void YenidenAdlandirmaIsaretlenir()
    {
        var set = AdliGostergeler.Doldur(Iz(false, yenidenAdlandirma: true));
        Assert.True(set.Icerir(GostergeTipi.MftKaniti, "RENAMED"));
    }

    [Fact]
    public void GecmisGostergesiTekBasinaIsaretlemeUretmez()
    {

        var kural = new Kural("R-USN", "silinmis jar", "HISTORY", "HIGH", 2,
            new List<Gosterge>
            {
                new(GostergeTipi.UsnOlayi, new List<string> { UsnKanitTuru.Silme }),
                new(GostergeTipi.DosyaAdi, new List<string> { "hile" }),
            });

        var motor = new KuralMotoru(new KuralPaketi("2026.11.03.1", new List<Kural> { kural }));
        var eslesme = Assert.Single(motor.Degerlendir(AdliGostergeler.Doldur(Iz(true))));

        Assert.False(eslesme.IsaretlenirMi);
        Assert.Equal(Guven.Orta, eslesme.Guven);
    }

    [Fact]
    public void BilinenHashIleBirlesirseIsaretlenir()
    {
        var kural = new Kural("R-COMBO", "bilinen istemci + silme", "KNOWN_CLIENT", "CRITICAL", 2,
            new List<Gosterge>
            {
                new(GostergeTipi.Sha256, new List<string> { "deadbeef" }),
                new(GostergeTipi.UsnOlayi, new List<string> { UsnKanitTuru.Silme }),
            });

        var set = AdliGostergeler.Doldur(Iz(true));
        set.Ekle(GostergeTipi.Sha256, "deadbeef");

        var motor = new KuralMotoru(new KuralPaketi("2026.11.03.1", new List<Kural> { kural }));
        var eslesme = Assert.Single(motor.Degerlendir(set));

        Assert.True(eslesme.IsaretlenirMi);
        Assert.Equal(Guven.Yuksek, eslesme.Guven);
    }
}

public class ProtokolUyumTestleri
{

    private static readonly string[] Izinli = { "GOOD", "LIMITED", "UNAVAILABLE" };

    [Theory]
    [InlineData(Kapsam.Iyi)]
    [InlineData(Kapsam.Sinirli)]
    [InlineData(Kapsam.Yok)]
    public void AdliKapsamProtokolDegeriSemayaUyar(Kapsam _)
    {
        foreach (var kapsam in Kapsamlar())
            Assert.Contains(kapsam.ProtokolDegeri, Izinli);
    }

    [Fact]
    public void RuntimeKapsamProtokolDegeriSemayaUyar()
    {
        foreach (var kapsam in Kapsamlar())
            Assert.Contains(kapsam.RuntimeProtokolDegeri, Izinli);
    }

    [Fact]
    public void InsanOkunurOzetteFullKullanilir()
    {
        var kapsam = KapsamUret(RuntimeErisimi.Tam);
        Assert.Equal("FULL", kapsam.RuntimeInsanOkunur);
        Assert.Contains("Runtime erisim: FULL", kapsam.Ozet());
    }

    private static IEnumerable<AdliKapsam> Kapsamlar()
    {
        foreach (var runtime in new[] { RuntimeErisimi.Tam, RuntimeErisimi.Sinirli, RuntimeErisimi.Yok })
            yield return KapsamUret(runtime);
    }

    private static AdliKapsam KapsamUret(RuntimeErisimi runtime)
    {
        var birim = new BirimBilgisi(@"C:\", "NTFS");
        return new AdliKapsamHesaplayici().Hesapla(new[]
        {
            new BirimDegerlendirmesi(birim,
                new MftOkumaSonucu(MftSonucTuru.ErisimReddedildi),
                UsnOkumaSonucu.ErisimYok(birim.Kok)),
        }, AyricalikDurumu.Standart, runtime);
    }
}

public class CaprazDilPaketImzasiTestleri
{
    private const string Yol = "../../../../test-vectors/kural_paketi_vektoru.json";

    private static JsonElement? Vektor()
    {
        var tam = Path.Combine(AppContext.BaseDirectory, Yol);
        if (!File.Exists(tam)) return null;
        return JsonDocument.Parse(File.ReadAllText(tam)).RootElement.Clone();
    }

    [Fact]
    public void PythonImzasiScannerTarafindaDogrulanir()
    {
        if (Vektor() is not { } v) return;

        var paketJson = v.GetProperty("package_json").GetString()!;
        var imza = v.GetProperty("signature").GetString()!;
        var anahtar = v.GetProperty("publisher_public_key").GetString()!;

        var sonuc = new KuralPaketiYukleyici(new[] { anahtar }).Yukle(paketJson, imza);

        Assert.True(sonuc.Kabul, sonuc.Aciklama);
        Assert.Equal(v.GetProperty("package_sha256").GetString(), sonuc.PaketOzeti);
        Assert.Equal(v.GetProperty("ruleset_version").GetString(), sonuc.Paket!.RulesetSurumu);
    }

    [Fact]
    public void VektordekiPaketIcerikDogrulamasindanGecer()
    {
        if (Vektor() is not { } v) return;

        var paket = KuralMotoru.PaketCoz(v.GetProperty("package_json").GetString()!);
        Assert.Null(KuralPaketiYukleyici.Dogrula(paket));
    }

    [Fact]
    public void VektorP256BildiriyorVeBaglamIceriyor()
    {
        if (Vektor() is not { } v) return;

        var algoritma = v.GetProperty("algorithm").GetString()!;
        Assert.Contains("ECDSA P-256", algoritma);
        Assert.Contains("ruleset|", algoritma);
    }
}
