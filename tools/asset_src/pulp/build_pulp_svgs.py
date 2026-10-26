"""Writes every ThorVG-safe SVG source of the pulp UI kit (UI_SPEC section 7.1 plus
the components of section 6.2 that are textures), the matching jobs for
tools/generate_assets.gd (groups "pulp" and "pulp_icons_raw"), the .tres gradient
resources, and tools/asset_src/pulp/catalog.json (the machine-readable asset
catalog: size, nine-slice margins, expand margins, mipmap flag, plate it serves).

Run from the repository root:
    py tools/asset_src/pulp/build_pulp_svgs.py
then   godot --headless --path . --script res://tools/generate_assets.gd -- group=pulp,pulp_icons_raw
then   py tools/asset_src/pulp/build_pulp_images.py     (PIL-made rasters)
       py tools/asset_src/pulp/finish_pulp_icons.py     (icon layer composites)
"""
from __future__ import annotations

import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from pulp_svg import *  # noqa: E402,F401
import icons_geometry as G  # noqa: E402

REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
SVG_DIR = os.path.join(HERE, "svg")
OUT_RES = "res://assets/sprites/ui/pulp/"
JOBS = os.path.join(REPO, "tools", "asset_src", "jobs.json")

CATALOG: dict = {}
JOBS_NEW: list = []
os.makedirs(SVG_DIR, exist_ok=True)


def emit(name, text, size, group="pulp", ss=3, margins=None, expand=None, mip=False, serves="",
         notes="", tile=False, out_dir="", stretch=None, content=None, raw=False):
    path = os.path.join(SVG_DIR, name + ".svg")
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)
    w, h = int(round(size[0])), int(round(size[1]))
    if raw:
        out = f"res://tools/asset_src/pulp/raw/{name}.png"
    else:
        out = OUT_RES + out_dir + name + ".png"
    JOBS_NEW.append({"type": "svg", "group": group, "src": f"res://tools/asset_src/pulp/svg/{name}.svg",
                     "out": out, "width": w, "height": h, "supersample": ss})
    if raw:
        return
    entry = {"file": out_dir + name + ".png", "size": [w, h], "serves": serves}
    if margins:
        entry["nine_slice_margins_LTRB"] = list(margins)
    if expand:
        entry["expand_margins_LTRB"] = list(expand)
    if content:
        entry["content_margins_hint_LTRB"] = list(content)
    if stretch:
        entry["axis_stretch"] = stretch
    entry["mipmaps"] = bool(mip)
    if tile:
        entry["tile"] = True
    if notes:
        entry["notes"] = notes
    CATALOG[out_dir + name] = entry


def nine(name, plate: Plate, serves, mip=False, min_center=8, notes="", ss=3):
    """Emit a plate as a nine-slice: pick the border box so the centre slice is at least min_center."""
    plate.w = plate.h = 200          # a nominal size while the margins are computed (a pill test needs the height)
    m = plate.margins()
    L, T, R, B = plate.ext()
    plate.w = max(m[0] + m[2] + min_center - L - R, 4)
    plate.h = max(m[1] + m[3] + min_center - T - B, 4)
    W, H = plate.size()
    emit(name, plate.render(), (W, H), margins=m, expand=(L, T, R, B), mip=mip, serves=serves, notes=notes, ss=ss)
    return plate


# ---------------------------------------------------------------------------------------------
# 1. Panels, card frames, bubble
# ---------------------------------------------------------------------------------------------

nine("panel_dark", Plate(0, 0, border=(3, PAPER), rings=[(3, INK)], shadow=(9, 9, 3, INK), fill=BG1,
                         fill_opacity=0.80, r=0),
     "plate 1: dark ink panel (PNL-B). centre = bg1 @0.80; ring 3 black + paper border 3 + hard shadow slab 9,9",
     notes="Spec margins 6/6/15/15 put the shadow's start (3px below the box edge) outside the corner slices; "
           "these margins keep it inside. Use draw_center=true.")
