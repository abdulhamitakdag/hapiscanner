using System.IO.Compression;
using System.Text;
using HapiScanner.Core.Bicim;

namespace HapiScanner.Tests;

public class JarTestleri
{
    private static MemoryStream JarKur(Action<ZipArchive> doldur)
    {
        var bellek = new MemoryStream();
        using (var arsiv = new ZipArchive(bellek, ZipArchiveMode.Create, leaveOpen: true))
            doldur(arsiv);
        bellek.Position = 0;
        return bellek;
    }

    private static void Yaz(ZipArchive arsiv, string ad, string icerik)
    {
        var girdi = arsiv.CreateEntry(ad);
        using var akis = girdi.Open();
        using var yazici = new StreamWriter(akis);
        yazici.Write(icerik);
    }

    [Fact]
    public void SiniflarVePaketlerOkunur()
    {
        using var jar = JarKur(a =>
        {
            Yaz(a, "com/ornek/istemci/Ana.class", "x");
            Yaz(a, "com/ornek/istemci/alt/Modul.class", "x");
        });

        var sonuc = JarOkuyucu.Oku(jar);

        Assert.True(sonuc.GecerliArsiv);
        Assert.Contains("com.ornek.istemci.Ana", sonuc.SinifAdlari);
        Assert.Contains("com.ornek.istemci", sonuc.PaketAdlari);
        Assert.Contains("com.ornek.istemci.alt", sonuc.PaketAdlari);
    }

    [Fact]
    public void ManifestCozulur()
    {
        using var jar = JarKur(a => Yaz(a, "META-INF/MANIFEST.MF",
            "Manifest-Version: 1.0\r\nImplementation-Title: OrnekIstemci\r\nPremain-Class: a.B\r\n"));

        var sonuc = JarOkuyucu.Oku(jar);

        Assert.Equal("OrnekIstemci", sonuc.Manifest["Implementation-Title"]);
        Assert.True(sonuc.Manifest.ContainsKey("Premain-Class"));
    }

    [Fact]
    public void ManifestDevamSatiriCokmeyeYolAcmaz()
    {
        using var jar = JarKur(a => Yaz(a, "META-INF/MANIFEST.MF",
            "Manifest-Version: 1.0\r\nUzun-Deger: bas\r\n  devam\r\nDiger: x\r\n"));
        var sonuc = JarOkuyucu.Oku(jar);
        Assert.Equal("x", sonuc.Manifest["Diger"]);
    }

    [Fact]
    public void FabricModKimligiOkunur()
    {
        using var jar = JarKur(a => Yaz(a, "fabric.mod.json",
            """{"schemaVersion":1,"id":"ornekmod","version":"1.0.0"}"""));
        Assert.Contains("ornekmod", JarOkuyucu.Oku(jar).ModKimlikleri);
    }

    [Fact]
    public void ForgeModKimligiOkunur()
    {
        using var jar = JarKur(a => Yaz(a, "META-INF/mods.toml",
            "modLoader=\"javafml\"\nmodId = \"ornekforge\"\nversion=\"1.0\"\n"));
        Assert.Contains("ornekforge", JarOkuyucu.Oku(jar).ModKimlikleri);
    }

    [Fact]
    public void BozukJsonModKimligiCokmeyeYolAcmaz()
    {
        using var jar = JarKur(a => Yaz(a, "fabric.mod.json", "{bozuk json"));
        var sonuc = JarOkuyucu.Oku(jar);
        Assert.True(sonuc.GecerliArsiv);
        Assert.Empty(sonuc.ModKimlikleri);
    }

    [Fact]
    public void ZipOlmayanDosyaBozukRaporlanir()
    {
        using var akis = new MemoryStream(Encoding.UTF8.GetBytes("bu bir zip degil"));
        var sonuc = JarOkuyucu.Oku(akis);
        Assert.False(sonuc.GecerliArsiv);
        Assert.NotNull(sonuc.Hata);
    }

    [Fact]
    public void KesilmisArsivCokmeyeYolAcmaz()
    {
        using var tam = JarKur(a => Yaz(a, "com/a/B.class", new string('x', 5000)));
        var ham = tam.ToArray();
        using var kesik = new MemoryStream(ham[..(ham.Length / 2)]);

        var sonuc = JarOkuyucu.Oku(kesik);
        Assert.False(sonuc.GecerliArsiv);
    }

    [Fact]
    public void BosDosyaCokmeyeYolAcmaz()
    {
        using var bos = new MemoryStream();
        Assert.False(JarOkuyucu.Oku(bos).GecerliArsiv);
    }

    [Fact]
    public void OlagandisiUnicodeDosyaAdiCokmeyeYolAcmaz()
    {
        using var jar = JarKur(a =>
        {
            Yaz(a, "çğüöşı/ЖЪ/kayıt.class", "x");
            Yaz(a, new string('u', 200) + ".class", "x");
        });
        var sonuc = JarOkuyucu.Oku(jar);
        Assert.True(sonuc.GecerliArsiv);
        Assert.Equal(2, sonuc.SinifAdlari.Count);
    }

    [Fact]
    public void CokFazlaGirdiSinirlanir()
    {
        using var jar = JarKur(a =>
        {
            for (var i = 0; i < 2000; i++) Yaz(a, $"com/a/S{i}.class", "x");
        });
        var sonuc = JarOkuyucu.Oku(jar);
        Assert.True(sonuc.GecerliArsiv);
        Assert.Equal(2000, sonuc.SinifAdlari.Count);
    }

    [Fact]
    public void OlmayanDosyaBozukRaporlanir()
    {
        var sonuc = JarOkuyucu.Oku("/olmayan/bir/yol/dosya.jar");
        Assert.False(sonuc.GecerliArsiv);
        Assert.NotNull(sonuc.Hata);
    }
}

