"""Assembles demonstration compositions from the shipped kit (cover, select, Story HUD, Fighter HUD,
pause panel) the way the Phase B screens will, using only the PNGs under assets/sprites/ui/pulp/
and PIL's text for placeholder labels.  Evidence that the nine-slice margins and expand margins in
pulp_kit.json are right; compare the output with the mockup renders (D:/Projects/ftt-scratch/mix-preview).

usage (repo root): py tools/asset_src/pulp/build_kit_demo.py  ->  D:/Projects/ftt-scratch/ur-captures/art/demo_*.png
"""
import json
import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
KIT = os.path.join(REPO, "assets", "sprites", "ui", "pulp")
OUTDIR = "D:/Projects/ftt-scratch/ur-captures/art"
CAT = json.load(open(os.path.join(KIT, "pulp_kit.json"), encoding="utf-8"))
FONT = "C:/Windows/Fonts/arialbd.ttf"
SERIF = "C:/Windows/Fonts/georgiab.ttf"


def font(sz, serif=False):
    try:
        return ImageFont.truetype(SERIF if serif else FONT, int(sz))
    except OSError:
        return ImageFont.load_default()


def img(name):
    return Image.open(os.path.join(KIT, name)).convert("RGBA")


def tint(im, rgb, alpha=1.0):
    a = np.asarray(im).astype("float32")
    a[..., 0], a[..., 1], a[..., 2] = rgb
    a[..., 3] *= alpha
    return Image.fromarray(a.astype("uint8"), "RGBA")


def nine(canvas, name, x, y, w, h, mod=None):
    """Draw a nine-slice so that its border box is (x, y, w, h)."""
    e = CAT[name]
    L, T, R, B = e["expand_margins_LTRB"]
    m = e["nine_slice_margins_LTRB"]
    im = img(e["file"])
    tw, th = w + L + R, h + T + B
    out = Image.new("RGBA", (tw, th), (0, 0, 0, 0))
    W, H = im.size
    xs = [(0, m[0], 0, m[0]), (m[0], W - m[2], m[0], tw - m[2]), (W - m[2], W, tw - m[2], tw)]
    ys = [(0, m[1], 0, m[1]), (m[1], H - m[3], m[1], th - m[3]), (H - m[3], H, th - m[3], th)]
    for sx0, sx1, dx0, dx1 in xs:
        for sy0, sy1, dy0, dy1 in ys:
            if sx1 > sx0 and sy1 > sy0 and dx1 > dx0 and dy1 > dy0:
                out.alpha_composite(im.crop((sx0, sy0, sx1, sy1)).resize((dx1 - dx0, dy1 - dy0), Image.BILINEAR), (dx0, dy0))
    if mod:
        out = tint(out, mod[:3], 1) if False else out
    canvas.alpha_composite(out, (x - L, y - T))


def label(canvas, text, xy, size, fill, serif=False, anchor="la", stroke=0, spacing=0):
    d = ImageDraw.Draw(canvas)
    d.text(xy, text, font=font(size, serif), fill=fill, anchor=anchor, stroke_width=stroke, stroke_fill=(0, 0, 0, 255))


def icon(canvas, name, xy, size, rgb, shadow=True):
    base = "icons/ico_" + name
    fill, ink, sh = img(base + "_fill.png"), img(base + "_ink.png"), img(base + "_sh.png")
    sz = (size, size) if fill.width == fill.height else (int(size * fill.width / fill.height), size)
    fill, ink, sh = (i.resize(sz, Image.LANCZOS) for i in (fill, ink, sh))
    x, y = xy
    if shadow:
        canvas.alpha_composite(sh, (x + 2, y + 2))
    canvas.alpha_composite(tint(fill, rgb), (x, y))
    canvas.alpha_composite(ink, (x, y))


def rotate_paste(canvas, im, centre, deg):
    r = im.rotate(-deg, resample=Image.BICUBIC, expand=True)
    canvas.alpha_composite(r, (int(centre[0] - r.width / 2), int(centre[1] - r.height / 2)))


