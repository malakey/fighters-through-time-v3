"""Pompeii (Level 6) environment SVG sources (Package 10 pass 1).

Emits into this directory:
  vesuvius_overlay.svg     2600x1080 volcano + eruption column + lightning (alpha)
  skyline_burning.svg      1920x600 burning Roman skyline, horizontally tileable
  ruins_near.svg           1920x460 dark near-ground ruin fragments, tileable
  platform_stone.svg       220x28 travertine ledge nine-patch
  floor_stone.svg          220x48 stone road nine-patch
  oneway_planks.svg        200x20 charred scaffold planks nine-patch
  wall_stone.svg           48x220 stacked-stone wall nine-patch

The ash-sky base itself is procedural (an "atmosphere" job in jobs.json); the
volcano rides its own non-repeating Parallax2D layer so Vesuvius stays a single
landmark while the ember sky tiles every 1920 px.

Run:  python build_pompeii_svgs.py
"""
from __future__ import annotations

import math
import os
import random

HERE = os.path.dirname(os.path.abspath(__file__))


def svg(w: int, h: int, defs, body) -> str:
    return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="0 0 {w} {h}">\n'
            f'<defs>{"".join(defs)}</defs>\n' + "\n".join(body) + "\n</svg>\n")


def poly(points) -> str:
    return "M" + " L".join(f"{x:.1f} {y:.1f}" for x, y in points) + " Z"


# --- Vesuvius --------------------------------------------------------------

