"""Einstein sprite set builder (Package 10 pass 1).

A parametric paper-doll rig emits one 96x128 SVG per animation frame for every
animation in resources/SpriteFrames/einstein_frames.tres, plus the rebuilt
.tres itself and the 512x512 character portrait SVG.

Contract (CharacterFactory.BuildVisual): 96x128 frame, feet on the bottom
canvas edge, facing right, drawn area roughly filling the 80x128 that maps to
the 40x64 world body box at 0.5 scale.

Style: engraved-pulp vector - dark sepia outlines, flat fills with one shadow
tone, wild white hair, brown tweed, cream vest; ability energy is cyan-white
(Apex violet is reserved for enemies).

Run:  python build_einstein.py
Then: godot --headless --script res://tools/generate_assets.gd -- group=einstein
"""
from __future__ import annotations

import math
import os

HERE = os.path.dirname(os.path.abspath(__file__))
GEN = os.path.join(HERE, "generated")
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))

W, H = 96, 128
CX = 46.0

# --- palette ---------------------------------------------------------------
OUT = "#241708"          # engraved outline
HAIR = "#ECF1F8"
HAIR_SH = "#B8C4D8"
SKIN = "#E8C49A"
SKIN_SH = "#C09A6E"
JACKET = "#6E4E30"
JACKET_D = "#4A3018"
VEST = "#D8C9A0"
SHIRT = "#F2EDE2"
TIE = "#7A2E22"
TROUSER = "#463726"
SHOE = "#332414"
CYAN = "#7FE4FF"
CYAN_D = "#2FA8CC"
GOLD = "#D8B96B"

# --- geometry --------------------------------------------------------------
TORSO = 36.0
ARM_U, ARM_F = 16.0, 14.0
THIGH, SHIN = 22.0, 21.0


def polar(l: float, a: float):
    """Angle in degrees: 0 points down, positive swings toward +x (facing)."""
    r = math.radians(a)
    return l * math.sin(r), l * math.cos(r)


def up(l: float, a: float):
    r = math.radians(a)
    return l * math.sin(r), -l * math.cos(r)


def add(p, d):
    return p[0] + d[0], p[1] + d[1]


def seg(p0, p1, w, fill, outline=OUT, ow=2.4, opacity=1.0):
    """Outlined capsule: a fat dark stroke under a slimmer fill stroke."""
    d = f'M{p0[0]:.1f} {p0[1]:.1f} L{p1[0]:.1f} {p1[1]:.1f}'
    return (f'<path d="{d}" stroke="{outline}" stroke-width="{w + ow}" stroke-linecap="round" stroke-opacity="{opacity}"/>'
            f'<path d="{d}" stroke="{fill}" stroke-width="{w}" stroke-linecap="round" stroke-opacity="{opacity}"/>')


def chain(p0, l1, a1, l2, a2):
    p1 = add(p0, polar(l1, a1))
    p2 = add(p1, polar(l2, a2))
    return p1, p2


def circle(c, r, fill, outline=OUT, ow=2.0, opacity=1.0):
    return (f'<circle cx="{c[0]:.1f}" cy="{c[1]:.1f}" r="{r}" fill="{fill}" '
            f'stroke="{outline}" stroke-width="{ow}" fill-opacity="{opacity}" stroke-opacity="{opacity}"/>')


def blob(c, r, fill, opacity=1.0):
    return f'<circle cx="{c[0]:.1f}" cy="{c[1]:.1f}" r="{r}" fill="{fill}" fill-opacity="{opacity}"/>'


# --- pose ------------------------------------------------------------------

def P(**kw):
    pose = dict(
        hips=(CX, 79.0), lean=7.0,
        head_dx=0.0, head_dy=0.0, head_tilt=0.0,
        arm_b=(14.0, 22.0), arm_f=(-10.0, -16.0),
        leg_b=(-14.0, -5.0), leg_f=(12.0, 3.0),
        foot_b=0.0, foot_f=0.0,
        coat=8.0,             # coattail sweep-back, degrees
        alpha=1.0,
        body_rot=0.0,         # whole-figure rotation around the hips
        ball=False, ball_rot=0.0,
        fx=[],                # [(fx_id, {params})]
    )
    pose.update(kw)
    return pose


