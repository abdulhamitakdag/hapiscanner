namespace HapiScanner.Core.Adli;

public enum MftDurumu
{
    Mevcut,
    Yok,
    ErisimReddedildi,
}

public enum UsnDurumu
{
    Mevcut,

    Bozulmus,
    Yok,
    ErisimReddedildi,
}

public enum Kapsam
{
    Yok,
    Sinirli,
    Iyi,
}

public enum RuntimeErisimi
{
    Yok,
    Sinirli,
    Tam,
}

public sealed record BirimDegerlendirmesi(
    BirimBilgisi Birim,
    MftOkumaSonucu Mft,
    UsnOkumaSonucu Usn,
    ulong? OncekiJournalId = null);

public sealed record AdliKapsam(
    MftDurumu Mft,
    UsnDurumu Usn,
    Kapsam GecmisKapsami,
    RuntimeErisimi Runtime,
    AyricalikDurumu Ayricalik,
    TimeSpan TalepEdilenPencere,
    TimeSpan? KapsananPencere,
    IReadOnlyList<string> Notlar)
{

    public Kapsam Genel
    {
        get
        {
            if (Usn == UsnDurumu.Mevcut && Mft == MftDurumu.Mevcut && GecmisKapsami == Kapsam.Iyi)
                return Kapsam.Iyi;
            if (Usn is UsnDurumu.Yok or UsnDurumu.ErisimReddedildi && Mft != MftDurumu.Mevcut)
                return Kapsam.Yok;
            return Kapsam.Sinirli;
        }
    }

    public bool GecmisGuvenilirMi => Usn == UsnDurumu.Mevcut && GecmisKapsami == Kapsam.Iyi;

    public bool AdliBozulmaVar => Usn == UsnDurumu.Bozulmus;

    public string ProtokolDegeri => Genel switch
    {
        Kapsam.Iyi => "GOOD",
        Kapsam.Sinirli => "LIMITED",

        _ => "UNAVAILABLE",
    };

    public string RuntimeProtokolDegeri => Runtime switch
    {
        RuntimeErisimi.Tam => "GOOD",
        RuntimeErisimi.Sinirli => "LIMITED",
        _ => "UNAVAILABLE",
    };

    public string RuntimeInsanOkunur => Runtime switch
    {
        RuntimeErisimi.Tam => "FULL",
        RuntimeErisimi.Sinirli => "LIMITED",
        _ => "NONE",
    };

    public IReadOnlyList<string> Ozet() => new[]
    {
        $"MFT: {Mft switch { MftDurumu.Mevcut => "AVAILABLE", MftDurumu.ErisimReddedildi => "PERMISSION_DENIED", _ => "UNAVAILABLE" }}",
        $"USN: {Usn switch { UsnDurumu.Mevcut => "AVAILABLE", UsnDurumu.Bozulmus => "DEGRADED", UsnDurumu.ErisimReddedildi => "PERMISSION_DENIED", _ => "UNAVAILABLE" }}",
        $"USN kapsami: {(GecmisKapsami == Kapsam.Iyi ? "GOOD" : GecmisKapsami == Kapsam.Sinirli ? "LIMITED" : "NONE")}",
        $"Runtime erisim: {RuntimeInsanOkunur}",
        $"Admin yetkisi: {(Ayricalik == AyricalikDurumu.Yonetici ? "YES" : "NO")}",
    };
}

public sealed class AdliKapsamHesaplayici
{

    public TimeSpan TalepEdilenPencere { get; init; } = TimeSpan.FromDays(90);

    private const double YeterlilikOrani = 0.95;

    public AdliKapsam Hesapla(
        IReadOnlyList<BirimDegerlendirmesi> birimler,
        AyricalikDurumu ayricalik,
        RuntimeErisimi runtime,
        DateTimeOffset? simdi = null)
    {
        var an = simdi ?? DateTimeOffset.UtcNow;
        var notlar = new List<string>();

        var ilgililer = birimler.Where(b => b.Birim.Ilgili).ToList();
        if (ilgililer.Count == 0)
        {
            notlar.Add("Taramaya konu olabilecek birim bulunamadi.");
            return new AdliKapsam(MftDurumu.Yok, UsnDurumu.Yok, Kapsam.Yok, runtime,
                ayricalik, TalepEdilenPencere, null, notlar);
        }

        var mft = EnIyiMft(ilgililer, notlar);
        var (usn, kapsananPencere) = EnIyiUsn(ilgililer, an, notlar);

        var gecmis = usn switch
        {
            UsnDurumu.Mevcut when kapsananPencere is not null &&
                kapsananPencere.Value.TotalSeconds >= TalepEdilenPencere.TotalSeconds * YeterlilikOrani
                => Kapsam.Iyi,
            UsnDurumu.Mevcut or UsnDurumu.Bozulmus => Kapsam.Sinirli,
            _ => Kapsam.Yok,
        };

        if (gecmis == Kapsam.Sinirli && usn == UsnDurumu.Mevcut)
            notlar.Add("USN journal mevcut ancak talep edilen gecmis penceresinin tamamini kapsamiyor; " +
                       "eski kayitlar dondurulmus olabilir.");

        if (ayricalik != AyricalikDurumu.Yonetici &&
            (mft == MftDurumu.ErisimReddedildi || usn == UsnDurumu.ErisimReddedildi))
            notlar.Add(AyricalikMesaji.YoneticiGerekli);

        return new AdliKapsam(mft, usn, gecmis, runtime, ayricalik,
            TalepEdilenPencere, kapsananPencere, notlar);
    }

