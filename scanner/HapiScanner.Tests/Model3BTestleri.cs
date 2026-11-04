using HapiScanner.Core.Arayuz;
using HapiScanner.Core.Model3B;

namespace HapiScanner.Tests;

public class SkinYerlesimiTestleri
{
    [Fact]
    public void YeniBicimUvleriDokununIcindeKalir()
    {
        var parcalar = SkinYerlesimi.Parcalar(pelerinVar: true);
        var hatalar = SkinYerlesimi.UvDenetle(
            parcalar, SkinYerlesimi.DokuGenisligi, SkinYerlesimi.DokuYuksekligi);
        Assert.Empty(hatalar);
    }

    [Fact]
    public void EskiBicimUvleriUstYariyaSigar()
    {

        var parcalar = SkinYerlesimi.Parcalar(eskiBicim: true);
        var hatalar = SkinYerlesimi.UvDenetle(
            parcalar, SkinYerlesimi.DokuGenisligi, SkinYerlesimi.EskiDokuYuksekligi);
        Assert.Empty(hatalar);
    }

    [Fact]
    public void EskiBicimdeIkinciKatmanSadeceSapkadir()
    {
        var parcalar = SkinYerlesimi.Parcalar(eskiBicim: true);
        var katmanlar = parcalar.Where(p => p.IkinciKatman).Select(p => p.Ad).ToList();
        Assert.Equal(new[] { "bas_katman" }, katmanlar);
    }

    [Fact]
    public void EskiBicimdeSolKolSagKolunAynasidir()
    {
        var parcalar = SkinYerlesimi.Parcalar(eskiBicim: true);
        var sag = parcalar.Single(p => p.Ad == "sag_kol");
        var sol = parcalar.Single(p => p.Ad == "sol_kol");

        Assert.True(sol.Uv.On.Genislik < 0);
        Assert.Equal(sag.Uv.On.Sag, sol.Uv.On.X);
    }

    [Fact]
    public void AynalamaIkiKezUygulanincaBasaDoner()
    {
        var uv = new UvAlani(40, 20, 4, 12);
        Assert.Equal(uv, uv.Aynala().Aynala());
    }

    [Fact]
    public void AynalananKutudaSagVeSolYuzlerYerDegistirir()
    {
        var parcalar = SkinYerlesimi.Parcalar(eskiBicim: true);
        var sag = parcalar.Single(p => p.Ad == "sag_bacak");
        var sol = parcalar.Single(p => p.Ad == "sol_bacak");
        Assert.Equal(sag.Uv.Sol.Sag, sol.Uv.Sag.X);
        Assert.Equal(sag.Uv.Sag.Sag, sol.Uv.Sol.X);
    }

    [Fact]
    public void InceModelKollariUcBirimGenistir()
    {
        var ince = SkinYerlesimi.Parcalar(ince: true).Single(p => p.Ad == "sag_kol");
        var klasik = SkinYerlesimi.Parcalar(ince: false).Single(p => p.Ad == "sag_kol");
        Assert.Equal(3, ince.Olcu.X);
        Assert.Equal(4, klasik.Olcu.X);
    }

    [Fact]
    public void IkinciKatmanTabanKutudanBuyuktur()
    {

        foreach (var p in SkinYerlesimi.Parcalar().Where(p => p.IkinciKatman))
            Assert.True(p.Sisme > 0, p.Ad);
    }

    [Fact]
    public void PelerinYalnizcaIstenirseEklenir()
    {
        Assert.DoesNotContain(
            SkinYerlesimi.Parcalar(), p => p.Eklem == Eklem.Pelerin);
        Assert.Contains(
            SkinYerlesimi.Parcalar(pelerinVar: true), p => p.Eklem == Eklem.Pelerin);
    }

    [Fact]
    public void KollarGovdeninIkiYanindadir()
    {
        var parcalar = SkinYerlesimi.Parcalar();
        var sag = parcalar.Single(p => p.Ad == "sag_kol");
        var sol = parcalar.Single(p => p.Ad == "sol_kol");
        Assert.True(sag.Pivot.X < 0);
        Assert.True(sol.Pivot.X > 0);
        Assert.Equal(-sag.Pivot.X, sol.Pivot.X, 6);
        Assert.Equal(SkinYerlesimi.OmuzYuksekligi - 2, sag.Pivot.Y);
    }

