# HapiVerify / HapiScanner — Gizlilik Modeli (taslak, v0)

Bu belge pazarlama metni değildir. Buradaki her cümle uygulamanın gerçek davranışıyla birebir tutarlı olmak zorundadır. README, uygulama içi gizlilik ekranı ve bu belge arasında çelişki bulunması bir **hata**dır ve release engeli sayılır.

HapiScanner'ın açık kaynak olmasının ana sebebi budur: burada yazdıklarımızı oyuncu kaynak koddan doğrulayabilir.

---

## 1. HapiScanner neleri YAPMAZ

HapiScanner:

- ekran görüntüsü almaz
- ekran kaydı yapmaz
- mikrofonu kullanmaz
- web kamerasını kullanmaz
- tuş vuruşlarını kaydetmez (keylogger yoktur)
- panoyu (clipboard) okumaz
- tarayıcı geçmişini taramaz
- tarayıcı çerezlerini toplamaz
- kayıtlı şifreleri okumaz
- kimlik bilgisi (credential) dump'ı almaz
- Discord token'ı aramaz
- oturum/kimlik doğrulama token'ı toplamaz
- kişisel fotoğraf yüklemez
- belgelerinizi sunucuya yüklemez
- uzak masaüstü / uzaktan erişim sağlamaz
- gizli kalıcılık (persistence) kurmaz
- başlangıçta otomatik çalışacak şekilde kendini kaydetmez
- gizli servis kurmaz
- arka planda siz kapattıktan sonra çalışmaya devam etmez

Bu listedeki her madde için kaynak kodda karşılık gelen bir yetenek **bulunmamalıdır**. Bu maddelerden birini ihlal eden bir pull request reddedilir.

---

## 2. HapiScanner neye bakar

Scanner'ın ilgilendiği tek şey Minecraft ile ilgili hile izleridir:

- Minecraft dizinleri: `%APPDATA%\.minecraft` ve altındaki `mods`, `versions`, `libraries`, `logs`
- Bilinen launcher dizinleri
- İndirilenler, Masaüstü, Temp gibi hile dosyalarının tipik olarak bulunduğu konumlar (aday filtresi ile, körlemesine değil)
- Dosya sistemi metadata'sı ve geçmişi (MFT, USN Journal, Geri Dönüşüm Kutusu kayıtları)
- HapiCraft'a bağlı olan aktif Minecraft process'i
- O process'e yüklenmiş native modüller ve JVM başlatma parametreleri
- Minecraft ile ilişkili şüpheli yardımcı process'ler

Scanner **tüm diski taramaz ve tüm dosyaların hash'ini almaz.** Aday odaklı bir hat kullanılır:

```
metadata filtresi -> aday tespiti -> yapısal inceleme -> hash -> kural değerlendirmesi
```

Video, ISO, yedek ve arşiv gibi büyük ve alakasız dosyalar hash'lenmez.

---

## 3. Sunucuya ne gönderilir

Analizin tamamı **oyuncunun bilgisayarında, lokal olarak** yapılır. Sunucuya gönderilen şey dosyalar değil, kural sonuçlarıdır.

Gönderilen:

| Alan | Örnek |
|---|---|
| `verification_id` | oturum kimliği |
| `rule_id` | `CHEAT_0127` |
| `rule_version` / `ruleset_version` | `2026.11.03.1` |
| `evidence_type` | `KNOWN_HASH`, `JAR_CLASS`, `USN_DELETE` |
| `artifact_category` | `KNOWN_CLIENT`, `AUTOCLICKER` |
| `first_seen` / `last_seen` | zaman damgası |
| `currently_present` | true/false |
| `confidence` / `severity` | `HIGH` / `MEDIUM` |
| `forensic_coverage` / `runtime_coverage` | `GOOD` / `LIMITED` / `UNAVAILABLE` |
| `scanner_version` | `0.1.0` |
| `report_hash` + imza | bütünlük doğrulaması |
| cihaz public key + fingerprint + assurance level | `TPM` / `SOFTWARE` |

Gönderilmeyen:

