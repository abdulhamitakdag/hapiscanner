package tr.hapicraft.hapiverify;

import net.kyori.adventure.text.Component;
import net.kyori.adventure.text.minimessage.MiniMessage;

public final class Mesajlar {

    private static final MiniMessage MM = MiniMessage.miniMessage();

    public static final String ONEK =
            "<green><bold>Hapi</bold></green><white><bold>Craft</bold></white> <gray>» <gray>";

    private Mesajlar() { }

    public static Component mesaj(String govde) {
        return MM.deserialize(ONEK + govde);
    }

    public static Component ham(String govde) {
        return MM.deserialize(govde);
    }

    public static Component kullanim(String komut, String arguman) {
        return mesaj("<gray>Kullanım: <white>/" + komut + " " + arguman + "</white>");
    }

    public static Component oyuncuYok(String ad) {
        return mesaj("<red>" + temiz(ad) + " <gray>şu anda çevrim içi değil.");
    }

    public static Component istekGonderildi(String ad) {
        return mesaj("<gray>Doğrulama isteği oluşturuldu, kod <white>" + temiz(ad)
                + "</white> <gray>oyuncusuna özel olarak gönderildi.");
    }

    public static Component kodGizli() {
        return mesaj("<dark_gray>Kod sana gösterilmez; taramanın doğru bilgisayarda "
                + "çalışması için yalnızca oyuncuya iletilir.");
    }

    public static Component durumSatiri(String ad, String durum, String sonuc) {
        String sonucMetni = (sonuc == null || sonuc.isBlank()) ? "-" : temiz(sonuc);
        return mesaj("<white>" + temiz(ad) + "</white> <gray>durum: <white>" + temiz(durum)
                + "</white> <gray>sonuç: <white>" + sonucMetni + "</white>");
    }

    public static Component iptalEdildi(String ad) {
        return mesaj("<gray>" + temiz(ad) + " <gray>için doğrulama iptal edildi.");
    }

    public static Component kayitYok(String ad) {
        return mesaj("<gray>" + temiz(ad) + " <gray>için açık bir doğrulama yok.");
    }

    public static Component apiHatasi(String kod) {
        String metin = switch (kod == null ? "" : kod) {
            case "PLAYER_NOT_CONNECTED" ->
                    "Oyuncu şu anda sunucuda görünmüyor, doğrulama açılamadı.";
            case "ALREADY_PENDING" ->
                    "Bu oyuncunun zaten bekleyen bir doğrulaması var.";
            case "RATE_LIMITED" ->
                    "Çok sık doğrulama istendi, biraz bekle.";
            case "UNAUTHORIZED" ->
                    "Eklentinin jetonu backend tarafından kabul edilmedi.";
            case "NETWORK", "HTTP_502", "HTTP_503", "HTTP_504" ->
                    "Doğrulama sunucusuna ulaşılamadı.";
            default -> "Doğrulama sunucusu isteği reddetti <dark_gray>(" + temiz(kod) + ")";
        };
        return mesaj("<red>" + metin);
    }

    public static Component hazirDegil() {
        return mesaj("<red>HapiVerify yapılandırılmamış. <gray>config.properties "
                + "içindeki plugin_token değiştirilmeli.");
    }

    public static Component sonucBildirimi(String ad, String sonuc) {
        return mesaj("<white>" + temiz(ad) + "</white> <gray>için tarama bitti, sonuç: <white>"
                + temiz(sonuc) + "</white>");
    }

    public static Component oyuncuyaKod(String kod, String indirmeAdresi, long dakika) {
        String temizKod = temiz(kod);
        return mesaj("<gray>Bir yetkili PC doğrulaması istedi.\n"
                + "<gray>Kodun: <hover:show_text:'<gray>Tıkla, kod panoya kopyalanır'>"
                + "<click:copy_to_clipboard:'" + temizKod + "'>"
                + "<green><bold>" + temizKod + "</bold></green></click></hover> "
                + "<dark_gray>(tıkla, kopyala)\n"
                + "<gray>İndir: <white><click:open_url:'" + temiz(indirmeAdresi) + "'>"
                + temiz(indirmeAdresi) + "</click></white>\n"
                + "<dark_gray>Kod " + dakika + " dakika geçerli. Tarama senin onayınla başlar.");
    }

    public static Component oyuncuyaIptal() {
        return mesaj("<gray>PC doğrulama isteği iptal edildi.");
    }

    static String temiz(String metin) {
        if (metin == null) return "";
        return metin.replace("<", "\\<").replace("'", "");
    }
}