nine("panel_paper", Plate(0, 0, border=(3, INK), shadow=(9, 9, 0, INK), fill=PAPER),
     "plate 2: paper panel (stat cards): #000 border 3, paper centre, shadow slab 9,9")
nine("card_frame", Plate(0, 0, border=(4, INK), rings=[(3, PAPER)], shadow=(7, 8, 3, INK), fill=DARK),
     "plate 20: trading card frame (CARD-B). centre #0C0A10; paper ring 3 + border 4 + shadow 7,8", mip=True,
     notes="Tilts at runtime (pick states rotate +-1.6deg): mipmaps on.")
nine("card_frame_pick", Plate(0, 0, border=(4, INK), rings=[(4, "#FFFFFF"), (4, INK)], shadow=(9, 10, 8, INK), fill=DARK),
     "plate 21: card, picked (WHITE ring 4 + black 4, shadow 9,10). The ring is white so it takes the player's active slot colour from a runtime SelfModulate (Settings > Gameplay > player-slot palette); the black ring, the shadow and the dark centre stay dark under any tint", mip=True)
nine("bubble", Plate(0, 0, border=(5, INK), r=30, shadow=(6, 6, 0, INK), fill=PAPER),
     "plate 27: speech bubble body (tail is bubble_tail.png); idle variant = modulate #D8CDB2", mip=True)
emit("bubble_tail", svg(40, 28, f'<path fill="{INK}" d="M0,0 L40,0 L6,28 Z"/>'), (40, 28), mip=True,
     serves="speech bubble tail: black triangle 40x28, apex 6px from the left; sits on the bubble's bottom edge at left 34, "
            "overlapping the bubble border by 5px")

# ---------------------------------------------------------------------------------------------
# 2. Tiles: hatches and halftone dots
# ---------------------------------------------------------------------------------------------
def tile_hatch(name, P, perp_target, width_target, angle, color, opacity, base=None, serves=""):
    perp = P / math.sqrt(2)
    wd = width_target * perp / perp_target
    reg = [(0, 0), (P, 0), (P, P), (0, P)]
    # use a larger region so bands wrap; clip happens against the tile itself
    body = ""
    if base:
        body += f'<rect width="{P}" height="{P}" fill="{base}"/>'
    body += diagonal_bands(reg, perp, wd, angle, color, offset=0.0, opacity=opacity)
    emit(name, svg(P, P, body), (P, P), ss=1, tile=True, serves=serves,
         notes=f"seamless {P}x{P} tile; stripe perpendicular period {perp:.2f}px (spec {perp_target}), band {wd:.2f}px")


tile_hatch("hatch_hp", 16, 11, 5, 45, "#000000", 0.22,
           serves="HP / meter / stat fill overlay: 135deg stripes, rgba(0,0,0,.22) (alpha baked); clip to the fill, tile in pre-shear space")
tile_hatch("hatch_boss", 20, 14, 6, 45, "#000000", 0.20,
           serves="boss fill overlay: 135deg stripes, rgba(0,0,0,.20) (alpha baked)")
tile_hatch("hatch_disabled", 17, 12, 6, -45, "#2C2733", 1.0, base="#221E29",
           serves="disabled button stripes #2C2733 / #221E29, 45deg (spec 12px tile is a perpendicular period; 17px is the horizontal period)")
tile_hatch("hatch_random", 34, 24, 12, -45, "#2A2434", 1.0, base="#1B1724",
           serves="Random card photo window hatch #2A2434 / #1B1724, 45deg")


def dot_tile(name, P, r_solid, r_end, serves):
    c = P / 2
    defs = (f'<radialGradient id="g" gradientUnits="userSpaceOnUse" cx="{c}" cy="{c}" r="{r_end}">'
            f'<stop offset="0" stop-color="#fff" stop-opacity="1"/><stop offset="{r_solid / r_end:.4f}" stop-color="#fff" stop-opacity="1"/>'
            f'<stop offset="1" stop-color="#fff" stop-opacity="0"/></radialGradient>')
    body = f'<circle cx="{c}" cy="{c}" r="{r_end}" fill="url(#g)"/>'
    emit(name, svg(P, P, body, defs), (P, P), ss=4, tile=True, serves=serves,
         notes="white dot, solid to r%s fading to r%s; colour and alpha come from modulate" % (r_solid, r_end))


