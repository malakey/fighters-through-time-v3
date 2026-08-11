"""Builds the Temporal Glass menu SVG sources (Package 10 pass 1).

Emits into this directory:
  glass_shards.svg          1920x1080 hero cluster of six era-tinted glass shards
  clock_etch.svg            1920x1080 faint horological ring overlay for the starfield
  glass_button_normal.svg   480x64 chamfered glass button (plus focus/pressed/disabled)
  title_flourish.svg        640x72 silver filigree ornament with a cyan gem
  card_frame.svg            220x260 engraved portrait card frame (plus _p1/_p2 focus)

Everything is plain paths + gradients + opacity: the subset Godot's ThorVG
importer is proven to rasterize (tools/asset_src/preview/thorvg_probe.png).
Scene silhouettes inside each shard are bilinear-mapped into the shard
trapezoid instead of relying on clipPath support.

Run:  python build_menu_svgs.py
"""
from __future__ import annotations

import os
from dataclasses import dataclass

HERE = os.path.dirname(os.path.abspath(__file__))


def lerp(a: float, b: float, t: float) -> float:
    return a + (b - a) * t


# ---------------------------------------------------------------------------
# Shard geometry: a tapered quad, wider at the top, converging near the floor.
# Local scene coordinates (u, v) run u: 0..1 left->right, v: 0..1 bottom->top,
# and are mapped through the quad bilinearly so silhouettes always stay inside.
# ---------------------------------------------------------------------------

@dataclass
class Shard:
    top_x: float
    top_y: float
    top_w: float
    base_x: float
    base_y: float
    base_w: float
    tint_hi: str      # upper glass colour (era hue, bright)
    tint_lo: str      # lower glass colour (dense, near floor)
    rim: str          # rim light colour
    scene: str        # scene id, drawn in local coords

    def corners(self):
        tl = (self.top_x - self.top_w / 2, self.top_y)
        tr = (self.top_x + self.top_w / 2, self.top_y)
        br = (self.base_x + self.base_w / 2, self.base_y)
        bl = (self.base_x - self.base_w / 2, self.base_y)
        return tl, tr, br, bl

    def map(self, u: float, v: float):
        tl, tr, br, bl = self.corners()
        bottom = (lerp(bl[0], br[0], u), lerp(bl[1], br[1], u))
        top = (lerp(tl[0], tr[0], u), lerp(tl[1], tr[1], u))
        return (lerp(bottom[0], top[0], v), lerp(bottom[1], top[1], v))

    def poly(self, points_uv, close=True):
        mapped = [self.map(u, v) for u, v in points_uv]
        d = "M" + " L".join(f"{x:.1f} {y:.1f}" for x, y in mapped)
        return d + (" Z" if close else "")


def polygon_path(points) -> str:
    return "M" + " L".join(f"{x:.1f} {y:.1f}" for x, y in points) + " Z"


# --- per-era inner scenes, drawn in (u, v) local space ----------------------
# Each returns svg fragments given the shard; silhouettes are darker translucent
# shapes with a glowing horizon so the shard reads as a window into an era.

def scene_temple(s: Shard) -> str:
    ground = 0.30
    steps = s.poly([(0.14, ground), (0.86, ground), (0.82, ground + 0.03), (0.18, ground + 0.03)])
    cols = ""
    for i in range(4):
        u = 0.26 + i * 0.16
        cols += f'<path d="{s.poly([(u, ground + 0.03), (u + 0.05, ground + 0.03), (u + 0.05, ground + 0.30), (u, ground + 0.30)])}" fill="#4a3010" fill-opacity="0.62"/>'
    pediment = s.poly([(0.18, ground + 0.33), (0.82, ground + 0.33), (0.50, ground + 0.46)])
    architrave = s.poly([(0.20, ground + 0.30), (0.80, ground + 0.30), (0.80, ground + 0.33), (0.20, ground + 0.33)])
    sun_c = s.map(0.50, 0.80)
    return (
        f'<path d="{steps}" fill="#4a3010" fill-opacity="0.62"/>'
        f'{cols}'
        f'<path d="{architrave}" fill="#553c14" fill-opacity="0.62"/>'
        f'<path d="{pediment}" fill="#553c14" fill-opacity="0.62"/>'
        f'<circle cx="{sun_c[0]:.1f}" cy="{sun_c[1]:.1f}" r="{s.top_w * 0.16:.1f}" fill="url(#sun_disc)"/>'
    )


