using HapiScanner.Core;
using HapiScanner.Core.Adli;
using HapiScanner.Core.Arayuz;

namespace HapiScanner.Tests;

internal sealed class SahteAkis : ITaramaAkisi
{
    private readonly Func<IAsamaBildirici, CancellationToken, Task<AkisSonucu>> _is;

    public SahteAkis(Func<IAsamaBildirici, CancellationToken, Task<AkisSonucu>> isi) => _is = isi;

    public string? GorulenToken { get; private set; }

    public Task<AkisSonucu> CalistirAsync(string token, IAsamaBildirici bildirici, CancellationToken iptal)
    {
        GorulenToken = token;
        return _is(bildirici, iptal);
    }

    public static AkisSonucu Temiz => new(
        Sonuc.BilinenKanitYok, "GOOD", "GOOD", Array.Empty<KanitIstegi>());

    public static SahteAkis Basarili(AkisSonucu? sonuc = null)
        => new((_, _) => Task.FromResult(sonuc ?? Temiz));
}

public class AkisYoneticisiTestleri
{
    private static AkisYoneticisi Yonetici(ITaramaAkisi? akis = null)
        => new(akis ?? SahteAkis.Basarili());

    [Fact]
    public void KarsilamaIleBaslar()
        => Assert.Equal(Ekran.Karsilama, Yonetici().AktifEkran);

    [Fact]
    public void BaslaGizlilikEkraninaGecer()
    {
        var y = Yonetici();
        y.Basla();
        Assert.Equal(Ekran.Gizlilik, y.AktifEkran);
    }

    [Fact]
    public void OnaysizKodEkraninaGecilemez()
    {
        var y = Yonetici();
        y.Basla();

        Assert.False(y.GizlilikOnayla());
        Assert.Equal(Ekran.Gizlilik, y.AktifEkran);
        Assert.Equal(Metinler.OnayUyarisi, y.HataMesaji);
    }

    [Fact]
    public void OnaydanSonraKodEkraniAcilir()
    {
        var y = Yonetici();
        y.Basla();
        y.OnayVerildi = true;

        Assert.True(y.GizlilikOnayla());
        Assert.Equal(Ekran.Kod, y.AktifEkran);
        Assert.Null(y.HataMesaji);
    }

    [Fact]
    public async Task OnaysizTaramaBaslamaz()
    {
        var akis = SahteAkis.Basarili();
        var y = new AkisYoneticisi(akis) { Kod = "HAPI-K7QD-3MWA" };

        await y.BaglanVeTaraAsync();

        Assert.Null(akis.GorulenToken);
        Assert.Equal(Ekran.Gizlilik, y.AktifEkran);
    }

    [Theory]
    [InlineData("hapi-k7qd-3mwa")]
    [InlineData("HAPIK7QD3MWA")]
    [InlineData(" HAPI-K7QD-3MWA ")]
    public void KodNormalizeEdilipKabulEdilir(string girdi)
    {
        var y = Yonetici();
        y.Kod = girdi;
        Assert.True(y.KodGecerli);
    }

    [Theory]
    [InlineData("")]
    [InlineData("HAPI-K7QD")]
    [InlineData("HAPI-K7Q0-3MWA")]
    public void BozukKodReddedilir(string girdi)
    {
        var y = Yonetici();
        y.Kod = girdi;
        Assert.False(y.KodGecerli);
        Assert.False(y.BaglanilabilirMi);
    }

    [Fact]
    public async Task GecersizKodlaBaglanilmaz()
    {
        var akis = SahteAkis.Basarili();
        var y = new AkisYoneticisi(akis) { OnayVerildi = true, Kod = "HAPI-XX" };

        await y.BaglanVeTaraAsync();

        Assert.Null(akis.GorulenToken);
        Assert.Equal(Metinler.KodGecersiz, y.KodHatasi);
    }

    [Fact]
    public async Task BasariliTaramaSonucEkranindaBiter()
    {
        var akis = SahteAkis.Basarili();
        var y = new AkisYoneticisi(akis) { OnayVerildi = true, Kod = "hapi-k7qd-3mwa" };

        await y.BaglanVeTaraAsync();

        Assert.Equal("HAPI-K7QD-3MWA", akis.GorulenToken);
        Assert.Equal(Ekran.Sonuc, y.AktifEkran);
        Assert.Equal(100, y.Ilerleme);
        Assert.Equal(Metinler.SonucTemiz, y.SonucBasligi);
        Assert.All(y.Asamalar, a => Assert.True(a.Bitti));
    }

