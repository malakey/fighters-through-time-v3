"""Harriet Tubman placeholder sprite set builder (Package 13 W5, decision D5).

Emits the SVG sources for Tubman's retro-contract presentation:

  generated/cells/tubman_<animation>_<frame>.svg   one 96x128 SVG per character frame
                                       (30 contract animations minus grab/throw, which
                                       reuse combat rows, x 3 frames)
  generated/cells/tubman_<ability>_<frame>.svg     one 192x192 SVG per ability-VFX frame
  generated/tubman_portrait.svg        512x512 head-and-shoulders portrait

The "tubman" group in tools/asset_src/jobs.json packs the cells into the six
retro atlases (svg_atlas jobs, rows in the order
tools/build_retro_character_spriteframes.py reads them) and the VFX atlas.
check_tubman_cells.gd audits the packed atlases for cell-edge bleed.

Original placeholder art — a parametric paper doll, not derived from any other
roster character's art. A dignified neutral silhouette: a Black woman of the
1850s in a long dark-blue dress, brown jacket and shawl, a head wrap, a walking
cane in the far hand and a small lantern (warm gold light) in the near hand.
Facing right, feet 3 px above the cell's bottom edge, like the rest of the roster.

ThorVG-safe SVG subset only (paths, circles, gradients, opacity, transforms).

Rebuild:
  python tools/asset_src/tubman/build_tubman_svgs.py
  Godot_console --headless --path <repo> --script res://tools/generate_assets.gd -- group=tubman
  python tools/build_retro_character_spriteframes.py tubman
  Godot_console --headless --path <repo> --import
  Godot_console --headless --path <repo> --script res://tools/asset_src/tubman/check_tubman_cells.gd
"""
from __future__ import annotations

import math
import os

HERE = os.path.dirname(os.path.abspath(__file__))
GEN = os.path.join(HERE, "generated")

CELL_W, CELL_H = 96, 128
BASELINE = 125  # feet sit 3 px above the cell's bottom edge

# --- palette ---------------------------------------------------------------
OUT = "#120c0a"
SKIN = "#5a3622"
SKIN_HI = "#7a4d31"
WRAP = "#9a4a2a"
WRAP_D = "#6e2f1b"
DRESS = "#22325a"
DRESS_D = "#16213e"
JACKET = "#4e3322"
JACKET_D = "#35221a"
SHAWL = "#735236"
BOOT = "#1c130d"
CANE = "#6b4424"
LANTERN_FRAME = "#2b1d12"
GOLD = "#ffcf5a"
GOLD_HOT = "#fff1bf"
GLOW = "#ffb43a"
SPECTRAL = "#bfe3ff"

SHEETS = {
    "locomotion": ["idle", "run", "jump", "fall", "skid", "crouch"],
    "traversal": ["roll_startup", "roll", "roll_recovery", "ledge_hang", "ledge_pull_up", "ledge_drop"],
    "combat": ["basic_attack_1", "basic_attack_2", "basic_attack_3", "block", "hitstun"],
    "states": ["dazed", "death", "respawn", "victory", "defeat"],
    "abilities": ["special_1", "special_2", "movement_ability", "ultimate"],
    "directional_attacks": ["up_attack", "down_air"],
}

VFX_ROWS = ["tubman_conductors_call", "tubman_foresight", "tubman_north_star_leap", "tubman_freedom_line"]


def polar(length: float, angle: float):
    """0 deg points down, 90 points forward (+x), 180 points up (SVG y-down)."""
    r = math.radians(angle)
    return length * math.sin(r), length * math.cos(r)


def f(v: float) -> str:
    return f"{v:.2f}".rstrip("0").rstrip(".")


DEFS = f"""<defs>
<radialGradient id="glow"><stop offset="0" stop-color="{GOLD_HOT}" stop-opacity="0.85"/>
<stop offset="0.35" stop-color="{GLOW}" stop-opacity="0.45"/><stop offset="1" stop-color="{GLOW}" stop-opacity="0"/></radialGradient>
<radialGradient id="glass"><stop offset="0" stop-color="{GOLD_HOT}"/><stop offset="1" stop-color="{GOLD}"/></radialGradient>
<radialGradient id="spectral"><stop offset="0" stop-color="#ffffff" stop-opacity="0.9"/>
<stop offset="0.5" stop-color="{SPECTRAL}" stop-opacity="0.55"/><stop offset="1" stop-color="{SPECTRAL}" stop-opacity="0"/></radialGradient>
<linearGradient id="skirt" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="{DRESS}"/><stop offset="1" stop-color="{DRESS_D}"/></linearGradient>
</defs>"""


# --- the paper doll ---------------------------------------------------------

DEFAULT = dict(
    ox=0.0, oy=0.0, rot=0.0, pivot=(0.0, -50.0), lean=0.0, crouch=0.0,
    ff=5.0, fb=-5.0, lf=0.0, lb=0.0, sway=0.0, flare=0.0,
    near=(25.0, 45.0), far=(15.0, 35.0), cane=6.0, cane_len=50.0,
    lantern=True, glow=1.0, opacity=1.0, head_tilt=0.0, scale=1.0,
)


def pose(**kw):
    p = dict(DEFAULT)
    p.update(kw)
    return p


def arm(shoulder, a_up, a_fore, sleeve):
    sx, sy = shoulder
    ux, uy = polar(14.5, a_up)
    ex, ey = sx + ux, sy + uy
    fx, fy = polar(13.5, a_fore)
    hx, hy = ex + fx, ey + fy
    svg = (
        f'<path d="M{f(sx)},{f(sy)} L{f(ex)},{f(ey)} L{f(hx)},{f(hy)}" fill="none" stroke="{OUT}" '
        f'stroke-width="7.2" stroke-linecap="round" stroke-linejoin="round"/>'
        f'<path d="M{f(sx)},{f(sy)} L{f(ex)},{f(ey)} L{f(hx)},{f(hy)}" fill="none" stroke="{sleeve}" '
        f'stroke-width="5" stroke-linecap="round" stroke-linejoin="round"/>'
        f'<circle cx="{f(hx)}" cy="{f(hy)}" r="2.9" fill="{SKIN}" stroke="{OUT}" stroke-width="1"/>'
    )
    return svg, (hx, hy)


