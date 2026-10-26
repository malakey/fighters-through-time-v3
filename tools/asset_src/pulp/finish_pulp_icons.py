"""Composites the raw icon layer rasters (tools/asset_src/pulp/raw/, written by
generate_assets.gd group=pulp_icons_raw) into the shipped layers:

  ico_<name>_fill.png  white glyph shapes (tint by modulate)
  ico_<name>_ink.png   black outline ring (outside the fill only) + black / paper details, drawn OVER the fill
  ico_<name>_sh.png    black silhouette of the whole glyph, drawn UNDER the fill at a (2,2) screen-pixel offset

star_badge_{fill,ink,sh}.png follow the same convention (the shadow offset is baked there).

usage (repo root): py tools/asset_src/pulp/finish_pulp_icons.py
"""
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import icons_geometry as G  # noqa: E402

REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
RAW = os.path.join(REPO, "tools", "asset_src", "pulp", "raw")
OUT = os.path.join(REPO, "assets", "sprites", "ui", "pulp", "icons")
OUT_ROOT = os.path.join(REPO, "assets", "sprites", "ui", "pulp")
os.makedirs(OUT, exist_ok=True)


def load(name):
    return np.asarray(Image.open(os.path.join(RAW, name + "_raw.png")).convert("RGBA")).astype("float32") / 255.0


def save(arr, path):
    Image.fromarray((np.clip(arr, 0, 1) * 255 + 0.5).astype("uint8"), "RGBA").save(path)


def over(dst, src):
    a = src[..., 3:4]
    out_a = a + dst[..., 3:4] * (1 - a)
    rgb = np.where(out_a > 0, (src[..., :3] * a + dst[..., :3] * dst[..., 3:4] * (1 - a)) / np.maximum(out_a, 1e-6), 0)
    return np.concatenate([rgb, out_a], axis=-1)


def black(alpha):
    out = np.zeros(alpha.shape + (4,), dtype="float32")
    out[..., 3] = alpha
    return out


def finish(prefix, name, outdir):
    fill, sil, top = load(f"{prefix}{name}_fill"), load(f"{prefix}{name}_sil"), load(f"{prefix}{name}_top")
    ring = black(np.clip(sil[..., 3] - fill[..., 3], 0, 1))
    ink = over(ring, top)
    shadow_a = np.maximum.reduce([sil[..., 3], top[..., 3], fill[..., 3]])
    save(fill, os.path.join(outdir, f"{prefix}{name}_fill.png"))
    save(ink, os.path.join(outdir, f"{prefix}{name}_ink.png"))
    save(black(shadow_a), os.path.join(outdir, f"{prefix}{name}_sh.png"))


for n in G.ICONS:
    finish("ico_", n, OUT)
# star badge: raw has fill, ink (full outlined silhouette) and sh
fill, ink, sh = load("star_badge_fill"), load("star_badge_ink"), load("star_badge_sh")
save(fill, os.path.join(OUT_ROOT, "star_badge_fill.png"))
save(black(np.clip(ink[..., 3] - fill[..., 3], 0, 1)), os.path.join(OUT_ROOT, "star_badge_ink.png"))
save(sh, os.path.join(OUT_ROOT, "star_badge_sh.png"))
print("icons:", len(G.ICONS), "->", OUT)