def render(pose) -> str:
    body: list[str] = []
    fx_back: list[str] = []
    fx_front: list[str] = []
    hips = pose["hips"]
    anchors = {"hips": hips}

    if pose["ball"]:
        c = (48.0, 106.0)
        anchors.update(hand_f=c, hand_b=c, chest=c, head=c)
        g = [circle(c, 17.5, JACKET)]
        rr = math.radians(pose["ball_rot"])

        def orbit(a_deg, dist):
            a = math.radians(a_deg) + rr
            return (c[0] + dist * math.cos(a), c[1] + dist * math.sin(a))
        g.append(blob(orbit(-105, 9.5), 8.0, HAIR, 0.97))
        g.append(blob(orbit(-70, 11.5), 5.5, HAIR, 0.95))
        g.append(seg(orbit(30, 10), orbit(75, 13), 6.0, TROUSER, ow=2.0))
        g.append(seg(orbit(75, 13), orbit(108, 14), 4.5, SHOE, ow=1.6))
        g.append(seg(orbit(150, 9), orbit(185, 12), 5.0, JACKET_D, ow=1.8))
        g.append(f'<path d="M{c[0] - 24} {c[1] + 20} Q{c[0]} {c[1] + 27} {c[0] + 24} {c[1] + 20}" '
                 f'fill="none" stroke="{CYAN}" stroke-opacity="0.4" stroke-width="2"/>')
        body = g
    else:
        lean = pose["lean"]
        shoulder = add(hips, up(TORSO, lean))
        neck_top = add(shoulder, up(6.5, lean + pose["head_tilt"]))
        head_c = add(add(neck_top, up(7.5, lean + pose["head_tilt"])),
                     (pose["head_dx"], pose["head_dy"]))

        sh_b = add(shoulder, (-3.0, 2.0))
        sh_f = add(shoulder, (2.5, 2.0))
        elb_b, hand_b = chain(sh_b, ARM_U, pose["arm_b"][0], ARM_F, pose["arm_b"][1])
        elb_f, hand_f = chain(sh_f, ARM_U, pose["arm_f"][0], ARM_F, pose["arm_f"][1])
        hip_b = add(hips, (-2.5, 1.0))
        hip_f = add(hips, (2.5, 1.0))
        knee_b, ankle_b = chain(hip_b, THIGH, pose["leg_b"][0], SHIN, pose["leg_b"][1])
        knee_f, ankle_f = chain(hip_f, THIGH, pose["leg_f"][0], SHIN, pose["leg_f"][1])
        chest = add(hips, up(TORSO * 0.62, lean))
        anchors.update(hand_f=hand_f, hand_b=hand_b, chest=chest, head=head_c,
                       ankle_f=ankle_f, ankle_b=ankle_b, shoulder=shoulder)

        def shoe(ankle, ang):
            toe = add(ankle, polar(9.0, 96.0 + ang))
            return seg((ankle[0] - 1.5, ankle[1] + 0.5), toe, 5.5, SHOE, ow=2.0)

        # Back arm, back leg, front leg, torso, head, front arm.
        body.append(seg(sh_b, elb_b, 6.5, JACKET_D, ow=2.2))
        body.append(seg(elb_b, hand_b, 5.5, JACKET_D, ow=2.2))
        body.append(circle(hand_b, 3.0, SKIN_SH, ow=1.6))

        body.append(seg(hip_b, knee_b, 8.0, "#3A2C1E", ow=2.2))
        body.append(seg(knee_b, ankle_b, 6.5, "#3A2C1E", ow=2.2))
        body.append(shoe(ankle_b, pose["foot_b"]))

        body.append(seg(hip_f, knee_f, 8.5, TROUSER, ow=2.4))
        body.append(seg(knee_f, ankle_f, 7.0, TROUSER, ow=2.4))
        body.append(shoe(ankle_f, pose["foot_f"]))

        # Coattails behind the hips.
        tail_a = 180.0 - pose["coat"]
        body.append(seg(add(hips, (-4, 0)), add(hips, polar(11.0, -160.0 - pose["coat"] * 0.4)), 7.0, JACKET_D, ow=2.0))
        # Torso: jacket trunk + cream vest front + collar, lapel, tie.
        body.append(seg(add(hips, (0, 1.5)), shoulder, 20.0, JACKET, ow=2.8))
        # Vest: a narrow cream peek between the lapels, chest-high only.
        vest_lo = add(add(hips, up(TORSO * 0.34, lean)), (5.2, 0.0))
        body.append(seg(vest_lo, add(chest, (5.0, -1.0)), 6.5, VEST, ow=1.6))
        collar = add(shoulder, (5.0, 1.5))
        body.append(blob(collar, 3.2, SHIRT))
        tie_end = add(collar, polar(8.5, 12.0))
        body.append(seg(collar, tie_end, 3.0, TIE, ow=1.4))
        lapel_end = add(chest, (2.0, 2.0))
        body.append(f'<path d="M{shoulder[0] + 1:.1f} {shoulder[1] + 1:.1f} L{lapel_end[0]:.1f} {lapel_end[1]:.1f}" '
                    f'stroke="{JACKET_D}" stroke-width="2.2" stroke-linecap="round"/>')

        # Neck + head.
        body.append(seg(shoulder, neck_top, 6.0, SKIN, ow=2.0))
        body.append(circle(head_c, 9.0, SKIN))
        # Wild hair: a cloud of blobs hugging the skull's top and back, kept off
        # the face front so the eye and moustache stay readable.
        for dx, dy, r in ((-4.5, -8.0, 7.5), (-10.0, -3.5, 5.8), (2.0, -10.0, 6.2),
                          (6.6, -7.6, 4.6), (-12.5, 1.5, 4.2)):
            body.append(blob(add(head_c, (dx, dy)), r, HAIR, 0.97))
        body.append(f'<path d="M{head_c[0] - 9:.1f} {head_c[1] - 6:.1f} Q{head_c[0] - 4:.1f} {head_c[1] - 11:.1f} '
                    f'{head_c[0] + 2:.1f} {head_c[1] - 9:.1f}" fill="none" stroke="{HAIR_SH}" stroke-width="1.4" stroke-opacity="0.8"/>')
        # Face: eye, brow, nose bump, moustache - oversized slightly so they
        # survive the 0.5x draw scale.
        eye = add(head_c, (4.9, -2.4))
        body.append(f'<circle cx="{eye[0]:.1f}" cy="{eye[1]:.1f}" r="1.55" fill="{OUT}"/>')
        body.append(f'<path d="M{eye[0] - 2.6:.1f} {eye[1] - 2.8:.1f} L{eye[0] + 2.0:.1f} {eye[1] - 3.4:.1f}" '
                    f'stroke="{HAIR_SH}" stroke-width="1.8" stroke-linecap="round"/>')
        nose = add(head_c, (8.7, 0.2))
        body.append(f'<path d="M{nose[0] - 0.6:.1f} {nose[1] - 1.8:.1f} Q{nose[0] + 1.4:.1f} {nose[1]:.1f} '
                    f'{nose[0] - 0.8:.1f} {nose[1] + 1.6:.1f}" fill="none" stroke="{SKIN_SH}" '
                    f'stroke-width="1.6" stroke-linecap="round"/>')
        mo = add(head_c, (5.6, 2.6))
        body.append(f'<path d="M{mo[0] - 5.6:.1f} {mo[1] + 0.6:.1f} Q{mo[0]:.1f} {mo[1] - 2.8:.1f} {mo[0] + 4.6:.1f} {mo[1] - 0.2:.1f} '
                    f'Q{mo[0] + 3.4:.1f} {mo[1] + 2.8:.1f} {mo[0]:.1f} {mo[1] + 2.0:.1f} '
                    f'Q{mo[0] - 3.4:.1f} {mo[1] + 3.0:.1f} {mo[0] - 5.6:.1f} {mo[1] + 0.6:.1f} Z" '
                    f'fill="{HAIR}" stroke="#8A94A8" stroke-width="1.2"/>')

        # Front arm above everything.
        body.append(seg(sh_f, elb_f, 7.0, JACKET, ow=2.6))
        body.append(seg(elb_f, hand_f, 6.0, JACKET, ow=2.6))
        body.append(circle(hand_f, 3.6, SKIN, ow=1.8))

    for fx_id, params in pose["fx"]:
        target = fx_front if params.get("front", True) else fx_back
        target.append(FX[fx_id](anchors, params))

    group = "".join(body)
    if pose["body_rot"]:
        group = (f'<g transform="rotate({pose["body_rot"]:.1f} {hips[0]:.1f} {hips[1]:.1f})">' + group + "</g>")
    if pose["alpha"] < 1.0:
        group = f'<g opacity="{pose["alpha"]:.2f}">' + group + "</g>"
    return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}">'
            + "".join(fx_back) + group + "".join(fx_front) + "</svg>")