    [Fact]
    public async Task AsamaBildirimiSatirlariIsaretler()
    {
        var akis = new SahteAkis((bildirici, _) =>
        {
            bildirici.Asamaya(Asama.DosyaSistemi);
            bildirici.Ilerleme(40);
            return Task.FromResult<AkisSonucu>(SahteAkis.Temiz);
        });
        var y = new AkisYoneticisi(akis) { OnayVerildi = true, Kod = "HAPI-K7QD-3MWA" };

        await y.BaglanVeTaraAsync();

        var dosya = y.Asamalar.Single(a => a.Asama.Protokol == Asama.DosyaSistemi.Protokol);
        Assert.Equal(AsamaDurumu.Bitti, dosya.Durum);
    }

    [Fact]
    public async Task IlerlemeGeriGitmez()
    {
        var akis = new SahteAkis((bildirici, _) =>
        {
            bildirici.Ilerleme(70);
            bildirici.Ilerleme(20);
            return Task.FromResult<AkisSonucu>(SahteAkis.Temiz);
        });
        var y = new AkisYoneticisi(akis) { OnayVerildi = true, Kod = "HAPI-K7QD-3MWA" };

        await y.BaglanVeTaraAsync();

        Assert.Equal(100, y.Ilerleme);
    }

    [Fact]
    public async Task IptalHataEkraninaDuser()
    {
        var basladi = new TaskCompletionSource();
        var akis = new SahteAkis(async (_, iptal) =>
        {
            basladi.TrySetResult();
            await Task.Delay(Timeout.Infinite, iptal);
            return SahteAkis.Temiz;
        });
        var y = new AkisYoneticisi(akis) { OnayVerildi = true, Kod = "HAPI-K7QD-3MWA" };

        var calisan = y.BaglanVeTaraAsync();
        await basladi.Task;
        Assert.True(y.IptalEdilebilir);
        y.Iptal();
        await calisan;

        Assert.Equal(Ekran.Hata, y.AktifEkran);
        Assert.Equal(Metinler.IptalEdildi, y.HataMesaji);
    }

    [Fact]
    public async Task ApiHatasiOkunurMesajaCevrilir()
    {
        var akis = new SahteAkis((_, _) =>
            throw new ApiIstisnasi("TOKEN_EXPIRED", "token expired", 410));
        var y = new AkisYoneticisi(akis) { OnayVerildi = true, Kod = "HAPI-K7QD-3MWA" };

        await y.BaglanVeTaraAsync();

        Assert.Equal(Ekran.Hata, y.AktifEkran);
        Assert.Contains("süresi dolmuş", y.HataMesaji);
    }

    [Fact]
    public async Task BasarisizSonucHataEkraninaDuser()
    {
        var akis = SahteAkis.Basarili(new AkisSonucu(
            Sonuc.TaramaBasarisiz, "UNAVAILABLE", "UNAVAILABLE",
            Array.Empty<KanitIstegi>(), "disk okunamadi"));
        var y = new AkisYoneticisi(akis) { OnayVerildi = true, Kod = "HAPI-K7QD-3MWA" };

        await y.BaglanVeTaraAsync();

        Assert.Equal(Ekran.Hata, y.AktifEkran);
        Assert.Equal("disk okunamadi", y.HataMesaji);
    }

    [Fact]
    public async Task IsaretliSonucKotuTonVerir()
    {
        var akis = SahteAkis.Basarili(new AkisSonucu(
            Sonuc.Isaretli, "GOOD", "GOOD", Array.Empty<KanitIstegi>()));
        var y = new AkisYoneticisi(akis) { OnayVerildi = true, Kod = "HAPI-K7QD-3MWA" };

        await y.BaglanVeTaraAsync();

        Assert.Equal(Metinler.SonucIsaretli, y.SonucBasligi);
        Assert.Equal("kotu", y.SonucTonu);
    }

    [Fact]
    public async Task IkinciTaramaDurumuSifirlar()
    {
        var akis = new SahteAkis((bildirici, _) =>
        {
            bildirici.Not("asama notu");
            return Task.FromResult<AkisSonucu>(SahteAkis.Temiz);
        });
        var y = new AkisYoneticisi(akis) { OnayVerildi = true, Kod = "HAPI-K7QD-3MWA" };

        await y.BaglanVeTaraAsync();
        await y.BaglanVeTaraAsync();

        Assert.Single(y.Gunluk);
    }
}

