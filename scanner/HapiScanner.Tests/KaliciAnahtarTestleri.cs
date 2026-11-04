using HapiScanner.Core;
using HapiScanner.Core.Tarama;

namespace HapiScanner.Tests;

internal sealed class TersKoruyucu : IAnahtarKoruyucu
{
    public byte[] Sarmala(byte[] ham) => ham.Reverse().ToArray();
    public byte[] Coz(byte[] sarmali) => sarmali.Reverse().ToArray();
}

public class KaliciAnahtarTestleri : IDisposable
{
    private readonly string _dizin = Path.Combine(
        Path.GetTempPath(), "hapiscanner-anahtar-" + Guid.NewGuid().ToString("N"));

    private string Yol => Path.Combine(_dizin, "cihaz.key");

    public void Dispose()
    {
        if (Directory.Exists(_dizin)) Directory.Delete(_dizin, true);
    }

    [Fact]
    public void AnahtarIkinciCalistirmadaAyniKalir()
    {

        var ilk = new KaliciAnahtarDeposu(Yol, new TersKoruyucu()).Ac();
        var ikinci = new KaliciAnahtarDeposu(Yol, new TersKoruyucu()).Ac();

        Assert.Equal(ilk.PublicKeyBase64(), ikinci.PublicKeyBase64());
    }

    [Fact]
    public void IlkCalistirmadaDosyaOlusur()
    {
        var depo = new KaliciAnahtarDeposu(Yol, new TersKoruyucu());
        depo.Ac();
        Assert.True(File.Exists(Yol));
    }

    [Fact]
    public void AnahtarDiskeSarmalanmisYazilir()
    {
        var anahtar = new KaliciAnahtarDeposu(Yol, new TersKoruyucu()).Ac();
        var diskte = File.ReadAllBytes(Yol);
        var ham = anahtar.GizliAnahtariCikar();

        Assert.NotEqual(ham, diskte);
        Assert.Equal(ham, diskte.Reverse().ToArray());
    }

    [Fact]
    public void BozukAnahtarTaramayiDurdurmaz()
    {
        Directory.CreateDirectory(_dizin);
        File.WriteAllBytes(Yol, new byte[] { 1, 2, 3, 4 });

        var depo = new KaliciAnahtarDeposu(Yol, new TersKoruyucu());
        var anahtar = depo.Ac();

        Assert.True(depo.YenidenUretildi);
        Assert.NotEmpty(anahtar.PublicKeyBase64());
    }

    [Fact]
    public void BaskaKoruyucuylaYazilmisAnahtarYenidenUretilir()
    {

        new KaliciAnahtarDeposu(Yol, new TersKoruyucu()).Ac();

        var depo = new KaliciAnahtarDeposu(Yol, new SarmalamayanKoruyucu());
        depo.Ac();

        Assert.True(depo.YenidenUretildi);
    }
}

public class DosyaKuralPaketiKaynagiTestleri : IDisposable
{
    private readonly string _dizin = Path.Combine(
        Path.GetTempPath(), "hapiscanner-paket-" + Guid.NewGuid().ToString("N"));

    public DosyaKuralPaketiKaynagiTestleri() => Directory.CreateDirectory(_dizin);

    public void Dispose()
    {
        if (Directory.Exists(_dizin)) Directory.Delete(_dizin, true);
    }

    [Fact]
    public void PaketYoksaVarMiYanlis()
        => Assert.False(new DosyaKuralPaketiKaynagi(_dizin).VarMi);

    [Fact]
    public void ImzaDosyasiYoksaPaketYokSayilir()
    {

        File.WriteAllText(Path.Combine(_dizin, Yayinci.PaketDosyasi), "{}");
        Assert.False(new DosyaKuralPaketiKaynagi(_dizin).VarMi);
    }

    [Fact]
    public async Task PaketVeImzaOkunur()
    {
        File.WriteAllText(Path.Combine(_dizin, Yayinci.PaketDosyasi), "{\"a\":1}");
        File.WriteAllText(Path.Combine(_dizin, Yayinci.ImzaDosyasi), "  imza  \n");

        var (paket, imza) = await new DosyaKuralPaketiKaynagi(_dizin)
            .GetirAsync(CancellationToken.None);

        Assert.Equal("{\"a\":1}", paket);
        Assert.Equal("imza", imza);
    }

    [Fact]
    public async Task PaketYoksaAnlasilirHata()
    {
        var hata = await Assert.ThrowsAsync<FileNotFoundException>(() =>
            new DosyaKuralPaketiKaynagi(_dizin).GetirAsync(CancellationToken.None));
        Assert.Contains("Kural paketi", hata.Message);
    }
}

public class YayinciTestleri
{
    [Fact]
    public void YayinciAnahtariGecerliBirPublicKey()
    {
        foreach (var anahtar in Yayinci.PublicKeyler)
        {
            var ham = Convert.FromBase64String(anahtar);
            using var ecdsa = System.Security.Cryptography.ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(ham, out var okunan);
            Assert.Equal(ham.Length, okunan);
        }
    }

    [Fact]
    public async Task DepodakiKuralPaketiGomuluAnahtarlaDogrulanir()
    {

        var kok = DepoKoku();
        if (kok is null) return;

        var kaynak = new DosyaKuralPaketiKaynagi(Path.Combine(kok, "rules"));
        Assert.True(kaynak.VarMi, "rules/ altinda imzali paket bulunamadi");

        var (paket, imza) = await kaynak.GetirAsync(CancellationToken.None);

        var yukleyici = new HapiScanner.Core.Tespit.KuralPaketiYukleyici(
            Yayinci.PublicKeyler,
            new HapiScanner.Core.Tespit.BellekSurumDeposu());

        var sonuc = yukleyici.Yukle(paket, imza);
        Assert.True(sonuc.Kabul, sonuc.Aciklama ?? sonuc.Neden.ToString());
    }

    private static string? DepoKoku()
    {
        var dizin = new DirectoryInfo(AppContext.BaseDirectory);
        while (dizin is not null)
        {
            if (Directory.Exists(Path.Combine(dizin.FullName, "rules"))) return dizin.FullName;
            dizin = dizin.Parent;
        }
        return null;
    }
}
