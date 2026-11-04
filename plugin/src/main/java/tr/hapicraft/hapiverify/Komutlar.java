package tr.hapicraft.hapiverify;

import com.velocitypowered.api.command.CommandSource;
import com.velocitypowered.api.command.SimpleCommand;
import com.velocitypowered.api.proxy.Player;
import com.velocitypowered.api.proxy.ProxyServer;

import java.time.Instant;
import java.util.List;
import java.util.Optional;
import java.util.UUID;

public final class Komutlar {

    public static final String IZIN = "hapiverify.kullan";

    public static final String SONUC_IZNI = "hapiverify.sonuc";

    private Komutlar() { }

    private static Optional<Player> hedef(ProxyServer sunucu, String ad) {
        return sunucu.getPlayer(ad);
    }

    private static List<String> tamamla(ProxyServer sunucu, String[] args) {
        if (args.length > 1) return List.of();
        String onek = args.length == 0 ? "" : args[0].toLowerCase(java.util.Locale.ROOT);
        return sunucu.getAllPlayers().stream()
                .map(Player::getUsername)
                .filter(ad -> ad.toLowerCase(java.util.Locale.ROOT).startsWith(onek))
                .limit(30)
                .toList();
    }

    public static final class Dogrula implements SimpleCommand {
        private final HapiVerifyEklentisi eklenti;

        public Dogrula(HapiVerifyEklentisi eklenti) { this.eklenti = eklenti; }

        @Override
        public boolean hasPermission(Invocation cagri) {
            return cagri.source().hasPermission(IZIN);
        }

        @Override
        public List<String> suggest(Invocation cagri) {
            return tamamla(eklenti.sunucu(), cagri.arguments());
        }

        @Override
        public void execute(Invocation cagri) {
            CommandSource kaynak = cagri.source();
            if (!eklenti.hazirMi(kaynak)) return;

            String[] args = cagri.arguments();
            if (args.length != 1) {
                kaynak.sendMessage(Mesajlar.kullanim("pcverify", "<oyuncu>"));
                return;
            }
            Optional<Player> oyuncu = hedef(eklenti.sunucu(), args[0]);
            if (oyuncu.isEmpty()) {
                kaynak.sendMessage(Mesajlar.oyuncuYok(args[0]));
                return;
            }
            Player hedef = oyuncu.get();
            UUID isteyen = kaynak instanceof Player p ? p.getUniqueId() : null;

            eklenti.async(() -> {
                try {
                    var olusturma = eklenti.api().olustur(hedef.getUniqueId());
                    long dakika = Math.max(1, olusturma.ttlSaniye() / 60);

                    hedef.sendMessage(Mesajlar.oyuncuyaKod(
                            olusturma.token(), eklenti.ayarlar().indirmeAdresi(), dakika));

                    eklenti.oturumlar().ekle(new OturumDeposu.Bekleyen(
                            hedef.getUniqueId(), hedef.getUsername(), isteyen,
                            olusturma.verificationId(), Instant.now(), "PENDING"));

                    kaynak.sendMessage(Mesajlar.istekGonderildi(hedef.getUsername()));
                    kaynak.sendMessage(Mesajlar.kodGizli());
                } catch (ApiIstemcisi.ApiHatasi hata) {
                    kaynak.sendMessage(Mesajlar.apiHatasi(hata.kod()));
                    eklenti.hataLogla("dogrulama olusturulamadi", hata);
                }
            });
        }
    }

    public static final class Durum implements SimpleCommand {
        private final HapiVerifyEklentisi eklenti;

        public Durum(HapiVerifyEklentisi eklenti) { this.eklenti = eklenti; }

        @Override
        public boolean hasPermission(Invocation cagri) {
            return cagri.source().hasPermission(SONUC_IZNI);
        }

        @Override
        public List<String> suggest(Invocation cagri) {
            return tamamla(eklenti.sunucu(), cagri.arguments());
        }

        @Override
        public void execute(Invocation cagri) {
            CommandSource kaynak = cagri.source();
            if (!eklenti.hazirMi(kaynak)) return;

            String[] args = cagri.arguments();
            if (args.length != 1) {
                kaynak.sendMessage(Mesajlar.kullanim("pcstatus", "<oyuncu>"));
                return;
            }

            Optional<UUID> kimlik = eklenti.kimlikBul(args[0]);
            if (kimlik.isEmpty()) {
                kaynak.sendMessage(Mesajlar.oyuncuYok(args[0]));
                return;
            }
            String ad = args[0];

            eklenti.async(() -> {
                try {
                    var durum = eklenti.api().durum(kimlik.get());
                    if (durum == null) {
                        kaynak.sendMessage(Mesajlar.kayitYok(ad));
                        return;
                    }
                    kaynak.sendMessage(Mesajlar.durumSatiri(ad, durum.status(), durum.result()));
                } catch (ApiIstemcisi.ApiHatasi hata) {
                    kaynak.sendMessage(Mesajlar.apiHatasi(hata.kod()));
                }
            });
        }
    }

    public static final class Iptal implements SimpleCommand {
        private final HapiVerifyEklentisi eklenti;

        public Iptal(HapiVerifyEklentisi eklenti) { this.eklenti = eklenti; }

        @Override
        public boolean hasPermission(Invocation cagri) {
            return cagri.source().hasPermission(IZIN);
        }

        @Override
        public List<String> suggest(Invocation cagri) {
            return tamamla(eklenti.sunucu(), cagri.arguments());
        }

        @Override
        public void execute(Invocation cagri) {
            CommandSource kaynak = cagri.source();
            if (!eklenti.hazirMi(kaynak)) return;

            String[] args = cagri.arguments();
            if (args.length != 1) {
                kaynak.sendMessage(Mesajlar.kullanim("pccancel", "<oyuncu>"));
                return;
            }
            Optional<UUID> kimlik = eklenti.kimlikBul(args[0]);
            if (kimlik.isEmpty()) {
                kaynak.sendMessage(Mesajlar.oyuncuYok(args[0]));
                return;
            }
            String ad = args[0];

            eklenti.async(() -> {
                try {
                    boolean vardi = eklenti.api().iptal(kimlik.get());
                    eklenti.oturumlar().sil(kimlik.get());
                    kaynak.sendMessage(vardi ? Mesajlar.iptalEdildi(ad) : Mesajlar.kayitYok(ad));
                    eklenti.sunucu().getPlayer(kimlik.get())
                            .ifPresent(p -> p.sendMessage(Mesajlar.oyuncuyaIptal()));
                } catch (ApiIstemcisi.ApiHatasi hata) {
                    kaynak.sendMessage(Mesajlar.apiHatasi(hata.kod()));
                }
            });
        }
    }
}