dot_tile("dot_10", 10, 1.5, 2.0, "halftone corner of dark / paper panels: modulate rgba(255,255,255,.17) (dark) / rgba(0,0,0,.16) (paper); mask diagonal in a shader")
dot_tile("dot_9", 9, 1.6, 2.1, "halftone for the cover / portraits / select split (pitch 9): modulate rgba(0,0,0,.34) / .28")
dot_tile("dot_8", 8, 1.5, 2.0, "halftone of the card photo window (pitch 8): modulate rgba(0,0,0,.26)")

# ---------------------------------------------------------------------------------------------
# 3. Badge disc, burst star, Continue sticker, player star
# ---------------------------------------------------------------------------------------------
defs = ('<radialGradient id="g" gradientUnits="userSpaceOnUse" cx="22.4" cy="19.2" r="61.1">'
        '<stop offset="0" stop-color="#FFF7DE"/><stop offset="0.6" stop-color="#F3F0E6"/><stop offset="1" stop-color="#D8C690"/></radialGradient>')
emit("badge_disc", svg(64, 64, '<circle cx="32" cy="32" r="32" fill="url(#g)"/>', defs), (64, 64), ss=4, mip=True,
     serves="plate 18: status / HUD sticker disc (radial at 35% 30%: #FFF7DE, paper 60%, #D8C690); the 3px ink ring and the 3,4 shadow are drawn in code (their thickness must not scale)")

# burst star 18 points, outer 50% inner 38% of a 1000 box, radial #FFD36A 0 / #F4B63B .537 / #E0412F .99 (radius 500)
bp = star(18, 500, 380, 500, 500)
defs = ('<radialGradient id="g" gradientUnits="userSpaceOnUse" cx="500" cy="500" r="500">'
        '<stop offset="0" stop-color="#FFD36A"/><stop offset="0.537" stop-color="#F4B63B"/><stop offset="0.99" stop-color="#E0412F"/>'
        '<stop offset="1" stop-color="#E0412F"/></radialGradient>')
emit("burst_star", svg(1000, 1000, f'<path fill="url(#g)" d="{poly_d(bp)}"/>', defs), (1000, 1000), ss=1, mip=True,
     serves="menu burst (MENU-B): 18-point star, 1000x1000 at (60,300); runtime opacity 0.92. The same gradient is burst.tres")

# Continue sticker (2x): 14-point star box 270, outline 5, shadow 10,12
SC = 2
sp = star(14, 135, 108, 135 + 5, 135 + 5)
sw_, sh_ = 270 + 5 + 15, 270 + 5 + 17
body = (f'<path fill="{INK}" stroke="{INK}" stroke-width="10" stroke-linejoin="round" transform="translate(10,12)" d="{poly_d(sp)}"/>'
        f'<path fill="{INK}" stroke="{INK}" stroke-width="10" stroke-linejoin="round" d="{poly_d(sp)}"/>'
        f'<path fill="{ACCENT}" d="{poly_d(sp)}"/>')
emit("sticker_continue", svg(sw_ * SC, sh_ * SC, body, view=(0, 0, sw_, sh_)), (sw_ * SC, sh_ * SC), ss=2, mip=True,
     serves="plate 29: Continue sticker, 14-point star (outer 50% / inner 40% of 270), accent fill, 5px ink outline, shadow 10,12. 2x bake; "
            "the star's box origin is (5,5) in 1x units; rotate 8deg about the star centre (140,140 in 1x). Text is live labels.")

