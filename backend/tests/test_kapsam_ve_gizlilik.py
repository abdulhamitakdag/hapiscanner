import pytest
from datetime import timedelta

from conftest import OYUNCU, claim_et, heartbeat_at, rapor_gonder
from hapiverify.domain import TaramaSonucu
from hapiverify.servis import ServisHatasi

def _rapor(servis, cihaz, **kw):
    cevap = claim_et(servis, cihaz)
    vid = cevap["verification_id"]
    hb = heartbeat_at(servis, cihaz, vid, cevap["nonce"])
    return rapor_gonder(servis, cihaz, vid, hb["nonce"], **kw)

def test_A_kapsam_iyiyse_bilinen_artifact_yok(servis, cihaz):
    rapor = _rapor(servis, cihaz, forensic="GOOD", runtime="GOOD")
    assert rapor.overall_result == TaramaSonucu.BILINEN_ARTIFACT_YOK

def test_E_kapsam_sinirliysa_temiz_sonucu_verilmez(servis, cihaz):
    rapor = _rapor(servis, cihaz, forensic="LIMITED", runtime="GOOD")
    assert rapor.overall_result == TaramaSonucu.GECMIS_KAPSAMI_SINIRLI
    assert rapor.overall_result != TaramaSonucu.BILINEN_ARTIFACT_YOK

def test_V_adli_kapsam_yoksa_sonucsuz(servis, cihaz):
    rapor = _rapor(servis, cihaz, forensic="UNAVAILABLE", runtime="GOOD")
    assert rapor.overall_result == TaramaSonucu.SONUCSUZ

def test_W_runtime_kapsami_yoksa_sonucsuz(servis, cihaz):
    rapor = _rapor(servis, cihaz, forensic="GOOD", runtime="UNAVAILABLE")
    assert rapor.overall_result == TaramaSonucu.SONUCSUZ

def test_kapsam_dusukken_isaretlenmis_sonuc_korunur(servis, cihaz):

    rapor = _rapor(servis, cihaz, sonuc="FLAGGED", forensic="LIMITED",
                   kanitlar=[{"rule_id": "CHEAT_0127", "evidence_type": "SHA256",
                              "confidence": "HIGH", "severity": "HIGH",
                              "currently_present": True}])
    assert rapor.overall_result == TaramaSonucu.ISARETLENDI
    assert len(rapor.kanitlar) == 1

def test_gecersiz_sonuc_degeri_reddedilir(servis, cihaz):
    with pytest.raises(ServisHatasi) as hata:
        _rapor(servis, cihaz, sonuc="CLEAN")
    assert hata.value.kod == "INVALID_RESULT"

def test_gecersiz_kapsam_degeri_reddedilir(servis, cihaz):
    with pytest.raises(ServisHatasi) as hata:
        _rapor(servis, cihaz, forensic="MUKEMMEL")
    assert hata.value.kod == "INVALID_COVERAGE"

def test_kanit_kaydinda_dosya_yolu_alani_yok(servis, cihaz):
    rapor = _rapor(servis, cihaz, sonuc="FLAGGED",
                   kanitlar=[{"rule_id": "CHEAT_0127", "evidence_type": "SHA256"}])
    kanit = rapor.kanitlar[0]
    alanlar = set(kanit.__dict__.keys())

    for yasakli in ("path", "file_path", "filename", "content", "full_path"):
        assert yasakli not in alanlar

def test_ip_saklama_suresi_sonunda_bosaltilir(servis, cihaz, saat, ayarlar):
    cevap = claim_et(servis, cihaz, scanner_ip="198.51.100.7")
    vid = cevap["verification_id"]
    v = servis.depo.verification_al(vid)
    assert v.game_ip and v.scanner_ip
    onceki_durum = v.ag_durumu

    saat.ilerle(ayarlar.ip_saklama_gunu * 86400 + 1)
    assert servis.ip_temizligi() == 1

    assert v.game_ip is None and v.scanner_ip is None

    assert v.ag_durumu == onceki_durum

def test_ip_suresi_gelmeden_silinmez(servis, cihaz, saat):
    cevap = claim_et(servis, cihaz, scanner_ip="198.51.100.7")
    saat.ilerle(86400)
    assert servis.ip_temizligi() == 0
    v = servis.depo.verification_al(cevap["verification_id"])
    assert v.scanner_ip is not None