- Dosya içerikleri
- Tam dosya yolları (tespit için gerçekten gerekli olmadığı sürece)
- Kullanıcı adınızı içeren yollar, ör. `C:\Users\Ahmet\Documents\...`
- Alakasız dosyaların isimleri
- Geri Dönüşüm Kutusu'nun içeriği veya dosya listesi
- Minecraft veya launcher loglarının tamamı
- Sohbet içeriği
- Tüm process listesi
- Donanım seri numaraları (MAC, anakart, disk seri no toplanmaz)
- Private key — **cihazınızdan asla çıkmaz**

Tasarım kuralı: *bir dosyanın tam yolu tespit açısından gerçekten gerekli değilse sunucuya gönderilmez.* Bir özelliğin çalışması için kullanıcının disk içeriğinin backend'e aktarılması gerekiyorsa, o özellik yeniden tasarlanır.

---

## 4. Staff ne görür

Staff'ın bilgisayarınızdaki dosyaları görmesi bir tasarım hedefi **değildir**. Staff yalnızca doğrulama açısından anlamlı kanıtı görür:

```
Bilinen hile kanıtı bulundu
Geçmişte silinmiş hile izi bulundu
Şüpheli runtime göstergesi bulundu
```

Kanıt detayı da aynı ilkeyi izler: kural adı, kanıt tipi, zaman aralığı, güven seviyesi — kişisel veri değil.

---

## 5. Veri saklama

| Veri | Süre |
|---|---|
| IP adresleri (`game_ip`, `scanner_ip`) | **7 gün**, sonra otomatik olarak boşaltılır. Karşılaştırma sonucu (`NETWORK_MATCH/MISMATCH/UNKNOWN`) kalır. |
| Verification oturumları | retention politikasına göre, öneri 1 yıl |
| Tarama raporları ve kanıt kayıtları | retention politikasına göre, öneri 1 yıl |
| Denetim kaydı (audit log) | öneri 1 yıl, gereksiz kişisel veri içermez |

Verification token'ı backend'de **düz metin olarak saklanmaz**, yalnızca hash'i tutulur.

---

## 6. Loglama

Scanner loglarında kesinlikle tutulmaz:

- private key
- verification token'ın tamamı
- şifreler, kimlik bilgileri
- dosya içerikleri
- gereksiz tam dosya yolları

Backend loglarında da gereksiz kişisel veri tutulmaz. Log seviyeleri: `INFO`, `WARN`, `ERROR`, `SECURITY`. Güvenlik olayları yapılandırılmış biçimde loglanır.

---

## 7. Yönetici (Administrator) yetkisi

Scanner bazı işlemler için Administrator yetkisi ister:

- MFT (dosya sistemi tablosu) okuma
- USN Journal (dosya değişim geçmişi) okuma
- Bazı runtime analizleri

Bu yetki **neden** gerekiyor: silinmiş hile dosyalarının izleri normal kullanıcı yetkisiyle görülemez; bu veriler yalnızca birim (volume) seviyesinde erişimle okunabilir.

Yetki vermezseniz:

- Scanner **çökmez**, çalışmaya devam eder
- İlgili katmanlar devre dışı kalır
- Rapor `forensic_coverage: LIMITED` olarak işaretlenir ve staff bunu görür

Elevation yalnızca gerçekten gerekli olduğunda istenir.

---

## 8. Rıza

Tarama oyuncunun açık rızasıyla başlar. Tarama başlamadan önce uygulama şunları anlaşılır biçimde gösterir:

- hangi veri kaynaklarının tarandığı
- hangi bilgilerin yalnızca lokal işlendiği
- hangi bilgilerin sunucuya gönderildiği
- hangi bilgilerin kesinlikle toplanmadığı

Oyuncu bu ekranda taramayı iptal edebilir. İptal etmek bir hile itirafı değildir; yalnızca doğrulamanın tamamlanmadığı anlamına gelir ve staff bunu bu şekilde görür.

---

## 9. Snapshot sınırı — "temiz" demiyoruz

Bir PC taraması yalnızca bir **anlık görüntüdür**. 14:00'te tamamlanan bir tarama, 14:05'te açılan bir hile hakkında hiçbir şey söylemez.

Bu yüzden ne oyuncuya ne staff paneline "kesinlikle temizsin" denmez. Kullanılan ifade teknik ve dürüsttür:

> **BİLİNEN HİLE KANITI BULUNAMADI** (2026-11-03 14:00 itibarıyla)

"Verification passed forever" diye bir durum yoktur. Server-side anti-cheat sürekli çalışmaya devam eder; HapiVerify onun yerine geçmez.