# --- FX --------------------------------------------------------------------

def fx_slash(a, p):
    h = a[p.get("at", "hand_f")]
    r = p.get("r", 13.0)
    tilt = p.get("tilt", -20.0)
    t = math.radians(tilt)
    x0, y0 = h[0] + r * math.cos(t + 1.9), h[1] + r * math.sin(t + 1.9)
    x1, y1 = h[0] + r * math.cos(t - 1.9), h[1] + r * math.sin(t - 1.9)
    cx_, cy_ = h[0] + r * 1.5 * math.cos(t), h[1] + r * 1.5 * math.sin(t)
    return (f'<path d="M{x0:.1f} {y0:.1f} Q{cx_:.1f} {cy_:.1f} {x1:.1f} {y1:.1f}" fill="none" '
            f'stroke="{CYAN}" stroke-width="4" stroke-linecap="round" stroke-opacity="0.9"/>'
            f'<path d="M{x0:.1f} {y0:.1f} Q{cx_:.1f} {cy_:.1f} {x1:.1f} {y1:.1f}" fill="none" '
            f'stroke="#ffffff" stroke-width="1.6" stroke-linecap="round" stroke-opacity="0.9"/>')


def fx_watch(a, p):
    h = a[p.get("from", "hand_f")]
    d = p.get("dist", 0.0)
    w = (h[0] + d, h[1] - p.get("rise", 4.0)) if d else h
    parts = []
    if d:
        parts.append(f'<path d="M{h[0]:.1f} {h[1]:.1f} Q{(h[0] + w[0]) / 2:.1f} {h[1] + 4:.1f} {w[0]:.1f} {w[1]:.1f}" '
                     f'fill="none" stroke="{GOLD}" stroke-width="1.3" stroke-opacity="0.9" stroke-dasharray="3 2"/>')
    parts.append(f'<circle cx="{w[0]:.1f}" cy="{w[1]:.1f}" r="6.5" fill="{CYAN}" fill-opacity="0.25"/>')
    parts.append(circle(w, 4.4, GOLD, "#8A6A20", 1.6))
    parts.append(f'<circle cx="{w[0]:.1f}" cy="{w[1]:.1f}" r="2.2" fill="#F4EBD0"/>')
    parts.append(f'<path d="M{w[0]:.1f} {w[1]:.1f} L{w[0]:.1f} {w[1] - 1.8:.1f} M{w[0]:.1f} {w[1]:.1f} L{w[0] + 1.4:.1f} {w[1]:.1f}" '
                 f'stroke="#8A6A20" stroke-width="0.9"/>')
    return "".join(parts)


def fx_burst(a, p):
    c = p.get("c") or add(a[p.get("at", "hand_f")], tuple(p.get("off", (6, 0))))
    r = p.get("r", 10.0)
    parts = [f'<circle cx="{c[0]:.1f}" cy="{c[1]:.1f}" r="{r * 0.55:.1f}" fill="#ffffff" fill-opacity="0.85"/>',
             f'<circle cx="{c[0]:.1f}" cy="{c[1]:.1f}" r="{r * 0.95:.1f}" fill="{CYAN}" fill-opacity="0.3"/>']
    for i in range(8):
        ang = i * math.pi / 4 + 0.35
        x0, y0 = c[0] + r * 0.5 * math.cos(ang), c[1] + r * 0.5 * math.sin(ang)
        x1, y1 = c[0] + r * (1.5 if i % 2 == 0 else 1.1) * math.cos(ang), c[1] + r * (1.5 if i % 2 == 0 else 1.1) * math.sin(ang)
        parts.append(f'<path d="M{x0:.1f} {y0:.1f} L{x1:.1f} {y1:.1f}" stroke="#EAFBFF" stroke-width="1.6" stroke-opacity="0.9"/>')
    return "".join(parts)


def fx_ring(a, p):
    c = add(a[p.get("at", "hand_f")], tuple(p.get("off", (8, 0))))
    r = p.get("r", 14.0)
    return (f'<ellipse cx="{c[0]:.1f}" cy="{c[1]:.1f}" rx="{r * 0.62:.1f}" ry="{r:.1f}" fill="{CYAN}" fill-opacity="0.10" '
            f'stroke="{CYAN}" stroke-width="2.2" stroke-opacity="0.85"/>'
            f'<ellipse cx="{c[0]:.1f}" cy="{c[1]:.1f}" rx="{r * 0.38:.1f}" ry="{r * 0.62:.1f}" fill="none" '
            f'stroke="#ffffff" stroke-width="1" stroke-opacity="0.5"/>')


