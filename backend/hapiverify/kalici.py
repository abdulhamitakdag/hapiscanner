\
\
\
\
\
\
\
\
\
\
\
\
\
\
\
\
\
\
\
\
\

import os
import sqlite3
from datetime import datetime, timezone
from typing import Dict, Optional

from .depo import Depo, NonceDeposu, Saat
from .domain import (
    AgDurumu, Cihaz, CihazDurumu, DenetimKaydi, GuvenceSeviyesi, Kanit, Kapsam,
    OyuncuCihazBagi, TaramaRaporu, TaramaSonucu, Verification, VerificationDurumu,
)

SEMA = """
CREATE TABLE IF NOT EXISTS verifications (
    verification_id TEXT PRIMARY KEY,
    minecraft_uuid  TEXT NOT NULL,
    username        TEXT NOT NULL,
    game_session_id TEXT NOT NULL,
    token_hash      TEXT NOT NULL,
    created_at      TEXT NOT NULL,
    expires_at      TEXT NOT NULL,
    status          TEXT NOT NULL,
    device_id       TEXT,
    started_at      TEXT,
    completed_at    TEXT,
    game_ip         TEXT,
    scanner_ip      TEXT,
    ag_durumu       TEXT NOT NULL,
    son_heartbeat   TEXT,
    son_sira_no     INTEGER NOT NULL DEFAULT 0,
    yanlis_deneme   INTEGER NOT NULL DEFAULT 0,
    ip_silme_tarihi TEXT
);
CREATE INDEX IF NOT EXISTS ix_verifications_oyuncu ON verifications(minecraft_uuid);
CREATE INDEX IF NOT EXISTS ix_verifications_token ON verifications(token_hash);

CREATE TABLE IF NOT EXISTS acik_dogrulama (
    minecraft_uuid  TEXT PRIMARY KEY,
    verification_id TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS devices (
    device_id   TEXT PRIMARY KEY,
    public_key  TEXT NOT NULL,
    fingerprint TEXT NOT NULL,
    assurance   TEXT NOT NULL,
    first_seen  TEXT NOT NULL,
    last_seen   TEXT NOT NULL,
    status      TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_devices_izi ON devices(fingerprint);

CREATE TABLE IF NOT EXISTS player_devices (
    minecraft_uuid TEXT NOT NULL,
    device_id      TEXT NOT NULL,
    first_linked   TEXT NOT NULL,
    last_used      TEXT NOT NULL,
    status         TEXT NOT NULL,
    PRIMARY KEY (minecraft_uuid, device_id)
);

CREATE TABLE IF NOT EXISTS reports (
    report_id        TEXT PRIMARY KEY,
    verification_id  TEXT NOT NULL UNIQUE,
    scanner_version  TEXT NOT NULL,
    ruleset_version  TEXT NOT NULL,
    overall_result   TEXT NOT NULL,
    forensic_coverage TEXT NOT NULL,
    runtime_coverage TEXT NOT NULL,
    report_hash      TEXT NOT NULL,
    signature        TEXT NOT NULL,
    created_at       TEXT NOT NULL,
    ozete_indirildi  INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS evidence (
    evidence_id       TEXT PRIMARY KEY,
    report_id         TEXT NOT NULL,
    rule_id           TEXT NOT NULL,
    rule_version      TEXT NOT NULL,
    evidence_type     TEXT NOT NULL,
    artifact_category TEXT NOT NULL,
    confidence        TEXT NOT NULL,
    severity          TEXT NOT NULL,
    currently_present INTEGER NOT NULL,
    first_seen        TEXT,
    last_seen         TEXT
);
CREATE INDEX IF NOT EXISTS ix_evidence_rapor ON evidence(report_id);

CREATE TABLE IF NOT EXISTS audit (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    olay            TEXT NOT NULL,
    verification_id TEXT,
    minecraft_uuid  TEXT,
    zaman           TEXT NOT NULL,
    ayrinti         TEXT NOT NULL DEFAULT ''
);

CREATE TABLE IF NOT EXISTS nonces (
    nonce           TEXT PRIMARY KEY,
    verification_id TEXT NOT NULL,
    son_gecerlilik  TEXT NOT NULL,
    harcandi        INTEGER NOT NULL DEFAULT 0,
    harcanma_zamani TEXT
);
"""

