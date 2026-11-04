\
\
\

import pytest

from conftest import (
    OYUNCU, RULESET_SURUMU, SCANNER_SURUMU, SahteCihaz,
    claim_et, heartbeat_at, rapor_gonder,
)
from hapiverify import kripto
from hapiverify.domain import TaramaSonucu, VerificationDurumu
from hapiverify.servis import ServisHatasi

def test_L_reconnect_eski_verification_devralinamaz(servis, oturumlar, cihaz):
    _, token = servis.olustur(OYUNCU)

    oturumlar.yeniden_baglandi(OYUNCU, "oturum-BBB")
    with pytest.raises(ServisHatasi) as hata:
        claim_et(servis, cihaz, token=token)
    assert hata.value.kod == "SESSION_INTERRUPTED"

def test_L_tarama_ortasinda_session_degisirse_kesilir(servis, oturumlar, cihaz):
    cevap = claim_et(servis, cihaz)
    oturumlar.yeniden_baglandi(OYUNCU, "oturum-CCC")
    with pytest.raises(ServisHatasi) as hata:
        heartbeat_at(servis, cihaz, cevap["verification_id"], cevap["nonce"])
    assert hata.value.kod == "NOT_IN_PROGRESS"
    v = servis.depo.verification_al(cevap["verification_id"])
    assert v.status == VerificationDurumu.OTURUM_KESILDI

def test_T_minecraft_koparsa_dogrulama_gecersiz(servis, oturumlar, cihaz):
    cevap = claim_et(servis, cihaz)
    oturumlar.koptu(OYUNCU)
    with pytest.raises(ServisHatasi):
        heartbeat_at(servis, cihaz, cevap["verification_id"], cevap["nonce"])
    v = servis.depo.verification_al(cevap["verification_id"])
    assert v.status == VerificationDurumu.OTURUM_KESILDI

def test_M_ayni_nonce_ikinci_kez_kullanilamaz(servis, cihaz):
    cevap = claim_et(servis, cihaz)
    vid, nonce = cevap["verification_id"], cevap["nonce"]
    heartbeat_at(servis, cihaz, vid, nonce)
    with pytest.raises(ServisHatasi) as hata:
        heartbeat_at(servis, cihaz, vid, nonce, sira_no=2)
    assert hata.value.kod == "INVALID_NONCE"

def test_M_ayni_rapor_ikinci_kez_gonderilemez(servis, cihaz):
    cevap = claim_et(servis, cihaz)
    vid = cevap["verification_id"]
    hb = heartbeat_at(servis, cihaz, vid, cevap["nonce"])
    rapor_gonder(servis, cihaz, vid, hb["nonce"])

    with pytest.raises(ServisHatasi) as hata:
        rapor_gonder(servis, cihaz, vid, hb["nonce"])
    assert hata.value.kod in ("NOT_IN_PROGRESS", "REPORT_ALREADY_SUBMITTED")

def test_M_baska_verificationin_noncesi_kullanilamaz(servis, oturumlar, cihaz):
    cevap = claim_et(servis, cihaz)
    servis.iptal(OYUNCU)
    oturumlar.baglandi(OYUNCU, "TestOyuncu", "oturum-AAA", game_ip="203.0.113.10")
    ikinci = claim_et(servis, cihaz)

    with pytest.raises(ServisHatasi) as hata:
        heartbeat_at(servis, cihaz, ikinci["verification_id"], cevap["nonce"])
    assert hata.value.kod == "INVALID_NONCE"

def test_N_claim_edilmis_kod_tekrar_kullanilamaz(servis, cihaz):
    _, token = servis.olustur(OYUNCU)
    claim_et(servis, cihaz, token=token)
    with pytest.raises(ServisHatasi) as hata:
        claim_et(servis, SahteCihaz(), token=token)
    assert hata.value.kod == "TOKEN_ALREADY_USED"

def test_N_iptal_edilmis_kod_kullanilamaz(servis, cihaz):
    _, token = servis.olustur(OYUNCU)
    servis.iptal(OYUNCU)
    with pytest.raises(ServisHatasi) as hata:
        claim_et(servis, cihaz, token=token)
    assert hata.value.kod == "TOKEN_ALREADY_USED"

