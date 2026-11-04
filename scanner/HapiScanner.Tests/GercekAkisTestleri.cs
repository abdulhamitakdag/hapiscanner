using System.Net;
using System.Text.Json;
using HapiScanner.Core;
using HapiScanner.Core.Adli;
using HapiScanner.Core.Arayuz;
using HapiScanner.Core.Dosya;
using HapiScanner.Core.Runtime;
using HapiScanner.Core.Tarama;
using HapiScanner.Core.Tespit;

namespace HapiScanner.Tests;

internal sealed class SahtePaketKaynagi : IKuralPaketiKaynagi
{
    private readonly string _paket;
    private readonly string? _imza;

    public SahtePaketKaynagi(string paket, string? imza = null)
    {
        _paket = paket;
        _imza = imza;
    }

    public Task<(string Paket, string? ImzaBase64)> GetirAsync(CancellationToken iptal)
        => Task.FromResult((_paket, _imza));
}

internal sealed class SahteBaglantiBulucu : IOyunBaglantiBulucu
{
    private readonly OyunBaglantiSonucu _sonuc;
    public SahteBaglantiBulucu(OyunBaglantiSonucu sonuc) => _sonuc = sonuc;
    public IReadOnlyList<string> GorulenEndpointler { get; private set; } = Array.Empty<string>();

    public OyunBaglantiSonucu Bul(IReadOnlyList<string> endpointler)
    {
        GorulenEndpointler = endpointler;
        return _sonuc;
    }
}

public class GercekAkisTestleri : IDisposable
{
    private const string Token = "HAPI-K7QD-3MWA";

    private readonly SahteYayinci _yayinci = new();

    public void Dispose() => _yayinci.Dispose();

    private static string ClaimCevabiJson(int heartbeat = 1) => JsonSerializer.Serialize(new
    {
        verification_id = "v-1",
        username = "Hapitolar",
        nonce = "nonce-0",
        device_id = "d-1",
        new_device = true,
        network_status = "SAME_NETWORK",
        heartbeat_interval = heartbeat,
        hapicraft_endpoints = new[] { "hapicraft.net:25565" },
        expires_at = "2026-11-03T13:00:00+00:00",
    });

    private static string HeartbeatCevabiJson(string nonce) => JsonSerializer.Serialize(new
    {
        nonce,
        accepted = true,
        server_time = "2026-11-03T12:00:05+00:00",
    });

    private static string RaporCevabiJson(bool kabul = true) => JsonSerializer.Serialize(new
    {
        report_id = "r-1",
        accepted = kabul,
    });

    private KuralPaketiYukleyici Yukleyici()
        => new(new[] { _yayinci.PublicKey }, new BellekSurumDeposu());

    private static string PaketJson() => PaketYardimci.Json();

    private GercekTaramaAkisi Akis(
        SahteHandler handler,
        IDosyaSistemi? ds = null,
        OyunBaglantiSonucu? baglanti = null,
        AdliTarama? adli = null,
        RuntimeAnalizi? runtime = null,
        string? paketJson = null,
        bool imzaliMi = true)
    {
        var dosyaSistemi = ds ?? new SahteDosyaSistemi();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://ornek/") };
        var istemci = new DogrulamaIstemcisi(http, new YazilimAnahtari(), "2026.11.03.1");
        var paket = paketJson ?? PaketJson();

        return new GercekTaramaAkisi(
            istemci,
            new SahtePaketKaynagi(paket, imzaliMi ? _yayinci.Imzala(paket) : null),
            Yukleyici(),
            new SahteBaglantiBulucu(baglanti ?? OyunBaglantiSonucu.HicYok()),
            (motor, surum) => new TaramaHatti(
                new DosyaKesfi(dosyaSistemi), dosyaSistemi,
                adli ?? TemizAdli(), runtime ?? TemizRuntime(), motor, surum),
            new[] { new TaramaKoku("mc", AdayNedeni.MinecraftModDizini) })
        {
            VarsayilanHeartbeatSaniye = 60,
        };
    }