def cane_svg(hand, angle, length):
    hx, hy = hand
    dx, dy = polar(1.0, angle)
    tx, ty = hx + dx * length, hy + dy * length
    bx, by = hx - dx * 3.0, hy - dy * 3.0
    # crook: curls back over the hand, perpendicular to the shaft
    px, py = -dy, dx
    cx1, cy1 = bx - dx * 4 + px * 5, by - dy * 4 + py * 5
    ex, ey = bx + px * 6, by + py * 6
    return (
        f'<path d="M{f(tx)},{f(ty)} L{f(bx)},{f(by)} Q{f(cx1)},{f(cy1)} {f(ex)},{f(ey)}" fill="none" '
        f'stroke="{OUT}" stroke-width="4" stroke-linecap="round"/>'
        f'<path d="M{f(tx)},{f(ty)} L{f(bx)},{f(by)} Q{f(cx1)},{f(cy1)} {f(ex)},{f(ey)}" fill="none" '
        f'stroke="{CANE}" stroke-width="2.3" stroke-linecap="round"/>'
    )


def lantern_svg(hand, counter_rot, glow):
    hx, hy = hand
    g = []
    g.append(f'<g transform="rotate({f(counter_rot)} {f(hx)} {f(hy)})">')
    g.append(f'<path d="M{f(hx)},{f(hy)} L{f(hx)},{f(hy + 3.5)}" stroke="{OUT}" stroke-width="1.3"/>')
    top = hy + 3.5
    g.append(f'<path d="M{f(hx - 2.5)},{f(top + 2)} L{f(hx + 2.5)},{f(top + 2)} L{f(hx + 1.5)},{f(top)} '
             f'L{f(hx - 1.5)},{f(top)} Z" fill="{LANTERN_FRAME}" stroke="{OUT}" stroke-width="0.8"/>')
    g.append(f'<rect x="{f(hx - 3.6)}" y="{f(top + 2)}" width="7.2" height="8.5" rx="1.2" fill="url(#glass)" '
             f'stroke="{LANTERN_FRAME}" stroke-width="1.3"/>')
    g.append(f'<path d="M{f(hx)},{f(top + 2)} L{f(hx)},{f(top + 10.5)}" stroke="{LANTERN_FRAME}" stroke-width="0.8"/>')
    g.append(f'<rect x="{f(hx - 3)}" y="{f(top + 10.5)}" width="6" height="1.6" fill="{LANTERN_FRAME}"/>')
    g.append('</g>')
    return "".join(g), (hx, hy + 9.5)


def glow_svg(center, radius, opacity=1.0):
    cx, cy = center
    return (f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(radius)}" fill="url(#glow)" '
            f'opacity="{f(opacity)}"/>')