# player star badge: fill / ink / shadow layers (2x), 10 points outer 50% inner 36% of 118
pw = 118 + 4 + 4 + 5
ph = 118 + 4 + 4 + 6
ps = star(10, 59, 42.5, 59 + 4, 59 + 4)
for lay, body in (
    ("fill", f'<path fill="#FFFFFF" d="{poly_d(ps)}"/>'),
    ("ink", f'<path fill="{INK}" stroke="{INK}" stroke-width="8" stroke-linejoin="round" d="{poly_d(ps)}"/>'),
    ("sh", f'<path fill="{INK}" stroke="{INK}" stroke-width="8" stroke-linejoin="round" transform="translate(5,6)" d="{poly_d(ps)}"/>'),
):
    emit("star_badge_" + lay + "_raw", svg(pw * 2, ph * 2, body, view=(0, 0, pw, ph)), (pw * 2, ph * 2), group="pulp_icons_raw",
         ss=2, raw=True)
CATALOG["star_badge"] = {"file": "star_badge_{fill|ink|sh}.png", "size": [pw * 2, ph * 2], "mipmaps": True,
                         "serves": "P1 / P2 star badge (118 box, 10 points outer 50% / inner 36%; 2x bake, star box origin (4,4) in 1x). "
                                   "_fill = white star (tint with the player colour), _ink = 4px outline ring drawn OVER the fill, "
                                   "_sh = black silhouette with the (5,6) shadow offset already baked, drawn UNDER the fill",
                         "layers": True}

# ---------------------------------------------------------------------------------------------
# 4. Buttons (BTN-B), three heights x five states.  Skewed, so one texture per height.
# ---------------------------------------------------------------------------------------------
SK_BTN = 0.1405
BTN_STATES = {
    "normal":   dict(border=(3, PAPER), rings=[(3, INK)], shadow=(7, 7, 3, INK), fill=BG1, op=0.80),
    "focus":    dict(border=(3, INK), rings=[(3, PAPER), (3, INK)], shadow=(11, 11, 6, INK), fill=ACCENT, op=1.0),
    "selected": dict(border=(3, PAPER), rings=[(3, INK)], shadow=(7, 7, 3, INK), fill=PAPER, op=1.0),
    "pressed":  dict(border=(3, PAPER), rings=[(3, INK)], shadow=None, fill=ACCENT2, op=1.0),
    "disabled": dict(border=(3, "#6B6150"), rings=[(3, INK)], shadow=(4, 4, 3, INK), fill="none", op=1.0),
}
for hh in (80, 64, 58):
    for st, d in BTN_STATES.items():
        name = f"btn_{st}_h{hh}"
        pl = Plate(0, hh, s=SK_BTN, border=d["border"], rings=d["rings"], shadow=d["shadow"], fill=d["fill"], fill_opacity=d["op"])
        stretch = None
        if st == "disabled":
            # stripes drawn as clipped bands inside the inner parallelogram of the final plate
            stretch = {"mode": "tile", "period": 17}
        m = pl.margins()
        L, T, R, B = pl.ext()
        pl.w = max(m[0] + m[2] + 8 - L - R, 8)
        if stretch:
            centre = (L + pl.w + R) - m[0] - m[2]
            need = max(math.ceil(centre / 17), 1) * 17
            pl.w += need - centre
            b = pl.box()
            inner = b.points(-pl.border[0])
            pl.extra_inner = (diagonal_bands(inner, 12 / 1.0, 6, -45, "#2C2733", opacity=1.0))
            base = f'<path fill="#221E29" d="{b.path(-pl.border[0])}"/>'
            pl.extra_inner = base + pl.extra_inner
        W, H = pl.size()
        emit(name, pl.render(), (W, H), margins=m, expand=(L, T, R, B), mip=True, ss=3, stretch=stretch["mode"] if stretch else None,
             serves=f"BTN-B {st} plate, {hh} px high (plate rows 4-8). Hover = focus. Skew 0.1405 baked.",
             notes=("Label at x+64 (compact x+54), right padding 30, marker 20x28 at x+26. "
                    "Controls: draw the art offset by -expand and size by +expand; the focus pose (translate +16, rotate -1.2deg, scale 1.04) is a tween, not baked."))
