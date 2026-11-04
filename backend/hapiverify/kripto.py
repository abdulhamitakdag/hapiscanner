import base64
import hashlib
import hmac
import secrets

from cryptography.exceptions import InvalidSignature
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import ec

from .config import Ayarlar, VARSAYILAN

TOKEN_ALFABESI = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"

def token_uret(ayarlar: Ayarlar = VARSAYILAN) -> str:
    gruplar = [
        "".join(secrets.choice(TOKEN_ALFABESI) for _ in range(ayarlar.token_grup_uzunlugu))
        for _ in range(ayarlar.token_grup_sayisi)
    ]
    return "HAPI-" + "-".join(gruplar)

def token_normalize(token: str) -> str:

    return token.strip().upper().replace(" ", "")

def token_hashle(token: str) -> str:

    return hashlib.sha256(token_normalize(token).encode()).hexdigest()

def token_dogrula(token: str, beklenen_hash: str) -> bool:
    return hmac.compare_digest(token_hashle(token), beklenen_hash)

def nonce_uret(ayarlar: Ayarlar = VARSAYILAN) -> str:
    return secrets.token_hex(ayarlar.nonce_bit // 8)

def parmak_izi(public_key_b64: str) -> str:
    ham = base64.b64decode(public_key_b64)
    return hashlib.sha256(ham).hexdigest()[:32]

def imza_metni(baglam: str, **alanlar) -> bytes:
\
\
\
\

    parcalar = [baglam] + [f"{ad}={alanlar[ad]}" for ad in sorted(alanlar)]
    return "|".join(parcalar).encode()

def imza_dogrula(public_key_b64: str, imza_b64: str, metin: bytes) -> bool:
\
\
\
\
\
\

    try:
        anahtar = serialization.load_der_public_key(base64.b64decode(public_key_b64))
        if not isinstance(anahtar, ec.EllipticCurvePublicKey):
            return False
        if not isinstance(anahtar.curve, ec.SECP256R1):
            return False
        anahtar.verify(base64.b64decode(imza_b64), metin, ec.ECDSA(hashes.SHA256()))
        return True
    except (InvalidSignature, ValueError, TypeError):
        return False

def rapor_hash(govde: str) -> str:
    return hashlib.sha256(govde.encode()).hexdigest()

def surum_kucuk_mu(surum: str, asgari: str) -> bool:

    def ayikla(s: str):
        parcalar = []
        for p in s.split("."):
            try:
                parcalar.append(int(p))
            except ValueError:

                sayi = ""
                for ch in p:
                    if ch.isdigit():
                        sayi += ch
                    else:
                        break
                parcalar.append(int(sayi) if sayi else 0)
        return parcalar

    a, b = ayikla(surum), ayikla(asgari)
    uzunluk = max(len(a), len(b))
    a += [0] * (uzunluk - len(a))
    b += [0] * (uzunluk - len(b))
    return a < b
