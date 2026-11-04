\
\
\
\

import pytest
from fastapi.testclient import TestClient

from hapiverify.api import uygulama_olustur
from hapiverify.config import Ayarlar
from hapiverify.depo import Depo
from hapiverify.oturum import BildirilenOturumSaglayici
from hapiverify.servis import ServisHatasi, VerificationServisi

from conftest import OYUNCU

JETON = "test-jetonu"

@pytest.fixture
def besleme(saat):
    return BildirilenOturumSaglayici(saat, tazelik_saniye=90)

@pytest.fixture
def servis_beslemeli(saat, besleme, ayarlar):
    return VerificationServisi(Depo(), besleme, saat, ayarlar)

@pytest.fixture
def istemci(servis_beslemeli, ayarlar):
    app = uygulama_olustur(servis_beslemeli, ayarlar, plugin_jetonu=JETON)
    return TestClient(app, headers={"X-Plugin-Token": JETON})

def katil(besleme, oturum_id="oturum-AAA", ip="203.0.113.10"):
    return besleme.katildi(OYUNCU, "TestOyuncu", oturum_id, ip)

def test_katilan_oyuncu_gorunur(besleme):
    katil(besleme)
    o = besleme.oturum(OYUNCU)
    assert o.username == "TestOyuncu" and o.bagli and o.game_ip == "203.0.113.10"

def test_ayrilan_oyuncu_silinir(besleme):
    katil(besleme)
    assert besleme.ayrildi(OYUNCU, "oturum-AAA") is True
    assert besleme.oturum(OYUNCU) is None

def test_gec_gelen_eski_cikis_yeni_oturumu_silmez(besleme):

    katil(besleme, "oturum-AAA")
    katil(besleme, "oturum-BBB")
    assert besleme.ayrildi(OYUNCU, "oturum-AAA") is False
    assert besleme.oturum(OYUNCU).game_session_id == "oturum-BBB"

def test_anlik_goruntu_listede_olmayani_dusurur(besleme):
    katil(besleme)
    besleme.anlik_goruntu([{"minecraft_uuid": "baska", "username": "Biri",
                            "game_session_id": "oturum-CCC"}])
    assert besleme.oturum(OYUNCU) is None
    assert besleme.cevrimici_sayisi() == 1

def test_besleme_bayatlar(saat, besleme):
    assert besleme.beslenme_taze_mi() is False
    katil(besleme)
    assert besleme.beslenme_taze_mi() is True
    saat.ilerle(91)
    assert besleme.beslenme_taze_mi() is False

def test_bayat_beslemede_dogrulama_acilmaz(saat, besleme, servis_beslemeli):
    katil(besleme)
    saat.ilerle(91)
    with pytest.raises(ServisHatasi) as hata:
        servis_beslemeli.olustur(OYUNCU)
    assert hata.value.kod == "SESSION_FEED_STALE"

def test_bayat_besleme_acik_dogrulamayi_dusurmez(saat, besleme, servis_beslemeli, cihaz):

    katil(besleme)
    _, token = servis_beslemeli.olustur(OYUNCU)
    sonuc = servis_beslemeli.claim(token, cihaz.public_key, "TPM",
                                   "0.1.0", "2026.11.03.1", "203.0.113.10")
    saat.ilerle(120)
    v = servis_beslemeli.depo.verification_al(sonuc["verification_id"])
    assert servis_beslemeli._surekliligi_denetle(v) is False
    assert v.status.value == "IN_PROGRESS"

def test_gercek_ayrilis_dogrulamayi_keser(besleme, servis_beslemeli, cihaz):
    katil(besleme)
    _, token = servis_beslemeli.olustur(OYUNCU)
    sonuc = servis_beslemeli.claim(token, cihaz.public_key, "TPM",
                                   "0.1.0", "2026.11.03.1", "203.0.113.10")
    besleme.ayrildi(OYUNCU, "oturum-AAA")
    v = servis_beslemeli.depo.verification_al(sonuc["verification_id"])
    assert servis_beslemeli._surekliligi_denetle(v) is True
    assert v.status.value == "SESSION_INTERRUPTED"

def test_api_besleme_ve_dogrulama_akisi(istemci):
    cevap = istemci.post("/api/v1/plugin/sessions", json={
        "minecraft_uuid": OYUNCU, "username": "TestOyuncu",
        "game_session_id": "oturum-AAA", "game_ip": "203.0.113.10"})
    assert cevap.status_code == 200

    cevap = istemci.post("/api/v1/plugin/verifications",
                         json={"minecraft_uuid": OYUNCU})
    assert cevap.status_code == 200 and cevap.json()["token"]

def test_api_besleme_jetonsuz_kabul_etmez(istemci):
    cevap = istemci.put("/api/v1/plugin/sessions", json={"sessions": []},
                        headers={"X-Plugin-Token": "yanlis"})
    assert cevap.status_code == 401

def test_api_anlik_goruntu_sayi_doner(istemci):
    cevap = istemci.put("/api/v1/plugin/sessions", json={"sessions": [
        {"minecraft_uuid": OYUNCU, "username": "TestOyuncu",
         "game_session_id": "oturum-AAA"},
        {"minecraft_uuid": "22222222-3333-4444-5555-666666666666",
         "username": "Ikinci", "game_session_id": "oturum-BBB"},
    ]})
    assert cevap.json() == {"online": 2}

def test_api_cikis_oturum_kimligi_ile(istemci):
    istemci.post("/api/v1/plugin/sessions", json={
        "minecraft_uuid": OYUNCU, "username": "TestOyuncu",
        "game_session_id": "oturum-AAA"})
    cevap = istemci.delete(
        f"/api/v1/plugin/sessions/{OYUNCU}?game_session_id=oturum-ESKI")
    assert cevap.json() == {"closed": False}
    cevap = istemci.delete(
        f"/api/v1/plugin/sessions/{OYUNCU}?game_session_id=oturum-AAA")
    assert cevap.json() == {"closed": True}

def test_sahte_saglayici_besleme_kabul_etmez(servis, ayarlar):

    app = uygulama_olustur(servis, ayarlar, plugin_jetonu=JETON)
    istemci = TestClient(app, headers={"X-Plugin-Token": JETON})
    cevap = istemci.put("/api/v1/plugin/sessions", json={"sessions": []})
    assert cevap.status_code == 501
    assert cevap.json()["error"] == "SESSION_FEED_UNSUPPORTED"

def test_uygulama_gercek_saglayici_ile_kuruluyor():

    from hapiverify import app as giris
    assert isinstance(giris.oturumlar, BildirilenOturumSaglayici)
    assert giris.servis.oturumlar is giris.oturumlar

def test_tazelik_anlik_goruntu_araligindan_buyuk():

    assert Ayarlar().oturum_tazelik_saniye >= 3 * 30
