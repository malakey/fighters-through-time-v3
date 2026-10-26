"""Bakes small static hero poses for the pulp cover (and any screen that needs one pose of a
fighter) from the retro v2 atlases, so a menu never loads a ~45 MB atlas.

Each pose is one frame of an animation (default: the `victory` frame the mockup used),
cut from its manifest region, trimmed of empty space symmetrically about the cell's horizontal
centre (the pivot stays at the image's horizontal centre) and at the top; the bottom is kept,
so the feet sit `feet_gap` px above the image's bottom edge.

usage (repo root): py tools/asset_src/pulp/build_cover_heroes.py [id[:animation[:frame]] ...]
default: joan:victory:0 einstein:victory:2 cleopatra:victory:0
Output: assets/sprites/ui/pulp/cover_heroes/<id>_<animation>.png and cover_heroes.json
"""
import json
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
OUT = os.path.join(REPO, "assets", "sprites", "ui", "pulp", "cover_heroes")
DEFAULT = ["joan:victory:0", "einstein:victory:2", "cleopatra:victory:0"]


def bake(spec: str) -> dict:
    parts = spec.split(":")
    cid = parts[0]
    anim = parts[1] if len(parts) > 1 else "victory"
    idx = int(parts[2]) if len(parts) > 2 else 0
    m = json.load(open(os.path.join(REPO, "tools", "retro_sprites", "manifests", f"{cid}.json"), encoding="utf-8"))
    an = m["animations"][anim]
    x, y, w, h = an["regions"][idx]
    atlas = Image.open(os.path.join(REPO, "assets", "sprites", "characters", cid, "retro", f"{cid}_{an['sheet']}_atlas.png")).convert("RGBA")
    im = atlas.crop((x, y, x + w, y + h))
    a = np.asarray(im.getchannel("A"))
    ys, xs = np.nonzero(a > 6)
    cx = im.width / 2
    half = max(cx - xs.min(), xs.max() + 1 - cx) + 4
    x0, x1 = int(max(0, cx - half)), int(min(im.width, cx + half))
    y0 = int(max(0, ys.min() - 4))
    im = im.crop((x0, y0, x1, im.height))
    os.makedirs(OUT, exist_ok=True)
    name = f"{cid}_{anim}.png"
    im.save(os.path.join(OUT, name))
    return {"file": f"cover_heroes/{name}", "size": [im.width, im.height], "feet_px_from_bottom": int(an["feet_gap"]),
            "pivot_x": im.width / 2, "texel_scale": m.get("texel_scale", 2.0), "source": f"{cid}/{an['sheet']} region {idx} of {anim}"}


def main():
    specs = sys.argv[1:] or DEFAULT
    index = {}
    path = os.path.join(OUT, "cover_heroes.json")
    if os.path.exists(path) and sys.argv[1:]:
        index = json.load(open(path, encoding="utf-8"))
    for s in specs:
        r = bake(s)
        index[os.path.basename(r["file"])[:-4]] = r
        print(s, "->", r["file"], r["size"], "feet", r["feet_px_from_bottom"])
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        json.dump(index, fh, indent=1, sort_keys=True)


if __name__ == "__main__":
    main()