def test_O_yanlis_token_denemeleri_rate_limite_takilir(servis, cihaz, ayarlar):
    servis.olustur(OYUNCU)
    kodlar = []
    for _ in range(ayarlar.claim_limit_adet + 2):
        try:
            servis.claim("HAPI-ZZZZ-ZZZZ", cihaz.public_key, "TPM",
                         SCANNER_SURUMU, RULESET_SURUMU, "198.51.100.1")
        except ServisHatasi as e:
            kodlar.append(e.kod)
    assert "INVALID_TOKEN" in kodlar
    assert "RATE_LIMITED" in kodlar

def test_O_rate_limit_pencere_sonrasi_sifirlanir(servis, cihaz, saat, ayarlar):
    servis.olustur(OYUNCU)
    for _ in range(ayarlar.claim_limit_adet):
        try:
            servis.claim("HAPI-ZZZZ-ZZZZ", cihaz.public_key, "TPM",
                         SCANNER_SURUMU, RULESET_SURUMU, "198.51.100.2")
        except ServisHatasi:
            pass
    saat.ilerle(ayarlar.claim_limit_pencere_saniye + 1)
    with pytest.raises(ServisHatasi) as hata:
        servis.claim("HAPI-ZZZZ-ZZZZ", cihaz.public_key, "TPM",
                     SCANNER_SURUMU, RULESET_SURUMU, "198.51.100.2")
    assert hata.value.kod == "INVALID_TOKEN"

def test_P_eski_scanner_reddedilir(servis, cihaz):
    with pytest.raises(ServisHatasi) as hata:
        claim_et(servis, cihaz, scanner_surumu="0.0.9")
    assert hata.value.kod == "SCANNER_TOO_OLD"

def test_P_yasakli_scanner_surumu_reddedilir(servis, oturumlar, cihaz, saat):
    from hapiverify.config import Ayarlar
    from hapiverify.depo import Depo
    from hapiverify.servis import VerificationServisi
    s = VerificationServisi(Depo(), oturumlar, saat,
                            Ayarlar(yasakli_scanner_surumleri=("0.2.0",)))
    with pytest.raises(ServisHatasi) as hata:
        claim_et(s, cihaz, scanner_surumu="0.2.0")
    assert hata.value.kod == "SCANNER_VERSION_BLOCKED"

def test_Q_eski_ruleset_reddedilir(servis, cihaz):
    with pytest.raises(ServisHatasi) as hata:
        claim_et(servis, cihaz, ruleset_surumu="2026.10.01.1")
    assert hata.value.kod == "RULESET_TOO_OLD"

def test_Q_rapor_asamasinda_da_ruleset_denetlenir(servis, cihaz):
    cevap = claim_et(servis, cihaz)
    hb = heartbeat_at(servis, cihaz, cevap["verification_id"], cevap["nonce"])
    with pytest.raises(ServisHatasi) as hata:
        rapor_gonder(servis, cihaz, cevap["verification_id"], hb["nonce"],
                     ruleset_surumu="2026.01.01.1")
    assert hata.value.kod == "RULESET_TOO_OLD"

def test_R_client_saati_tokenin_suresini_uzatamaz(servis, cihaz, saat, ayarlar):
    _, token = servis.olustur(OYUNCU)
    saat.ilerle(ayarlar.token_ttl_saniye + 1)
    with pytest.raises(ServisHatasi) as hata:
        claim_et(servis, cihaz, token=token)
    assert hata.value.kod == "TOKEN_EXPIRED"

def test_R_client_timestamp_imza_kapsaminda_ama_otorite_degil(servis, cihaz):
    cevap = claim_et(servis, cihaz)
    vid, nonce = cevap["verification_id"], cevap["nonce"]

    yalan = "2030-01-01T00:00:00+00:00"
    metin = kripto.imza_metni("heartbeat", verification_id=vid, nonce=nonce,
                              sequence=1, stage="filesystem", timestamp=yalan)
    servis.heartbeat(vid, nonce, cihaz.imzala(metin), 1, "filesystem", 5, yalan)
    v = servis.depo.verification_al(vid)
    assert v.son_heartbeat == servis.saat.simdi()
    assert v.son_heartbeat.year == 2026

def test_S_kisa_kesinti_dogrulamayi_dusurmez(servis, cihaz, saat, ayarlar):
    cevap = claim_et(servis, cihaz)
    vid = cevap["verification_id"]
    saat.ilerle(ayarlar.heartbeat_tolerans_saniye - 5)
    hb = heartbeat_at(servis, cihaz, vid, cevap["nonce"])
    assert hb["nonce"]
    assert servis.depo.verification_al(vid).status == VerificationDurumu.DEVAM_EDIYOR

