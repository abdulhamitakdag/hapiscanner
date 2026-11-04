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

from __future__ import annotations

import json
import pathlib
import sys

def denetle(dizin: pathlib.Path) -> int:
    hata = 0
    dosyalar = sorted(dizin.glob("*.json"))
    if not dosyalar:
        print(f"{dizin}: kural paketi bulunamadi")
        return 1

    for yol in dosyalar:
        try:
            paket = json.loads(yol.read_text(encoding="utf-8"))
        except json.JSONDecodeError as e:
            print(f"{yol}: okunamadi ({e})")
            hata = 1
            continue

        if "ruleset_version" not in paket:
            print(f"{yol}: ruleset_version yok")
            hata = 1

        imzali = yol.with_suffix(yol.suffix + ".sig").exists() or "signature" in paket
        if not imzali and not yol.name.startswith("ornek-"):
            print(f"{yol}: imzasiz paket 'ornek-' onekiyle durmali")
            hata = 1

        print(f"{yol}: surum={paket.get('ruleset_version')} imzali={imzali}")

    return hata

def main() -> int:
    dizin = pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else "rules")
    return denetle(dizin)

if __name__ == "__main__":
    raise SystemExit(main())
