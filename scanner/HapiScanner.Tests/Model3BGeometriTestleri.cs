using HapiScanner.Core.Model3B;

namespace HapiScanner.Tests;

public class IskeletTestleri
{
    private static readonly IReadOnlyList<ParcaTanimi> Parcalar =
        SkinYerlesimi.Parcalar(pelerinVar: true);

    [Fact]
    public void HerEklemIcinDonusumUretilir()
    {
        var d = Iskelet.Donusumler(Parcalar, Poz.Durus);
        foreach (var eklem in Iskelet.Ebeveyn.Keys) Assert.True(d.ContainsKey(eklem));
    }

    [Fact]
    public void DurusPozundaParcalarTasarimKonumundaDurur()
    {
        var d = Iskelet.Donusumler(Parcalar, Poz.Durus);
        var bas = Parcalar.Single(p => p.Ad == "bas");
        var merkez = d[Eklem.Bas].Uygula(bas.MerkezPivota);

        Assert.Equal(0, merkez.X, 6);
        Assert.Equal(SkinYerlesimi.OmuzYuksekligi + 4, merkez.Y, 6);
        Assert.Equal(0, merkez.Z, 6);
    }

    [Fact]
    public void GovdeDonusuKollariVeBasiBeraberTasir()
    {

        var poz = new Poz(Govde: new Donus(Y: 90));
        var d = Iskelet.Donusumler(Parcalar, poz);
        var kol = Parcalar.Single(p => p.Ad == "sag_kol");

        var ucNokta = new Nokta3(0, -10, 0).Topla(kol.MerkezPivota with { Y = 0 });
        var dunya = d[Eklem.SagKol].Uygula(ucNokta);

        Assert.True(Math.Abs(dunya.X) < 1e-6);
        Assert.True(Math.Abs(dunya.Z) > 5);
    }

    [Fact]
    public void BacaklarGovdeDonusundenEtkilenmez()
    {
        var d0 = Iskelet.Donusumler(Parcalar, Poz.Durus);
        var d1 = Iskelet.Donusumler(Parcalar, new Poz(Govde: new Donus(X: 40, Y: 60)));
        var bacak = Parcalar.Single(p => p.Ad == "sag_bacak");

        var a = d0[Eklem.SagBacak].Uygula(bacak.MerkezPivota);
        var b = d1[Eklem.SagBacak].Uygula(bacak.MerkezPivota);

        Assert.Equal(a.X, b.X, 6);
        Assert.Equal(a.Y, b.Y, 6);
        Assert.Equal(a.Z, b.Z, 6);
    }

    [Fact]
    public void KolOneDonerkenElOneGider()
    {

        var poz = new Poz(SagKol: new Donus(X: -90));
        var d = Iskelet.Donusumler(Parcalar, poz);
        var el = d[Eklem.SagKol].Uygula(new Nokta3(0, -10, 0));

        Assert.True(el.Z > 8, $"el Z={el.Z}");
        Assert.True(Math.Abs(el.Y - (SkinYerlesimi.OmuzYuksekligi - 2)) < 1.5);
    }

    [Fact]
    public void ModelYuksekligiTumParcalariBirlikteKaldirir()
    {
        var d = Iskelet.Donusumler(Parcalar, new Poz(Yukseklik: 3));
        var bacak = Parcalar.Single(p => p.Ad == "sag_bacak");
        var taban = d[Eklem.SagBacak].Uygula(
            bacak.MerkezPivota with { Y = bacak.MerkezPivota.Y - bacak.Olcu.Y / 2 });
        Assert.Equal(3, taban.Y, 6);
    }

    [Fact]
    public void PelerinArkadaDurur()
    {
        var d = Iskelet.Donusumler(Parcalar, Poz.Durus);
        var pelerin = Parcalar.Single(p => p.Ad == "pelerin");
        var merkez = d[Eklem.Pelerin].Uygula(pelerin.MerkezPivota);
        Assert.True(merkez.Z < -2, $"pelerin Z={merkez.Z}");
    }
}

public class KutuTestleri
{
    [Fact]
    public void AltiYuzUretilir()
    {
        foreach (var parca in SkinYerlesimi.Parcalar(pelerinVar: true))
        {
            var yuzler = Kutu.Yuzler(parca);
            Assert.Equal(6, yuzler.Count);
            Assert.Equal(6, yuzler.Select(y => y.Ad).Distinct().Count());
        }
    }

    [Fact]
    public void UcgenSirasiDisaBakanNormalVerir()
    {

        foreach (var parca in SkinYerlesimi.Parcalar(pelerinVar: true))
        foreach (var yuz in Kutu.Yuzler(parca))
        {
            var a = Fark(yuz.SagAlt, yuz.SolAlt);
            var b = Fark(yuz.SagUst, yuz.SagAlt);
            var n = Carpim(a, b);
            var skaler = n.X * yuz.Normal.X + n.Y * yuz.Normal.Y + n.Z * yuz.Normal.Z;
            Assert.True(skaler > 0, $"{parca.Ad}/{yuz.Ad} ters donmus");
        }
    }

    [Fact]
    public void IkinciKatmanTabanKutuyuTamamenSarar()
    {
        var parcalar = SkinYerlesimi.Parcalar();
        foreach (var katman in parcalar.Where(p => p.IkinciKatman))
        {
            var taban = parcalar.First(p => !p.IkinciKatman && p.Eklem == katman.Eklem);
            Assert.True(katman.Olcu.X + katman.Sisme * 2 > taban.Olcu.X);
            Assert.True(katman.Olcu.Y + katman.Sisme * 2 > taban.Olcu.Y);
        }
    }

    private static Nokta3 Fark(Nokta3 a, Nokta3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    private static Nokta3 Carpim(Nokta3 a, Nokta3 b) => new(
        a.Y * b.Z - a.Z * b.Y,
        a.Z * b.X - a.X * b.Z,
        a.X * b.Y - a.Y * b.X);
}

public class ObjAktariciTestleri
{

    public static string OnizlemeDizini =>
        Path.Combine(Path.GetTempPath(), "hapiscanner-onizleme");

    [Fact]
    public void ObjCikisiGecerliYapidadir()
    {
        var parcalar = SkinYerlesimi.Parcalar(pelerinVar: true);
        var metin = ObjAktarici.Yaz(parcalar, Poz.Durus);

        var v = metin.Split('\n').Count(s => s.StartsWith("v "));
        var vt = metin.Split('\n').Count(s => s.StartsWith("vt "));
        var f = metin.Split('\n').Count(s => s.StartsWith("f "));

        Assert.Equal(parcalar.Count * 6, f);
        Assert.Equal(f * 4, v);
        Assert.Equal(v, vt);
        Assert.DoesNotContain(",", metin);
        Assert.DoesNotContain("NaN", metin);
    }

    [Fact]
    public void HerAnimasyonIcinOnizlemeYazilir()
    {
        Directory.CreateDirectory(OnizlemeDizini);
        var parcalar = SkinYerlesimi.Parcalar(pelerinVar: true);

        foreach (var tur in Enum.GetValues<AnimasyonTuru>())
        {

            var poz = Animasyonlar.Hesapla(tur, 0.9, 50);
            var yol = Path.Combine(OnizlemeDizini, $"{tur}.obj");
            File.WriteAllText(yol, ObjAktarici.Yaz(parcalar, poz));
            Assert.True(new FileInfo(yol).Length > 1000);
        }
    }
}
