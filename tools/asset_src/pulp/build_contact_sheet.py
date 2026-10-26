"""Contact sheet of the pulp kit: writes PNGs into D:/Projects/ftt-scratch/ur-captures/art/ (outside the repo).
usage: py tools/asset_src/pulp/build_contact_sheet.py
"""
import json
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
KIT = os.path.join(REPO, "assets", "sprites", "ui", "pulp")
OUTDIR = "D:/Projects/ftt-scratch/ur-captures/art"
os.makedirs(OUTDIR, exist_ok=True)
BG = (92, 74, 104, 255)


def checker(w, h, a=(88, 72, 100), b=(70, 56, 82), s=16):
    arr = np.zeros((h, w, 3), dtype="uint8")
    yy, xx = np.mgrid[0:h, 0:w]
    m = ((xx // s + yy // s) % 2) == 0
    arr[m] = a
    arr[~m] = b
    return Image.fromarray(arr, "RGB").convert("RGBA")


def tint(im, rgb):
    a = np.asarray(im.convert("RGBA")).astype("float32")
    a[..., 0], a[..., 1], a[..., 2] = rgb
    return Image.fromarray(a.astype("uint8"), "RGBA")


def icon_comp(name, rgb, size):
    base = os.path.join(KIT, "icons", "ico_" + name)
    fill, ink, sh = (Image.open(base + s).convert("RGBA") for s in ("_fill.png", "_ink.png", "_sh.png"))
    k = size / 32.0
    sh = sh.resize((int(sh.width * size / 64 if sh.width == 64 else sh.width), int(sh.height * size / 64 if sh.height == 64 else sh.height)))
    out = Image.new("RGBA", (size + 4, size + 4), (0, 0, 0, 0))
    fl, ik = fill.resize(sh.size), ink.resize(sh.size)
    out.alpha_composite(sh, (2, 2))
    out.alpha_composite(tint(fl, rgb), (0, 0))
    out.alpha_composite(ik, (0, 0))
    return out


def stretch9(path, margins, w, h):
    im = Image.open(path).convert("RGBA")
    L, T, R, B = margins
    W, H = im.size
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    xs = [(0, L, 0, L), (L, W - R, L, w - R), (W - R, W, w - R, w)]
    ys = [(0, T, 0, T), (T, H - B, T, h - B), (H - B, H, h - B, h)]
    for sx0, sx1, dx0, dx1 in xs:
        for sy0, sy1, dy0, dy1 in ys:
            if sx1 <= sx0 or sy1 <= sy0 or dx1 <= dx0 or dy1 <= dy0:
                continue
            part = im.crop((sx0, sy0, sx1, sy1)).resize((dx1 - dx0, dy1 - dy0), Image.NEAREST)
            out.alpha_composite(part, (dx0, dy0))
    return out


def main():
    cat = json.load(open(os.path.join(HERE, "catalog.json"), encoding="utf-8"))
    sheet = checker(1900, 2400)
    d = ImageDraw.Draw(sheet)
    x = y = 16
    rowh = 0

    def put(im, label):
        nonlocal x, y, rowh
        if x + im.width + 16 > sheet.width:
            x = 16
            y += rowh + 34
            rowh = 0
        sheet.alpha_composite(im, (x, y))
        d.text((x, y + im.height + 2), label, fill=(255, 255, 255, 255))
        x += max(im.width, len(label) * 6) + 18
        rowh = max(rowh, im.height + 2)

    # 9-slices at a typical size
    for name, e in sorted(cat.items()):
        if "nine_slice_margins_LTRB" not in e or name.startswith("btn_*"):
            continue
        L, T, R, B = e["expand_margins_LTRB"] if "expand_margins_LTRB" in e else (0, 0, 0, 0)
        m = e["nine_slice_margins_LTRB"]
        W, H = e["size"]
        w = max(m[0] + m[2] + 120, W)
        h = H if name.startswith(("btn_", "bar_", "tab_", "slider", "toggle", "tag_", "banner", "boss_", "stamp", "key")) else max(m[1] + m[3] + 80, H)
        put(stretch9(os.path.join(KIT, e["file"]), m, w, h), name)
    sheet.crop((0, 0, 1900, min(y + rowh + 40, 2400))).save(os.path.join(OUTDIR, "sheet_plates.png"))

    # fixed sprites + tiles
    sheet = checker(1900, 1500)
    d = ImageDraw.Draw(sheet)
    x = y = 16
    rowh = 0
    for name, e in sorted(cat.items()):
        if "nine_slice_margins_LTRB" in e or "file" not in e or "{" in e["file"] or e.get("layers") or e["file"].endswith(".tres"):
            continue
        im = Image.open(os.path.join(KIT, e["file"])).convert("RGBA")
        if e.get("tile"):
            t = Image.new("RGBA", (128, 128))
            for i in range(0, 128, im.width):
                for j in range(0, 128, im.height):
                    t.alpha_composite(im, (i, j))
            im = t
            if name.startswith("dot"):
                bgc = Image.new("RGBA", im.size, (40, 40, 60, 255))
                bgc.alpha_composite(tint(im, (255, 255, 255)))
                im = bgc
        if im.width > 600:
            im = im.resize((im.width // 3, im.height // 3))
        put(im, name)
    sheet.crop((0, 0, 1900, min(y + rowh + 40, 1500))).save(os.path.join(OUTDIR, "sheet_sprites.png"))

    # icons
    names = sorted(f[len("ico_"):-len("_fill.png")] for f in os.listdir(os.path.join(KIT, "icons")) if f.endswith("_fill.png"))
    sheet = checker(1500, 520)
    d = ImageDraw.Draw(sheet)
    x = y = 16
    rowh = 0
    gold = (243, 201, 91)
    for n in names:
        for size in (64, 28):
            put(icon_comp(n, gold, size), n if size == 64 else "")
    sheet.crop((0, 0, 1500, min(y + rowh + 40, 520))).save(os.path.join(OUTDIR, "sheet_icons.png"))
    ability_sheet()
    print("ok")


def ability_sheet():
    """The 144 px ability icons (one row per roster character) plus the baked hero poses."""
    d = os.path.join(KIT, "ability_icons")
    ab = json.load(open(os.path.join(d, "ability_icons.json"), encoding="utf-8"))
    ids = []
    for k in ab:
        cid = k.split("_special_")[0].split("_movement")[0].split("_ultimate")[0]
        if cid not in ids:
            ids.append(cid)
    slots = ["special_1", "special_2", "movement", "ultimate"]
    sheet = Image.new("RGBA", (130 + 4 * 160 + 700, len(ids) * 160 + 20), (42, 36, 52, 255))
    dr = ImageDraw.Draw(sheet)
    for r, cid in enumerate(ids):
        dr.text((8, r * 160 + 70), cid, fill=(255, 255, 255, 255))
        for k, s in enumerate(slots):
            sheet.alpha_composite(Image.open(os.path.join(d, f"{cid}_{s}.png")).convert("RGBA"), (130 + k * 160, r * 160 + 8))
    heroes = json.load(open(os.path.join(KIT, "cover_heroes", "cover_heroes.json"), encoding="utf-8"))
    x = 130 + 4 * 160 + 10
    for name in sorted(heroes):
        im = Image.open(os.path.join(KIT, heroes[name]["file"])).convert("RGBA")
        im = im.resize((int(im.width * 0.7), int(im.height * 0.7)), Image.LANCZOS)
        sheet.alpha_composite(im, (x, 20))
        dr.text((x, 20 + im.height + 4), name, fill=(255, 255, 255, 255))
        x += im.width + 20
    sheet.save(os.path.join(OUTDIR, "sheet_ability_heroes.png"))


if __name__ == "__main__":
    main()
