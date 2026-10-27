"""TG-ART: bakes the character select's hero sprites (UI_SPEC_GLASS 3.7, 7.5; plan 2.3).

The select shows the hovered or locked hero's retro v2 idle loop per side. Loading a hero's SpriteFrames loads
all six of its atlases (41-51 MiB a hero), so this bakes the idle animation's four frames into small PNGs instead.

The roster is read from resources/Content/content_manifest.csv (its `Character` rows, in grid order), never
listed here. For each member, tools/retro_sprites/manifests/<id>.json gives the idle animation's atlas sheet, its
four cell regions, its feet gap (the sole line's distance above the cell bottom) and its frame rate. The four frames
share one crop: the union of their art, padded, symmetric about the cell's horizontal centre (the pivot), so the
loop stays registered and a horizontal flip keeps the pivot in place. Output, all at the atlases' own 2x texels:

  assets/sprites/ui/glass/select_heroes/<id>_idle_<n>.png     n = 0..3, mipmapped (apply_import_settings.py)
  assets/sprites/ui/glass/select_heroes/select_heroes.json    per hero: fps, loop, texel scale, and per frame the
                                                              file, size, pivot_x and feet_y (image px from the top)

Drawing (the mock, _build/lib.py spr()): scale the PNG by 2.05, put its (pivot_x, feet_y) on the screen point
(side centre x, 700); P2 flipped. usage (repo root): py tools/asset_src/glass/build_select_heroes.py
"""
from __future__ import annotations

import csv
import json
import math
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import glass_svg as G  # noqa: E402

OUT = os.path.join(G.REPO, "assets", "sprites", "ui", "glass", "select_heroes")
MANIFEST = os.path.join(G.REPO, "resources", "Content", "content_manifest.csv")
PAD = 4             # transparent margin around the union of the frames' art
ALPHA_FLOOR = 6     # alpha at or below this counts as empty when trimming
SELECT_SCALE = 2.05
SELECT_FEET_Y = 700


def roster():
    ids = []
    with open(MANIFEST, encoding="utf-8", newline="") as fh:
        for row in csv.reader(fh):
            if len(row) > 1 and row[0] == "Character":
                ids.append(row[1].strip())
    if not ids:
        raise SystemExit("no Character rows in " + MANIFEST)
    return ids


def bake(cid: str) -> dict:
    m = json.load(open(os.path.join(G.REPO, "tools", "retro_sprites", "manifests", f"{cid}.json"), encoding="utf-8"))
    an = m["animations"]["idle"]
    atlas = Image.open(os.path.join(G.REPO, "assets", "sprites", "characters", cid, "retro",
                                    f"{cid}_{an['sheet']}_atlas.png")).convert("RGBA")
    cells = [atlas.crop((x, y, x + w, y + h)) for x, y, w, h in an["regions"]]
    if len(cells) != 4 or len({c.size for c in cells}) != 1:
        raise SystemExit(f"{cid}: idle must be four equal cells, got {[c.size for c in cells]}")
    cw, ch = cells[0].size
    alpha = np.maximum.reduce([np.asarray(c.getchannel("A")) for c in cells])
    ys, xs = np.nonzero(alpha > ALPHA_FLOOR)
    pivot = cw / 2.0
    half = int(math.ceil(max(pivot - xs.min(), xs.max() + 1 - pivot))) + PAD
    half = min(half, int(math.floor(pivot)))                     # never past the cell
    x0, x1 = int(pivot) - half, int(pivot) + half
    y0 = max(0, int(ys.min()) - PAD)
    y1 = min(ch, int(ys.max()) + 1 + PAD)
    feet = ch - float(an["feet_gap"])                            # the sole line in the cell
    y1 = max(y1, min(ch, int(math.ceil(feet)) + PAD))            # keep the feet line inside the image
    os.makedirs(OUT, exist_ok=True)
    frames = []
    for n, cell in enumerate(cells):
        name = f"{cid}_idle_{n}.png"
        img = cell.crop((x0, y0, x1, y1))
        a = np.asarray(img).copy()
        a[a[..., 3] == 0, :3] = 0                                # deterministic colour under alpha 0
        Image.fromarray(a, "RGBA").save(os.path.join(OUT, name), optimize=False, compress_level=9)
        frames.append({"file": f"select_heroes/{name}", "size": [img.width, img.height],
                       "pivot_x": pivot - x0, "feet_y": feet - y0,
                       "source": f"{an['sheet']} atlas region {an['regions'][n]} ({an['sources'][n]})"})
    return {"id": cid, "fps": float(an["speed"]), "loop": bool(an["loop"]), "texel_scale": m.get("texel_scale", 2.0),
            "frames": frames}


def main():
    heroes = [bake(cid) for cid in roster()]
    doc = {
        "about": "TG-ART select hero idle frames (UI_SPEC_GLASS 3.7): the retro v2 idle loop of every roster member, "
                 "cropped to one box per hero, symmetric about the pivot. pivot_x and feet_y are image pixels from "
                 "the top-left; draw at select_scale with (pivot_x, feet_y) on (the side's centre x, select_feet_y); "
                 "flip P2. Rebuilt by tools/asset_src/glass/build_select_heroes.py from the roster in "
                 "resources/Content/content_manifest.csv.",
        "select_scale": SELECT_SCALE, "select_feet_y": SELECT_FEET_Y, "heroes": heroes,
    }
    with open(os.path.join(OUT, "select_heroes.json"), "w", encoding="utf-8", newline="\n") as fh:
        json.dump(doc, fh, indent=1)
        fh.write("\n")
    for h in heroes:
        f = h["frames"][0]
        print(f"{h['id']:12s} {f['size'][0]:4d} x {f['size'][1]:4d}  pivot {f['pivot_x']:6.1f}  feet {f['feet_y']:6.1f}"
              f"  fps {h['fps']}")


if __name__ == "__main__":
    main()