def scene_pyramid(s: Shard) -> str:
    dune = s.poly([(0.0, 0.24), (0.5, 0.30), (1.0, 0.22), (1.0, 0.16), (0.0, 0.16)])
    pyr = s.poly([(0.22, 0.28), (0.78, 0.28), (0.52, 0.62)])
    edge = s.poly([(0.52, 0.62), (0.60, 0.28), (0.78, 0.28)])
    moon = s.map(0.32, 0.82)
    return (
        f'<path d="{dune}" fill="#5a4018" fill-opacity="0.58"/>'
        f'<path d="{pyr}" fill="#4a3410" fill-opacity="0.66"/>'
        f'<path d="{edge}" fill="#6a5022" fill-opacity="0.66"/>'
        f'<circle cx="{moon[0]:.1f}" cy="{moon[1]:.1f}" r="{s.top_w * 0.10:.1f}" fill="url(#sun_disc)"/>'
    )


def scene_castle(s: Shard) -> str:
    wall = s.poly([(0.16, 0.26), (0.84, 0.26), (0.84, 0.46), (0.16, 0.46)])
    t1 = s.poly([(0.20, 0.26), (0.36, 0.26), (0.36, 0.62), (0.20, 0.62)])
    t1_roof = s.poly([(0.17, 0.62), (0.39, 0.62), (0.28, 0.74)])
    t2 = s.poly([(0.62, 0.26), (0.78, 0.26), (0.78, 0.56), (0.62, 0.56)])
    t2_roof = s.poly([(0.59, 0.56), (0.81, 0.56), (0.70, 0.68)])
    banner = s.poly([(0.28, 0.74), (0.285, 0.86), (0.38, 0.83), (0.29, 0.80)], close=True)
    return (
        f'<path d="{wall}" fill="#16305e" fill-opacity="0.62"/>'
        f'<path d="{t1}" fill="#1c3a6e" fill-opacity="0.66"/>'
        f'<path d="{t1_roof}" fill="#122a52" fill-opacity="0.72"/>'
        f'<path d="{t2}" fill="#1c3a6e" fill-opacity="0.66"/>'
        f'<path d="{t2_roof}" fill="#122a52" fill-opacity="0.72"/>'
        f'<path d="{banner}" fill="#6bb8e8" fill-opacity="0.85"/>'
    )


def scene_ship(s: Shard) -> str:
    waves = s.poly([(0.0, 0.22), (1.0, 0.22), (1.0, 0.14), (0.0, 0.14)])
    hull = s.poly([(0.22, 0.28), (0.78, 0.28), (0.68, 0.20), (0.30, 0.20)])
    mast = s.poly([(0.49, 0.28), (0.52, 0.28), (0.52, 0.68), (0.49, 0.68)])
    sail = s.poly([(0.52, 0.64), (0.80, 0.52), (0.52, 0.36)])
    jib = s.poly([(0.49, 0.60), (0.26, 0.48), (0.49, 0.38)])
    moon = s.map(0.68, 0.84)
    return (
        f'<path d="{waves}" fill="#123058" fill-opacity="0.6"/>'
        f'<path d="{hull}" fill="#152648" fill-opacity="0.68"/>'
        f'<path d="{mast}" fill="#152648" fill-opacity="0.68"/>'
        f'<path d="{sail}" fill="#d8e8f8" fill-opacity="0.30"/>'
        f'<path d="{jib}" fill="#d8e8f8" fill-opacity="0.22"/>'
        f'<circle cx="{moon[0]:.1f}" cy="{moon[1]:.1f}" r="{s.top_w * 0.11:.1f}" fill="url(#moon_disc)"/>'
    )


def scene_fair(s: Shard) -> str:
    """1893 World's Fair: rooflines and a Ferris wheel."""
    blocks = ""
    for u0, u1, v1 in ((0.10, 0.30, 0.42), (0.34, 0.52, 0.50), (0.56, 0.74, 0.40), (0.76, 0.92, 0.46)):
        blocks += f'<path d="{s.poly([(u0, 0.22), (u1, 0.22), (u1, v1), (u0, v1)])}" fill="#2a2050" fill-opacity="0.62"/>'
    hub = s.map(0.48, 0.66)
    r = s.top_w * 0.20
    spokes = ""
    for i in range(8):
        import math
        a = i * math.pi / 4
        spokes += (f'<path d="M{hub[0]:.1f} {hub[1]:.1f} '
                   f'L{hub[0] + r * math.cos(a):.1f} {hub[1] + r * math.sin(a):.1f}" '
                   f'stroke="#7a68c0" stroke-width="2" stroke-opacity="0.8"/>')
    return (
        f'{blocks}'
        f'<circle cx="{hub[0]:.1f}" cy="{hub[1]:.1f}" r="{r:.1f}" fill="none" stroke="#7a68c0" stroke-width="3" stroke-opacity="0.85"/>'
        f'{spokes}'
        f'<circle cx="{hub[0]:.1f}" cy="{hub[1]:.1f}" r="{r * 0.12:.1f}" fill="#7a68c0"/>'
    )