CATALOG["btn_*"] = {"note": "buttons: btn_<normal|focus|selected|pressed|disabled>_h<80|64|58>.png; disabled uses axis_stretch tile (centre is a multiple of 17px)"}

# ---------------------------------------------------------------------------------------------
# 5. Bars (GAUGE-B) tracks, tabs, toggle, slider
# ---------------------------------------------------------------------------------------------
for hh, nm in ((30, "h30"), (44, "h44"), (16, "h16"), (14, "h14")):
    name = f"bar_track_{nm}"
    pl = Plate(0, hh, s=0.2126, border=(3, INK), rings=[(2, PAPER)], shadow=(5, 6, 2, INK), fill=TRACK)
    m = pl.margins(); L, T, R, B = pl.ext()
    pl.w = max(m[0] + m[2] + 8 - L - R, 8)
    W, H = pl.size()
    emit(name, pl.render(), (W, H), margins=m, expand=(L, T, R, B), mip=True,
         serves=f"plate 9: HP / meter / stat track, {hh} px high (Story HP 30, Fighter HP 44, Fighter meter 16, stat bar 14); centre #3A3144",
         notes="skew 0.2126; draw fill, tracker, echo, hatch, ticks inside the inner area (box inset 3); the track box is w-8 wide with margin-left 8")
pl = Plate(0, 44, s=0.1763, border=(5, INK), rings=[(3, PAPER)], shadow=(8, 8, 3, INK), fill=TRACK)
m = pl.margins(); L, T, R, B = pl.ext(); pl.w = max(m[0] + m[2] + 8 - L - R, 8); W, H = pl.size()
emit("bar_track_boss", pl.render(), (W, H), margins=m, expand=(L, T, R, B), mip=True,
     serves="plate 10: boss bar track, 44 high, skew 0.1763, border 5 + paper ring 3 + shadow 8,8; inner area #3A3144 (tracker accent, fill #FF5A5A)")

for st, fill, op in (("normal", "#1E1C34", 0.78), ("selected", ACCENT, 1.0)):
    pl = Plate(0, 52, s=SK_BTN, border=(4, INK), rings=[(2, PAPER)], shadow=(4, 4, 2, INK), fill=fill, fill_opacity=op)
    m = pl.margins(); L, T, R, B = pl.ext(); pl.w = max(m[0] + m[2] + 8 - L - R, 8); W, H = pl.size()
    emit(f"tab_{st}", pl.render(), (W, H), margins=m, expand=(L, T, R, B), mip=True,
         serves=f"Settings tab, {st} (Appendix B): bg2 / accent fill, border 4, ring 2 paper, shadow 4,4,2, skew 0.1405, 52 high (agent default height)")

for st, fill in (("off", TRACK), ("on", "#8CF0B8")):
    pl = Plate(0, 44, s=0.1763, border=(4, INK), rings=[(2, PAPER)], shadow=(4, 4, 2, INK), fill=fill)
    m = pl.margins(); L, T, R, B = pl.ext(); pl.w = 132 - 0; W, H = pl.size()
    emit(f"toggle_track_{st}", pl.render(), (W, H), margins=m, expand=(L, T, R, B), mip=True,
         serves=f"toggle track {st} (132x44 border box): fill {'#3A3144' if st == 'off' else '#8CF0B8'}; text and knob are separate")
pl = Plate(34, 34, s=0.1763, border=(4, INK), fill=PAPER)
W, H = pl.size()
emit("toggle_knob", pl.render(), (W, H), mip=True, serves="toggle knob 34x34, paper, border 4, skewed 0.1763 (at top 6, left 5 off / right 5 on)")
pl = Plate(0, 16, s=0.1763, border=(0, INK), rings=[(3, PAPER)], shadow=(3, 3, 3, INK), fill=INK)
m = pl.margins(); L, T, R, B = pl.ext(); pl.w = max(m[0] + m[2] + 8 - L - R, 8); W, H = pl.size()
emit("slider_track", pl.render(), (W, H), margins=m, expand=(L, T, R, B), mip=True,
     serves="slider track 16 high, black body, paper ring 3, shadow 3,3,3, skew 0.1763")