def build_vesuvius() -> str:
    w, h = 2600, 1080
    cx = 1300.0
    peak_y = 315.0
    defs = [
        '<radialGradient id="crater" cx="0.5" cy="0.5" r="0.5">'
        '<stop offset="0" stop-color="#ffd890" stop-opacity="0.9"/>'
        '<stop offset="0.45" stop-color="#ff8a30" stop-opacity="0.55"/>'
        '<stop offset="1" stop-color="#ff8a30" stop-opacity="0"/></radialGradient>',
        '<linearGradient id="cone" x1="0" y1="0" x2="0" y2="1">'
        '<stop offset="0" stop-color="#2a1710"/>'
        '<stop offset="0.5" stop-color="#1d100c"/>'
        '<stop offset="1" stop-color="#120a08"/></linearGradient>',
        '<radialGradient id="column_glow" cx="0.5" cy="0.9" r="0.65">'
        '<stop offset="0" stop-color="#ff9a40" stop-opacity="0.28"/>'
        '<stop offset="0.55" stop-color="#a04818" stop-opacity="0.10"/>'
        '<stop offset="1" stop-color="#a04818" stop-opacity="0"/></radialGradient>',
    ]
    body = []

    # Eruption column: a dense billow stack, narrow at the vent and blooming
    # upward, undersides caught by crater light.
    body.append(f'<ellipse cx="{cx}" cy="300" rx="330" ry="240" fill="url(#column_glow)"/>')
    rng = random.Random(6)
    billows = []
    for layer in range(8):
        t = layer / 7.0
        y = 310 - t * 250
        spread = 58 + t * 165
        for k in range(6):
            bx = cx + (k - 2.5) * spread * 0.40 + rng.uniform(-18, 18)
            r = (34 + t * 30) * rng.uniform(0.75, 1.2)
            billows.append((bx, y + rng.uniform(-14, 14), r, t))
    for bx, by, r, t in billows:
        body.append(f'<circle cx="{bx:.0f}" cy="{by:.0f}" r="{r:.0f}" fill="#1d1218" fill-opacity="0.96"/>')
    for bx, by, r, t in billows:
        if t < 0.55 and rng.random() < 0.6:
            body.append(f'<circle cx="{bx:.0f}" cy="{by + r * 0.5:.0f}" r="{r * 0.55:.0f}" '
                        f'fill="#5a2814" fill-opacity="{0.5 - t * 0.5:.2f}"/>')

    # Volcanic lightning inside the column.
    for x0, y0, seed in ((cx - 120, 110, 2), (cx + 80, 80, 5), (cx - 10, 160, 9)):
        r2 = random.Random(seed)
        pts = [(x0, y0)]
        for _ in range(5):
            last = pts[-1]
            pts.append((last[0] + r2.uniform(-40, 40), last[1] + r2.uniform(24, 50)))
        d = "M" + " L".join(f"{x:.0f} {y:.0f}" for x, y in pts)
        body.append(f'<path d="{d}" fill="none" stroke="#d8c9ff" stroke-width="4" stroke-opacity="0.25" stroke-linecap="round"/>')
        body.append(f'<path d="{d}" fill="none" stroke="#efe8ff" stroke-width="1.6" stroke-opacity="0.8" stroke-linecap="round"/>')

    # The cone: a solid concave-profile silhouette so it reads against the sky.
    left_base, right_base = cx - 900, cx + 900
    crater_l, crater_r = cx - 118, cx + 96
    body.append(f'<path d="M{left_base} 1082 L{left_base} 1046 '
                f'Q{cx - 620} 900 {cx - 380} 620 Q{cx - 230} 430 {crater_l} {peak_y} '
                f'L{cx - 40} {peak_y + 24} L{cx + 28} {peak_y + 8} L{crater_r} {peak_y - 6} '
                f'Q{cx + 210} 420 {cx + 360} 610 Q{cx + 610} 900 {right_base} 1044 '
                f'L{right_base} 1082 Z" fill="url(#cone)"/>')
    body.append(f'<path d="M{crater_l} {peak_y} L{cx - 40} {peak_y + 24} L{cx + 28} {peak_y + 8} L{crater_r} {peak_y - 6} '
                f'L{crater_r - 38} {peak_y - 26} L{cx - 58} {peak_y - 22} Z" fill="#3a1410"/>')
    body.append(f'<ellipse cx="{cx - 12}" cy="{peak_y + 2}" rx="118" ry="42" fill="url(#crater)"/>')
    # Warm rim along the crater-facing slope edges.
    body.append(f'<path d="M{crater_l} {peak_y + 2} Q{cx - 226} 434 {cx - 376} 622" fill="none" '
                f'stroke="#c2591e" stroke-width="5" stroke-opacity="0.55" stroke-linecap="round"/>')
    body.append(f'<path d="M{crater_r} {peak_y - 4} Q{cx + 206} 424 {cx + 350} 604" fill="none" '
                f'stroke="#8a3a16" stroke-width="4" stroke-opacity="0.4" stroke-linecap="round"/>')

    # Lava rivulets: short, broken, pooling - upper slopes only.
    for sx, dx, seed, wd in ((cx - 70, -150, 3, 4.0), (cx + 34, 130, 7, 3.2), (cx - 16, -60, 11, 2.6)):
        r3 = random.Random(seed)
        steps = 5
        pts = []
        for i in range(steps + 1):
            t = i / steps
            x = sx + dx * t + r3.uniform(-8, 8) * t
            y = peak_y + 8 + 240 * (t ** 1.3)
            pts.append((x, y))
        d = "M" + " L".join(f"{x:.0f} {y:.0f}" for x, y in pts)
        body.append(f'<path d="{d}" fill="none" stroke="#ff7a24" stroke-width="{wd + 3}" stroke-opacity="0.18" stroke-linecap="round"/>')
        body.append(f'<path d="{d}" fill="none" stroke="#ffb060" stroke-width="{wd}" stroke-opacity="0.75" stroke-linecap="round" stroke-dasharray="26 7"/>')
        body.append(f'<circle cx="{pts[-1][0]:.0f}" cy="{pts[-1][1]:.0f}" r="5" fill="#ffd890" fill-opacity="0.45"/>')

    # Thin ash sheets sliding down the flanks.
    body.append(f'<path d="M{cx - 340} 660 Q{cx - 470} 830 {cx - 600} 1000 L{cx - 470} 1000 '
                f'Q{cx - 380} 830 {cx - 300} 700 Z" fill="#3a2a22" fill-opacity="0.25"/>')
    body.append(f'<path d="M{cx + 300} 640 Q{cx + 430} 820 {cx + 560} 1000 L{cx + 430} 1000 '
                f'Q{cx + 350} 820 {cx + 260} 690 Z" fill="#3a2a22" fill-opacity="0.22"/>')

    return svg(w, h, defs, body)


# --- skyline ---------------------------------------------------------------