public class GunlukSuzgeciTestleri
{
    [Theory]
    [InlineData(@"C:\Users\Ahmet\Downloads\hile.jar taranıyor")]
    [InlineData(@"aday: D:\games\.minecraft\mods\x.jar")]
    [InlineData(@"\\SUNUCU\paylasim\dosya.jar okundu")]
    [InlineData("/home/ahmet/.minecraft/mods/x.jar")]
    public void YollarMaskelenir(string satir)
    {
        var temiz = GunlukSuzgeci.Temizle(satir);
        Assert.Contains(GunlukSuzgeci.YolMaskesi, temiz);
        Assert.DoesNotContain(@":\", temiz);
        Assert.DoesNotContain("Ahmet", temiz);
        Assert.DoesNotContain("ahmet", temiz);
    }

    [Fact]
    public void TokenMaskelenir()
    {
        var temiz = GunlukSuzgeci.Temizle("oturum HAPI-K7QD-3MWA ile acildi");
        Assert.Equal("oturum HAPI-****-**** ile acildi", temiz);
    }

    [Fact]
    public void NormalSatirKorunur()
        => Assert.Equal("12 aday dosya bulundu",
            GunlukSuzgeci.Temizle("  12 aday dosya bulundu  "));

    [Fact]
    public void UzunSatirKisalir()
        => Assert.True(GunlukSuzgeci.Temizle(new string('x', 1000)).Length <= 303);

    [Fact]
    public void BosSatirAtilir()
        => Assert.Equal(string.Empty, GunlukSuzgeci.Temizle("   "));

    [Fact]
    public async Task GunlugeDusenSatirSuzulur()
    {
        var akis = new SahteAkis((bildirici, _) =>
        {
            bildirici.Not(@"C:\Users\Ahmet\Downloads\hile.jar incelendi");
            bildirici.Not("   ");
            return Task.FromResult<AkisSonucu>(SahteAkis.Temiz);
        });
        var y = new AkisYoneticisi(akis) { OnayVerildi = true, Kod = "HAPI-K7QD-3MWA" };

        await y.BaglanVeTaraAsync();

        Assert.Single(y.Gunluk);
        Assert.DoesNotContain("Ahmet", y.Gunluk[0]);
    }
}

public class AsamaTestleri
{
    [Fact]
    public void AsamaProtokolAdlariBenzersiz()
        => Assert.Equal(Asama.Tumu.Count, Asama.Tumu.Select(a => a.Protokol).Distinct().Count());

    [Fact]
    public void AsamaProtokolAdlariSemayaUyar()
        => Assert.All(Asama.Tumu, a =>
        {
            Assert.InRange(a.Protokol.Length, 1, 64);
            Assert.True(a.Protokol.All(c => c is >= (char)32 and < (char)127));
        });

    [Fact]
    public void SonAsamaYuzYapar()
        => Assert.Equal(100, Asama.BitisYuzdesi(Asama.Tumu[^1]));

    [Fact]
    public void YuzdelerArtanSirada()
    {
        var yuzdeler = Asama.Tumu.Select(Asama.BitisYuzdesi).ToList();
        Assert.Equal(yuzdeler.OrderBy(y => y), yuzdeler);
    }
}

public class GizlilikMetniTestleri
{
    private static string? PrivacyYolu()
    {
        var dizin = new DirectoryInfo(AppContext.BaseDirectory);
        while (dizin is not null)
        {
            var aday = Path.Combine(dizin.FullName, "docs", "PRIVACY.md");
            if (File.Exists(aday)) return aday;
            dizin = dizin.Parent;
        }
        return null;
    }

    [Fact]
    public void YapmazListesiBelgeyleAyni()
    {
        var yol = PrivacyYolu();
        if (yol is null) return;

        var belge = Sadelestir(File.ReadAllText(yol));
        foreach (var madde in Metinler.Yapmaz)
            Assert.Contains(Sadelestir(madde), belge);
    }

    private static string Sadelestir(string metin)
    {
        var harfler = metin.ToLowerInvariant()
            .Replace('ı', 'i').Replace('ş', 's').Replace('ğ', 'g')
            .Replace('ü', 'u').Replace('ö', 'o').Replace('ç', 'c')
            .Replace('İ', 'i').Replace('’', '\'');
        return new string(harfler.Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '\'')
                                 .ToArray());
    }
}
