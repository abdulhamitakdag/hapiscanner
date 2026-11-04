using System.Net;
using System.Text;
using HapiScanner.Core;

namespace HapiScanner.Tests;

internal sealed class SahteHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _cevapla;
    public List<string> Govdeler { get; } = new();
    public List<string> Yollar { get; } = new();

    public SahteHandler(Func<HttpRequestMessage, HttpResponseMessage> cevapla)
        => _cevapla = cevapla;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage istek, CancellationToken iptal)
    {
        Yollar.Add(istek.RequestUri!.AbsolutePath);
        if (istek.Content is not null)
            Govdeler.Add(await istek.Content.ReadAsStringAsync(iptal));
        return _cevapla(istek);
    }

    public static HttpResponseMessage Json(HttpStatusCode kod, string govde)
        => new(kod) { Content = new StringContent(govde, Encoding.UTF8, "application/json") };
}

public class TokenTestleri
{
    [Theory]
    [InlineData("HAPI-K7F2-8QW9", "HAPI-K7F2-8QW9")]
    [InlineData("hapi-k7f2-8qw9", "HAPI-K7F2-8QW9")]
    [InlineData("  HAPI-K7F2-8QW9  ", "HAPI-K7F2-8QW9")]
    [InlineData("HAPI K7F2 8QW9", "HAPI-K7F2-8QW9")]
    [InlineData("K7F2-8QW9", "HAPI-K7F2-8QW9")]
    [InlineData("HAPIK7F28QW9", "HAPI-K7F2-8QW9")]
    public void OyuncununYazimHatalariToparlanir(string girdi, string beklenen)
        => Assert.Equal(beklenen, Token.Normalize(girdi));

    [Theory]
    [InlineData("HAPI-K7F2-8QW9")]
    [InlineData("hapi-k7f2-8qw9")]
    public void GecerliTokenKabul(string girdi) => Assert.True(Token.GecerliMi(girdi));

    [Theory]
    [InlineData("")]
    [InlineData("HAPI")]
    [InlineData("HAPI-K7F2")]
    [InlineData("HAPI-K7F2-8QW9-EXTRA")]
    [InlineData("HAPI-I0O1-8QW9")]
    [InlineData("HAPI-K7F2-8QW")]
    [InlineData("<script>alert(1)</script>")]
    public void GecersizTokenRed(string girdi) => Assert.False(Token.GecerliMi(girdi));

    [Fact]
    public void NullCokmeyeYolAcmaz()
    {
        Assert.Equal(string.Empty, Token.Normalize(null!));
        Assert.False(Token.GecerliMi(null!));
    }
}

public class IstemciTestleri
{
    private const string ClaimCevap = """
    {"verification_id":"v-1","username":"TestOyuncu","nonce":"n-1","device_id":"d-1",
     "new_device":false,"network_status":"NETWORK_MATCH","heartbeat_interval":5,
     "hapicraft_endpoints":["203.0.113.5:25565"],"expires_at":"2026-11-03T12:05:00+00:00"}
    """;

    private static (DogrulamaIstemcisi, SahteHandler, YazilimAnahtari) Kur(
        Func<HttpRequestMessage, HttpResponseMessage> cevapla)
    {
        var handler = new SahteHandler(cevapla);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://ornek.test/") };
        var anahtar = new YazilimAnahtari();
        return (new DogrulamaIstemcisi(http, anahtar, "2026.11.03.1"), handler, anahtar);
    }

    [Fact]
    public async Task ClaimBasariliCevabiOkur()
    {
        var (istemci, handler, anahtar) = Kur(_ => SahteHandler.Json(HttpStatusCode.OK, ClaimCevap));
        using var _a = anahtar;

        var cevap = await istemci.ClaimAsync("hapi-k7f2-8qw9");

        Assert.Equal("v-1", cevap.VerificationId);
        Assert.Equal(5, cevap.HeartbeatInterval);
        Assert.Single(cevap.HapicraftEndpoints);
        Assert.Equal("/api/v1/scanner/claim", handler.Yollar[0]);
    }

    [Fact]
    public async Task ClaimTokeniNormalizeEdipGonderir()
    {
        var (istemci, handler, anahtar) = Kur(_ => SahteHandler.Json(HttpStatusCode.OK, ClaimCevap));
        using var _a = anahtar;
        await istemci.ClaimAsync("  hapi k7f2 8qw9 ");
        Assert.Contains("\"token\":\"HAPI-K7F2-8QW9\"", handler.Govdeler[0]);
    }

    [Fact]
    public async Task BozukTokenAgaCikmadanReddedilir()
    {
        var (istemci, handler, anahtar) = Kur(_ => throw new InvalidOperationException("istek gitmemeli"));
        using var _a = anahtar;

        var hata = await Assert.ThrowsAsync<ApiIstisnasi>(() => istemci.ClaimAsync("bozuk"));
        Assert.Equal("INVALID_TOKEN_FORMAT", hata.Kod);
        Assert.Empty(handler.Yollar);
    }

    [Fact]
    public async Task ClaimGizliAnahtariGondermez()
    {
        var (istemci, handler, anahtar) = Kur(_ => SahteHandler.Json(HttpStatusCode.OK, ClaimCevap));
        using var _a = anahtar;
        await istemci.ClaimAsync("HAPI-K7F2-8QW9");

        var govde = handler.Govdeler[0];

        Assert.DoesNotContain("PRIVATE", govde, StringComparison.OrdinalIgnoreCase);

        using var belge = System.Text.Json.JsonDocument.Parse(govde);
        Assert.Equal(anahtar.PublicKeyBase64(),
                     belge.RootElement.GetProperty("public_key").GetString());

        var pkcs8 = Convert.ToBase64String(anahtar.GizliAnahtariCikar());
        Assert.DoesNotContain(pkcs8, govde);

        Assert.DoesNotContain(pkcs8[..24], govde);
    }