def build_skyline() -> str:
    w, h = 1920, 600
    ink = "#241108"
    lit = "#5a2410"
    defs = [
        '<radialGradient id="fire" cx="0.5" cy="0.5" r="0.5">'
        '<stop offset="0" stop-color="#ffb060" stop-opacity="0.65"/>'
        '<stop offset="0.6" stop-color="#ff7a24" stop-opacity="0.25"/>'
        '<stop offset="1" stop-color="#ff7a24" stop-opacity="0"/></radialGradient>',
        '<linearGradient id="wallband" x1="0" y1="0" x2="0" y2="1">'
        f'<stop offset="0" stop-color="{ink}"/>'
        '<stop offset="1" stop-color="#180a04"/></linearGradient>',
    ]
    body = []
    base = 600

    def block(x, bw, bh, roof="flat"):
        y = base - bh
        parts = [f'<path d="{poly([(x, base), (x, y), (x + bw, y), (x + bw, base)])}" fill="{ink}"/>']
        if roof == "pediment":
            parts.append(f'<path d="{poly([(x - bw * 0.06, y), (x + bw * 1.06, y), (x + bw / 2, y - bw * 0.24)])}" fill="{ink}"/>')
        elif roof == "tile":
            parts.append(f'<path d="{poly([(x - 6, y), (x + bw + 6, y), (x + bw + 2, y - 12), (x + 2, y - 12)])}" fill="{ink}"/>')
        return parts

    # Fire glows behind the roofline, then the continuous city wall band that
    # makes the tile seamless, then the buildings.
    for gx, gy, gr in ((240, 420, 130), (700, 400, 170), (1180, 430, 150), (1620, 410, 140)):
        body.append(f'<ellipse cx="{gx}" cy="{gy}" rx="{gr}" ry="{gr * 0.62:.0f}" fill="url(#fire)"/>')
    body.append(f'<rect x="0" y="{base - 88}" width="{w}" height="88" fill="url(#wallband)"/>')
    for x in range(0, w, 96):
        body.append(f'<rect x="{x + 34}" y="{base - 108}" width="26" height="22" fill="{ink}"/>')

    # Temple with pediment.
    body += block(150, 240, 250, "pediment")
    for i in range(5):
        body.append(f'<rect x="{176 + i * 44}" y="{base - 224}" width="18" height="128" fill="{lit}" fill-opacity="0.55"/>')
    # Colonnade row.
    body += block(560, 320, 170, "tile")
    for i in range(7):
        body.append(f'<rect x="{578 + i * 42}" y="{base - 148}" width="14" height="96" fill="{lit}" fill-opacity="0.5"/>')
    # Insulae with burning windows.
    body += block(1000, 190, 300, "tile")
    body += block(1230, 150, 232, "flat")
    rng = random.Random(17)
    for bx, bw_, bh_ in ((1000, 190, 300), (1230, 150, 232)):
        for _ in range(9):
            wx = bx + 16 + rng.random() * (bw_ - 40)
            wy = base - 30 - rng.random() * (bh_ - 70)
            body.append(f'<rect x="{wx:.0f}" y="{wy:.0f}" width="9" height="13" fill="#ff9a40" fill-opacity="{rng.uniform(0.5, 0.95):.2f}"/>')
    # Statue on a column.
    body.append(f'<rect x="1520" y="{base - 260}" width="26" height="260" fill="{ink}"/>')
    body.append(f'<path d="{poly([(1510, base - 260), (1556, base - 260), (1550, base - 276), (1516, base - 276)])}" fill="{ink}"/>')
    body.append(f'<path d="M1533 {base - 276} l-7 -26 l7 -8 l8 6 l-4 12 l9 4 l-5 14 Z" fill="{ink}"/>')
    # Second temple, far right, kept off the seam.
    body += block(1680, 180, 200, "pediment")
    for i in range(4):
        body.append(f'<rect x="{1700 + i * 42}" y="{base - 178}" width="15" height="100" fill="{lit}" fill-opacity="0.5"/>')
    # Dark smoke columns rising off the burning blocks.
    for sx, sy in ((360, base - 250), (1090, base - 300)):
        body.append(f'<path d="M{sx} {sy} Q{sx + 26} {sy - 60} {sx + 8} {sy - 120} Q{sx - 8} {sy - 168} {sx + 18} {sy - 210} '
                    f'L{sx + 54} {sy - 210} Q{sx + 30} {sy - 160} {sx + 44} {sy - 110} Q{sx + 56} {sy - 54} {sx + 36} {sy} Z" '
                    f'fill="#120a0a" fill-opacity="0.5"/>')
    return svg(w, h, defs, body)


# --- near ruins ------------------------------------------------------------