def scene_future(s: Shard) -> str:
    towers = ""
    for u0, u1, v1 in ((0.14, 0.26, 0.60), (0.30, 0.44, 0.76), (0.48, 0.58, 0.55), (0.62, 0.78, 0.68)):
        towers += f'<path d="{s.poly([(u0, 0.18), (u1, 0.18), (u1, v1), ((u0 + u1) / 2, v1 + 0.06), (u0, v1)])}" fill="#241448" fill-opacity="0.68"/>'
        for i in range(3):
            v = 0.26 + i * 0.12
            towers += f'<path d="{s.poly([(u0 + 0.02, v), (u1 - 0.02, v), (u1 - 0.02, v + 0.012), (u0 + 0.02, v + 0.012)])}" fill="#8a5cd8" fill-opacity="0.5"/>'
    m1 = s.map(0.26, 0.88)
    m2 = s.map(0.70, 0.90)
    return (
        f'{towers}'
        f'<circle cx="{m1[0]:.1f}" cy="{m1[1]:.1f}" r="{s.top_w * 0.07:.1f}" fill="url(#moon_disc)"/>'
        f'<circle cx="{m2[0]:.1f}" cy="{m2[1]:.1f}" r="{s.top_w * 0.045:.1f}" fill="url(#moon_disc)"/>'
    )


SCENES = {
    "temple": scene_temple,
    "pyramid": scene_pyramid,
    "castle": scene_castle,
    "ship": scene_ship,
    "fair": scene_fair,
    "future": scene_future,
}


