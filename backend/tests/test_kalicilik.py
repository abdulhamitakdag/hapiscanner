\
\
\
\
\

import os
import time

import pytest

from conftest import OYUNCU, RULESET_SURUMU, SCANNER_SURUMU, heartbeat_at, rapor_gonder
from hapiverify.config import Ayarlar
from hapiverify.depo import SahteSaat
from hapiverify.kalici import KaliciDepo, KaliciNonceDeposu, baglanti_ac
from hapiverify.oturum import SahteOturumSaglayici
from hapiverify.servis import ServisHatasi, VerificationServisi

@pytest.fixture
def db_yolu(tmp_path):
    return str(tmp_path / "hapiverify.db")

def kur(db_yolu, saat=None, oturumlar=None):

    saat = saat or SahteSaat()
    if oturumlar is None:
        oturumlar = SahteOturumSaglayici()
        oturumlar.baglandi(OYUNCU, "TestOyuncu", "oturum-AAA", game_ip="203.0.113.10")
    baglanti = baglanti_ac(db_yolu)
    depo = KaliciDepo(baglanti)
    servis = VerificationServisi(depo, oturumlar, saat, Ayarlar(),
                                 nonce_deposu=KaliciNonceDeposu(saat, baglanti))
    return servis, depo, saat, oturumlar

def test_bekleyen_dogrulama_yeniden_baslatmayi_atlatir(db_yolu, cihaz):
    servis, depo, saat, oturumlar = kur(db_yolu)
    _, token = servis.olustur(OYUNCU)
    depo.yaz()

    servis2, _, _, _ = kur(db_yolu, SahteSaat(), oturumlar)
    sonuc = servis2.claim(token, cihaz.public_key, "TPM",
                          SCANNER_SURUMU, RULESET_SURUMU, "203.0.113.10")
    assert sonuc["username"] == "TestOyuncu"

def test_nonce_zinciri_yeniden_baslatmayi_atlatir(db_yolu, cihaz):

    servis, depo, saat, oturumlar = kur(db_yolu)
    _, token = servis.olustur(OYUNCU)
    sonuc = servis.claim(token, cihaz.public_key, "TPM",
                         SCANNER_SURUMU, RULESET_SURUMU, "203.0.113.10")
    depo.yaz()

    servis2, _, _, _ = kur(db_yolu, saat, oturumlar)
    cevap = heartbeat_at(servis2, cihaz, sonuc["verification_id"], sonuc["nonce"])
    assert cevap["nonce"]

def test_harcanmis_nonce_yeniden_baslatmadan_sonra_da_reddedilir(db_yolu, cihaz):

    servis, depo, saat, oturumlar = kur(db_yolu)
    _, token = servis.olustur(OYUNCU)
    sonuc = servis.claim(token, cihaz.public_key, "TPM",
                         SCANNER_SURUMU, RULESET_SURUMU, "203.0.113.10")
    heartbeat_at(servis, cihaz, sonuc["verification_id"], sonuc["nonce"])
    depo.yaz()

    servis2, _, _, _ = kur(db_yolu, saat, oturumlar)
    with pytest.raises(ServisHatasi) as hata:
        heartbeat_at(servis2, cihaz, sonuc["verification_id"], sonuc["nonce"], sira_no=2)
    assert hata.value.kod == "INVALID_NONCE"

def test_rapor_ve_kanitlar_kalici(db_yolu, cihaz):
    servis, depo, saat, oturumlar = kur(db_yolu)
    _, token = servis.olustur(OYUNCU)
    sonuc = servis.claim(token, cihaz.public_key, "TPM",
                         SCANNER_SURUMU, RULESET_SURUMU, "203.0.113.10")
    cevap = heartbeat_at(servis, cihaz, sonuc["verification_id"], sonuc["nonce"])
    rapor_gonder(servis, cihaz, sonuc["verification_id"], cevap["nonce"],
                 sonuc="FLAGGED", kanitlar=[{
                     "rule_id": "k1", "rule_version": "1", "evidence_type": "FILE_HASH",
                     "artifact_category": "CLIENT", "confidence": "HIGH",
                     "severity": "HIGH", "currently_present": True,
                     "first_seen": None, "last_seen": None}])
    depo.yaz()

    _, depo2, _, _ = kur(db_yolu, saat, oturumlar)
    rapor = depo2.verification_raporu(sonuc["verification_id"])
    assert rapor.overall_result.value == "FLAGGED"
    assert len(rapor.kanitlar) == 1
    assert rapor.kanitlar[0].rule_id == "k1"