pl = Plate(0, 12, s=0.1763, border=(0, INK), fill=ACCENT)
m = pl.margins(safe=1); L, T, R, B = pl.ext(); pl.w = max(m[0] + m[2] + 6 - L - R, 6); W, H = pl.size()
emit("slider_fill", pl.render(), (W, H), margins=m, expand=(L, T, R, B), mip=True,
     serves="slider fill: accent skewed bar, 12 high; lay it inside the black body of slider_track (2px inset), cut its right end at the value")
pl = Plate(28, 28, circle=True, border=(4, INK), shadow=(3, 3, 0, INK), fill=PAPER)
W, H = pl.size()
emit("slider_grabber", pl.render(), (W, H), mip=True, serves="slider handle: 28 px circle, paper, border 4, shadow 3,3,0")

# ---------------------------------------------------------------------------------------------
# 6. Slot frames, tags, banners, boss plates, stamp, keycap
# ---------------------------------------------------------------------------------------------
nine("slot_frame", Plate(0, 0, r=6, border=(4, INK), rings=[(2, PAPER)], shadow=(5, 6, 2, INK), fill=SLOT),
     "plate 13: square ability slot frame (Story 74, Fighter 54, select 50); centre #2A2434", mip=True)
nine("slot_frame_ready", Plate(0, 0, r=6, border=(4, INK), rings=[(2, ACCENT), (2, INK)], shadow=(5, 6, 4, INK), fill=SLOT),
     "plate 14: ready slot frame (2 accent + 2 black rings, shadow 5,6,4)", mip=True)
for nm, rings, sd, serves in (
        ("slot_ult", [(2, PAPER)], (5, 6, 2, INK), "plate 15: Ultimate slot frame, circle 92 (not stretchable)"),
        ("slot_ult_ready", [(2, ACCENT), (2, INK)], (5, 6, 4, INK), "plate 15 ready: accent ring + black ring, shadow 5,6,4")):
    pl = Plate(92, 92, circle=True, border=(4, INK), rings=rings, shadow=sd, fill=SLOT)
    W, H = pl.size(); L, T, R, B = pl.ext()
    emit(nm, pl.render(), (W, H), expand=(L, T, R, B), mip=True, serves=serves,
         notes="fixed-size sprite; the 92 px circle's box origin is (expand L, expand T). The 116 px progress ring is drawn in code around it.")


def simple(name, h, s, border, shadow, fill, op, serves, rot_mip=True, r=0):
    pl = Plate(0, h, s=s, border=border, shadow=shadow, fill=fill, fill_opacity=op, r=r)
    m = pl.margins(); L, T, R, B = pl.ext(); pl.w = max(m[0] + m[2] + 8 - L - R, 8); W, H = pl.size()
    emit(name, pl.render(), (W, H), margins=m, expand=(L, T, R, B), mip=rot_mip, serves=serves)


simple("tag_panel", 38, 0.1763, (3, INK), (4, 4, 0, INK), ACCENT, 1.0, "plate 3: panel title tag (VITALS, ABILITIES, pause location, card style); text un-skewed; rotate -1.2deg")
simple("banner_select", 90, SK_BTN, (7, INK), (11, 11, 0, INK), ACCENT, 1.0, "plate 24: select banner (gold), 90 high, skew 0.1405, border 7, shadow 11,11; rotate -1deg")
simple("banner_cover", 98, SK_BTN, (7, INK), (12, 12, 0, INK), DARK, 0.86, "plate 25: cover banner THROUGH TIME as the dark ink plate (orchestrator ruling Q1: no pulp red)")
simple("boss_nameplate", 62, SK_BTN, (5, INK), (7, 7, 0, INK), DANGER, 1.0, "plate 11: boss nameplate, #FF5A5A, 62 high (687 wide in HUD-S1); rotate -1deg")
simple("boss_phase_tab", 35, 0.0, (4, INK), (4, 4, 0, INK), ACCENT, 1.0, "plate 12: boss PHASE tab, accent, 35 high; rotate +2deg")
simple("stamp_ready", 54, 0.0, (4, INK), (5, 5, 0, INK), DARK, 0.86, "plate 26: card READY stamp as the dark ink plate (orchestrator ruling Q1); rotate -10deg")
simple("key_tag", 22, 0.0, (3, INK), None, PAPER, 1.0, "plate 16: slot key tag (S1 S2 MOV ULT), paper, border 3; rotate -4deg at (-10,-12) of the slot")