    [Fact]
    public async Task SunucuHatasiYapilandirilmisSekildeAktarilir()
    {
        var (istemci, _, anahtar) = Kur(_ => SahteHandler.Json(HttpStatusCode.Unauthorized,
            """{"error":"INVALID_TOKEN","message":"kod gecersiz"}"""));
        using var _a = anahtar;

        var hata = await Assert.ThrowsAsync<ApiIstisnasi>(() => istemci.ClaimAsync("HAPI-K7F2-8QW9"));
        Assert.Equal("INVALID_TOKEN", hata.Kod);
        Assert.Equal(401, hata.Http);
    }

    [Fact]
    public async Task SemaDisiHataCevabiCokmeyeYolAcmaz()
    {
        var (istemci, _, anahtar) = Kur(_ => SahteHandler.Json(HttpStatusCode.BadGateway,
            "<html>nginx hata sayfasi</html>"));
        using var _a = anahtar;

        var hata = await Assert.ThrowsAsync<ApiIstisnasi>(() => istemci.ClaimAsync("HAPI-K7F2-8QW9"));
        Assert.Equal("HTTP_502", hata.Kod);
    }

    [Fact]
    public async Task AgHatasiYakalanir()
    {
        var (istemci, _, anahtar) = Kur(_ => throw new HttpRequestException("baglanti yok"));
        using var _a = anahtar;

        var hata = await Assert.ThrowsAsync<ApiIstisnasi>(() => istemci.ClaimAsync("HAPI-K7F2-8QW9"));
        Assert.Equal("NETWORK_ERROR", hata.Kod);
    }

    [Fact]
    public async Task HeartbeatImzayiIsteyeKoyar()
    {
        var (istemci, handler, anahtar) = Kur(_ => SahteHandler.Json(HttpStatusCode.OK,
            """{"nonce":"n-2","server_time":"2026-11-03T12:00:01+00:00"}"""));
        using var _a = anahtar;

        var cevap = await istemci.HeartbeatAsync("v-1", "n-1", 1, "filesystem", 42,
            DateTimeOffset.Parse("2026-11-03T12:00:00+00:00"));

        Assert.Equal("n-2", cevap.Nonce);
        Assert.Contains("\"signature\":", handler.Govdeler[0]);
        Assert.Contains("\"sequence_number\":1", handler.Govdeler[0]);
        Assert.Equal("/api/v1/scanner/heartbeat", handler.Yollar[0]);
    }

    [Fact]
    public async Task RaporGovdeHashiniKendiHesaplar()
    {
        var (istemci, handler, anahtar) = Kur(_ => SahteHandler.Json(HttpStatusCode.OK,
            """{"report_id":"r-1","accepted":true}"""));
        using var _a = anahtar;

        const string govde = "{\"kanit\":[]}";
        var cevap = await istemci.RaporGonderAsync("v-1", "n-2", "NO_KNOWN_ARTIFACTS",
            govde, "GOOD", "GOOD", new List<KanitIstegi>(),
            DateTimeOffset.Parse("2026-11-03T12:00:05+00:00"));

        Assert.True(cevap.Accepted);
        Assert.Contains($"\"report_hash\":\"{DogrulamaIstemcisi.RaporHash(govde)}\"",
                        handler.Govdeler[0]);
    }

    [Fact]
    public async Task RaporIstegindeDosyaYoluAlaniYok()
    {
        var (istemci, handler, anahtar) = Kur(_ => SahteHandler.Json(HttpStatusCode.OK,
            """{"report_id":"r-1","accepted":true}"""));
        using var _a = anahtar;

        await istemci.RaporGonderAsync("v-1", "n-2", "FLAGGED", "{}", "GOOD", "GOOD",
            new List<KanitIstegi> { new("CHEAT_0127", "SHA256", "KNOWN_CLIENT", "HIGH", "HIGH", true) },
            DateTimeOffset.UtcNow);

        var govde = handler.Govdeler[0];
        foreach (var yasakli in new[] { "path", "file_path", "filename", "C:\\\\", "Users" })
            Assert.DoesNotContain(yasakli, govde, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ScannerSurumuHerIstekteBildirilir()
    {
        var (istemci, handler, anahtar) = Kur(_ => SahteHandler.Json(HttpStatusCode.OK, ClaimCevap));
        using var _a = anahtar;
        await istemci.ClaimAsync("HAPI-K7F2-8QW9");
        Assert.Contains($"\"scanner_version\":\"{ScannerBilgisi.Surum}\"", handler.Govdeler[0]);
        Assert.Contains("\"ruleset_version\":\"2026.11.03.1\"", handler.Govdeler[0]);
    }

    [Fact]
    public async Task YazilimAnahtariSoftwareOlarakBildirilir()
    {
        var (istemci, handler, anahtar) = Kur(_ => SahteHandler.Json(HttpStatusCode.OK, ClaimCevap));
        using var _a = anahtar;
        await istemci.ClaimAsync("HAPI-K7F2-8QW9");
        Assert.Contains("\"assurance_level\":\"SOFTWARE\"", handler.Govdeler[0]);
    }
}
