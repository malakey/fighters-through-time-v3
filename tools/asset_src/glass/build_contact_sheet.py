"""TG-ART: review sheets of the Temporal Glass kit, written to D:/Projects/ftt-scratch/tg-captures/art/.

Not part of the shipped kit: it reads assets/sprites/ui/glass/ (and glass_kit.json when present) and renders
  glass_kit_sheet.png      every PNG at 1:1 and 3x, on a checkerboard and on the dark field;
  glass_ninesliced.png     the nine-slices drawn the way Godot's StyleBoxTexture draws them (texture margins
                           1:1, centre and edges stretched or tiled, expand margins outside the rect) at the spec's
                           sizes: 560 x 68 chevrons in every state (focus drawn over normal and over pressed, as the
                           engine does), 290 x 58 compact, 620 x 214 and 600 x 768 slabs, tags, tabs, fields;
  glass_icons_sheet.png    every icon at 64 px in both weights, then at its display sizes with its tint, on dark and
                           on bright art;
  glass_heroes_sheet.png   the select heroes' baked idle frames with their feet line and pivot;
  compare_*.png            side by sides with the spec's reference crops (glass_spec_assets/crops/, renders with the
                           browser's backdrop blur, which the build cannot have): the menu column, the pause panel and
                           the Story HUD's left column, each also as a 50 % overlay. Labels, markers and the panel
                           sheen in these are approximations drawn here (the shipped ones are code-drawn by the
                           TG-WIDGETS decor); they exist only to judge the art in context.

usage (repo root): py tools/asset_src/glass/build_contact_sheet.py [out_dir]
"""
from __future__ import annotations

import json
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import glass_svg as G  # noqa: E402

KIT = os.path.join(G.REPO, "assets", "sprites", "ui", "glass")
OUT = sys.argv[1] if len(sys.argv) > 1 else r"D:/Projects/ftt-scratch/tg-captures/art"
SPEC_DIR = r"D:/Projects/fighters-through-time-docs-2/mockups/ui_redesign_2026-10-25"
CROPS = os.path.join(SPEC_DIR, "glass_spec_assets", "crops")
BG0 = (5, 7, 26, 255)
INK, INK2, INK3 = (243, 240, 230), (192, 186, 166), (138, 131, 116)
CINZEL = os.path.join(G.REPO, "assets", "fonts", "cinzel", "Cinzel-Variable.ttf")
OUTFIT = os.path.join(G.REPO, "assets", "fonts", "outfit", "Outfit-Variable.ttf")


def font(path, size, weight):
    f = ImageFont.truetype(path, int(round(size)))
    try:
        f.set_variation_by_axes([weight])
    except Exception:  # noqa: BLE001 - a static build of the font still draws
        pass
    return f


LABEL = font(OUTFIT, 15, 500)
SMALL = font(OUTFIT, 12, 500)


def tex(name):
    return Image.open(os.path.join(KIT, name)).convert("RGBA")


def entry(stem):
    e = dict(G.KIT[stem])
    spec = e.get("spec", {})
    e.setdefault("margins", spec.get("margins", (0, 0, 0, 0)))
    e.setdefault("expand", spec.get("expand", (0, 0, 0, 0)))
    return e


# ---- a StyleBoxTexture in Python -------------------------------------------------------------------------------

def nine(stem, w, h):
    """Draws `stem` the way StyleBoxTexture does on a w x h rect; returns (image, offset of the rect in it)."""
    e = entry(stem)
    t = tex(e["file"]).convert("RGBa")
    l, tp, r, b = e["margins"]
    el, et, er, eb = e["expand"]
    W, H = int(round(w + el + er)), int(round(h + et + eb))
    tw, th = t.size
    sx, dx = [0, l, tw - r, tw], [0, l, W - r, W]
    sy, dy = [0, tp, th - b, th], [0, tp, H - b, H]
    out = Image.new("RGBa", (W, H), (0, 0, 0, 0))
    for i in range(3):
        for j in range(3):
            if sx[i + 1] <= sx[i] or sy[j + 1] <= sy[j] or dx[i + 1] <= dx[i] or dy[j + 1] <= dy[j]:
                continue
            piece = t.crop((sx[i], sy[j], sx[i + 1], sy[j + 1]))
            pw, ph = dx[i + 1] - dx[i], dy[j + 1] - dy[j]
            if e.get("stretch") == "tile" and i == 1:
                n = int(math.ceil(pw / piece.width))
                strip = Image.new("RGBa", (n * piece.width, piece.height))
                for k in range(n):
                    strip.paste(piece, (k * piece.width, 0))
                piece = strip.crop((0, 0, pw, piece.height)).resize((pw, ph), Image.BILINEAR)
            else:
                piece = piece.resize((pw, ph), Image.BILINEAR)
            out.paste(piece, (dx[i], dy[j]))
    return out.convert("RGBA"), (el, et)