def build_glass_shards() -> str:
    floor_y = 952.0
    # Near-parallel panes with a mild outward lean: heavy taper shears the
    # bilinear-mapped era scenes into unreadable spikes (first-pass lesson).
    shards = [
        Shard(1005, 150, 150, 1075, floor_y, 118, "#e8b36b", "#7a4a1e", "#ffd9a0", "temple"),
        Shard(1150, 108, 158, 1190, floor_y, 122, "#d8c07a", "#8a6428", "#ffe9b8", "pyramid"),
        Shard(1300, 62, 172, 1315, floor_y, 132, "#7ac4e8", "#1e5a8a", "#c9ecff", "castle"),
        Shard(1455, 84, 165, 1440, floor_y, 126, "#9fd8ff", "#2a4a8a", "#e0f4ff", "ship"),
        Shard(1610, 122, 155, 1555, floor_y, 118, "#a98ae8", "#4a2a8a", "#d8c4ff", "fair"),
        Shard(1745, 168, 145, 1660, floor_y, 110, "#8a5cd8", "#38206e", "#c0a0ff", "future"),
    ]
    # Per-era backlit sky inside each pane, so silhouettes read against light.
    skies = ["#f4c87a", "#e8d49a", "#9fd8ff", "#c4e2ff", "#b89aff", "#9a6bff"]

    defs = [
        '<radialGradient id="haze" cx="0.5" cy="0.5" r="0.5">'
        '<stop offset="0" stop-color="#6f9fe8" stop-opacity="0.32"/>'
        '<stop offset="0.55" stop-color="#3d5aa8" stop-opacity="0.14"/>'
        '<stop offset="1" stop-color="#3d5aa8" stop-opacity="0"/></radialGradient>',
        '<radialGradient id="sun_disc" cx="0.5" cy="0.5" r="0.5">'
        '<stop offset="0" stop-color="#ffedc4" stop-opacity="0.95"/>'
        '<stop offset="0.5" stop-color="#f4c87a" stop-opacity="0.55"/>'
        '<stop offset="1" stop-color="#f4c87a" stop-opacity="0"/></radialGradient>',
        '<radialGradient id="moon_disc" cx="0.5" cy="0.5" r="0.5">'
        '<stop offset="0" stop-color="#eef6ff" stop-opacity="0.95"/>'
        '<stop offset="0.55" stop-color="#b8d4f4" stop-opacity="0.5"/>'
        '<stop offset="1" stop-color="#b8d4f4" stop-opacity="0"/></radialGradient>',
        '<radialGradient id="floor_pool" cx="0.5" cy="0.5" r="0.5">'
        '<stop offset="0" stop-color="#9fd8ff" stop-opacity="0.30"/>'
        '<stop offset="0.6" stop-color="#5a8ad8" stop-opacity="0.12"/>'
        '<stop offset="1" stop-color="#5a8ad8" stop-opacity="0"/></radialGradient>',
        '<linearGradient id="ray" x1="0" y1="0" x2="0" y2="1">'
        '<stop offset="0" stop-color="#bfe4ff" stop-opacity="0.18"/>'
        '<stop offset="1" stop-color="#bfe4ff" stop-opacity="0"/></linearGradient>',
    ]
    for i, s in enumerate(shards):
        defs.append(
            f'<linearGradient id="shard{i}" gradientUnits="userSpaceOnUse" '
            f'x1="{s.top_x}" y1="{s.top_y}" x2="{s.base_x}" y2="{s.base_y}">'
            f'<stop offset="0" stop-color="{s.tint_hi}" stop-opacity="0.34"/>'
            f'<stop offset="0.62" stop-color="{s.tint_hi}" stop-opacity="0.16"/>'
            f'<stop offset="1" stop-color="{s.tint_lo}" stop-opacity="0.30"/></linearGradient>')
        defs.append(
            f'<linearGradient id="refl{i}" gradientUnits="userSpaceOnUse" '
            f'x1="{s.base_x}" y1="{floor_y}" x2="{s.base_x}" y2="{floor_y + 120}">'
            f'<stop offset="0" stop-color="{s.tint_hi}" stop-opacity="0.16"/>'
            f'<stop offset="1" stop-color="{s.tint_hi}" stop-opacity="0"/></linearGradient>')
        # Backlit sky: brightest at the scene horizon, fading upward.
        horizon = s.map(0.5, 0.30)
        top_mid = s.map(0.5, 0.95)
        defs.append(
            f'<linearGradient id="sky{i}" gradientUnits="userSpaceOnUse" '
            f'x1="{horizon[0]:.1f}" y1="{horizon[1]:.1f}" x2="{top_mid[0]:.1f}" y2="{top_mid[1]:.1f}">'
            f'<stop offset="0" stop-color="{skies[i]}" stop-opacity="0.40"/>'
            f'<stop offset="0.45" stop-color="{skies[i]}" stop-opacity="0.16"/>'
            f'<stop offset="1" stop-color="{skies[i]}" stop-opacity="0.02"/></linearGradient>')

    body = [f'<ellipse cx="1360" cy="560" rx="640" ry="500" fill="url(#haze)"/>']

    # Light rays falling from above, behind the glass - kept faint so their
    # unblurrable hard edges read as sheen, not columns.
    for x, w, drop in ((1240, 46, 640), (1520, 38, 560)):
        body.append(f'<path d="M{x - w * 0.3} 0 L{x + w * 0.7} 0 L{x + w} {drop} L{x - w} {drop} Z" '
                    f'fill="url(#ray)" opacity="0.5"/>')

    # Outer panes first, centre pane last, so the cluster stacks toward the middle.
    order = sorted(range(len(shards)), key=lambda i: -abs(shards[i].base_x - 1370.0))
    for i in order:
        s = shards[i]
        tl, tr, br, bl = s.corners()
        outline = polygon_path([tl, tr, br, bl])
        body.append(f'<path d="{outline}" fill="url(#shard{i})"/>')
        # Sky band inside the pane (u/v inset so it never bleeds past the glass).
        body.append(f'<path d="{s.poly([(0.045, 0.10), (0.955, 0.10), (0.955, 0.93), (0.045, 0.93)])}" '
                    f'fill="url(#sky{i})"/>')
        body.append(SCENES[s.scene](s))
        # Inner facet: a diagonal micro-crack across the pane.
        body.append(f'<path d="{s.poly([(0.08, 0.72), (0.55, 0.52), (0.92, 0.60)], close=False)}" '
                    f'fill="none" stroke="#ffffff" stroke-opacity="0.10" stroke-width="1.6"/>')
        body.append(f'<path d="{s.poly([(0.22, 0.16), (0.60, 0.30), (0.86, 0.22)], close=False)}" '
                    f'fill="none" stroke="#ffffff" stroke-opacity="0.07" stroke-width="1.2"/>')
        # Rim light: bright core stroke plus two soft halo strokes.
        for width, opacity in ((10.0, 0.05), (5.0, 0.12), (2.2, 0.55)):
            body.append(f'<path d="{outline}" fill="none" stroke="{s.rim}" '
                        f'stroke-opacity="{opacity}" stroke-width="{width}"/>')
        # Floor reflection: a fading smear under the shard base.
        rw = s.base_w * 1.05
        body.append(f'<path d="M{s.base_x - rw / 2:.1f} {floor_y} L{s.base_x + rw / 2:.1f} {floor_y} '
                    f'L{s.base_x + rw * 0.40:.1f} {floor_y + 88} L{s.base_x - rw * 0.40:.1f} {floor_y + 88} Z" '
                    f'fill="url(#refl{i})" opacity="0.75"/>')

    body.append(f'<ellipse cx="1345" cy="{floor_y + 6}" rx="470" ry="46" fill="url(#floor_pool)"/>')
    body.append(_observer(1235.0, floor_y))

    # Long facet lines tying the cluster together.
    body.append('<path d="M980 300 L1400 470 L1760 380" fill="none" stroke="#dff0ff" stroke-opacity="0.08" stroke-width="2"/>')
    body.append('<path d="M1040 640 L1420 760 L1740 660" fill="none" stroke="#dff0ff" stroke-opacity="0.06" stroke-width="2"/>')

    return _svg(1920, 1080, defs, body)


