package tr.hapicraft.hapiverify;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.time.Duration;
import java.util.HashMap;
import java.util.Locale;
import java.util.Map;

public final class Ayarlar {

    public static final String VARSAYILAN_JETON = "DEGISTIR";

    private final String apiUrl;
    private final String pluginJetonu;
    private final Duration zamanAsimi;
    private final Duration sorgulamaAraligi;
    private final Duration oturumGoruntuAraligi;
    private final String indirmeAdresi;

    public Ayarlar(String apiUrl, String pluginJetonu, Duration zamanAsimi,
                   Duration sorgulamaAraligi, Duration oturumGoruntuAraligi,
                   String indirmeAdresi) {
        this.apiUrl = apiUrl;
        this.pluginJetonu = pluginJetonu;
        this.zamanAsimi = zamanAsimi;
        this.sorgulamaAraligi = sorgulamaAraligi;
        this.oturumGoruntuAraligi = oturumGoruntuAraligi;
        this.indirmeAdresi = indirmeAdresi;
    }

    public String apiUrl() { return apiUrl; }
    public String pluginJetonu() { return pluginJetonu; }
    public Duration zamanAsimi() { return zamanAsimi; }
    public Duration sorgulamaAraligi() { return sorgulamaAraligi; }

    public Duration oturumGoruntuAraligi() { return oturumGoruntuAraligi; }
    public String indirmeAdresi() { return indirmeAdresi; }

    public boolean kullanimaHazir() {
        return !pluginJetonu.isBlank()
                && !pluginJetonu.equals(VARSAYILAN_JETON)
                && apiUrl.startsWith("http");
    }

    public static Ayarlar coz(String icerik) {
        Map<String, String> m = new HashMap<>();
        for (String satir : icerik.split("\n")) {
            String temiz = satir.trim();
            if (temiz.isEmpty() || temiz.startsWith("#")) continue;
            int esit = temiz.indexOf('=');
            if (esit <= 0) continue;
            m.put(temiz.substring(0, esit).trim().toLowerCase(Locale.ROOT),
                  temiz.substring(esit + 1).trim());
        }
        return new Ayarlar(
                m.getOrDefault("api_url", "http://127.0.0.1:8095"),
                m.getOrDefault("plugin_token", VARSAYILAN_JETON),
                Duration.ofSeconds(sayi(m.get("timeout_seconds"), 8, 1, 60)),
                Duration.ofSeconds(sayi(m.get("poll_seconds"), 10, 3, 120)),
                Duration.ofSeconds(sayi(m.get("session_snapshot_seconds"), 30, 5, 60)),
                m.getOrDefault("download_url", "https://hapicraft.com.tr/dogrula"));
    }

    private static long sayi(String deger, long varsayilan, long alt, long ust) {
        if (deger == null) return varsayilan;
        try {
            long d = Long.parseLong(deger.trim());
            return Math.max(alt, Math.min(ust, d));
        } catch (NumberFormatException yok) {
            return varsayilan;
        }
    }

    public static final String SABLON = String.join("\n",
            "# HapiVerify - Velocity eklentisi ayarlari",
            "#",
            "# plugin_token backend'deki HAPIVERIFY_PLUGIN_TOKEN ile ayni olmali.",
            "# Degistirilmeden birakilirsa eklenti komutlari ACILMAZ.",
            "api_url=http://127.0.0.1:8095",
            "plugin_token=" + VARSAYILAN_JETON,
            "",
            "# Backend istekleri icin zaman asimi (saniye)",
            "timeout_seconds=8",
            "",
            "# Bekleyen dogrulamalarin durumu kac saniyede bir sorulsun",
            "poll_seconds=10",
            "",
            "# Tam oyuncu listesi kac saniyede bir backend'e gonderilsin",
            "session_snapshot_seconds=30",
            "",
            "# Oyuncuya gosterilecek indirme adresi",
            "download_url=https://hapicraft.com.tr/dogrula",
            "");

    public static Ayarlar yukle(Path dizin) throws IOException {
        Files.createDirectories(dizin);
        Path dosya = dizin.resolve("config.properties");
        if (!Files.exists(dosya)) {
            Files.writeString(dosya, SABLON, StandardCharsets.UTF_8);
        }
        return coz(Files.readString(dosya, StandardCharsets.UTF_8));
    }
}
