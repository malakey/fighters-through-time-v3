"""Shared helpers for the pulp UI kit's SVG sources (ThorVG-safe subset only:
paths, gradients, opacity; no filters, masks, patterns or text).

Geometry model (UI_SPEC section 0.3): a plate is a *border box* plus outward
rings plus one hard offset shadow.  Rings are the border box grown by their
cumulative width; the shadow is the border box grown by its spread and moved by
(dx, dy).  CSS clips outer shadows (and rings) out of the border box, so the
interior of a translucent plate shows the scene: both are emitted as even-odd
paths with the box cut out.
"""
from __future__ import annotations

import math
from dataclasses import dataclass, field

INK = "#000000"
PAPER = "#F3F0E6"
ACCENT = "#F3C95B"
ACCENT2 = "#FFE39A"
BG1 = "#090C1E"
DARK = "#0C0A10"
TRACK = "#3A3144"
SLOT = "#2A2434"
P1 = "#00F0FF"
P2 = "#FF3366"
DANGER = "#FF5A5A"
CREAM = "#FFF3D8"


def f(v: float) -> str:
    s = f"{v:.3f}".rstrip("0").rstrip(".")
    return s if s not in ("-0", "") else "0"


def svg(w: float, h: float, body: str, defs: str = "", view: tuple | None = None) -> str:
    vb = view or (0, 0, w, h)
    d = f"<defs>{defs}</defs>" if defs else ""
    return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{f(w)}" height="{f(h)}" '
            f'viewBox="{" ".join(f(v) for v in vb)}">{d}{body}</svg>\n')


@dataclass
class Box:
    """A border box in texture pixels.  s = skew slope (CSS skewX(-a) leans '/'),
    r = corner radius, circle = ellipse."""
    x: float
    y: float
    w: float
    h: float
    s: float = 0.0
    r: float = 0.0
    circle: bool = False

    def path(self, grow: float = 0.0, dx: float = 0.0, dy: float = 0.0) -> str:
        x, y, w, h = self.x + dx, self.y + dy, self.w, self.h
        if self.circle:
            cx, cy = x + w / 2, y + h / 2
            rx, ry = w / 2 + grow, h / 2 + grow
            return (f"M{f(cx - rx)},{f(cy)} A{f(rx)},{f(ry)} 0 1 0 {f(cx + rx)},{f(cy)} "
                    f"A{f(rx)},{f(ry)} 0 1 0 {f(cx - rx)},{f(cy)} Z")
        if self.s:
            k = grow * math.sqrt(1 + self.s * self.s)  # horizontal offset of a slanted edge
            sh = self.s * h / 2
            x0, x1 = x - k, x + w + k
            y0, y1 = y - grow, y + h + grow
            # the grown polygon keeps the same slant about the same centre line
            sh2 = self.s * (h + 2 * grow) / 2
            return (f"M{f(x0 + sh2)},{f(y0)} L{f(x1 + sh2)},{f(y0)} "
                    f"L{f(x1 - sh2)},{f(y1)} L{f(x0 - sh2)},{f(y1)} Z")
        x0, y0, x1, y1 = x - grow, y - grow, x + w + grow, y + h + grow
        r = max(0.0, self.r + grow) if self.r else 0.0
        if r <= 0:
            return f"M{f(x0)},{f(y0)} L{f(x1)},{f(y0)} L{f(x1)},{f(y1)} L{f(x0)},{f(y1)} Z"
        r = min(r, (x1 - x0) / 2, (y1 - y0) / 2)
        return (f"M{f(x0 + r)},{f(y0)} L{f(x1 - r)},{f(y0)} A{f(r)},{f(r)} 0 0 1 {f(x1)},{f(y0 + r)} "
                f"L{f(x1)},{f(y1 - r)} A{f(r)},{f(r)} 0 0 1 {f(x1 - r)},{f(y1)} "
                f"L{f(x0 + r)},{f(y1)} A{f(r)},{f(r)} 0 0 1 {f(x0)},{f(y1 - r)} "
                f"L{f(x0)},{f(y0 + r)} A{f(r)},{f(r)} 0 0 1 {f(x0 + r)},{f(y0)} Z")

    def points(self, grow: float = 0.0, dx: float = 0.0, dy: float = 0.0):
        """Polygon vertices (skewed or plain rectangles only)."""
        x, y, w, h = self.x + dx, self.y + dy, self.w, self.h
        k = grow * math.sqrt(1 + self.s * self.s)
        sh2 = self.s * (h + 2 * grow) / 2
        x0, x1 = x - k, x + w + k
        y0, y1 = y - grow, y + h + grow
        return [(x0 + sh2, y0), (x1 + sh2, y0), (x1 - sh2, y1), (x0 - sh2, y1)]