def cover():
    c = img("cover_art.png")
    a = np.asarray(c).astype("float32")
    r = np.asarray(img("rays.png")).astype("float32")
    a[..., :3] = np.clip(a[..., :3] + r[..., :3] * r[..., 3:4] / 255, 0, 255)
    h = np.asarray(img("halftone_screen.png")).astype("float32")
    a[..., :3] *= (1 - h[..., 3:4] / 255 * 0.55)
    c = Image.fromarray(a.astype("uint8"), "RGBA")
    b = img("burst_star.png")
    b.putalpha(b.getchannel("A").point(lambda v: int(v * 0.92)))
    c.alpha_composite(b, (60, 300))
    # title block rotated -3.2deg about its top-left at (84,96)
    blk = Image.new("RGBA", (860, 290), (0, 0, 0, 0))
    for k in range(10, 0, -1):
        label(blk, "FIGHTERS", (2 * k, 2 * k), 160, (0, 0, 0, 255), serif=True, stroke=7)
    label(blk, "FIGHTERS", (0, 0), 160, (243, 201, 91, 255), serif=True, stroke=7)
    nine(blk, "banner_cover", 18, 179, 710, 98)
    label(blk, "THROUGH TIME", (373, 228), 62, (243, 240, 230, 255), anchor="mm", stroke=3)
    rot = blk.rotate(3.2, resample=Image.BICUBIC, expand=True, center=(0, 0))
    c.alpha_composite(rot, (84, 96))
    heroes = [("joan", 250, 1.62), ("einstein", 540, 1.55), ("cleopatra", 800, 1.50)]
    poses = CAT["cover_heroes"]["poses"]
    for cid, px, sc in heroes:
        p = poses[f"{cid}_victory"]
        im = img(p["file"])
        # the mock scale applies to the baked pixels directly (Joan 174x335 -> 282x543)
        s = sc
        im = im.resize((int(im.width * s), int(im.height * s)), Image.LANCZOS)
        ink = tint(im.filter(__import__("PIL.ImageFilter", fromlist=["x"]).MaxFilter(5)), (0, 0, 0))
        feet = int(p["feet_px_from_bottom"] * s)
        x = int(px - im.width / 2)
        y = int(1020 + feet - im.height)
        c.alpha_composite(tint(ink, (0, 0, 0), 0.6), (x + 10, y + 12))
        c.alpha_composite(ink, (x, y))
        c.alpha_composite(im, (x, y))
    items = ["STORY MODE", "FIGHTER MODE", "CALIBRATION DRILLS", "SETTINGS", "EXTRAS", "QUIT GAME"]
    for i, t in enumerate(items):
        x, y = 1180, 322 + 97 * i
        if i == 0:
            nine(c, "btn_focus_h80", x + 16, y - 4, 640, 80)
            mk = tint(img("icons/ico_marker_fill.png").resize((20, 28), Image.LANCZOS), (12, 10, 16))
            c.alpha_composite(mk, (x + 16 + 26, y - 4 + 26))
            label(c, t, (x + 16 + 64, y - 4 + 40), 34, (12, 10, 16, 255), anchor="lm")
        else:
            nine(c, "btn_normal_h80", x, y, 640, 80)
            mk = img("icons/ico_marker_fill.png").resize((20, 28), Image.LANCZOS)
            c.alpha_composite(tint(img("icons/ico_marker_sh.png").resize((20, 28), Image.LANCZOS), (0, 0, 0)), (x + 28, y + 28))
            c.alpha_composite(tint(mk, (138, 131, 116)), (x + 26, y + 26))
            label(c, t, (x + 64, y + 40), 34, (243, 240, 230, 255), anchor="lm")
    st = img("sticker_continue.png")
    rotate_paste(c, st.resize((st.width // 2, st.height // 2), Image.LANCZOS), (1554 + 140, 36 + 140), 8)
    label(c, "CONTINUE", (1694, 120), 40, (12, 10, 16, 255), anchor="mm")
    label(c, "CHAPTER 6", (1694, 164), 28, (122, 31, 25, 255), anchor="mm")
    label(c, "VERSION 1.0.3", (70, 1034), 18, (192, 186, 166, 255), anchor="lm", stroke=2)
    c.convert("RGB").save(os.path.join(OUTDIR, "demo_cover.png"))


def select():
    c = img("select_split.png")
    h = np.asarray(img("halftone_screen.png")).astype("float32")
    a = np.asarray(c).astype("float32")
    a[..., :3] *= (1 - h[..., 3:4] / 255 * 0.8)
    c = Image.fromarray(a.astype("uint8"), "RGBA")
    nine(c, "banner_select", 538, 36, 844, 90)
    label(c, "SELECT YOUR FIGHTER", (960, 81), 52, (12, 10, 16, 255), serif=True, anchor="mm")
    ids = ["einstein", "joan", "leonardo", "lincoln", "cleopatra", "tesla", "shakespeare", "mozart", "tubman", None]
    names = ["EINSTEIN", "JOAN", "LEONARDO", "LINCOLN", "CLEOPATRA", "TESLA", "SHAKESPEARE", "MOZART", "TUBMAN", "RANDOM"]
    field, dots = img("card_field.png"), img("card_halftone.png")
    for i, cid in enumerate(ids):
        col, row = i % 5, i // 5
        x, y = 444 + col * 214, 190 + row * 300
        frame = "card_frame"
        dy = 0
        if cid == "joan":
            frame, dy = "card_frame_pick", -10
        if cid == "tesla":
            frame = "card_frame_pick"
        tile = Image.new("RGBA", (260, 340), (0, 0, 0, 0))
        nine(tile, frame, 30, 30, 196, 276)
        if cid:
            tile.alpha_composite(field, (34, 34))
            tile.alpha_composite(dots, (34, 34))
            por = img(f"../../characters/portraits/{cid}_portrait.png").resize((248, 248), Image.LANCZOS)
            win = Image.new("RGBA", (188, 204), (0, 0, 0, 0))
            win.alpha_composite(por, (-30, -8))
            tile.alpha_composite(win, (34, 34))
        else:
            d = ImageDraw.Draw(tile)
            hat = img("hatch_random.png")
            for hx in range(0, 188, hat.width):
                for hy in range(0, 200, hat.height):
                    tile.alpha_composite(hat.crop((0, 0, min(hat.width, 188 - hx), min(hat.height, 200 - hy))), (34 + hx, 34 + hy))
            label(tile, "?", (128, 130), 110, (243, 201, 91, 255), anchor="mm", stroke=3)
        d = ImageDraw.Draw(tile)
        d.rectangle((34, 34 + 276 - 64 - 8, 34 + 187, 34 + 267), fill=(12, 10, 16, 255))
        label(tile, names[i], (128, 34 + 276 - 40), 24, (243, 240, 230, 255), anchor="mm")
        if cid in ("joan", "tesla"):
            rotate_paste(c, tile, (x + 98, y + 138 + dy), -1.6 if cid == "joan" else 1.6)
        else:
            c.alpha_composite(tile, (x - 30, y - 30 + dy))
    # stat card + stars + bubble
    nine(c, "panel_paper", 30, 704, 360, 260)
    nine(c, "tag_panel", 19, 683, 125, 38)
    label(c, "MELEE", (81, 702), 20, (12, 10, 16, 255), anchor="mm")
    label(c, "JOAN OF ARC", (55, 740), 31, (12, 10, 16, 255), serif=True)
    for i, (lab, v, fr) in enumerate((("MAX HP", "110", 0.8), ("SPEED", "6.2", 0.7), ("WEIGHT", "1.1", 0.5))):
        yy = 790 + 43 * i
        label(c, lab, (55, yy), 18, (58, 49, 68, 255), anchor="lm")
        nine(c, "bar_track_h14", 175, yy - 7, 118, 14)
        sp = img("hatch_hp.png")
        label(c, v, (350, yy), 24, (20, 16, 26, 255), anchor="rm")
    for i in range(3):
        nine(c, "slot_frame", 75 + 100 * i, 884, 50, 50)
        for slot in ("special_1", "special_2", "movement")[i:i + 1]:
            ic = img(f"ability_icons/joan_{slot}.png").resize((34, 34), Image.LANCZOS)
            c.alpha_composite(ic, (83 + 100 * i, 892))
    for xx, col, t in ((30, (0, 240, 255), "P1"), (1760, (255, 51, 102), "P2")):
        base = "star_badge_"
        sh = img(base + "sh.png")
        fl = tint(img(base + "fill.png"), col)
        ik = img(base + "ink.png")
        for layer in (sh, fl, ik):
            c.alpha_composite(layer.resize((layer.width // 2, layer.height // 2), Image.LANCZOS), (xx - 4, 86))
        label(c, t, (xx + 59, 149), 44, (12, 10, 16, 255), anchor="mm")
    nine(c, "bubble", 150, 200, 237, 74)
    c.alpha_composite(img("bubble_tail.png"), (150 + 34, 200 + 74 - 5))
    label(c, "READY!", (268, 237), 46, (12, 10, 16, 255), anchor="mm")
    for i, t in enumerate(("MOVE LIST", "SYSTEMS CARD", "BACK")):
        nine(c, "btn_normal_h58", 466 + 336 * i, 840, 316, 58)
        label(c, t, (466 + 336 * i + 54, 869), 27, (243, 240, 230, 255), anchor="lm")
    c.convert("RGB").save(os.path.join(OUTDIR, "demo_select.png"))


def story():
    sc = Image.new("RGBA", (1920, 1080), (86, 52, 44, 255))
    ImageDraw.Draw(sc).rectangle((0, 880, 1920, 1080), fill=(40, 28, 24, 255))
    c = sc
    # vitals panel
    nine(c, "panel_dark", 20, 56, 640, 230)
    nine(c, "tag_panel", 9, 35, 131, 38)
    label(c, "VITALS", (74, 54), 21, (12, 10, 16, 255), anchor="mm")
    por = img("../../characters/portraits/einstein_portrait.png").resize((160, 160), Image.LANCZOS)
    rd = img("portrait_roundel.png")
    c.alpha_composite(rd.resize((rd.width // 2, rd.height // 2), Image.LANCZOS), (46 - 10, 84 - 10))
    mask = Image.new("L", (112, 112), 0)
    ImageDraw.Draw(mask).ellipse((0, 0, 111, 111), fill=255)
    pw = Image.new("RGBA", (112, 112), (0, 0, 0, 0))
    pw.paste(por.resize((159, 159)), (-23, -8), None)
    pw.putalpha(mask)
    c.alpha_composite(pw, (46, 84))
    label(c, "ALBERT EINSTEIN", (186, 100), 22, (243, 240, 230, 255), serif=True, anchor="lm")
    nine(c, "bar_track_h30", 190, 120, 380, 30)
    # fill (sheared quad) + hatch
    fill = Image.new("RGBA", (380, 30), (0, 0, 0, 0))
    ImageDraw.Draw(fill).polygon([(8, 3), (260, 3), (252, 27), (3, 27)], fill=(140, 240, 184, 255))
    c.alpha_composite(fill, (190, 120))
    label(c, "72/100", (550, 135), 22, (243, 240, 230, 255), anchor="rm", stroke=3)
    for i in range(3):
        icon(c, "shield", (190 + 34 * i, 162), 30, (223, 232, 255))
    badge = img("badge_disc.png").resize((44, 44), Image.LANCZOS)
    for i, (n, col) in enumerate((("venom", (255, 122, 92)), ("dilation", (143, 199, 255)))):
        x = 190 + i * 150
        d = ImageDraw.Draw(c)
        d.ellipse((x - 3 + 3, 208 - 3 + 4, x + 47 + 3, 208 + 47 + 4), fill=(0, 0, 0, 255))
        d.ellipse((x - 3, 208 - 3, x + 47, 208 + 47), fill=(0, 0, 0, 255))
        c.alpha_composite(badge, (x, 208))
        icon(c, n, (x + 8, 216), 28, col, shadow=False)
        label(c, "VENOM" if i == 0 else "SLOW", (x + 56, 218), 18, (243, 240, 230, 255), anchor="lm")
        label(c, "4s", (x + 56, 238), 20, (192, 186, 166, 255), anchor="lm")
    # abilities panel
    nine(c, "panel_dark", 20, 330, 640, 150)
    nine(c, "tag_panel", 9, 309, 165, 38)
    label(c, "ABILITIES", (91, 328), 21, (12, 10, 16, 255), anchor="mm")
    u = img("slot_ult_ready.png")
    c.alpha_composite(u, (60 - 6, 360 - 6))
    for i, (slot, nm) in enumerate((("special_1", "S1"), ("special_2", "S2"), ("movement", "MOV"))):
        x = 300 + i * 120
        nine(c, "slot_frame_ready" if i == 0 else "slot_frame", x, 365, 74, 74)
        ic = img(f"ability_icons/einstein_{slot}.png").resize((56, 56), Image.LANCZOS)
        c.alpha_composite(ic, (x + 9, 374))
        kt = Image.new("RGBA", (80, 30), (0, 0, 0, 0))
        nine(kt, "key_tag", 8, 4, 48, 22)
        label(kt, nm, (32, 15), 15, (12, 10, 16, 255), anchor="mm")
        rotate_paste(c, kt, (x - 10 + 30, 365 - 12 + 12), -4)
    # time tools
    nine(c, "panel_dark", 20, 520, 640, 70)
    badge = img("badge_disc.png").resize((46, 46), Image.LANCZOS)
    c.alpha_composite(badge, (50, 532))
    icon(c, "hourglass", (59, 540), 29, (255, 227, 154), shadow=False)
    label(c, "x3", (110, 555), 32, (243, 240, 230, 255), anchor="lm", stroke=3)
    c.alpha_composite(badge, (230, 532))
    icon(c, "freeze", (239, 540), 29, (255, 227, 154), shadow=False)
    label(c, "TIME FREEZE", (290, 545), 17, (192, 186, 166, 255), anchor="lm")
    label(c, "READY", (290, 566), 20, (255, 240, 176, 255), anchor="lm")
    # dust + dial
    nine(c, "panel_dark", 1500, 28, 330, 70)
    icon(c, "dust", (1520, 44), 38, (243, 201, 91))
    label(c, "1,250", (1578, 63), 37, (243, 240, 230, 255), anchor="lm", stroke=2)
    db, dt = img("dial_back.png"), img("dial_ticks.png")
    h = db.resize((db.width // 2, db.height // 2), Image.LANCZOS)
    c.alpha_composite(h, (1736 - 4, 130 - 16))
    d = ImageDraw.Draw(c)
    cx, cy = 1736 + 56, 130 + 56
    d.ellipse((cx - 46, cy - 46, cx + 46, cy + 46), fill=(255, 243, 216, 255))
    d.pieslice((cx - 46, cy - 46, cx + 46, cy + 46), -90, -90 + 360 * 0.64, fill=(255, 90, 90, 255))
    c.alpha_composite(dt.resize((dt.width // 2, dt.height // 2), Image.LANCZOS), (1736 - 4, 130 - 16))
    label(c, "64%", (cx, 130 + 112 + 18), 27, (243, 240, 230, 255), anchor="mm", stroke=2)
    # boss bar
    nine(c, "bar_track_boss", 725, 99, 830, 34)
    d = ImageDraw.Draw(c)
    nine(c, "boss_nameplate", 720, 22, 687, 62)
    label(c, "VULCAN DECIMATOR", (760, 54), 38, (243, 240, 230, 255), serif=True, anchor="lm", stroke=3)
    nine(c, "boss_phase_tab", 1432, 49, 128, 35)
    label(c, "PHASE 2", (1496, 66), 18, (12, 10, 16, 255), anchor="mm")
    c.convert("RGB").save(os.path.join(OUTDIR, "demo_story.png"))


def fighter():
    c = Image.new("RGBA", (1920, 1080), (70, 90, 120, 255))
    for side in (0, 1):
        x = 70 if side == 0 else 1920 - 70 - 510
        nine(c, "bar_track_h44", x + 8, 60, 502, 44)
        fill = Image.new("RGBA", (510, 44), (0, 0, 0, 0))
        ImageDraw.Draw(fill).polygon([(12, 3), (400, 3), (390, 41), (6, 41)], fill=(140, 240, 184, 255))
        if side == 1:
            fill = fill.transpose(Image.FLIP_LEFT_RIGHT)
        c.alpha_composite(fill, (x, 60))
        nine(c, "bar_track_h16", x + 8, 112, 382, 16)
        for i in range(3):
            icon(c, "star4", (x + 8 + 34 * i, 138), 28, (0, 240, 255) if side == 0 else (255, 51, 102))
        for i, s in enumerate(("special_1", "special_2", "movement")):
            nine(c, "slot_frame", x + 280 + 64 * i, 140, 54, 54)
            c.alpha_composite(img(f"ability_icons/{'joan' if side == 0 else 'tesla'}_{s}.png").resize((38, 38), Image.LANCZOS), (x + 288 + 64 * i, 148))
    nine(c, "panel_dark", 800, 40, 320, 84)
    label(c, "1:42", (960, 82), 60, (243, 240, 230, 255), anchor="mm")
    nine(c, "tag_panel", 790, 20, 140, 38)
    c.convert("RGB").save(os.path.join(OUTDIR, "demo_fighter.png"))


def pause():
    c = Image.new("RGBA", (1920, 1080), (60, 40, 36, 255))
    nine(c, "panel_dark", 660, 92, 600, 775)
    nine(c, "tag_panel", 649, 71, 229, 38)
    label(c, "POMPEII 79 AD", (763, 90), 20, (12, 10, 16, 255), anchor="mm")
    label(c, "PAUSED", (960, 172), 78, (255, 255, 255, 255), serif=True, anchor="mm")
    items = ["RESUME", "SETTINGS", "MOVE LIST", "DIALOGUE LOG", "SAVE GAME", "RESTART LEVEL"]
    for i, t in enumerate(items):
        x, y = 683, 261 + 73 * i
        if i == 0:
            nine(c, "btn_focus_h64", x + 16, y - 2, 560, 64)
            label(c, t, (x + 16 + 64, y + 30), 34, (12, 10, 16, 255), anchor="lm")
        elif i == 5:
            nine(c, "btn_disabled_h64", x, y, 560, 64)
            label(c, t, (x + 64, y + 32), 34, (122, 112, 90, 255), anchor="lm")
        else:
            nine(c, "btn_normal_h64", x, y, 560, 64)
            label(c, t, (x + 64, y + 32), 34, (243, 240, 230, 255), anchor="lm")
    nine(c, "keycap", 700, 900, 120, 34)
    label(c, "ENTER", (760, 917), 18, (255, 243, 216, 255), anchor="mm")
    c.convert("RGB").save(os.path.join(OUTDIR, "demo_pause.png"))


if __name__ == "__main__":
    os.makedirs(OUTDIR, exist_ok=True)
    for fn in (cover, select, story, fighter, pause):
        fn()
    print("demos written")