# keycap: wider prompt variant (plan item 14) -- black pill, cream border 2, black outer ring 2
pl = Plate(0, 34, r=17, border=(2, CREAM), rings=[(2, INK)], fill=INK)
m = pl.margins(); L, T, R, B = pl.ext(); pl.w = max(m[0] + m[2] + 6 - L - R, 6); W, H = pl.size()
emit("keycap", pl.render(), (W, H), margins=m, expand=(L, T, R, B), mip=False, serves=
     "key-cap prompt (keyboard / mouse): black pill 34 high, cream border 2 + black ring 2 (UI_SPEC 3.7 prompt bar glyphs, wide variant); label #FFF3D8 centred")

# ---------------------------------------------------------------------------------------------
# 7. Portrait roundel, dial
# ---------------------------------------------------------------------------------------------
def roundel_svg(D=112):
    S = 2
    rt = 10
    L = T = rt
    R = B = 10 + 7
    W, H = L + D + R, T + D + B
    cx, cy = L + D / 2, T + D / 2
    body = (f'<circle cx="{cx + 6}" cy="{cy + 7}" r="{D / 2 + 10}" fill="{INK}"/>'
            f'<circle cx="{cx}" cy="{cy}" r="{D / 2 + 10}" fill="{INK}"/>'
            f'<circle cx="{cx}" cy="{cy}" r="{D / 2 + 7}" fill="{PAPER}"/>'
            f'<circle cx="{cx}" cy="{cy}" r="{D / 2 + 4}" fill="{INK}"/>'
            f'<circle cx="{cx}" cy="{cy}" r="{D / 2}" fill="{ACCENT}"/>'
            f'<circle cx="{cx}" cy="{cy}" r="{0.3 * D / 2 * math.sqrt(2)}" fill="{ACCENT2}"/>')
    return svg(W * S, H * S, body, view=(0, 0, W, H)), (W * S, H * S), (L, T, R, B)


t, sz, ex = roundel_svg()
emit("portrait_roundel", t, sz, ss=2, mip=True, expand=ex,
     serves="plate 17: portrait roundel back (shadow circle (6,7), black 4 + paper 3 + black 3 rings, gold disc with a #FFE39A core). 2x bake; "
            "the 112 disc's box origin is (10,10) in 1x units. Draw the portrait on a circular Polygon2D above it.",
     notes="portrait art zoom 142%, offset (-21%, -6.8%) of the diameter; scale the whole texture to the display diameter / 112")

# Integrity dial (2x): crown + black ring + shadow + paper face; ticks and inner ring are a second layer
D = 112
Ld, Td, Rd, Bd = 4, 16, 9, 10
W = Ld + D + Rd
H = Td + D + Bd
cx, cy = Ld + D / 2, Td + D / 2
crown = (f'<rect x="{cx - 17 + 3}" y="{Td - 16 + 3}" width="34" height="24" fill="{INK}"/>'
         f'<rect x="{cx - 17}" y="{Td - 16}" width="34" height="24" fill="{INK}"/>'
         f'<rect x="{cx - 13}" y="{Td - 12}" width="26" height="16" fill="{PAPER}"/>')