def eo(path_outer: str, path_hole: str, fill: str, opacity: float = 1.0) -> str:
    op = f' fill-opacity="{f(opacity)}"' if opacity != 1.0 else ""
    return f'<path fill-rule="evenodd" fill="{fill}"{op} d="{path_outer} {path_hole}"/>'


def pth(d: str, fill: str = "none", stroke: str = "", sw: float = 0, extra: str = "", opacity: float = 1.0) -> str:
    a = f'fill="{fill}"'
    if opacity != 1.0:
        a += f' fill-opacity="{f(opacity)}"'
    if stroke:
        a += f' stroke="{stroke}" stroke-width="{f(sw)}" stroke-linejoin="round" stroke-linecap="round"'
    return f'<path {a} {extra} d="{d}"/>'


@dataclass
class Plate:
    """A fully specified ink plate."""
    w: float                      # border box width (the stretchable axis for 9-slices)
    h: float
    s: float = 0.0
    r: float = 0.0
    circle: bool = False
    border: tuple = (3, PAPER)    # (width, colour)
    rings: list = field(default_factory=list)   # [(width, colour)] outward order
    shadow: tuple | None = None   # (dx, dy, spread, colour)
    fill: str = BG1
    fill_opacity: float = 1.0
    extra_inner: str = ""         # svg drawn inside the inner area (texture coords, box-relative via callback)
    defs: str = ""
    pad: float = 0.0              # extra transparent margin on every side

    # -- extents (outward from the border box, including the skew overhang)
    def ext(self):
        rt = sum(w for w, _ in self.rings)
        sd = self.shadow
        biggest = max(rt, sd[2] if sd else 0)
        slant = (self.s * (self.h / 2 + biggest) + (math.sqrt(1 + self.s * self.s) - 1) * biggest) if self.s else 0
        if sd:
            dx, dy, S, _ = sd
            L = max(rt, S - dx) + slant
            R = max(rt, S + dx) + slant
            T = max(rt, S - dy)
            B = max(rt, S + dy)
        else:
            L = R = rt + slant
            T = B = rt
        return [math.ceil(v + self.pad) for v in (L, T, R, B)]

    def box(self):
        L, T, _, _ = self.ext()
        return Box(L, T, self.w, self.h, self.s, self.r, self.circle)

    def size(self):
        L, T, R, B = self.ext()
        return (L + self.w + R, T + self.h + B)

    def render(self, scale: float = 1.0) -> str:
        W, H = self.size()
        b = self.box()
        out = []
        if self.shadow:
            dx, dy, S, col = self.shadow
            out.append(eo(b.path(S, dx, dy), b.path(0), col))
        g = 0.0
        cum = []
        for wd, col in self.rings:
            g += wd
            cum.append((g, col))
        for i in range(len(cum) - 1, -1, -1):
            gi, col = cum[i]
            inner = cum[i - 1][0] if i > 0 else 0.0
            out.append(eo(b.path(gi), b.path(inner), col))
        bw, bc = self.border
        inner_box = Box(b.x + bw if not b.s else b.x + bw, b.y + bw, b.w - 2 * bw, b.h - 2 * bw, b.s, max(0, b.r - bw), b.circle)
        if bw:
            out.append(eo(b.path(0), b.path(-bw), bc))
        if self.fill and self.fill != "none":
            out.append(f'<path fill="{self.fill}"' + (f' fill-opacity="{f(self.fill_opacity)}"' if self.fill_opacity != 1 else "")
                       + f' d="{b.path(-bw)}"/>')
        out.append(self.extra_inner)
        return svg(W * scale, H * scale, "".join(out), self.defs, view=(0, 0, W, H))

    # -- nine-slice margins in texture px (see handoff for the reasoning)
    def margins(self, safe: float = 2.0):
        L, T, R, B = self.ext()
        W, H = self.size()
        bw = self.border[0]
        cosk = math.sqrt(1 + self.s * self.s)
        slant_h = self.s * self.h if self.s else 0
        sh = self.shadow
        sdy = (sh[1] - sh[2]) if sh else 0
        sdx = (sh[0] - sh[2]) if sh else 0
        if self.s:
            # the whole slanted end lives in the corner slices
            left = L + slant_h / 2 + bw * cosk + safe
            right = (W - (L + self.w)) + slant_h / 2 + bw * cosk + safe
        else:
            left = L + max(bw, sdx) + safe
            right = (W - (L + self.w)) + bw + safe
        top = T + max(bw, sdy) + safe
        bottom = (H - (T + self.h)) + bw + safe
        if self.circle:
            return None
        if self.r:
            left = max(left, L + self.r + safe)
            top = max(top, T + self.r + safe)
            right = max(right, (W - (L + self.w)) + self.r + safe)
            bottom = max(bottom, (H - (T + self.h)) + self.r + safe)
        if self.r and self.r * 2 >= self.h:          # a pill: the whole height is corner, stretch horizontally only
            top = H // 2
            bottom = H - top
        return [math.ceil(v) for v in (left, top, right, bottom)]