    private static MftDurumu EnIyiMft(IReadOnlyList<BirimDegerlendirmesi> birimler, List<string> notlar)
    {
        var durumlar = new List<MftDurumu>();
        foreach (var b in birimler)
        {
            if (!b.Birim.MftMumkunMu)
            {
                notlar.Add($"{b.Birim.Kok}: {b.Birim.DosyaSistemiAdi} — MFT desteklenmiyor, kapsam dusuruldu.");
                durumlar.Add(MftDurumu.Yok);
                continue;
            }

            durumlar.Add(b.Mft.Tur switch
            {
                MftSonucTuru.Okundu => MftDurumu.Mevcut,
                MftSonucTuru.ErisimReddedildi => MftDurumu.ErisimReddedildi,
                MftSonucTuru.Desteklenmiyor => MftDurumu.Yok,
                _ => MftDurumu.Yok,
            });

            if (b.Mft.Tur != MftSonucTuru.Okundu && b.Mft.Aciklama is { } aciklama)
                notlar.Add(aciklama);
        }

        if (durumlar.Contains(MftDurumu.Mevcut)) return MftDurumu.Mevcut;
        if (durumlar.Contains(MftDurumu.ErisimReddedildi)) return MftDurumu.ErisimReddedildi;
        return MftDurumu.Yok;
    }

    private (UsnDurumu, TimeSpan?) EnIyiUsn(
        IReadOnlyList<BirimDegerlendirmesi> birimler, DateTimeOffset an, List<string> notlar)
    {
        var durumlar = new List<UsnDurumu>();
        TimeSpan? enIyiPencere = null;

        foreach (var b in birimler)
        {
            if (!b.Birim.UsnMumkunMu)
            {
                notlar.Add($"{b.Birim.Kok}: {b.Birim.DosyaSistemiAdi} — USN journal desteklenmiyor, kapsam dusuruldu.");
                durumlar.Add(UsnDurumu.Yok);
                continue;
            }

            var sonuc = b.Usn;
            if (sonuc.Aciklama is { } aciklama && sonuc.Tur != UsnSonucTuru.Okundu)
                notlar.Add(aciklama);

            switch (sonuc.Tur)
            {
                case UsnSonucTuru.ErisimReddedildi:
                    durumlar.Add(UsnDurumu.ErisimReddedildi);
                    continue;
                case UsnSonucTuru.JournalYok:
                    notlar.Add($"{b.Birim.Kok}: USN journal bulunamadi. Bu tek basina hile kaniti degildir, " +
                               "yalnizca gecmis verisi yok demektir.");
                    durumlar.Add(UsnDurumu.Yok);
                    continue;
                case UsnSonucTuru.Desteklenmiyor:
                case UsnSonucTuru.Hata:
                    durumlar.Add(UsnDurumu.Yok);
                    continue;
            }

            if (sonuc.Journal is not { } journal)
            {
                durumlar.Add(UsnDurumu.Yok);
                continue;
            }

            if (b.OncekiJournalId is { } onceki && onceki != journal.JournalId)
            {
                notlar.Add($"{b.Birim.Kok}: USN journal kimligi degismis (onceki {onceki}, simdiki {journal.JournalId}). " +
                           "Journal silinmis veya yeniden olusturulmus olabilir; gecmis kapsami guvenilmez.");
                durumlar.Add(UsnDurumu.Bozulmus);
                continue;
            }

            if (journal.IlkUsn >= journal.SonUsn || journal.EnEskiKayitZamani is null)
            {
                notlar.Add($"{b.Birim.Kok}: USN journal bos veya zaman penceresi belirlenemedi.");
                durumlar.Add(UsnDurumu.Bozulmus);
                continue;
            }

            var pencere = an - journal.EnEskiKayitZamani.Value;
            if (pencere < TimeSpan.Zero) pencere = TimeSpan.Zero;
            if (enIyiPencere is null || pencere > enIyiPencere) enIyiPencere = pencere;
            durumlar.Add(UsnDurumu.Mevcut);
        }

        if (durumlar.Contains(UsnDurumu.Mevcut)) return (UsnDurumu.Mevcut, enIyiPencere);
        if (durumlar.Contains(UsnDurumu.Bozulmus)) return (UsnDurumu.Bozulmus, null);
        if (durumlar.Contains(UsnDurumu.ErisimReddedildi)) return (UsnDurumu.ErisimReddedildi, null);
        return (UsnDurumu.Yok, null);
    }
}
