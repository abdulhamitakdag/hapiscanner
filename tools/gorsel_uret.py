\
\
\
\
\
\
\
\
\

from __future__ import annotations

import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from PIL import Image, ImageDraw, ImageFilter

OLCEK = 12
TOHUM = 20261103

RENK = {
    ".": None,
    "t": (58, 42, 32, 255),
    "T": (74, 54, 40, 255),
    "c": (198, 150, 118, 255),
    "C": (214, 166, 132, 255),
    "g": (150, 110, 86, 255),
    "e": (44, 62, 92, 255),
    "w": (236, 240, 238, 255),
    "m": (116, 78, 58, 255),
    "s": (47, 168, 85, 255),
    "S": (62, 196, 104, 255),
    "d": (33, 126, 63, 255),
    "p": (44, 54, 74, 255),
    "P": (56, 68, 92, 255),
    "b": (34, 38, 46, 255),
    "k": (24, 26, 31, 255),
    "y": (226, 178, 58, 255),
    "Y": (245, 206, 92, 255),
    "r": (178, 54, 56, 255),
    "R": (208, 72, 72, 255),
    "a": (120, 132, 148, 255),
    "A": (158, 170, 186, 255),
    "u": (74, 162, 214, 255),
    "q": (34, 42, 60, 255),
    "x": (20, 22, 28, 255),
    "n": (92, 104, 120, 255),
}

def ciz(desen: list[str], golge: bool = True, tohum: int = TOHUM) -> Image.Image:

    rnd = random.Random(tohum)
    gen = max(len(s) for s in desen)
    yuk = len(desen)
    img = Image.new("RGBA", (gen, yuk), (0, 0, 0, 0))
    px = img.load()
    for y, satir in enumerate(desen):
        for x, ch in enumerate(satir.ljust(gen, ".")):
            renk = RENK.get(ch)
            if renk is None:
                continue
            if golge and ch not in "ewk":

                sapma = rnd.choice((-5, -2, 0, 0, 2, 4))
                renk = tuple(
                    max(0, min(255, renk[i] + sapma)) for i in range(3)
                ) + (renk[3],)
            px[x, y] = renk
    return img

def buyut(img: Image.Image, olcek: int = OLCEK) -> Image.Image:
    return img.resize((img.width * olcek, img.height * olcek), Image.NEAREST)

def golgeli(img: Image.Image, olcek: int = OLCEK, yumusak: int = 10) -> Image.Image:

    buyuk = buyut(img, olcek)
    pay = yumusak * 3
    tuval = Image.new("RGBA", (buyuk.width + pay * 2, buyuk.height + pay * 2), (0, 0, 0, 0))
    maske = Image.new("RGBA", tuval.size, (0, 0, 0, 0))
    maske.paste((0, 0, 0, 120), (pay, pay + olcek), buyuk)
    maske = maske.filter(ImageFilter.GaussianBlur(yumusak))
    tuval = Image.alpha_composite(tuval, maske)
    tuval.alpha_composite(buyuk, (pay, pay))
    return tuval

def yaz(ad: str, img: Image.Image, hedef: str) -> None:
    yol = os.path.join(hedef, ad)
    img.save(yol)
    print(f"  {ad}  {img.width}x{img.height}")

IKON_KALKAN = [
    "..aaaaaaaaaaaa..",
    ".aAAAAAAAAAAAAa.",
    ".aAssssssssssAa.",
    ".aAsssssssdssAa.",
    ".aAsssssdssssAa.",
    ".aAsswsssssssAa.",
    ".aAswwwsssdssAa.",
    ".aAsswwwwssssAa.",
    "..aAsswwwwwsAa..",
    "..aAssswwwwsAa..",
    "...aAssswwssAa..",
    "...aAsssswssa...",
    "....aAssssssa...",
    ".....aAsssaa....",
    "......aAssa.....",
    ".......aaa......",
]

IKON_BUYUTEC = [
    "....aaaaaa......",
    "..aauuuuuuaa....",
    "..auuwwuuuua....",
    ".auuwwuuuuuua...",
    ".auwwuuuuuuua...",
    ".auuuuuuuuuua...",
    ".auuuuuuuuuua...",
    ".auuuuuuuuuua...",
    "..auuuuuuuua....",
    "..aauuuuuuaa....",
    "....aaaaaan.....",
    ".........nna....",
    "..........naa...",
    "...........naa..",
    "............nna.",
    ".............na.",
]

IKON_KILIT = [
    "....aaaaaaaa....",
    "...aAn....nAa...",
    "...aA......Aa...",
    "...aA......Aa...",
    "..yyyyyyyyyyyy..",
    "..yYYYYYYYYYYy..",
    "..yYYYYkkYYYYy..",
    "..yYYYkkkkYYYy..",
    "..yYYYkkkkYYYy..",
    "..yYYYYkkYYYYy..",
    "..yYYYYkkYYYYy..",
    "..yYYYYYYYYYYy..",
    "..yYYYYYYYYYYy..",
    "..yyyyyyyyyyyy..",
    "................",
    "................",
]

