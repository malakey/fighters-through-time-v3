"""Crops one 144 x 144 ability-slot icon per roster character and slot from the retro v2
ability-VFX atlas (assets/sprites/vfx/<id>/<id>_ability_vfx_atlas.png: 384 px cells, 4 rows
special_1 / special_2 / movement / ultimate, 3 frame columns; tools/retro_sprites/vfx_pack.py).

The frame used is the middle one (the effect at its peak) unless that cell is nearly empty, in which
case the fullest of the three is used.  The cell is trimmed to its opaque bounds, centred on a
square with 6% padding and resized to 144 (the mockup's prep_assets.vfx_icon).

The roster is read from resources/Content/content_manifest.csv (Character rows), never listed here.

usage (repo root): py tools/asset_src/pulp/build_ability_icons.py [id ...]
Output: assets/sprites/ui/pulp/ability_icons/<id>_<special_1|special_2|movement|ultimate>.png + ability_icons.json
"""
import csv
import json
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
OUT = os.path.join(REPO, "assets", "sprites", "ui", "pulp", "ability_icons")
SLOTS = ["special_1", "special_2", "movement", "ultimate"]
CELL, SIZE, PAD = 384, 144, 0.06


def roster() -> list:
    ids = []
    with open(os.path.join(REPO, "resources", "Content", "content_manifest.csv"), encoding="utf-8-sig", newline="") as fh:
        rows = [r for r in csv.reader(fh) if r and not r[0].startswith("#") and r[0] != "schema_version"]
    header = [h.strip().lower() for h in rows[0]]
    ci, ii = header.index("category"), header.index("id")
    for r in rows[1:]:
        if r[ci].strip() == "Character":
            ids.append(r[ii].strip())
    return ids


def trim(im, pad=2):
    bb = im.getchannel("A").point(lambda v: 255 if v > 8 else 0).getbbox()
    if not bb:
        return im
    x0, y0, x1, y1 = bb
    return im.crop((max(0, x0 - pad), max(0, y0 - pad), min(im.width, x1 + pad), min(im.height, y1 + pad)))


def bake(cid: str, index: dict):
    at = Image.open(os.path.join(REPO, "assets", "sprites", "vfx", cid, f"{cid}_ability_vfx_atlas.png")).convert("RGBA")
    os.makedirs(OUT, exist_ok=True)
    for row, slot in enumerate(SLOTS):
        cells = [at.crop((c * CELL, row * CELL, c * CELL + CELL, row * CELL + CELL)) for c in range(3)]
        mass = [int((np.asarray(c.getchannel("A")) > 24).sum()) for c in cells]
        col = 1 if mass[1] >= 0.35 * max(mass) else int(np.argmax(mass))
        cr = trim(cells[col])
        side = int(max(cr.width, cr.height) * (1 + PAD * 2))
        sq = Image.new("RGBA", (side, side), (0, 0, 0, 0))
        sq.alpha_composite(cr, ((side - cr.width) // 2, (side - cr.height) // 2))
        sq = sq.resize((SIZE, SIZE), Image.LANCZOS)
        name = f"{cid}_{slot}.png"
        sq.save(os.path.join(OUT, name))
        index[f"{cid}_{slot}"] = {"file": f"ability_icons/{name}", "size": [SIZE, SIZE], "atlas_row": row, "atlas_column": col, "opaque_px": mass}


def main():
    ids = sys.argv[1:] or roster()
    path = os.path.join(OUT, "ability_icons.json")
    index = json.load(open(path, encoding="utf-8")) if (sys.argv[1:] and os.path.exists(path)) else {}
    for cid in ids:
        bake(cid, index)
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        json.dump(index, fh, indent=1, sort_keys=True)
    print("ability icons:", len(index))


if __name__ == "__main__":
    main()
