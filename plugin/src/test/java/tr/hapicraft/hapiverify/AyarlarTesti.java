package tr.hapicraft.hapiverify;

import org.junit.jupiter.api.Test;

import java.time.Duration;

import static org.junit.jupiter.api.Assertions.*;

class AyarlarTesti {

    @Test
    void sablonVarsayilanJetonlaGelir() {
        Ayarlar a = Ayarlar.coz(Ayarlar.SABLON);
        assertEquals(Ayarlar.VARSAYILAN_JETON, a.pluginJetonu());
        assertFalse(a.kullanimaHazir(), "varsayilan jetonla eklenti acilmamali");
    }

    @Test
    void jetonDegisinceHazir() {
        Ayarlar a = Ayarlar.coz("api_url=http://127.0.0.1:8095\nplugin_token=gercek-jeton");
        assertTrue(a.kullanimaHazir());
    }

    @Test
    void bosJetonHazirDegil() {
        assertFalse(Ayarlar.coz("plugin_token=").kullanimaHazir());
    }

    @Test
    void httpOlmayanAdresReddedilir() {
        assertFalse(Ayarlar.coz("api_url=ftp://x\nplugin_token=gercek").kullanimaHazir());
    }

    @Test
    void yorumVeBosSatirAtlanir() {
        Ayarlar a = Ayarlar.coz("# yorum\n\n  plugin_token = jeton  \n");
        assertEquals("jeton", a.pluginJetonu());
    }

    @Test
    void sayilarSinirlanir() {
        Ayarlar a = Ayarlar.coz("timeout_seconds=9999\npoll_seconds=0");
        assertEquals(Duration.ofSeconds(60), a.zamanAsimi());
        assertEquals(Duration.ofSeconds(3), a.sorgulamaAraligi());
    }

    @Test
    void bozukSayiVarsayilanaDuser() {
        assertEquals(Duration.ofSeconds(8), Ayarlar.coz("timeout_seconds=abc").zamanAsimi());
    }
}