def baglanti_ac(yol: str) -> sqlite3.Connection:
\
\
\
\

    if yol != ":memory:":
        dizin = os.path.dirname(os.path.abspath(yol))
        os.makedirs(dizin, exist_ok=True)
    baglanti = sqlite3.connect(yol, check_same_thread=False)
    baglanti.row_factory = sqlite3.Row
    if yol != ":memory:":
        baglanti.execute("PRAGMA journal_mode=WAL")
    baglanti.execute("PRAGMA synchronous=FULL")
    baglanti.executescript(SEMA)
    baglanti.commit()
    return baglanti

def _z(deger: Optional[datetime]) -> Optional[str]:
    return deger.isoformat() if deger else None

def _t(deger) -> Optional[datetime]:
    if not deger:
        return None
    an = datetime.fromisoformat(deger)

    return an if an.tzinfo else an.replace(tzinfo=timezone.utc)

class KaliciDepo(Depo):

    def __init__(self, baglanti: sqlite3.Connection):
        super().__init__()
        self._db = baglanti
        self._imzalar: Dict[str, tuple] = {}
        self._silinen_raporlar: set = set()
        self.yukle()

    def yukle(self):
        for s in self._db.execute("SELECT * FROM verifications"):
            v = Verification(
                verification_id=s["verification_id"],
                minecraft_uuid=s["minecraft_uuid"],
                username=s["username"],
                game_session_id=s["game_session_id"],
                token_hash=s["token_hash"],
                created_at=_t(s["created_at"]),
                expires_at=_t(s["expires_at"]),
                status=VerificationDurumu(s["status"]),
                device_id=s["device_id"],
                started_at=_t(s["started_at"]),
                completed_at=_t(s["completed_at"]),
                game_ip=s["game_ip"],
                scanner_ip=s["scanner_ip"],
                ag_durumu=AgDurumu(s["ag_durumu"]),
                son_heartbeat=_t(s["son_heartbeat"]),
                son_sira_no=s["son_sira_no"],
                yanlis_deneme=s["yanlis_deneme"],
                ip_silme_tarihi=_t(s["ip_silme_tarihi"]),
            )
            self.verificationlar[v.verification_id] = v

        for s in self._db.execute("SELECT * FROM acik_dogrulama"):
            self._acik_oyuncu[s["minecraft_uuid"]] = s["verification_id"]

        for s in self._db.execute("SELECT * FROM devices"):
            self.cihazlar[s["device_id"]] = Cihaz(
                device_id=s["device_id"],
                public_key=s["public_key"],
                public_key_fingerprint=s["fingerprint"],
                assurance_level=GuvenceSeviyesi(s["assurance"]),
                first_seen=_t(s["first_seen"]),
                last_seen=_t(s["last_seen"]),
                status=CihazDurumu(s["status"]),
            )

        for s in self._db.execute("SELECT * FROM player_devices"):
            self.oyuncu_cihazlari.append(OyuncuCihazBagi(
                minecraft_uuid=s["minecraft_uuid"],
                device_id=s["device_id"],
                first_linked=_t(s["first_linked"]),
                last_used=_t(s["last_used"]),
                status=CihazDurumu(s["status"]),
            ))

        kanitlar: Dict[str, list] = {}
        for s in self._db.execute("SELECT * FROM evidence"):
            kanitlar.setdefault(s["report_id"], []).append(Kanit(
                evidence_id=s["evidence_id"],
                report_id=s["report_id"],
                rule_id=s["rule_id"],
                rule_version=s["rule_version"],
                evidence_type=s["evidence_type"],
                artifact_category=s["artifact_category"],
                confidence=s["confidence"],
                severity=s["severity"],
                currently_present=bool(s["currently_present"]),
                first_seen=_t(s["first_seen"]),
                last_seen=_t(s["last_seen"]),
            ))

        for s in self._db.execute("SELECT * FROM reports"):
            self.raporlar[s["report_id"]] = TaramaRaporu(
                report_id=s["report_id"],
                verification_id=s["verification_id"],
                scanner_version=s["scanner_version"],
                ruleset_version=s["ruleset_version"],
                overall_result=TaramaSonucu(s["overall_result"]),
                forensic_coverage=Kapsam(s["forensic_coverage"]),
                runtime_coverage=Kapsam(s["runtime_coverage"]),
                report_hash=s["report_hash"],
                signature=s["signature"],
                created_at=_t(s["created_at"]),
                kanitlar=kanitlar.get(s["report_id"], []),
                ozete_indirildi=bool(s["ozete_indirildi"]),
            )

        self._imzalari_tazele()

    def rapor_temizligi(self, simdi, tam_gun, ozet_gun) -> dict:
        oncesi = set(self.raporlar)
        sonuc = super().rapor_temizligi(simdi, tam_gun, ozet_gun)
        self._silinen_raporlar |= (oncesi - set(self.raporlar))
        return sonuc

    def yaz(self):

        yazildi = 0
        for v in self.verificationlar.values():
            if self._degisti("v", v.verification_id, self._v_imza(v)):
                self._v_yaz(v)
                yazildi += 1

        acik_imza = tuple(sorted(self._acik_oyuncu.items()))
        if self._degisti("acik", "", acik_imza):
            self._db.execute("DELETE FROM acik_dogrulama")
            self._db.executemany(
                "INSERT INTO acik_dogrulama(minecraft_uuid, verification_id) VALUES (?, ?)",
                list(self._acik_oyuncu.items()))
            yazildi += 1

        for c in self.cihazlar.values():
            if self._degisti("c", c.device_id, self._c_imza(c)):
                self._c_yaz(c)
                yazildi += 1

        for b in self.oyuncu_cihazlari:
            anahtar = b.minecraft_uuid + "/" + b.device_id
            if self._degisti("b", anahtar, self._b_imza(b)):
                self._b_yaz(b)
                yazildi += 1

        for r in self.raporlar.values():
            if self._degisti("r", r.report_id, self._r_imza(r)):
                self._r_yaz(r)
                yazildi += 1

        for report_id in self._silinen_raporlar:
            self._db.execute("DELETE FROM evidence WHERE report_id = ?", (report_id,))
            self._db.execute("DELETE FROM reports WHERE report_id = ?", (report_id,))
            self._imzalar.pop("r:" + report_id, None)
            yazildi += 1
        self._silinen_raporlar.clear()

        yazilmamis = self.denetim[self._imzalar.get("denetim:", (0,))[0]:]
        for k in yazilmamis:
            self._denetim_yaz(k)
        if yazilmamis:
            self._imzalar["denetim:"] = (len(self.denetim),)
            yazildi += len(yazilmamis)

        if yazildi:
            self._db.commit()
        return yazildi

    def _degisti(self, tur: str, kimlik: str, imza: tuple) -> bool:
        anahtar = f"{tur}:{kimlik}"
        if self._imzalar.get(anahtar) == imza:
            return False
        self._imzalar[anahtar] = imza
        return True

    def _imzalari_tazele(self):
        self._imzalar = {"denetim:": (len(self.denetim),)}
        for v in self.verificationlar.values():
            self._imzalar["v:" + v.verification_id] = self._v_imza(v)
        for c in self.cihazlar.values():
            self._imzalar["c:" + c.device_id] = self._c_imza(c)
        for b in self.oyuncu_cihazlari:
            self._imzalar[f"b:{b.minecraft_uuid}/{b.device_id}"] = self._b_imza(b)
        for r in self.raporlar.values():
            self._imzalar["r:" + r.report_id] = self._r_imza(r)
        self._imzalar["acik:"] = tuple(sorted(self._acik_oyuncu.items()))

    @staticmethod
    def _v_imza(v: Verification) -> tuple:
        return (v.status.value, v.device_id, _z(v.started_at), _z(v.completed_at),
                v.game_ip, v.scanner_ip, v.ag_durumu.value, _z(v.son_heartbeat),
                v.son_sira_no, v.yanlis_deneme, _z(v.ip_silme_tarihi),
                v.username, v.game_session_id, v.token_hash,
                _z(v.created_at), _z(v.expires_at))

    @staticmethod
    def _c_imza(c: Cihaz) -> tuple:
        return (c.public_key, c.public_key_fingerprint, c.assurance_level.value,
                _z(c.first_seen), _z(c.last_seen), c.status.value)

    @staticmethod
    def _b_imza(b: OyuncuCihazBagi) -> tuple:
        return (_z(b.first_linked), _z(b.last_used), b.status.value)

    @staticmethod
    def _r_imza(r: TaramaRaporu) -> tuple:
        return (r.overall_result.value, r.report_hash, r.signature,
                r.ozete_indirildi, len(r.kanitlar))

    def _v_yaz(self, v: Verification):
        self._db.execute("""
            INSERT INTO verifications VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)
            ON CONFLICT(verification_id) DO UPDATE SET
                username=excluded.username, status=excluded.status,
                device_id=excluded.device_id, started_at=excluded.started_at,
                completed_at=excluded.completed_at, game_ip=excluded.game_ip,
                scanner_ip=excluded.scanner_ip, ag_durumu=excluded.ag_durumu,
                son_heartbeat=excluded.son_heartbeat, son_sira_no=excluded.son_sira_no,
                yanlis_deneme=excluded.yanlis_deneme,
                ip_silme_tarihi=excluded.ip_silme_tarihi
        """, (v.verification_id, v.minecraft_uuid, v.username, v.game_session_id,
              v.token_hash, _z(v.created_at), _z(v.expires_at), v.status.value,
              v.device_id, _z(v.started_at), _z(v.completed_at), v.game_ip,
              v.scanner_ip, v.ag_durumu.value, _z(v.son_heartbeat), v.son_sira_no,
              v.yanlis_deneme, _z(v.ip_silme_tarihi)))

    def _c_yaz(self, c: Cihaz):
        self._db.execute("""
            INSERT INTO devices VALUES (?,?,?,?,?,?,?)
            ON CONFLICT(device_id) DO UPDATE SET
                last_seen=excluded.last_seen, status=excluded.status
        """, (c.device_id, c.public_key, c.public_key_fingerprint,
              c.assurance_level.value, _z(c.first_seen), _z(c.last_seen),
              c.status.value))

    def _b_yaz(self, b: OyuncuCihazBagi):
        self._db.execute("""
            INSERT INTO player_devices VALUES (?,?,?,?,?)
            ON CONFLICT(minecraft_uuid, device_id) DO UPDATE SET
                last_used=excluded.last_used, status=excluded.status
        """, (b.minecraft_uuid, b.device_id, _z(b.first_linked), _z(b.last_used),
              b.status.value))

    def _r_yaz(self, r: TaramaRaporu):
        self._db.execute("""
            INSERT INTO reports VALUES (?,?,?,?,?,?,?,?,?,?,?)
            ON CONFLICT(report_id) DO UPDATE SET
                overall_result=excluded.overall_result,
                report_hash=excluded.report_hash, signature=excluded.signature,
                ozete_indirildi=excluded.ozete_indirildi
        """, (r.report_id, r.verification_id, r.scanner_version, r.ruleset_version,
              r.overall_result.value, r.forensic_coverage.value,
              r.runtime_coverage.value, r.report_hash, r.signature,
              _z(r.created_at), int(r.ozete_indirildi)))

        self._db.execute("DELETE FROM evidence WHERE report_id = ?", (r.report_id,))
        self._db.executemany("""
            INSERT INTO evidence VALUES (?,?,?,?,?,?,?,?,?,?,?)
        """, [(k.evidence_id, k.report_id, k.rule_id, k.rule_version,
               k.evidence_type, k.artifact_category, k.confidence, k.severity,
               int(k.currently_present), _z(k.first_seen), _z(k.last_seen))
              for k in r.kanitlar])

    def _denetim_yaz(self, k: DenetimKaydi):
        self._db.execute("""
            INSERT INTO audit(olay, verification_id, minecraft_uuid, zaman, ayrinti)
            VALUES (?,?,?,?,?)
        """, (k.olay, k.verification_id, k.minecraft_uuid, _z(k.zaman), k.ayrinti))