public class PeTestleri
{

    private static MemoryStream PeKur(bool dll = false, ushort makine = 0x8664,
                                      uint zaman = 1700000000, string bolum = ".text")
    {
        var ham = new byte[0x400];
        ham[0] = 0x4D; ham[1] = 0x5A;
        const int pe = 0x80;
        BitConverter.GetBytes(pe).CopyTo(ham, 0x3C);
        ham[pe] = 0x50; ham[pe + 1] = 0x45;

        var coff = pe + 4;
        BitConverter.GetBytes(makine).CopyTo(ham, coff);
        BitConverter.GetBytes((ushort)1).CopyTo(ham, coff + 2);
        BitConverter.GetBytes(zaman).CopyTo(ham, coff + 4);
        BitConverter.GetBytes((ushort)0xF0).CopyTo(ham, coff + 16);
        BitConverter.GetBytes((ushort)(dll ? 0x2000 : 0x0002)).CopyTo(ham, coff + 18);

        var opsiyonel = coff + 20;
        BitConverter.GetBytes((ushort)0x20b).CopyTo(ham, opsiyonel);

        var bolumBasi = opsiyonel + 0xF0;
        Encoding.ASCII.GetBytes(bolum).CopyTo(ham, bolumBasi);

        return new MemoryStream(ham);
    }

    [Fact]
    public void GecerliPeOkunur()
    {
        using var akis = PeKur();
        var sonuc = PeOkuyucu.Oku(akis);

        Assert.True(sonuc.GecerliPe);
        Assert.True(sonuc.X64);
        Assert.False(sonuc.Dll);
        Assert.Contains(".text", sonuc.BolumAdlari);
        Assert.NotNull(sonuc.DerlemeZamani);
    }

    [Fact]
    public void DllAyirtEdilir()
    {
        using var akis = PeKur(dll: true);
        Assert.True(PeOkuyucu.Oku(akis).Dll);
    }

    [Fact]
    public void X86AyirtEdilir()
    {
        using var akis = PeKur(makine: 0x014c);
        var sonuc = PeOkuyucu.Oku(akis);
        Assert.True(sonuc.X86);
        Assert.False(sonuc.X64);
    }

    [Fact]
    public void PackerBolumAdiOkunur()
    {
        using var akis = PeKur(bolum: "UPX0");
        Assert.Contains("UPX0", PeOkuyucu.Oku(akis).BolumAdlari);
    }

    [Fact]
    public void AnlamsizZamanDamgasiYokSayilir()
    {
        using var akis = PeKur(zaman: 1);
        Assert.Null(PeOkuyucu.Oku(akis).DerlemeZamani);
    }

    [Fact]
    public void MzImzasiYoksaGecersiz()
    {
        using var akis = new MemoryStream(new byte[0x400]);
        var sonuc = PeOkuyucu.Oku(akis);
        Assert.False(sonuc.GecerliPe);
        Assert.Equal("MZ imzasi yok", sonuc.Hata);
    }

    [Fact]
    public void CokKisaDosyaCokmeyeYolAcmaz()
    {
        using var akis = new MemoryStream(new byte[] { 0x4D, 0x5A });
        Assert.False(PeOkuyucu.Oku(akis).GecerliPe);
    }

    [Fact]
    public void GecersizElfanewCokmeyeYolAcmaz()
    {
        var ham = new byte[0x400];
        ham[0] = 0x4D; ham[1] = 0x5A;
        BitConverter.GetBytes(0x7FFFFFFF).CopyTo(ham, 0x3C);
        using var akis = new MemoryStream(ham);

        var sonuc = PeOkuyucu.Oku(akis);
        Assert.False(sonuc.GecerliPe);
        Assert.Equal("gecersiz e_lfanew", sonuc.Hata);
    }

    [Fact]
    public void NegatifElfanewCokmeyeYolAcmaz()
    {
        var ham = new byte[0x400];
        ham[0] = 0x4D; ham[1] = 0x5A;
        BitConverter.GetBytes(-5).CopyTo(ham, 0x3C);
        using var akis = new MemoryStream(ham);
        Assert.False(PeOkuyucu.Oku(akis).GecerliPe);
    }

    [Fact]
    public void AsiriBolumSayisiSinirlanir()
    {
        var ham = new byte[0x400];
        ham[0] = 0x4D; ham[1] = 0x5A;
        const int pe = 0x80;
        BitConverter.GetBytes(pe).CopyTo(ham, 0x3C);
        ham[pe] = 0x50; ham[pe + 1] = 0x45;
        var coff = pe + 4;
        BitConverter.GetBytes((ushort)0x8664).CopyTo(ham, coff);
        BitConverter.GetBytes(ushort.MaxValue).CopyTo(ham, coff + 2);
        BitConverter.GetBytes((ushort)0xF0).CopyTo(ham, coff + 16);
        using var akis = new MemoryStream(ham);

        var sonuc = PeOkuyucu.Oku(akis);
        Assert.True(sonuc.GecerliPe);
        Assert.True(sonuc.BolumAdlari.Count <= 96);
    }

    [Fact]
    public void RastgeleCopVeriCokmeyeYolAcmaz()
    {
        var rastgele = new Random(42);
        for (var i = 0; i < 200; i++)
        {
            var ham = new byte[rastgele.Next(0, 2048)];
            rastgele.NextBytes(ham);
            using var akis = new MemoryStream(ham);

            _ = PeOkuyucu.Oku(akis);
            akis.Position = 0;
            _ = JarOkuyucu.Oku(akis);
        }
    }
}