def fx_speed(a, p):
    hips = a["hips"]
    parts = []
    for i, (dy, ln) in enumerate(((-34, 26), (-16, 34), (4, 24))):
        y = hips[1] + dy
        x1 = hips[0] - 12 - (i * 3)
        parts.append(f'<path d="M{x1 - ln} {y} L{x1} {y}" stroke="{CYAN}" stroke-width="2.2" '
                     f'stroke-linecap="round" stroke-opacity="{0.5 - i * 0.1}"/>')
    return "".join(parts)


def fx_warpbits(a, p):
    c = a["chest"]
    parts = []
    for dx, dy, r in ((-16, -22, 3.4), (14, -30, 2.6), (-20, 6, 2.8), (18, -4, 3.2), (-6, -42, 2.4), (8, 16, 2.2)):
        x, y = c[0] + dx, c[1] + dy
        pts = " ".join(f"{x + r * math.cos(math.pi / 3 * k):.1f},{y + r * math.sin(math.pi / 3 * k):.1f}" for k in range(6))
        parts.append(f'<polygon points="{pts}" fill="none" stroke="{CYAN}" stroke-width="1.3" stroke-opacity="{p.get("o", 0.8)}"/>')
    return "".join(parts)


def fx_beam(a, p):
    c = a["hips"]
    return (f'<path d="M{c[0] - 13} 6 L{c[0] + 13} 6 L{c[0] + 9} 124 L{c[0] - 9} 124 Z" '
            f'fill="{CYAN}" fill-opacity="{p.get("o", 0.14)}"/>')


def fx_stars(a, p):
    hd = a["head"]
    parts = []
    for i, (dx, dy) in enumerate(((-13, -14), (0, -19), (12, -13))):
        x, y = hd[0] + dx, hd[1] + dy
        parts.append(f'<path d="M{x} {y - 3} L{x + 1.1} {y - 1.1} L{x + 3} {y} L{x + 1.1} {y + 1.1} L{x} {y + 3} '
                     f'L{x - 1.1} {y + 1.1} L{x - 3} {y} L{x - 1.1} {y - 1.1} Z" fill="#FFE9A0" stroke="#B8923E" stroke-width="0.8"/>')
    return "".join(parts)


def fx_impact(a, p):
    c = add(a["chest"], (6, -2))
    parts = []
    for i in range(6):
        ang = i * math.pi / 3 + 0.5
        parts.append(f'<path d="M{c[0] + 3 * math.cos(ang):.1f} {c[1] + 3 * math.sin(ang):.1f} '
                     f'L{c[0] + 9 * math.cos(ang):.1f} {c[1] + 9 * math.sin(ang):.1f}" '
                     f'stroke="#FFD86B" stroke-width="1.8" stroke-opacity="0.9"/>')
    return "".join(parts)


def fx_dust(a, p):
    f = a.get("ankle_b", (30, 122))
    return (blob((f[0] - 8, f[1] - 2), 4.5, "#9a9282", 0.5) + blob((f[0] - 14, f[1] - 5), 3.2, "#9a9282", 0.35)
            + blob((f[0] - 4, f[1] - 7), 2.6, "#9a9282", 0.3))


def fx_shield(a, p):
    c = a["chest"]
    x = c[0] + 15
    return (f'<path d="M{x} {c[1] - 22} L{x + 6} {c[1] - 16} L{x + 6} {c[1] + 16} L{x} {c[1] + 22} '
            f'L{x - 3} {c[1] + 16} L{x - 3} {c[1] - 16} Z" fill="{CYAN}" fill-opacity="0.13" '
            f'stroke="{CYAN}" stroke-width="1.6" stroke-opacity="0.55"/>')


def fx_sparkle(a, p):
    h = a[p.get("at", "hand_f")]
    parts = []
    for dx, dy, r in ((6, -8, 3.2), (-4, -13, 2.2), (11, -1, 1.8)):
        x, y = h[0] + dx, h[1] + dy
        parts.append(f'<path d="M{x - r} {y} L{x + r} {y} M{x} {y - r} L{x} {y + r}" '
                     f'stroke="#EAFBFF" stroke-width="1.2" stroke-opacity="0.9"/>')
    return "".join(parts)


def fx_aura(a, p):
    c = a["chest"]
    r = p.get("r", 26.0)
    return (f'<ellipse cx="{c[0]:.1f}" cy="{c[1]:.1f}" rx="{r:.1f}" ry="{r * 1.35:.1f}" fill="{CYAN}" fill-opacity="0.10"/>'
            f'<ellipse cx="{c[0]:.1f}" cy="{c[1]:.1f}" rx="{r * 0.7:.1f}" ry="{r:.1f}" fill="#ffffff" fill-opacity="0.06"/>')


def fx_clockring(a, p):
    c = a["chest"]
    r = p.get("r", 28.0)
    parts = [f'<circle cx="{c[0]:.1f}" cy="{c[1]:.1f}" r="{r:.1f}" fill="none" stroke="{CYAN}" stroke-width="1.8" stroke-opacity="0.7"/>']
    for i in range(12):
        ang = i * math.pi / 6
        parts.append(f'<path d="M{c[0] + (r - 3) * math.cos(ang):.1f} {c[1] + (r - 3) * math.sin(ang):.1f} '
                     f'L{c[0] + r * math.cos(ang):.1f} {c[1] + r * math.sin(ang):.1f}" '
                     f'stroke="{CYAN}" stroke-width="1.6" stroke-opacity="0.8"/>')
    return "".join(parts)


def fx_rise(a, p):
    hips = a["hips"]
    parts = []
    for dx in (-14, 0, 13):
        parts.append(f'<path d="M{hips[0] + dx} {hips[1] + 30} L{hips[0] + dx} {hips[1] + 14}" '
                     f'stroke="{CYAN}" stroke-width="1.8" stroke-opacity="0.4" stroke-linecap="round"/>')
    return "".join(parts)


