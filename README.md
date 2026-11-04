# HapiScanner / HapiVerify

HapiScanner, HapiCraft Minecraft sunucusu için geliştirilen bir **PC doğrulama ve hile kanıtı tarayıcısıdır**. Oyuncunun açık rızasıyla, tek bir Windows uygulaması olarak çalışır.

## İndir

Oyuncular için tek gereken dosya `HapiScanner.exe`:

**[→ Son sürümü indir](https://github.com/abdulhamitakdag/hapiscanner/releases/latest)** · [adım adım anlatım](https://hapicraft.com.tr/dogrula)

Açılan sayfadaki **Assets** listesinden yalnızca `HapiScanner.exe`'yi indirin. Kurulum yoktur, dosyaya çift tıklamanız yeterlidir (Windows 10/11, 64 bit). Kaynak kodu indirmeniz (`Source code (zip)`) gerekmez; `HapiVerify-*.jar` yalnızca sunucu yöneticileri içindir.

Her düzeltmede yeni bir sürüm yayınlanır; indirdiğiniz dosya her zaman `releases/latest` sayfasındaki olmalıdır.

## Bu nedir

Staff bir oyuncudan PC doğrulaması istediğinde:

1. Oyuncuya kısa ömürlü bir doğrulama kodu verilir
2. Oyuncu `HapiScanner.exe`'yi açıp kodu girer
3. Scanner, oyuncunun bilgisayarında bilinen hile izlerini **lokal olarak** arar
4. Sunucuya dosyalar değil, kural sonuçları gönderilir
5. Staff teknik kanıtlara bakarak karar verir

Oyuncunun mod, launcher, tarayıcı eklentisi veya ikinci bir yardımcı program kurması gerekmez.

## Bu ne değildir

HapiScanner bir hüküm mekanizması değildir. Kanıt üretir, karar vermez. Sunucu tarafındaki anti-cheat sisteminin **yerine geçmez**; onunla birlikte çalışır.

Scanner "program var mı?" sorusuna bakar. Anti-cheat "oyuncu nasıl davranıyor?" sorusuna bakar.

## Neden açık kaynak

Çünkü bu uygulama oyuncunun bilgisayarında, bazı durumlarda Administrator yetkisiyle çalışıyor. Böyle bir programın ne yaptığını oyuncunun kaynak koddan doğrulayabilmesi gerekir.

Güvenlik modeli hiçbir yerde "nasıl çalıştığını kimse bilmiyor" varsayımına dayanmaz. Saldırganın kaynak kodu okuduğu varsayılarak tasarlanmıştır.

## Neleri tarar

- Minecraft dizinleri (`.minecraft` ve `mods`, `versions`, `libraries`, `logs`)
- Bilinen launcher dizinleri
- Hile dosyalarının tipik olarak bulunduğu konumlar (aday filtresi ile, körlemesine değil)
- Dosya sistemi metadata'sı ve geçmişi (MFT, USN Journal, Geri Dönüşüm Kutusu kayıtları)
- HapiCraft'a bağlı aktif Minecraft process'i ve ona yüklenmiş native modüller

Tüm diskin hash'i alınmaz. Aday odaklı bir hat kullanılır: metadata -> aday -> yapısal inceleme -> hash -> kural.

## Neleri taramaz

Ekran görüntüsü almaz, mikrofon veya web kamerası kullanmaz, tuş vuruşu kaydetmez, panoyu okumaz, tarayıcı geçmişi veya çerez toplamaz, şifre veya Discord token aramaz, kişisel dosyalarınızı yüklemez, uzaktan erişim sağlamaz, gizli kalıcılık kurmaz.

Ayrıntılı liste: [docs/PRIVACY.md](docs/PRIVACY.md)

## Sunucuya ne gönderilir

Analiz bilgisayarınızda yapılır. Sunucuya gönderilen şey dosyalar değil, kural sonuçlarıdır: kural kimliği, kanıt tipi, zaman damgası, güven seviyesi, artifact durumu ve kapsam bilgisi.

Gönderilmez: dosya içerikleri, kişisel dosya yolları, tüm process listesi, Minecraft loglarının tamamı, sohbet içeriği, donanım seri numaraları. Cihaz özel anahtarı bilgisayarınızdan asla çıkmaz.

## Neden Administrator yetkisi isteyebilir

Silinmiş hile dosyalarının izleri (MFT, USN Journal) yalnızca birim seviyesinde erişimle okunabilir. Yetki vermezseniz scanner çökmez — ilgili katmanlar devre dışı kalır ve rapor `forensic_coverage: LIMITED` olarak işaretlenir.

Yetki yalnızca gerçekten gerekli olduğunda istenir.

## Sınırlar — dürüst liste

HapiScanner şunları **garanti etmez**:

- Her hileyi yakalamayı
- Donanım (fare/klavye içine gömülü) makrosunu yakalamayı
- Kernel seviyesi hileyi yakalamayı
- Bilinmeyen zero-day hileleri yakalamayı
- Geçmişteki her silinmiş dosyayı bulmayı
- Taramanın hileli bilgisayarda yapıldığını kriptografik olarak kanıtlamayı
- Tarama sonrası açılan bir hileyi önceden görmeyi

Bir tarama yalnızca anlık görüntüdür. Bu yüzden hiçbir yerde "bu oyuncu kesinlikle temiz" denmez; kullanılan ifade "belirtilen tarihte bilinen hile kanıtı bulunamadı"dır.

## Yanlış pozitifler

Yanlış pozitif bu projede bir hata değil, bir **tehdit** olarak ele alınır. Meşru modlar, Discord/Steam/NVIDIA overlay'leri, anti-virüs modülleri ve makro özellikli oyun çevre birimleri hileye benzer sinyaller üretebilir.

Bu yüzden: tek bir zayıf gösterge asla sonuç üretmez, kurallar asgari gösterge sayısı taşır ve hattın sonunda daima bir insan vardır. **Otomatik ban yoktur.**

## Desteklenen platform

İlk hedef: Windows 10 / Windows 11, x64, Minecraft Java Edition.

Bedrock oyuncuları bu modele otomatik olarak dahil edilmez; Bedrock desteği ayrı bir çalışma olarak değerlendirilecektir.

## Arayüz

Uygulama tek pencereli ve dört adımdır: **gizlilik onayı → doğrulama kodu → tarama → sonuç.**

Gizlilik ekranı bir "kabul et ve geç" ekranı değildir: ne okunduğu, ne okunmadığı, sunucuya ne gidip ne gitmediği dört ayrı listede gösterilir ve bu listeler `docs/PRIVACY.md` ile birebir aynı tutulur — ayrışırsa test başarısız olur. Onay kutusu işaretlenmeden tarama başlamaz; bu kural arayüzde değil, taramayı başlatan akış makinesinin içindedir.

Tarama ekranındaki günlük bir gizlilik süzgecinden geçer: tam dosya yolu, Windows kullanıcı adı ve doğrulama kodu maskelenir — ekran görüntüsü paylaşılsa bile.

Sonuç ekranında "temiz / hileci" ikilisi yoktur. Sonuç, tarama kapsamı ve kanıt özeti ayrı ayrı gösterilir, karar yetkiliye bırakılır.

### Derleme

| İş | Komut |
|---|---|
| Çekirdek + testler | `dotnet test scanner/HapiScanner.sln` |
| Windows arayüzü | `dotnet build scanner/HapiScanner.App/HapiScanner.App.csproj` |
| Arayüzü veri olmadan görmek | `HapiScanner.exe --tanitim` |

Yayınlanan `HapiScanner.exe` bu depodaki kaynaktan GitHub Actions üzerinde üretilir; `.github/workflows/release.yml` tam olarak hangi adımların çalıştığını gösterir.

## Lisans

Apache License 2.0 — bkz. [LICENSE](LICENSE).

## Bu uygulama virüs mü? Nasıl emin olabilirim?

Haklı bir soru: tanımadığınız bir programı, bazen Administrator yetkisiyle çalıştırmanız isteniyor. "Güven bize" demek yeterli değil, bu yüzden doğrulayabileceğiniz bir zincir kuruyoruz:

1. **Kaynak kod burada.** Scanner'ın da sunucu tarafının da tamamı açık. Ne yaptığını okuyabilirsiniz.
2. **Release'ler GitHub Actions ile derlenir.** Yayınlanan `HapiScanner.exe`'nin hangi commit'ten üretildiği görünür; kimsenin elinde derleyip yüklediği bir dosya değildir.
3. **SHA-256 yayınlanır.** İndirdiğiniz dosyanın hash'ini `SHA256SUMS` ile karşılaştırabilirsiniz:
   ```
   certutil -hashfile HapiScanner.exe SHA256
   ```
4. **Obfuscation, packer, gizli kalıcılık yok.** Kod okunabilir olmak üzere yazılır; antivirüsü yanıltacak hiçbir teknik kullanılmaz.

Dürüst not: açık kaynak olmak tek başına "virüs değil" kanıtı değildir — asıl kanıt yukarıdaki derleme ve checksum zinciridir. Windows kod imzalama (code signing) ileride eklenecektir; o zamana kadar Windows SmartScreen tanımadığı bir yayıncı için uyarı gösterebilir.

Scanner'ı yalnızca bu depodaki resmî release sayfasından indirin. Başka bir yerden gelen "HapiScanner" dosyasına güvenmeyin.