def figure(p):
    """Returns SVG for one doll, local frame: feet centre at (0,0), facing +x."""
    crouch = p["crouch"]
    wy = -56.0 + 22.0 * crouch  # waist height
    hem = -5.0 - p["flare"] * 0.6
    ff, fb = p["ff"], p["fb"]
    left = min(ff, fb) - 9.0 - p["flare"] * 0.5
    right = max(ff, fb) + 9.0 + p["flare"] * 0.5
    left = min(left, -14.0 - crouch * 4)
    right = max(right, 14.0 + crouch * 4)
    left += p["sway"]
    right += p["sway"]
    mid = (wy + hem) / 2

    out = []
    # boots
    for fx_, lift in ((fb, p["lb"]), (ff, p["lf"])):
        out.append(f'<ellipse cx="{f(fx_ + 1.5)}" cy="{f(-3.4 - lift)}" rx="5" ry="2.8" fill="{BOOT}" '
                   f'stroke="{OUT}" stroke-width="1"/>')
    # skirt
    out.append(
        f'<path d="M-9,{f(wy)} L9,{f(wy)} Q{f(right + 1)},{f(mid)} {f(right)},{f(hem)} '
        f'Q{f((left + right) / 2)},{f(hem + 3)} {f(left)},{f(hem)} Q{f(left - 1)},{f(mid)} -9,{f(wy)} Z" '
        f'fill="url(#skirt)" stroke="{OUT}" stroke-width="1.3" stroke-linejoin="round"/>')
    for t in (0.3, 0.62):
        x_top = -9 + 18 * t
        x_bot = left + (right - left) * t
        out.append(f'<path d="M{f(x_top)},{f(wy + 4)} Q{f((x_top + x_bot) / 2 + 1)},{f(mid)} {f(x_bot)},{f(hem + 1)}" '
                   f'fill="none" stroke="{DRESS_D}" stroke-width="1.4" opacity="0.9"/>')

    # torso group (lean pivots at the waist)
    lean = p["lean"]
    total_rot = lean + p["rot"]
    t = [f'<g transform="rotate({f(lean)} 0 {f(wy)})">']
    sh_far = (-1.5, wy - 26)
    sh_near = (3.0, wy - 26)
    far_svg, far_hand = arm(sh_far, p["far"][0], p["far"][1], JACKET_D)
    t.append(far_svg)
    if p["cane"] is not None:
        t.append(cane_svg(far_hand, p["cane"] - lean, p["cane_len"]))
    # bodice / jacket
    t.append(f'<path d="M-9,{f(wy)} L9.5,{f(wy)} L10.5,{f(wy - 24)} Q6,{f(wy - 30)} 0,{f(wy - 30)} '
             f'Q-7,{f(wy - 30)} -10,{f(wy - 24)} Z" fill="{JACKET}" stroke="{OUT}" stroke-width="1.3" '
             f'stroke-linejoin="round"/>')
    t.append(f'<path d="M-9.5,{f(wy)} L9.5,{f(wy)} L9,{f(wy + 3)} L-9,{f(wy + 3)} Z" fill="{JACKET_D}"/>')
    # shawl across shoulders
    t.append(f'<path d="M-11,{f(wy - 25)} Q0,{f(wy - 32)} 11,{f(wy - 25)} L6,{f(wy - 13)} L2,{f(wy - 18)} '
             f'L-6,{f(wy - 12)} Z" fill="{SHAWL}" stroke="{OUT}" stroke-width="1.1" stroke-linejoin="round"/>')
    # neck + head
    hx, hy = 2.0, wy - 39
    t.append(f'<g transform="rotate({f(p["head_tilt"])} {f(hx)} {f(hy + 8)})">')
    t.append(f'<rect x="-0.5" y="{f(wy - 33)}" width="5" height="5" fill="{SKIN}" stroke="{OUT}" stroke-width="0.9"/>')
    t.append(f'<ellipse cx="{f(hx + 1)}" cy="{f(hy)}" rx="8" ry="8.8" fill="{SKIN}" stroke="{OUT}" stroke-width="1.2"/>')
    t.append(f'<path d="M{f(hx + 6)},{f(hy + 1)} Q{f(hx + 8.5)},{f(hy + 3)} {f(hx + 6.5)},{f(hy + 4.5)}" fill="none" '
             f'stroke="{OUT}" stroke-width="0.9"/>')
    t.append(f'<circle cx="{f(hx + 5)}" cy="{f(hy - 0.5)}" r="0.9" fill="{OUT}"/>')
    t.append(f'<path d="M{f(hx + 2)},{f(hy + 4)} Q{f(hx + 5)},{f(hy + 6)} {f(hx + 7.5)},{f(hy + 4.5)}" fill="none" '
             f'stroke="{SKIN_HI}" stroke-width="1"/>')
    # head wrap: covers crown and back, knot at the back
    t.append(f'<path d="M{f(hx + 8.5)},{f(hy - 3)} Q{f(hx + 8)},{f(hy - 12)} {f(hx - 1)},{f(hy - 11.5)} '
             f'Q{f(hx - 9)},{f(hy - 11)} {f(hx - 8.5)},{f(hy - 1)} Q{f(hx - 8)},{f(hy + 5)} {f(hx - 4)},{f(hy + 4)} '
             f'Q{f(hx - 2)},{f(hy - 3)} {f(hx + 2)},{f(hy - 4)} Z" fill="{WRAP}" stroke="{OUT}" stroke-width="1.2" '
             f'stroke-linejoin="round"/>')
    t.append(f'<path d="M{f(hx - 6)},{f(hy - 9)} Q{f(hx + 1)},{f(hy - 5)} {f(hx + 7.5)},{f(hy - 5)}" fill="none" '
             f'stroke="{WRAP_D}" stroke-width="1.2"/>')
    t.append(f'<ellipse cx="{f(hx - 9.5)}" cy="{f(hy - 7)}" rx="3.3" ry="2.6" fill="{WRAP}" stroke="{OUT}" '
             f'stroke-width="1"/>')
    t.append('</g>')
    near_svg, near_hand = arm(sh_near, p["near"][0], p["near"][1], JACKET)
    lantern_part = ""
    lantern_center = None
    if p["lantern"]:
        lantern_part, lantern_center = lantern_svg(near_hand, -total_rot, p["glow"])
    t.append(near_svg)
    t.append(lantern_part)
    if lantern_center is not None and p["glow"] >= 0.4:  # below 0.4 the flame is guttering: no halo
        t.append(glow_svg(lantern_center, min(22.0, 11 + 6 * p["glow"]), min(1.0, 0.45 + 0.35 * p["glow"])))
    t.append('</g>')
    out.extend(t)

    px, py = p["pivot"]
    body = "".join(out)
    return (f'<g transform="translate({f(p["ox"])} {f(p["oy"])}) rotate({f(p["rot"])} {f(px)} {f(py)}) '
            f'scale({f(p["scale"])})" opacity="{f(p["opacity"])}">{body}</g>')


# --- effects (cell-local coordinates, feet origin) --------------------------

def arc_fx(cx, cy, r, a0, a1, color=GOLD, width=3.0, opacity=0.85):
    x0, y0 = cx + r * math.cos(math.radians(a0)), cy + r * math.sin(math.radians(a0))
    x1, y1 = cx + r * math.cos(math.radians(a1)), cy + r * math.sin(math.radians(a1))
    large = 1 if abs(a1 - a0) > 180 else 0
    sweep = 1 if a1 > a0 else 0
    return (f'<path d="M{f(x0)},{f(y0)} A{f(r)},{f(r)} 0 {large} {sweep} {f(x1)},{f(y1)}" fill="none" '
            f'stroke="{color}" stroke-width="{f(width)}" stroke-linecap="round" opacity="{f(opacity)}"/>')


def star_fx(cx, cy, r, color=GOLD_HOT, opacity=1.0):
    pts = []
    for i in range(8):
        rad = r if i % 2 == 0 else r * 0.32
        a = math.radians(i * 45 - 90)
        pts.append(f"{f(cx + rad * math.cos(a))},{f(cy + rad * math.sin(a))}")
    return f'<path d="M{" L".join(pts)} Z" fill="{color}" opacity="{f(opacity)}"/>'


def line_fx(x0, y0, x1, y1, color=GOLD_HOT, width=1.6, opacity=0.8):
    return (f'<path d="M{f(x0)},{f(y0)} L{f(x1)},{f(y1)}" stroke="{color}" stroke-width="{f(width)}" '
            f'stroke-linecap="round" opacity="{f(opacity)}"/>')


# --- animation poses ---------------------------------------------------------

