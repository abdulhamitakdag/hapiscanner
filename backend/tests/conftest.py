import base64
import os
import sys

import pytest
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import ec

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

os.environ.setdefault("HAPIVERIFY_DB", ":memory:")

from hapiverify import kripto
from hapiverify.config import Ayarlar
from hapiverify.depo import Depo, SahteSaat
from hapiverify.oturum import SahteOturumSaglayici
from hapiverify.servis import VerificationServisi

OYUNCU = "11111111-2222-3333-4444-555555555555"
SCANNER_SURUMU = "0.1.0"
RULESET_SURUMU = "2026.11.03.1"

class SahteCihaz:

    def __init__(self):
        self._gizli = ec.generate_private_key(ec.SECP256R1())

    @property
    def public_key(self) -> str:
        der = self._gizli.public_key().public_bytes(
            encoding=serialization.Encoding.DER,
            format=serialization.PublicFormat.SubjectPublicKeyInfo,
        )
        return base64.b64encode(der).decode()

    def imzala(self, metin: bytes) -> str:
        imza = self._gizli.sign(metin, ec.ECDSA(hashes.SHA256()))
        return base64.b64encode(imza).decode()

@pytest.fixture
def saat():
    return SahteSaat()

@pytest.fixture
def oturumlar():
    s = SahteOturumSaglayici()
    s.baglandi(OYUNCU, "TestOyuncu", "oturum-AAA", game_ip="203.0.113.10")
    return s

@pytest.fixture
def ayarlar():
    return Ayarlar()

@pytest.fixture
def servis(saat, oturumlar, ayarlar):
    return VerificationServisi(Depo(), oturumlar, saat, ayarlar)

@pytest.fixture
def cihaz():
    return SahteCihaz()

def claim_et(servis, cihaz, scanner_ip="203.0.113.10", token=None,
             scanner_surumu=SCANNER_SURUMU, ruleset_surumu=RULESET_SURUMU):
    if token is None:
        _, token = servis.olustur(OYUNCU)
    return servis.claim(token, cihaz.public_key, "TPM",
                        scanner_surumu, ruleset_surumu, scanner_ip)

def heartbeat_at(servis, cihaz, vid, nonce, sira_no=1, asama="filesystem"):
    zaman = servis.saat.simdi().isoformat()
    metin = kripto.imza_metni("heartbeat", verification_id=vid, nonce=nonce,
                              sequence=sira_no, stage=asama, timestamp=zaman)
    return servis.heartbeat(vid, nonce, cihaz.imzala(metin), sira_no, asama, 10, zaman)

def rapor_gonder(servis, cihaz, vid, nonce, sonuc="NO_KNOWN_ARTIFACTS",
                 forensic="GOOD", runtime="GOOD", govde='{"kanit":[]}',
                 kanitlar=None, bozuk_hash=None, scanner_surumu=SCANNER_SURUMU,
                 ruleset_surumu=RULESET_SURUMU):
    rh = bozuk_hash or kripto.rapor_hash(govde)
    zaman = servis.saat.simdi().isoformat()
    metin = kripto.imza_metni("report", verification_id=vid, nonce=nonce,
                              scanner_version=scanner_surumu,
                              ruleset_version=ruleset_surumu,
                              report_hash=rh, timestamp=zaman)
    return servis.rapor_tamamla(
        vid, nonce, cihaz.imzala(metin), scanner_surumu, ruleset_surumu,
        sonuc, rh, govde, forensic, runtime, kanitlar or [], zaman,
    )