    private static AdliTarama TemizAdli()
        => new(new SahteBirimSaglayici(AdliYardimci.Ntfs()),
               new SahteMft(_ => AdliYardimci.MftOkundu()),
               new SahteUsn(_ => AdliYardimci.Okundu(120)),
               new SabitAyricalik(AyricalikDurumu.Yonetici));

    private static RuntimeAnalizi TemizRuntime()
        => new(new SahteProcessSaglayici(
            new[] { RuntimeYardimci.Hedef() }, RuntimeYardimci.MesruModuller()));

    private static SahteHandler MutluYol(string? paketImzasi = null)
        => new(istek => istek.RequestUri!.AbsolutePath switch
        {
            var y when y.EndsWith("claim") =>
                SahteHandler.Json(HttpStatusCode.OK, ClaimCevabiJson(heartbeat: 60)),
            var y when y.EndsWith("heartbeat") =>
                SahteHandler.Json(HttpStatusCode.OK, HeartbeatCevabiJson("nonce-1")),
            var y when y.EndsWith("report") =>
                SahteHandler.Json(HttpStatusCode.OK, RaporCevabiJson()),
            _ => SahteHandler.Json(HttpStatusCode.NotFound, "{}"),
        });

    [Fact]
    public async Task MutluYolRaporGonderir()
    {
        var handler = MutluYol();
        var bildirici = new SahteBildirici();

        var sonuc = await Akis(handler).CalistirAsync(Token, bildirici, default);

        Assert.True(sonuc.Basarili);
        Assert.Contains("/api/v1/scanner/claim", handler.Yollar);
        Assert.Contains("/api/v1/scanner/report", handler.Yollar);
    }

    [Fact]
    public async Task TumAsamalarSirasiylaBildirilir()
    {
        var bildirici = new SahteBildirici();
        await Akis(MutluYol()).CalistirAsync(Token, bildirici, default);

        Assert.Equal(
            Asama.Tumu.Select(a => a.Protokol).ToArray(),
            bildirici.Asamalar.ToArray());
    }

    [Fact]
    public async Task IlerlemeYuzdeYuzdeBiter()
    {
        var bildirici = new SahteBildirici();
        await Akis(MutluYol()).CalistirAsync(Token, bildirici, default);

        Assert.Equal(100, bildirici.Yuzdeler[^1]);
        Assert.Equal(bildirici.Yuzdeler.OrderBy(y => y), bildirici.Yuzdeler);
    }

    [Fact]
    public async Task RaporGovdesindeDosyaYoluVeKullaniciAdiYok()
    {

        var ds = new SahteDosyaSistemi().DosyaEkle("mc", "hile.jar", 4096);
        var handler = MutluYol();

        await Akis(handler, ds).CalistirAsync(Token, new SahteBildirici(), default);

        var raporGovdesi = handler.Govdeler[^1];
        Assert.DoesNotContain("hile.jar", raporGovdesi);
        Assert.DoesNotContain("Hapitolar", raporGovdesi);
        Assert.DoesNotContain(@"C:\\", raporGovdesi);
    }

    [Fact]
    public async Task RaporGovdesiOzetAlanlariniTasir()
    {
        var handler = MutluYol();
        await Akis(handler).CalistirAsync(Token, new SahteBildirici(), default);

        using var belge = JsonDocument.Parse(
            JsonDocument.Parse(handler.Govdeler[^1]).RootElement
                .GetProperty("report_body").GetString()!);

        var kok = belge.RootElement;
        Assert.Equal(ScannerBilgisi.Surum, kok.GetProperty("scanner").GetString());
        Assert.True(kok.TryGetProperty("adli_kapsam", out _));
        Assert.True(kok.TryGetProperty("gezilen_dizin", out _));
    }

    [Fact]
    public async Task ImzasizKuralPaketiReddedilirVeTaramaYapilmaz()
    {
        var handler = MutluYol();
        var akis = Akis(handler, imzaliMi: false);

        var sonuc = await akis.CalistirAsync(Token, new SahteBildirici(), default);

        Assert.False(sonuc.Basarili);
        Assert.Contains("Kural paketi", sonuc.Hata);
        Assert.DoesNotContain("/api/v1/scanner/report", handler.Yollar);
    }

