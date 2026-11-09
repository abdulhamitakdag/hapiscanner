namespace HapiScanner.Core.Arayuz;

public static class Metinler
{
    public const string UygulamaAdi = "HapiScanner";
    public const string Slogan = "HapiCraft PC doğrulama aracı";

    public const string KarsilamaBaslik = "Bilgisayarını doğrula";

    public const string KarsilamaOzet =
        "Bu araç, bir yetkilinin başlattığı doğrulama için bilgisayarındaki " +
        "Minecraft hile izlerini arar. Tarama senin onayınla başlar, " +
        "istediğin an iptal edebilirsin.";

    public const string KarsilamaButon = "Başla";

    public const string GizlilikBaslik = "Tarama başlamadan önce";

    public const string GizlilikAltBaslik =
        "Bu aracın ne yaptığını ve ne yapmadığını okumadan devam etme. " +
        "Kaynak kodu açıktır, buradaki her maddeyi doğrulayabilirsin.";

    public static IReadOnlyList<string> Yapmaz { get; } = new[]
    {
        "ekran görüntüsü almaz",
        "ekran kaydı yapmaz",
        "mikrofonu kullanmaz",
        "web kamerasını kullanmaz",
        "tuş vuruşlarını kaydetmez (keylogger yoktur)",
        "panoyu (clipboard) okumaz",
        "tarayıcı geçmişini taramaz",
        "kayıtlı şifreleri okumaz",
        "Discord token'ı aramaz",
        "belgelerinizi sunucuya yüklemez",
        "uzak masaüstü / uzaktan erişim sağlamaz",
        "arka planda siz kapattıktan sonra çalışmaya devam etmez",
    };

    public static IReadOnlyList<string> Bakar { get; } = new[]
    {
        "Minecraft dizinleri: .minecraft ve altındaki mods, versions, libraries, logs",
        "Bilinen launcher dizinleri",
        "İndirilenler, Masaüstü, Temp gibi tipik konumlar (aday filtresiyle, körlemesine değil)",
        "Dosya sistemi metadata'sı ve geçmişi (MFT, USN Journal, Geri Dönüşüm Kutusu)",
        "HapiCraft'a bağlı olan aktif Minecraft process'i",
        "O process'e yüklenmiş native modüller ve JVM başlatma parametreleri",
    };

    public static IReadOnlyList<string> Gonderilir { get; } = new[]
    {
        "Kural kimliği ve kural paketi sürümü",
        "Kanıt tipi ve kategori (örneğin KNOWN_HASH, AUTOCLICKER)",
        "Güven ve şiddet seviyesi, ilk/son görülme zamanı",
        "Tarama kapsamı (GOOD / LIMITED / UNAVAILABLE)",
        "Scanner sürümü, rapor özeti ve imza",
    };

    public static IReadOnlyList<string> Gonderilmez { get; } = new[]
    {
        "Dosya içerikleri",
        "Kullanıcı adınızı içeren tam dosya yolları",
        "Alakasız dosyaların isimleri",
        "Geri Dönüşüm Kutusu'nun içeriği",
        "Sohbet içeriği",
        "Tüm process listesi",
        "Donanım seri numaraları (MAC, anakart, disk seri no)",
        "Private key — cihazınızdan asla çıkmaz",
    };

    public const string OnayMetni =
        "Okudum ve bu bilgisayarda taramanın yapılmasını onaylıyorum.";

    public const string OnayUyarisi =
        "Devam etmek için onay kutusunu işaretlemen gerekiyor.";

    public const string KodBaslik = "Doğrulama kodunu gir";

    public const string KodAciklama =
        "Oyun içinde sohbetteki koda tıklayıp kopyala, buraya yapıştır. HAPI- " +
        "öneki ve tireler kendiliğinden yazılır, kod kısa süre geçerlidir.";

    public const string KodGecersiz = "Kod biçimi hatalı. Örnek: HAPI-K7QD-3MWA";
    public const string KodIpucu = "HAPI-XXXX-XXXX";
    public const string BaglanButon = "Bağlan";
    public const string Baglaniyor = "Sunucuya bağlanılıyor...";

    public const string TaramaBaslik = "Tarama sürüyor";

    public const string TaramaAciklama =
        "Oyunu kapatma ve bu pencereyi açık bırak. Tarama bitince sonuç " +
        "otomatik olarak yetkiliye iletilir.";

    public const string IptalButon = "İptal et";
    public const string IptalEdildi = "Tarama iptal edildi.";

    public const string SonucTemiz = "Bilinen hile izi bulunamadı";
    public const string SonucSupheli = "Şüpheli gösterge bulundu";
    public const string SonucIsaretli = "Hile kanıtı bulundu";
    public const string SonucKararsiz = "Sonuç kesin değil";
    public const string SonucBasarisiz = "Tarama tamamlanamadı";

    public const string KapatButon = "Kapat";

    public const string KapsamUyarisi =
        "Yönetici yetkisi olmadan çalıştığın için silinmiş dosya izleri okunamadı. " +
        "Sonuç geçerlidir ama kapsamı düşüktür.";

    public const string SonucNotu =
        "Bu sonuç bir ceza kararı değildir. Kararı her zaman bir yetkili verir.";
}
