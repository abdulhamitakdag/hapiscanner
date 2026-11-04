package tr.hapicraft.hapiverify;

import java.util.Collection;
import java.util.UUID;

public interface OturumYuzeyi {

    record Oturum(UUID oyuncu, String oyuncuAdi, String oturumKimligi, String ip) { }

    void oturumKatildi(Oturum oturum);

    void oturumAyrildi(UUID oyuncu, String oturumKimligi);

    void oturumGoruntusu(Collection<Oturum> oturumlar);
}