def build_ruins() -> str:
    w, h = 1920, 460
    ink = "#160a05"
    edge = "#3a1c0c"
    defs: list[str] = []
    body = [f'<rect x="0" y="{h - 46}" width="{w}" height="46" fill="{ink}"/>']

    def column_drum(x, y, cw, chh):
        return (f'<path d="{poly([(x, y), (x + cw, y), (x + cw - 3, y - chh), (x + 3, y - chh)])}" fill="{ink}" '
                f'stroke="{edge}" stroke-width="2"/>')

    # Aqueduct fragment: two piers carrying a broken deck with an arch cut.
    body.append(f'<path d="M180 {h} L180 236 L164 236 L164 206 L436 206 L436 236 L420 236 L420 {h} '
                f'L352 {h} L352 300 Q352 258 300 258 Q248 258 248 300 L248 {h} Z" '
                f'fill="{ink}" stroke="{edge}" stroke-width="3"/>')
    body.append(f'<path d="M436 206 L470 214 L470 236 L436 236 Z" fill="{ink}" stroke="{edge}" stroke-width="2.4"/>')
    # Fallen column drums.
    body.append(column_drum(820, h - 40, 150, 44))
    body.append(column_drum(850, h - 88, 130, 40))
    body.append(f'<ellipse cx="1060" cy="{h - 58}" rx="52" ry="46" fill="{ink}" stroke="{edge}" stroke-width="3"/>')
    body.append(f'<ellipse cx="1060" cy="{h - 58}" rx="30" ry="26" fill="none" stroke="{edge}" stroke-width="2" stroke-opacity="0.6"/>')
    # Standing broken column pair.
    for x, hh in ((1310, 260), (1400, 180)):
        body.append(f'<path d="{poly([(x, h), (x + 44, h), (x + 40, h - hh), (x + 22, h - hh - 16), (x + 4, h - hh)])}" '
                    f'fill="{ink}" stroke="{edge}" stroke-width="3"/>')
        for i in range(3):
            body.append(f'<path d="M{x + 8 + i * 12} {h - 8} L{x + 8 + i * 12} {h - hh + 14}" '
                        f'stroke="{edge}" stroke-width="1.6" stroke-opacity="0.5"/>')
    # Half pediment leaning.
    body.append(f'<path d="{poly([(1620, h), (1852, h), (1836, h - 60), (1700, h - 118), (1636, h - 48)])}" '
                f'fill="{ink}" stroke="{edge}" stroke-width="3"/>')
    # Ember specks drifting.
    rng = random.Random(23)
    for _ in range(26):
        ex = 60 + rng.random() * (w - 120)
        ey = 40 + rng.random() * (h - 120)
        body.append(f'<circle cx="{ex:.0f}" cy="{ey:.0f}" r="{rng.uniform(1.2, 2.6):.1f}" '
                    f'fill="#ff9a40" fill-opacity="{rng.uniform(0.25, 0.7):.2f}"/>')
    return svg(w, h, defs, body)


# --- surface tiles ---------------------------------------------------------

def build_platform() -> str:
    w, h = 220, 28
    defs = [
        '<linearGradient id="stone" x1="0" y1="0" x2="0" y2="1">'
        '<stop offset="0" stop-color="#7a5a38"/>'
        '<stop offset="0.55" stop-color="#5c4226"/>'
        '<stop offset="1" stop-color="#3a2716"/></linearGradient>',
    ]
    body = [
        f'<rect x="0" y="2" width="{w}" height="{h - 2}" rx="3" fill="url(#stone)" stroke="#241708" stroke-width="2.4"/>',
        f'<rect x="2" y="2.6" width="{w - 4}" height="3.4" rx="1.6" fill="#c99a5e" fill-opacity="0.9"/>',
        f'<rect x="2" y="{h - 5}" width="{w - 4}" height="3.4" rx="1.6" fill="#1c0f08" fill-opacity="0.7"/>',
    ]
    for x in (74, 148):
        body.append(f'<path d="M{x} 4 L{x - 2} {h - 3}" stroke="#33200f" stroke-width="2.4"/>')
        body.append(f'<path d="M{x + 2} 4 L{x} {h - 3}" stroke="#8a6844" stroke-width="1" stroke-opacity="0.5"/>')
    body.append(f'<path d="M30 8 L44 14 L38 22" fill="none" stroke="#33200f" stroke-width="1.6"/>')
    body.append(f'<path d="M182 7 L172 15 L180 21" fill="none" stroke="#33200f" stroke-width="1.6"/>')
    body.append(f'<path d="M104 24 q 6 -3 12 0" fill="none" stroke="#4a5a2c" stroke-width="2.4" stroke-opacity="0.6"/>')
    return svg(w, h, defs, body)


