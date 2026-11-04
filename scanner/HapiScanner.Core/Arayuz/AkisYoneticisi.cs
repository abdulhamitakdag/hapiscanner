using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HapiScanner.Core.Arayuz;

public sealed class AsamaSatiri : INotifyPropertyChanged
{
    private AsamaDurumu _durum = AsamaDurumu.Bekliyor;

    public AsamaSatiri(Asama asama) => Asama = asama;

    public Asama Asama { get; }
    public string Baslik => Asama.Baslik;
    public string Aciklama => Asama.Aciklama;

    public AsamaDurumu Durum
    {
        get => _durum;
        set
        {
            if (_durum == value) return;
            _durum = value;
            Bildir();
            Bildir(nameof(Calisiyor));
            Bildir(nameof(Bitti));
        }
    }

    public bool Calisiyor => _durum == AsamaDurumu.Calisiyor;
    public bool Bitti => _durum is AsamaDurumu.Bitti or AsamaDurumu.Atlandi;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Bildir([CallerMemberName] string? ad = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(ad));
}

public interface IAsamaBildirici
{
    void Asamaya(Asama asama);
    void Ilerleme(int yuzde);
    void Not(string satir);
}

public sealed record AkisSonucu(
    string Sonuc,
    string ForensicCoverage,
    string RuntimeCoverage,
    IReadOnlyList<KanitIstegi> Kanitlar,
    string? Hata = null)
{
    public bool Basarili => Hata is null;
}

public interface ITaramaAkisi
{
    Task<AkisSonucu> CalistirAsync(string token, IAsamaBildirici bildirici, CancellationToken iptal);
}

public sealed class AkisYoneticisi : INotifyPropertyChanged, IAsamaBildirici
{
    private readonly ITaramaAkisi _akis;
    private readonly SynchronizationContext? _baglam;
    private CancellationTokenSource? _iptalKaynagi;

    private Ekran _aktifEkran = Ekran.Karsilama;
    private bool _onayVerildi;
    private string _kod = string.Empty;
    private string? _kodHatasi;
    private int _ilerleme;
    private Asama? _aktifAsama;
    private AkisSonucu? _sonuc;
    private string? _hataMesaji;
    private bool _mesgul;

    public AkisYoneticisi(ITaramaAkisi akis, SynchronizationContext? baglam = null)
    {
        _akis = akis;
        _baglam = baglam;
        Asamalar = new ObservableCollection<AsamaSatiri>(
            Asama.Tumu.Select(a => new AsamaSatiri(a)));
        Gunluk = new ObservableCollection<string>();
    }

    public ObservableCollection<AsamaSatiri> Asamalar { get; }
    public ObservableCollection<string> Gunluk { get; }

    public Ekran AktifEkran
    {
        get => _aktifEkran;
        private set => Ata(ref _aktifEkran, value);
    }

    public bool OnayVerildi
    {
        get => _onayVerildi;
        set
        {
            if (Ata(ref _onayVerildi, value)) Bildir(nameof(DevamEdilebilir));
        }
    }

    public bool DevamEdilebilir => _onayVerildi;

    public string Kod
    {
        get => _kod;
        set
        {
            if (!Ata(ref _kod, value ?? string.Empty)) return;
            KodHatasi = null;
            Bildir(nameof(KodGecerli));
            Bildir(nameof(BaglanilabilirMi));
        }
    }

    public bool KodGecerli => Token.GecerliMi(_kod);

    public bool BaglanilabilirMi => KodGecerli && !_mesgul;

    public string? KodHatasi
    {
        get => _kodHatasi;
        private set => Ata(ref _kodHatasi, value);
    }

    public int Ilerleme
    {
        get => _ilerleme;
        private set => Ata(ref _ilerleme, Math.Clamp(value, 0, 100));
    }

    public Asama? AktifAsama
    {
        get => _aktifAsama;
        private set
        {
            if (Ata(ref _aktifAsama, value)) Bildir(nameof(AktifAsamaBasligi));
        }
    }

    public string AktifAsamaBasligi => _aktifAsama?.Baslik ?? string.Empty;

    public AkisSonucu? Sonuc
    {
        get => _sonuc;
        private set
        {
            if (!Ata(ref _sonuc, value)) return;
            Bildir(nameof(SonucBasligi));
            Bildir(nameof(SonucTonu));
        }
    }

    public string SonucBasligi => _sonuc is null
        ? string.Empty
        : _sonuc.Sonuc switch
        {
            Adli.Sonuc.BilinenKanitYok => Metinler.SonucTemiz,
            Adli.Sonuc.Supheli => Metinler.SonucSupheli,
            Adli.Sonuc.Isaretli => Metinler.SonucIsaretli,
            Adli.Sonuc.Kararsiz => Metinler.SonucKararsiz,
            Adli.Sonuc.AdliBozulma => Metinler.SonucKararsiz,
            Adli.Sonuc.SinirliGecmis => Metinler.SonucKararsiz,
            _ => Metinler.SonucBasarisiz,
        };

    public string SonucTonu => _sonuc?.Sonuc switch
    {
        Adli.Sonuc.BilinenKanitYok => "iyi",
        Adli.Sonuc.Isaretli => "kotu",
        Adli.Sonuc.Supheli => "uyari",
        null => "notr",
        Adli.Sonuc.TaramaBasarisiz => "kotu",
        _ => "uyari",
    };

    public string? HataMesaji
    {
        get => _hataMesaji;
        private set => Ata(ref _hataMesaji, value);
    }

    public bool IptalEdilebilir => AktifEkran is Ekran.Tarama or Ekran.Baglaniyor;