    [Fact]
    public void ModelinBoyuOtuzIkiBirimdir()
    {
        var parcalar = SkinYerlesimi.Parcalar();
        var bas = parcalar.Single(p => p.Ad == "bas");
        var tepe = bas.Pivot.Y + bas.MerkezPivota.Y + bas.Olcu.Y / 2;
        Assert.Equal(SkinYerlesimi.Boy, tepe);

        var bacak = parcalar.Single(p => p.Ad == "sag_bacak");
        var taban = bacak.Pivot.Y + bacak.MerkezPivota.Y - bacak.Olcu.Y / 2;
        Assert.Equal(0, taban);
    }
}

public class AnimasyonTestleri
{
    private static readonly AnimasyonTuru[] Tumu =
        Enum.GetValues<AnimasyonTuru>();

    [Fact]
    public void HerAnimasyonunTanimiVar()
    {
        foreach (var tur in Tumu)
        {
            var tanim = Animasyonlar.Tanim(tur);
            Assert.Equal(tur, tanim.Tur);
            Assert.True(tanim.Hiz > 0, tur.ToString());
            Assert.False(string.IsNullOrWhiteSpace(tanim.Aciklama), tur.ToString());
        }
    }

    [Fact]
    public void HerTaramaAsamasiAyriBirAnimasyonAlir()
    {
        var turler = Asama.Tumu
            .Select(a => Animasyonlar.Sec(Ekran.Tarama, a, null))
            .ToList();

        Assert.Equal(Asama.Tumu.Count, turler.Distinct().Count());
        Assert.DoesNotContain(AnimasyonTuru.Baglaniyor, turler);
    }

    [Fact]
    public void AsamaBilinmiyorsaBeklemeAnimasyonuGosterilir()
    {
        Assert.Equal(AnimasyonTuru.Baglaniyor, Animasyonlar.Sec(Ekran.Tarama, null, null));
        Assert.Equal(
            AnimasyonTuru.Baglaniyor,
            Animasyonlar.Sec(Ekran.Tarama, new Asama("x", "X", "", 1), null));
    }

    [Theory]
    [InlineData("iyi", AnimasyonTuru.SonucIyi)]
    [InlineData("kotu", AnimasyonTuru.SonucKotu)]
    [InlineData("uyari", AnimasyonTuru.SonucUyari)]
    [InlineData("notr", AnimasyonTuru.SonucUyari)]
    [InlineData(null, AnimasyonTuru.SonucUyari)]
    public void SonucTonuAnimasyonuBelirler(string? ton, AnimasyonTuru beklenen)
        => Assert.Equal(beklenen, Animasyonlar.Sec(Ekran.Sonuc, null, ton));

    [Fact]
    public void HerEkranBirAnimasyonaEslesir()
    {
        foreach (var ekran in Enum.GetValues<Ekran>())
        {
            var tur = Animasyonlar.Sec(ekran, Asama.Hazirlik, "iyi");
            Assert.Contains(tur, Tumu);
        }
    }

    [Fact]
    public void PozlarMakulAciAraligindaKalir()
    {

        foreach (var tur in Tumu)
        {
            for (var i = 0; i <= 600; i++)
            {
                var poz = Animasyonlar.Hesapla(tur, i * 0.05, i % 101);
                foreach (var eklem in Enum.GetValues<Eklem>())
                {
                    var d = poz[eklem];
                    foreach (var aci in new[] { d.X, d.Y, d.Z })
                    {
                        Assert.True(double.IsFinite(aci), $"{tur}/{eklem}");
                        Assert.InRange(aci, -190, 190);
                    }
                }
                Assert.InRange(poz.Yukseklik, -4, 6);
                Assert.InRange(poz.ModelDonusu, -60, 60);
                Assert.InRange(poz.EsyaGorunurlugu, 0, 1);
            }
        }
    }

    [Fact]
    public void AnimasyonlarSurekliDegisir()
    {

        foreach (var tur in Tumu)
        {
            var a = Animasyonlar.Hesapla(tur, 0.0);
            var b = Animasyonlar.Hesapla(tur, 1.0);
            Assert.NotEqual(a, b);
        }
    }

    [Fact]
    public void IlerlemeYalnizcaDosyaSistemiAsamasininHizindaEtkili()
    {
        var yavas = Animasyonlar.Hesapla(AnimasyonTuru.AsamaDosyaSistemi, 2.0, 0);
        var hizli = Animasyonlar.Hesapla(AnimasyonTuru.AsamaDosyaSistemi, 2.0, 100);
        Assert.NotEqual(yavas.SagKol, hizli.SagKol);

        var a = Animasyonlar.Hesapla(AnimasyonTuru.Karsilama, 2.0, 0);
        var b = Animasyonlar.Hesapla(AnimasyonTuru.Karsilama, 2.0, 100);
        Assert.Equal(a, b);
    }