    [Fact]
    public async Task SunucuRaporuKabulEtmezseAkisBasarisiz()
    {
        var handler = new SahteHandler(istek => istek.RequestUri!.AbsolutePath switch
        {
            var y when y.EndsWith("claim") =>
                SahteHandler.Json(HttpStatusCode.OK, ClaimCevabiJson(60)),
            var y when y.EndsWith("report") =>
                SahteHandler.Json(HttpStatusCode.OK, RaporCevabiJson(kabul: false)),
            _ => SahteHandler.Json(HttpStatusCode.OK, HeartbeatCevabiJson("n")),
        });

        var sonuc = await Akis(handler).CalistirAsync(Token, new SahteBildirici(), default);

        Assert.False(sonuc.Basarili);
        Assert.Equal(Sonuc.TaramaBasarisiz, sonuc.Sonuc);
    }

    [Fact]
    public async Task GecersizKodSunucuyaGitmez()
    {
        var handler = MutluYol();
        await Assert.ThrowsAsync<ApiIstisnasi>(() =>
            Akis(handler).CalistirAsync("BOZUK", new SahteBildirici(), default));

        Assert.Empty(handler.Yollar);
    }

    [Fact]
    public async Task EndpointListesiSunucudanGelir()
    {

        var bulucu = new SahteBaglantiBulucu(OyunBaglantiSonucu.HicYok());
        var handler = MutluYol();
        var ds = new SahteDosyaSistemi();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://ornek/") };

        var akis = new GercekTaramaAkisi(
            new DogrulamaIstemcisi(http, new YazilimAnahtari(), "2026.11.03.1"),
            new SahtePaketKaynagi(PaketJson(), _yayinci.Imzala(PaketJson())),
            Yukleyici(),
            bulucu,
            (motor, surum) => new TaramaHatti(new DosyaKesfi(ds), ds,
                TemizAdli(), TemizRuntime(), motor, surum),
            new[] { new TaramaKoku("mc", AdayNedeni.MinecraftModDizini) });

        await akis.CalistirAsync(Token, new SahteBildirici(), default);

        Assert.Equal(new[] { "hapicraft.net:25565" }, bulucu.GorulenEndpointler);
    }

    [Fact]
    public async Task OyunBulunamazsaTaramaYineDeTamamlanir()
    {

        var handler = MutluYol();
        var sonuc = await Akis(handler, baglanti: OyunBaglantiSonucu.HicYok())
            .CalistirAsync(Token, new SahteBildirici(), default);

        Assert.True(sonuc.Basarili);
        var govde = JsonDocument.Parse(handler.Govdeler[^1]).RootElement
            .GetProperty("report_body").GetString()!;
        Assert.Contains("\"sunucuya_bagli\":false", govde);
    }

    [Fact]
    public async Task IptalEdilenTaramaRaporGondermez()
    {
        var handler = MutluYol();
        using var iptal = new CancellationTokenSource();
        iptal.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Akis(handler).CalistirAsync(Token, new SahteBildirici(), iptal.Token));

        Assert.DoesNotContain("/api/v1/scanner/report", handler.Yollar);
    }

    [Fact]
    public async Task HeartbeatSirasindakiAgHatasiTaramayiDusurmez()
    {

        var heartbeatSayaci = 0;
        var handler = new SahteHandler(istek =>
        {
            var yol = istek.RequestUri!.AbsolutePath;
            if (yol.EndsWith("claim"))
                return SahteHandler.Json(HttpStatusCode.OK, ClaimCevabiJson(heartbeat: 1));
            if (yol.EndsWith("heartbeat"))
            {
                heartbeatSayaci++;
                throw new HttpRequestException("ag yok");
            }
            return SahteHandler.Json(HttpStatusCode.OK, RaporCevabiJson());
        });

        var ds = new SahteDosyaSistemi();
        for (var i = 0; i < 200; i++) ds.DosyaEkle("mc", $"d{i}.jar");

        var sonuc = await Akis(handler, ds).CalistirAsync(Token, new SahteBildirici(), default);

        Assert.True(sonuc.Basarili);
    }
}
