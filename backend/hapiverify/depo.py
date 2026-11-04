from collections import defaultdict, deque
from datetime import datetime, timedelta, timezone
from typing import Dict, List, Optional

from .domain import Cihaz, DenetimKaydi, OyuncuCihazBagi, TaramaRaporu, Verification

class Saat:

    def simdi(self) -> datetime:
        return datetime.now(timezone.utc)

class SahteSaat(Saat):

    def __init__(self, baslangic: Optional[datetime] = None):
        self._an = baslangic or datetime(2026, 11, 3, 12, 0, 0, tzinfo=timezone.utc)

    def simdi(self) -> datetime:
        return self._an

    def ilerle(self, saniye: int):
        self._an += timedelta(seconds=saniye)

class NonceDeposu:

    def __init__(self, saat: Saat):
        self._saat = saat
        self._acik: Dict[str, tuple] = {}
        self._harcanan: Dict[str, datetime] = {}

    def kaydet(self, nonce: str, verification_id: str, son_gecerlilik: datetime):
        self._acik[nonce] = (verification_id, son_gecerlilik)

    def harca(self, nonce: str, verification_id: str) -> bool:

        kayit = self._acik.get(nonce)
        if kayit is None:
            return False
        sahibi, son_gecerlilik = kayit
        if sahibi != verification_id:
            return False
        if self._saat.simdi() > son_gecerlilik:
            del self._acik[nonce]
            return False
        del self._acik[nonce]
        self._harcanan[nonce] = self._saat.simdi()
        return True

    def harcanmis_mi(self, nonce: str) -> bool:
        return nonce in self._harcanan

    def temizle(self):
        simdi = self._saat.simdi()
        for nonce in [n for n, (_, bitis) in self._acik.items() if simdi > bitis]:
            del self._acik[nonce]

class HizSiniri:

    def __init__(self, saat: Saat, adet: int, pencere_saniye: int):
        self._saat = saat
        self._adet = adet
        self._pencere = pencere_saniye
        self._kayitlar: Dict[str, deque] = defaultdict(deque)

    def izin_var_mi(self, anahtar: str) -> bool:
        simdi = self._saat.simdi()
        kuyruk = self._kayitlar[anahtar]
        while kuyruk and (simdi - kuyruk[0]).total_seconds() > self._pencere:
            kuyruk.popleft()
        return len(kuyruk) < self._adet

    def isle(self, anahtar: str):
        self._kayitlar[anahtar].append(self._saat.simdi())

class Depo:
\
\
\
\

    def __init__(self):
        self.verificationlar: Dict[str, Verification] = {}
        self.cihazlar: Dict[str, Cihaz] = {}
        self.oyuncu_cihazlari: List[OyuncuCihazBagi] = []
        self.raporlar: Dict[str, TaramaRaporu] = {}
        self.denetim: List[DenetimKaydi] = []

        self._acik_oyuncu: Dict[str, str] = {}

    def verification_ekle(self, v: Verification):
        self.verificationlar[v.verification_id] = v
        self._acik_oyuncu[v.minecraft_uuid] = v.verification_id

    def verification_al(self, verification_id: str) -> Optional[Verification]:
        return self.verificationlar.get(verification_id)

    def acik_verification(self, minecraft_uuid: str) -> Optional[Verification]:
        vid = self._acik_oyuncu.get(minecraft_uuid)
        if not vid:
            return None
        v = self.verificationlar.get(vid)
        if v and not v.bitmis_mi():
            return v
        return None

    def acigi_kapat(self, minecraft_uuid: str):
        self._acik_oyuncu.pop(minecraft_uuid, None)

    def token_hash_ile_bul(self, token_hash: str) -> Optional[Verification]:
        for v in self.verificationlar.values():
            if v.token_hash == token_hash:
                return v
        return None

    def cihaz_kaydet(self, cihaz: Cihaz):
        self.cihazlar[cihaz.device_id] = cihaz

    def cihaz_parmak_izi_ile(self, parmak_izi: str) -> Optional[Cihaz]:
        for c in self.cihazlar.values():
            if c.public_key_fingerprint == parmak_izi:
                return c
        return None

    def cihaz_bagi_al(self, minecraft_uuid: str, device_id: str) -> Optional[OyuncuCihazBagi]:
        for b in self.oyuncu_cihazlari:
            if b.minecraft_uuid == minecraft_uuid and b.device_id == device_id:
                return b
        return None

    def oyuncunun_cihazlari(self, minecraft_uuid: str) -> List[OyuncuCihazBagi]:
        return [b for b in self.oyuncu_cihazlari if b.minecraft_uuid == minecraft_uuid]

    def rapor_ekle(self, rapor: TaramaRaporu):
        self.raporlar[rapor.report_id] = rapor

    def verification_raporu(self, verification_id: str) -> Optional[TaramaRaporu]:
        for r in self.raporlar.values():
            if r.verification_id == verification_id:
                return r
        return None

    def denetime_yaz(self, kayit: DenetimKaydi):
        self.denetim.append(kayit)

    def rapor_temizligi(self, simdi: datetime, tam_gun: int, ozet_gun: int) -> dict:
\
\
\
\
\
\

        if tam_gun > ozet_gun:
            raise ValueError("tam saklama suresi ozet suresinden uzun olamaz")

        ozete_inen, silinen = 0, 0
        for report_id in list(self.raporlar):
            rapor = self.raporlar[report_id]
            yas = simdi - rapor.created_at
            if yas >= timedelta(days=ozet_gun):
                del self.raporlar[report_id]
                silinen += 1
            elif yas >= timedelta(days=tam_gun) and not rapor.ozete_indirildi:
                rapor.ozete_indir()
                ozete_inen += 1
        return {"ozete_inen": ozete_inen, "silinen": silinen}

    def ip_temizligi(self, simdi: datetime) -> int:

        sayac = 0
        for v in self.verificationlar.values():
            if v.ip_silme_tarihi and simdi >= v.ip_silme_tarihi:
                if v.game_ip or v.scanner_ip:
                    v.game_ip = None
                    v.scanner_ip = None
                    sayac += 1
        return sayac
