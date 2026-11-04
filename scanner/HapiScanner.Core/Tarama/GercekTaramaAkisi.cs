using System.Text.Json;
using HapiScanner.Core.Adli;
using HapiScanner.Core.Arayuz;
using HapiScanner.Core.Dosya;
using HapiScanner.Core.Tespit;

namespace HapiScanner.Core.Tarama;

public interface IKuralPaketiKaynagi
{
    Task<(string Paket, string? ImzaBase64)> GetirAsync(CancellationToken iptal);
}

public sealed class GercekTaramaAkisi : ITaramaAkisi
{
    private readonly DogrulamaIstemcisi _istemci;
    private readonly IKuralPaketiKaynagi _paketKaynagi;
    private readonly KuralPaketiYukleyici _yukleyici;
    private readonly IOyunBaglantiBulucu _baglanti;
    private readonly Func<KuralMotoru, string, TaramaHatti> _hatKurucu;
    private readonly IReadOnlyList<TaramaKoku> _kokler;
    private readonly Func<DateTimeOffset> _saat;

    public GercekTaramaAkisi(
        DogrulamaIstemcisi istemci,
        IKuralPaketiKaynagi paketKaynagi,
        KuralPaketiYukleyici yukleyici,
        IOyunBaglantiBulucu baglanti,
        Func<KuralMotoru, string, TaramaHatti> hatKurucu,
        IReadOnlyList<TaramaKoku> kokler,
        Func<DateTimeOffset>? saat = null)
    {
        _istemci = istemci;
        _paketKaynagi = paketKaynagi;
        _yukleyici = yukleyici;
        _baglanti = baglanti;
        _hatKurucu = hatKurucu;
        _kokler = kokler;
        _saat = saat ?? (() => DateTimeOffset.UtcNow);
    }

    public int VarsayilanHeartbeatSaniye { get; init; } = 5;

    public async Task<AkisSonucu> CalistirAsync(
        string token, IAsamaBildirici bildirici, CancellationToken iptal)
    {

        bildirici.Asamaya(Asama.Hazirlik);
        bildirici.Not($"HapiScanner {ScannerBilgisi.Surum}");

        var claim = await _istemci.ClaimAsync(token, iptal).ConfigureAwait(false);
        bildirici.Not($"doğrulama açıldı: {claim.Username}");
        if (claim.NewDevice) bildirici.Not("bu bilgisayar ilk kez doğrulanıyor");
        bildirici.Ilerleme(Asama.BitisYuzdesi(Asama.Hazirlik));

        var durum = new HeartbeatDurumu(claim.Nonce, Asama.Hazirlik);

        bildirici.Asamaya(Asama.KuralPaketi);
        durum.Asama = Asama.KuralPaketi;

        var (paketJson, imza) = await _paketKaynagi.GetirAsync(iptal).ConfigureAwait(false);
        var paket = _yukleyici.Yukle(paketJson, imza);
        if (!paket.Kabul)
            return Basarisiz($"Kural paketi reddedildi: {paket.Aciklama ?? paket.Neden.ToString()}");

        bildirici.Not($"kural paketi {paket.Paket!.RulesetSurumu} doğrulandı");
        bildirici.Ilerleme(Asama.BitisYuzdesi(Asama.KuralPaketi));

        bildirici.Asamaya(Asama.Baglanti);
        durum.Asama = Asama.Baglanti;

        var oyun = _baglanti.Bul(claim.HapicraftEndpoints);
        bildirici.Not(oyun.Process == ProcessDurumu.Bulundu
            ? $"Minecraft bulundu ({oyun.AdayProcessler.Count} aday process)"
            : "çalışan Minecraft bulunamadı");
        bildirici.Not(oyun.Baglanti == BaglantiDurumu.Bulundu
            ? "HapiCraft bağlantısı doğrulandı"
            : "bu bilgisayarda HapiCraft bağlantısı görülmedi");
        bildirici.Ilerleme(Asama.BitisYuzdesi(Asama.Baglanti));

        var motor = new KuralMotoru(paket.Paket!);
        var hat = _hatKurucu(motor, motor.RulesetSurumu);

        using var taramaBitti = new CancellationTokenSource();
        using var birlesik = CancellationTokenSource.CreateLinkedTokenSource(
            iptal, taramaBitti.Token);

        var heartbeat = HeartbeatDongusu(claim, durum, bildirici,
            claim.HeartbeatInterval > 0 ? claim.HeartbeatInterval : VarsayilanHeartbeatSaniye,
            birlesik.Token);

        TaramaCiktisi cikti;
        try
        {
            cikti = await Task.Run(
                () => hat.Calistir(_kokler, oyun.AdayProcessler.Count > 0
                                       ? oyun.AdayProcessler[0] : (int?)null,
                                   new AsamaKopyalayici(bildirici, durum),
                                   simdi: _saat(), iptal: iptal),
                iptal).ConfigureAwait(false);
        }
        finally
        {
            taramaBitti.Cancel();
            await heartbeat.ConfigureAwait(false);
        }

        bildirici.Asamaya(Asama.Rapor);
        durum.Asama = Asama.Rapor;

        var govde = RaporGovdesi(cikti, oyun, claim);
        var cevap = await _istemci.RaporGonderAsync(
            claim.VerificationId, durum.Nonce, cikti.Sonuc, govde,
            cikti.AdliKapsam, cikti.RuntimeKapsam, cikti.Kanitlar.ToList(),
            _saat(), iptal).ConfigureAwait(false);

        bildirici.Not(cevap.Accepted ? "rapor yetkiliye iletildi" : "rapor kabul edilmedi");
        bildirici.Ilerleme(100);

        if (!cevap.Accepted)
            return Basarisiz("Sunucu raporu kabul etmedi.");

        return new AkisSonucu(cikti.Sonuc, cikti.AdliKapsam, cikti.RuntimeKapsam,
                              cikti.Kanitlar);
    }

