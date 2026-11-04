package tr.hapicraft.hapiverify;

import com.google.inject.Inject;
import com.velocitypowered.api.command.CommandManager;
import com.velocitypowered.api.command.CommandMeta;
import com.velocitypowered.api.command.CommandSource;
import com.velocitypowered.api.event.Subscribe;
import com.velocitypowered.api.event.connection.DisconnectEvent;
import com.velocitypowered.api.event.connection.PostLoginEvent;
import com.velocitypowered.api.event.proxy.ProxyInitializeEvent;
import com.velocitypowered.api.event.proxy.ProxyShutdownEvent;
import com.velocitypowered.api.plugin.Plugin;
import com.velocitypowered.api.plugin.annotation.DataDirectory;
import com.velocitypowered.api.proxy.Player;
import com.velocitypowered.api.proxy.ProxyServer;
import com.velocitypowered.api.scheduler.ScheduledTask;
import org.slf4j.Logger;

import java.io.IOException;
import java.nio.file.Path;
import java.time.Instant;
import java.util.ArrayList;
import java.util.List;
import java.util.Optional;
import java.util.UUID;
import java.util.concurrent.TimeUnit;

@Plugin(id = "hapiverify", name = "HapiVerify", version = "0.1.1",
        description = "HapiCraft PC dogrulama - proxy tarafi",
        authors = {"HapiCraft"})
public final class HapiVerifyEklentisi {

    private final ProxyServer sunucu;
    private final Logger log;
    private final Path veriDizini;

    private Ayarlar ayarlar;
    private ApiIstemcisi api;
    private final OturumDeposu oturumlar = new OturumDeposu();
    private OturumBildirici bildirici;
    private ScheduledTask izleyici;
    private ScheduledTask oturumGoruntusu;

    @Inject
    public HapiVerifyEklentisi(ProxyServer sunucu, Logger log, @DataDirectory Path veriDizini) {
        this.sunucu = sunucu;
        this.log = log;
        this.veriDizini = veriDizini;
    }

    public ProxyServer sunucu() { return sunucu; }
    public Ayarlar ayarlar() { return ayarlar; }
    public ApiIstemcisi api() { return api; }
    public OturumDeposu oturumlar() { return oturumlar; }

    @Subscribe
    public void baslarken(ProxyInitializeEvent olay) {
        try {
            ayarlar = Ayarlar.yukle(veriDizini);
        } catch (IOException hata) {
            log.error("HapiVerify ayarlari okunamadi, eklenti devre disi", hata);
            ayarlar = Ayarlar.coz("");
        }

        if (!ayarlar.kullanimaHazir()) {
            log.warn("HapiVerify yapilandirilmamis: config.properties icindeki "
                    + "plugin_token degistirilmeden komutlar calismayacak.");
        } else {
            api = new ApiIstemcisi(ayarlar);
            bildirici = new OturumBildirici(api, this::async,
                    mesaj -> log.debug("HapiVerify: {}", mesaj));
        }

        komutKaydet("pcverify", new Komutlar.Dogrula(this));
        komutKaydet("pcstatus", new Komutlar.Durum(this));
        komutKaydet("pccancel", new Komutlar.Iptal(this));

        izleyici = sunucu.getScheduler().buildTask(this, this::durumlariTara)
                .repeat(ayarlar.sorgulamaAraligi().toSeconds(), TimeUnit.SECONDS)
                .schedule();

        if (bildirici != null) {

            goruntuGonder();
            oturumGoruntusu = sunucu.getScheduler().buildTask(this, this::goruntuGonder)
                    .repeat(ayarlar.oturumGoruntuAraligi().toSeconds(), TimeUnit.SECONDS)
                    .schedule();
        }

        log.info("HapiVerify hazir (api={}, sorgulama={}sn)",
                ayarlar.apiUrl(), ayarlar.sorgulamaAraligi().toSeconds());
    }

    @Subscribe
    public void kapanirken(ProxyShutdownEvent olay) {
        if (izleyici != null) izleyici.cancel();
        if (oturumGoruntusu != null) oturumGoruntusu.cancel();
    }

