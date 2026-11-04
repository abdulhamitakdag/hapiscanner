package tr.hapicraft.hapiverify;

import org.junit.jupiter.api.Test;

import java.util.ArrayList;
import java.util.List;
import java.util.UUID;

import static org.junit.jupiter.api.Assertions.*;

class OturumBildiriciTesti {

    private static final class SahteApi implements OturumYuzeyi {
        final List<OturumYuzeyi.Oturum> girisler = new ArrayList<>();
        final List<String> cikislar = new ArrayList<>();
        final List<List<OturumYuzeyi.Oturum>> goruntuler = new ArrayList<>();
        boolean patla = false;

        @Override public void oturumKatildi(Oturum oturum) {
            if (patla) throw new ApiIstemcisi.ApiHatasi("NETWORK", "kapali");
            girisler.add(oturum);
        }

        @Override public void oturumAyrildi(UUID oyuncu, String oturumKimligi) {
            if (patla) throw new ApiIstemcisi.ApiHatasi("NETWORK", "kapali");
            cikislar.add(oyuncu + "/" + oturumKimligi);
        }

        @Override public void oturumGoruntusu(java.util.Collection<Oturum> oturumlar) {
            if (patla) throw new ApiIstemcisi.ApiHatasi("NETWORK", "kapali");
            goruntuler.add(new ArrayList<>(oturumlar));
        }
    }

    private final SahteApi api = new SahteApi();
    private final List<String> hatalar = new ArrayList<>();

    private final OturumBildirici bildirici =
            new OturumBildirici(api, Runnable::run, hatalar::add);

    private static OturumBildirici.Oyuncu oyuncu(String ad) {
        return new OturumBildirici.Oyuncu(UUID.randomUUID(), ad, "203.0.113.10");
    }

    @Test
    void girisBildirilir() {
        var o = oyuncu("TestOyuncu");
        bildirici.katildi(o);

        assertEquals(1, api.girisler.size());
        assertEquals(o.kimlik(), api.girisler.get(0).oyuncu());
        assertEquals("TestOyuncu", api.girisler.get(0).oyuncuAdi());
        assertEquals("203.0.113.10", api.girisler.get(0).ip());
        assertFalse(api.girisler.get(0).oturumKimligi().isBlank());
    }

    @Test
    void yenidenGirisYeniOturumKimligiAlir() {
        var o = oyuncu("TestOyuncu");
        bildirici.katildi(o);
        bildirici.katildi(o);

        assertNotEquals(api.girisler.get(0).oturumKimligi(),
                        api.girisler.get(1).oturumKimligi());
    }

    @Test
    void cikisOturumKimligiyleBildirilir() {
        var o = oyuncu("TestOyuncu");
        bildirici.katildi(o);
        String kimlik = api.girisler.get(0).oturumKimligi();

        bildirici.ayrildi(o.kimlik());
        assertEquals(List.of(o.kimlik() + "/" + kimlik), api.cikislar);
    }

    @Test
    void bilinmeyenOyuncuIcinCikisGonderilmez() {
        bildirici.ayrildi(UUID.randomUUID());
        assertTrue(api.cikislar.isEmpty());
    }

    @Test
    void goruntuBilinmeyenOyuncuyaKimlikUretir() {
        var o = oyuncu("SonradanGorulen");
        bildirici.goruntuGonder(List.of(o));

        assertEquals(1, api.goruntuler.get(0).size());
        assertFalse(api.goruntuler.get(0).get(0).oturumKimligi().isBlank());
    }

    @Test
    void goruntuMevcutKimligiKorur() {
        var o = oyuncu("TestOyuncu");
        bildirici.katildi(o);
        String kimlik = api.girisler.get(0).oturumKimligi();

        bildirici.goruntuGonder(List.of(o));
        assertEquals(kimlik, api.goruntuler.get(0).get(0).oturumKimligi());
    }

    @Test
    void cevrimdisiOlanTablodanDuser() {
        var o = oyuncu("TestOyuncu");
        bildirici.katildi(o);
        String kimlik = bildirici.oturumKimligi(o.kimlik());

        bildirici.goruntuGonder(List.of());
        assertNotEquals(kimlik, bildirici.oturumKimligi(o.kimlik()));
    }

    @Test
    void agHatasiBildiriciyiDusurmez() {
        api.patla = true;
        bildirici.katildi(oyuncu("TestOyuncu"));
        assertEquals(1, hatalar.size());

        api.patla = false;
        bildirici.katildi(oyuncu("TestOyuncu"));
        assertEquals(1, api.girisler.size());
    }

    @Test
    void goruntuAraligiBackendTazelikPenceresininAltinda() {

        assertTrue(Ayarlar.coz("").oturumGoruntuAraligi().toSeconds() * 3 <= 90);
    }
}
