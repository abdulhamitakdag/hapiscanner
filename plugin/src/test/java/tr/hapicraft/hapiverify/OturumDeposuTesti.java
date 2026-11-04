package tr.hapicraft.hapiverify;

import org.junit.jupiter.api.Test;

import java.time.Duration;
import java.time.Instant;
import java.util.UUID;

import static org.junit.jupiter.api.Assertions.*;

class OturumDeposuTesti {

    private static OturumDeposu.Bekleyen bekleyen(Instant baslangic) {
        return new OturumDeposu.Bekleyen(UUID.randomUUID(), "Ali", UUID.randomUUID(),
                "v-1", baslangic, "PENDING");
    }

    @Test
    void eklenenKayitBulunur() {
        OturumDeposu depo = new OturumDeposu();
        var b = bekleyen(Instant.now());
        depo.ekle(b);
        assertTrue(depo.bul(b.oyuncu()).isPresent());
        assertEquals(1, depo.sayi());
    }

    @Test
    void suresiGecenDusurulur() {
        OturumDeposu depo = new OturumDeposu();
        Instant simdi = Instant.now();
        var eski = bekleyen(simdi.minus(OturumDeposu.AZAMI_OMUR).minus(Duration.ofMinutes(1)));
        var yeni = bekleyen(simdi);
        depo.ekle(eski);
        depo.ekle(yeni);

        var dusenler = depo.suresiGecenleriTemizle(simdi);

        assertEquals(1, dusenler.size());
        assertEquals(eski.oyuncu(), dusenler.get(0).oyuncu());
        assertEquals(1, depo.sayi());
    }

    @Test
    void durumGuncellemesiKaydiKorur() {
        OturumDeposu depo = new OturumDeposu();
        var b = bekleyen(Instant.now());
        depo.ekle(b);
        depo.guncelle(b.durumla("IN_PROGRESS"));
        assertEquals("IN_PROGRESS", depo.bul(b.oyuncu()).orElseThrow().sonDurum());
        assertEquals(1, depo.sayi());
    }

    @Test
    void bitmisDurumlarTaninir() {
        assertTrue(OturumDeposu.bitmisDurum("COMPLETED"));
        assertTrue(OturumDeposu.bitmisDurum("CANCELLED"));
        assertTrue(OturumDeposu.bitmisDurum("EXPIRED"));
        assertFalse(OturumDeposu.bitmisDurum("PENDING"));
        assertFalse(OturumDeposu.bitmisDurum("IN_PROGRESS"));
        assertFalse(OturumDeposu.bitmisDurum(null));
    }
}