def build_floor() -> str:
    w, h = 220, 48
    defs = [
        '<linearGradient id="road" x1="0" y1="0" x2="0" y2="1">'
        '<stop offset="0" stop-color="#6a4a2c"/>'
        '<stop offset="0.4" stop-color="#4c3520"/>'
        '<stop offset="1" stop-color="#2a1a0e"/></linearGradient>',
    ]
    body = [
        f'<rect x="0" y="0" width="{w}" height="{h}" fill="url(#road)" stroke="#241708" stroke-width="2.6"/>',
        f'<rect x="1.5" y="1.5" width="{w - 3}" height="3.6" fill="#c99a5e" fill-opacity="0.85"/>',
    ]
    rng = random.Random(31)
    for row, y in ((0, 12), (1, 24), (2, 36)):
        offset = 18 if row % 2 else 0
        for x in range(offset, w, 44):
            body.append(f'<path d="M{x + 4} {y} L{x + 34 + rng.randint(-3, 3)} {y}" '
                        f'stroke="#241708" stroke-width="1.8" stroke-opacity="0.75"/>')
    body.append(f'<path d="M58 8 L66 20 L60 30" fill="none" stroke="#1c0f08" stroke-width="1.8"/>')
    body.append(f'<path d="M168 10 L158 22" fill="none" stroke="#1c0f08" stroke-width="1.8"/>')
    body.append(f'<circle cx="120" cy="30" r="4" fill="#ff9a40" fill-opacity="0.25"/>')
    return svg(w, h, defs, body)


def build_oneway() -> str:
    w, h = 200, 20
    defs = [
        '<linearGradient id="plank" x1="0" y1="0" x2="0" y2="1">'
        '<stop offset="0" stop-color="#5c3a1c"/>'
        '<stop offset="1" stop-color="#33200f"/></linearGradient>',
    ]
    body = [
        f'<rect x="0" y="3" width="{w}" height="{h - 5}" rx="2.5" fill="url(#plank)" stroke="#1c0e06" stroke-width="2"/>',
        f'<rect x="2" y="3.5" width="{w - 4}" height="2.6" fill="#a86a34" fill-opacity="0.9"/>',
    ]
    for x in (66, 134):
        body.append(f'<path d="M{x} 4 L{x} {h - 3}" stroke="#1c0e06" stroke-width="2.2"/>')
    for x0, x1, y in ((10, 52, 11), (76, 120, 13), (146, 188, 10)):
        body.append(f'<path d="M{x0} {y} q {(x1 - x0) / 2} -2.5 {x1 - x0} 0" fill="none" '
                    f'stroke="#241206" stroke-width="1.1" stroke-opacity="0.8"/>')
    for x in (4, 192):
        body.append(f'<rect x="{x}" y="4.5" width="4" height="{h - 8}" fill="#6a6a72" fill-opacity="0.8"/>')
    return svg(w, h, defs, body)


def build_wall() -> str:
    w, h = 48, 220
    defs = [
        '<linearGradient id="wallstone" x1="0" y1="0" x2="1" y2="0">'
        '<stop offset="0" stop-color="#6a4a2c"/>'
        '<stop offset="0.5" stop-color="#4c3520"/>'
        '<stop offset="1" stop-color="#2e1c10"/></linearGradient>',
    ]
    body = [f'<rect x="0" y="0" width="{w}" height="{h}" fill="url(#wallstone)" stroke="#241708" stroke-width="2.4"/>']
    rng = random.Random(41)
    y = 0
    row = 0
    while y < h - 10:
        step = rng.randint(26, 38)
        body.append(f'<path d="M2 {y + step} L{w - 2} {y + step}" stroke="#241708" stroke-width="1.8" stroke-opacity="0.8"/>')
        split = rng.randint(14, w - 14) if row % 2 else rng.randint(10, w - 20)
        body.append(f'<path d="M{split} {y + 2} L{split} {y + step - 2}" stroke="#241708" stroke-width="1.5" stroke-opacity="0.6"/>')
        y += step
        row += 1
    body.append(f'<rect x="1.5" y="1.5" width="3" height="{h - 3}" fill="#c99a5e" fill-opacity="0.5"/>')
    return svg(w, h, defs, body)


def main() -> None:
    outputs = {
        "vesuvius_overlay.svg": build_vesuvius(),
        "skyline_burning.svg": build_skyline(),
        "ruins_near.svg": build_ruins(),
        "platform_stone.svg": build_platform(),
        "floor_stone.svg": build_floor(),
        "oneway_planks.svg": build_oneway(),
        "wall_stone.svg": build_wall(),
    }
    for name, text in outputs.items():
        with open(os.path.join(HERE, name), "w", encoding="utf-8", newline="\n") as f:
            f.write(text)
        print(f"wrote {name}")


if __name__ == "__main__":
    main()