def test_denetim_kaydinda_token_yok(servis, cihaz):
    _, token = servis.olustur(OYUNCU)
    claim_et(servis, cihaz, token=token)
    for kayit in servis.depo.denetim:
        assert token not in kayit.ayrinti
        assert token not in str(kayit.__dict__)

def test_kanit_zaman_penceresi_saklanir(servis, cihaz):
    rapor = _rapor(servis, cihaz, kanitlar=[{
        "rule_id": "CHEAT_0127", "evidence_type": "HISTORICAL_USN",
        "currently_present": False,
        "first_seen": "2026-08-01T10:00:00Z", "last_seen": "2026-09-15T22:30:00Z",
    }])
    kanit = rapor.kanitlar[0]
    assert kanit.first_seen is not None and kanit.first_seen.year == 2026
    assert kanit.last_seen is not None and kanit.last_seen.month == 9
    assert kanit.currently_present is False

def test_bozuk_zaman_raporu_reddetmez(servis, cihaz):

    rapor = _rapor(servis, cihaz, kanitlar=[{
        "rule_id": "CHEAT_0127", "evidence_type": "HISTORICAL_USN",
        "first_seen": "dun", "last_seen": "",
    }])
    assert rapor.kanitlar[0].first_seen is None
    assert rapor.kanitlar[0].last_seen is None

def test_kanitta_yol_alani_kabul_edilmez(servis, cihaz):

    rapor = _rapor(servis, cihaz, kanitlar=[{
        "rule_id": "CHEAT_0127", "evidence_type": "FILE_PRESENT",
        "file_path": r"C:\Users\oyuncu\Downloads\hile.jar",
    }])
    assert not hasattr(rapor.kanitlar[0], "file_path")
    assert "hile.jar" not in str(rapor.kanitlar[0].__dict__)

def _kanitli_rapor(servis, cihaz):

    rapor = _rapor(servis, cihaz, sonuc="FLAGGED", forensic="GOOD",
                   kanitlar=[{"rule_id": "CHEAT_0127", "evidence_type": "SHA256",
                              "confidence": "HIGH", "severity": "HIGH",
                              "currently_present": True}])
    return rapor.verification_id

def _rapor_yasla(servis, gun):

    for rapor in servis.depo.raporlar.values():
        rapor.created_at -= timedelta(days=gun)

def test_rapor_bir_ay_sonra_ozete_iner(servis, cihaz):
    v = _kanitli_rapor(servis, cihaz)
    rapor = servis.depo.verification_raporu(v)
    assert rapor.kanitlar, "test kurulumu kanit uretmeliydi"

    _rapor_yasla(servis, 31)
    sonuc = servis.bakim()

    assert sonuc["ozete_inen"] == 1
    rapor = servis.depo.verification_raporu(v)
    assert rapor is not None
    assert rapor.ozete_indirildi
    assert rapor.kanitlar == []

    assert rapor.signature == ""
    assert rapor.report_hash == ""

    assert rapor.overall_result is not None
    assert rapor.forensic_coverage is not None

def test_ozet_de_sonunda_silinir(servis, cihaz):
    v = _kanitli_rapor(servis, cihaz)
    _rapor_yasla(servis, 366)

    sonuc = servis.bakim()

    assert sonuc["silinen"] == 1
    assert servis.depo.verification_raporu(v) is None

def test_bir_ay_dolmadan_rapor_tam_kalir(servis, cihaz):
    v = _kanitli_rapor(servis, cihaz)
    _rapor_yasla(servis, 29)

    servis.bakim()

    rapor = servis.depo.verification_raporu(v)
    assert not rapor.ozete_indirildi
    assert rapor.kanitlar
    assert rapor.signature

def test_ozete_inmis_rapor_tekrar_islenmez(servis, cihaz):
    _kanitli_rapor(servis, cihaz)
    _rapor_yasla(servis, 31)

    assert servis.bakim()["ozete_inen"] == 1
    assert servis.bakim()["ozete_inen"] == 0

def test_saklama_suresi_tutarsizsa_hata(servis):
    with pytest.raises(ValueError):
        servis.depo.rapor_temizligi(servis.saat.simdi(), tam_gun=400, ozet_gun=30)
