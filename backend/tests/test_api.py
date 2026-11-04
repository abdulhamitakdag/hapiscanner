import pytest
from fastapi.testclient import TestClient

from conftest import OYUNCU, RULESET_SURUMU, SCANNER_SURUMU
from hapiverify import kripto
from hapiverify.api import uygulama_olustur

JETON = "test-plugin-jetonu"

@pytest.fixture
def istemci(servis, ayarlar):
    return TestClient(uygulama_olustur(servis, ayarlar, plugin_jetonu=JETON))

@pytest.fixture
def basliklar():
    return {"x-plugin-token": JETON}

def test_health_acik(istemci):
    assert istemci.get("/health").json() == {"status": "ok"}

def test_plugin_yuzeyi_jeton_istiyor(istemci):
    cevap = istemci.post("/api/v1/plugin/verifications",
                         json={"minecraft_uuid": OYUNCU})
    assert cevap.status_code == 401
    assert cevap.json()["error"] == "UNAUTHORIZED"

def test_yanlis_jeton_reddedilir(istemci):
    cevap = istemci.post("/api/v1/plugin/verifications",
                         json={"minecraft_uuid": OYUNCU},
                         headers={"x-plugin-token": "yanlis"})
    assert cevap.status_code == 401

def test_http_uzerinden_tam_akis(istemci, basliklar, cihaz, servis):
    olustur = istemci.post("/api/v1/plugin/verifications",
                           json={"minecraft_uuid": OYUNCU}, headers=basliklar)
    assert olustur.status_code == 200
    token = olustur.json()["token"]

    claim = istemci.post("/api/v1/scanner/claim", json={
        "token": token, "public_key": cihaz.public_key,
        "assurance_level": "TPM", "scanner_version": SCANNER_SURUMU,
        "ruleset_version": RULESET_SURUMU,
    })
    assert claim.status_code == 200
    vid, nonce = claim.json()["verification_id"], claim.json()["nonce"]

    zaman = servis.saat.simdi().isoformat()
    metin = kripto.imza_metni("heartbeat", verification_id=vid, nonce=nonce,
                              sequence=1, stage="filesystem", timestamp=zaman)
    hb = istemci.post("/api/v1/scanner/heartbeat", json={
        "verification_id": vid, "nonce": nonce, "signature": cihaz.imzala(metin),
        "sequence_number": 1, "scan_stage": "filesystem", "progress": 42,
        "timestamp": zaman,
    })
    assert hb.status_code == 200
    nonce2 = hb.json()["nonce"]

    govde = '{"kanit":[]}'
    rh = kripto.rapor_hash(govde)
    metin2 = kripto.imza_metni("report", verification_id=vid, nonce=nonce2,
                               scanner_version=SCANNER_SURUMU,
                               ruleset_version=RULESET_SURUMU,
                               report_hash=rh, timestamp=zaman)
    rapor = istemci.post("/api/v1/scanner/report", json={
        "verification_id": vid, "nonce": nonce2, "signature": cihaz.imzala(metin2),
        "scanner_version": SCANNER_SURUMU, "ruleset_version": RULESET_SURUMU,
        "overall_result": "NO_KNOWN_ARTIFACTS", "report_hash": rh,
        "report_body": govde, "forensic_coverage": "GOOD",
        "runtime_coverage": "GOOD", "evidence": [], "timestamp": zaman,
    })
    assert rapor.status_code == 200
    assert rapor.json()["accepted"] is True

    durum = istemci.get(f"/api/v1/plugin/verifications/{OYUNCU}", headers=basliklar)
    assert durum.json()["status"] == "COMPLETED"
    assert durum.json()["result"] == "NO_KNOWN_ARTIFACTS"

def test_sema_disi_alan_reddedilir(istemci):
    cevap = istemci.post("/api/v1/scanner/claim", json={
        "token": "HAPI-ABCD-EFGH", "public_key": "x" * 20,
        "assurance_level": "BELKI", "scanner_version": "0.1.0",
        "ruleset_version": RULESET_SURUMU,
    })
    assert cevap.status_code == 422

def test_eksik_alan_reddedilir(istemci):
    assert istemci.post("/api/v1/scanner/claim", json={"token": "x"}).status_code == 422

def test_asiri_uzun_token_reddedilir(istemci):
    cevap = istemci.post("/api/v1/scanner/claim", json={
        "token": "A" * 500, "public_key": "x" * 20, "assurance_level": "TPM",
        "scanner_version": "0.1.0", "ruleset_version": RULESET_SURUMU,
    })
    assert cevap.status_code == 422

def test_cok_buyuk_govde_reddedilir(istemci):
    cevap = istemci.post("/api/v1/scanner/report",
                         content=b"x" * (300 * 1024),
                         headers={"content-type": "application/json"})
    assert cevap.status_code == 413
    assert cevap.json()["error"] == "BODY_TOO_LARGE"

def test_asiri_kanit_sayisi_reddedilir(istemci, servis):
    kanitlar = [{"rule_id": f"R{i}", "evidence_type": "SHA256"} for i in range(600)]
    cevap = istemci.post("/api/v1/scanner/report", json={
        "verification_id": "x" * 20, "nonce": "y" * 64, "signature": "z" * 64,
        "scanner_version": SCANNER_SURUMU, "ruleset_version": RULESET_SURUMU,
        "overall_result": "FLAGGED", "report_hash": "a" * 64,
        "report_body": "{}", "forensic_coverage": "GOOD",
        "runtime_coverage": "GOOD", "evidence": kanitlar, "timestamp": "simdi",
    })
    assert cevap.status_code == 422

def test_hata_cevabi_ic_detay_sizdirmaz(istemci):
    cevap = istemci.post("/api/v1/scanner/claim", json={
        "token": "HAPI-ZZZZ-ZZZZ", "public_key": "x" * 20,
        "assurance_level": "TPM", "scanner_version": SCANNER_SURUMU,
        "ruleset_version": RULESET_SURUMU,
    })
    assert cevap.status_code == 401
    govde = cevap.json()
    assert set(govde.keys()) == {"error", "message"}
    assert "Traceback" not in str(govde)
    assert "hapiverify" not in str(govde)

def test_kanit_semasinda_yol_alani_yok(istemci):
    from hapiverify.api import KanitIstegi
    alanlar = set(KanitIstegi.model_fields.keys())
    for yasakli in ("path", "file_path", "filename", "content", "full_path"):
        assert yasakli not in alanlar

def test_x_forwarded_for_ilk_deger_alinir(istemci, basliklar, cihaz, servis):
    olustur = istemci.post("/api/v1/plugin/verifications",
                           json={"minecraft_uuid": OYUNCU}, headers=basliklar)
    token = olustur.json()["token"]
    claim = istemci.post("/api/v1/scanner/claim", json={
        "token": token, "public_key": cihaz.public_key, "assurance_level": "TPM",
        "scanner_version": SCANNER_SURUMU, "ruleset_version": RULESET_SURUMU,
    }, headers={"x-forwarded-for": "203.0.113.10, 10.0.0.1"})
    assert claim.json()["network_status"] == "NETWORK_MATCH"

def test_scanner_yuzeyi_jeton_istemiyor(istemci):

    cevap = istemci.post("/api/v1/scanner/claim", json={
        "token": "HAPI-ZZZZ-ZZZZ", "public_key": "x" * 20,
        "assurance_level": "TPM", "scanner_version": SCANNER_SURUMU,
        "ruleset_version": RULESET_SURUMU,
    })
    assert cevap.status_code != 401 or cevap.json()["error"] != "UNAUTHORIZED"