FX = {"slash": fx_slash, "watch": fx_watch, "burst": fx_burst, "ring": fx_ring,
      "speed": fx_speed, "warpbits": fx_warpbits, "beam": fx_beam, "stars": fx_stars,
      "impact": fx_impact, "dust": fx_dust, "shield": fx_shield, "sparkle": fx_sparkle,
      "aura": fx_aura, "clockring": fx_clockring, "rise": fx_rise}


# --- animation poses -------------------------------------------------------

def run_frame(front_leg, front_arm, i):
    """Half the run cycle; the other half swaps limb slots."""
    frames = [
        P(lean=13, leg_f=(30, 16), leg_b=(-32, -10), foot_f=6, foot_b=-24,
          arm_f=(-26, -46), arm_b=(24, 52)),
        P(lean=14, leg_f=(18, 6), leg_b=(-22, -38), foot_b=-30,
          arm_f=(-12, -28), arm_b=(10, 34)),
        P(lean=12, leg_f=(-16, -32), leg_b=(28, 58), foot_b=10, foot_f=-26,
          arm_f=(14, 42), arm_b=(-18, -36), hips=(CX, 77.0)),
    ]
    pose = frames[i]
    if not front_leg:
        pose["leg_f"], pose["leg_b"] = pose["leg_b"], pose["leg_f"]
        pose["foot_f"], pose["foot_b"] = pose["foot_b"], pose["foot_f"]
    if not front_arm:
        pose["arm_f"], pose["arm_b"] = pose["arm_b"], pose["arm_f"]
    return pose


CROUCH = dict(hips=(CX, 92.0), leg_f=(45, -30), leg_b=(-40, 25), lean=18,
              head_dy=1.0, arm_f=(28, 12), arm_b=(-14, -4), coat=16)