    @Subscribe
    public void oyuncuGirdi(PostLoginEvent olay) {
        if (bildirici != null) bildirici.katildi(oyuncuGoruntusu(olay.getPlayer()));
    }

    @Subscribe
    public void oyuncuCikti(DisconnectEvent olay) {
        if (bildirici != null) bildirici.ayrildi(olay.getPlayer().getUniqueId());
    }

    private void goruntuGonder() {
        List<OturumBildirici.Oyuncu> liste = new ArrayList<>();
        for (Player oyuncu : sunucu.getAllPlayers()) liste.add(oyuncuGoruntusu(oyuncu));
        bildirici.goruntuGonder(liste);
    }

    private static OturumBildirici.Oyuncu oyuncuGoruntusu(Player oyuncu) {
        String ip = oyuncu.getRemoteAddress() != null && oyuncu.getRemoteAddress().getAddress() != null
                ? oyuncu.getRemoteAddress().getAddress().getHostAddress() : null;
        return new OturumBildirici.Oyuncu(oyuncu.getUniqueId(), oyuncu.getUsername(), ip);
    }

    private void komutKaydet(String ad, com.velocitypowered.api.command.SimpleCommand komut) {
        CommandManager yonetici = sunucu.getCommandManager();
        CommandMeta meta = yonetici.metaBuilder(ad).plugin(this).build();
        yonetici.register(meta, komut);
    }

    public boolean hazirMi(CommandSource kaynak) {
        if (api == null) {
            kaynak.sendMessage(Mesajlar.hazirDegil());
            return false;
        }
        return true;
    }

    public void async(Runnable is) {
        sunucu.getScheduler().buildTask(this, is).schedule();
    }

    public void hataLogla(String ne, Throwable hata) {
        log.warn("HapiVerify: {} ({})", ne, hata.getMessage());
    }

    public Optional<UUID> kimlikBul(String ad) {
        Optional<Player> cevrimici = sunucu.getPlayer(ad);
        if (cevrimici.isPresent()) return cevrimici.map(Player::getUniqueId);
        return oturumlar.hepsi().stream()
                .filter(b -> b.oyuncuAdi().equalsIgnoreCase(ad))
                .map(OturumDeposu.Bekleyen::oyuncu)
                .findFirst();
    }

    void durumlariTara() {
        if (api == null) return;

        for (OturumDeposu.Bekleyen dusen : oturumlar.suresiGecenleriTemizle(Instant.now())) {
            bildir(dusen, "EXPIRED");
        }

        for (OturumDeposu.Bekleyen bekleyen : oturumlar.hepsi()) {
            try {
                ApiIstemcisi.Durum durum = api.durum(bekleyen.oyuncu());
                if (durum == null) {
                    oturumlar.sil(bekleyen.oyuncu());
                    continue;
                }
                if (durum.status() == null || durum.status().equals(bekleyen.sonDurum())) continue;

                oturumlar.guncelle(bekleyen.durumla(durum.status()));

                if (OturumDeposu.bitmisDurum(durum.status())) {
                    oturumlar.sil(bekleyen.oyuncu());
                    String sonuc = durum.result() != null ? durum.result() : durum.status();
                    bildir(bekleyen, sonuc);
                }
            } catch (ApiIstemcisi.ApiHatasi hata) {

                log.debug("HapiVerify durum sorgusu basarisiz: {}", hata.kod());
            }
        }
    }

    private void bildir(OturumDeposu.Bekleyen bekleyen, String sonuc) {
        if (bekleyen.isteyen() == null) {
            log.info("HapiVerify: {} icin dogrulama sonucu {}", bekleyen.oyuncuAdi(), sonuc);
            return;
        }
        sunucu.getPlayer(bekleyen.isteyen()).ifPresent(yetkili -> {

            if (!yetkili.hasPermission(Komutlar.SONUC_IZNI)) {
                log.info("HapiVerify: {} sonucu gonderilmedi, {} artik yetkili degil",
                        bekleyen.oyuncuAdi(), yetkili.getUsername());
                return;
            }
            yetkili.sendMessage(Mesajlar.sonucBildirimi(bekleyen.oyuncuAdi(), sonuc));
        });
    }
}
