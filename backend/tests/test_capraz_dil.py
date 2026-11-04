\
\
\
\
\

import json
import os

import pytest

from hapiverify import kripto

VEKTOR_YOLU = os.path.join(
    os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
    "scanner", "test-vectors", "imza_vektoru.json",
)

def vektor():
    if not os.path.exists(VEKTOR_YOLU):
        pytest.skip("imza vektoru bulunamadi, C# tarafinda uretilmeli")
    with open(VEKTOR_YOLU, encoding="utf-8") as f:
        return json.load(f)

def test_vektor_p256_bildiriyor():
    assert "ECDSA P-256" in vektor()["algorithm"]

def test_csharp_imzalari_backend_tarafinda_dogrulanir():
    v = vektor()
    assert v["cases"], "vektor bos"
    for durum in v["cases"]:
        metin = durum["payload"].encode()
        assert kripto.imza_dogrula(v["public_key"], durum["signature"], metin),\
            f"{durum['name']} imzasi dogrulanamadi"

def test_payload_backendin_urettigiyle_ayni():
    v = vektor()
    beklenenler = {d["name"]: d["payload"] for d in v["cases"]}

    hb = kripto.imza_metni(
        "heartbeat",
        verification_id="7f3a9c1e-0000-4000-8000-000000000001",
        nonce="a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f90",
        sequence=1, stage="filesystem",
        timestamp="2026-11-03T12:00:00.000000+00:00",
    ).decode()
    assert hb == beklenenler["heartbeat"]

    rp = kripto.imza_metni(
        "report",
        verification_id="7f3a9c1e-0000-4000-8000-000000000001",
        nonce="0f1e2d3c4b5a69788796a5b4c3d2e1f00f1e2d3c4b5a69788796a5b4c3d2e1f0",
        scanner_version=v["scanner_version"], ruleset_version="2026.11.03.1",
        report_hash=v["report_hash"],
        timestamp="2026-11-03T12:00:05.000000+00:00",
    ).decode()
    assert rp == beklenenler["report"]

def test_rapor_hash_hesabi_ayni():
    v = vektor()
    assert kripto.rapor_hash(v["report_body"]) == v["report_hash"]

def test_parmak_izi_hesabi_ayni():
    v = vektor()
    assert kripto.parmak_izi(v["public_key"]) == v["fingerprint"]

def test_payload_degisirse_imza_tutmaz():
    v = vektor()
    durum = v["cases"][0]
    bozuk = durum["payload"].replace("sequence=1", "sequence=2").encode()
    assert not kripto.imza_dogrula(v["public_key"], durum["signature"], bozuk)

def test_baglam_degisirse_imza_tutmaz():

    v = vektor()
    hb = next(d for d in v["cases"] if d["name"] == "heartbeat")
    bozuk = hb["payload"].replace("heartbeat|", "report|", 1).encode()
    assert not kripto.imza_dogrula(v["public_key"], hb["signature"], bozuk)

PAKET_VEKTOR_YOLU = os.path.join(
    os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
    "scanner", "test-vectors", "kural_paketi_vektoru.json",
)

def paket_vektoru():
    if not os.path.exists(PAKET_VEKTOR_YOLU):
        pytest.skip("kural paketi vektoru bulunamadi")
    with open(PAKET_VEKTOR_YOLU, encoding="utf-8") as f:
        return json.load(f)

def test_paket_ozeti_govdeyle_tutuyor():
    import hashlib
    v = paket_vektoru()
    ozet = hashlib.sha256(v["package_json"].encode()).hexdigest()
    assert ozet == v["package_sha256"]

def test_paket_imzasi_yayinci_anahtariyla_dogrulanir():
    import base64
    from cryptography.hazmat.primitives import hashes, serialization
    from cryptography.hazmat.primitives.asymmetric import ec

    v = paket_vektoru()
    anahtar = serialization.load_der_public_key(
        base64.b64decode(v["publisher_public_key"]))

    metin = kripto.imza_metni(
        "ruleset",
        package_sha256=v["package_sha256"],
        ruleset_version=v["ruleset_version"],
    )
    anahtar.verify(base64.b64decode(v["signature"]), metin, ec.ECDSA(hashes.SHA256()))

def test_imza_metni_araciyla_ayni():
    import importlib.util
    yol = os.path.join(
        os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
        "tools", "kural_imzala.py",
    )
    spec = importlib.util.spec_from_file_location("kural_imzala", yol)
    arac = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(arac)

    v = paket_vektoru()
    assert arac.imza_metni(v["package_sha256"], v["ruleset_version"]) == kripto.imza_metni(
        "ruleset",
        package_sha256=v["package_sha256"],
        ruleset_version=v["ruleset_version"],
    )