def test_cihaz_gecmisi_kalici(db_yolu, cihaz):

    servis, depo, saat, oturumlar = kur(db_yolu)
    _, token = servis.olustur(OYUNCU)
    servis.claim(token, cihaz.public_key, "TPM",
                 SCANNER_SURUMU, RULESET_SURUMU, "203.0.113.10")
    depo.yaz()

    servis2, depo2, _, _ = kur(db_yolu, saat, oturumlar)
    assert len(depo2.oyuncunun_cihazlari(OYUNCU)) == 1

    servis2.iptal(OYUNCU)
    _, token2 = servis2.olustur(OYUNCU)
    sonuc = servis2.claim(token2, cihaz.public_key, "TPM",
                          SCANNER_SURUMU, RULESET_SURUMU, "203.0.113.10")
    assert sonuc["new_device"] is False

def test_denetim_kaydi_kalici_ve_tekrarlanmaz(db_yolu):
    servis, depo, saat, oturumlar = kur(db_yolu)
    servis.olustur(OYUNCU)
    depo.yaz()
    depo.yaz()

    _, depo2, _, _ = kur(db_yolu, saat, oturumlar)
    satirlar = depo2._db.execute(
        "SELECT olay FROM audit WHERE olay = 'verification started'").fetchall()
    assert len(satirlar) == 1

def test_degismeyen_durumda_yazma_yapilmaz(db_yolu):
    servis, depo, _, _ = kur(db_yolu)
    servis.olustur(OYUNCU)
    assert depo.yaz() > 0
    assert depo.yaz() == 0

def test_ozete_inen_raporun_kanitlari_diskten_de_silinir(db_yolu, cihaz):

    servis, depo, saat, oturumlar = kur(db_yolu)
    _, token = servis.olustur(OYUNCU)
    sonuc = servis.claim(token, cihaz.public_key, "TPM",
                         SCANNER_SURUMU, RULESET_SURUMU, "203.0.113.10")
    cevap = heartbeat_at(servis, cihaz, sonuc["verification_id"], sonuc["nonce"])
    rapor = rapor_gonder(servis, cihaz, sonuc["verification_id"], cevap["nonce"],
                         kanitlar=[{"rule_id": "k1", "rule_version": "1",
                                    "evidence_type": "FILE_HASH",
                                    "artifact_category": "CLIENT",
                                    "confidence": "HIGH", "severity": "HIGH",
                                    "currently_present": True,
                                    "first_seen": None, "last_seen": None}])
    depo.yaz()

    saat.ilerle(31 * 24 * 3600)
    depo.rapor_temizligi(saat.simdi(), 30, 365)
    depo.yaz()

    _, depo2, _, _ = kur(db_yolu, saat, oturumlar)
    assert depo2.raporlar[rapor.report_id].ozete_indirildi is True
    assert depo2.raporlar[rapor.report_id].kanitlar == []
    kalan = depo2._db.execute("SELECT COUNT(*) c FROM evidence").fetchone()["c"]
    assert kalan == 0

def test_silinen_rapor_diskten_gider(db_yolu, cihaz):
    servis, depo, saat, oturumlar = kur(db_yolu)
    _, token = servis.olustur(OYUNCU)
    sonuc = servis.claim(token, cihaz.public_key, "TPM",
                         SCANNER_SURUMU, RULESET_SURUMU, "203.0.113.10")
    cevap = heartbeat_at(servis, cihaz, sonuc["verification_id"], sonuc["nonce"])
    rapor_gonder(servis, cihaz, sonuc["verification_id"], cevap["nonce"])
    depo.yaz()

    saat.ilerle(366 * 24 * 3600)
    depo.rapor_temizligi(saat.simdi(), 30, 365)
    depo.yaz()

    _, depo2, _, _ = kur(db_yolu, saat, oturumlar)
    assert depo2.raporlar == {}
    assert depo2._db.execute("SELECT COUNT(*) c FROM reports").fetchone()["c"] == 0

