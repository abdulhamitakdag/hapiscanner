using System.Buffers.Binary;
using System.Text;
using HapiScanner.Core.Adli;
using HapiScanner.Core.Tespit;

namespace HapiScanner.Tests;

internal static class AdliYardimci
{
    internal static readonly DateTimeOffset An = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    internal static BirimBilgisi Ntfs(string kok = @"C:\", bool ilgili = true)
        => new(kok, "NTFS", ilgili);

    internal static UsnJournalDurumu Journal(int gunGeriye, ulong id = 7, long ilk = 100, long son = 9_000)
        => new(id, ilk, son, 32L * 1024 * 1024, An.AddDays(-gunGeriye));

    internal static UsnOkumaSonucu Okundu(int gunGeriye, params UsnKaydi[] kayitlar)
        => new(UsnSonucTuru.Okundu, Journal(gunGeriye), kayitlar);

    internal static MftOkumaSonucu MftOkundu(long sayi = 5000)
        => new(MftSonucTuru.Okundu, sayi);

    internal static UsnKaydi Kayit(string ad, UsnOlayi olay, int gunGeriye, long usn = 1, ulong dosyaRef = 0)
        => new(usn, ad, olay, An.AddDays(-gunGeriye), dosyaRef);

    internal static AdliKapsam Hesapla(
        IReadOnlyList<BirimDegerlendirmesi> birimler,
        AyricalikDurumu ayricalik = AyricalikDurumu.Yonetici,
        RuntimeErisimi runtime = RuntimeErisimi.Tam)
        => new AdliKapsamHesaplayici().Hesapla(birimler, ayricalik, runtime, An);
}

internal sealed class SahteBirimSaglayici : IBirimSaglayici
{
    private readonly IReadOnlyList<BirimBilgisi> _birimler;
    public SahteBirimSaglayici(params BirimBilgisi[] birimler) => _birimler = birimler;
    public IReadOnlyList<BirimBilgisi> Birimler() => _birimler;
}

internal sealed class SahteUsn : IUsnSaglayici
{
    private readonly Func<BirimBilgisi, UsnOkumaSonucu> _cevap;
    public SahteUsn(Func<BirimBilgisi, UsnOkumaSonucu> cevap) => _cevap = cevap;
    public UsnOkumaSonucu Oku(BirimBilgisi birim) => _cevap(birim);
}

internal sealed class SahteMft : IMftSaglayici
{
    private readonly Func<BirimBilgisi, MftOkumaSonucu> _cevap;
    public SahteMft(Func<BirimBilgisi, MftOkumaSonucu> cevap) => _cevap = cevap;
    public MftOkumaSonucu Oku(BirimBilgisi birim) => _cevap(birim);
}

public class DosyaSistemiTestleri
{
    [Theory]
    [InlineData("NTFS", true, true)]
    [InlineData("ReFS", false, true)]
    [InlineData("exFAT", false, false)]
    [InlineData("FAT32", false, false)]
    [InlineData("", false, false)]
    public void DesteklerDosyaSistemineGoreBelirlenir(string bicim, bool mft, bool usn)
    {
        var birim = new BirimBilgisi(@"D:\", bicim);
        Assert.Equal(mft, birim.MftMumkunMu);
        Assert.Equal(usn, birim.UsnMumkunMu);
    }
}

public class AdliKapsamTestleri
{
    [Fact]
    public void NtfsVeTamJournalIyiKapsam()
    {
        var birim = AdliYardimci.Ntfs();
        var kapsam = AdliYardimci.Hesapla(new[]
        {
            new BirimDegerlendirmesi(birim, AdliYardimci.MftOkundu(), AdliYardimci.Okundu(180)),
        });

        Assert.Equal(MftDurumu.Mevcut, kapsam.Mft);
        Assert.Equal(UsnDurumu.Mevcut, kapsam.Usn);
        Assert.Equal(Kapsam.Iyi, kapsam.GecmisKapsami);
        Assert.Equal(Kapsam.Iyi, kapsam.Genel);
        Assert.True(kapsam.GecmisGuvenilirMi);
        Assert.Equal("GOOD", kapsam.ProtokolDegeri);
    }

