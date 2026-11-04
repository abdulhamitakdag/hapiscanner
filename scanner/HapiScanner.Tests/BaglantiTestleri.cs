using HapiScanner.Core;

namespace HapiScanner.Tests;

public class BaglantiTestleri
{
    private static readonly string[] Endpointler = { "203.0.113.5:25565" };

    private static AdayProcess Java(int pid = 100) => new(pid, "javaw.exe", "-Xmx4G net.minecraft.client.main.Main");
    private static TcpBaglantisi Bagli(int pid = 100, string adres = "203.0.113.5", int port = 25565)
        => new(pid, adres, port, "ESTABLISHED");

    [Fact]
    public void JavawAdayKabulEdilir()
    {
        Assert.True(BaglantiEslestirici.AdayMi(new AdayProcess(1, "javaw.exe", null)));
        Assert.True(BaglantiEslestirici.AdayMi(new AdayProcess(1, "java", null)));
    }

    [Fact]
    public void YenidenAdlandirilmisJavaKomutSatirindanYakalanir()
    {

        var p = new AdayProcess(1, "oyun.exe", "-Xmx4G -cp fabric-loader net.minecraft.client.main.Main");
        Assert.True(BaglantiEslestirici.AdayMi(p));
    }

    [Fact]
    public void AlakasizProcessAdayDegil()
    {
        Assert.False(BaglantiEslestirici.AdayMi(new AdayProcess(1, "chrome.exe", "--profile")));
        Assert.False(BaglantiEslestirici.AdayMi(new AdayProcess(1, "notepad.exe", null)));
    }

    [Fact]
    public void DogruProcessVeBaglantiVarsaDogrulanabilir()
    {
        var sonuc = BaglantiEslestirici.Eslestir(
            new[] { Java() }, new[] { Bagli() }, Endpointler);
        Assert.Equal(ProcessDurumu.Bulundu, sonuc.Process);
        Assert.Equal(BaglantiDurumu.Bulundu, sonuc.Baglanti);
        Assert.True(sonuc.DogrulanabilirMi);
    }

    [Fact]
    public void K_MinecraftYokkenDogrulanamaz()
    {
        var sonuc = BaglantiEslestirici.Eslestir(
            Array.Empty<AdayProcess>(), Array.Empty<TcpBaglantisi>(), Endpointler);
        Assert.Equal(ProcessDurumu.Bulunamadi, sonuc.Process);
        Assert.False(sonuc.DogrulanabilirMi);
    }

    [Fact]
    public void K_MinecraftAcikAmaBaskaSunucuyaBagliysaDogrulanamaz()
    {

        var sonuc = BaglantiEslestirici.Eslestir(
            new[] { Java() },
            new[] { Bagli(adres: "198.51.100.9") },
            Endpointler);
        Assert.Equal(ProcessDurumu.Bulundu, sonuc.Process);
        Assert.Equal(BaglantiDurumu.Bulunamadi, sonuc.Baglanti);
        Assert.False(sonuc.DogrulanabilirMi);
    }

    [Fact]
    public void BaglantiBaskaProcesseAitseSayilmaz()
    {

        var sonuc = BaglantiEslestirici.Eslestir(
            new[] { Java(100) }, new[] { Bagli(pid: 999) }, Endpointler);
        Assert.False(sonuc.DogrulanabilirMi);
    }

    [Fact]
    public void EstablishedOlmayanBaglantiSayilmaz()
    {
        var sonuc = BaglantiEslestirici.Eslestir(
            new[] { Java() },
            new[] { new TcpBaglantisi(100, "203.0.113.5", 25565, "TIME_WAIT") },
            Endpointler);
        Assert.Equal(BaglantiDurumu.Bulunamadi, sonuc.Baglanti);
    }

    [Fact]
    public void YanlisPortSayilmaz()
    {
        var sonuc = BaglantiEslestirici.Eslestir(
            new[] { Java() }, new[] { Bagli(port: 25566) }, Endpointler);
        Assert.Equal(BaglantiDurumu.Bulunamadi, sonuc.Baglanti);
    }

    [Fact]
    public void X_CoklyInstancedaDogruProcessBulunur()
    {
        var sonuc = BaglantiEslestirici.Eslestir(
            new[] { Java(100), Java(200), Java(300) },
            new[] { Bagli(pid: 200), Bagli(pid: 100, adres: "198.51.100.1") },
            Endpointler);
        Assert.True(sonuc.DogrulanabilirMi);

        Assert.Equal(3, sonuc.AdayProcessler.Count);
    }

    [Fact]
    public void Z_IPv6EndpointEslesir()
    {
        var sonuc = BaglantiEslestirici.Eslestir(
            new[] { Java() },
            new[] { new TcpBaglantisi(100, "2001:db8::1", 25565, "ESTABLISHED") },
            new[] { "[2001:db8::1]:25565" });
        Assert.True(sonuc.DogrulanabilirMi);
    }

    [Fact]
    public void Z_IPv4MappedIPv6Eslesir()
    {

        var sonuc = BaglantiEslestirici.Eslestir(
            new[] { Java() },
            new[] { new TcpBaglantisi(100, "::ffff:203.0.113.5", 25565, "ESTABLISHED") },
            Endpointler);
        Assert.True(sonuc.DogrulanabilirMi);
    }

    [Fact]
    public void PortsuzEndpointVarsayilan25565()
    {
        var sonuc = BaglantiEslestirici.Eslestir(
            new[] { Java() }, new[] { Bagli() }, new[] { "203.0.113.5" });
        Assert.True(sonuc.DogrulanabilirMi);
    }

    [Fact]
    public void BirdenFazlaEndpointDenenir()
    {
        var sonuc = BaglantiEslestirici.Eslestir(
            new[] { Java() },
            new[] { Bagli(adres: "203.0.113.9") },
            new[] { "203.0.113.5:25565", "203.0.113.9:25565" });
        Assert.True(sonuc.DogrulanabilirMi);
    }

    [Fact]
    public void BozukEndpointCokmeyeYolAcmaz()
    {
        var sonuc = BaglantiEslestirici.Eslestir(
            new[] { Java() }, new[] { Bagli() },
            new[] { "", "::::", "[bozuk", "a:b:c:yanlis" });
        Assert.Equal(BaglantiDurumu.Bulunamadi, sonuc.Baglanti);
    }

    [Fact]
    public void HicYokKisayoluTutarli()
    {
        var sonuc = OyunBaglantiSonucu.HicYok();
        Assert.False(sonuc.DogrulanabilirMi);
        Assert.Empty(sonuc.AdayProcessler);
    }
}
