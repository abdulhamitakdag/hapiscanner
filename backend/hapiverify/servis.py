import uuid
from datetime import datetime, timedelta
from typing import Optional

from . import kripto
from .config import Ayarlar, VARSAYILAN
from .depo import Depo, HizSiniri, NonceDeposu, Saat
from .domain import (
    AgDurumu, Cihaz, DenetimKaydi, GuvenceSeviyesi,
    Kanit, Kapsam, OyuncuCihazBagi, TaramaRaporu, TaramaSonucu, Verification,
    VerificationDurumu,
)
from .oturum import OturumSaglayici

def _zaman_coz(deger) -> Optional[datetime]:
\
\
\
\
\

    if not deger:
        return None
    try:
        return datetime.fromisoformat(str(deger).replace("Z", "+00:00"))
    except (ValueError, TypeError):
        return None

class ServisHatasi(Exception):
    def __init__(self, kod: str, mesaj: str, http: int = 400):
        super().__init__(mesaj)
        self.kod = kod
        self.mesaj = mesaj
        self.http = http

class VerificationServisi:
    def __init__(self, depo: Depo, oturumlar: OturumSaglayici,
                 saat: Saat, ayarlar: Ayarlar = VARSAYILAN,
                 nonce_deposu: Optional[NonceDeposu] = None):
        self.depo = depo
        self.oturumlar = oturumlar
        self.saat = saat
        self.ayarlar = ayarlar

        self.nonce = nonce_deposu or NonceDeposu(saat)
        self.claim_limiti = HizSiniri(saat, ayarlar.claim_limit_adet,
                                      ayarlar.claim_limit_pencere_saniye)

    def _denetim(self, olay: str, v: Optional[Verification] = None, ayrinti: str = ""):
        self.depo.denetime_yaz(DenetimKaydi(
            olay=olay,
            verification_id=v.verification_id if v else None,
            minecraft_uuid=v.minecraft_uuid if v else None,
            zaman=self.saat.simdi(),
            ayrinti=ayrinti,
        ))

    def _besleme_taze_mi(self) -> bool:
        kontrol = getattr(self.oturumlar, "beslenme_taze_mi", None)
        return kontrol() if kontrol else True

    def olustur(self, minecraft_uuid: str) -> tuple:
        if not self._besleme_taze_mi():

            raise ServisHatasi("SESSION_FEED_STALE", "oyuncu listesi guncel degil", 503)

        oturum = self.oturumlar.oturum(minecraft_uuid)
        if oturum is None or not oturum.bagli:
            raise ServisHatasi("PLAYER_NOT_CONNECTED", "oyuncu sunucuda degil", 409)

        mevcut = self.depo.acik_verification(minecraft_uuid)
        if mevcut:

            raise ServisHatasi("ALREADY_PENDING", "oyuncunun acik dogrulamasi var", 409)

        token = kripto.token_uret(self.ayarlar)
        simdi = self.saat.simdi()
        v = Verification(
            verification_id=str(uuid.uuid4()),
            minecraft_uuid=minecraft_uuid,
            username=oturum.username,
            game_session_id=oturum.game_session_id,
            token_hash=kripto.token_hashle(token),
            created_at=simdi,
            expires_at=simdi + timedelta(seconds=self.ayarlar.token_ttl_saniye),
            game_ip=oturum.game_ip,
            ip_silme_tarihi=simdi + timedelta(days=self.ayarlar.ip_saklama_gunu),
        )
        self.depo.verification_ekle(v)
        self._denetim("verification started", v)

        return v, token

    def durum(self, minecraft_uuid: str) -> Optional[Verification]:
        v = self.depo.acik_verification(minecraft_uuid)
        if v:
            self._surekliligi_denetle(v)
            self._suresi_gecti_mi(v)
        return v or self._son_verification(minecraft_uuid)

    def _son_verification(self, minecraft_uuid: str) -> Optional[Verification]:
        adaylar = [v for v in self.depo.verificationlar.values()
                   if v.minecraft_uuid == minecraft_uuid]
        return max(adaylar, key=lambda v: v.created_at) if adaylar else None

    def iptal(self, minecraft_uuid: str) -> Verification:
        v = self.depo.acik_verification(minecraft_uuid)
        if not v:
            raise ServisHatasi("NO_ACTIVE_VERIFICATION", "acik dogrulama yok", 404)
        self._bitir(v, VerificationDurumu.IPTAL)
        self._denetim("verification cancelled", v)
        return v

    def _bitir(self, v: Verification, durum: VerificationDurumu):
        v.status = durum
        v.completed_at = self.saat.simdi()
        self.depo.acigi_kapat(v.minecraft_uuid)

    def _suresi_gecti_mi(self, v: Verification) -> bool:
        if v.bitmis_mi():
            return v.status == VerificationDurumu.SURESI_GECTI

        if self.saat.simdi() > v.expires_at and v.status == VerificationDurumu.BEKLIYOR:
            self._bitir(v, VerificationDurumu.SURESI_GECTI)
            self._denetim("verification expired", v)
            return True
        return False

    def _surekliligi_denetle(self, v: Verification) -> bool:

        if v.bitmis_mi():
            return False
        if not self._besleme_taze_mi():

            return False
        oturum = self.oturumlar.oturum(v.minecraft_uuid)
        if oturum is None or not oturum.bagli:
            self._bitir(v, VerificationDurumu.OTURUM_KESILDI)
            self._denetim("session interrupted", v, "baglanti koptu")
            return True
        if oturum.game_session_id != v.game_session_id:

            self._bitir(v, VerificationDurumu.OTURUM_KESILDI)
            self._denetim("session interrupted", v, "game_session_id degisti")
            return True
        return False

    def claim(self, token: str, public_key: str, assurance: str,
              scanner_version: str, ruleset_version: str,
              scanner_ip: Optional[str] = None) -> dict:
        limit_anahtari = scanner_ip or "bilinmeyen"
        if not self.claim_limiti.izin_var_mi(limit_anahtari):
            raise ServisHatasi("RATE_LIMITED", "cok fazla deneme", 429)

        self._surum_politikasi(scanner_version, ruleset_version)

        v = self.depo.token_hash_ile_bul(kripto.token_hashle(token))
        if v is None:

            self.claim_limiti.isle(limit_anahtari)
            self._denetim("scanner rejected", None, "token bulunamadi")
            raise ServisHatasi("INVALID_TOKEN", "kod gecersiz", 401)

        self._suresi_gecti_mi(v)
        self._surekliligi_denetle(v)

        if v.status == VerificationDurumu.SURESI_GECTI:
            raise ServisHatasi("TOKEN_EXPIRED", "kodun suresi gecti", 410)
        if v.status == VerificationDurumu.OTURUM_KESILDI:
            raise ServisHatasi("SESSION_INTERRUPTED", "minecraft oturumu kesildi", 409)
        if v.status != VerificationDurumu.BEKLIYOR:

            self._denetim("scanner rejected", v, f"tekrar claim denemesi ({v.status.value})")
            raise ServisHatasi("TOKEN_ALREADY_USED", "kod zaten kullanilmis", 409)

        cihaz = self._cihaz_kaydet(public_key, assurance)
        yeni_cihaz = self._cihazi_bagla(v.minecraft_uuid, cihaz)

        v.device_id = cihaz.device_id
        v.started_at = self.saat.simdi()
        v.status = VerificationDurumu.DEVAM_EDIYOR
        v.scanner_ip = scanner_ip
        v.ag_durumu = self._ag_durumu(v.game_ip, scanner_ip)
        v.son_heartbeat = self.saat.simdi()

        nonce = self._nonce_ver(v)
        self._denetim("verification claimed", v,
                      f"cihaz={cihaz.device_id[:8]} yeni_cihaz={yeni_cihaz}")

        return {
            "verification_id": v.verification_id,
            "username": v.username,
            "nonce": nonce,
            "device_id": cihaz.device_id,
            "new_device": yeni_cihaz,
            "network_status": v.ag_durumu.value,
            "heartbeat_interval": self.ayarlar.heartbeat_saniye,

            "hapicraft_endpoints": list(self.ayarlar.hapicraft_endpointleri),
            "expires_at": v.expires_at.isoformat(),
        }

    def _surum_politikasi(self, scanner_version: str, ruleset_version: str):
        if scanner_version in self.ayarlar.yasakli_scanner_surumleri:
            raise ServisHatasi("SCANNER_VERSION_BLOCKED", "bu scanner surumu reddedildi", 403)
        if kripto.surum_kucuk_mu(scanner_version, self.ayarlar.asgari_scanner_surumu):
            raise ServisHatasi("SCANNER_TOO_OLD", "scanner surumu cok eski", 403)
        if kripto.surum_kucuk_mu(ruleset_version, self.ayarlar.asgari_ruleset_surumu):
            raise ServisHatasi("RULESET_TOO_OLD", "kural paketi surumu cok eski", 403)

    def _cihaz_kaydet(self, public_key: str, assurance: str) -> Cihaz:
        try:
            seviye = GuvenceSeviyesi(assurance)
        except ValueError:
            raise ServisHatasi("INVALID_ASSURANCE", "gecersiz guvence seviyesi") from None
        izi = kripto.parmak_izi(public_key)
        mevcut = self.depo.cihaz_parmak_izi_ile(izi)
        simdi = self.saat.simdi()
        if mevcut:
            mevcut.last_seen = simdi
            return mevcut
        cihaz = Cihaz(
            device_id=str(uuid.uuid4()),
            public_key=public_key,
            public_key_fingerprint=izi,
            assurance_level=seviye,
            first_seen=simdi,
            last_seen=simdi,
        )
        self.depo.cihaz_kaydet(cihaz)
        return cihaz

    def _cihazi_bagla(self, minecraft_uuid: str, cihaz: Cihaz) -> bool:
        simdi = self.saat.simdi()
        bag = self.depo.cihaz_bagi_al(minecraft_uuid, cihaz.device_id)
        if bag:
            bag.last_used = simdi
            return False

        self.depo.oyuncu_cihazlari.append(OyuncuCihazBagi(
            minecraft_uuid=minecraft_uuid,
            device_id=cihaz.device_id,
            first_linked=simdi,
            last_used=simdi,
        ))
        yeni = len(self.depo.oyuncunun_cihazlari(minecraft_uuid)) > 1
        if yeni:
            self._denetim("device changed", None, f"oyuncu={minecraft_uuid}")
        return yeni

    def _ag_durumu(self, game_ip: Optional[str], scanner_ip: Optional[str]) -> AgDurumu:

        if not game_ip or not scanner_ip:
            return AgDurumu.BILINMIYOR
        return AgDurumu.ESLESTI if game_ip == scanner_ip else AgDurumu.ESLESMEDI

    def _nonce_ver(self, v: Verification) -> str:
        nonce = kripto.nonce_uret(self.ayarlar)
        self.nonce.kaydet(nonce, v.verification_id,
                          self.saat.simdi() + timedelta(seconds=self.ayarlar.nonce_ttl_saniye))
        return nonce

    def heartbeat(self, verification_id: str, nonce: str, imza: str, sira_no: int,
                  asama: str, yuzde: int, client_timestamp: str) -> dict:
        v = self._aktif_al(verification_id)
        cihaz = self.depo.cihazlar[v.device_id]

        metin = kripto.imza_metni(
            "heartbeat",
            verification_id=verification_id,
            nonce=nonce,
            sequence=sira_no,
            stage=asama,
            timestamp=client_timestamp,
        )
        if not self.nonce.harca(nonce, verification_id):
            self._denetim("replay rejected", v, "heartbeat nonce gecersiz")
            raise ServisHatasi("INVALID_NONCE", "nonce gecersiz veya kullanilmis", 409)
        if not kripto.imza_dogrula(cihaz.public_key, imza, metin):
            self._denetim("signature rejected", v, "heartbeat imzasi gecersiz")
            raise ServisHatasi("INVALID_SIGNATURE", "imza dogrulanamadi", 401)
        if sira_no <= v.son_sira_no:
            raise ServisHatasi("STALE_SEQUENCE", "sira numarasi geriye gidemez", 409)

        v.son_sira_no = sira_no

        v.son_heartbeat = self.saat.simdi()
        return {"nonce": self._nonce_ver(v), "server_time": v.son_heartbeat.isoformat()}

    def _aktif_al(self, verification_id: str) -> Verification:
        v = self.depo.verification_al(verification_id)
        if v is None:
            raise ServisHatasi("UNKNOWN_VERIFICATION", "dogrulama bulunamadi", 404)
        self._suresi_gecti_mi(v)
        self._surekliligi_denetle(v)
        self._heartbeat_denetle(v)
        if v.status != VerificationDurumu.DEVAM_EDIYOR:
            raise ServisHatasi("NOT_IN_PROGRESS", f"dogrulama aktif degil ({v.status.value})", 409)
        if v.device_id is None or v.device_id not in self.depo.cihazlar:
            raise ServisHatasi("DEVICE_UNKNOWN", "cihaz kayitli degil", 409)
        return v

    def _heartbeat_denetle(self, v: Verification) -> bool:

        if v.status != VerificationDurumu.DEVAM_EDIYOR or v.son_heartbeat is None:
            return False
        gecen = (self.saat.simdi() - v.son_heartbeat).total_seconds()
        if gecen > self.ayarlar.heartbeat_tolerans_saniye:
            self._bitir(v, VerificationDurumu.BAGLANTI_KAYBI)
            self._denetim("client connection lost", v, f"{int(gecen)} sn sessiz")
            return True
        return False

    def rapor_tamamla(self, verification_id: str, nonce: str, imza: str,
                      scanner_version: str, ruleset_version: str,
                      sonuc: str, report_hash: str, govde: str,
                      forensic_coverage: str, runtime_coverage: str,
                      kanitlar: list, client_timestamp: str) -> TaramaRaporu:
        v = self._aktif_al(verification_id)
        cihaz = self.depo.cihazlar[v.device_id]

        self._surum_politikasi(scanner_version, ruleset_version)

        if self.depo.verification_raporu(verification_id) is not None:

            self._denetim("replay rejected", v, "rapor zaten var")
            raise ServisHatasi("REPORT_ALREADY_SUBMITTED", "rapor zaten gonderilmis", 409)

        metin = kripto.imza_metni(
            "report",
            verification_id=verification_id,
            nonce=nonce,
            scanner_version=scanner_version,
            ruleset_version=ruleset_version,
            report_hash=report_hash,
            timestamp=client_timestamp,
        )
        if not self.nonce.harca(nonce, verification_id):
            self._denetim("replay rejected", v, "rapor nonce gecersiz")
            raise ServisHatasi("INVALID_NONCE", "nonce gecersiz veya kullanilmis", 409)
        if not kripto.imza_dogrula(cihaz.public_key, imza, metin):
            self._bitir(v, VerificationDurumu.TAMAMLANDI)
            self._kayit_oynanmis(v, scanner_version, ruleset_version, report_hash, imza)
            self._denetim("signature rejected", v, "rapor imzasi gecersiz")
            raise ServisHatasi("INVALID_SIGNATURE", "imza dogrulanamadi", 401)
        if kripto.rapor_hash(govde) != report_hash:

            self._bitir(v, VerificationDurumu.TAMAMLANDI)
            self._kayit_oynanmis(v, scanner_version, ruleset_version, report_hash, imza)
            self._denetim("tamper suspected", v, "report_hash uyusmuyor")
            raise ServisHatasi("REPORT_HASH_MISMATCH", "rapor butunlugu dogrulanamadi", 409)

        nihai = self._sonucu_sinifla(sonuc, forensic_coverage, runtime_coverage)
        rapor = TaramaRaporu(
            report_id=str(uuid.uuid4()),
            verification_id=verification_id,
            scanner_version=scanner_version,
            ruleset_version=ruleset_version,
            overall_result=nihai,
            forensic_coverage=Kapsam(forensic_coverage),
            runtime_coverage=Kapsam(runtime_coverage),
            report_hash=report_hash,
            signature=imza,
            created_at=self.saat.simdi(),
        )
        for k in kanitlar:
            rapor.kanitlar.append(Kanit(
                evidence_id=str(uuid.uuid4()),
                report_id=rapor.report_id,
                rule_id=k["rule_id"],
                rule_version=k.get("rule_version", ruleset_version),
                evidence_type=k["evidence_type"],
                artifact_category=k.get("artifact_category", "UNKNOWN"),
                confidence=k.get("confidence", "MEDIUM"),
                severity=k.get("severity", "MEDIUM"),
                currently_present=bool(k.get("currently_present", False)),
                first_seen=_zaman_coz(k.get("first_seen")),
                last_seen=_zaman_coz(k.get("last_seen")),
            ))

        self.depo.rapor_ekle(rapor)
        self._bitir(v, VerificationDurumu.TAMAMLANDI)
        self._denetim("verification completed", v, f"sonuc={nihai.value}")
        return rapor

    def _kayit_oynanmis(self, v: Verification, sv: str, rv: str, rh: str, imza: str):
        rapor = TaramaRaporu(
            report_id=str(uuid.uuid4()),
            verification_id=v.verification_id,
            scanner_version=sv,
            ruleset_version=rv,
            overall_result=TaramaSonucu.OYNANMIS_SUPHESI,
            forensic_coverage=Kapsam.YOK,
            runtime_coverage=Kapsam.YOK,
            report_hash=rh,
            signature=imza,
            created_at=self.saat.simdi(),
        )
        self.depo.rapor_ekle(rapor)

    def _sonucu_sinifla(self, sonuc: str, forensic: str, runtime: str) -> TaramaSonucu:

        try:
            istenen = TaramaSonucu(sonuc)
        except ValueError:
            raise ServisHatasi("INVALID_RESULT", "gecersiz sonuc degeri") from None
        try:
            f_kapsam, r_kapsam = Kapsam(forensic), Kapsam(runtime)
        except ValueError:
            raise ServisHatasi("INVALID_COVERAGE", "gecersiz kapsam degeri") from None

        if istenen == TaramaSonucu.BILINEN_ARTIFACT_YOK:
            if f_kapsam == Kapsam.YOK:
                return TaramaSonucu.SONUCSUZ
            if f_kapsam == Kapsam.SINIRLI:
                return TaramaSonucu.GECMIS_KAPSAMI_SINIRLI
            if r_kapsam == Kapsam.YOK:
                return TaramaSonucu.SONUCSUZ
        return istenen

    def ip_temizligi(self) -> int:
        return self.depo.ip_temizligi(self.saat.simdi())

    def rapor_temizligi(self) -> dict:
        return self.depo.rapor_temizligi(
            self.saat.simdi(),
            self.ayarlar.rapor_tam_saklama_gunu,
            self.ayarlar.rapor_ozet_saklama_gunu)

    def bakim(self) -> dict:
\
\
\

        return {"ip": self.ip_temizligi(), **self.rapor_temizligi()}
