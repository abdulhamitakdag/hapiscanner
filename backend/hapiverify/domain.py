from dataclasses import dataclass, field
from datetime import datetime
from enum import Enum
from typing import Optional

class VerificationDurumu(str, Enum):
    BEKLIYOR = "PENDING"
    DEVAM_EDIYOR = "IN_PROGRESS"
    TAMAMLANDI = "COMPLETED"
    IPTAL = "CANCELLED"
    SURESI_GECTI = "EXPIRED"
    OTURUM_KESILDI = "SESSION_INTERRUPTED"
    BAGLANTI_KAYBI = "CLIENT_CONNECTION_LOST"
    TOKEN_YAKILDI = "TOKEN_BURNED"

class TaramaSonucu(str, Enum):
    BILINEN_ARTIFACT_YOK = "NO_KNOWN_ARTIFACTS"
    ISARETLENDI = "FLAGGED"
    SUPHELI = "SUSPICIOUS"
    SONUCSUZ = "INCONCLUSIVE"
    TARAMA_BASARISIZ = "SCAN_FAILED"
    OTURUM_KESILDI = "SESSION_INTERRUPTED"
    BAGLANTI_KAYBI = "CLIENT_CONNECTION_LOST"
    OYNANMIS_SUPHESI = "TAMPER_SUSPECTED"
    ADLI_KAPSAM_DUSUK = "FORENSICS_DEGRADED"
    DESTEKLENMEYEN_ORTAM = "UNSUPPORTED_ENVIRONMENT"
    GECMIS_KAPSAMI_SINIRLI = "LIMITED_HISTORICAL_COVERAGE"

class AgDurumu(str, Enum):
    ESLESTI = "NETWORK_MATCH"
    ESLESMEDI = "NETWORK_MISMATCH"
    BILINMIYOR = "NETWORK_UNKNOWN"

class GuvenceSeviyesi(str, Enum):
    TPM = "TPM"
    YAZILIM = "SOFTWARE"

class CihazDurumu(str, Enum):
    AKTIF = "ACTIVE"
    EMEKLI = "RETIRED"

class Kapsam(str, Enum):
    IYI = "GOOD"
    SINIRLI = "LIMITED"
    YOK = "UNAVAILABLE"

BITMIS_DURUMLAR = frozenset({
    VerificationDurumu.TAMAMLANDI,
    VerificationDurumu.IPTAL,
    VerificationDurumu.SURESI_GECTI,
    VerificationDurumu.OTURUM_KESILDI,
    VerificationDurumu.BAGLANTI_KAYBI,
    VerificationDurumu.TOKEN_YAKILDI,
})

@dataclass
class Cihaz:
    device_id: str
    public_key: str
    public_key_fingerprint: str
    assurance_level: GuvenceSeviyesi
    first_seen: datetime
    last_seen: datetime
    status: CihazDurumu = CihazDurumu.AKTIF

@dataclass
class OyuncuCihazBagi:
    minecraft_uuid: str
    device_id: str
    first_linked: datetime
    last_used: datetime
    status: CihazDurumu = CihazDurumu.AKTIF

@dataclass
class Verification:
    verification_id: str
    minecraft_uuid: str
    username: str
    game_session_id: str
    token_hash: str
    created_at: datetime
    expires_at: datetime
    status: VerificationDurumu = VerificationDurumu.BEKLIYOR
    device_id: Optional[str] = None
    started_at: Optional[datetime] = None
    completed_at: Optional[datetime] = None
    game_ip: Optional[str] = None
    scanner_ip: Optional[str] = None
    ag_durumu: AgDurumu = AgDurumu.BILINMIYOR
    son_heartbeat: Optional[datetime] = None
    son_sira_no: int = 0
    yanlis_deneme: int = 0

    ip_silme_tarihi: Optional[datetime] = None

    def bitmis_mi(self) -> bool:
        return self.status in BITMIS_DURUMLAR

@dataclass
class Kanit:
    evidence_id: str
    report_id: str
    rule_id: str
    rule_version: str
    evidence_type: str
    artifact_category: str
    confidence: str
    severity: str
    currently_present: bool
    first_seen: Optional[datetime] = None
    last_seen: Optional[datetime] = None

@dataclass
class TaramaRaporu:
    report_id: str
    verification_id: str
    scanner_version: str
    ruleset_version: str
    overall_result: TaramaSonucu
    forensic_coverage: Kapsam
    runtime_coverage: Kapsam
    report_hash: str
    signature: str
    created_at: datetime
    kanitlar: list = field(default_factory=list)

    ozete_indirildi: bool = False

    def ozete_indir(self):
\
\
\
\
\

        self.kanitlar = []
        self.signature = ""
        self.report_hash = ""
        self.ozete_indirildi = True

@dataclass
class DenetimKaydi:
    olay: str
    verification_id: Optional[str]
    minecraft_uuid: Optional[str]
    zaman: datetime
    ayrinti: str = ""