ANIMS: dict[str, dict] = {
    "idle": dict(loop=True, speed=6.0, frames=[
        P(lean=6.5), P(lean=7.6, head_dy=0.5, arm_f=(-12, -18), coat=10),
        P(lean=6.5), P(lean=5.6, head_dy=-0.4, arm_f=(-8, -13), coat=6),
    ]),
    "run": dict(loop=True, speed=12.0, frames=[
        run_frame(True, True, 0), run_frame(True, True, 1), run_frame(True, True, 2),
        run_frame(False, False, 0), run_frame(False, False, 1), run_frame(False, False, 2),
    ]),
    "dash": dict(loop=False, speed=12.0, frames=[
        P(lean=24, leg_f=(40, 30), leg_b=(-36, -18), foot_f=10, foot_b=-20,
          arm_f=(-34, -58), arm_b=(-24, -44), coat=30, fx=[("speed", {"front": False})]),
        P(lean=22, leg_f=(34, 24), leg_b=(-32, -14), foot_f=8, foot_b=-18,
          arm_f=(-30, -52), arm_b=(-20, -40), coat=26, fx=[("speed", {"front": False})]),
    ]),
    "roll_startup": dict(loop=False, speed=12.0, frames=[
        P(**{**CROUCH, "arm_f": (44, 92), "arm_b": (30, 70), "lean": 26, "head_dy": 2.5}),
    ]),
    "roll": dict(loop=False, speed=12.0, frames=[
        P(ball=True, ball_rot=0.0), P(ball=True, ball_rot=170.0),
    ]),
    "roll_recovery": dict(loop=False, speed=12.0, frames=[
        P(hips=(CX, 86.0), leg_f=(36, -16), leg_b=(-32, 16), lean=13,
          arm_f=(14, 26), arm_b=(-18, -10), coat=12),
    ]),
    "jump": dict(loop=False, speed=8.0, frames=[
        P(hips=(CX, 70.0), lean=-3, leg_f=(96, 30), leg_b=(18, -14), foot_f=-18,
          arm_f=(152, 168), arm_b=(-26, -18), coat=-14),
        P(hips=(CX, 68.0), lean=0, leg_f=(88, 24), leg_b=(26, -6), foot_f=-14,
          arm_f=(140, 156), arm_b=(-20, -8), coat=-10),
    ]),
    "fall": dict(loop=True, speed=8.0, frames=[
        P(hips=(CX, 72.0), lean=-8, leg_f=(20, -6), leg_b=(-26, -20),
          arm_f=(118, 96), arm_b=(38, 20), coat=-24),
        P(hips=(CX, 73.5), lean=-6, leg_f=(26, 0), leg_b=(-20, -26),
          arm_f=(108, 88), arm_b=(46, 28), coat=-28),
    ]),
    "basic_attack_1": dict(loop=False, speed=8.0, frames=[
        P(lean=-4, arm_f=(-48, -136), arm_b=(20, 34), leg_f=(20, 12), leg_b=(-24, -12), coat=4),
        P(lean=17, arm_f=(84, 88), arm_b=(-22, -36), leg_f=(32, 22), leg_b=(-36, -20),
          coat=20, fx=[("slash", {"tilt": -14, "r": 12})]),
        P(lean=13, arm_f=(66, 58), arm_b=(-16, -26), leg_f=(30, 20), leg_b=(-32, -18), coat=12),
    ]),
    "basic_attack_2": dict(loop=False, speed=8.0, frames=[
        P(lean=8, arm_f=(-56, -30), arm_b=(16, 30), leg_f=(22, 14), leg_b=(-26, -14)),
        P(lean=6, arm_f=(118, 132), arm_b=(-18, -30), leg_f=(24, 16), leg_b=(-30, -16),
          coat=10, fx=[("slash", {"tilt": -75, "r": 13})]),
        P(lean=9, arm_f=(96, 104), arm_b=(-14, -22), leg_f=(24, 16), leg_b=(-28, -14), coat=8),
    ]),
    "basic_attack_3": dict(loop=False, speed=7.0, frames=[
        P(lean=-8, arm_f=(-44, -128), arm_b=(-38, -120), leg_f=(18, 10), leg_b=(-22, -10), head_dy=0.5),
        P(lean=19, arm_f=(88, 92), arm_b=(78, 84), leg_f=(34, 24), leg_b=(-38, -22),
          coat=24, fx=[("burst", {"off": (9, -1), "r": 11})]),
        P(lean=14, arm_f=(74, 70), arm_b=(64, 58), leg_f=(32, 22), leg_b=(-34, -20), coat=14),
    ]),
    "special_1": dict(loop=False, speed=8.0, frames=[
        P(lean=-6, arm_f=(-36, -116), arm_b=(22, 36), head_tilt=-4,
          fx=[("watch", {"dist": 0})]),
        P(lean=16, arm_f=(94, 100), arm_b=(-24, -38), leg_f=(30, 20), leg_b=(-34, -18),
          coat=18, fx=[("watch", {"dist": 24, "rise": 8})]),
        P(lean=12, arm_f=(76, 68), arm_b=(-18, -28), leg_f=(28, 18), leg_b=(-30, -16),
          fx=[("watch", {"dist": 36, "rise": 10}), ("burst", {"off": (42, -12), "r": 7})]),
    ]),
    "special_2": dict(loop=False, speed=8.0, frames=[
        P(lean=4, arm_f=(96, 124), arm_b=(-16, -24), fx=[("ring", {"off": (9, -2), "r": 7})]),
        P(lean=12, arm_f=(88, 94), arm_b=(-22, -34), leg_f=(26, 18), leg_b=(-30, -16),
          fx=[("ring", {"off": (12, 0), "r": 15})]),
        P(lean=10, arm_f=(86, 90), arm_b=(-20, -30), leg_f=(26, 18), leg_b=(-28, -14),
          fx=[("ring", {"off": (14, 0), "r": 22}), ("warpbits", {"o": 0.45})]),
    ]),
    "movement_ability": dict(loop=False, speed=10.0, frames=[
        P(lean=16, hips=(CX, 84.0), leg_f=(36, -20), leg_b=(-32, 18), arm_f=(20, 40),
          arm_b=(-16, -8), fx=[("warpbits", {"o": 0.7}), ("beam", {"o": 0.10, "front": False})]),
        P(lean=18, hips=(CX, 82.0), leg_f=(38, -18), leg_b=(-34, 20), arm_f=(26, 48),
          arm_b=(-18, -10), alpha=0.45,
          fx=[("warpbits", {"o": 0.95}), ("beam", {"o": 0.2, "front": False})]),
    ]),
    "ultimate": dict(loop=False, speed=8.0, frames=[
        P(lean=-4, arm_f=(136, 152), arm_b=(128, 146), head_tilt=-6,
          fx=[("aura", {"r": 20, "front": False})]),
        P(lean=-8, arm_f=(158, 172), arm_b=(150, 166), head_tilt=-10, head_dy=-1,
          fx=[("aura", {"r": 27, "front": False}), ("clockring", {"r": 26})]),
        P(lean=-8, arm_f=(162, 176), arm_b=(154, 170), head_tilt=-10, head_dy=-1.5,
          fx=[("aura", {"r": 32, "front": False}), ("clockring", {"r": 33}),
              ("burst", {"c": (52, 24), "r": 9})]),
    ]),
    "block": dict(loop=True, speed=3.0, frames=[
        P(lean=9, arm_f=(58, 152), arm_b=(44, 142), leg_f=(24, 16), leg_b=(-28, -16),
          head_dy=0.5, fx=[("shield", {})]),
        P(lean=10, arm_f=(60, 155), arm_b=(46, 145), leg_f=(24, 16), leg_b=(-28, -16),
          head_dy=0.8, fx=[("shield", {})]),
    ]),
    "dazed": dict(loop=True, speed=4.0, frames=[
        P(lean=-7, head_tilt=-11, arm_f=(16, 30), arm_b=(-8, 10), leg_f=(18, 8),
          leg_b=(-20, -8), fx=[("stars", {})]),
        P(lean=10, head_tilt=12, arm_f=(10, 22), arm_b=(-14, 2), leg_f=(16, 6),
          leg_b=(-18, -6), fx=[("stars", {})]),
    ]),
    "hitstun": dict(loop=False, speed=8.0, frames=[
        P(lean=-19, head_tilt=-9, head_dy=1, arm_f=(62, 84), arm_b=(44, 62),
          leg_f=(30, 22), leg_b=(-10, -22), coat=-18, fx=[("impact", {"front": False})]),
    ]),
    "death": dict(loop=False, speed=6.0, frames=[
        P(lean=-24, head_tilt=-12, arm_f=(96, 120), arm_b=(70, 96), leg_f=(26, 18),
          leg_b=(-16, -24), coat=-16),
        P(hips=(CX, 100.0), lean=-8, head_tilt=-6, leg_f=(52, -58), leg_b=(-46, 48),
          arm_f=(30, 44), arm_b=(-18, -6), coat=6),
        P(hips=(40.0, 114.0), body_rot=-80, lean=2, head_tilt=-8, leg_f=(8, 2),
          leg_b=(-8, -6), arm_f=(38, 26), arm_b=(-28, -18), alpha=0.98),
    ]),
    "skid": dict(loop=False, speed=12.0, frames=[
        P(lean=-15, leg_f=(38, 30), foot_f=-14, leg_b=(-18, -44), arm_f=(-42, -66),
          arm_b=(-30, -50), coat=-22, fx=[("dust", {"front": False})]),
    ]),
    "crouch": dict(loop=True, speed=4.0, frames=[
        P(**CROUCH), P(**{**CROUCH, "lean": 20, "head_dy": 1.6}),
    ]),
    "ledge_hang": dict(loop=True, speed=3.0, frames=[
        P(hips=(CX, 86.0), lean=3, arm_f=(168, 176), arm_b=(158, 170), leg_f=(10, 4),
          leg_b=(-8, -4), coat=-6),
        P(hips=(CX, 87.0), lean=4, arm_f=(166, 174), arm_b=(156, 168), leg_f=(13, 7),
          leg_b=(-5, -1), coat=-8),
    ]),
    "ledge_pull_up": dict(loop=False, speed=8.0, frames=[
        P(hips=(CX, 74.0), lean=14, arm_f=(120, 70), arm_b=(108, 60), leg_f=(52, 20),
          leg_b=(6, -8), head_dy=-1),
        P(hips=(CX, 62.0), lean=16, arm_f=(72, 30), arm_b=(58, 22), leg_f=(66, 6),
          leg_b=(30, 36), head_dy=-1),
    ]),
    "ledge_drop": dict(loop=False, speed=8.0, frames=[
        P(hips=(CX, 82.0), lean=-4, arm_f=(148, 138), arm_b=(136, 126), leg_f=(14, 6),
          leg_b=(-12, -8), coat=-16, fx=[("rise", {"front": False})]),
    ]),
    "respawn": dict(loop=False, speed=8.0, frames=[
        P(alpha=0.35, arm_f=(24, 36), arm_b=(-22, -10),
          fx=[("warpbits", {"o": 0.9}), ("beam", {"o": 0.2, "front": False})]),
        P(alpha=0.8, arm_f=(16, 26), arm_b=(-16, -6),
          fx=[("warpbits", {"o": 0.5}), ("beam", {"o": 0.08, "front": False})]),
    ]),
    "victory": dict(loop=True, speed=6.0, frames=[
        P(lean=-7, arm_f=(162, 176), arm_b=(-24, -102), head_tilt=-6,
          fx=[("watch", {"dist": 0}), ("sparkle", {})]),
        P(lean=-5, arm_f=(158, 172), arm_b=(-24, -102), head_tilt=-4, head_dy=0.6,
          fx=[("watch", {"dist": 0}), ("sparkle", {"at": "hand_b"})]),
    ]),
    "defeat": dict(loop=True, speed=4.0, frames=[
        P(hips=(CX, 104.0), lean=27, head_tilt=17, head_dy=2.5, leg_f=(84, 12),
          leg_b=(78, 6), arm_f=(30, 22), arm_b=(18, 12), coat=10),
        P(hips=(CX, 104.5), lean=29, head_tilt=19, head_dy=3, leg_f=(84, 12),
          leg_b=(78, 6), arm_f=(32, 24), arm_b=(20, 14), coat=10),
    ]),
}