def frames_for(name):
    """Three (pose, fx_before, fx_after) frames per animation."""
    P = pose
    if name == "idle":
        return [(P(), "", ""), (P(oy=-0.6, glow=1.15, near=(27, 47)), "", ""), (P(glow=0.95, sway=0.6), "", "")]
    if name == "run":
        return [
            (P(lean=12, ff=13, fb=-9, lb=3, near=(55, 85), far=(-30, -5), cane=-20, cane_len=36, sway=-2), "", ""),
            (P(lean=12, oy=-2, ff=3, fb=-2, lf=4, near=(30, 60), far=(0, 20), cane=-30, cane_len=40, sway=-1), "", ""),
            (P(lean=12, ff=-8, fb=12, lf=3, near=(5, 40), far=(35, 55), cane=-20, cane_len=40, sway=-2), "", ""),
        ]
    if name == "jump":
        return [
            (P(oy=-4, crouch=0.2, ff=6, fb=-3, lf=3, lb=5, flare=4, near=(110, 140), far=(40, 70), cane=-30), "", ""),
            (P(oy=-7, ff=5, fb=-4, lf=6, lb=7, flare=6, near=(125, 155), far=(50, 80), cane=-35), "", ""),
            (P(oy=-6, ff=4, fb=-4, lf=5, lb=6, flare=5, near=(115, 145), far=(45, 75), cane=-30), "", ""),
        ]
    if name == "fall":
        return [
            (P(oy=-3, ff=6, fb=-6, flare=8, near=(95, 120), far=(70, 100), cane=-40, glow=1.1), "", ""),
            (P(oy=-3, ff=7, fb=-5, flare=10, near=(100, 125), far=(75, 105), cane=-45, glow=1.2), "", ""),
            (P(oy=-3, ff=6, fb=-6, flare=9, near=(98, 122), far=(72, 102), cane=-42, glow=1.1), "", ""),
        ]
    if name == "skid":
        return [
            (P(lean=-12, ox=-8, ff=14, fb=-4, near=(-20, 10), far=(45, 60), cane=35, cane_len=34), "", line_fx(-26, -3, -12, -3)),
            (P(lean=-15, ox=-8, ff=15, fb=-5, near=(-25, 5), far=(50, 65), cane=38, cane_len=34), "", line_fx(-30, -2, -14, -2)),
            (P(lean=-8, ox=-6, ff=12, fb=-3, near=(-10, 20), far=(40, 55), cane=30, cane_len=36), "", ""),
        ]
    if name == "crouch":
        return [
            (P(crouch=0.8, lean=18, ff=8, fb=-7, near=(40, 70), far=(30, 45), cane=20, cane_len=38), "", ""),
            (P(crouch=0.9, lean=20, ff=8, fb=-7, near=(42, 72), far=(32, 47), cane=22, cane_len=36, glow=0.9), "", ""),
            (P(crouch=0.85, lean=19, ff=8, fb=-7, near=(41, 71), far=(31, 46), cane=21, cane_len=37), "", ""),
        ]
    if name == "roll_startup":
        return [
            (P(ox=-4, crouch=0.7, lean=30, near=(40, 90), far=(40, 80), cane=100, cane_len=26), "", ""),
            (P(ox=-4, crouch=0.9, lean=45, near=(60, 110), far=(55, 95), cane=110, cane_len=24), "", ""),
            (P(ox=-5, crouch=1.0, lean=60, oy=-2, near=(80, 130), far=(70, 110), cane=120, cane_len=22, flare=4), "", ""),
        ]
    if name == "roll":
        base = dict(crouch=1.0, lean=55, near=(90, 150), far=(80, 130), cane=120, cane_len=20, flare=6,
                    pivot=(0.0, -30.0), ff=3, fb=-3, scale=0.8)
        return [
            (P(rot=90, oy=-18, **base), "", ""),
            (P(rot=200, oy=-4, **base), "", ""),
            (P(rot=310, oy=-4, **base), "", ""),
        ]
    if name == "roll_recovery":
        return [
            (P(crouch=0.7, lean=25, near=(40, 70), far=(30, 50), cane=15, cane_len=40), "", ""),
            (P(crouch=0.4, lean=12, near=(30, 55), far=(20, 40), cane=10, cane_len=46), "", ""),
            (P(crouch=0.1, lean=4), "", ""),
        ]
    if name == "ledge_hang":
        base = dict(near=(175, 178), far=(172, 176), cane=10, cane_len=40, ff=3, fb=-3, lf=0, flare=-2)
        return [
            (P(oy=-5, **base), "", ""),
            (P(oy=-5.5, sway=1, **base), "", ""),
            (P(oy=-5, sway=-1, **base), "", ""),
        ]
    if name == "ledge_pull_up":
        return [
            (P(oy=3, crouch=0.3, near=(160, 175), far=(158, 172), cane=150, cane_len=20, lf=6, lb=2), "", ""),
            (P(oy=-4, ox=-10, crouch=0.6, lean=15, near=(120, 20), far=(115, 25), cane=150, cane_len=34, lf=8), "", ""),
            (P(crouch=0.4, lean=10, near=(40, 60), far=(30, 45), cane=15, cane_len=44), "", ""),
        ]
    if name == "ledge_drop":
        return [
            (P(oy=-5, near=(165, 175), far=(160, 170), cane=10, cane_len=40, flare=2), "", ""),
            (P(oy=-4, near=(130, 155), far=(120, 150), cane=-20, cane_len=44, flare=5), "", ""),
            (P(oy=-3, near=(100, 125), far=(80, 110), cane=-35, flare=7), "", ""),
        ]
    if name == "basic_attack_1":  # cane jab
        return [
            (P(ox=-8, lean=-6, ff=9, fb=-7, far=(-20, 10), cane=-45, cane_len=36, near=(20, 40)), "", ""),
            (P(ox=-16, lean=10, ff=12, fb=-8, far=(80, 88), cane=92, cane_len=27, near=(10, 25)), "",
             line_fx(12, -58, 32, -58) + line_fx(16, -64, 30, -64, width=1.1)),
            (P(ox=-14, lean=6, ff=11, fb=-8, far=(65, 80), cane=78, cane_len=33, near=(15, 30)), "", ""),
        ]
    if name == "basic_attack_2":  # overhead cane swing
        return [
            (P(ox=-6, lean=-8, ff=8, fb=-7, far=(130, 160), cane=230, cane_len=24, near=(10, 30)), "", ""),
            (P(ox=-15, lean=14, ff=12, fb=-8, far=(95, 115), cane=125, cane_len=27, near=(0, 20)), "",
             arc_fx(4, -56, 34, -80, 20, width=2.6)),
            (P(ox=-14, lean=10, ff=11, fb=-8, far=(70, 90), cane=100, cane_len=26, near=(5, 25)), "", ""),
        ]
    if name == "basic_attack_3":  # lantern swing launcher
        return [
            (P(ox=-4, lean=-6, ff=8, fb=-8, near=(-45, -30), far=(20, 30), cane=8, glow=1.0), "", ""),
            (P(ox=-8, lean=10, ff=12, fb=-8, near=(70, 90), far=(20, 30), cane=10, glow=1.6), "",
             arc_fx(4, -62, 26, 150, 20, color=GLOW, width=3.4, opacity=0.7)),
            (P(ox=-6, lean=4, ff=10, fb=-8, near=(150, 170), far=(20, 30), cane=10, glow=2.0), "",
             arc_fx(4, -70, 28, 60, -60, color=GOLD, width=3.0, opacity=0.6) + star_fx(26, -100, 4)),
        ]
    if name == "block":
        ring = arc_fx(6, -60, 36, -65, 65, color=SPECTRAL, width=2.4, opacity=0.6)
        base = dict(ff=7, fb=-8, lean=-4, far=(70, 150), cane=178, cane_len=28, near=(30, 70))
        return [
            (P(**base), "", ring),
            (P(glow=1.2, **base), "", arc_fx(6, -60, 36, -70, 70, color=SPECTRAL, width=2.8, opacity=0.7)),
            (P(**base), "", ring),
        ]
    if name == "hitstun":
        return [
            (P(ox=-4, lean=-20, ff=8, fb=-8, near=(-40, -10), far=(-30, -60), cane=-60, cane_len=30, glow=0.6, head_tilt=-10), "",
             star_fx(24, -86, 3.5, color="#ffffff")),
            (P(ox=-2, lean=-24, oy=-1, ff=6, fb=-10, near=(-50, -20), far=(-40, -70), cane=-70, cane_len=22, glow=0.5,
               head_tilt=-14), "", ""),
            (P(ox=-5, lean=-14, ff=7, fb=-8, near=(-25, 0), far=(-20, -50), cane=-50, cane_len=32, glow=0.7, head_tilt=-6), "", ""),
        ]
    if name == "dazed":
        def stars(k):
            s = ""
            for i in range(3):
                a = math.radians(k * 40 + i * 120)
                s += star_fx(4 + 11 * math.cos(a), -112 + 3.5 * math.sin(a), 3.2)
            return s
        return [
            (P(lean=-6, near=(10, 20), far=(10, 20), cane=5, glow=0.6, head_tilt=-8), "", stars(0)),
            (P(lean=-2, sway=1, near=(12, 22), far=(12, 22), cane=5, glow=0.6, head_tilt=6), "", stars(1)),
            (P(lean=-8, sway=-1, near=(8, 18), far=(8, 18), cane=5, glow=0.6, head_tilt=-4), "", stars(2)),
        ]
    if name == "death":
        return [
            (P(crouch=0.8, lean=30, near=(40, 20), far=(30, 20), cane=40, cane_len=28, glow=0.5, head_tilt=12), "", ""),
            (P(crouch=0.6, rot=-50, pivot=(0, -4), ox=24, oy=-10, scale=0.9, near=(60, 40), far=(50, 30), cane=40, cane_len=22,
               glow=0.3, lantern=False), "", lantern_svg((-14, -26), 40, 0)[0]),
            (P(crouch=0.5, rot=-86, pivot=(0, -4), ox=36, oy=-10, scale=0.82, near=(90, 70), far=(80, 60), cane=80, cane_len=22,
               glow=0.15, lantern=False), lantern_svg((-24, -9), 80, 0)[0], ""),
        ]
    if name == "respawn":
        return [
            (P(opacity=0.35, glow=2.0), glow_svg((0, -56), 40, 0.7), ""),
            (P(opacity=0.7, glow=1.6), glow_svg((0, -56), 37, 0.5), ""),
            (P(opacity=1.0, glow=1.2), glow_svg((0, -56), 33, 0.3), ""),
        ]
    if name == "victory":
        return [
            (P(near=(150, 160), cane=6, glow=1.6), "", ""),
            (P(near=(170, 175), cane=6, glow=2.0, oy=-1), "", star_fx(24, -112, 3.5) + star_fx(-16, -100, 2.5)),
            (P(near=(168, 174), cane=6, glow=1.8), "", star_fx(-14, -112, 3) + star_fx(26, -98, 2.5)),
        ]
    if name == "defeat":
        return [
            (P(crouch=0.9, lean=30, near=(20, 5), far=(40, 20), cane=30, cane_len=36, glow=0.35, head_tilt=18), "", ""),
            (P(crouch=0.95, lean=32, near=(18, 3), far=(40, 20), cane=30, cane_len=36, glow=0.3, head_tilt=20), "", ""),
            (P(crouch=0.9, lean=30, near=(20, 5), far=(40, 20), cane=30, cane_len=36, glow=0.35, head_tilt=18), "", ""),
        ]
    if name == "special_1":  # Conductor's Call: lantern thrust forward, cane raised
        return [
            (P(ox=-6, ff=8, fb=-8, near=(50, 70), far=(105, 125), cane=160, cane_len=28, glow=1.2), "", ""),
            (P(ox=-10, lean=8, ff=11, fb=-8, near=(85, 92), far=(115, 135), cane=165, cane_len=28, glow=1.8), "",
             line_fx(26, -8, 40, -8, color=SPECTRAL) + line_fx(22, -16, 38, -16, color=SPECTRAL, width=1.1)),
            (P(ox=-9, lean=6, ff=10, fb=-8, near=(80, 90), far=(110, 130), cane=162, cane_len=28, glow=1.5), "", ""),
        ]
    if name == "special_2":  # Foresight: lantern held close, counter stance
        return [
            (P(ff=6, fb=-8, lean=-4, near=(60, 150), far=(35, 55), cane=12, glow=1.3), "",
             arc_fx(2, -60, 30, 0, 359, color=GOLD, width=1.6, opacity=0.35)),
            (P(ff=6, fb=-8, lean=-4, near=(62, 152), far=(35, 55), cane=12, glow=1.7), "",
             arc_fx(2, -60, 36, 0, 359, color=GOLD, width=2.2, opacity=0.55)),
            (P(ff=6, fb=-8, lean=-4, near=(60, 150), far=(35, 55), cane=12, glow=1.4), "",
             arc_fx(2, -60, 40, 0, 359, color=GOLD, width=1.4, opacity=0.3)),
        ]
    if name == "movement_ability":  # North Star Leap
        return [
            (P(ox=-6, crouch=0.5, lean=20, ff=8, fb=-8, near=(110, 140), far=(20, 30), cane=-20, cane_len=34, glow=1.3), "", ""),
            (P(oy=-8, ox=-6, lean=14, ff=6, fb=-6, lf=6, lb=4, flare=6, near=(145, 160), far=(-20, -10), cane=-25, cane_len=32,
               glow=1.8), "", star_fx(26, -110, 5) + line_fx(-30, -20, -6, -40, color=GOLD, width=2, opacity=0.6)),
            (P(oy=-6, ox=-6, lean=12, ff=6, fb=-6, lf=4, lb=3, flare=5, near=(130, 150), far=(0, 10), cane=-20, cane_len=34,
               glow=1.5), "", star_fx(28, -110, 3.5, opacity=0.8)),
        ]
    if name == "ultimate":  # Freedom Line: lantern lifted high, blazing
        return [
            (P(ff=8, fb=-8, near=(140, 160), far=(20, 30), cane=6, glow=1.8), glow_svg((6, -80), 34, 0.4), ""),
            (P(ff=8, fb=-8, near=(172, 176), far=(25, 35), cane=6, glow=2.6, oy=-1),
             glow_svg((6, -86), 36, 0.6), star_fx(20, -114, 4)),
            (P(ff=8, fb=-8, near=(165, 172), far=(25, 35), cane=6, glow=2.2), glow_svg((6, -84), 35, 0.5), ""),
        ]
    if name == "up_attack":
        return [
            (P(ox=-4, crouch=0.4, lean=4, ff=8, fb=-8, far=(40, 20), cane=-10, cane_len=40), "", ""),
            (P(ox=-6, oy=-2, ff=8, fb=-8, far=(130, 150), cane=178, cane_len=16, near=(20, 40)), "",
             arc_fx(0, -92, 20, 200, 340, width=2.6)),
            (P(ox=-5, oy=-1, ff=8, fb=-8, far=(125, 145), cane=170, cane_len=16, near=(20, 40)), "", ""),
        ]
    if name == "down_air":
        return [
            (P(oy=-10, ff=5, fb=-5, lf=6, lb=6, flare=6, far=(100, 130), cane=120, cane_len=18, near=(60, 90)), "", ""),
            (P(oy=-12, ff=5, fb=-5, lf=8, lb=8, flare=8, far=(15, 5), cane=2, cane_len=27, near=(80, 110)), "",
             line_fx(20, -16, 20, -6, width=2.4) + line_fx(14, -18, 14, -9, width=1.4)),
            (P(oy=-11, ff=5, fb=-5, lf=7, lb=7, flare=7, far=(20, 10), cane=5, cane_len=27, near=(70, 100)), "", ""),
        ]
    raise KeyError(name)