    [Fact]
    public void NtfsOlmayanBirimTaramayiBasarisizYapmaz()
    {
        var birim = new BirimBilgisi(@"D:\", "exFAT");
        var kapsam = AdliYardimci.Hesapla(new[]
        {
            new BirimDegerlendirmesi(birim,
                MftOkumaSonucu.Desteklenmiyor(birim.Kok),
                UsnOkumaSonucu.Desteklenmiyor(birim.Kok)),
        });

        Assert.Equal(MftDurumu.Yok, kapsam.Mft);
        Assert.Equal(UsnDurumu.Yok, kapsam.Usn);
        Assert.Equal(Kapsam.Yok, kapsam.Genel);
        Assert.False(kapsam.GecmisGuvenilirMi);
        Assert.Contains(kapsam.Notlar, n => n.Contains("exFAT"));

        Assert.Equal(Sonuc.Kararsiz, SonucKarari.Belirle(Array.Empty<Eslesme>(), kapsam));
    }

    [Fact]
    public void IlgisizExFatYedekDiskiKapsamiDusurmez()
    {
        var sistem = AdliYardimci.Ntfs();
        var yedek = new BirimBilgisi(@"E:\", "exFAT", Ilgili: false);

        var kapsam = AdliYardimci.Hesapla(new[]
        {
            new BirimDegerlendirmesi(sistem, AdliYardimci.MftOkundu(), AdliYardimci.Okundu(120)),
            new BirimDegerlendirmesi(yedek,
                MftOkumaSonucu.Desteklenmiyor(yedek.Kok),
                UsnOkumaSonucu.Desteklenmiyor(yedek.Kok)),
        });

        Assert.Equal(Kapsam.Iyi, kapsam.Genel);
        Assert.DoesNotContain(kapsam.Notlar, n => n.Contains(@"E:\"));
    }

    [Fact]
    public void JournalYokKapsamDusururAmaKanitSaymaz()
    {
        var birim = AdliYardimci.Ntfs();
        var kapsam = AdliYardimci.Hesapla(new[]
        {
            new BirimDegerlendirmesi(birim, AdliYardimci.MftOkundu(),
                new UsnOkumaSonucu(UsnSonucTuru.JournalYok,
                    Aciklama: "C:\\: USN journal etkin degil veya siliniyor")),
        });

        Assert.Equal(UsnDurumu.Yok, kapsam.Usn);
        Assert.Equal(MftDurumu.Mevcut, kapsam.Mft);
        Assert.Equal(Kapsam.Sinirli, kapsam.Genel);
        Assert.False(kapsam.GecmisGuvenilirMi);
        Assert.Contains(kapsam.Notlar, n => n.Contains("tek basina hile kaniti degildir"));
    }

    [Fact]
    public void IzinReddiCokmezYalnizcaKapsamDuser()
    {
        var birim = AdliYardimci.Ntfs();
        var kapsam = AdliYardimci.Hesapla(new[]
        {
            new BirimDegerlendirmesi(birim,
                MftOkumaSonucu.ErisimYok(birim.Kok),
                UsnOkumaSonucu.ErisimYok(birim.Kok)),
        }, ayricalik: AyricalikDurumu.Standart, runtime: RuntimeErisimi.Sinirli);

        Assert.Equal(MftDurumu.ErisimReddedildi, kapsam.Mft);
        Assert.Equal(UsnDurumu.ErisimReddedildi, kapsam.Usn);
        Assert.Equal(Kapsam.Yok, kapsam.Genel);
        Assert.Contains(kapsam.Notlar, n => n.Contains("yonetici"));
        Assert.Contains("Admin yetkisi: NO", kapsam.Ozet());
    }

    [Fact]
    public void KisaJournalPenceresiSinirliGecmisUretir()
    {
        var birim = AdliYardimci.Ntfs();
        var kapsam = AdliYardimci.Hesapla(new[]
        {
            new BirimDegerlendirmesi(birim, AdliYardimci.MftOkundu(), AdliYardimci.Okundu(5)),
        });

        Assert.Equal(UsnDurumu.Mevcut, kapsam.Usn);
        Assert.Equal(Kapsam.Sinirli, kapsam.GecmisKapsami);
        Assert.False(kapsam.GecmisGuvenilirMi);
        Assert.Equal(Sonuc.SinirliGecmis, SonucKarari.Belirle(Array.Empty<Eslesme>(), kapsam));
    }

    [Fact]
    public void JournalKimligiDegistiyseBozulmusSayilir()
    {
        var birim = AdliYardimci.Ntfs();
        var kapsam = AdliYardimci.Hesapla(new[]
        {
            new BirimDegerlendirmesi(birim, AdliYardimci.MftOkundu(),
                AdliYardimci.Okundu(200), OncekiJournalId: 99),
        });

        Assert.Equal(UsnDurumu.Bozulmus, kapsam.Usn);
        Assert.True(kapsam.AdliBozulmaVar);
        Assert.Equal(Kapsam.Sinirli, kapsam.Genel);
        Assert.Equal(Sonuc.AdliBozulma, SonucKarari.Belirle(Array.Empty<Eslesme>(), kapsam));
    }

    [Fact]
    public void BosJournalBozulmusSayilir()
    {
        var birim = AdliYardimci.Ntfs();
        var bos = new UsnOkumaSonucu(UsnSonucTuru.Okundu,
            new UsnJournalDurumu(7, 500, 500, 1024, null), Array.Empty<UsnKaydi>());

        var kapsam = AdliYardimci.Hesapla(new[]
        {
            new BirimDegerlendirmesi(birim, AdliYardimci.MftOkundu(), bos),
        });

        Assert.Equal(UsnDurumu.Bozulmus, kapsam.Usn);
    }

    [Fact]
    public void BeklenmedikHataTaramayiCokertmez()
    {
        var birim = AdliYardimci.Ntfs();
        var kapsam = AdliYardimci.Hesapla(new[]
        {
            new BirimDegerlendirmesi(birim,
                new MftOkumaSonucu(MftSonucTuru.Hata, Aciklama: "beklenmedik"),
                new UsnOkumaSonucu(UsnSonucTuru.Hata, Aciklama: "beklenmedik")),
        });

        Assert.Equal(Kapsam.Yok, kapsam.Genel);
        Assert.NotEqual(Sonuc.TaramaBasarisiz, SonucKarari.Belirle(Array.Empty<Eslesme>(), kapsam));
    }
}

public class SonucKarariTestleri
{
    private static Eslesme Eslesme(Guven guven) =>
        new("R-1", "ornek", "CLIENT", "HIGH", guven, 100, new[] { GostergeTipi.Sha256 });

    private static AdliKapsam TamKapsam() => AdliYardimci.Hesapla(new[]
    {
        new BirimDegerlendirmesi(AdliYardimci.Ntfs(), AdliYardimci.MftOkundu(), AdliYardimci.Okundu(180)),
    });

    private static AdliKapsam ZayifKapsam() => AdliYardimci.Hesapla(new[]
    {
        new BirimDegerlendirmesi(AdliYardimci.Ntfs(), AdliYardimci.MftOkundu(), AdliYardimci.Okundu(3)),
    });

    [Fact]
    public void TamKapsamVeKanitYokBilinenKanitYokUretir()
        => Assert.Equal(Sonuc.BilinenKanitYok, SonucKarari.Belirle(Array.Empty<Eslesme>(), TamKapsam()));

    [Fact]
    public void DusukKapsamBilinenKanitYokUretmez()
        => Assert.NotEqual(Sonuc.BilinenKanitYok, SonucKarari.Belirle(Array.Empty<Eslesme>(), ZayifKapsam()));

    [Fact]
    public void GucluKanitDusukKapsamdaBileIsaretlenir()
        => Assert.Equal(Sonuc.Isaretli,
            SonucKarari.Belirle(new[] { Eslesme(Guven.Yuksek) }, ZayifKapsam()));

    [Fact]
    public void OrtaKanitSuphelidir()
        => Assert.Equal(Sonuc.Supheli, SonucKarari.Belirle(new[] { Eslesme(Guven.Orta) }, TamKapsam()));

    [Fact]
    public void AciklamalardaTemizKelimesiGecmez()
    {
        foreach (var sonuc in new[]
                 {
                     Sonuc.BilinenKanitYok, Sonuc.Isaretli, Sonuc.Supheli,
                     Sonuc.SinirliGecmis, Sonuc.AdliBozulma, Sonuc.Kararsiz,
                 })
        {
            var metin = SonucKarari.Aciklama(sonuc, AdliYardimci.An).ToLowerInvariant();
            Assert.DoesNotContain("temiz", metin);
        }
    }
}

public class UsnGecmisTestleri
{
    private readonly UsnGecmisAnalizi _analiz = new();

    [Fact]
    public void SilinmisHileJarIzOlarakCikar()
    {
        var izler = _analiz.Analiz(new[]
        {
            AdliYardimci.Kayit("hile.jar", UsnOlayi.Olusturma, 30, usn: 1, dosyaRef: 10),
            AdliYardimci.Kayit("hile.jar", UsnOlayi.Silme, 10, usn: 2, dosyaRef: 10),
        });

        var iz = Assert.Single(izler);
        Assert.True(iz.SilinmeGorulduMu);
        Assert.False(iz.SuAndaVarMi);
        Assert.Equal(AdliYardimci.An.AddDays(-30), iz.IlkGorulme);
        Assert.Equal(AdliYardimci.An.AddDays(-10), iz.SonGorulme);
        Assert.Contains(UsnKanitTuru.Silme, iz.KanitTurleri());
    }

    [Fact]
    public void YenidenAdlandirmaAyniDosyaOlarakGruplanir()
    {
        var izler = _analiz.Analiz(new[]
        {
            AdliYardimci.Kayit("hile.jar", UsnOlayi.Olusturma, 20, usn: 1, dosyaRef: 42),
            AdliYardimci.Kayit("masum.jar", UsnOlayi.YenidenAdlandirmaYeni, 19, usn: 2, dosyaRef: 42),
        });

        var iz = Assert.Single(izler);
        Assert.True(iz.YenidenAdlandirmaGorulduMu);
        Assert.Equal("masum.jar", iz.Ad);
        Assert.Equal(2, iz.KayitSayisi);
    }

    [Fact]
    public void HalenVarOlanDosyaIsaretlenir()
    {
        var izler = _analiz.Analiz(
            new[] { AdliYardimci.Kayit("hile.jar", UsnOlayi.Olusturma, 5) },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "hile.jar" });

        Assert.True(Assert.Single(izler).SuAndaVarMi);
    }

    [Fact]
    public void AlakasizDosyaHareketleriTutulmaz()
    {
        var izler = _analiz.Analiz(new[]
        {
            AdliYardimci.Kayit("vergi-beyani.pdf", UsnOlayi.Olusturma, 3),
            AdliYardimci.Kayit("tatil.mp4", UsnOlayi.Silme, 3),
            AdliYardimci.Kayit("notlar.txt", UsnOlayi.VeriDegisimi, 3),
        });

        Assert.Empty(izler);
    }

    [Fact]
    public void OrtukKimlikYolVeyaAdSizdirmaz()
    {
        var iz = Assert.Single(_analiz.Analiz(
            new[] { AdliYardimci.Kayit("hile.jar", UsnOlayi.Olusturma, 1) }));

        Assert.Equal(16, iz.OrtukKimlik.Length);
        Assert.DoesNotContain("hile", iz.OrtukKimlik);
    }
}

public class UsnCozumleyiciTestleri
{

    private static byte[] V2(string ad, uint reason, long usn, long filetime,
        ulong dosyaRef = 1, ulong ustRef = 2)
    {
        var adBaytlari = Encoding.Unicode.GetBytes(ad);
        var uzunluk = 60 + adBaytlari.Length;
        if (uzunluk % 8 != 0) uzunluk += 8 - (uzunluk % 8);

        var tampon = new byte[uzunluk];
        BinaryPrimitives.WriteUInt32LittleEndian(tampon.AsSpan(0), (uint)uzunluk);
        BinaryPrimitives.WriteUInt16LittleEndian(tampon.AsSpan(4), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(tampon.AsSpan(6), 0);
        BinaryPrimitives.WriteUInt64LittleEndian(tampon.AsSpan(8), dosyaRef);
        BinaryPrimitives.WriteUInt64LittleEndian(tampon.AsSpan(16), ustRef);
        BinaryPrimitives.WriteInt64LittleEndian(tampon.AsSpan(24), usn);
        BinaryPrimitives.WriteInt64LittleEndian(tampon.AsSpan(32), filetime);
        BinaryPrimitives.WriteUInt32LittleEndian(tampon.AsSpan(40), reason);
        BinaryPrimitives.WriteUInt16LittleEndian(tampon.AsSpan(56), (ushort)adBaytlari.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(tampon.AsSpan(58), 60);
        adBaytlari.CopyTo(tampon, 60);
        return tampon;
    }

    private static long FileTime(DateTimeOffset an) => an.UtcDateTime.ToFileTimeUtc();

    [Fact]
    public void V2KaydiCozulur()
    {
        var an = AdliYardimci.An.AddDays(-7);
        var kayit = Assert.Single(UsnKayitCozumleyici.Coz(V2("hile.jar", 0x100, 1234, FileTime(an))));

        Assert.Equal("hile.jar", kayit.Ad);
        Assert.Equal(1234, kayit.Usn);
        Assert.Equal(UsnOlayi.Olusturma, kayit.Olay);
        Assert.Equal(an, kayit.Zaman);
    }

    [Fact]
    public void ArdArdaGelenKayitlarCozulur()
    {
        var a = V2("bir.jar", 0x100, 1, FileTime(AdliYardimci.An));
        var b = V2("iki.exe", 0x200, 2, FileTime(AdliYardimci.An));
        var tampon = a.Concat(b).ToArray();

        var kayitlar = UsnKayitCozumleyici.Coz(tampon);
        Assert.Equal(2, kayitlar.Count);
        Assert.Equal("iki.exe", kayitlar[1].Ad);
        Assert.Equal(UsnOlayi.Silme, kayitlar[1].Olay);
    }

    [Fact]
    public void KesikTamponSessizceDurur()
    {
        var tam = V2("hile.jar", 0x100, 1, FileTime(AdliYardimci.An));
        var kesik = tam.Take(tam.Length - 10).ToArray();

        Assert.Empty(UsnKayitCozumleyici.Coz(kesik));
    }

    [Fact]
    public void BozukUzunlukSonsuzDonguYapmaz()
    {
        var tampon = new byte[128];
        BinaryPrimitives.WriteUInt32LittleEndian(tampon.AsSpan(0), 0);
        Assert.Empty(UsnKayitCozumleyici.Coz(tampon));

        BinaryPrimitives.WriteUInt32LittleEndian(tampon.AsSpan(0), uint.MaxValue);
        Assert.Empty(UsnKayitCozumleyici.Coz(tampon));
    }

    [Fact]
    public void TamponDisinaTasanAdReddedilir()
    {
        var tampon = V2("hile.jar", 0x100, 1, FileTime(AdliYardimci.An));
        BinaryPrimitives.WriteUInt16LittleEndian(tampon.AsSpan(56), 4096);
        Assert.Empty(UsnKayitCozumleyici.Coz(tampon));
    }

    [Fact]
    public void BilinmeyenSurumAtlanir()
    {
        var tampon = V2("hile.jar", 0x100, 1, FileTime(AdliYardimci.An));
        BinaryPrimitives.WriteUInt16LittleEndian(tampon.AsSpan(4), 9);
        Assert.Empty(UsnKayitCozumleyici.Coz(tampon));
    }

    [Fact]
    public void GecersizZamanEpochaDuser()
        => Assert.Equal(DateTimeOffset.UnixEpoch, UsnKayitCozumleyici.ZamanCoz(0));

    [Theory]
    [InlineData(0x100u, UsnOlayi.Olusturma)]
    [InlineData(0x200u, UsnOlayi.Silme)]
    [InlineData(0x1000u, UsnOlayi.YenidenAdlandirmaEski)]
    [InlineData(0x2000u, UsnOlayi.YenidenAdlandirmaYeni)]
    [InlineData(0x1u, UsnOlayi.VeriDegisimi)]
    public void OlayMaskeleriCozulur(uint reason, UsnOlayi beklenen)
        => Assert.True(UsnKayitCozumleyici.OlayCoz(reason).HasFlag(beklenen));
}

public class AdliTaramaTestleri
{
    [Fact]
    public void UctanUcaIyiKapsam()
    {
        var birim = AdliYardimci.Ntfs();
        var tarama = new AdliTarama(
            new SahteBirimSaglayici(birim),
            new SahteMft(_ => AdliYardimci.MftOkundu()),
            new SahteUsn(_ => AdliYardimci.Okundu(200,
                AdliYardimci.Kayit("hile.jar", UsnOlayi.Olusturma, 60, usn: 1, dosyaRef: 3),
                AdliYardimci.Kayit("hile.jar", UsnOlayi.Silme, 59, usn: 2, dosyaRef: 3))),
            new SabitAyricalik(AyricalikDurumu.Yonetici));

        var sonuc = tarama.Calistir(runtime: RuntimeErisimi.Tam, simdi: AdliYardimci.An);

        Assert.Equal(Kapsam.Iyi, sonuc.Kapsam.Genel);
        Assert.True(Assert.Single(sonuc.Izler).SilinmeGorulduMu);
    }

    [Fact]
    public void NtfsOlmayanBirimdeSaglayiciCagrilmaz()
    {
        var mftCagrildi = false;
        var usnCagrildi = false;

        var tarama = new AdliTarama(
            new SahteBirimSaglayici(new BirimBilgisi(@"D:\", "exFAT")),
            new SahteMft(_ => { mftCagrildi = true; return AdliYardimci.MftOkundu(); }),
            new SahteUsn(_ => { usnCagrildi = true; return AdliYardimci.Okundu(90); }),
            new SabitAyricalik(AyricalikDurumu.Yonetici));

        var sonuc = tarama.Calistir(simdi: AdliYardimci.An);

        Assert.False(mftCagrildi);
        Assert.False(usnCagrildi);
        Assert.Equal(Kapsam.Yok, sonuc.Kapsam.Genel);
    }

    [Fact]
    public void OncekiJournalIdFarkliysaBozulmaIsaretlenir()
    {
        var birim = AdliYardimci.Ntfs();
        var tarama = new AdliTarama(
            new SahteBirimSaglayici(birim),
            new SahteMft(_ => AdliYardimci.MftOkundu()),
            new SahteUsn(_ => AdliYardimci.Okundu(200, AdliYardimci.Kayit("hile.jar", UsnOlayi.Olusturma, 30))),
            new SabitAyricalik(AyricalikDurumu.Yonetici));

        var sonuc = tarama.Calistir(
            new Dictionary<string, ulong> { [birim.Kok] = 1234 },
            simdi: AdliYardimci.An);

        Assert.True(sonuc.Kapsam.AdliBozulmaVar);

        Assert.Single(sonuc.Izler);
    }

    [Fact]
    public void BirimYokKapsamYok()
    {
        var tarama = new AdliTarama(
            new SahteBirimSaglayici(),
            new SahteMft(_ => AdliYardimci.MftOkundu()),
            new SahteUsn(_ => AdliYardimci.Okundu(90)),
            new SabitAyricalik(AyricalikDurumu.Bilinmeyen));

        var sonuc = tarama.Calistir(simdi: AdliYardimci.An);
        Assert.Equal(Kapsam.Yok, sonuc.Kapsam.Genel);
        Assert.Empty(sonuc.Izler);
    }
}