def _observer(cx: float, fy: float) -> str:
    """Back-view figure watching the panes: dark suit, wild white hair, cyan rim."""
    ink = '#0b1022'
    hair = '#ecf1f8'
    parts = [
        f'<ellipse cx="{cx}" cy="{fy + 4}" rx="56" ry="10" fill="#000208" fill-opacity="0.4"/>',
        # Trousers.
        f'<path d="M{cx - 19} {fy - 114} L{cx - 3} {fy - 114} L{cx - 5} {fy} L{cx - 21} {fy} Z" fill="{ink}"/>',
        f'<path d="M{cx + 3} {fy - 114} L{cx + 19} {fy - 114} L{cx + 21} {fy} L{cx + 5} {fy} Z" fill="{ink}"/>',
        # Coat: shoulders to a flared hem, with a neck dip.
        f'<path d="M{cx - 12} {fy - 246} L{cx - 26} {fy - 240} L{cx - 34} {fy - 228} '
        f'L{cx - 37} {fy - 162} L{cx - 45} {fy - 98} L{cx + 45} {fy - 98} '
        f'L{cx + 37} {fy - 162} L{cx + 34} {fy - 228} L{cx + 26} {fy - 240} '
        f'L{cx + 12} {fy - 246} L{cx + 8} {fy - 238} L{cx - 8} {fy - 238} Z" fill="{ink}"/>',
        # Sleeve seams, barely lighter than the coat.
        f'<path d="M{cx - 30} {fy - 224} L{cx - 35} {fy - 128}" stroke="#1c2440" stroke-width="2.4" stroke-opacity="0.9"/>',
        f'<path d="M{cx + 30} {fy - 224} L{cx + 35} {fy - 128}" stroke="#1c2440" stroke-width="2.4" stroke-opacity="0.9"/>',
        # Head and the unmistakable hair.
        f'<circle cx="{cx}" cy="{fy - 254}" r="18" fill="{ink}"/>',
        f'<circle cx="{cx - 14}" cy="{fy - 266}" r="13" fill="{hair}" fill-opacity="0.96"/>',
        f'<circle cx="{cx + 2}" cy="{fy - 273}" r="14" fill="{hair}" fill-opacity="0.96"/>',
        f'<circle cx="{cx + 16}" cy="{fy - 264}" r="12" fill="{hair}" fill-opacity="0.96"/>',
        f'<circle cx="{cx - 21}" cy="{fy - 252}" r="9" fill="{hair}" fill-opacity="0.9"/>',
        f'<circle cx="{cx + 22}" cy="{fy - 251}" r="9" fill="{hair}" fill-opacity="0.9"/>',
        # Cool rim light on the pane side, warm echo on the other.
        f'<path d="M{cx + 34} {fy - 226} L{cx + 37} {fy - 162} L{cx + 45} {fy - 100}" '
        f'fill="none" stroke="#7fe4ff" stroke-opacity="0.55" stroke-width="2.6"/>',
        f'<path d="M{cx + 20} {fy - 112} L{cx + 22} {fy - 4}" fill="none" stroke="#7fe4ff" stroke-opacity="0.4" stroke-width="2"/>',
        f'<path d="M{cx - 34} {fy - 226} L{cx - 37} {fy - 162} L{cx - 45} {fy - 100}" '
        f'fill="none" stroke="#e8b36b" stroke-opacity="0.28" stroke-width="2"/>',
    ]
    return "".join(parts)