def star(n: int, ro: float, ri: float, cx: float, cy: float, rot: float = -90.0):
    pts = []
    for i in range(2 * n):
        a = math.radians(rot + i * 180.0 / n)
        r = ro if i % 2 == 0 else ri
        pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts


def poly_d(pts) -> str:
    return "M" + " L".join(f"{f(x)},{f(y)}" for x, y in pts) + " Z"


def clip_convex(subject, clipper):
    """Sutherland-Hodgman: subject polygon clipped by a convex clipper polygon
    (both clockwise or both counter-clockwise)."""
    def inside(p, a, b, sign):
        return sign * ((b[0] - a[0]) * (p[1] - a[1]) - (b[1] - a[1]) * (p[0] - a[0])) >= 0

    def inter(p1, p2, a, b):
        x1, y1, x2, y2 = p1[0], p1[1], p2[0], p2[1]
        x3, y3, x4, y4 = a[0], a[1], b[0], b[1]
        den = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4)
        if abs(den) < 1e-12:
            return p2
        t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / den
        return (x1 + t * (x2 - x1), y1 + t * (y2 - y1))

    area = sum(clipper[i][0] * clipper[(i + 1) % len(clipper)][1] - clipper[(i + 1) % len(clipper)][0] * clipper[i][1]
               for i in range(len(clipper)))
    sign = 1 if area > 0 else -1
    out = list(subject)
    for i in range(len(clipper)):
        a, b = clipper[i], clipper[(i + 1) % len(clipper)]
        inp, out = out, []
        if not inp:
            break
        s = inp[-1]
        for e in inp:
            if inside(e, a, b, sign):
                if not inside(s, a, b, sign):
                    out.append(inter(s, e, a, b))
                out.append(e)
            elif inside(s, a, b, sign):
                out.append(inter(s, e, a, b))
            s = e
    return out


def diagonal_bands(region, period: float, width: float, angle_deg: float, color: str, offset: float = 0.0,
                   opacity: float = 1.0, bounds=(0, 0, 4000, 4000)):
    """Stripes of `width` every `period` (measured perpendicular), the stripe
    lines running along angle_deg (0 = horizontal, 45 = '/' in screen space with y
    down means direction (cos, -sin)); each is clipped to the convex `region`."""
    a = math.radians(angle_deg)
    d = (math.cos(a), -math.sin(a))          # along the stripe
    n = (math.sin(a), math.cos(a))           # perpendicular
    xs = [p[0] for p in region]
    ys = [p[1] for p in region]
    cx, cy = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2
    reach = math.hypot(max(xs) - min(xs), max(ys) - min(ys))
    k0 = -int(reach / period) - 2
    k1 = int(reach / period) + 3
    out = []
    for k in range(k0, k1):
        off = k * period + offset
        c0 = (cx + n[0] * off, cy + n[1] * off)
        c1 = (cx + n[0] * (off + width), cy + n[1] * (off + width))
        ext = reach
        band = [(c0[0] - d[0] * ext, c0[1] - d[1] * ext), (c0[0] + d[0] * ext, c0[1] + d[1] * ext),
                (c1[0] + d[0] * ext, c1[1] + d[1] * ext), (c1[0] - d[0] * ext, c1[1] - d[1] * ext)]
        poly = clip_convex(band, region)
        if len(poly) >= 3:
            op = f' fill-opacity="{f(opacity)}"' if opacity != 1 else ""
            out.append(f'<path fill="{color}"{op} d="{poly_d(poly)}"/>')
    return "".join(out)
