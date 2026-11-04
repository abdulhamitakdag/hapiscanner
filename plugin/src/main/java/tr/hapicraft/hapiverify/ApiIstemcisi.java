package tr.hapicraft.hapiverify;

import com.google.gson.JsonArray;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;

import java.net.URI;
import java.net.URLEncoder;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.nio.charset.StandardCharsets;
import java.time.Duration;
import java.util.Collection;
import java.util.UUID;
import java.util.function.Function;

public final class ApiIstemcisi implements OturumYuzeyi {

    public static final class ApiHatasi extends RuntimeException {
        private final String kod;

        public ApiHatasi(String kod, String mesaj) {
            super(mesaj);
            this.kod = kod;
        }

        public String kod() { return kod; }
    }

    public record Olusturma(String verificationId, String token, long ttlSaniye) { }

    public record Durum(String verificationId, String status, String networkStatus, String result) { }

    private final String taban;
    private final String jeton;
    private final Duration zamanAsimi;
    private final Function<HttpRequest, HttpResponse<String>> gonderici;

    public ApiIstemcisi(Ayarlar ayarlar) {
        this(ayarlar.apiUrl(), ayarlar.pluginJetonu(), ayarlar.zamanAsimi(),
             varsayilanGonderici(ayarlar.zamanAsimi()));
    }

    public ApiIstemcisi(String taban, String jeton, Duration zamanAsimi,
                        Function<HttpRequest, HttpResponse<String>> gonderici) {
        this.taban = taban.endsWith("/") ? taban.substring(0, taban.length() - 1) : taban;
        this.jeton = jeton;
        this.zamanAsimi = zamanAsimi;
        this.gonderici = gonderici;
    }

    private static Function<HttpRequest, HttpResponse<String>> varsayilanGonderici(Duration zamanAsimi) {
        HttpClient istemci = HttpClient.newBuilder()
                .connectTimeout(zamanAsimi)
                .followRedirects(HttpClient.Redirect.NEVER)
                .build();
        return istek -> {
            try {
                return istemci.send(istek, HttpResponse.BodyHandlers.ofString(StandardCharsets.UTF_8));
            } catch (java.io.IOException hata) {
                throw new ApiHatasi("NETWORK", hata.getClass().getSimpleName());
            } catch (InterruptedException kesildi) {
                Thread.currentThread().interrupt();
                throw new ApiHatasi("INTERRUPTED", "istek kesildi");
            }
        };
    }

    public Olusturma olustur(UUID oyuncu) {
        String govde = "{\"minecraft_uuid\":\"" + oyuncu + "\"}";
        JsonObject cevap = cagir(istek("/api/v1/plugin/verifications")
                .POST(HttpRequest.BodyPublishers.ofString(govde, StandardCharsets.UTF_8))
                .header("Content-Type", "application/json")
                .build());
        return new Olusturma(
                metin(cevap, "verification_id"),
                metin(cevap, "token"),
                cevap.has("ttl_seconds") ? cevap.get("ttl_seconds").getAsLong() : 0L);
    }

    @Override
    public void oturumKatildi(Oturum oturum) {
        cagir(istek("/api/v1/plugin/sessions")
                .POST(HttpRequest.BodyPublishers.ofString(oturumJson(oturum), StandardCharsets.UTF_8))
                .header("Content-Type", "application/json")
                .build());
    }

    @Override
    public void oturumAyrildi(UUID oyuncu, String oturumKimligi) {
        cagir(istek("/api/v1/plugin/sessions/" + oyuncu
                + "?game_session_id=" + URLEncoder.encode(oturumKimligi, StandardCharsets.UTF_8))
                .DELETE().build());
    }

    @Override
    public void oturumGoruntusu(Collection<Oturum> oturumlar) {
        JsonArray dizi = new JsonArray();
        for (Oturum o : oturumlar) dizi.add(oturumNesnesi(o));
        JsonObject govde = new JsonObject();
        govde.add("sessions", dizi);
        cagir(istek("/api/v1/plugin/sessions")
                .PUT(HttpRequest.BodyPublishers.ofString(govde.toString(), StandardCharsets.UTF_8))
                .header("Content-Type", "application/json")
                .build());
    }

    private static String oturumJson(Oturum oturum) {
        return oturumNesnesi(oturum).toString();
    }

    private static JsonObject oturumNesnesi(Oturum oturum) {

        JsonObject n = new JsonObject();
        n.addProperty("minecraft_uuid", oturum.oyuncu().toString());
        n.addProperty("username", oturum.oyuncuAdi());
        n.addProperty("game_session_id", oturum.oturumKimligi());
        if (oturum.ip() != null) n.addProperty("game_ip", oturum.ip());
        return n;
    }

    public Durum durum(UUID oyuncu) {
        try {
            JsonObject cevap = cagir(istek("/api/v1/plugin/verifications/" + oyuncu).GET().build());
            return new Durum(metin(cevap, "verification_id"), metin(cevap, "status"),
                             metin(cevap, "network_status"), metin(cevap, "result"));
        } catch (ApiHatasi hata) {
            if ("NO_VERIFICATION".equals(hata.kod())) return null;
            throw hata;
        }
    }

    public boolean iptal(UUID oyuncu) {
        try {
            cagir(istek("/api/v1/plugin/verifications/" + oyuncu).DELETE().build());
            return true;
        } catch (ApiHatasi hata) {
            if ("NO_VERIFICATION".equals(hata.kod())
                    || "NO_ACTIVE_VERIFICATION".equals(hata.kod())) return false;
            throw hata;
        }
    }

    private HttpRequest.Builder istek(String yol) {
        return HttpRequest.newBuilder(URI.create(taban + yol))
                .timeout(zamanAsimi)
                .header("X-Plugin-Token", jeton)
                .header("Accept", "application/json");
    }

    private JsonObject cagir(HttpRequest istek) {
        HttpResponse<String> cevap = gonderici.apply(istek);
        JsonObject govde = coz(cevap.body());
        if (cevap.statusCode() >= 400) {
            String kod = govde != null && govde.has("error")
                    ? govde.get("error").getAsString() : "HTTP_" + cevap.statusCode();
            String mesaj = govde != null && govde.has("message")
                    ? govde.get("message").getAsString() : "istek basarisiz";
            throw new ApiHatasi(kod, mesaj);
        }
        if (govde == null) throw new ApiHatasi("BAD_RESPONSE", "cevap okunamadi");
        return govde;
    }

    private static JsonObject coz(String govde) {
        if (govde == null || govde.isBlank()) return null;
        try {
            var cozulen = JsonParser.parseString(govde);
            return cozulen.isJsonObject() ? cozulen.getAsJsonObject() : null;
        } catch (RuntimeException bozuk) {
            return null;
        }
    }

    private static String metin(JsonObject govde, String alan) {
        if (!govde.has(alan) || govde.get(alan).isJsonNull()) return null;
        return govde.get(alan).getAsString();
    }
}
