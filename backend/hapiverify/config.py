from dataclasses import dataclass

@dataclass(frozen=True)
class Ayarlar:

    token_ttl_saniye: int = 300
    token_grup_sayisi: int = 2
    token_grup_uzunlugu: int = 4

    nonce_ttl_saniye: int = 600
    nonce_bit: int = 256

    heartbeat_saniye: int = 5
    heartbeat_tolerans_saniye: int = 30

    oturum_tazelik_saniye: int = 90

    claim_limit_adet: int = 10
    claim_limit_pencere_saniye: int = 60

    verification_yanlis_deneme_siniri: int = 5

    asgari_scanner_surumu: str = "0.1.0"
    asgari_ruleset_surumu: str = "2026.11.03.1"

    yasakli_scanner_surumleri: tuple = ()

    ip_saklama_gunu: int = 7

    rapor_tam_saklama_gunu: int = 30
    rapor_ozet_saklama_gunu: int = 365

    bakim_araligi_saniye: int = 3600

    hapicraft_endpointleri: tuple = ("hapicraft.net:25565",)

    def __post_init__(self):
        if self.heartbeat_tolerans_saniye <= self.heartbeat_saniye:
            raise ValueError("heartbeat toleransi heartbeat araligindan buyuk olmali")

VARSAYILAN = Ayarlar()