def build_clock_etch() -> str:
    """A faint horological ring behind the shard cluster."""
    cx, cy = 1330.0, 560.0
    defs: list[str] = []
    body = []
    for r, opacity, width in ((492, 0.10, 2.5), (470, 0.05, 1.2), (330, 0.06, 1.5)):
        body.append(f'<circle cx="{cx}" cy="{cy}" r="{r}" fill="none" stroke="#9fc4e8" '
                    f'stroke-opacity="{opacity}" stroke-width="{width}"/>')
    import math
    for i in range(60):
        a = i * math.pi / 30
        major = i % 5 == 0
        r0 = 492 - (26 if major else 12)
        x0, y0 = cx + r0 * math.cos(a), cy + r0 * math.sin(a)
        x1, y1 = cx + 492 * math.cos(a), cy + 492 * math.sin(a)
        body.append(f'<path d="M{x0:.1f} {y0:.1f} L{x1:.1f} {y1:.1f}" stroke="#9fc4e8" '
                    f'stroke-opacity="{0.11 if major else 0.055}" stroke-width="{2.2 if major else 1.1}"/>')
    # One elegant hand frozen at an angle, echoing a stopped moment.
    a = -math.pi / 3.4
    body.append(f'<path d="M{cx:.1f} {cy:.1f} L{cx + 300 * math.cos(a):.1f} {cy + 300 * math.sin(a):.1f}" '
                f'stroke="#9fc4e8" stroke-opacity="0.07" stroke-width="4"/>')
    body.append(f'<circle cx="{cx}" cy="{cy}" r="10" fill="#9fc4e8" fill-opacity="0.07"/>')
    return _svg(1920, 1080, defs, body)


# --- glass buttons ----------------------------------------------------------

def build_button(state: str) -> str:
    w, h = 480, 64
    ch = 26          # right-edge chamfer depth
    inset = 2.0
    fill_top = {"normal": ("#16224a", 0.62), "focus": ("#1b3a6e", 0.80),
                "pressed": ("#0d1a3d", 0.88), "disabled": ("#10141f", 0.42)}.get(state, ("#16224a", 0.62))
    fill_bot = {"normal": ("#0a1130", 0.72), "focus": ("#123058", 0.86),
                "pressed": ("#081026", 0.92), "disabled": ("#0a0d16", 0.5)}.get(state, ("#0a1130", 0.72))
    border = {"normal": ("#8fb8d8", 0.42), "focus": ("#35e8ff", 0.95),
              "pressed": ("#22b8cc", 0.8), "disabled": ("#5a6478", 0.35)}.get(state, ("#8fb8d8", 0.42))

    outline = (f"M{inset} {inset} L{w - ch - inset} {inset} "
               f"L{w - inset} {h / 2:.1f} L{w - ch - inset} {h - inset} "
               f"L{inset} {h - inset} Z")
    if state == "focus_ring":
        # Transparent overlay for Button/styles/focus: glow ring + lit gem only,
        # so the pressed/hover panel underneath stays visible.
        defs = [
            '<radialGradient id="gem" cx="0.5" cy="0.5" r="0.5">'
            '<stop offset="0" stop-color="#d8fbff" stop-opacity="1"/>'
            '<stop offset="0.55" stop-color="#35e8ff" stop-opacity="0.9"/>'
            '<stop offset="1" stop-color="#35e8ff" stop-opacity="0"/></radialGradient>',
        ]
        body = []
        for sw, so in ((9.0, 0.10), (5.0, 0.22), (2.4, 0.95)):
            body.append(f'<path d="{outline}" fill="none" stroke="#35e8ff" stroke-opacity="{so}" stroke-width="{sw}"/>')
        body.append(f'<circle cx="30" cy="{h / 2}" r="15" fill="url(#gem)"/>')
        body.append(f'<path d="M30 {h / 2 - 8} L36 {h / 2} L30 {h / 2 + 8} L24 {h / 2} Z" '
                    f'fill="#eaffff" stroke="#35e8ff" stroke-width="1.4"/>')
        return _svg(w, h, defs, body)
    defs = [
        f'<linearGradient id="fill" x1="0" y1="0" x2="0" y2="1">'
        f'<stop offset="0" stop-color="{fill_top[0]}" stop-opacity="{fill_top[1]}"/>'
        f'<stop offset="1" stop-color="{fill_bot[0]}" stop-opacity="{fill_bot[1]}"/></linearGradient>',
        '<linearGradient id="sheen" x1="0" y1="0" x2="0" y2="1">'
        '<stop offset="0" stop-color="#cfe8ff" stop-opacity="0.16"/>'
        '<stop offset="0.5" stop-color="#cfe8ff" stop-opacity="0.02"/>'
        '<stop offset="1" stop-color="#cfe8ff" stop-opacity="0"/></linearGradient>',
        '<radialGradient id="gem" cx="0.5" cy="0.5" r="0.5">'
        '<stop offset="0" stop-color="#d8fbff" stop-opacity="1"/>'
        '<stop offset="0.55" stop-color="#35e8ff" stop-opacity="0.9"/>'
        '<stop offset="1" stop-color="#35e8ff" stop-opacity="0"/></radialGradient>',
    ]
    body = [f'<path d="{outline}" fill="url(#fill)"/>']
    body.append(f'<path d="M{inset + 3} {inset + 2} L{w - ch - inset - 2} {inset + 2} '
                f'L{w - ch - inset - 2} {h * 0.42:.1f} L{inset + 3} {h * 0.42:.1f} Z" fill="url(#sheen)"/>')
    # Micro-facet crack across the glass.
    body.append(f'<path d="M{w * 0.30:.0f} {inset} L{w * 0.44:.0f} {h - inset}" '
                f'stroke="#dff0ff" stroke-opacity="{0.10 if state == "focus" else 0.06}" stroke-width="1.2"/>')
    if state == "focus":
        for sw, so in ((9.0, 0.10), (5.0, 0.22), (2.4, 0.95)):
            body.append(f'<path d="{outline}" fill="none" stroke="{border[0]}" stroke-opacity="{so}" stroke-width="{sw}"/>')
        # The mockup's diamond bullet, lit, at the left of the focused row.
        body.append(f'<circle cx="30" cy="{h / 2}" r="15" fill="url(#gem)"/>')
        body.append(f'<path d="M30 {h / 2 - 8} L36 {h / 2} L30 {h / 2 + 8} L24 {h / 2} Z" '
                    f'fill="#eaffff" stroke="#35e8ff" stroke-width="1.4"/>')
    else:
        body.append(f'<path d="{outline}" fill="none" stroke="{border[0]}" '
                    f'stroke-opacity="{border[1]}" stroke-width="1.8"/>')
        if state != "disabled":
            body.append(f'<path d="M30 {h / 2 - 6} L34.5 {h / 2} L30 {h / 2 + 6} L25.5 {h / 2} Z" '
                        f'fill="none" stroke="#8fb8d8" stroke-opacity="0.5" stroke-width="1.3"/>')
    return _svg(w, h, defs, body)


