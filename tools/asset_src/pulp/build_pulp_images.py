"""PIL-made rasters of the pulp kit (everything that is image-derived or a per-pixel pattern):
cover_art.png (posterized Pompeii key art), rays.png, halftone_screen.png, select_split.png,
card_field.png, card_halftone.png, portrait_halftone.png, and the GradientTexture2D .tres files.
Also merges tools/asset_src/pulp/catalog_svg.json with these entries into catalog.json.

usage (repo root): py tools/asset_src/pulp/build_pulp_images.py
"""
import json
import math
import os
import random

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
OUT = os.path.join(REPO, "assets", "sprites", "ui", "pulp")
os.makedirs(OUT, exist_ok=True)
W, H = 1920, 1080
CAT = json.load(open(os.path.join(HERE, "catalog_svg.json"), encoding="utf-8"))


def rp(*p):
    return os.path.join(REPO, *p)


def load(path):
    return Image.open(path).convert("RGBA")


def paste_clip(dst, src, xy):
    x, y = xy
    sx0, sy0 = max(0, -x), max(0, -y)
    dx0, dy0 = max(0, x), max(0, y)
    sx1, sy1 = min(src.width, dst.width - x), min(src.height, dst.height - y)
    if sx1 > sx0 and sy1 > sy0:
        dst.alpha_composite(src.crop((sx0, sy0, sx1, sy1)), (dx0, dy0))