def put(canvas, img, xy):
    x, y = int(round(xy[0])), int(round(xy[1]))
    cx0, cy0 = max(0, -x), max(0, -y)
    if cx0 or cy0:
        img = img.crop((cx0, cy0, img.width, img.height))
        x, y = x + cx0, y + cy0
    if x >= canvas.width or y >= canvas.height:
        return
    img = img.crop((0, 0, min(img.width, canvas.width - x), min(img.height, canvas.height - y)))
    canvas.alpha_composite(img, (x, y))


def put_nine(canvas, stem, x, y, w, h):
    img, (ox, oy) = nine(stem, w, h)
    put(canvas, img, (x - ox, y - oy))


def text(canvas, xy, s, f, fill, tracking=0.0, anchor="lm"):
    d = ImageDraw.Draw(canvas)
    if not tracking:
        d.text(xy, s, font=f, fill=fill, anchor=anchor)
        return
    x, y = xy
    for ch in s:
        d.text((x, y), ch, font=f, fill=fill, anchor=anchor)
        x += f.getlength(ch) + tracking


def tracked_width(s, f, tracking):
    return sum(f.getlength(ch) + tracking for ch in s) - tracking


def checker(w, h, c=10):
    im = Image.new("RGBA", (w, h), (60, 60, 70, 255))
    d = ImageDraw.Draw(im)
    for y in range(0, h, c):
        for x in range(0, w, c):
            if (x // c + y // c) % 2:
                d.rectangle((x, y, x + c - 1, y + c - 1), fill=(90, 90, 100, 255))
    return im


def cover(path, w, h):
    im = Image.open(path).convert("RGBA")
    s = max(w / im.width, h / im.height)
    im = im.resize((int(math.ceil(im.width * s)), int(math.ceil(im.height * s))), Image.LANCZOS)
    x0, y0 = (im.width - w) // 2, (im.height - h) // 2
    return im.crop((x0, y0, x0 + w, y0 + h))


def menu_art():
    art = cover(os.path.join(G.REPO, "assets", "backgrounds", "menu", "temporal_glass_main_menu_background.png"),
                1920, 1080)
    # the menu scrim of 1.3, evaluated directly (90 deg: .96 0, .84 26 %, .35 48 %, 0 66 %)
    x = np.arange(1920) / 1919.0
    a = np.interp(x, [0, .26, .48, .66, 1.0], [.96, .84, .35, 0, 0])
    scrim = np.zeros((1080, 1920, 4), dtype=np.uint8)
    scrim[..., :3] = (4, 7, 20)
    scrim[..., 3] = np.round(a * 255)[None, :]
    art.alpha_composite(Image.fromarray(scrim, "RGBA"))
    return art


MOCK_SCENE = os.path.join(SPEC_DIR, "_build", "cache", "img", "scene_pompeii.webp")


def mock_scene():
    """The mock's own Pompeii composition (read-only, outside the repo) when present, so a comparison sits on the
    same backdrop as the crop; else the repo's Pompeii layers."""
    if os.path.exists(MOCK_SCENE):
        return Image.open(MOCK_SCENE).convert("RGBA").resize((1920, 1080))
    return pompeii()


def pompeii():
    base = os.path.join(G.REPO, "assets", "environments", "pompeii")
    im = cover(os.path.join(base, "pompeii_sky.png"), 1920, 1080)
    for layer in ("pompeii_vesuvius.png", "pompeii_skyline.png", "pompeii_ruins.png"):
        p = os.path.join(base, layer)
        if os.path.exists(p):
            lay = Image.open(p).convert("RGBA")
            s = 1920 / lay.width
            lay = lay.resize((1920, int(round(lay.height * s))), Image.LANCZOS)
            put(im, lay, (0, 1080 - lay.height))
    return im


# ---- code-drawn parts approximated for context (the shipped ones belong to TG-WIDGETS) -------------------------

def diamond_marker(canvas, cx, cy, state):
    s = 4
    size = 60
    lay = Image.new("RGBA", (size * s, size * s), (0, 0, 0, 0))
    d = ImageDraw.Draw(lay)
    c = size * s / 2
    half = 7 * math.sqrt(2) * s

    def dia(hh):
        return [(c, c - hh), (c + hh, c), (c, c + hh), (c - hh, c)]
    if state == "focus":
        glow = Image.new("RGBA", lay.size, (0, 0, 0, 0))
        ImageDraw.Draw(glow).polygon(dia(half), fill=(255, 212, 92, 255))
        glow = glow.filter(ImageFilter.GaussianBlur(8 * s))
        lay.alpha_composite(glow)
        d.polygon(dia(half + 5.5 * s), fill=(243, 201, 91, 77))
        d.polygon(dia(half + 1.5 * s), fill=(255, 255, 255, 255))
        d.polygon(dia(half), fill=(255, 212, 92, 255))
    elif state == "selected":
        d.polygon(dia(half), fill=(243, 201, 91, 255))
    else:
        d.polygon(dia(half + 0.75 * s), fill=(243, 201, 91, 140))
        d.polygon(dia(half - 0.75 * s), fill=(0, 0, 0, 0))
        inner = Image.new("L", lay.size, 0)
        ImageDraw.Draw(inner).polygon(dia(half - 0.75 * s), fill=255)
        lay.putalpha(Image.fromarray(np.minimum(np.asarray(lay.getchannel("A")),
                                                255 - np.asarray(inner)).astype(np.uint8)))
    lay = lay.resize((size, size), Image.LANCZOS)
    put(canvas, lay, (cx - size / 2, cy - size / 2))


def prompt(canvas, cx, cy, letter, colour, r=18):
    s = 4
    lay = Image.new("RGBA", ((2 * r + 4) * s, (2 * r + 4) * s), (0, 0, 0, 0))
    d = ImageDraw.Draw(lay)
    c = lay.width / 2
    d.ellipse((c - r * s, c - r * s, c + r * s, c + r * s), outline=colour, width=2 * s)
    lay = lay.resize((2 * r + 4, 2 * r + 4), Image.LANCZOS)
    put(canvas, lay, (cx - r - 2, cy - r - 2))
    text(canvas, (cx, cy + 1), letter, font(OUTFIT, 19, 700), colour, anchor="mm")


BTN_FONT = font(CINZEL, 34, 700)
BTN_FONT_SM = font(CINZEL, 24.48, 700)


def chevron_row(canvas, x, y, w, h, label, state, compact=False):
    """A BTN-A row: the state box, then (if focused) the focus overlay, as Godot draws them."""
    slide = 14 if state in ("focus", "focus_pressed") else 0
    if state == "focus_pressed":
        slide = 6
    base = {"normal": "glass_btn_normal", "focus": "glass_btn_normal", "selected": "glass_btn_selected",
            "pressed": "glass_btn_pressed", "focus_pressed": "glass_btn_pressed",
            "disabled": "glass_btn_disabled", "hover": "glass_btn_hover"}[state]
    put_nine(canvas, base, x + slide, y, w, h)
    if state in ("focus", "focus_pressed"):
        put_nine(canvas, "glass_btn_focus", x + slide, y, w, h)
    colour = {"normal": INK2, "selected": INK, "disabled": INK3}.get(state, (255, 255, 255))
    f = BTN_FONT_SM if compact else BTN_FONT
    lx = x + slide + (52 if compact else 58)
    text(canvas, (lx, y + h / 2), label.upper(), f, colour, tracking=(3.18 if compact else 4.42))
    mk = "focus" if state in ("focus", "focus_pressed", "hover") else ("selected" if state == "selected" else "normal")
    diamond_marker(canvas, x + slide + (29 if compact else 31), y + h / 2, mk)
    if state in ("focus", "hover") and not compact:
        prompt(canvas, x + slide + w - 72, y + h / 2, "A", (255, 255, 255))


def sheen(canvas, x, y, w, h):
    """The 3.1 sheen and streak lines over a slab (GlassSheen's job in the build), for context only."""
    s = 4
    W, H = int(round(w)), int(round(h))
    yy, xx = np.mgrid[0:H * s, 0:W * s] / s
    bx, by, bw, bh = 1.5, 1.5, w - 3, h - 3
    t = G.css_t(112, (bx, by, bw, bh), xx, yy)
    a = np.where(t <= 0.34, 0.05, 0.0) + np.where((t >= .57) & (t <= .575), .075, 0) + \
        np.where((t >= .63) & (t <= .634), .04, 0)
    mask = Image.new("L", (W * s, H * s), 0)
    ImageDraw.Draw(mask).polygon([(px * s, py * s) for px, py in G.chamfer_fill(0, 0, w, h, 18)], fill=255)
    a = a * (np.asarray(mask) / 255.0)
    lay = np.zeros((H * s, W * s, 4), dtype=np.float64)
    lay[..., :3] = 255 * a[..., None]
    lay[..., 3] = a
    pm = lay.reshape(H, s, W, s, 4).mean(axis=(1, 3))
    al = pm[..., 3]
    rgb = np.where(al[..., None] > 0, pm[..., :3] / np.maximum(al[..., None], 1e-9), 0)
    img = Image.fromarray(np.concatenate([rgb, al[..., None] * 255], -1).round().clip(0, 255).astype(np.uint8),
                          "RGBA")
    put(canvas, img, (x, y))


def caption(canvas, xy, s, fill=(220, 220, 230, 255)):
    text(canvas, xy, s, LABEL, fill, anchor="la")


# ---- the sheets ----------------------------------------------------------------------------------------------

def sheet_kit():
    files = [e["file"] for e in G.KIT.values()]
    cell_w, cell_h = 420, 330
    cols = 4
    rows = int(math.ceil(len(files) / cols))
    im = Image.new("RGBA", (cols * cell_w, rows * cell_h + 40), BG0)
    caption(im, (10, 10), "TG-ART glass kit: every nine-slice / texture at 1:1 (left) and 3x on a checkerboard")
    for i, f in enumerate(files):
        x0, y0 = (i % cols) * cell_w, 40 + (i // cols) * cell_h
        t = tex(f)
        caption(im, (x0 + 8, y0 + 4), f"{f}  {t.width}x{t.height}")
        put(im, t, (x0 + 10, y0 + 30))
        z = t.resize((t.width * 3, t.height * 3), Image.NEAREST)
        ch = checker(min(z.width, cell_w - 20 - t.width - 20), min(z.height, cell_h - 40))
        ch.alpha_composite(z.crop((0, 0, ch.width, ch.height)))
        put(im, ch, (x0 + t.width + 30, y0 + 30))
    im.save(os.path.join(OUT, "glass_kit_sheet.png"))


def sheet_ninesliced():
    W, H = 1920, 1860
    im = Image.new("RGBA", (W, H), BG0)
    art = menu_art()
    put(im, art.crop((0, 0, 1920, 760)), (0, 0))
    caption(im, (20, 10), "560 x 68 chevrons over the menu art: under the scrim (left) and over the bright art (right): normal / focus "
                          "(normal + focus overlay) / hover box / selected / pressed + focus / pressed alone / disabled")
    states = [("normal", "Fighter Mode"), ("focus", "Story Mode"), ("hover", "Hover Box"),
              ("selected", "Selected"), ("focus_pressed", "Pressed+Focus"), ("pressed", "Pressed Alone"),
              ("disabled", "Disabled")]
    for k, (st, lab) in enumerate(states):
        chevron_row(im, 96, 50 + k * 79, 560, 68, lab, st)
        chevron_row(im, 1000, 50 + k * 79, 560, 68, lab, st)
    y = 640
    caption(im, (20, y), "compact 290 x 58: normal / focus / pressed+focus / disabled")
    for k, (st, lab) in enumerate([("normal", "Back"), ("focus", "Move List"), ("focus_pressed", "Systems Card"),
                                   ("disabled", "Locked")]):
        chevron_row(im, 96 + k * 330, y + 30, 290, 58, lab, st, compact=True)
    # slabs over Pompeii
    scene = pompeii().crop((0, 80, 1920, 1080))
    dim = Image.new("RGBA", scene.size, (3, 5, 14, 179))
    scene.alpha_composite(dim)
    put(im, scene, (0, 780))
    caption(im, (20, 790), "slabs 620 x 214 and 620 x 155 (Vitals, Abilities) and 600 x 768 (pause) over the dimmed "
                           "Pompeii scene; right: same with the 3.1 sheen preview (GlassSheen draws it in the build)")
    for k, (w, h, x, yy, tg) in enumerate([(620, 214, 40, 830, 107), (620, 155, 40, 1080, 132)]):
        put_nine(im, "glass_panel", x, yy, w, h)
        put_nine(im, "glass_tag", x + 26, yy - 11, tg, 24)
        text(im, (x + 26 + 12, yy - 11 + 12), ("VITALS" if k == 0 else "ABILITIES"), font(OUTFIT, 18, 500), INK,
             tracking=2.88)
    put_nine(im, "glass_panel", 700, 830, 600, 768)
    sheen(im, 700, 830, 600, 768)
    put_nine(im, "glass_panel", 1340, 830, 540, 214)
    sheen(im, 1340, 830, 540, 214)
    put_nine(im, "glass_tag", 726, 819, 189, 24)
    text(im, (726 + 12, 819 + 12), "POMPEII 79 AD", font(OUTFIT, 18, 500), INK, tracking=2.88)
    put_nine(im, "glass_tag", 1366, 819, 233, 24)
    text(im, (1366 + 12, 819 + 12), "CONTINUE · SLOT 1", font(OUTFIT, 18, 500), INK, tracking=2.88)
    for k, (st, lab) in enumerate([("focus", "Resume"), ("normal", "Settings"), ("normal", "Move List")]):
        chevron_row(im, 720, 980 + k * 73, 560, 64, lab, st)
    # tabs, fields, grabber, diamonds
    y = 1640
    caption(im, (20, y), "tabs (unselected / selected / selected + focus overlay / hover box), fields 300 x 44 "
                         "(normal / normal + focus overlay / hover box), grabber, diamonds (tinted accent / boss)")
    tx = 40
    tf = font(OUTFIT, 24, 700)
    for st, lab in (("glass_tab", "AUDIO"), ("glass_tab_selected", "CONTROLS"), ("focus", "DISPLAY"),
                    ("glass_tab_hover", "GAMEPLAY")):
        w = int(tracked_width(lab, tf, 3.12) + 52)
        if st == "focus":
            put_nine(im, "glass_tab_selected", tx, y + 40, w, 48)
            put_nine(im, "glass_tab_focus", tx, y + 40, w, 48)
            col = (4, 20, 26)
        else:
            put_nine(im, st, tx, y + 40, w, 48)
            col = (4, 20, 26) if st != "glass_tab" else INK2
        text(im, (tx + 26, y + 64), lab, tf, col, tracking=3.12)
        tx += w + 30
    for k, st in enumerate(("glass_field", "focus", "glass_field_hover")):
        x = 40 + k * 360
        if st == "focus":
            put_nine(im, "glass_field", x, y + 130, 300, 44)
            put_nine(im, "glass_field_focus", x, y + 130, 300, 44)
        else:
            put_nine(im, st, x, y + 130, 300, 44)
        text(im, (x + 16, y + 152), "1920 x 1080", font(OUTFIT, 22, 600), INK)
    g = tex("glass_slider_grabber.png")
    put(im, g, (1150, y + 120))
    for k, (f, tint) in enumerate((("glass_diamond_11.png", (243, 201, 91)), ("glass_diamond_12.png", (255, 90, 90)),
                                   ("glass_diamond_12.png", (243, 201, 91)))):
        d = tex(f)
        arr = np.asarray(d).astype(np.float64)
        arr[..., :3] *= np.asarray(tint) / 255.0
        put(im, Image.fromarray(arr.round().astype(np.uint8), "RGBA"), (1250 + k * 70, y + 128))
    im.save(os.path.join(OUT, "glass_ninesliced.png"))


def tinted(img, rgb):
    a = np.asarray(img).astype(np.float64)
    a[..., :3] *= np.asarray(rgb, dtype=np.float64) / 255.0
    return Image.fromarray(a.round().clip(0, 255).astype(np.uint8), "RGBA")


TINTS = {"dilation": (143, 199, 255), "venom": (255, 122, 92), "static": (143, 199, 255), "burn": (255, 122, 92),
         "root": (143, 199, 255), "suppress": (56, 242, 255), "shield": (223, 232, 255),
         "shield_off": (223, 232, 255), "hourglass": (255, 227, 154), "freeze": (255, 227, 154),
         "dust": (243, 201, 91), "star4": (0, 240, 255), "star4_filled": (0, 240, 255), "flag": (255, 240, 176),
         "lock_dormant": (138, 131, 116), "lock_suppressed": (56, 242, 255), "dormant": (138, 131, 116),
         "cross": (56, 242, 255), "defy_ready": (255, 240, 176), "defy_notready": (138, 131, 116),
         "defy_spent": (138, 131, 116), "defy_barred": (255, 90, 90), "slot_p1": (0, 240, 255),
         "slot_p2": (255, 51, 102), "check_solid": (4, 20, 26), "hourglass_caret": (255, 240, 176),
         "beacon_anchor": (255, 227, 154), "beacon_anchor_hollow": (138, 131, 116), "chevron_down": (192, 186, 166)}


def sheet_icons():
    names = G.icon_names()
    sizes = [18, 22, 24, 28.5, 30, 34, 38, 50]
    row_h = 74
    W = 1900
    im = Image.new("RGBA", (W, 60 + row_h * len(names)), BG0)
    patch = mock_scene().crop((1340, 180, 1640, 480))          # the eruption core, the brightest art in the mocks
    for yy in range(0, im.height, patch.height):
        for xx in range(1300, 1900, patch.width):
            put(im, patch, (xx, yy))
    caption(im, (10, 10), "icons: regular and bold at 64 px (white, 1:1); then tinted at 18 / 22 / 24 / 28.5 (bold "
                          "below 30) / 30 / 34 / 38 / 50 px on bg0 and on bright art (right)")
    for r, name in enumerate(names):
        y = 50 + r * row_h
        caption(im, (10, y + 22), name, (200, 200, 210, 255))
        for k, suffix in enumerate(("", "_bold")):
            t = tex(f"icons/{name}{suffix}.png")
            ch = checker(64, 64, 8)
            ch.alpha_composite(t)
            put(im, ch, (200 + k * 70, y))
        tint = TINTS.get(name, (255, 255, 255))
        for k, s in enumerate(sizes):
            t = tex(f"icons/{name}{'_bold' if s < 30 else ''}.png")
            sz = int(round(s))
            small = tinted(t, tint).resize((sz, sz), Image.LANCZOS)
            put(im, small, (360 + k * 60, y + 32 - sz / 2))
            put(im, small, (1320 + k * 70, y + 32 - sz / 2))
    im.save(os.path.join(OUT, "glass_icons_sheet.png"))


def sheet_heroes():
    path = os.path.join(KIT, "select_heroes", "select_heroes.json")
    if not os.path.exists(path):
        return
    data = json.load(open(path, encoding="utf-8"))
    heroes = data["heroes"]
    row_h = 330
    im = Image.new("RGBA", (1900, 40 + row_h * len(heroes)), BG0)
    caption(im, (10, 10), "select heroes: the baked unique idle poses 1:1 (feet line red, pivot cyan), then frame 0 "
                          "at the select's 2.05 x on a feet line, mirrored for P2")
    for r, hero in enumerate(heroes):
        y = 40 + r * row_h
        caption(im, (10, y), f"{hero['id']}  fps {hero['fps']}")
        for k, fr in enumerate(hero["frames"]):
            t = tex("select_heroes/" + fr["file"].split("/")[-1])
            ch = checker(t.width, t.height, 8)
            ch.alpha_composite(t)
            d = ImageDraw.Draw(ch)
            d.line((0, fr["feet_y"], t.width, fr["feet_y"]), fill=(255, 60, 60, 255))
            d.line((fr["pivot_x"], 0, fr["pivot_x"], t.height), fill=(0, 240, 255, 255))
            put(im, ch, (10 + k * (t.width + 10), y + 20))
        t = tex("select_heroes/" + hero["frames"][0]["file"].split("/")[-1])
        fr = hero["frames"][0]
        s = 2.05 * 0.6
        big = t.resize((int(t.width * s), int(t.height * s)), Image.LANCZOS)
        floor = y + row_h - 10
        put(im, big, (1300 - fr["pivot_x"] * s, floor - fr["feet_y"] * s))
        put(im, big.transpose(Image.FLIP_LEFT_RIGHT), (1650 - (t.width - fr["pivot_x"]) * s, floor - fr["feet_y"] * s))
        ImageDraw.Draw(im).line((1150, floor, 1850, floor), fill=(255, 60, 60, 255))
    im.save(os.path.join(OUT, "glass_heroes_sheet.png"))


def side_by_side(mine, ref, name, note):
    w, h = ref.size
    mine = mine.crop((0, 0, w, h))
    blend = Image.blend(mine.convert("RGB"), ref.convert("RGB"), 0.5).convert("RGBA")
    out = Image.new("RGBA", (w * 3 + 40, h + 50), BG0)
    caption(out, (10, 8), note)
    for k, (img, lab) in enumerate(((mine, "build art (TG-ART kit)"), (ref, "spec crop (browser, with blur)"),
                                    (blend, "50 % overlay"))):
        put(out, img, (10 + k * (w + 10), 40))
        caption(out, (14 + k * (w + 10), 40 + h - 24), lab, (255, 255, 0, 255))
    out.save(os.path.join(OUT, name))


def crop_ref(prefix):
    for fn in os.listdir(CROPS):
        if fn.startswith(prefix):
            parts = fn[:-4].split("__from_")[1].split("_")
            return Image.open(os.path.join(CROPS, fn)).convert("RGBA"), (int(parts[1]), int(parts[2]))
    raise FileNotFoundError(prefix)


def compare_menu():
    ref, (ox, oy) = crop_ref("menu_button_column")
    canvas = menu_art()
    labels = ["Story Mode", "Fighter Mode", "Calibration Drills", "Settings", "Extras", "Quit Game"]
    for k, lab in enumerate(labels):
        chevron_row(canvas, 96, 520 + 79 * k, 560, 68, lab, "focus" if k == 0 else "normal")
    side_by_side(canvas.crop((ox, oy, ox + ref.width, oy + ref.height)), ref, "compare_menu_column.png",
                 "rec_01 menu column: the glass chevrons over the menu art and scrim (labels, markers and the A "
                 "prompt approximated here)")


def compare_pause():
    ref, (ox, oy) = crop_ref("pause_panel")
    scene = mock_scene()
    grey = scene.convert("L").convert("RGBA")
    canvas = Image.blend(grey, scene, 0.7).filter(ImageFilter.GaussianBlur(6))   # saturate .7, the mock's blur
    canvas.alpha_composite(Image.new("RGBA", canvas.size, (3, 5, 14, 148)))       # the as-drawn dim .58
    put_nine(canvas, "glass_panel", 660, 92, 600, 769)
    sheen(canvas, 660, 92, 600, 769)
    put_nine(canvas, "glass_tag", 686, 81, 189, 24)
    text(canvas, (686 + 12, 81 + 12), "POMPEII 79 AD", font(OUTFIT, 18, 500), INK, tracking=2.88)
    title = font(CINZEL, 78, 900)
    text(canvas, (960 - tracked_width("PAUSED", title, 10.92) / 2, 122 + 39), "PAUSED", title, (255, 255, 255),
         tracking=10.92, anchor="lm")
    rows = ["Resume", "Settings", "Move List", "Dialogue Log", "Save Game", "Restart Level", "Lower Difficulty",
            "Quit to Main Menu"]
    for k, lab in enumerate(rows):
        chevron_row(canvas, 680, 257.6 + 73 * k, 560, 64, lab, "focus" if k == 0 else "normal")
    side_by_side(canvas.crop((ox, oy, ox + ref.width, oy + ref.height)), ref, "compare_pause_panel.png",
                 "rec_03 pause panel: the glass slab (+ the 3.1 sheen preview) and chevrons over the mock's scene, "
                 "saturate .7, blurred, the as-drawn .58 dim (the build's fill is the darker 1.4 one)")


def compare_hud():
    ref, (ox, oy) = crop_ref("hud_story_left_column")
    canvas = mock_scene()
    for (x, y, w, h, tag, tw) in ((32, 108, 620, 214, "VITALS", 107), (32, 348, 620, 155, "ABILITIES", 132),
                                  (32, 529, 620, 74, None, 0)):
        put_nine(canvas, "glass_panel", x, y, w, h)
        sheen(canvas, x, y, w, h)
        if tag:
            put_nine(canvas, "glass_tag", x + 26, y - 11, tw, 24)
            text(canvas, (x + 26 + 12, y - 11 + 12), tag, font(OUTFIT, 18, 500), INK, tracking=2.88)
    side_by_side(canvas.crop((ox, oy, ox + ref.width, oy + ref.height)), ref, "compare_hud_left_column.png",
                 "rec_04 Story HUD left column: the three glass panels and their tags only (the lenses, tubes, "
                 "portrait and text are TG-WIDGETS / TG-HUDS work), over the mock's own Pompeii scene")


def read_tres(name):
    """The written GradientTexture2D, parsed back (not the definition), and filled the way Godot's CPU fill does:
    pos = (x / (w - 1), y / (h - 1)); radial = |pos - from| / |to - from|; linear = the projection on from -> to."""
    t = open(os.path.join(KIT, name), encoding="utf-8").read()

    def nums(key):
        return [float(v) for v in t.split(key + "(", 1)[1].split(")", 1)[0].split(",")]
    offs, cols = nums("offsets = PackedFloat32Array"), nums("colors = PackedColorArray")
    cols = np.asarray(cols).reshape(-1, 4)
    w, h = int(t.split("width = ")[1].split()[0]), int(t.split("height = ")[1].split()[0])
    fill = int(t.split("fill = ")[1].split()[0])
    frm, to = nums("fill_from = Vector2"), nums("fill_to = Vector2")
    yy, xx = np.mgrid[0:h, 0:w]
    px, py = xx / max(w - 1, 1), yy / max(h - 1, 1)
    fx, fy, tx, ty = frm[0], frm[1], to[0], to[1]
    if fill == 1:
        o = np.hypot(px - fx, py - fy) / math.hypot(tx - fx, ty - fy)
    else:
        dx, dy = tx - fx, ty - fy
        o = ((px - fx) * dx + (py - fy) * dy) / (dx * dx + dy * dy)
    o = np.clip(o, 0.0, 1.0)
    img = np.stack([np.interp(o, offs, cols[:, c]) for c in range(4)], axis=-1)
    return Image.fromarray(np.round(img * 255).astype(np.uint8), "RGBA")


def sheet_gradients():
    """The seven GradientTexture2D resources: each on a checkerboard, then the three scrims in context."""
    im = Image.new("RGBA", (1920, 1560), BG0)
    caption(im, (10, 10), "gradient resources (parsed back from the .tres and filled as GradientTexture2D does): radials "
                          "at 128 px; menu_scrim, select_vignette, fighter_top_shade in context below")
    for k, (stem, g) in enumerate(G.GRADIENTS.items()):
        t = read_tres(g["file"])
        show = t.resize((160, 160), Image.BILINEAR)                # linear strips stretched square, as drawn
        ch = checker(160, 160, 10)
        put(ch, show, ((160 - show.width) / 2, (160 - show.height) / 2))
        put(im, ch, (20 + k * 270, 50))
        caption(im, (20 + k * 270, 215), g["file"], (200, 200, 210, 255))
    art = menu_art_plain = cover(os.path.join(G.REPO, "assets", "backgrounds", "menu",
                                              "temporal_glass_main_menu_background.png"), 1920, 1080)
    scrim = read_tres("menu_scrim.tres").resize((1920, 1080), Image.BILINEAR)
    menu = menu_art_plain.copy()
    menu.alpha_composite(scrim)
    put(im, menu.resize((620, 349), Image.LANCZOS), (20, 260))
    caption(im, (20, 615), "menu_scrim over the menu art (stretch Scale)")
    sel = cover(os.path.join(G.REPO, "assets", "backgrounds", "menu", "temporal_glass_fighter_select_background.png"),
                1920, 1080)
    v = read_tres("select_vignette.tres")
    s = max(1920 / v.width, 1080 / v.height)                     # KeepAspectCovered
    vv = v.resize((int(round(v.width * s)), int(round(v.height * s))), Image.BILINEAR)
    put(sel, vv, ((1920 - vv.width) / 2, (1080 - vv.height) / 2))
    put(im, sel.resize((620, 349), Image.LANCZOS), (650, 260))
    caption(im, (650, 615), "select_vignette over the select art (KeepAspectCovered)")
    stage = cover(os.path.join(G.REPO, "assets", "environments", "florence", "florence_far_background.png"), 1920, 1080)
    shade = read_tres("fighter_top_shade.tres").resize((1920, 300), Image.BILINEAR)
    put(stage, shade, (0, 0))
    put(im, stage.resize((620, 349), Image.LANCZOS), (1280, 260))
    caption(im, (1280, 615), "fighter_top_shade over a stage (1920 x 300 at the top)")
    # lens / badge / dial / portrait radials as circles (the widgets map circle polygons over them), at 92 / 44 / 112
    for k, (name, d) in enumerate((("glass_lens_radial.tres", 92), ("glass_badge_radial.tres", 44),
                                   ("glass_dial_radial.tres", 112), ("glass_portrait_radial.tres", 112))):
        t = read_tres(name).resize((d * 4, d * 4), Image.BILINEAR)
        mask = Image.new("L", t.size, 0)
        if "portrait" in name:
            ImageDraw.Draw(mask).polygon([(x * 4, y * 4) for x, y in G.chamfer(0, 0, d, d, d * 0.22)], fill=255)
        else:
            ImageDraw.Draw(mask).ellipse((0, 0, d * 4 - 1, d * 4 - 1), fill=255)
        t.putalpha(Image.fromarray((np.asarray(t.getchannel("A")).astype(np.float64) *
                                    np.asarray(mask) / 255).astype(np.uint8)))
        t = t.resize((d, d), Image.LANCZOS)
        scene = mock_scene().crop((1300, 200, 1300 + 260, 200 + 260))
        put(scene, t, ((260 - d) / 2, (260 - d) / 2))
        put(im, scene, (20 + k * 290, 680))
        caption(im, (20 + k * 290, 945), f"{name} as a {d} px {'frame' if 'portrait' in name else 'circle'}",
                (200, 200, 210, 255))
    im.crop((0, 0, 1920, 980)).save(os.path.join(OUT, "glass_gradients.png"))


def main():
    os.makedirs(OUT, exist_ok=True)
    sheet_kit()
    sheet_ninesliced()
    sheet_icons()
    sheet_heroes()
    sheet_gradients()
    compare_menu()
    compare_pause()
    compare_hud()
    print("contact sheets written to", OUT)


if __name__ == "__main__":
    main()
