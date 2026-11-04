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
\
\
\
\
\
\
\
\
\

import argparse
import base64
import hashlib
import json
import pathlib
import sys

from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import ec
from cryptography.exceptions import InvalidSignature

def imza_metni(paket_ozeti: str, ruleset_surumu: str) -> bytes:

    alanlar = {"package_sha256": paket_ozeti, "ruleset_version": ruleset_surumu}
    parcalar = ["ruleset"] + [f"{ad}={alanlar[ad]}" for ad in sorted(alanlar)]
    return "|".join(parcalar).encode()

def paket_oku(yol: pathlib.Path):
    govde = yol.read_bytes()
    ozet = hashlib.sha256(govde).hexdigest()
    surum = json.loads(govde.decode("utf-8"))["ruleset_version"]
    return govde, ozet, surum

def anahtar_uret(cikti: pathlib.Path):
    anahtar = ec.generate_private_key(ec.SECP256R1())
    pem = anahtar.private_bytes(
        encoding=serialization.Encoding.PEM,
        format=serialization.PrivateFormat.PKCS8,
        encryption_algorithm=serialization.NoEncryption(),
    )
    cikti.parent.mkdir(parents=True, exist_ok=True)
    cikti.write_bytes(pem)
    cikti.chmod(0o600)
    print(f"private key yazildi: {cikti} (mod 600 — depoya KOYMA)")
    print("public key (scanner'a gomulecek):")
    print(public_key_b64(anahtar))

def public_key_b64(anahtar) -> str:
    ham = anahtar.public_key().public_bytes(
        encoding=serialization.Encoding.DER,
        format=serialization.PublicFormat.SubjectPublicKeyInfo,
    )
    return base64.b64encode(ham).decode()

def anahtar_yukle(yol: pathlib.Path):
    return serialization.load_pem_private_key(yol.read_bytes(), password=None)

def imzala(anahtar_yolu: pathlib.Path, paket_yolu: pathlib.Path):
    anahtar = anahtar_yukle(anahtar_yolu)
    _, ozet, surum = paket_oku(paket_yolu)
    imza = anahtar.sign(imza_metni(ozet, surum), ec.ECDSA(hashes.SHA256()))

    sig_yolu = paket_yolu.with_suffix(paket_yolu.suffix + ".sig")
    sig_yolu.write_text(base64.b64encode(imza).decode() + "\n")
    print(f"ruleset_version : {surum}")
    print(f"package_sha256  : {ozet}")
    print(f"imza yazildi    : {sig_yolu}")

def dogrula(public_key: str, paket_yolu: pathlib.Path) -> int:
    sig_yolu = paket_yolu.with_suffix(paket_yolu.suffix + ".sig")
    if not sig_yolu.exists():
        print(f"imza dosyasi yok: {sig_yolu}", file=sys.stderr)
        return 1

    anahtar = serialization.load_der_public_key(base64.b64decode(public_key))
    _, ozet, surum = paket_oku(paket_yolu)
    imza = base64.b64decode(sig_yolu.read_text().strip())

    try:
        anahtar.verify(imza, imza_metni(ozet, surum), ec.ECDSA(hashes.SHA256()))
    except InvalidSignature:
        print("IMZA GECERSIZ", file=sys.stderr)
        return 1

    print(f"imza gecerli (surum {surum})")
    return 0

def main() -> int:
    ayristirici = argparse.ArgumentParser(description="Kural paketi imzalama araci")
    altlar = ayristirici.add_subparsers(dest="komut", required=True)

    a = altlar.add_parser("anahtar-uret")
    a.add_argument("--cikti", type=pathlib.Path, required=True)

    p = altlar.add_parser("public-key")
    p.add_argument("--anahtar", type=pathlib.Path, required=True)

    i = altlar.add_parser("imzala")
    i.add_argument("--anahtar", type=pathlib.Path, required=True)
    i.add_argument("--paket", type=pathlib.Path, required=True)

    d = altlar.add_parser("dogrula")
    d.add_argument("--public-key", required=True)
    d.add_argument("--paket", type=pathlib.Path, required=True)

    args = ayristirici.parse_args()

    if args.komut == "anahtar-uret":
        anahtar_uret(args.cikti)
        return 0
    if args.komut == "public-key":
        print(public_key_b64(anahtar_yukle(args.anahtar)))
        return 0
    if args.komut == "imzala":
        imzala(args.anahtar, args.paket)
        return 0
    return dogrula(args.public_key, args.paket)

if __name__ == "__main__":
    sys.exit(main())