body = (crown + f'<circle cx="{cx + 5}" cy="{cy + 6}" r="{D / 2 + 4}" fill="{INK}"/>'
        f'<circle cx="{cx}" cy="{cy}" r="{D / 2 + 4}" fill="{INK}"/><circle cx="{cx}" cy="{cy}" r="{D / 2}" fill="{PAPER}"/>')
emit("dial_back", svg(W * 2, H * 2, body, view=(0, 0, W, H)), (W * 2, H * 2), ss=2, mip=True, expand=(Ld, Td, Rd, Bd),
     serves="plate 28: Integrity stopwatch back: crown (34x24, shadow 3,3), black ring 4, shadow 5,6, paper face. 2x bake; the 112 face's box origin is (4,16) in 1x. "
            "Draw the wedge disc (diameter 92, centre = face centre) above it, then dial_ticks.")
ticks = ""
for k in range(12):
    a = math.radians(k * 30)
    r0, r1 = 56 - 2.24, 56 - 2.24 - 9
    hw = math.radians(1.0)
    pts = [(cx + r0 * math.sin(a - hw), cy - r0 * math.cos(a - hw)), (cx + r0 * math.sin(a + hw), cy - r0 * math.cos(a + hw)),
           (cx + r1 * math.sin(a + hw), cy - r1 * math.cos(a + hw)), (cx + r1 * math.sin(a - hw), cy - r1 * math.cos(a - hw))]
    ticks += f'<path fill="{INK}" d="{poly_d(pts)}"/>'
ticks += f'<circle cx="{cx}" cy="{cy}" r="44.5" fill="none" stroke="{INK}" stroke-width="3"/>'
emit("dial_ticks", svg(W * 2, H * 2, ticks, view=(0, 0, W, H)), (W * 2, H * 2), ss=2, mip=True, expand=(Ld, Td, Rd, Bd),
     serves="Integrity dial overlay: 12 ticks (every 30deg, 2deg wide, 9 long, ring inset 2%) and the 3px black ring around the wedge disc (r 43-46). Same canvas as dial_back.")

# ---------------------------------------------------------------------------------------------
# 8. Icons (raw layers, composited by finish_pulp_icons.py) and the marker
# ---------------------------------------------------------------------------------------------
ICON_PX = 64
for name, spec in G.ICONS.items():
    for lay, body in G.layers(spec).items():
        body = body or '<rect width="1" height="1" fill="#000" fill-opacity="0"/>'
        view = spec.get("view", (0, 0, 32, 32))
        pxw = ICON_PX if view[2] == 32 else int(view[2] * 2)
        pxh = ICON_PX if view[3] == 32 else int(view[3] * 2)
        emit(f"ico_{name}_{lay}_raw", svg(pxw, pxh, body, view=view), (pxw, pxh), group="pulp_icons_raw", ss=4, raw=True)

# ---------------------------------------------------------------------------------------------
# catalog, jobs
# ---------------------------------------------------------------------------------------------
with open(os.path.join(HERE, "catalog_svg.json"), "w", encoding="utf-8", newline="\n") as fh:
    json.dump(CATALOG, fh, indent=1, sort_keys=True)

NL = chr(10)
CR = chr(13)
text = open(JOBS, encoding="utf-8", newline="").read()
nl = CR + NL if (CR + NL) in text else NL
text = text.replace(CR + NL, NL)
cut = text.find('"group": "pulp')
if cut >= 0:                                   # idempotent: drop the jobs a previous run appended
    cut = text.rfind("    {" + NL, 0, cut)
    text = text[:cut].rstrip().rstrip(",") + NL + "  ]" + NL + "}" + NL
head = text[: text.rindex("]")].rstrip()
body = ("," + NL).join("    " + json.dumps(j, indent=2).replace(NL, NL + "    ") for j in JOBS_NEW)
text = head + "," + NL + body + NL + "  ]" + NL + "}" + NL
json.loads(text)
with open(JOBS, "w", encoding="utf-8", newline="") as fh:
    fh.write(text.replace(NL, nl))
print("svgs:", len(JOBS_NEW), "catalog entries:", len(CATALOG))
