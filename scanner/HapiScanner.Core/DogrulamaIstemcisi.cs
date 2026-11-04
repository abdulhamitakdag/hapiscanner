using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HapiScanner.Core;

public sealed class ApiIstisnasi : Exception
{
    public ApiIstisnasi(string kod, string mesaj, int http) : base(mesaj)
    {
        Kod = kod;
        Http = http;
    }

    public string Kod { get; }
    public int Http { get; }
}

public sealed class DogrulamaIstemcisi
{
    private readonly HttpClient _http;
    private readonly ICihazAnahtari _anahtar;
    private readonly string _rulesetSurumu;

    public DogrulamaIstemcisi(HttpClient http, ICihazAnahtari anahtar, string rulesetSurumu)
    {
        _http = http;
        _anahtar = anahtar;
        _rulesetSurumu = rulesetSurumu;
    }

    public async Task<ClaimCevabi> ClaimAsync(string token, CancellationToken iptal = default)
    {
        var normalize = Token.Normalize(token);
        if (!Token.GecerliMi(normalize))
            throw new ApiIstisnasi("INVALID_TOKEN_FORMAT", "kod bicimi hatali", 0);

        var istek = new ClaimIstegi(
            normalize,
            _anahtar.PublicKeyBase64(),
            _anahtar.Guvence == GuvenceSeviyesi.Tpm ? "TPM" : "SOFTWARE",
            ScannerBilgisi.Surum,
            _rulesetSurumu);

        return await GonderAsync<ClaimIstegi, ClaimCevabi>("api/v1/scanner/claim", istek, iptal);
    }

    public async Task<HeartbeatCevabi> HeartbeatAsync(string verificationId, string nonce,
        long siraNo, string asama, int yuzde, DateTimeOffset zaman,
        CancellationToken iptal = default)
    {
        var zamanMetni = ZamanMetni(zaman);
        var imza = _anahtar.Imzala(
            Imza.HeartbeatMetni(verificationId, nonce, siraNo, asama, zamanMetni));

        var istek = new HeartbeatIstegi(verificationId, nonce, imza, siraNo,
                                        asama, yuzde, zamanMetni);
        return await GonderAsync<HeartbeatIstegi, HeartbeatCevabi>(
            "api/v1/scanner/heartbeat", istek, iptal);
    }

    public async Task<RaporCevabi> RaporGonderAsync(string verificationId, string nonce,
        string sonuc, string govde, string adliKapsam, string runtimeKapsam,
        List<KanitIstegi> kanitlar, DateTimeOffset zaman, CancellationToken iptal = default)
    {
        var zamanMetni = ZamanMetni(zaman);
        var raporHash = RaporHash(govde);
        var imza = _anahtar.Imzala(Imza.RaporMetni(verificationId, nonce,
            ScannerBilgisi.Surum, _rulesetSurumu, raporHash, zamanMetni));

        var istek = new RaporIstegi(verificationId, nonce, imza, ScannerBilgisi.Surum,
            _rulesetSurumu, sonuc, raporHash, govde, adliKapsam, runtimeKapsam,
            kanitlar, zamanMetni);
        return await GonderAsync<RaporIstegi, RaporCevabi>(
            "api/v1/scanner/report", istek, iptal);
    }

    public static string RaporHash(string govde)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(govde)))
                  .ToLowerInvariant();

    public static string ZamanMetni(DateTimeOffset zaman)
        => zaman.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.ffffffzzz",
            System.Globalization.CultureInfo.InvariantCulture);

    private async Task<TCevap> GonderAsync<TIstek, TCevap>(string yol, TIstek istek,
        CancellationToken iptal)
    {
        HttpResponseMessage cevap;
        try
        {
            cevap = await _http.PostAsJsonAsync(yol, istek, iptal);
        }
        catch (HttpRequestException hata)
        {
            throw new ApiIstisnasi("NETWORK_ERROR", hata.Message, 0);
        }
        catch (TaskCanceledException) when (!iptal.IsCancellationRequested)
        {
            throw new ApiIstisnasi("TIMEOUT", "sunucu zamaninda cevap vermedi", 0);
        }

        var govde = await cevap.Content.ReadAsStringAsync(iptal);
        if (!cevap.IsSuccessStatusCode)
        {
            var kod = "HTTP_" + (int)cevap.StatusCode;
            var mesaj = "sunucu hata dondurdu";
            try
            {
                var hata = JsonSerializer.Deserialize<ApiHatasi>(govde);
                if (hata is not null && !string.IsNullOrEmpty(hata.Error))
                {
                    kod = hata.Error;
                    mesaj = hata.Message;
                }
            }
            catch (JsonException)
            {

            }
            throw new ApiIstisnasi(kod, mesaj, (int)cevap.StatusCode);
        }

        var sonuc = JsonSerializer.Deserialize<TCevap>(govde);
        if (sonuc is null)
            throw new ApiIstisnasi("INVALID_RESPONSE", "sunucu cevabi okunamadi",
                                   (int)cevap.StatusCode);
        return sonuc;
    }
}
