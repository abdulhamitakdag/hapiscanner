import pytest

from conftest import OYUNCU, claim_et, heartbeat_at, rapor_gonder
from hapiverify.domain import AgDurumu, TaramaSonucu, VerificationDurumu
from hapiverify.servis import ServisHatasi

def test_olusturulan_token_formatli_ve_duz_metin_saklanmaz(servis):
    v, token = servis.olustur(OYUNCU)
    assert token.startswith("HAPI-")
    assert len(token.split("-")) == 3

    assert token not in str(v.__dict__)
    assert v.token_hash != token

def test_tam_akis_bastan_sona(servis, cihaz):
    cevap = claim_et(servis, cihaz)
    vid = cevap["verification_id"]
    hb = heartbeat_at(servis, cihaz, vid, cevap["nonce"])
    rapor = rapor_gonder(servis, cihaz, vid, hb["nonce"])

    assert rapor.overall_result == TaramaSonucu.BILINEN_ARTIFACT_YOK
    v = servis.depo.verification_al(vid)
    assert v.status == VerificationDurumu.TAMAMLANDI

def test_cevrimdisi_oyuncuya_kod_uretilmez(servis, oturumlar):
    oturumlar.koptu(OYUNCU)
    with pytest.raises(ServisHatasi) as hata:
        servis.olustur(OYUNCU)
    assert hata.value.kod == "PLAYER_NOT_CONNECTED"

def test_ayni_oyuncuya_ikinci_kod_uretilmez(servis):
    servis.olustur(OYUNCU)
    with pytest.raises(ServisHatasi) as hata:
        servis.olustur(OYUNCU)
    assert hata.value.kod == "ALREADY_PENDING"

def test_iptal_sonrasi_yeni_kod_uretilebilir(servis):
    servis.olustur(OYUNCU)
    servis.iptal(OYUNCU)
    v, _ = servis.olustur(OYUNCU)
    assert v.status == VerificationDurumu.BEKLIYOR

def test_acik_dogrulama_yokken_iptal_hata_verir(servis):
    with pytest.raises(ServisHatasi) as hata:
        servis.iptal(OYUNCU)
    assert hata.value.kod == "NO_ACTIVE_VERIFICATION"

def test_ag_eslesmesi_sadece_sinyal(servis, cihaz):

    cevap = claim_et(servis, cihaz, scanner_ip="203.0.113.10")
    assert cevap["network_status"] == AgDurumu.ESLESTI.value

def test_farkli_ip_eslesmedi_ama_akis_devam_eder(servis, cihaz):

    cevap = claim_et(servis, cihaz, scanner_ip="198.51.100.7")
    assert cevap["network_status"] == AgDurumu.ESLESMEDI.value
    v = servis.depo.verification_al(cevap["verification_id"])
    assert v.status == VerificationDurumu.DEVAM_EDIYOR

def test_scanner_ip_yoksa_ag_durumu_bilinmiyor(servis, cihaz):
    cevap = claim_et(servis, cihaz, scanner_ip=None)
    assert cevap["network_status"] == AgDurumu.BILINMIYOR.value

def test_ilk_cihaz_yeni_isaretlenmez_ikincisi_isaretlenir(servis, cihaz):
    from conftest import SahteCihaz
    cevap = claim_et(servis, cihaz)
    assert cevap["new_device"] is False
    servis.iptal(OYUNCU) if not servis.depo.verification_al(cevap["verification_id"]).bitmis_mi() else None

    ikinci = SahteCihaz()
    cevap2 = claim_et(servis, ikinci)
    assert cevap2["new_device"] is True

    v = servis.depo.verification_al(cevap2["verification_id"])
    assert v.status == VerificationDurumu.DEVAM_EDIYOR

def test_denetim_kaydi_tutuluyor(servis, cihaz):
    cevap = claim_et(servis, cihaz)
    hb = heartbeat_at(servis, cihaz, cevap["verification_id"], cevap["nonce"])
    rapor_gonder(servis, cihaz, cevap["verification_id"], hb["nonce"])
    olaylar = [k.olay for k in servis.depo.denetim]
    assert "verification started" in olaylar
    assert "verification claimed" in olaylar
    assert "verification completed" in olaylar