    public void Basla() => AktifEkran = Ekran.Gizlilik;

    public bool GizlilikOnayla()
    {
        if (!OnayVerildi)
        {
            HataMesaji = Metinler.OnayUyarisi;
            return false;
        }
        HataMesaji = null;
        AktifEkran = Ekran.Kod;
        return true;
    }

    public void Geri()
    {
        AktifEkran = AktifEkran switch
        {
            Ekran.Gizlilik => Ekran.Karsilama,
            Ekran.Kod => Ekran.Gizlilik,
            Ekran.Hata => Ekran.Kod,
            _ => AktifEkran,
        };
        HataMesaji = null;
    }

    public async Task BaglanVeTaraAsync()
    {
        if (!OnayVerildi)
        {
            HataMesaji = Metinler.OnayUyarisi;
            AktifEkran = Ekran.Gizlilik;
            return;
        }
        if (!KodGecerli)
        {
            KodHatasi = Metinler.KodGecersiz;
            return;
        }
        if (_mesgul) return;

        _mesgul = true;
        Bildir(nameof(BaglanilabilirMi));
        Sifirla();
        AktifEkran = Ekran.Baglaniyor;
        Bildir(nameof(IptalEdilebilir));

        _iptalKaynagi = new CancellationTokenSource();
        try
        {
            AktifEkran = Ekran.Tarama;
            Bildir(nameof(IptalEdilebilir));
            var sonuc = await _akis.CalistirAsync(
                Token.Normalize(_kod), this, _iptalKaynagi.Token).ConfigureAwait(false);
            Sonuc = sonuc;
            Ilerleme = 100;
            AktifEkran = sonuc.Basarili ? Ekran.Sonuc : Ekran.Hata;
            if (!sonuc.Basarili) HataMesaji = sonuc.Hata;
            if (sonuc.Basarili) AsamalariKapat();
        }
        catch (OperationCanceledException)
        {
            HataMesaji = Metinler.IptalEdildi;
            AktifEkran = Ekran.Hata;
            AktifAsamayiIsaretle(AsamaDurumu.Basarisiz);
        }
        catch (ApiIstisnasi ex)
        {
            HataMesaji = ApiHataMetni(ex);
            AktifEkran = Ekran.Hata;
            AktifAsamayiIsaretle(AsamaDurumu.Basarisiz);
        }
        catch (Exception ex)
        {
            HataMesaji = ex.Message;
            AktifEkran = Ekran.Hata;
            AktifAsamayiIsaretle(AsamaDurumu.Basarisiz);
        }
        finally
        {
            _iptalKaynagi?.Dispose();
            _iptalKaynagi = null;
            _mesgul = false;
            Bildir(nameof(BaglanilabilirMi));
            Bildir(nameof(IptalEdilebilir));
        }
    }

    public void Iptal() => _iptalKaynagi?.Cancel();

    public static string ApiHataMetni(ApiIstisnasi ex) => ex.Kod switch
    {
        "TOKEN_NOT_FOUND" => "Kod bulunamadı. Yetkiliden yeni kod iste.",
        "TOKEN_EXPIRED" => "Kodun süresi dolmuş. Yetkiliden yeni kod iste.",
        "TOKEN_ALREADY_USED" => "Bu kod zaten kullanılmış.",
        "RULESET_TOO_OLD" => "Kural paketi eski. Scanner'ı güncelle.",
        "SCANNER_TOO_OLD" => "Scanner sürümü eski. Güncel sürümü indir.",
        _ => $"Sunucu hatası: {ex.Message}",
    };

    private void Sifirla()
    {
        HataMesaji = null;
        KodHatasi = null;
        Sonuc = null;
        Ilerleme = 0;
        AktifAsama = null;
        Gunluk.Clear();
        foreach (var satir in Asamalar) satir.Durum = AsamaDurumu.Bekliyor;
    }

    private void AsamalariKapat()
    {
        foreach (var satir in Asamalar)
            if (satir.Durum is AsamaDurumu.Bekliyor or AsamaDurumu.Calisiyor)
                satir.Durum = AsamaDurumu.Bitti;
    }

    private void AktifAsamayiIsaretle(AsamaDurumu durum)
    {
        foreach (var satir in Asamalar)
            if (satir.Durum == AsamaDurumu.Calisiyor)
                satir.Durum = durum;
    }

    void IAsamaBildirici.Asamaya(Asama asama) => Calistir(() =>
    {
        foreach (var satir in Asamalar)
        {
            if (satir.Durum == AsamaDurumu.Calisiyor) satir.Durum = AsamaDurumu.Bitti;
            if (satir.Asama.Protokol == asama.Protokol) satir.Durum = AsamaDurumu.Calisiyor;
        }
        AktifAsama = asama;
    });

    void IAsamaBildirici.Ilerleme(int yuzde) => Calistir(() =>
    {
        if (yuzde > Ilerleme) Ilerleme = yuzde;
    });

    void IAsamaBildirici.Not(string satir) => Calistir(() =>
    {
        var temiz = GunlukSuzgeci.Temizle(satir);
        if (temiz.Length == 0) return;
        Gunluk.Add(temiz);
        while (Gunluk.Count > 200) Gunluk.RemoveAt(0);
    });

    private void Calistir(Action eylem)
    {
        if (_baglam is null) eylem();
        else _baglam.Post(_ => eylem(), null);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Ata<T>(ref T alan, T deger, [CallerMemberName] string? ad = null)
    {
        if (EqualityComparer<T>.Default.Equals(alan, deger)) return false;
        alan = deger;
        Bildir(ad);
        return true;
    }

    private void Bildir(string? ad)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(ad));
}
