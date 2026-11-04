package tr.hapicraft.hapiverify;

import java.util.ArrayList;
import java.util.Collection;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.ConcurrentHashMap;
import java.util.function.Consumer;

public final class OturumBildirici {

    public record Oyuncu(UUID kimlik, String ad, String ip) { }

    private final OturumYuzeyi api;
    private final Consumer<Runnable> async;
    private final Consumer<String> hataLog;

    private final Map<UUID, String> oturumKimlikleri = new ConcurrentHashMap<>();

    public OturumBildirici(OturumYuzeyi api, Consumer<Runnable> async, Consumer<String> hataLog) {
        this.api = api;
        this.async = async;
        this.hataLog = hataLog;
    }

    public String oturumKimligi(UUID oyuncu) {
        return oturumKimlikleri.computeIfAbsent(oyuncu, k -> UUID.randomUUID().toString());
    }

    public void katildi(Oyuncu oyuncu) {

        String kimlik = UUID.randomUUID().toString();
        oturumKimlikleri.put(oyuncu.kimlik(), kimlik);
        gonder("giris bildirimi", () -> api.oturumKatildi(
                new OturumYuzeyi.Oturum(oyuncu.kimlik(), oyuncu.ad(), kimlik, oyuncu.ip())));
    }

    public void ayrildi(UUID oyuncu) {
        String kimlik = oturumKimlikleri.remove(oyuncu);
        if (kimlik == null) return;
        gonder("cikis bildirimi", () -> api.oturumAyrildi(oyuncu, kimlik));
    }

    public void goruntuGonder(Collection<Oyuncu> cevrimici) {
        List<OturumYuzeyi.Oturum> liste = new ArrayList<>(cevrimici.size());
        for (Oyuncu o : cevrimici) {
            liste.add(new OturumYuzeyi.Oturum(o.kimlik(), o.ad(), oturumKimligi(o.kimlik()), o.ip()));
        }

        oturumKimlikleri.keySet().removeIf(
                k -> cevrimici.stream().noneMatch(o -> o.kimlik().equals(k)));
        gonder("oyuncu listesi", () -> api.oturumGoruntusu(liste));
    }

    private void gonder(String ne, Runnable is) {

        async.accept(() -> {
            try {
                is.run();
            } catch (ApiIstemcisi.ApiHatasi hata) {

                hataLog.accept(ne + " basarisiz: " + hata.kod());
            }
        });
    }
}