def cell(col, row, content):
    x = col * CELL_W + CELL_W / 2
    y = row * CELL_H + BASELINE
    return f'<g transform="translate({f(x)} {f(y)})">{content}</g>'


def cell_svg(content, x, y, w, h):
    """One atlas cell as its own SVG document (a viewBox window at x,y)."""
    return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="{x} {y} {w} {h}">'
            f'{DEFS}{content}</svg>\n')


def character_cells():
    """Yields (file stem, svg) for every character animation frame."""
    for animations in SHEETS.values():
        for anim in animations:
            for col, (p, before, after) in enumerate(frames_for(anim)):
                yield f"tubman_{anim}_{col}", cell_svg(cell(0, 0, before + figure(p) + after), 0, 0, CELL_W, CELL_H)


# --- ability VFX atlas (576x768, 192x192 cells) -----------------------------

def spectral_scout(x, y, scale, opacity):
    """A pale, translucent running guide silhouette (original placeholder)."""
    s = scale
    return (
        f'<g transform="translate({f(x)} {f(y)}) scale({f(s)})" opacity="{f(opacity)}">'
        f'<ellipse cx="0" cy="-26" rx="20" ry="30" fill="url(#spectral)"/>'
        f'<path d="M-6,-30 Q0,-38 7,-30 L12,-6 Q0,-2 -12,-6 Z" fill="{SPECTRAL}" opacity="0.75"/>'
        f'<circle cx="2" cy="-38" r="5" fill="#ffffff" opacity="0.85"/>'
        f'<path d="M5,-28 L16,-22" stroke="{SPECTRAL}" stroke-width="2.4" stroke-linecap="round"/>'
        f'<circle cx="17" cy="-20" r="2.6" fill="{GOLD_HOT}"/>'
        f'<path d="M-12,-10 L-40,-12 M-10,-18 L-34,-22" stroke="{SPECTRAL}" stroke-width="1.6" opacity="0.6" '
        f'stroke-linecap="round"/>'
        f'</g>'
    )


