package tr.hapicraft.hapiverify;

import org.junit.jupiter.api.Test;

import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.time.Duration;
import java.util.ArrayList;
import java.util.List;
import java.util.Optional;
import java.util.UUID;
import java.util.function.Function;
import javax.net.ssl.SSLSession;

import static org.junit.jupiter.api.Assertions.*;

class ApiIstemcisiTesti {

    private final List<HttpRequest> gorulen = new ArrayList<>();

    private ApiIstemcisi istemci(int kod, String govde) {
        Function<HttpRequest, HttpResponse<String>> gonderici = istek -> {
            gorulen.add(istek);
            return cevap(kod, govde);
        };
        return new ApiIstemcisi("http://test/", "jeton", Duration.ofSeconds(2), gonderici);
    }

    private static HttpResponse<String> cevap(int kod, String govde) {
        return new HttpResponse<>() {
            @Override public int statusCode() { return kod; }
            @Override public HttpRequest request() { return null; }
            @Override public Optional<HttpResponse<String>> previousResponse() { return Optional.empty(); }
            @Override public java.net.http.HttpHeaders headers() {
                return java.net.http.HttpHeaders.of(java.util.Map.of(), (a, b) -> true);
            }
            @Override public String body() { return govde; }
            @Override public Optional<SSLSession> sslSession() { return Optional.empty(); }
            @Override public java.net.URI uri() { return java.net.URI.create("http://test"); }
            @Override public java.net.http.HttpClient.Version version() {
                return java.net.http.HttpClient.Version.HTTP_1_1;
            }
        };
    }

    @Test
    void olusturTokenVeTtlOkur() {
        var api = istemci(200, "{\"verification_id\":\"v-1\",\"token\":\"HAPI-K7QD-3MWA\",\"ttl_seconds\":600}");
        var sonuc = api.olustur(UUID.randomUUID());

        assertEquals("v-1", sonuc.verificationId());
        assertEquals("HAPI-K7QD-3MWA", sonuc.token());
        assertEquals(600, sonuc.ttlSaniye());
    }

    @Test
    void pluginJetonuBasliktaGider() {
        var api = istemci(200, "{\"verification_id\":\"v\",\"token\":\"t\"}");
        api.olustur(UUID.randomUUID());

        assertEquals("jeton", gorulen.get(0).headers().firstValue("X-Plugin-Token").orElse(null));
    }

    @Test
    void sondakiEgikCizgiIkiKereEklenmez() {
        var api = istemci(200, "{\"verification_id\":\"v\",\"token\":\"t\"}");
        api.olustur(UUID.randomUUID());

        assertFalse(gorulen.get(0).uri().toString().contains("//api"));
    }

    @Test
    void kayitYoksaDurumNull() {
        var api = istemci(404, "{\"error\":\"NO_VERIFICATION\",\"message\":\"kayit yok\"}");
        assertNull(api.durum(UUID.randomUUID()));
    }

    @Test
    void iptalKayitYoksaFalseDoner() {
        var api = istemci(404, "{\"error\":\"NO_VERIFICATION\",\"message\":\"kayit yok\"}");
        assertFalse(api.iptal(UUID.randomUUID()));
    }

    @Test
    void yetkisizHatasiKoduyleGelir() {
        var api = istemci(401, "{\"error\":\"UNAUTHORIZED\",\"message\":\"yetkisiz\"}");
        var hata = assertThrows(ApiIstemcisi.ApiHatasi.class, () -> api.olustur(UUID.randomUUID()));
        assertEquals("UNAUTHORIZED", hata.kod());
    }

    @Test
    void jsonOlmayanCevapCokmez() {
        var api = istemci(502, "<html>bad gateway</html>");
        var hata = assertThrows(ApiIstemcisi.ApiHatasi.class, () -> api.durum(UUID.randomUUID()));
        assertEquals("HTTP_502", hata.kod());
    }

    @Test
    void bosBasariliCevapHataSayilir() {
        var api = istemci(200, "");
        assertThrows(ApiIstemcisi.ApiHatasi.class, () -> api.olustur(UUID.randomUUID()));
    }

    @Test
    void durumAlanlariOkunur() {
        var api = istemci(200, "{\"verification_id\":\"v-9\",\"status\":\"COMPLETED\","
                + "\"network_status\":\"MATCH\",\"result\":\"NO_KNOWN_ARTIFACTS\"}");
        var durum = api.durum(UUID.randomUUID());

        assertEquals("COMPLETED", durum.status());
        assertEquals("NO_KNOWN_ARTIFACTS", durum.result());
    }