def glow(size, xy, radius, color, strength=1.0):
    layer = Image.new("RGBA", size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    x, y = xy
    for i in range(48, 0, -1):
        t = i / 48
        a = int(255 * strength * (1 - t) ** 2.0)
        r = radius * t
        d.ellipse((x - r, y - r, x + r, y + r), fill=color + (a,))
    return layer.filter(ImageFilter.GaussianBlur(radius / 18))


def add_layer(base, layer):
    b = np.asarray(base.convert("RGBA")).astype("float32")
    l = np.asarray(layer.convert("RGBA")).astype("float32")
    a = l[..., 3:4] / 255.0
    b[..., :3] = np.clip(b[..., :3] + l[..., :3] * a, 0, 255)
    return Image.fromarray(b.astype("uint8"), "RGBA")


def gradient_v(size, stops):
    w, h = size
    arr = np.zeros((h, w, 4), dtype="float32")
    ys = np.linspace(0, 1, h)
    for ch in range(4):
        arr[..., ch] = np.interp(ys, [s[0] for s in stops], [s[1][ch] for s in stops])[:, None]
    return Image.fromarray(arr.astype("uint8"), "RGBA")


def lift(im, gamma=0.8, gain=1.0):
    a = np.asarray(im.convert("RGBA")).astype("float32") / 255.0
    a[..., :3] = np.clip((a[..., :3] ** gamma) * gain, 0, 1)
    return Image.fromarray((a * 255).astype("uint8"), "RGBA")


def tile_row(tile, width):
    row = Image.new("RGBA", (width, tile.height), (0, 0, 0, 0))
    x = 0
    while x < width:
        row.alpha_composite(tile, (x, 0))
        x += tile.width
    return row


def compose_pompeii():
    """The mockup's Level 6 scene composition (prep_assets.compose_pompeii)."""
    random.seed(79)
    base = load(rp("assets/environments/pompeii/pompeii_sky.png"))
    base = lift(base, 0.72, 1.25)
    hot = gradient_v((W, H), [(0.0, (60, 18, 36, 120)), (0.45, (150, 50, 30, 150)), (0.72, (255, 120, 40, 190)),
                              (0.9, (255, 150, 60, 120)), (1.0, (60, 20, 10, 0))])
    base = add_layer(base, hot)
    paste_clip(base, lift(load(rp("assets/environments/pompeii/pompeii_vesuvius.png")), 0.75, 1.15), (1500 - 1300, 0))
    base = add_layer(base, glow((W, H), (1500, 300), 520, (255, 110, 30), 0.85))
    base = add_layer(base, glow((W, H), (1500, 322), 170, (255, 200, 110), 0.9))
    sky = lift(load(rp("assets/environments/pompeii/pompeii_skyline.png")), 0.75, 1.2)
    paste_clip(base, sky, (0, H - sky.height - 40))
    ruins = lift(load(rp("assets/environments/pompeii/pompeii_ruins.png")), 0.75, 1.2)
    paste_clip(base, ruins, (0, H - ruins.height - 130))
    base = add_layer(base, glow((W, H), (330, 820), 480, (255, 120, 45), 0.45))
    base = add_layer(base, glow((W, H), (1000, 860), 520, (255, 100, 40), 0.35))
    floor_y = 880
    base.alpha_composite(gradient_v((W, H - floor_y), [(0, (46, 34, 28, 255)), (0.35, (26, 19, 16, 255)), (1, (8, 6, 6, 255))]), (0, floor_y))
    ft = lift(load(rp("assets/environments/pompeii/floor_stone.png")), 0.8, 1.15)
    base.alpha_composite(tile_row(ft, W + ft.width).crop((0, 0, W, ft.height)), (0, floor_y))
    base.alpha_composite(Image.new("RGBA", (W, 4), (255, 175, 95, 120)), (0, floor_y))
    em = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ed = ImageDraw.Draw(em)
    for _ in range(230):
        x, y = random.randint(0, W), random.randint(40, 870)
        r = random.choice([1, 1, 1, 2, 2, 3])
        a = random.randint(110, 240)
        col = random.choice([(255, 170, 70), (255, 120, 40), (255, 215, 130)])
        ed.ellipse((x - r, y - r, x + r, y + r), fill=col + (a,))
    base.alpha_composite(em.filter(ImageFilter.GaussianBlur(0.7)))
    return base


LEVELS = np.array([0, .13, .26, .40, .54, .68, .82, .92, 1.0], dtype="float32")


def posterize(im):
    a = np.asarray(im.convert("RGB")).astype("float32") / 255.0
    idx = np.abs(a[..., None] - LEVELS[None, None, None, :]).argmin(axis=-1)
    a = LEVELS[idx]
    lum = (a * np.array([0.2126, 0.7152, 0.0722], dtype="float32")).sum(-1, keepdims=True)
    a = np.clip(lum + (a - lum) * 1.2, 0, 1)            # saturate 1.2
    a = np.clip((a - 0.5) * 1.08 + 0.5, 0, 1)            # contrast 1.08
    return Image.fromarray((a * 255 + 0.5).astype("uint8"), "RGB")


def save(im, name, serves, notes="", mip=False):
    im.save(os.path.join(OUT, name))
    CAT[name[:-4]] = {"file": name, "size": [im.width, im.height], "serves": serves, "mipmaps": mip,
                      **({"notes": notes} if notes else {})}


def rays():
    ys, xs = np.mgrid[0:H, 0:W].astype("float32")
    cx, cy = 614.0, 713.0
    dx, dy = xs + 0.5 - cx, ys + 0.5 - cy
    r = np.hypot(dx, dy)
    th = (np.degrees(np.arctan2(dx, -dy)) + 360.0) % 360.0
    t = th % 12.0
    inside = np.minimum(t, 4.0 - t)
    outside = -np.minimum(np.abs(t - 4.0), np.abs(12.0 - t))
    sd = np.where(t <= 4.0, inside, outside) * (math.pi / 180.0) * r
    cov = np.clip(sd + 0.5, 0.0, 1.0)
    out = np.zeros((H, W, 4), dtype="uint8")
    out[..., 0], out[..., 1], out[..., 2] = 244, 182, 59
    out[..., 3] = (cov * 0.17 * 255 + 0.5).astype("uint8")
    save(Image.fromarray(out, "RGBA"), "rays.png",
         "cover rays: 30 rays, 4deg lit / 8deg clear from (614,713), rgba(244,182,59,.22) baked at .17 so an ADD blend matches the mock Screen blend; blend ADD (a Screen blend does not exist in Compatibility)")


def dot_alpha(w, h, pitch, r_solid, r_end):
    ys, xs = np.mgrid[0:h, 0:w].astype("float32")
    px = ((xs + 0.5) % pitch) - pitch / 2
    py = ((ys + 0.5) % pitch) - pitch / 2
    d = np.hypot(px, py)
    return np.clip((r_end - d) / (r_end - r_solid), 0.0, 1.0)


def halftone_screen():
    a = dot_alpha(W, H, 9, 1.7, 2.2)
    mask = (0.2 + 0.7 * (np.arange(H, dtype="float32") / (H - 1)))[:, None]
    out = np.zeros((H, W, 4), dtype="uint8")
    out[..., 3] = (a * mask * 0.34 * 255 + 0.5).astype("uint8")
    save(Image.fromarray(out, "RGBA"), "halftone_screen.png",
         "full-screen halftone for the cover (layer opacity 0.55) and the select split (0.80): black dots pitch 9 r1.7-2.2, alpha .34 x vertical mask .2 (top) to .9 (bottom) baked; MUL blend or plain alpha")


def select_split():
    ys, xs = np.mgrid[0:H, 0:W].astype("float32")
    ang = math.radians(108)
    d = (math.sin(ang), -math.cos(ang))
    L = abs(W * d[0]) + abs(H * d[1])
    p = ((xs + 0.5 - W / 2) * d[0] + (ys + 0.5 - H / 2) * d[1]) / L + 0.5
    teal, red, blk = np.array([0x13, 0x8A, 0x94], "float32"), np.array([0xCF, 0x3A, 0x2C], "float32"), np.zeros(3, "float32")
    col = np.where((p < 0.495)[..., None], teal, np.where((p < 0.505)[..., None], blk, red))
    rx, ry = 1.10 * W, 0.90 * H
    rr = np.sqrt(((xs + 0.5 - W / 2) / rx) ** 2 + ((ys + 0.5 - H / 2) / ry) ** 2)
    va = np.clip((rr - 0.4) / 0.6, 0, 1) * 0.55
    col = col * (1 - va[..., None])
    save(Image.fromarray(np.clip(col + 0.5, 0, 255).astype("uint8"), "RGB"), "select_split.png",
         "CARD-B background: 108deg split #138A94 (P1) / #CF3A2C (P2), 1% black divider, vignette; the halftone_screen goes over it at 0.80")


def card_field():
    w, h = 188, 204
    ys, xs = np.mgrid[0:h, 0:w].astype("float32")
    cx, cy = 0.5 * w, 1.18 * h
    far = max(math.hypot(cx - 0, cy - 0), math.hypot(cx - w, cy - 0))
    t = np.clip(np.hypot(xs + 0.5 - cx, ys + 0.5 - cy) / far, 0, 1)
    stops = [(0.0, (0xFF, 0xE0, 0x8A)), (0.42, (0xF3, 0xC9, 0x5B)), (1.0, (0xC1, 0x75, 0x2B))]
    out = np.zeros((h, w, 3), dtype="float32")
    for ch in range(3):
        out[..., ch] = np.interp(t, [s[0] for s in stops], [s[1][ch] for s in stops])
    im = Image.fromarray(out.astype("uint8"), "RGB").convert("RGBA")
    ImageDraw.Draw(im).rectangle((0, h - 4, w, h), fill=(0, 0, 0, 255))
    save(im, "card_field.png", "CARD-B photo window field (188x204): radial gradient at (50%,118%) #FFE08A / accent 42% / #C1752B, bottom 4px black rule")
    a = dot_alpha(w, h, 8, 1.5, 2.0)
    ang = math.radians(200)
    dv = (math.sin(ang), -math.cos(ang))
    L = abs(w * dv[0]) + abs(h * dv[1])
    p = ((xs + 0.5 - w / 2) * dv[0] + (ys + 0.5 - h / 2) * dv[1]) / L + 0.5
    m = np.clip((p - 0.45) / 0.55, 0, 1)
    out = np.zeros((h, w, 4), dtype="uint8")
    out[..., 3] = (a * m * 0.26 * 255 + 0.5).astype("uint8")
    save(Image.fromarray(out, "RGBA"), "card_halftone.png", "card photo halftone (188x204): black dots pitch 8, alpha .26, 200deg fade; draw over the field")
    s = 112
    ys, xs = np.mgrid[0:s, 0:s].astype("float32")
    a = dot_alpha(s, s, 9, 1.5, 2.0)
    L = abs(s * dv[0]) + abs(s * dv[1])
    p = ((xs + 0.5 - s / 2) * dv[0] + (ys + 0.5 - s / 2) * dv[1]) / L + 0.5
    m = np.clip((p - 0.45) / 0.55, 0, 1)
    out = np.zeros((s, s, 4), dtype="uint8")
    out[..., 3] = (a * m * 0.28 * 255 + 0.5).astype("uint8")
    save(Image.fromarray(out, "RGBA"), "portrait_halftone.png",
         "portrait roundel halftone (112x112 square, clip to the disc): black dots pitch 9 alpha .28, 200deg fade; scale with the roundel diameter / 112")


def gradient_tres():
    def col(h):
        h = h.lstrip("#")
        return ", ".join(f"{int(h[i:i + 2], 16) / 255:.4f}" for i in (0, 2, 4)) + ", 1"

    burst = ("[gd_resource type=\"GradientTexture2D\" load_steps=2 format=3]\n\n"
             "[sub_resource type=\"Gradient\" id=\"Gradient_burst\"]\n"
             "offsets = PackedFloat32Array(0, 0.537, 0.99, 1)\n"
             f"colors = PackedColorArray({col('#FFD36A')}, {col('#F4B63B')}, {col('#E0412F')}, {col('#E0412F')})\n\n"
             "[resource]\ngradient = SubResource(\"Gradient_burst\")\nwidth = 512\nheight = 512\nfill = 1\n"
             "fill_from = Vector2(0.5, 0.5)\nfill_to = Vector2(1, 0.5)\n")
    field = ("[gd_resource type=\"GradientTexture2D\" load_steps=2 format=3]\n\n"
             "[sub_resource type=\"Gradient\" id=\"Gradient_field\"]\n"
             "offsets = PackedFloat32Array(0, 0.42, 1)\n"
             f"colors = PackedColorArray({col('#FFE08A')}, {col('#F3C95B')}, {col('#C1752B')})\n\n"
             "[resource]\ngradient = SubResource(\"Gradient_field\")\nwidth = 188\nheight = 204\nfill = 1\n"
             "fill_from = Vector2(0.5, 1.18)\nfill_to = Vector2(1.82, 1.18)\n")
    for name, text, serves in (("burst.tres", burst, "GradientTexture2D: the menu burst's radial fill (stops 0 / .537 / .99 of the 500 px radius); burst_star.png is the baked star"),
                               ("card_field.tres", field, "GradientTexture2D: card photo field, radial from (50%,118%); card_field.png is the exact bake")):
        with open(os.path.join(OUT, name), "w", encoding="utf-8", newline="\n") as fh:
            fh.write(text)
        CAT[name[:-5] + "_tres"] = {"file": name, "serves": serves, "mipmaps": False}


def main():
    cover = posterize(compose_pompeii())
    save(cover, "cover_art.png", "MENU-B background: the Level 6 Pompeii scene (assets/environments/pompeii/*) posterized to 9 levels per channel (0 .13 .26 .40 .54 .68 .82 .92 1), saturate 1.2, contrast 1.08; opaque, no vignette")
    rays()
    halftone_screen()
    select_split()
    card_field()
    gradient_tres()
    with open(os.path.join(HERE, "catalog.json"), "w", encoding="utf-8", newline="\n") as fh:
        json.dump(CAT, fh, indent=1, sort_keys=True)
    print("images ok, catalog entries:", len(CAT))


if __name__ == "__main__":
    main()