def vfx_cell(row, col):
    cx, cy = col * 192 + 96, row * 192 + 96
    ground = row * 192 + 170
    g = []
    if row == 0:  # Conductor's Call: spectral scouts rushing along the ground
        offsets = [(-40, 1.0, 0.8), (0, 0.85, 0.65), (38, 0.7, 0.5)]
        shift = [10, 20, 34][col]
        fade = [1.0, 1.0, 0.55][col]
        g.append(f'<rect x="{col * 192 + 12}" y="{ground - 3}" width="168" height="5" rx="2.5" fill="{GOLD}" '
                 f'opacity="{f(0.25 * fade)}"/>')
        for dx, sc, op in offsets:
            x = cx + dx + shift * sc
            if x < col * 192 + 30 or x > col * 192 + 170:
                continue
            g.append(spectral_scout(x, ground, 1.3 * sc, op * fade))
    elif row == 1:  # Foresight: lantern-lit counter ring
        r = [44, 62, 70][col]
        op = [0.6, 0.95, 0.55][col]
        g.append(f'<circle cx="{cx}" cy="{cy}" r="{r + 14}" fill="url(#glow)" opacity="{f(op * 0.8)}"/>')
        g.append(f'<circle cx="{cx}" cy="{cy}" r="{r}" fill="none" stroke="{GOLD}" stroke-width="5" opacity="{f(op)}"/>')
        g.append(f'<circle cx="{cx}" cy="{cy}" r="{r - 10}" fill="none" stroke="{GOLD_HOT}" stroke-width="2" '
                 f'stroke-dasharray="6 6" opacity="{f(op)}"/>')
        for i in range(8):
            a = math.radians(i * 45 + col * 15)
            g.append(star_fx(cx + r * math.cos(a), cy + r * math.sin(a), 5, opacity=op))
        g.append(f'<rect x="{cx - 7}" y="{cy - 9}" width="14" height="18" rx="2" fill="url(#glass)" '
                 f'stroke="{LANTERN_FRAME}" stroke-width="2" opacity="{f(min(1, op + 0.2))}"/>')
        if col == 2:
            g.append(f'<circle cx="{cx}" cy="{cy}" r="84" fill="none" stroke="#ffffff" stroke-width="2" opacity="0.5"/>')
    elif row == 2:  # North Star Leap: a north-star streak
        head = [(cx - 20, cy + 20), (cx + 20, cy - 25), (cx + 45, cy - 50)][col]
        tail_len = [40, 110, 80][col]
        op = [0.8, 1.0, 0.6][col]
        hx, hy = head
        tx, ty = hx - tail_len * 0.7, hy + tail_len * 0.7
        g.append(f'<path d="M{f(tx)},{f(ty)} L{f(hx)},{f(hy)}" stroke="{GLOW}" stroke-width="12" '
                 f'stroke-linecap="round" opacity="{f(0.35 * op)}"/>')
        g.append(f'<path d="M{f(tx)},{f(ty)} L{f(hx)},{f(hy)}" stroke="{GOLD_HOT}" stroke-width="4" '
                 f'stroke-linecap="round" opacity="{f(op)}"/>')
        g.append(f'<circle cx="{f(hx)}" cy="{f(hy)}" r="30" fill="url(#glow)" opacity="{f(op)}"/>')
        g.append(star_fx(hx, hy, [16, 24, 18][col], color="#ffffff", opacity=op))
        for i in range(4 + col * 2):
            a = i * 1.7
            g.append(star_fx(tx + (hx - tx) * (i / 8) + 14 * math.sin(a), ty + (hy - ty) * (i / 8) + 10 * math.cos(a),
                             3, opacity=op * 0.7))
    else:  # Freedom Line: a train of lantern light along a rail
        count = [3, 5, 6][col]
        g.append(f'<path d="M{col * 192 + 8},{cy + 34} L{col * 192 + 184},{cy + 34} M{col * 192 + 8},{cy + 44} '
                 f'L{col * 192 + 184},{cy + 44}" stroke="{GOLD}" stroke-width="2.4" opacity="0.55"/>')
        for i in range(10):
            x = col * 192 + 14 + i * 18
            g.append(f'<rect x="{x}" y="{cy + 31}" width="4" height="16" fill="{LANTERN_FRAME}" opacity="0.55"/>')
        for i in range(count):
            x = col * 192 + 26 + i * 28
            y = cy + 18 - 6 * math.sin(i * 0.9 + col)
            op = 0.55 + 0.45 * (i + 1) / count
            g.append(f'<circle cx="{f(x)}" cy="{f(y)}" r="20" fill="url(#glow)" opacity="{f(op)}"/>')
            g.append(f'<rect x="{f(x - 5)}" y="{f(y - 7)}" width="10" height="14" rx="2" fill="url(#glass)" '
                     f'stroke="{LANTERN_FRAME}" stroke-width="1.6" opacity="{f(op)}"/>')
        g.append(f'<path d="M{col * 192 + 12},{cy + 10} Q{cx},{cy - 10} {col * 192 + 20 + count * 28},{cy + 8}" '
                 f'fill="none" stroke="{GOLD_HOT}" stroke-width="2" opacity="0.5"/>')
    return "".join(g)


