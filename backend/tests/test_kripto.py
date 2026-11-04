import pytest

from hapiverify import kripto
from hapiverify.config import Ayarlar

def test_token_formati():
    token = kripto.token_uret()
    assert token.startswith("HAPI-")
    parcalar = token.split("-")
    assert len(parcalar) == 3
    assert all(len(p) == 4 for p in parcalar[1:])

def test_token_karistirilabilir_harf_icermez():

    for _ in range(200):
        govde = kripto.token_uret().replace("HAPI-", "")
        assert not set(govde) & set("IO01")

def test_token_tekrar_etmiyor():
    uretilenler = {kripto.token_uret() for _ in range(2000)}
    assert len(uretilenler) == 2000

def test_token_normalize_kucuk_harf_ve_bosluk():
    assert kripto.token_dogrula(" hapi-abcd-efgh ",
                                kripto.token_hashle("HAPI-ABCD-EFGH"))

def test_token_hash_duz_metni_aciga_cikarmaz():
    token = kripto.token_uret()
    h = kripto.token_hashle(token)
    assert token not in h
    assert len(h) == 64

def test_yanlis_token_dogrulanmaz():
    assert not kripto.token_dogrula("HAPI-AAAA-AAAA",
                                    kripto.token_hashle("HAPI-BBBB-BBBB"))

def test_nonce_256_bit():
    nonce = kripto.nonce_uret()
    assert len(nonce) == 64
    assert len({kripto.nonce_uret() for _ in range(1000)}) == 1000

def test_imza_metni_alan_sirasindan_bagimsiz():
    a = kripto.imza_metni("report", b=2, a=1)
    b = kripto.imza_metni("report", a=1, b=2)
    assert a == b

def test_imza_metni_baglami_ayirir():
    assert kripto.imza_metni("heartbeat", a=1) != kripto.imza_metni("report", a=1)

@pytest.mark.parametrize("surum,asgari,beklenen", [
    ("0.0.9", "0.1.0", True),
    ("0.1.0", "0.1.0", False),
    ("0.1.1", "0.1.0", False),
    ("1.0.0", "0.9.9", False),
    ("0.2", "0.10.0", True),
    ("2026.10.01.1", "2026.11.03.1", True),
    ("2026.11.03.1", "2026.11.03.1", False),
    ("2026.11.04.1", "2026.11.03.1", False),
    ("0.1.0-rc1", "0.1.0", False),
])
def test_surum_karsilastirma(surum, asgari, beklenen):
    assert kripto.surum_kucuk_mu(surum, asgari) is beklenen

def test_parmak_izi_kararli():
    import base64
    anahtar = base64.b64encode(b"x" * 32).decode()
    assert kripto.parmak_izi(anahtar) == kripto.parmak_izi(anahtar)
    assert len(kripto.parmak_izi(anahtar)) == 32

def test_p256_imza_dogrulanir():
    from conftest import SahteCihaz
    cihaz = SahteCihaz()
    metin = kripto.imza_metni("report", a=1)
    assert kripto.imza_dogrula(cihaz.public_key, cihaz.imzala(metin), metin)

def test_metin_degisirse_imza_tutmaz():
    from conftest import SahteCihaz
    cihaz = SahteCihaz()
    imza = cihaz.imzala(kripto.imza_metni("report", a=1))
    assert not kripto.imza_dogrula(cihaz.public_key, imza,
                                   kripto.imza_metni("report", a=2))

def test_yanlis_egri_reddedilir():

    import base64
    from cryptography.hazmat.primitives import hashes, serialization
    from cryptography.hazmat.primitives.asymmetric import ec
    gizli = ec.generate_private_key(ec.SECP384R1())
    pub = base64.b64encode(gizli.public_key().public_bytes(
        serialization.Encoding.DER,
        serialization.PublicFormat.SubjectPublicKeyInfo)).decode()
    metin = kripto.imza_metni("report", a=1)
    imza = base64.b64encode(gizli.sign(metin, ec.ECDSA(hashes.SHA256()))).decode()
    assert kripto.imza_dogrula(pub, imza, metin) is False

def test_ed25519_anahtar_reddedilir():

    import base64
    from cryptography.hazmat.primitives import serialization
    from cryptography.hazmat.primitives.asymmetric.ed25519 import Ed25519PrivateKey
    gizli = Ed25519PrivateKey.generate()
    pub = base64.b64encode(gizli.public_key().public_bytes(
        serialization.Encoding.DER,
        serialization.PublicFormat.SubjectPublicKeyInfo)).decode()
    metin = kripto.imza_metni("report", a=1)
    imza = base64.b64encode(gizli.sign(metin)).decode()
    assert kripto.imza_dogrula(pub, imza, metin) is False

def test_rsa_anahtar_reddedilir():
    import base64
    from cryptography.hazmat.primitives import hashes, serialization
    from cryptography.hazmat.primitives.asymmetric import padding, rsa
    gizli = rsa.generate_private_key(public_exponent=65537, key_size=2048)
    pub = base64.b64encode(gizli.public_key().public_bytes(
        serialization.Encoding.DER,
        serialization.PublicFormat.SubjectPublicKeyInfo)).decode()
    metin = kripto.imza_metni("report", a=1)
    imza = base64.b64encode(gizli.sign(metin, padding.PKCS1v15(), hashes.SHA256())).decode()
    assert kripto.imza_dogrula(pub, imza, metin) is False

def test_bozuk_imza_cokmeye_yol_acmaz():

    for kotu in ["", "!!!", "YWJj", "x" * 500]:
        assert kripto.imza_dogrula(kotu, kotu, b"metin") is False

def test_heartbeat_toleransi_araliktan_buyuk_olmali():
    with pytest.raises(ValueError):
        Ayarlar(heartbeat_saniye=30, heartbeat_tolerans_saniye=5)
