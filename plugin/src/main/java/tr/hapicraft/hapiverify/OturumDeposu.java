package tr.hapicraft.hapiverify;

import java.time.Duration;
import java.time.Instant;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.UUID;
import java.util.concurrent.ConcurrentHashMap;

public final class OturumDeposu {

    public record Bekleyen(UUID oyuncu, String oyuncuAdi, UUID isteyen,
                           String verificationId, Instant baslangic, String sonDurum) {

        public Bekleyen durumla(String yeni) {
            return new Bekleyen(oyuncu, oyuncuAdi, isteyen, verificationId, baslangic, yeni);
        }
    }

    public static final Duration AZAMI_OMUR = Duration.ofMinutes(30);

    private final Map<UUID, Bekleyen> kayitlar = new ConcurrentHashMap<>();

    public void ekle(Bekleyen bekleyen) {
        kayitlar.put(bekleyen.oyuncu(), bekleyen);
    }

    public Optional<Bekleyen> bul(UUID oyuncu) {
        return Optional.ofNullable(kayitlar.get(oyuncu));
    }

    public void sil(UUID oyuncu) {
        kayitlar.remove(oyuncu);
    }

    public void guncelle(Bekleyen bekleyen) {
        kayitlar.put(bekleyen.oyuncu(), bekleyen);
    }

    public List<Bekleyen> hepsi() {
        return new ArrayList<>(kayitlar.values());
    }

    public int sayi() {
        return kayitlar.size();
    }

    public List<Bekleyen> suresiGecenleriTemizle(Instant simdi) {
        List<Bekleyen> dusenler = new ArrayList<>();
        for (Bekleyen b : hepsi()) {
            if (Duration.between(b.baslangic(), simdi).compareTo(AZAMI_OMUR) > 0) {
                kayitlar.remove(b.oyuncu());
                dusenler.add(b);
            }
        }
        return dusenler;
    }

    public static boolean bitmisDurum(String durum) {
        if (durum == null) return false;
        return switch (durum.toUpperCase(java.util.Locale.ROOT)) {
            case "COMPLETED", "CANCELLED", "EXPIRED", "FAILED" -> true;
            default -> false;
        };
    }
}