    @Test
    void bosResultNullOkunur() {
        var api = istemci(200, "{\"verification_id\":\"v\",\"status\":\"PENDING\",\"result\":null}");
        assertNull(api.durum(UUID.randomUUID()).result());
    }

    @Test
    void acikDogrulamaYoksaIptalFalseDoner() {
        var api = istemci(404, "{\"error\":\"NO_ACTIVE_VERIFICATION\",\"message\":\"acik dogrulama yok\"}");
        assertFalse(api.iptal(UUID.randomUUID()));
    }

    @Test
    void oturumGirisiGonderilir() {
        var api = istemci(200, "{\"accepted\":true}");
        UUID oyuncu = UUID.randomUUID();
        api.oturumKatildi(new OturumYuzeyi.Oturum(oyuncu, "TestOyuncu", "oturum-AAA", "203.0.113.10"));

        HttpRequest istek = gorulen.get(0);
        assertEquals("POST", istek.method());
        assertEquals("http://test/api/v1/plugin/sessions", istek.uri().toString());
        assertEquals("{\"minecraft_uuid\":\"" + oyuncu + "\",\"username\":\"TestOyuncu\","
                + "\"game_session_id\":\"oturum-AAA\",\"game_ip\":\"203.0.113.10\"}", govde(istek));
    }

    @Test
    void oyuncuAdindakiTirnakGovdeyiBozmaz() {
        var api = istemci(200, "{\"accepted\":true}");
        api.oturumKatildi(new OturumYuzeyi.Oturum(UUID.randomUUID(), "a\"b\\c", "oturum-AAA", null));

        String g = govde(gorulen.get(0));
        assertTrue(g.contains("\"username\":\"a\\\"b\\\\c\""), g);
        assertFalse(g.contains("game_ip"));
    }

    @Test
    void oturumCikisiKimlikIleGider() {
        var api = istemci(200, "{\"closed\":true}");
        UUID oyuncu = UUID.randomUUID();
        api.oturumAyrildi(oyuncu, "oturum-A A");

        HttpRequest istek = gorulen.get(0);
        assertEquals("DELETE", istek.method());
        assertEquals("http://test/api/v1/plugin/sessions/" + oyuncu
                + "?game_session_id=oturum-A+A", istek.uri().toString());
    }

    @Test
    void oturumGoruntusuTamListeGonderir() {
        var api = istemci(200, "{\"online\":2}");
        api.oturumGoruntusu(List.of(
                new OturumYuzeyi.Oturum(UUID.randomUUID(), "Bir", "o-1", null),
                new OturumYuzeyi.Oturum(UUID.randomUUID(), "Iki", "o-2", null)));

        HttpRequest istek = gorulen.get(0);
        assertEquals("PUT", istek.method());
        assertEquals("http://test/api/v1/plugin/sessions", istek.uri().toString());
        assertTrue(govde(istek).startsWith("{\"sessions\":["));
    }

    @Test
    void bosGoruntuDeGonderilir() {

        var api = istemci(200, "{\"online\":0}");
        api.oturumGoruntusu(List.of());
        assertEquals("{\"sessions\":[]}", govde(gorulen.get(0)));
    }

    @Test
    void oturumBeslemesiDesteklenmiyorsaKodOkunur() {
        var api = istemci(501, "{\"error\":\"SESSION_FEED_UNSUPPORTED\",\"message\":\"kabul etmiyor\"}");
        var hata = assertThrows(ApiIstemcisi.ApiHatasi.class,
                () -> api.oturumGoruntusu(List.of()));
        assertEquals("SESSION_FEED_UNSUPPORTED", hata.kod());
    }

    private static String govde(HttpRequest istek) {
        var yayinci = istek.bodyPublisher().orElseThrow();
        var parcalar = new java.util.ArrayList<java.nio.ByteBuffer>();
        var bitti = new java.util.concurrent.CountDownLatch(1);
        yayinci.subscribe(new java.util.concurrent.Flow.Subscriber<>() {
            @Override public void onSubscribe(java.util.concurrent.Flow.Subscription s) {
                s.request(Long.MAX_VALUE);
            }
            @Override public void onNext(java.nio.ByteBuffer parca) { parcalar.add(parca); }
            @Override public void onError(Throwable hata) { bitti.countDown(); }
            @Override public void onComplete() { bitti.countDown(); }
        });
        try {
            assertTrue(bitti.await(2, java.util.concurrent.TimeUnit.SECONDS));
        } catch (InterruptedException kesildi) {
            Thread.currentThread().interrupt();
            throw new AssertionError(kesildi);
        }
        var cikti = new StringBuilder();
        for (var parca : parcalar) {
            cikti.append(java.nio.charset.StandardCharsets.UTF_8.decode(parca));
        }
        return cikti.toString();
    }
}