    [Fact]
    public void EsyasizAnimasyonlardaSimgeGizlenir()
    {
        foreach (var tur in Tumu)
        {
            var tanim = Animasyonlar.Tanim(tur);
            if (tanim.Esya != Esya.Yok) continue;
            var poz = Animasyonlar.Hesapla(tur, 1.0);
            Assert.Equal(0, poz.EsyaGorunurlugu);
        }
    }

    [Fact]
    public void PozKaristirmaUcNoktadaDogru()
    {
        var a = new Poz(Bas: new Donus(10, 0, 0), Yukseklik: 0);
        var b = new Poz(Bas: new Donus(20, 0, 0), Yukseklik: 2);

        Assert.Equal(a, Poz.Karistir(a, b, 0));
        Assert.Equal(b, Poz.Karistir(a, b, 1));
        var orta = Poz.Karistir(a, b, 0.5);
        Assert.Equal(15, orta.Bas.X, 6);
        Assert.Equal(1, orta.Yukseklik, 6);

        Assert.Equal(b, Poz.Karistir(a, b, 5));
        Assert.Equal(a, Poz.Karistir(a, b, -3));
    }
}

public class AnimasyonSurucusuTestleri
{
    [Fact]
    public void AyniTureGecisAnimasyonuBasaSarmaz()
    {
        var s = new AnimasyonSurucusu(AnimasyonTuru.AsamaDosyaSistemi);
        s.Guncelle(0.1);
        var once = s.AktifPoz;

        s.Gecis(AnimasyonTuru.AsamaDosyaSistemi);
        var sonra = s.Guncelle(0);

        Assert.False(s.GecisVar);
        Assert.Equal(once, sonra);
    }

    [Fact]
    public void GecisPozuAniSicratmaz()
    {
        var s = new AnimasyonSurucusu(AnimasyonTuru.SonucIyi);
        for (var i = 0; i < 20; i++) s.Guncelle(0.05);
        var oncesi = s.AktifPoz;

        s.Gecis(AnimasyonTuru.Hata);
        var ilkKare = s.Guncelle(0.016);

        Assert.True(Math.Abs(ilkKare.SagKol.Z - oncesi.SagKol.Z) < 30);
        Assert.True(s.GecisVar);
    }

    [Fact]
    public void GecisSuresiSonundaHedefAnimasyonaKilitlenir()
    {
        var s = new AnimasyonSurucusu(AnimasyonTuru.Karsilama);
        s.Gecis(AnimasyonTuru.AsamaAdli);

        var gecen = 0.0;
        while (gecen < Animasyonlar.GecisSuresi + 0.05)
        {
            s.Guncelle(0.016);
            gecen += 0.016;
        }

        Assert.False(s.GecisVar);
        Assert.Equal(AnimasyonTuru.AsamaAdli, s.Tur);
        Assert.Equal(Animasyonlar.Hesapla(AnimasyonTuru.AsamaAdli, gecen * s.Tanim.Hiz / s.Tanim.Hiz).Bas.X,
                     s.AktifPoz.Bas.X, 1);
    }

    [Fact]
    public void BuyukZamanAtlamasiKirpilir()
    {

        var a = new AnimasyonSurucusu(AnimasyonTuru.Kod);
        var b = new AnimasyonSurucusu(AnimasyonTuru.Kod);

        a.Guncelle(30.0);
        b.Guncelle(AnimasyonSurucusu.EnBuyukAdim);

        Assert.Equal(b.AktifPoz, a.AktifPoz);
    }

    [Fact]
    public void NegatifZamanGeriSarmaz()
    {
        var s = new AnimasyonSurucusu(AnimasyonTuru.Kod);
        s.Guncelle(0.05);
        var once = s.AktifPoz;
        Assert.Equal(once, s.Guncelle(-5));
    }

    [Fact]
    public void AsamaSirasiBoyuncaGecisZinciriKopmaz()
    {
        var s = new AnimasyonSurucusu(Animasyonlar.Sec(Ekran.Tarama, Asama.Hazirlik, null));
        foreach (var asama in Asama.Tumu)
        {
            s.Gecis(Animasyonlar.Sec(Ekran.Tarama, asama, null));
            for (var i = 0; i < 40; i++)
            {
                var poz = s.Guncelle(0.016, Asama.BitisYuzdesi(asama));
                Assert.True(double.IsFinite(poz.Yukseklik));
            }
        }
        Assert.Equal(AnimasyonTuru.AsamaRapor, s.Tur);
    }
}
