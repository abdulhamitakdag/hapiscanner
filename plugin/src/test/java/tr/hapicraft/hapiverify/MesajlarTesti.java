package tr.hapicraft.hapiverify;

import net.kyori.adventure.text.Component;
import net.kyori.adventure.text.serializer.plain.PlainTextComponentSerializer;
import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.*;

class MesajlarTesti {

    private static String duz(Component c) {
        return PlainTextComponentSerializer.plainText().serialize(c);
    }

    @Test
    void onekMarkaRenkleriniKullanir() {
        assertTrue(Mesajlar.ONEK.contains("<green><bold>Hapi</bold></green>"));
        assertTrue(Mesajlar.ONEK.contains("<white><bold>Craft</bold></white>"));
    }

    @Test
    void oyuncuAdiEtiketEnjekteEdemez() {
        String kotu = "<red><click:run_command:'/op kotu'>Ahmet";
        String cikti = duz(Mesajlar.oyuncuYok(kotu));
        assertTrue(cikti.contains("Ahmet"));
        assertTrue(cikti.contains("<red>"), "etiket metin olarak gorunmeli");
    }

    @Test
    void kodMesajiKoduVeAdresiIcerir() {
        String cikti = duz(Mesajlar.oyuncuyaKod("HAPI-K7QD-3MWA", "https://ornek/indir", 10));
        assertTrue(cikti.contains("HAPI-K7QD-3MWA"));
        assertTrue(cikti.contains("https://ornek/indir"));
        assertTrue(cikti.contains("10 dakika"));
    }

    @Test
    void bosSonucTireGosterir() {
        assertTrue(duz(Mesajlar.durumSatiri("Ali", "PENDING", null)).contains("sonuç: -"));
    }

    @Test
    void bilinenApiHatasiOkunurMetneCevrilir() {
        assertTrue(duz(Mesajlar.apiHatasi("PLAYER_NOT_CONNECTED")).contains("sunucuda görünmüyor"));
    }

    @Test
    void bekleyenDogrulamaHatasiAyriMetin() {
        assertTrue(duz(Mesajlar.apiHatasi("ALREADY_PENDING")).contains("bekleyen bir doğrulaması"));
    }

    @Test
    void bilinmeyenApiHatasiKoduGosterir() {
        assertTrue(duz(Mesajlar.apiHatasi("TUHAF_KOD")).contains("TUHAF_KOD"));
    }
}
