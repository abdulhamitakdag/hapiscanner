namespace HapiScanner.Core.Arayuz;

public enum Ekran
{
    Karsilama,
    Gizlilik,
    Kod,
    Baglaniyor,
    Tarama,
    Sonuc,
    Hata,
}

public sealed record Asama(string Protokol, string Baslik, string Aciklama, int Agirlik)
{
    public static readonly Asama Hazirlik =
        new("init", "Hazırlık", "Sürüm ve kural paketi doğrulanıyor", 5);

    public static readonly Asama KuralPaketi =
        new("ruleset", "Kural paketi", "İmzalı kural paketi indiriliyor ve doğrulanıyor", 5);

    public static readonly Asama Baglanti =
        new("connection", "Oyun bağlantısı", "Minecraft process'i ve sunucu bağlantısı kontrol ediliyor", 10);

    public static readonly Asama DosyaSistemi =
        new("filesystem", "Dosya sistemi", "Bilinen hile dizinleri ve aday dosyalar taranıyor", 35);

    public static readonly Asama Adli =
        new("forensics", "Silinmiş izler", "USN / MFT kayıtları üzerinden geçmiş izler okunuyor", 25);

    public static readonly Asama Runtime =
        new("runtime", "Çalışan yazılım", "Oyuna yüklenmiş modüller inceleniyor", 15);

    public static readonly Asama Rapor =
        new("report", "Rapor", "Sonuç imzalanıyor ve gönderiliyor", 5);

    public static IReadOnlyList<Asama> Tumu { get; } = new[]
    {
        Hazirlik, KuralPaketi, Baglanti, DosyaSistemi, Adli, Runtime, Rapor,
    };

    public static int BitisYuzdesi(Asama asama)
    {
        var toplam = Tumu.Sum(a => a.Agirlik);
        var birikim = 0;
        foreach (var a in Tumu)
        {
            birikim += a.Agirlik;
            if (ReferenceEquals(a, asama) || a.Protokol == asama.Protokol)
                return (int)Math.Round(birikim * 100.0 / toplam);
        }
        return 100;
    }
}

public enum AsamaDurumu
{
    Bekliyor,
    Calisiyor,
    Bitti,
    Atlandi,
    Basarisiz,
}
