\
\
\
\
\
\
\
\
\
\
\
\
\

import os

from .api import uygulama_olustur
from .config import VARSAYILAN
from .depo import Saat
from .kalici import KaliciDepo, KaliciNonceDeposu, baglanti_ac
from .oturum import BildirilenOturumSaglayici
from .servis import VerificationServisi

saat = Saat()
baglanti = baglanti_ac(os.environ.get("HAPIVERIFY_DB", "veri/hapiverify.db"))
oturumlar = BildirilenOturumSaglayici(saat, VARSAYILAN.oturum_tazelik_saniye)
servis = VerificationServisi(
    KaliciDepo(baglanti), oturumlar, saat, VARSAYILAN,

    nonce_deposu=KaliciNonceDeposu(saat, baglanti),
)

app = uygulama_olustur(
    servis,
    VARSAYILAN,
    plugin_jetonu=os.environ.get("HAPIVERIFY_PLUGIN_TOKEN", "gelistirme-jetonu"),
)