class KaliciNonceDeposu(NonceDeposu):
\
\
\
\
\
\
\
\
\

    def __init__(self, saat: Saat, baglanti: sqlite3.Connection):
        super().__init__(saat)
        self._db = baglanti
        for s in self._db.execute("SELECT * FROM nonces"):
            if s["harcandi"]:
                self._harcanan[s["nonce"]] = _t(s["harcanma_zamani"])
            else:
                self._acik[s["nonce"]] = (s["verification_id"], _t(s["son_gecerlilik"]))

    def kaydet(self, nonce, verification_id, son_gecerlilik):
        super().kaydet(nonce, verification_id, son_gecerlilik)
        self._db.execute(
            "INSERT OR REPLACE INTO nonces(nonce, verification_id, son_gecerlilik, harcandi)"
            " VALUES (?,?,?,0)", (nonce, verification_id, _z(son_gecerlilik)))
        self._db.commit()

    def harca(self, nonce, verification_id) -> bool:
        sonuc = super().harca(nonce, verification_id)
        if sonuc:
            self._db.execute(
                "UPDATE nonces SET harcandi = 1, harcanma_zamani = ? WHERE nonce = ?",
                (_z(self._saat.simdi()), nonce))
        else:

            self._db.execute("DELETE FROM nonces WHERE nonce = ? AND harcandi = 0", (nonce,))
        self._db.commit()
        return sonuc

    def temizle(self):
        super().temizle()
        self._db.execute(
            "DELETE FROM nonces WHERE harcandi = 0 AND son_gecerlilik < ?",
            (_z(self._saat.simdi()),))
        self._db.commit()