# --- portrait --------------------------------------------------------------

def build_portrait() -> str:
    return f'''<svg xmlns="http://www.w3.org/2000/svg" width="512" height="512" viewBox="0 0 512 512">
<defs>
<linearGradient id="bg" x1="0" y1="0" x2="0" y2="1">
<stop offset="0" stop-color="#0d1330"/><stop offset="1" stop-color="#171f4a"/>
</linearGradient>
<radialGradient id="field" cx="0.5" cy="0.42" r="0.62">
<stop offset="0" stop-color="#233a6e"/><stop offset="0.7" stop-color="#101838"/><stop offset="1" stop-color="#0a0f28"/>
</radialGradient>
<linearGradient id="brass" x1="0" y1="0" x2="0" y2="1">
<stop offset="0" stop-color="#e8d49a"/><stop offset="0.5" stop-color="#a8854a"/><stop offset="1" stop-color="#6e5426"/>
</linearGradient>
<linearGradient id="jacket" x1="0" y1="0" x2="0" y2="1">
<stop offset="0" stop-color="#7a5836"/><stop offset="1" stop-color="#4a3018"/>
</linearGradient>
</defs>
<rect x="8" y="8" width="496" height="496" rx="42" fill="url(#bg)" stroke="#2a355e" stroke-width="6"/>
<circle cx="256" cy="252" r="212" fill="url(#field)"/>
<circle cx="256" cy="252" r="212" fill="none" stroke="url(#brass)" stroke-width="13"/>
<circle cx="256" cy="252" r="199" fill="none" stroke="#3a2c14" stroke-width="3"/>
<circle cx="256" cy="252" r="220" fill="none" stroke="#8a6a34" stroke-width="2.5" stroke-opacity="0.6"/>
<path d="M256 32 L262 44 L256 56 L250 44 Z" fill="#e8d49a"/>
<path d="M256 448 L262 460 L256 472 L250 460 Z" fill="#e8d49a"/>
<!-- shoulders / jacket -->
<path d="M96 452 Q118 336 200 318 L312 318 Q394 336 416 452 Z" fill="url(#jacket)" stroke="#241708" stroke-width="7"/>
<path d="M214 322 L256 452 L298 322 L282 318 L256 372 L230 318 Z" fill="#d8c9a0" stroke="#241708" stroke-width="5"/>
<path d="M245 330 L256 372 L267 330 L256 338 Z" fill="#7a2e22" stroke="#241708" stroke-width="4"/>
<path d="M214 322 L188 360 L206 388" fill="none" stroke="#241708" stroke-width="6"/>
<path d="M298 322 L324 360 L306 388" fill="none" stroke="#241708" stroke-width="6"/>
<!-- neck -->
<path d="M226 282 L232 330 Q256 344 280 330 L286 282 Z" fill="#d4a878" stroke="#241708" stroke-width="6"/>
<!-- head -->
<path d="M182 214 Q180 132 256 126 Q332 132 330 214 Q330 262 306 292 Q282 316 256 316 Q230 316 206 292 Q182 262 182 214 Z" fill="#e8c49a" stroke="#241708" stroke-width="7"/>
<path d="M306 292 Q282 312 256 312 L256 316 Q282 316 306 292 Z" fill="#c09a6e"/>
<!-- age lines -->
<path d="M196 196 Q256 178 316 196" fill="none" stroke="#c09a6e" stroke-width="4" stroke-linecap="round" stroke-opacity="0.7"/>
<path d="M204 176 Q256 160 308 176" fill="none" stroke="#c09a6e" stroke-width="3.4" stroke-linecap="round" stroke-opacity="0.55"/>
<path d="M214 262 Q222 272 218 282 M298 262 Q290 272 294 282" fill="none" stroke="#c09a6e" stroke-width="3.4" stroke-opacity="0.7"/>
<!-- eyes -->
<path d="M202 221 Q220 211 239 220 Q221 230 202 221 Z" fill="#f6f2e8" stroke="#3a2a16" stroke-width="3.4"/>
<path d="M273 220 Q292 211 310 221 Q291 230 273 220 Z" fill="#f6f2e8" stroke="#3a2a16" stroke-width="3.4"/>
<circle cx="221" cy="220" r="6.4" fill="#4a3620"/><circle cx="223.5" cy="217.5" r="2.2" fill="#ffffff"/>
<circle cx="291" cy="220" r="6.4" fill="#4a3620"/><circle cx="293.5" cy="217.5" r="2.2" fill="#ffffff"/>
<path d="M203 214 Q221 206 238 213 M274 213 Q291 206 309 214" fill="none" stroke="#3a2a16" stroke-width="3.4" stroke-linecap="round"/>
<path d="M200 229 Q220 236 241 228 M271 228 Q292 236 312 229" fill="none" stroke="#c09a6e" stroke-width="3.2" stroke-opacity="0.8"/>
<!-- brows -->
<path d="M194 203 Q219 191 245 200" fill="none" stroke="#d8dfe8" stroke-width="9" stroke-linecap="round"/>
<path d="M267 200 Q293 191 318 203" fill="none" stroke="#d8dfe8" stroke-width="9" stroke-linecap="round"/>
<!-- nose -->
<path d="M251 216 Q247 244 243 256" fill="none" stroke="#241708" stroke-width="4.6" stroke-linecap="round"/>
<path d="M243 256 Q250 262 259 260" fill="none" stroke="#241708" stroke-width="4.2" stroke-linecap="round"/>
<path d="M261 258 Q266 259 268 255" fill="none" stroke="#c09a6e" stroke-width="3.6" stroke-linecap="round"/>
<!-- walrus moustache -->
<path d="M202 282 Q228 260 256 266 Q284 260 310 282 Q314 296 300 304 Q286 310 272 302 Q263 297 256 297 Q249 297 240 302 Q226 310 212 304 Q198 296 202 282 Z" fill="#dfe4ec" stroke="#8e9ab0" stroke-width="4"/>
<path d="M212 300 Q206 308 210 314 M300 300 Q306 308 302 314" fill="none" stroke="#dfe4ec" stroke-width="7" stroke-linecap="round"/>
<path d="M228 274 Q238 270 248 272 M264 272 Q276 269 288 274" fill="none" stroke="#b8c2d4" stroke-width="3" stroke-opacity="0.8"/>
<!-- hair -->
<g fill="#ecf1f8" stroke="#241708" stroke-width="6" stroke-linejoin="round">
<path d="M176 216 Q140 208 148 168 Q118 156 150 122 Q140 84 186 88 Q196 52 240 66 Q268 40 296 68 Q342 52 344 96 Q382 100 368 140 Q396 168 362 186 Q372 214 336 216 Q330 160 300 142 Q262 122 216 138 Q186 152 182 190 Z"/>
<path d="M148 236 Q120 238 128 210 Q150 202 166 214 Q160 230 148 236 Z"/>
<path d="M364 236 Q392 238 384 210 Q362 202 346 214 Q352 230 364 236 Z"/>
</g>
<path d="M196 108 Q220 92 248 98 M284 92 Q312 92 330 112 M166 150 Q180 134 202 132" fill="none" stroke="#b8c4d8" stroke-width="5" stroke-linecap="round" stroke-opacity="0.9"/>
</svg>
'''