def test_S_uzun_kesinti_baglanti_kaybi(servis, cihaz, saat, ayarlar):
    cevap = claim_et(servis, cihaz)
    vid = cevap["verification_id"]
    saat.ilerle(ayarlar.heartbeat_tolerans_saniye + 5)
    with pytest.raises(ServisHatasi) as hata:
        heartbeat_at(servis, cihaz, vid, cevap["nonce"])
    assert hata.value.kod == "NOT_IN_PROGRESS"
    assert servis.depo.verification_al(vid).status == VerificationDurumu.BAGLANTI_KAYBI

def test_B4_baska_anahtarla_imzalanan_heartbeat_reddedilir(servis, cihaz):
    cevap = claim_et(servis, cihaz)
    vid, nonce = cevap["verification_id"], cevap["nonce"]
    saldirgan = SahteCihaz()
    zaman = servis.saat.simdi().isoformat()
    metin = kripto.imza_metni("heartbeat", verification_id=vid, nonce=nonce,
                              sequence=1, stage="filesystem", timestamp=zaman)
    with pytest.raises(ServisHatasi) as hata:
        servis.heartbeat(vid, nonce, saldirgan.imzala(metin), 1, "filesystem", 5, zaman)
    assert hata.value.kod == "INVALID_SIGNATURE"

def test_B4_imzasiz_rapor_reddedilir(servis, cihaz):
    cevap = claim_et(servis, cihaz)
    vid = cevap["verification_id"]
    hb = heartbeat_at(servis, cihaz, vid, cevap["nonce"])
    with pytest.raises(ServisHatasi) as hata:
        servis.rapor_tamamla(vid, hb["nonce"], "Z2VjZXJzaXo=", SCANNER_SURUMU,
                             RULESET_SURUMU, "NO_KNOWN_ARTIFACTS",
                             kripto.rapor_hash("{}"), "{}", "GOOD", "GOOD", [],
                             servis.saat.simdi().isoformat())
    assert hata.value.kod == "INVALID_SIGNATURE"

def test_B4_heartbeat_imzasi_rapor_icin_kullanilamaz(servis, cihaz):

    cevap = claim_et(servis, cihaz)
    vid = cevap["verification_id"]
    hb = heartbeat_at(servis, cihaz, vid, cevap["nonce"])
    nonce = hb["nonce"]
    rh = kripto.rapor_hash("{}")
    zaman = servis.saat.simdi().isoformat()
    yanlis_baglam = kripto.imza_metni(
        "heartbeat", verification_id=vid, nonce=nonce,
        scanner_version=SCANNER_SURUMU, ruleset_version=RULESET_SURUMU,
        report_hash=rh, timestamp=zaman)
    with pytest.raises(ServisHatasi) as hata:
        servis.rapor_tamamla(vid, nonce, cihaz.imzala(yanlis_baglam), SCANNER_SURUMU,
                             RULESET_SURUMU, "NO_KNOWN_ARTIFACTS", rh, "{}",
                             "GOOD", "GOOD", [], zaman)
    assert hata.value.kod == "INVALID_SIGNATURE"

def test_B9_hash_uyusmazsa_oynanmis_suphesi(servis, cihaz):
    cevap = claim_et(servis, cihaz)
    vid = cevap["verification_id"]
    hb = heartbeat_at(servis, cihaz, vid, cevap["nonce"])
    with pytest.raises(ServisHatasi) as hata:
        rapor_gonder(servis, cihaz, vid, hb["nonce"], govde='{"gercek":1}',
                     bozuk_hash=kripto.rapor_hash('{"farkli":2}'))
    assert hata.value.kod == "REPORT_HASH_MISMATCH"
    rapor = servis.depo.verification_raporu(vid)
    assert rapor.overall_result == TaramaSonucu.OYNANMIS_SUPHESI

def test_sira_numarasi_geriye_gidemez(servis, cihaz):
    cevap = claim_et(servis, cihaz)
    vid = cevap["verification_id"]
    hb = heartbeat_at(servis, cihaz, vid, cevap["nonce"], sira_no=5)
    with pytest.raises(ServisHatasi) as hata:
        heartbeat_at(servis, cihaz, vid, hb["nonce"], sira_no=3)
    assert hata.value.kod == "STALE_SEQUENCE"

def test_bilinmeyen_verification_id_reddedilir(servis, cihaz):
    with pytest.raises(ServisHatasi) as hata:
        heartbeat_at(servis, cihaz, "yok-boyle-bir-id", "abc")
    assert hata.value.kod == "UNKNOWN_VERIFICATION"