def vfx_cells():
    """Yields (file stem, svg) per VFX cell: tubman_<ability>_<frame>."""
    for row, anim in enumerate(VFX_ROWS):
        for col in range(3):
            yield f"{anim}_{col}", cell_svg(vfx_cell(row, col), col * 192, row * 192, 192, 192)


# --- portrait (512x512) -----------------------------------------------------

def build_portrait():
    return f"""<svg xmlns="http://www.w3.org/2000/svg" width="512" height="512" viewBox="0 0 512 512">
{DEFS}
<defs>
<linearGradient id="face" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#4a2c1b"/><stop offset="1" stop-color="#6e452b"/></linearGradient>
<linearGradient id="wrapg" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#ad5632"/><stop offset="1" stop-color="{WRAP_D}"/></linearGradient>
<linearGradient id="dressg" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#2b3d6b"/><stop offset="1" stop-color="{DRESS_D}"/></linearGradient>
</defs>
<!-- shoulders: dark-blue dress, brown jacket, shawl -->
<path d="M40,512 Q52,380 150,352 L206,334 L306,334 L362,352 Q460,380 472,512 Z" fill="url(#dressg)" stroke="{OUT}" stroke-width="6" stroke-linejoin="round"/>
<path d="M70,512 Q86,410 170,372 L256,420 L342,372 Q426,410 442,512 Z" fill="{JACKET}" stroke="{OUT}" stroke-width="5" stroke-linejoin="round"/>
<path d="M120,380 Q256,330 392,380 L330,470 L256,420 L182,470 Z" fill="{SHAWL}" stroke="{OUT}" stroke-width="5" stroke-linejoin="round"/>
<path d="M182,470 L256,420 L330,470" fill="none" stroke="#5a3e27" stroke-width="4"/>
<!-- neck -->
<path d="M214,300 L214,356 Q256,380 298,356 L298,300 Z" fill="#4e2f1d" stroke="{OUT}" stroke-width="5"/>
<!-- face -->
<ellipse cx="258" cy="236" rx="78" ry="96" fill="url(#face)" stroke="{OUT}" stroke-width="6"/>
<ellipse cx="182" cy="248" rx="13" ry="22" fill="#4a2c1b" stroke="{OUT}" stroke-width="5"/>
<path d="M222,222 Q238,214 252,222" fill="none" stroke="{OUT}" stroke-width="6" stroke-linecap="round"/>
<path d="M280,222 Q296,212 312,220" fill="none" stroke="{OUT}" stroke-width="6" stroke-linecap="round"/>
<path d="M226,240 Q238,232 250,240 Q238,246 226,240 Z" fill="#1a0f0a"/>
<path d="M282,240 Q294,232 306,240 Q294,246 282,240 Z" fill="#1a0f0a"/>
<circle cx="241" cy="239" r="2.5" fill="#f3e2c8"/><circle cx="297" cy="239" r="2.5" fill="#f3e2c8"/>
<path d="M270,244 Q262,276 266,286 Q276,294 290,286" fill="none" stroke="#3a2214" stroke-width="5" stroke-linecap="round"/>
<path d="M240,306 Q268,318 298,304" fill="none" stroke="#2a170e" stroke-width="6" stroke-linecap="round"/>
<path d="M248,314 Q268,322 290,312" fill="none" stroke="#6e3a28" stroke-width="3" stroke-linecap="round"/>
<path d="M300,190 Q326,230 322,280" fill="none" stroke="#8a5a3a" stroke-width="7" stroke-linecap="round" opacity="0.55"/>
<!-- head wrap -->
<path d="M176,214 Q168,110 258,98 Q350,94 344,200 Q330,168 300,160 Q250,150 214,170 Q188,184 176,214 Z" fill="url(#wrapg)" stroke="{OUT}" stroke-width="6" stroke-linejoin="round"/>
<path d="M196,172 Q250,128 330,150" fill="none" stroke="{WRAP_D}" stroke-width="6" stroke-linecap="round"/>
<path d="M208,132 Q262,112 322,124" fill="none" stroke="#c46a3e" stroke-width="5" stroke-linecap="round"/>
<path d="M176,150 Q138,126 150,94 Q176,112 196,120 Z" fill="url(#wrapg)" stroke="{OUT}" stroke-width="5" stroke-linejoin="round"/>
<ellipse cx="176" cy="130" rx="20" ry="16" fill="#a14f2e" stroke="{OUT}" stroke-width="5"/>
<!-- lantern, lower right, lighting the face -->
<circle cx="420" cy="426" r="84" fill="url(#glow)"/>
<path d="M420,356 L420,378" stroke="{OUT}" stroke-width="5"/>
<path d="M400,392 L440,392 L432,378 L408,378 Z" fill="{LANTERN_FRAME}" stroke="{OUT}" stroke-width="4" stroke-linejoin="round"/>
<rect x="394" y="392" width="52" height="66" rx="8" fill="url(#glass)" stroke="{LANTERN_FRAME}" stroke-width="7"/>
<path d="M420,392 L420,458" stroke="{LANTERN_FRAME}" stroke-width="4"/>
<rect x="390" y="456" width="60" height="12" rx="3" fill="{LANTERN_FRAME}" stroke="{OUT}" stroke-width="3"/>
<path d="M346,512 Q352,470 392,452 L406,470 Q384,486 382,512 Z" fill="{SKIN}" stroke="{OUT}" stroke-width="5" stroke-linejoin="round"/>
</svg>
"""


def main():
    cells_dir = os.path.join(GEN, "cells")
    os.makedirs(cells_dir, exist_ok=True)
    for name in os.listdir(cells_dir):
        if name.endswith(".svg"):
            os.remove(os.path.join(cells_dir, name))
    count = 0
    for stem, svg in list(character_cells()) + list(vfx_cells()):
        with open(os.path.join(cells_dir, stem + ".svg"), "w", encoding="utf-8", newline="\n") as fh:
            fh.write(svg)
        count += 1
    with open(os.path.join(GEN, "tubman_portrait.svg"), "w", encoding="utf-8", newline="\n") as fh:
        fh.write(build_portrait())
    print(f"wrote {count} cell SVGs + the portrait to {GEN}")


if __name__ == "__main__":
    main()