    private async Task HeartbeatDongusu(
        ClaimCevabi claim, HeartbeatDurumu durum, IAsamaBildirici bildirici,
        int saniye, CancellationToken iptal)
    {
        var sira = 1L;
        while (!iptal.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(saniye), iptal).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                var cevap = await _istemci.HeartbeatAsync(
                    claim.VerificationId, durum.Nonce, sira++,
                    durum.Asama.Protokol, durum.Yuzde, _saat(),
                    CancellationToken.None).ConfigureAwait(false);
                durum.Nonce = cevap.Nonce;
            }
            catch (ApiIstisnasi hata) when (hata.Kod is "NETWORK_ERROR" or "TIMEOUT")
            {
                bildirici.Not("sunucuya ulaşılamadı, tekrar denenecek");
            }
        }
    }

    internal static string RaporGovdesi(
        TaramaCiktisi cikti, OyunBaglantiSonucu oyun, ClaimCevabi claim)
    {
        var govde = new Dictionary<string, object?>
        {
            ["scanner"] = ScannerBilgisi.Surum,
            ["sonuc"] = cikti.Sonuc,
            ["adli_kapsam"] = cikti.AdliKapsam,
            ["runtime_kapsam"] = cikti.RuntimeKapsam,
            ["incelenen_aday"] = cikti.IncelenenAday,
            ["gezilen_dizin"] = cikti.GezilenDizin,
            ["kanit_sayisi"] = cikti.Kanitlar.Count,
            ["oyun_process_bulundu"] = oyun.Process == ProcessDurumu.Bulundu,
            ["sunucuya_bagli"] = oyun.Baglanti == BaglantiDurumu.Bulundu,
            ["ag_durumu"] = claim.NetworkStatus,
            ["notlar"] = cikti.Notlar.Take(20).ToList(),
        };

        return JsonSerializer.Serialize(govde, new JsonSerializerOptions
        {
            WriteIndented = false,
        });
    }

    private static AkisSonucu Basarisiz(string hata)
        => new(Sonuc.TaramaBasarisiz, "UNAVAILABLE", "UNAVAILABLE",
               Array.Empty<KanitIstegi>(), hata);

    private sealed class HeartbeatDurumu
    {
        private string _nonce;

        public HeartbeatDurumu(string nonce, Asama asama)
        {
            _nonce = nonce;
            Asama = asama;
        }

        public string Nonce
        {
            get => Volatile.Read(ref _nonce);
            set => Volatile.Write(ref _nonce, value);
        }

        public Asama Asama { get; set; }
        public int Yuzde { get; set; }
    }

    private sealed class AsamaKopyalayici : IAsamaBildirici
    {
        private readonly IAsamaBildirici _ic;
        private readonly HeartbeatDurumu _durum;

        public AsamaKopyalayici(IAsamaBildirici ic, HeartbeatDurumu durum)
        {
            _ic = ic;
            _durum = durum;
        }

        public void Asamaya(Asama asama)
        {
            _durum.Asama = asama;
            _ic.Asamaya(asama);
        }

        public void Ilerleme(int yuzde)
        {
            _durum.Yuzde = yuzde;
            _ic.Ilerleme(yuzde);
        }

        public void Not(string satir) => _ic.Not(satir);
    }
}
