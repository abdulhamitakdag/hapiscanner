using System.Reflection;
using HapiScanner.Core;
using HapiScanner.Core.Arayuz;
using HapiScanner.Core.Model3B;

namespace HapiScanner.Tests;

public class TurkceMetinTestleri
{

    private static readonly (string Yanlis, string Dogru)[] Yazimlar =
    {
        ("dogrula", "doğrula"),
        ("dogru", "doğru"),
        ("sonuc", "sonuç"),
        ("basla", "başla"),
        ("bitis", "bitiş"),
        ("icin", "için"),
        ("gonder", "gönder"),
        ("goster", "göster"),
        ("goruntu", "görüntü"),
        ("gorul", "görül"),
        ("surum", "sürüm"),
        ("yuzde", "yüzde"),
        ("yukle", "yükle"),
        ("kullanici", "kullanıcı"),
        ("yapilir", "yapılır"),
        ("yapilan", "yapılan"),
        ("taraniyor", "taranıyor"),
        ("suphe", "şüphe"),
        ("silinmis", "silinmiş"),
        ("calis", "çalış"),
        ("acik", "açık"),
        ("kapsami", "kapsamı"),
        ("gecerli", "geçerli"),
        ("hatali", "hatalı"),
        ("iptal edildi.", "iptal edildi."),
    };

    private static void Denetle(string nereden, string metin)
    {
        var kucuk = metin.ToLowerInvariant();
        foreach (var (yanlis, dogru) in Yazimlar)
        {
            if (yanlis == dogru) continue;
            var yer = kucuk.IndexOf(yanlis, StringComparison.Ordinal);
            while (yer >= 0)
            {

                var sonra = yer + yanlis.Length;
                var ekUnlusu = sonra < kucuk.Length && "aeiıioöuü".Contains(kucuk[sonra]);
                Assert.True(
                    ekUnlusu,
                    $"{nereden}: \"{metin}\" icinde ASCII yazim '{yanlis}' var, '{dogru}' olmali");
                yer = kucuk.IndexOf(yanlis, sonra, StringComparison.Ordinal);
            }
        }
    }

    public static IEnumerable<object[]> MetinlerAlanlari()
    {
        foreach (var alan in typeof(Metinler).GetFields(BindingFlags.Public | BindingFlags.Static))
            if (alan.IsLiteral && alan.GetValue(null) is string s)
                yield return new object[] { alan.Name, s };
    }

    [Theory]
    [MemberData(nameof(MetinlerAlanlari))]
    public void ArayuzMetinleriTamTurkce(string ad, string metin)
    {

        if (ad is nameof(Metinler.UygulamaAdi) or nameof(Metinler.KodIpucu)) return;
        Denetle($"Metinler.{ad}", metin);
    }

    [Fact]
    public void GizlilikListeleriTamTurkce()
    {
        foreach (var liste in new[]
                 {
                     (nameof(Metinler.Yapmaz), Metinler.Yapmaz),
                     (nameof(Metinler.Bakar), Metinler.Bakar),
                     (nameof(Metinler.Gonderilir), Metinler.Gonderilir),
                     (nameof(Metinler.Gonderilmez), Metinler.Gonderilmez),
                 })
        foreach (var madde in liste.Item2)
            Denetle($"Metinler.{liste.Item1}", madde);
    }

    [Fact]
    public void AsamaBasliklariTamTurkce()
    {
        foreach (var asama in Asama.Tumu)
        {
            Denetle("Asama.Baslik", asama.Baslik);
            Denetle("Asama.Aciklama", asama.Aciklama);
        }
    }

    [Fact]
    public void AnimasyonAciklamalariTamTurkce()
    {
        foreach (var tur in Enum.GetValues<AnimasyonTuru>())
            Denetle($"Animasyon.{tur}", Animasyonlar.Tanim(tur).Aciklama);
    }

    [Fact]
    public void ApiHataMetinleriTamTurkce()
    {
        foreach (var kod in new[]
                 {
                     "TOKEN_NOT_FOUND", "TOKEN_EXPIRED", "TOKEN_ALREADY_USED",
                     "RULESET_TOO_OLD", "SCANNER_TOO_OLD",
                 })
            Denetle(kod, AkisYoneticisi.ApiHataMetni(new ApiIstisnasi(kod, "x", 400)));
    }

    [Fact]
    public void AsamaProtokolAlanlariAsciiKalir()
    {
        foreach (var asama in Asama.Tumu)
        {
            Assert.All(asama.Protokol, c => Assert.InRange(c, ' ', '~'));
            Assert.InRange(asama.Protokol.Length, 1, 64);
        }
    }
}