def build_title_flourish() -> str:
    w, h = 640, 72
    cx, cy = w / 2, h / 2
    defs = [
        '<linearGradient id="silver" x1="0" y1="0" x2="1" y2="0">'
        '<stop offset="0" stop-color="#8d99b8" stop-opacity="0"/>'
        '<stop offset="0.2" stop-color="#c9d4e8" stop-opacity="0.85"/>'
        '<stop offset="0.5" stop-color="#eef4ff" stop-opacity="1"/>'
        '<stop offset="0.8" stop-color="#c9d4e8" stop-opacity="0.85"/>'
        '<stop offset="1" stop-color="#8d99b8" stop-opacity="0"/></linearGradient>',
        '<radialGradient id="gemglow" cx="0.5" cy="0.5" r="0.5">'
        '<stop offset="0" stop-color="#d8fbff" stop-opacity="0.9"/>'
        '<stop offset="0.6" stop-color="#35c4e8" stop-opacity="0.35"/>'
        '<stop offset="1" stop-color="#35c4e8" stop-opacity="0"/></radialGradient>',
    ]
    body = [
        f'<path d="M40 {cy} L{cx - 60} {cy}" stroke="url(#silver)" stroke-width="2.4"/>',
        f'<path d="M{cx + 60} {cy} L{w - 40} {cy}" stroke="url(#silver)" stroke-width="2.4"/>',
        f'<path d="M70 {cy - 5} L{cx - 80} {cy - 5}" stroke="url(#silver)" stroke-width="1" stroke-opacity="0.5"/>',
        f'<path d="M{cx + 80} {cy - 5} L{w - 70} {cy - 5}" stroke="url(#silver)" stroke-width="1" stroke-opacity="0.5"/>',
        f'<circle cx="{cx}" cy="{cy}" r="30" fill="url(#gemglow)"/>',
        f'<path d="M{cx} {cy - 16} L{cx + 11} {cy} L{cx} {cy + 16} L{cx - 11} {cy} Z" '
        f'fill="#eef8ff" stroke="#9fd8f4" stroke-width="1.6"/>',
        f'<path d="M{cx} {cy - 9} L{cx + 6} {cy} L{cx} {cy + 9} L{cx - 6} {cy} Z" fill="#35c4e8" fill-opacity="0.8"/>',
        f'<path d="M{cx - 42} {cy - 7} L{cx - 32} {cy} L{cx - 42} {cy + 7} L{cx - 52} {cy} Z" fill="#c9d4e8" fill-opacity="0.9"/>',
        f'<path d="M{cx + 42} {cy - 7} L{cx + 52} {cy} L{cx + 42} {cy + 7} L{cx + 32} {cy} Z" fill="#c9d4e8" fill-opacity="0.9"/>',
    ]
    return _svg(w, h, defs, body)