# --- outputs ---------------------------------------------------------------

def emit_frames() -> list[tuple[str, str]]:
    files = []
    for anim, spec in ANIMS.items():
        for i, pose in enumerate(spec["frames"]):
            files.append((f"einstein_{anim}_{i}.svg", render(pose)))
    return files


def emit_tres() -> str:
    entries = []
    ext = []
    idx = 1
    for anim, spec in ANIMS.items():
        frame_refs = []
        for i in range(len(spec["frames"])):
            ext.append(f'[ext_resource type="Texture2D" '
                       f'path="res://assets/sprites/characters/einstein/einstein_{anim}_{i}.png" id="t{idx}"]')
            frame_refs.append(f'{{"duration": 1.0, "texture": ExtResource("t{idx}")}}')
            idx += 1
        entries.append('{\n"frames": [' + ", ".join(frame_refs) + '],\n'
                       f'"loop": {"true" if spec["loop"] else "false"},\n'
                       f'"name": &"{anim}",\n'
                       f'"speed": {spec["speed"]}\n}}')
    return (f'[gd_resource type="SpriteFrames" load_steps={idx} format=3]\n\n'
            + "\n".join(ext)
            + "\n\n[resource]\nanimations = [" + ", ".join(entries) + "]\n")


def main() -> None:
    os.makedirs(GEN, exist_ok=True)
    files = emit_frames()
    for name, text in files:
        with open(os.path.join(GEN, name), "w", encoding="utf-8", newline="\n") as f:
            f.write(text)
    print(f"wrote {len(files)} frame SVGs")

    tres = os.path.join(REPO, "resources", "SpriteFrames", "einstein_frames.tres")
    with open(tres, "w", encoding="utf-8", newline="\n") as f:
        f.write(emit_tres())
    print("wrote einstein_frames.tres")

    portrait = os.path.join(REPO, "assets", "placeholders", "characters", "einstein_portrait.svg")
    with open(portrait, "w", encoding="utf-8", newline="\n") as f:
        f.write(build_portrait())
    print("wrote einstein_portrait.svg")


if __name__ == "__main__":
    main()