IKON_ONAY = [
    "................",
    "..dddddddddddd..",
    ".dssssssssssssd.",
    ".dssssssssswssd.",
    ".dsssssssswwssd.",
    ".dswssssswwsssd.",
    ".dswwsssswwsssd.",
    ".dsswwsswwssssd.",
    ".dssswwwwwssssd.",
    ".dsssswwwssssssd",
    ".dsssssWssssssd.",
    ".dssssssssssssd.",
    ".dssssssssssssd.",
    "..dddddddddddd..",
    "................",
    "................",
]

IKON_UYARI = [
    ".......kk.......",
    "......krrk......",
    "......krrk......",
    ".....krRRrk.....",
    ".....krwwrk.....",
    "....krrwwrrk....",
    "....krrwwrrk....",
    "...krrrwwrrrk...",
    "...krrrwwrrrk...",
    "..krrrrwwrrrrk..",
    "..krrrrrrrrrrk..",
    ".krrrrrwwrrrrrk.",
    ".krrrrrwwrrrrrk.",
    "kkkkkkkkkkkkkkkk",
    "................",
    "................",
]

IKON_DOSYA = [
    "..wwwwwwwwww....",
    "..wAAAAAAAAww...",
    "..wAAAAAAAAAw...",
    "..wAnnnnnAAAw...",
    "..wAAAAAAAAAw...",
    "..wAnnnnnnnAw...",
    "..wAAAAAAAAAw...",
    "..wAnnnnnAAAw...",
    "..wAAAAAAAAAw...",
    "..wAnnnnnnnAw...",
    "..wAAAAAAAAAw...",
    "..wAnnnnnAAAw...",
    "..wAAAAAAAAAw...",
    "..wwwwwwwwwww...",
    "................",
    "................",
]

IKON_SUNUCU = [
    "..nnnnnnnnnnnn..",
    ".naaaaaaaaaaaan.",
    ".naAAAAAAAAAAan.",
    ".naAsAAAAAAAAan.",
    ".naaaaaaaaaaaan.",
    ".naAAAAAAAAAAan.",
    ".naAsAAAAAAAAan.",
    ".naaaaaaaaaaaan.",
    ".naAAAAAAAAAAan.",
    ".naAyAAAAAAAAan.",
    ".naaaaaaaaaaaan.",
    "..nnnnnnnnnnnn..",
    "....n......n....",
    "...nnn....nnn...",
    "................",
    "................",
]

def ikonlar() -> dict[str, list[str]]:
    return {
        "ikon_kalkan.png": IKON_KALKAN,
        "ikon_buyutec.png": IKON_BUYUTEC,
        "ikon_kilit.png": IKON_KILIT,
        "ikon_onay.png": IKON_ONAY,
        "ikon_uyari.png": IKON_UYARI,
        "ikon_dosya.png": IKON_DOSYA,
        "ikon_sunucu.png": IKON_SUNUCU,
    }

def desen_arkaplan(genislik: int = 640, yukseklik: int = 640) -> Image.Image:

    rnd = random.Random(TOHUM + 7)
    blok = 16
    kucuk = Image.new("RGBA", (genislik // blok, yukseklik // blok), (0, 0, 0, 0))
    d = ImageDraw.Draw(kucuk)
    for y in range(kucuk.height):
        for x in range(kucuk.width):
            taban = 20 + rnd.randint(0, 6)
            yesil = taban + rnd.choice((0, 0, 0, 3))
            d.point((x, y), (taban, yesil + 4, taban + 2, 255))
    return kucuk.resize((genislik, yukseklik), Image.NEAREST)

def logo_hazirla(kaynak: str, hedef: str) -> None:
    if not os.path.exists(kaynak):
        print(f"  ! logo bulunamadi: {kaynak}")
        return
    logo = Image.open(kaynak).convert("RGBA")
    for ad, boy in (("logo_hapicraft.png", 512), ("logo_hapicraft_kucuk.png", 128)):
        kopya = logo.copy()
        kopya.thumbnail((boy, boy), Image.LANCZOS)
        yaz(ad, kopya, hedef)

def main() -> int:
    hedef = sys.argv[1] if len(sys.argv) > 1 else os.path.join(
        os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
        "scanner", "HapiScanner.App", "Varliklar",
    )
    os.makedirs(hedef, exist_ok=True)
    print(f"Hedef: {hedef}")

    for ad, desen in ikonlar().items():
        yaz(ad, buyut(ciz(desen, golge=False), 8), hedef)

    yaz("arkaplan_doku.png", desen_arkaplan(), hedef)

    logo_hazirla(os.environ.get("HAPICRAFT_LOGO", ""), hedef)
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