def build_card_frame(variant: str) -> str:
    """Engraved portrait card frame; the centre stays transparent for the art."""
    w, h = 220, 260
    m = 7.0
    glow = {"neutral": None, "p1": "#35b8ff", "p2": "#b06bff"}[variant]
    defs = [
        '<linearGradient id="metal" x1="0" y1="0" x2="0" y2="1">'
        '<stop offset="0" stop-color="#d9c9a3"/>'
        '<stop offset="0.5" stop-color="#8a7952"/>'
        '<stop offset="1" stop-color="#5c4f34"/></linearGradient>',
        '<linearGradient id="inner_shade" x1="0" y1="0" x2="0" y2="1">'
        '<stop offset="0" stop-color="#0a0e20" stop-opacity="0.55"/>'
        '<stop offset="1" stop-color="#0a0e20" stop-opacity="0.25"/></linearGradient>',
    ]
    cut = 16
    outer = (f"M{m + cut} {m} L{w - m - cut} {m} L{w - m} {m + cut} L{w - m} {h - m - cut} "
             f"L{w - m - cut} {h - m} L{m + cut} {h - m} L{m} {h - m - cut} L{m} {m + cut} Z")
    b = 8.0
    inner = (f"M{m + cut + b * 0.6} {m + b} L{w - m - cut - b * 0.6} {m + b} L{w - m - b} {m + cut + b * 0.6} "
             f"L{w - m - b} {h - m - cut - b * 0.6} L{w - m - cut - b * 0.6} {h - m - b} "
             f"L{m + cut + b * 0.6} {h - m - b} L{m + b} {h - m - cut - b * 0.6} L{m + b} {m + cut + b * 0.6} Z")
    body = []
    if glow:
        for sw, so in ((13.0, 0.12), (7.0, 0.28), (3.5, 0.8)):
            body.append(f'<path d="{outer}" fill="none" stroke="{glow}" stroke-opacity="{so}" stroke-width="{sw}"/>')
    body += [
        f'<path d="{outer}" fill="none" stroke="url(#metal)" stroke-width="{b}"/>',
        f'<path d="{outer}" fill="none" stroke="#2e2618" stroke-width="1.4"/>',
        f'<path d="{inner}" fill="none" stroke="#e8dcb8" stroke-opacity="0.55" stroke-width="1.4"/>',
        # Corner clock-tick ornaments.
    ]
    for cxo, cyo, dx, dy in ((m + cut, m + cut, 1, 1), (w - m - cut, m + cut, -1, 1),
                             (w - m - cut, h - m - cut, -1, -1), (m + cut, h - m - cut, 1, -1)):
        body.append(f'<path d="M{cxo - 8 * dx} {cyo} L{cxo + 4 * dx} {cyo} M{cxo} {cyo - 8 * dy} L{cxo} {cyo + 4 * dy}" '
                    f'stroke="#e8dcb8" stroke-opacity="0.8" stroke-width="2"/>')
        body.append(f'<circle cx="{cxo}" cy="{cyo}" r="2.6" fill="#e8dcb8" fill-opacity="0.9"/>')
    # A soft inner shading band so any art behind the frame seats into it.
    body.append(f'<path d="{inner}" fill="url(#inner_shade)" fill-opacity="0.35"/>')
    return _svg(w, h, defs, body)


def _svg(w: int, h: int, defs, body) -> str:
    return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="0 0 {w} {h}">\n'
            f'<defs>{"".join(defs)}</defs>\n' + "\n".join(body) + "\n</svg>\n")


def main() -> None:
    outputs = {
        "glass_shards.svg": build_glass_shards(),
        "clock_etch.svg": build_clock_etch(),
        "glass_button_normal.svg": build_button("normal"),
        "glass_button_focus.svg": build_button("focus"),
        "glass_button_pressed.svg": build_button("pressed"),
        "glass_button_disabled.svg": build_button("disabled"),
        "glass_button_focus_ring.svg": build_button("focus_ring"),
        "title_flourish.svg": build_title_flourish(),
        "card_frame.svg": build_card_frame("neutral"),
        "card_frame_p1.svg": build_card_frame("p1"),
        "card_frame_p2.svg": build_card_frame("p2"),
    }
    for name, text in outputs.items():
        with open(os.path.join(HERE, name), "w", encoding="utf-8", newline="\n") as f:
            f.write(text)
        print(f"wrote {name}")


if __name__ == "__main__":
    main()
