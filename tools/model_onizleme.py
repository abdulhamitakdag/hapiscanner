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

from __future__ import annotations

import pathlib
import sys
import tempfile

import numpy as np
from PIL import Image

KAYNAK = pathlib.Path(__file__).resolve().parent.parent / "scanner/HapiScanner.App/Varliklar/kaynak"
SKIN = KAYNAK / "hapitolar_skin.png"
PELERIN = KAYNAK / "hapitolar_pelerin.png"

GENISLIK, YUKSEKLIK = 320, 420
OLCEK = 11.0
MERKEZ_Y = 16.0
ISIK = np.array([-0.45, 0.7, 1.0])
ISIK = ISIK / np.linalg.norm(ISIK)

def obj_oku(yol: pathlib.Path):
    koseler: list[tuple[float, float, float]] = []
    uvler: list[tuple[float, float]] = []
    yuzler: list[tuple[list[int], list[int], str]] = []
    malzeme = "skin"
    for satir in yol.read_text(encoding="utf-8").splitlines():
        p = satir.split()
        if not p:
            continue
        if p[0] == "v":
            koseler.append((float(p[1]), float(p[2]), float(p[3])))
        elif p[0] == "vt":
            uvler.append((float(p[1]), float(p[2])))
        elif p[0] == "usemtl":
            malzeme = p[1]
        elif p[0] == "f":
            k, u = [], []
            for alan in p[1:]:
                a, b = alan.split("/")[:2]
                k.append(int(a) - 1)
                u.append(int(b) - 1)
            yuzler.append((k, u, malzeme))
    return np.array(koseler), np.array(uvler), yuzler

def doku_oku(yol: pathlib.Path) -> np.ndarray:
    return np.asarray(Image.open(yol).convert("RGBA"), dtype=np.float32) / 255.0

def ucgen_ciz(tuval, zbuf, p0, p1, p2, uv0, uv1, uv2, doku, parlaklik):

    minx = max(int(np.floor(min(p0[0], p1[0], p2[0]))), 0)
    maxx = min(int(np.ceil(max(p0[0], p1[0], p2[0]))), GENISLIK - 1)
    miny = max(int(np.floor(min(p0[1], p1[1], p2[1]))), 0)
    maxy = min(int(np.ceil(max(p0[1], p1[1], p2[1]))), YUKSEKLIK - 1)
    if minx > maxx or miny > maxy:
        return

    alan = (p1[0] - p0[0]) * (p2[1] - p0[1]) - (p2[0] - p0[0]) * (p1[1] - p0[1])
    if abs(alan) < 1e-9:
        return

    ys, xs = np.mgrid[miny:maxy + 1, minx:maxx + 1]
    px, py = xs + 0.5, ys + 0.5

    w0 = ((p1[0] - px) * (p2[1] - py) - (p2[0] - px) * (p1[1] - py)) / alan
    w1 = ((p2[0] - px) * (p0[1] - py) - (p0[0] - px) * (p2[1] - py)) / alan
    w2 = 1.0 - w0 - w1

    icinde = (w0 >= 0) & (w1 >= 0) & (w2 >= 0)
    if not icinde.any():
        return

    z = w0 * p0[2] + w1 * p1[2] + w2 * p2[2]
    onde = icinde & (z > zbuf[miny:maxy + 1, minx:maxx + 1])
    if not onde.any():
        return

    u = w0 * uv0[0] + w1 * uv1[0] + w2 * uv2[0]
    v = w0 * uv0[1] + w1 * uv1[1] + w2 * uv2[1]
    dy, dx = doku.shape[0], doku.shape[1]
    tx = np.clip((u * dx).astype(int), 0, dx - 1)
    ty = np.clip(((1 - v) * dy).astype(int), 0, dy - 1)
    renk = doku[ty, tx]

    gorunur = onde & (renk[..., 3] > 0.5)
    if not gorunur.any():
        return

    hedef = tuval[miny:maxy + 1, minx:maxx + 1]
    hedef[gorunur] = np.clip(renk[gorunur][:, :3] * parlaklik, 0, 1)
    zb = zbuf[miny:maxy + 1, minx:maxx + 1]
    zb[gorunur] = z[gorunur]

def render(yol: pathlib.Path) -> Image.Image:
    koseler, uvler, yuzler = obj_oku(yol)
    dokular = {"skin": doku_oku(SKIN), "pelerin": doku_oku(PELERIN)}

    tuval = np.zeros((YUKSEKLIK, GENISLIK, 3), dtype=np.float32)
    tuval[:] = np.array([0.055, 0.075, 0.09])
    zbuf = np.full((YUKSEKLIK, GENISLIK), -1e9, dtype=np.float32)

    ekran = koseler.copy()
    ekran[:, 0] = GENISLIK / 2 + koseler[:, 0] * OLCEK
    ekran[:, 1] = YUKSEKLIK / 2 - (koseler[:, 1] - MERKEZ_Y) * OLCEK
    ekran[:, 2] = koseler[:, 2]

    for k, u, malzeme in yuzler:
        a, b, c = koseler[k[0]], koseler[k[1]], koseler[k[2]]
        normal = np.cross(b - a, c - b)
        norm = np.linalg.norm(normal)
        if norm < 1e-9:
            continue
        normal = normal / norm
        if normal[2] <= 0:
            continue
        parlaklik = 0.55 + 0.45 * max(float(np.dot(normal, ISIK)), 0.0)

        for i in (1, 2):
            ucgen_ciz(
                tuval, zbuf,
                ekran[k[0]], ekran[k[i]], ekran[k[i + 1]],
                uvler[u[0]], uvler[u[i]], uvler[u[i + 1]],
                dokular[malzeme], parlaklik)

    return Image.fromarray((tuval * 255).astype(np.uint8))

def main() -> int:
    kaynak = pathlib.Path(tempfile.gettempdir()) / "hapiscanner-onizleme"
    if not kaynak.is_dir() or not list(kaynak.glob("*.obj")):
        print(f"OBJ bulunamadi: {kaynak}\nOnce: dotnet test scanner/HapiScanner.sln")
        return 1

    cikis = pathlib.Path(sys.argv[1]) if len(sys.argv) > 1 else kaynak
    cikis.mkdir(parents=True, exist_ok=True)

    dosyalar = sorted(kaynak.glob("*.obj"))
    kareler = []
    for obj in dosyalar:
        resim = render(obj)
        resim.save(cikis / f"{obj.stem}.png")
        kareler.append((obj.stem, resim))
        print(f"{obj.stem}.png")

    sutun = 5
    satir = (len(kareler) + sutun - 1) // sutun
    tabaka = Image.new("RGB", (sutun * GENISLIK, satir * YUKSEKLIK), (14, 19, 23))
    for i, (_, resim) in enumerate(kareler):
        tabaka.paste(resim, ((i % sutun) * GENISLIK, (i // sutun) * YUKSEKLIK))
    tabaka.save(cikis / "tumu.png")
    print(f"tumu.png ({len(kareler)} animasyon)")
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