def test_ip_temizligi_diske_yansir(db_yolu):
    servis, depo, saat, oturumlar = kur(db_yolu)
    servis.olustur(OYUNCU)
    depo.yaz()

    saat.ilerle(8 * 24 * 3600)
    assert depo.ip_temizligi(saat.simdi()) == 1
    depo.yaz()

    _, depo2, _, _ = kur(db_yolu, saat, oturumlar)
    v = next(iter(depo2.verificationlar.values()))
    assert v.game_ip is None and v.scanner_ip is None

def test_acik_dogrulama_kaydi_kalici(db_yolu):

    servis, depo, saat, oturumlar = kur(db_yolu)
    servis.olustur(OYUNCU)
    depo.yaz()

    servis2, _, _, _ = kur(db_yolu, saat, oturumlar)
    with pytest.raises(ServisHatasi) as hata:
        servis2.olustur(OYUNCU)
    assert hata.value.kod == "ALREADY_PENDING"

def test_kapanan_dogrulama_acik_listeden_dusurulur(db_yolu):
    servis, depo, saat, oturumlar = kur(db_yolu)
    servis.olustur(OYUNCU)
    servis.iptal(OYUNCU)
    depo.yaz()

    servis2, _, _, _ = kur(db_yolu, saat, oturumlar)
    servis2.olustur(OYUNCU)

def test_zamanlar_utc_olarak_geri_gelir(db_yolu):
    servis, depo, saat, oturumlar = kur(db_yolu)
    v, _ = servis.olustur(OYUNCU)
    depo.yaz()

    _, depo2, _, _ = kur(db_yolu, saat, oturumlar)
    geri = depo2.verification_al(v.verification_id)
    assert geri.created_at == v.created_at
    assert geri.created_at.tzinfo is not None

def test_veritabani_dizini_yoksa_olusturulur(tmp_path):
    yol = str(tmp_path / "olmayan" / "alt" / "hapiverify.db")
    baglanti_ac(yol)
    assert os.path.exists(yol)

def test_istek_sonunda_otomatik_yazilir(db_yolu):

    from fastapi.testclient import TestClient

    from hapiverify.api import uygulama_olustur

    servis, _, saat, oturumlar = kur(db_yolu)
    istemci = TestClient(uygulama_olustur(servis, Ayarlar(), plugin_jetonu="j"),
                         headers={"X-Plugin-Token": "j"})
    cevap = istemci.post("/api/v1/plugin/verifications",
                         json={"minecraft_uuid": OYUNCU})
    assert cevap.status_code == 200

    _, depo2, _, _ = kur(db_yolu, saat, oturumlar)
    assert len(depo2.verificationlar) == 1

def test_bakim_dongusu_servisle_birlikte_calisir(db_yolu):
\

    from dataclasses import replace

    from fastapi.testclient import TestClient

    from hapiverify.api import uygulama_olustur

    servis, depo, saat, _ = kur(db_yolu)
    servis.olustur(OYUNCU)
    saat.ilerle(8 * 24 * 3600)

    ayarlar = replace(Ayarlar(), bakim_araligi_saniye=1)
    with TestClient(uygulama_olustur(servis, ayarlar, plugin_jetonu="j")) as istemci:
        istemci.get("/health")
        for _ in range(30):
            v = next(iter(depo.verificationlar.values()))
            if v.game_ip is None:
                break
            time.sleep(0.1)

    assert next(iter(depo.verificationlar.values())).game_ip is None

def test_bakim_kapatilabilir(db_yolu):
    from dataclasses import replace

    from fastapi.testclient import TestClient

    from hapiverify.api import uygulama_olustur

    servis, depo, saat, _ = kur(db_yolu)
    servis.olustur(OYUNCU)
    saat.ilerle(8 * 24 * 3600)

    ayarlar = replace(Ayarlar(), bakim_araligi_saniye=0)
    with TestClient(uygulama_olustur(servis, ayarlar, plugin_jetonu="j")) as istemci:
        istemci.get("/health")
        time.sleep(0.3)

    assert next(iter(depo.verificationlar.values())).game_ip is not None
