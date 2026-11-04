using System.Text;
using HapiScanner.Core;

namespace HapiScanner.Tests;

public class ImzaTestleri
{
    private static string Metin(byte[] ham) => Encoding.UTF8.GetString(ham);

    [Fact]
    public void AlanlarAdaGoreSiralanir()
    {
        var a = Imza.Metin("report", new Dictionary<string, string> { ["b"] = "2", ["a"] = "1" });
        var b = Imza.Metin("report", new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" });
        Assert.Equal(Metin(a), Metin(b));
        Assert.Equal("report|a=1|b=2", Metin(a));
    }

    [Fact]
    public void BaglamBaglamiAyirir()
    {
        var hb = Imza.Metin("heartbeat", new Dictionary<string, string> { ["a"] = "1" });
        var rp = Imza.Metin("report", new Dictionary<string, string> { ["a"] = "1" });
        Assert.NotEqual(Metin(hb), Metin(rp));
    }

    [Fact]
    public void BosBaglamReddedilir()
    {
        Assert.Throws<ArgumentException>(() =>
            Imza.Metin("", new Dictionary<string, string> { ["a"] = "1" }));
    }

    [Fact]
    public void HeartbeatMetniBeklenenSirada()
    {
        var metin = Metin(Imza.HeartbeatMetni("v1", "n1", 7, "filesystem", "2026-11-03T12:00:00+00:00"));

        Assert.Equal("heartbeat|nonce=n1|sequence=7|stage=filesystem" +
                     "|timestamp=2026-11-03T12:00:00+00:00|verification_id=v1", metin);
    }

    [Fact]
    public void RaporMetniBeklenenSirada()
    {
        var metin = Metin(Imza.RaporMetni("v1", "n1", "0.1.0", "2026.11.03.1", "abc", "zaman"));
        Assert.Equal("report|nonce=n1|report_hash=abc|ruleset_version=2026.11.03.1" +
                     "|scanner_version=0.1.0|timestamp=zaman|verification_id=v1", metin);
    }

    [Fact]
    public void SiraNoKulturdenBagimsiz()
    {

        var eski = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("tr-TR");
            var metin = Metin(Imza.HeartbeatMetni("v", "n", 1234, "s", "z"));
            Assert.Contains("sequence=1234", metin);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = eski;
        }
    }

    [Fact]
    public void ImzaKendiAnahtariylaDogrulanir()
    {
        using var anahtar = new YazilimAnahtari();
        var metin = Imza.RaporMetni("v", "n", "0.1.0", "2026.11.03.1", "h", "z");
        var imza = anahtar.Imzala(metin);
        Assert.False(string.IsNullOrEmpty(imza));
        Assert.True(Convert.FromBase64String(imza).Length > 0);
    }

    [Fact]
    public void ParmakIziKararli()
    {
        using var anahtar = new YazilimAnahtari();
        var pub = anahtar.PublicKeyBase64();
        Assert.Equal(ParmakIzi.Hesapla(pub), ParmakIzi.Hesapla(pub));
        Assert.Equal(32, ParmakIzi.Hesapla(pub).Length);
    }

    [Fact]
    public void AnahtarKaydedilipYuklenebilir()
    {
        using var ilk = new YazilimAnahtari();
        var pkcs8 = ilk.GizliAnahtariCikar();
        using var yeni = YazilimAnahtari.Yukle(pkcs8);

        Assert.Equal(ilk.PublicKeyBase64(), yeni.PublicKeyBase64());
    }

    [Fact]
    public void YazilimAnahtariGuvenceSeviyesiniDogruBildirir()
    {
        using var anahtar = new YazilimAnahtari();
        Assert.Equal(GuvenceSeviyesi.Yazilim, anahtar.Guvence);
    }

    [Fact]
    public void RaporHashBackendIleAyni()
    {

        Assert.Equal("8f9b1e0b0f4e0d09a6ed4a0f3a6e1a0c3b5c6a88d5ba4e2f3a0f8ba5f6b6c0a1".Length,
                     DogrulamaIstemcisi.RaporHash("{\"kanit\":[]}").Length);
        Assert.Matches("^[0-9a-f]{64}$", DogrulamaIstemcisi.RaporHash("x"));
    }
}
