"""TG-ART: the Temporal Glass UI kit's SVG sources, their raster jobs, and the kit definition table.

UI_SPEC_GLASS (D:/Projects/fighters-through-time-docs-2/mockups/ui_redesign_2026-10-25/UI_SPEC_GLASS.md) 6.4
lists the nine-slice textures; this script writes them as layered ThorVG-safe SVG sources (paths, linear
gradients, opacity, even-odd fills; no filters, masks, clip paths or text), writes a normalized copy of every
ICN-A icon (the committed sources in icons_src/, plus the bold weight derived for the glyphs the spec draws in
one weight only), and appends the matching jobs for tools/generate_assets.gd to tools/asset_src/jobs.json
(groups "glass" and "glass_icons_raw"; a rerun replaces exactly those two groups and nothing else).

Every SVG is declared at 4x its texture size with a 1x viewBox and its job renders it at supersample 1, so the
rasterizer writes a 4x image with no resize. build_glass_images.py downsamples 4:1 in premultiplied space (exact
area coverage, so hairlines keep their colour), adds the soft shadows and glows ThorVG cannot draw (6.3), and
writes the PNGs into assets/sprites/ui/glass/.

The module is also the kit's single definition table (KIT, ICON_TABLE, GRADIENTS): build_glass_images.py,
write_catalog.py and build_contact_sheet.py import it, so a margin or a colour lives in one place.

Run from the repository root:   py tools/asset_src/glass/glass_svg.py
then, holding D:/Projects/.ftt-gdunit-lock:
    godot --headless --path . --script res://tools/generate_assets.gd -- group=glass,glass_icons_raw
(tools/asset_src/glass/build_all.ps1 runs the whole chain.)
"""
from __future__ import annotations

import json
import math
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
SVG_DIR = os.path.join(HERE, "svg")
ICON_SRC = os.path.join(HERE, "icons_src")
RAW_RES = "res://tools/asset_src/glass/raw/"
SVG_RES = "res://tools/asset_src/glass/svg/"
JOBS = os.path.join(REPO, "tools", "asset_src", "jobs.json")
JOB_GROUPS = ("glass", "glass_icons_raw")
SS = 4  # every raster is rendered at 4x and downsampled 4:1 by build_glass_images.py

# ---------------------------------------------------------------------------------------------------------------
# Palette (UI_SPEC_GLASS 1.1, 1.3, 1.4; PAL-B). Colours are (r, g, b, a) with 0-255 channels and a 0-1 alpha.
# ---------------------------------------------------------------------------------------------------------------
GLOW = (243, 201, 91)                 # the PAL-B gold triple, rgba(glow, a)
EDGE = GLOW + (0.55,)                 # the standard hairline
EDGE_HI = (255, 212, 92, 1.0)         # the lit hairline (#FFD45C)
EDGE_DIM = GLOW + (0.25,)             # the quiet hairline
FOCUS = (255, 212, 92, 1.0)           # #FFD45C
ACCENT = GLOW + (1.0,)                # #F3C95B
SHADOW = (0, 2, 12, 0.60)             # #00020C @0.60
PANEL_FILL_TOP = (15, 24, 52, 0.92)   # build fill (1.4), drawn .80
PANEL_FILL_BOTTOM = (6, 10, 26, 0.95)  # build fill (1.4), drawn .88
BUTTON_FILL = (8, 13, 32, 0.90)       # normal, selected
BUTTON_FILL_FOCUS = (8, 16, 38, 0.92)  # focused, pressed
BUTTON_FILL_DISABLED = (8, 13, 32, 0.50)
FIELD_FILL = (8, 13, 32, 0.90)


def g(a: float) -> tuple:
    """rgba(glow, a)."""
    return GLOW + (a,)


# ---------------------------------------------------------------------------------------------------------------
# Small SVG helpers
# ---------------------------------------------------------------------------------------------------------------

def f(v: float) -> str:
    s = f"{v:.4f}".rstrip("0").rstrip(".")
    return s if s not in ("-0", "") else "0"


def hexc(c) -> str:
    return "#%02X%02X%02X" % tuple(max(0, min(255, int(round(v)))) for v in c[:3])


def poly_d(points) -> str:
    return "M" + " L".join(f"{f(x)},{f(y)}" for x, y in points) + " Z"


def path(d: str, fill: str, evenodd: bool = False) -> str:
    rule = ' fill-rule="evenodd"' if evenodd else ""
    return f'<path{rule} fill="{fill}" d="{d}"/>'


def solid(c) -> str:
    """A fill attribute value plus opacity for a flat colour."""
    return f'{hexc(c)}" fill-opacity="{f(c[3])}'


def svg_doc(w: float, h: float, body: str, defs: str = "") -> str:
    d = f"<defs>{defs}</defs>" if defs else ""
    return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{f(w * SS)}" height="{f(h * SS)}" '
            f'viewBox="0 0 {f(w)} {f(h)}">{d}{body}</svg>\n')


# ---------------------------------------------------------------------------------------------------------------
# CSS gradients as SVG gradients
# ---------------------------------------------------------------------------------------------------------------

def css_line(theta_deg: float, box):
    """UI_SPEC_GLASS 0.2: CSS linear-gradient(theta) on box (x, y, w, h): its centre, direction and length."""
    x, y, w, h = box
    th = math.radians(theta_deg)
    dx, dy = math.sin(th), -math.cos(th)
    return x + w / 2.0, y + h / 2.0, dx, dy, w * abs(dx) + h * abs(dy)


def css_t(theta_deg: float, box, px: float, py: float) -> float:
    cx, cy, dx, dy, L = css_line(theta_deg, box)
    return 0.5 + ((px - cx) * dx + (py - cy) * dy) / L


def grad_endpoints(theta_deg: float, ref_box, warp=(1.0, 0.0, 1.0, 0.0)):
    """Endpoints (x1, y1, x2, y2), in texture space, of an SVG userSpaceOnUse linear gradient equal to the CSS
    gradient `theta` laid on `ref_box`, where a texture point (x, y) sits at reference point
    (ax x + bx, ay y + by). The warp lets a nine-slice carry the gradient of the stretched size it is drawn at
    (the CSS gradient follows the element's own box), so the wash ends where the CSS ends at that size."""
    cx, cy, dx, dy, L = css_line(theta_deg, ref_box)
    ax, bx, ay, by = warp
    al = ax * dx / L
    be = ay * dy / L
    ga = 0.5 + ((bx - cx) * dx + (by - cy) * dy) / L
    n2 = al * al + be * be
    s1, s2 = -ga / n2, (1.0 - ga) / n2
    return s1 * al, s1 * be, s2 * al, s2 * be


def premult_stops(stops, sub: int = 8):
    """CSS interpolates gradient stops in premultiplied space; ThorVG and Godot's Gradient interpolate straight
    RGBA. Where two neighbouring stops differ in both colour and alpha, sub-stops computed in premultiplied
    space make the straight interpolation follow the CSS curve. A zero-alpha stop takes its neighbour's
    colour (its premultiplied equivalent), so a fade never darkens through black."""
    stops = [(t, tuple(c)) for t, c in stops]
    fixed = []
    for i, (t, c) in enumerate(stops):
        if c[3] == 0:
            nb = stops[i - 1][1] if i > 0 else stops[i + 1][1]
            c = tuple(nb[:3]) + (0.0,)
        fixed.append((t, c))
    out = [fixed[0]]
    for (t0, c0), (t1, c1) in zip(fixed, fixed[1:]):
        if tuple(c0[:3]) != tuple(c1[:3]) and c0[3] != c1[3] and c0[3] > 0 and c1[3] > 0:
            for k in range(1, sub):
                u = k / sub
                a = c0[3] + (c1[3] - c0[3]) * u
                rgb = tuple((c0[i] * c0[3] + (c1[i] * c1[3] - c0[i] * c0[3]) * u) / a for i in range(3))
                out.append((t0 + (t1 - t0) * u, rgb + (a,)))
        out.append((t1, c1))
    return out


def lingrad(gid: str, endpoints, stops) -> str:
    x1, y1, x2, y2 = endpoints
    body = "".join(f'<stop offset="{f(t)}" stop-color="{hexc(c)}" stop-opacity="{f(c[3])}"/>'
                   for t, c in premult_stops(stops))
    return (f'<linearGradient id="{gid}" gradientUnits="userSpaceOnUse" x1="{f(x1)}" y1="{f(y1)}" '
            f'x2="{f(x2)}" y2="{f(y2)}">{body}</linearGradient>')


# ---------------------------------------------------------------------------------------------------------------
# Shapes (UI_SPEC_GLASS 0.2 chamfer, 0.3 the edge / fill recipe, 3.0-3.3)
# ---------------------------------------------------------------------------------------------------------------

def chamfer(x, y, w, h, n):
    """CSS polygon(N 0, 100% 0, 100% calc(100% - N), calc(100% - N) 100%, 0 100%, 0 N)."""
    return [(x + n, y), (x + w, y), (x + w, y + h - n), (x + w - n, y + h), (x, y + h), (x, y + n)]


def chamfer_fill(x, y, w, h, n, inset=1.5):
    """The fill shape: the box inset 1.5 px, clipped with N - 1 in its own coordinates (3.1), so the edge keeps
    1.41 px along the chamfers."""
    return chamfer(x + inset, y + inset, w - 2 * inset, h - 2 * inset, n - 1)


def chevron(x, y, w, h, cut=30.0):
    """BTN-A edge shape: polygon(0 0, W-30 0, W H/2, W-30 H, 0 H) (3.2)."""
    return [(x, y), (x + w - cut, y), (x + w, y + h / 2.0), (x + w - cut, y + h), (x, y + h)]


def chevron_fill(x, y, w, h, cut=30.0, inset=1.5):
    """BTN-A fill shape: the box inset 1.5, polygon(0 0, W'-31 0, W'-1 H'/2, W'-31 H', 0 H') (3.2)."""
    X, Y, W, H = x + inset, y + inset, w - 2 * inset, h - 2 * inset
    return [(X, Y), (X + W - cut - 1, Y), (X + W - 1, Y + H / 2.0), (X + W - cut - 1, Y + H), (X, Y + H)]


def diamond(cx, cy, half):
    return [(cx, cy - half), (cx + half, cy), (cx, cy + half), (cx - half, cy)]


def clip_x(points, x0, x1):
    """Sutherland-Hodgman: a convex polygon clipped to the vertical strip x0 <= x <= x1."""
    def clip(pts, keep, xc):
        out = []
        for i in range(len(pts)):
            p, q = pts[i], pts[(i + 1) % len(pts)]
            kp, kq = keep(p), keep(q)
            if kp:
                out.append(p)
            if kp != kq:
                out.append((xc, p[1] + (q[1] - p[1]) * (xc - p[0]) / (q[0] - p[0])))
        return out
    pts = clip(points, lambda p: p[0] >= x0, x0)
    return clip(pts, lambda p: p[0] <= x1, x1) if pts else []


# ---------------------------------------------------------------------------------------------------------------
# The kit definition table (UI_SPEC_GLASS 6.4 plus the plan's 0.4 P5 focus split). Coordinates are texture px.
# "spec" carries the 6.4 row (canvas, nine-slice margins, expand margins) the catalog and GlassKitContentTests
# compare against; "deviation" says why an entry differs from it or is not in it.
# ---------------------------------------------------------------------------------------------------------------

SPEC_PANEL = dict(size=(140, 140), margins=(42, 28, 42, 56), expand=(22, 8, 22, 36))
SPEC_TAG = dict(size=(40, 24), margins=(3, 0, 9, 0), expand=(0, 0, 0, 0))
SPEC_BTN = dict(size=(96, 70), margins=(2, 2, 32, 2), expand=(0, 0, 0, 0))
SPEC_BTN_FOCUS = dict(size=(124, 98), margins=(16, 16, 46, 16), expand=(14, 14, 14, 14))
SPEC_BTN_DISABLED = dict(size=(100, 70), margins=(2, 2, 32, 2), expand=(0, 0, 0, 0))
SPEC_TAB = dict(size=(64, 48), margins=(1, 0, 17, 0), expand=(0, 0, 0, 0))
SPEC_TAB_FOCUS = dict(size=(92, 76), margins=(15, 14, 31, 14), expand=(14, 14, 14, 14))
SPEC_FIELD = dict(size=(48, 44), margins=(10, 10, 10, 10), expand=(0, 0, 0, 0))
SPEC_FIELD_FOCUS = dict(size=(76, 72), margins=(24, 24, 24, 24), expand=(14, 14, 14, 14))

OVERLAY_NOTE = ("plan 0.4 P5: an OVERLAY, drawn by Godot over the state box (Button, OptionButton, LineEdit and "
                "TabBar draw `focus` / `tab_focus` after the state style); it carries only what focus adds, never "
                "the dark base, so a focused pressed row still reads as pressed. Spec 6.4 describes the full look.")

KIT = {
    "glass_panel": dict(
        file="glass_panel.png", spec=SPEC_PANEL, content=(28, 22, 28, 22),
        serves="G1 glass slab (PNL-A): every panel preset (InkPanel, DialogueGlassPanel, GlassPanel*), the "
               "default Panel and PanelContainer. Content margins are the paddings, one variation per preset (3.1).",
        notes="96 x 96 slab at (22, 8): chamfer 18 TL/BR; the 1.5 px edge with the 128-degree gradient "
              "(rgba(glow,.95) 0, .22 24 %, .10 58 %, .62 100 %); the build fill 160 deg rgba(15,24,52,.92) -> "
              "rgba(6,10,26,.95) over the edge layer, which shows through as in the CSS; the drop shadow (0, 14), "
              "Gaussian sigma 11, #00020C @0.60. Both gradients are laid out for the 620 x 214 Vitals panel through "
              "the nine-slice stretch (exact there, the CSS's trend elsewhere). The sheen and the two streak lines "
              "are NOT baked (GlassSheen draws them from the panel's own rect).",
        deviation="6.4 suggests placing the edge stops at the slice boundaries (the top-left corner patch .95 -> .22, "
                  "the bottom-right .10 -> .62). Measured over the 17 panel sizes the screens use, that leaves the "
                  "edge hairline a mean 0.189 alpha off the CSS (worst 0.271, the 1500 x 200 dialogue box; the Vitals "
                  "panel's left edge .44 where the CSS draws .78). Laying both gradients out for the 620 x 214 Vitals "
                  "panel cuts it to 0.066 (worst 0.144, the 680 x 769 pause panel). Canvas, slab, margins and "
                  "expand margins are as specified."),
    "glass_tag": dict(
        file="glass_tag.png", spec=SPEC_TAG, content=(14, 3, 14, 3),
        serves="G2 panel tag (PanelTagPlate; PulpTagPlate, PanelTagAnchor, PausePanel): text T07 (PanelTag).",
        notes="the 2 px #FFD45C bar, the 90-degree wash rgba(glow,.34) -> .08 (laid out for a 130 px tag, so it "
              "ends near .08 at the tag's right end), the 8 px right slant (the bottom edge is the shorter). "
              "Stretch horizontally only; the tag is always 24 px tall."),
    "glass_btn_normal": dict(
        file="glass_btn_normal.png", spec=SPEC_BTN,
        serves="G3 chevron button, normal (and hover / hover_pressed when the decor grabs focus on mouse-enter): "
               "TemporalGlassButton, PulpButton, PulpButtonCompact; one texture serves 58-70 px heights.",
        notes="edge-dim edge; fill rgba(8,13,32,.90) under the 100-degree wash rgba(glow,.10) -> 0 at 55 %. The "
              "washes and the focus edge are laid out for a 560 x 68 button (they follow the stretched width; a "
              "290 px compact button lands within 3 % of its own CSS stop)."),
    "glass_btn_selected": dict(
        file="glass_btn_selected.png", spec=SPEC_BTN,
        serves="G3 chevron button, selected / toggled (the current value in a list): edge `edge` @0.55, the "
               "normal fill. The decor swaps it in for `normal` (as the pulp decor swapped its paper plate)."),
    "glass_btn_pressed": dict(
        file="glass_btn_pressed.png", spec=SPEC_BTN,
        serves="G3 chevron button, pressed / hover_pressed. Built to sit UNDER glass_btn_focus: a pressed button "
               "is (almost always) focused, and pressed + the focus overlay equals the CSS focused-pressed fill "
               "(rgba(glow,.50) -> .16 over rgba(8,16,38,.92)) exactly; alone it reads as a dimmer pressed fill.",
        deviation="plan 0.4 P5: the interior is solved so that it composes with the focus overlay into the CSS "
                  "pressed look (spec 6.4 bakes the CSS pressed fill directly); the edge band keeps edge-dim."),
    "glass_btn_focus": dict(
        file="glass_btn_focus.png", spec=SPEC_BTN_FOCUS,
        serves="G3 chevron button, focus: the overlay Godot draws over the state box.",
        notes="the lit edge (100-degree #FFD45C -> rgba(glow,.55)), the focus wash (100-degree rgba(glow,.34) -> "
              ".07 at 75 %, no dark base) and the 14 px glow (0 0 14 rgba(glow,.55), Gaussian sigma 7) outside "
              "the chevron only. Over glass_btn_normal it lands within a few units of the CSS focus fill.",
        deviation=OVERLAY_NOTE),
    "glass_btn_hover": dict(
        file="glass_btn_hover.png", spec=SPEC_BTN_FOCUS,
        serves="G3 chevron button, the full CSS focus look as one box (edge, fill, glow): for a theme `hover` "
               "that cannot rely on the decor grabbing focus on mouse-enter. Hovered AND focused then draws the "
               "overlay on top of it (a brighter wash); prefer hover = glass_btn_normal plus focus-on-hover.",
        deviation="addition (not in 6.4): the overlay split of plan 0.4 P5 leaves no full focused box, and the "
                  "spec's hover = focus (AC8) needs one where hover does not imply focus."),
    "glass_btn_disabled": dict(
        file="glass_btn_disabled.png", spec=SPEC_BTN_DISABLED, stretch="tile",
        serves="G3 chevron button, disabled: the 1.5 px edge dashed (edge-dim, 6 on / 4 off from the left end, "
               "so the left end is solid), fill rgba(8,13,32,.50) over the averaged stripe haze rgba(glow,.15).",
        deviation="canvas 104 x 70, not 100 x 70: with margins 2 / 32 the centre slice is 70 px, a whole number of "
                  "10 px dash periods, so axis_stretch_horizontal TILE repeats it without a seam (100 x 70 left a "
                  "66 px centre). The CSS stripes run under the whole half-transparent fill, where the fill's "
                  "backdrop-filter blur(10px) averages them to a uniform haze; without blur they would show as hard "
                  "stripes across the label, so the interior bakes the averaged haze and only the ring is dashed. "
                  "Margins and the 30 px arrow are as specified."),
    "glass_tab": dict(
        file="glass_tab.png", spec=SPEC_TAB,
        serves="G14 tab, unselected (TabBar / TabContainer tab_unselected, tab_disabled): fill rgba(glow,.08), "
               "the 16 px slant on the right (the top edge is the shorter); text T55 (TabLabel)."),
    "glass_tab_selected": dict(
        file="glass_tab_selected.png", spec=SPEC_TAB,
        serves="G14 tab, selected: 100-degree #F3C95B -> rgba(glow,.55) (laid out for a 180 px tab); text "
               "#04141A (TabSelectedInk)."),
    "glass_tab_focus": dict(
        file="glass_tab_focus.png", spec=SPEC_TAB_FOCUS,
        serves="G14 tab focus (tab_focus): the 14 px gold glow (0 0 14 rgba(glow,.55)) outside the tab shape, "
               "over the selected tab Godot already drew.",
        deviation=OVERLAY_NOTE + " Spec 3.3: a focused tab is the selected look plus this glow."),
    "glass_tab_hover": dict(
        file="glass_tab_hover.png", spec=SPEC_TAB_FOCUS,
        serves="G14 tab, hovered (tab_hovered): the unselected tab lit, a fill of rgba(glow,.16) (TabHover) with the "
               "14 px glow outside the shape at .30; its text turns white. Quieter than the selected tab, so a hovered "
               "tab never reads as the selected one (TG-CLOSE).",
        deviation="addition (not in 6.4): tab_hovered replaces the tab's style, so it needs its own look. TG-ART drew "
                  "it as the selected look plus the glow, which read as selected (orchestrator: a quieter hover)."),
    "glass_field": dict(
        file="glass_field.png", spec=SPEC_FIELD,
        serves="glass field (3.3, plan 0.4 P8): the default Button, OptionButton, SpinBox and LineEdit field, the "
               "Controls tab's binding cells: chamfer 8 TL/BR, the 1.5 px edge-dim edge, fill rgba(8,13,32,.90)."),
    "glass_field_focus": dict(
        file="glass_field_focus.png", spec=SPEC_FIELD_FOCUS,
        serves="glass field, focus: the lit edge (100-degree #FFD45C -> rgba(glow,.55), laid out for a 300 px "
               "field) and the 14 px glow outside, drawn over glass_field.",
        deviation=OVERLAY_NOTE),
    "glass_field_hover": dict(
        file="glass_field_hover.png", spec=SPEC_FIELD_FOCUS,
        serves="glass field, the full CSS focus look as one box (OptionButton hover; AC8).",
        deviation="addition (not in 6.4): as glass_btn_hover, for a hover style that replaces the field's style."),
    "glass_slider_grabber": dict(
        file="glass_slider_grabber.png", size=(44, 44),
        serves="HSlider / VSlider grabber and grabber_highlight icon (3.3): the 31.7 px white diamond (a 28 px "
               "square rotated 45 degrees and scaled .8), its accent ring (3 px before the .8 scale, 2.4 px "
               "drawn) and the 16 px accent glow (sigma 6.4 drawn), centred. Not tinted.",
        deviation="canvas 44 x 44 (TG-ART drew 64 x 64): a Slider is as tall as its grabber icon, so the 64 px canvas "
                  "made every Settings slider row 64 tall; at 44 the glow is clipped to the canvas, fading over its "
                  "last 3 px, and a slider stands in a 44 px field like the option buttons (TG-CLOSE)."),
    "glass_option_arrow": dict(
        file="icons/chevron_down_18.png", size=(18, 18),
        serves="OptionButton arrow (3.3): chevron_down in the bold weight rasterized at its 18 px display size (an "
               "OptionButton draws its arrow at the texture's own size), its generic glow baked (sigma 2.5 x 18 / 64). "
               "Baked in ink2 / ink, so the theme's modulate_arrow, which tints it by the field's font colour, lands it "
               "on ink2 at rest (7.2), brighter when the field is lit and dimmer when disabled."),
    "glass_spin_up": dict(
        file="icons/chevron_up_18.png", size=(18, 18),
        serves="SpinBox up arrow (3.3, 6.2: a spin box's arrows are the chevron_down icon; TG-CLOSE polish): the "
               "OptionButton arrow above flipped vertically, the same bake, so the theme's SpinBox icon modulates "
               "(the field's font colours) land it on ink2 at rest, brighter when lit and dimmer when disabled. "
               "The down arrow is glass_option_arrow itself."),
    "glass_diamond_11": dict(
        file="glass_diamond_11.png", size=(48, 48),
        serves="flourish diamonds (3.7 select title, 3.8 menu title, GlassFlourish): an 11 px diamond with the "
               "menu's 14 px glow (sigma 7), white, tinted by modulate (#F3C95B). The select title's 12 px glow "
               "reuses it (the spec's two-texture choice)."),
    "glass_diamond_12": dict(
        file="glass_diamond_12.png", size=(48, 48),
        serves="the boss name diamond (3.6, 12 px, glow 12, tint #FF5A5A) and the phase-notch diamond (glow 10, "
               "tint #F3C95B): a 12 px diamond with a 12 px glow (sigma 6), white, tinted by modulate."),
}

for _stem, _entry in KIT.items():
    if "spec" in _entry:
        _entry["size"] = _entry["spec"]["size"]
_DISABLED_SIZE = (104, 70)
KIT["glass_btn_disabled"]["size"] = _DISABLED_SIZE

# ---------------------------------------------------------------------------------------------------------------
# Icons (UI_SPEC_GLASS 3.5, 7.2, Appendix A). One white 64 x 64 texture per glyph and weight with its glow baked.
# glow: (kind, sigma_regular, sigma_bold, strength, rgb) in 64 px bake pixels; rgb None = white (the tint colours
# it); a coloured glow is stored divided by the glyph's intended tint, so modulate restores the spec colour.
# ---------------------------------------------------------------------------------------------------------------

def _pre(rgb, tint):
    return tuple(min(255.0, rgb[i] * 255.0 / tint[i]) for i in range(3))


GENERIC = ("generic", 2.5, 2.5, 0.60, None)   # drop-shadow(0 0 3px currentColor @60%), sigma 2.5 bake px (7.2)
NO_GLOW = ("none", 0, 0, 0, None)
SHIELD_TINT = (223, 232, 255)                 # `shield` #DFE8FF
RES2 = (255, 240, 176)                        # `res2` #FFF0B0
COLD = (56, 242, 255)                         # `cold` #38F2FF (UIPalette.UnboundCold #33F2FF)


def _sig(blur_px, display_px):
    """CSS blur radius B at a display size -> Gaussian sigma in 64 px bake pixels (sigma = B / 2)."""
    return round(blur_px / 2.0 * 64.0 / display_px, 2)


ICON_TABLE = {
    # name: (serves, tint, shown at, glow)
    "dilation": ("Time Dilation status glyph (control)", "#8FC7FF", "27.3 Story / 24.8 Fighter badge (bold)", GENERIC),
    "venom": ("Venom status glyph (damage)", "#FF7A5C", "27.3 / 24.8 (bold)", GENERIC),
    "static": ("Static Charge status glyph (control)", "#8FC7FF", "27.3 / 24.8 (bold)", GENERIC),
    "burn": ("Radiant Burn status glyph (damage)", "#FF7A5C", "27.3 / 24.8 (bold)", GENERIC),
    "root": ("Root status glyph (control)", "#8FC7FF", "27.3 / 24.8 (bold)", GENERIC),
    "suppress": ("Suppression status glyph (the Severed)", "#38F2FF cold", "27.3 / 24.8 (bold)", GENERIC),
    "shield": ("block pip, on", "#DFE8FF", "30 Story (regular) / 24 Fighter (bold)",
               ("pip", _sig(4, 30), _sig(4, 24), 0.60, _pre((143, 216, 255), SHIELD_TINT))),
    "shield_off": ("block pip, off: the shield glyph with no glow (CSS .c-pips .pip.off .ic{filter:none}); draw "
                   "at opacity .28 and grayscale", "#DFE8FF", "30 / 24", NO_GLOW),
    "hourglass": ("rewind counter", "#FFE39A (accent2); #FF5A5A at 1 or 0", "28.5 in a 46 badge (bold)", GENERIC),
    "freeze": ("Time Freeze", "#FFE39A (accent2)", "28.5 in a 46 badge (bold)", GENERIC),
    "dust": ("Chronal Dust", "#F3C95B (res)", "38 (regular)", GENERIC),
    "star4": ("Fighter stock, off (at opacity .22)", "the slot colour", "28 (bold)", GENERIC),
    "star4_filled": ("Fighter stock, on", "the slot colour", "28 (bold)", GENERIC),
    "flag": ("objective flag", "#FFF0B0 (res2)", "22 (bold)", GENERIC),
    "lock_dormant": ("slot-lock overlay, dormant Legacy slot (flat, no glow)", "#8A8374 (ink3) at opacity .8",
                     "68 % of the lens: 50.3 / 62.6 / 36.7", NO_GLOW),
    "lock_suppressed": ("slot-lock overlay, suppressed (the cold cross-out, 6 px cold glow)", "#38F2FF cold",
                        "68 % of the lens", ("cold", _sig(6, 50.3), _sig(6, 50.3), 1.0, None)),
    "dormant": ("plain dormant glyph (defined, unused on the six screens; an ability slot uses lock_dormant)",
                "#8A8374", "-", GENERIC),
    "cross": ("plain cross glyph (defined, unused on the six screens; an ability slot uses lock_suppressed)",
              "#38F2FF cold", "-", GENERIC),
    "defy_ready": ("Defy seal, ready (ring, core, four rays)", "#FFF0B0 (res2)", "34 Story / 32 Fighter",
                   ("defy", _sig(7, 34), _sig(7, 28), 0.75, _pre((255, 232, 122), RES2))),
    "defy_notready": ("Defy seal, not ready (hollow ring)", "#8A8374 (ink3)", "34 / 32", GENERIC),
    "defy_spent": ("Defy seal, spent (broken ring)", "#8A8374 (ink3)", "34 / 32", GENERIC),
    "defy_barred": ("Defy seal, unavailable (ring and slash)", "#FF5A5A (danger)", "34 / 32", GENERIC),
    "slot_p1": ("player 1 slot shape (triangle; 20-unit box filling the texture)", "UIPalette.P1",
                "22 in a 30 box (28 on select tiles)", NO_GLOW),
    "slot_p2": ("player 2 slot shape (circle; 20-unit box filling the texture)", "UIPalette.P2",
                "22 in a 30 box", NO_GLOW),
    "check_solid": ("select tile lock mark (solid check)", "#04141A on the slot-colour square", "20", NO_GLOW),
    "hourglass_caret": ("dialogue advance caret (solid hourglass; its 1-unit black stroke never tints)",
                        "#FFF0B0 (res2)", "34", NO_GLOW),
    "beacon_anchor": ("Act III Beacon anchor (agent-authored, AC28)", "#FFE39A (accent2)",
                      "28.5 in a 46 badge (bold)", GENERIC),
    "beacon_anchor_hollow": ("Beacon anchor at zero charges: the diamond outline only (agent-authored, plan "
                             "0.4 P4)", "the steady last-stand tint", "28.5 (bold)", GENERIC),
    "chevron_down": ("option-button and spin-box arrow, scroll hints (agent-authored, AC28)", "#C0BAA6 (ink2)",
                     "18 (bold)", GENERIC),
    "gear": ("defined, unused on the six screens", "-", "-", GENERIC),
    "bolt": ("defined, unused on the six screens", "-", "-", GENERIC),
    "check": ("defined, unused on the six screens", "-", "-", GENERIC),
    "clock": ("defined, unused on the six screens", "-", "-", GENERIC),
    "spark": ("defined, unused on the six screens", "-", "-", GENERIC),
}

# Glyphs the spec draws in one weight. Their bold texture is derived: strokes x1.5 (F 1.5 -> 2.25, D 1.35 -> 2.0,
# the Defy 1.6 -> 2.4, the slot-lock cross 2.6 -> 3.9); a solid glyph's bold is its regular art.
SINGLE_WEIGHT = ("check_solid", "cross", "defy_barred", "defy_notready", "defy_ready", "defy_spent", "dormant",
                 "hourglass_caret", "lock_dormant", "lock_suppressed", "slot_p1", "slot_p2")
BOLD_STROKE = {"1.5": "2.25", "1.35": "2.0", "1.6": "2.4", "2.6": "3.9"}
ICON_SOURCE_OF = {"shield_off": "shield"}   # a texture name drawn from another glyph's source


def icon_names():
    return sorted(ICON_TABLE)


def icon_files(name):
    """(regular, bold) normalized SVG stems under svg/icons/ that a name's two textures render from."""
    src = ICON_SOURCE_OF.get(name, name)
    return src, src + "_bold"


# ---------------------------------------------------------------------------------------------------------------
# Gradient resources (UI_SPEC_GLASS 7.3), written by build_glass_images.py as GradientTexture2D .tres.
# Radials: fill_from = the CSS centre, fill_to = centre + s x (farthest corner - centre) (the CSS reference
# radius times the stop position, so the gradient's 0..1 is the CSS 0..s).
# ---------------------------------------------------------------------------------------------------------------

def _radial_to(cx, cy, s):
    fx = 1.0 if cx <= 0.5 else 0.0
    fy = 1.0 if cy <= 0.5 else 0.0
    return (round(cx + s * (fx - cx), 5), round(cy + s * (fy - cy), 5))


GRADIENTS = {
    "glass_lens_radial": dict(
        file="glass_lens_radial.tres", fill=1, size=(128, 128), frm=(0.32, 0.24), to=_radial_to(0.32, 0.24, 0.68),
        stops=[(0.0, g(0.30)), (1.0, (6, 10, 26, 0.92))],
        serves="G6 lens fill (3.4.2): radial at 32 % 24 %, radius 0.693 of the size: rgba(glow,.30) -> "
               "rgba(6,10,26,.92). Map a circle polygon's UVs over the lens square."),
    "glass_badge_radial": dict(
        file="glass_badge_radial.tres", fill=1, size=(128, 128), frm=(0.32, 0.24), to=_radial_to(0.32, 0.24, 0.62),
        stops=[(0.0, g(0.28)), (1.0, (8, 12, 30, 0.86))],
        serves="G7 badge fill (status, rewind, Time Freeze, Beacon): radial at 32 % 24 %, radius 0.632: "
               "rgba(glow,.28) -> rgba(8,12,30,.86)."),
    "glass_dial_radial": dict(
        file="glass_dial_radial.tres", fill=1, size=(128, 128), frm=(0.34, 0.26), to=_radial_to(0.34, 0.26, 0.70),
        stops=[(0.0, g(0.22)), (1.0, (5, 9, 24, 0.90))],
        serves="G9 Integrity dial face: radial at 34 % 26 %, radius 0.694: rgba(glow,.22) -> rgba(5,9,24,.90)."),
    "glass_portrait_radial": dict(
        file="glass_portrait_radial.tres", fill=1, size=(128, 128), frm=(0.5, 0.3), to=_radial_to(0.5, 0.3, 0.78),
        stops=[(0.0, g(0.38)), (1.0, (6, 10, 26, 0.95))],
        serves="G5 portrait frame's inner fill: radial at 50 % 30 %, radius 0.671: rgba(glow,.38) -> "
               "rgba(6,10,26,.95)."),
    "glass_menu_scrim": dict(
        file="menu_scrim.tres", fill=0, size=(512, 4), frm=(0.0, 0.5), to=(1.0, 0.5),
        stops=[(0.0, (4, 7, 20, 0.96)), (0.26, (4, 7, 20, 0.84)), (0.48, (4, 7, 20, 0.35)), (0.66, (4, 7, 20, 0.0))],
        serves="MENU-A menu scrim (1.3): 90 degrees, rgba(4,7,20,.96) 0, .84 26 %, .35 48 %, 0 66 %. A full-screen "
               "TextureRect, stretch mode Scale; FlipH for the mirrored scrim."),
    "glass_select_vignette": dict(
        file="select_vignette.tres", fill=1, size=(256, 168), frm=(0.5, 0.45714),
        to=(round(0.5 + 0.6 / math.sqrt(2), 5), round(0.45714 + 0.6 / math.sqrt(2), 5)),
        stops=[(0.0, (3, 5, 16, 0.55)), (0.8, (3, 5, 16, 0.0))],
        serves="CARD-A select pool (1.3): radial-gradient(60% 70% at 50% 45%, rgba(3,5,16,.55), transparent "
               "80%). Draw it as a full-screen TextureRect with stretch mode KeepAspectCovered: the texture's "
               "256:168 aspect is the CSS ellipse's 1152:756, so its UV circle lands as that ellipse centred at "
               "(960, 486). (Stretched with Scale, the x extent stays exact and the y extent is 14 % short.)"),
    "glass_fighter_top_shade": dict(
        file="fighter_top_shade.tres", fill=0, size=(4, 256), frm=(0.5, 0.0), to=(0.5, 1.0),
        stops=[(0.0, (6, 4, 12, 0.80)), (0.42, (6, 4, 12, 0.64)), (0.78, (6, 4, 12, 0.30)), (1.0, (6, 4, 12, 0.0))],
        serves="HUD-F2 Fighter top shade (1.3): 180 degrees over 1920 x 300: rgba(6,4,12,.80) 0, .64 42 %, "
               ".30 78 %, 0 100 %. A TextureRect 1920 x 300 at the top, stretch mode Scale."),
}

# ---------------------------------------------------------------------------------------------------------------
# Raster sources
# ---------------------------------------------------------------------------------------------------------------
RAW = []   # (name, svg text, (w, h)) -> svg/<name>.svg rendered to raw/<name>.png at 4x


def raw(name, w, h, body, defs=""):
    RAW.append((name, svg_doc(w, h, body, defs), (w, h)))


# The canvases the PIL step composites on (texture px). Buttons, tabs and fields are built on their focus canvas
# (the 14 px glow pad) and cropped for the state textures.
PANEL_CANVAS, SLAB, PANEL_N = (140, 140), (22, 8, 96, 96), 18
BTN_CANVAS, BTN_BOX = (124, 98), (14, 14, 96, 70)
BTN_DIS_CANVAS = _DISABLED_SIZE
TAB_CANVAS, TAB_BOX = (92, 76), (14, 14, 64, 48)
FIELD_CANVAS, FIELD_BOX, FIELD_N = (76, 72), (14, 14, 48, 44), 8
GRAB_CANVAS = (44, 44)   # TG-CLOSE: was 64 (a Slider stands as tall as its grabber)
DIAMOND_CANVAS = (48, 48)

# Reference sizes the stretched washes are laid out for (UI_SPEC_GLASS 3.2-3.3 sizes as drawn).
BTN_REF = (560.0, 68.0)
TAB_REF = (180.0, 48.0)
TAG_REF = (130.0, 24.0)
FIELD_REF = (300.0, 44.0)


def _warp(box, margins_lr, tex_w, ref_w, tex_h=None, ref_h=None):
    """Texture -> reference map for a nine-slice whose centre column stretches: a point x in the box's centre
    slice maps to where it lands on a ref_w-wide element; y is scaled to the reference height."""
    bx0, by0, bw, bh = box
    L, R = margins_lr
    ax = (ref_w - L - R) / (tex_w - L - R)
    bx = L - (bx0 + L) * ax
    if tex_h is None:
        return ax, bx, 1.0, -by0
    ay = ref_h / tex_h
    return ax, bx, ay, -by0 * ay


def clip_y(points, y0, y1):
    """clip_x with the axes swapped."""
    swapped = clip_x([(py, px) for px, py in points], y0, y1)
    return [(px, py) for py, px in swapped]


PANEL_SLICE = 20            # the slice boundaries sit 20 px inside the slab (margins 42/28/42/56 - expand 22/8/22/36)
PANEL_REF = (620.0, 214.0)  # the Story HUD's Vitals panel: the gradients are laid out for it (see KIT notes)


def build_panel():
    """The slab's edge (128 deg) and fill (160 deg) gradients follow each panel's own box in the CSS, which one
    nine-slice cannot. They are laid out here for the reference panel PANEL_REF through the nine-slice's own
    stretch: each of the 9 slice cells maps affinely onto the reference (corners 1:1, strips scaled), so a
    stretched slab shows exactly the CSS on a 620 x 214 panel and the CSS's trend on other sizes. Over the 17
    panel sizes the screens use, the edge hairline's mean alpha error is 0.066 (worst 0.144, the 680 x 769 pause
    panel), against 0.189 (worst 0.271) for 6.4's stops-at-the-slice-boundaries placement. The cell boundaries
    fall on whole pixels, so the per-cell paths leave no seam."""
    x, y, w, h = SLAB
    n = PANEL_SLICE
    wr, hr = PANEL_REF
    xs, ys = [x, x + n, x + w - n, x + w], [y, y + n, y + h - n, y + h]

    def axis(lo, size, ref, cell):
        if cell == 0:
            return 1.0, -lo
        if cell == 2:
            return 1.0, ref - size - lo
        a = (ref - 2 * n) / (size - 2 * n)
        return a, n - (lo + n) * a

    outer, inner = chamfer(x, y, w, h, PANEL_N), chamfer_fill(x, y, w, h, PANEL_N)
    defs, edges, fills = "", [], []
    for j in range(3):
        for i in range(3):
            ax, bx = axis(x, w, wr, i)
            ay, by = axis(y, h, hr, j)
            warp = (ax, bx, ay, by)
            k = f"{i}{j}"
            defs += lingrad("pe" + k, grad_endpoints(128, (0.0, 0.0, wr, hr), warp),
                            [(0.0, g(0.95)), (0.24, g(0.22)), (0.58, g(0.10)), (1.0, g(0.62))])
            defs += lingrad("pf" + k, grad_endpoints(160, (1.5, 1.5, wr - 3, hr - 3), warp),
                            [(0.0, PANEL_FILL_TOP), (1.0, PANEL_FILL_BOTTOM)])
            cell_o = clip_y(clip_x(outer, xs[i], xs[i + 1]), ys[j], ys[j + 1])
            cell_i = clip_y(clip_x(inner, xs[i], xs[i + 1]), ys[j], ys[j + 1])
            if len(cell_o) >= 3:
                edges.append(path(poly_d(cell_o), f"url(#pe{k})"))
            if len(cell_i) >= 3:
                fills.append(path(poly_d(cell_i), f"url(#pf{k})"))
    # the edge layer is the full box under the fill (0.3): every edge cell before any fill cell
    raw("panel_slab", *PANEL_CANVAS, "".join(edges) + "".join(fills), defs)


def build_tag():
    W, H = 40, 24
    warp = _warp((0, 0, W, H), (3, 9), W, TAG_REF[0])
    grad = lingrad("tg", grad_endpoints(90, (0, 0) + TAG_REF, warp), [(0.0, g(0.34)), (1.0, g(0.08))])
    body = path(poly_d([(0, 0), (W, 0), (W - 8, H), (0, H)]), "url(#tg)") + \
        path(poly_d([(0, 0), (2, 0), (2, H), (0, H)]), solid(EDGE_HI))
    raw("tag", W, H, body, grad)


def build_buttons():
    x, y, w, h = BTN_BOX
    outer, inner = chevron(x, y, w, h), chevron_fill(x, y, w, h)
    warp = _warp(BTN_BOX, (2, 32), w, BTN_REF[0], h, BTN_REF[1])
    ref = (0.0, 0.0) + BTN_REF
    ref_in = (1.5, 1.5, BTN_REF[0] - 3, BTN_REF[1] - 3)

    def edge_grad(gid):
        return lingrad(gid, grad_endpoints(100, ref, warp), [(0.0, FOCUS), (1.0, g(0.55))])

    def wash(gid, stops):
        return lingrad(gid, grad_endpoints(100, ref_in, warp), stops)

    W_NORMAL = [(0.0, g(0.10)), (0.55, g(0.0))]
    W_FOCUS = [(0.0, g(0.34)), (0.75, g(0.07))]
    W_PRESS = [(0.0, g(0.50)), (1.0, g(0.16))]
    O, I = poly_d(outer), poly_d(inner)

    raw("btn_sil", *BTN_CANVAS, path(O, "#FFFFFF"))
    raw("btn_inner", *BTN_CANVAS, path(I, "#FFFFFF"))
    raw("btn_normal", *BTN_CANVAS,
        path(O, solid(EDGE_DIM)) + path(I, solid(BUTTON_FILL)) + path(I, "url(#w)"), wash("w", W_NORMAL))
    raw("btn_selected", *BTN_CANVAS,
        path(O, solid(EDGE)) + path(I, solid(BUTTON_FILL)) + path(I, "url(#w)"), wash("w", W_NORMAL))
    raw("btn_focus_css", *BTN_CANVAS,
        path(O, "url(#e)") + path(I, solid(BUTTON_FILL_FOCUS)) + path(I, "url(#w)"),
        edge_grad("e") + wash("w", W_FOCUS))
    raw("btn_pressed_css", *BTN_CANVAS,
        path(O, solid(EDGE_DIM)) + path(I, solid(BUTTON_FILL_FOCUS)) + path(I, "url(#w)"), wash("w", W_PRESS))
    raw("btn_pressed_focus_css", *BTN_CANVAS,
        path(O, "url(#e)") + path(I, solid(BUTTON_FILL_FOCUS)) + path(I, "url(#w)"),
        edge_grad("e") + wash("w", W_PRESS))
    raw("btn_overlay_parts", *BTN_CANVAS,
        path(O + " " + I, "url(#e)", evenodd=True) + path(I, "url(#w)"), edge_grad("e") + wash("w", W_FOCUS))

    # disabled: its own canvas (no glow). The CSS edge layer is vertical stripes (edge-dim 6 on / 4 off) under the
    # half-transparent fill, but the fill's backdrop-filter blur(10px) averages the stripes under it to a uniform
    # haze (a 10 px period under sigma 5 keeps 0.7 % of its modulation), so only the 1.5 px ring shows dashes.
    # Built as intended: dashes in the ring, the averaged haze (0.6 duty x .25 = rgba(glow,.15)) under the fill.
    dw, dh = BTN_DIS_CANVAS
    d_outer, d_inner = chevron(0, 0, dw, dh), chevron_fill(0, 0, dw, dh)
    dashes = ""
    for k in range(int(math.ceil(dw / 10.0))):
        x0, x1 = 10 * k, min(10 * k + 6, dw)
        band, hole = clip_x(d_outer, x0, x1), clip_x(d_inner, x0, x1)
        if len(band) >= 3:
            d = poly_d(band) + (" " + poly_d(hole) if len(hole) >= 3 else "")
            dashes += path(d, solid(EDGE_DIM), evenodd=True)
    haze = g(EDGE_DIM[3] * 0.6)
    raw("btn_disabled", dw, dh, dashes + path(poly_d(d_inner), solid(haze)) +
        path(poly_d(d_inner), solid(BUTTON_FILL_DISABLED)))


def build_tabs():
    x, y, w, h = TAB_BOX
    shape = poly_d([(x, y), (x + w - 16, y), (x + w, y + h), (x, y + h)])
    warp = _warp(TAB_BOX, (1, 17), w, TAB_REF[0])
    sel = lingrad("s", grad_endpoints(100, (0.0, 0.0) + TAB_REF, warp), [(0.0, ACCENT), (1.0, g(0.55))])
    raw("tab_sil", *TAB_CANVAS, path(shape, "#FFFFFF"))
    raw("tab_normal", *TAB_CANVAS, path(shape, solid(g(0.08))))
    raw("tab_selected", *TAB_CANVAS, path(shape, "url(#s)"), sel)
    raw("tab_hover_fill", *TAB_CANVAS, path(shape, solid(g(0.16))))   # TG-CLOSE: the quieter hover (TabHover)


def build_fields():
    x, y, w, h = FIELD_BOX
    O, I = poly_d(chamfer(x, y, w, h, FIELD_N)), poly_d(chamfer_fill(x, y, w, h, FIELD_N))
    warp = _warp(FIELD_BOX, (10, 10), w, FIELD_REF[0])
    edge = lingrad("e", grad_endpoints(100, (0.0, 0.0) + FIELD_REF, warp), [(0.0, FOCUS), (1.0, g(0.55))])
    raw("field_sil", *FIELD_CANVAS, path(O, "#FFFFFF"))
    raw("field_normal", *FIELD_CANVAS, path(O, solid(EDGE_DIM)) + path(I, solid(FIELD_FILL)))
    raw("field_focus_css", *FIELD_CANVAS, path(O, "url(#e)") + path(I, solid(FIELD_FILL)), edge)
    raw("field_ring_focus", *FIELD_CANVAS, path(O + " " + I, "url(#e)", evenodd=True), edge)


def build_grabber():
    cx, cy = GRAB_CANVAS[0] / 2.0, GRAB_CANVAS[1] / 2.0
    core = 28 * 0.8 / math.sqrt(2)            # the 28 px square scaled .8: half-diagonal 15.84 (31.7 px across)
    ring = (28 + 2 * 3) * 0.8 / math.sqrt(2)  # its 3 px box-shadow ring, scaled .8: half-diagonal 19.23
    raw("grabber_box", *GRAB_CANVAS, path(poly_d(diamond(cx, cy, core)), "#FFFFFF"))
    raw("grabber_art", *GRAB_CANVAS,
        path(poly_d(diamond(cx, cy, ring)) + " " + poly_d(diamond(cx, cy, core)), solid(ACCENT), evenodd=True) +
        path(poly_d(diamond(cx, cy, core)), "#FFFFFF"))


def build_diamonds():
    cx, cy = DIAMOND_CANVAS[0] / 2.0, DIAMOND_CANVAS[1] / 2.0
    for side in (11, 12):
        raw(f"diamond_{side}", *DIAMOND_CANVAS, path(poly_d(diamond(cx, cy, side / math.sqrt(2))), "#FFFFFF"))


# ---------------------------------------------------------------------------------------------------------------
# Icons: normalized 4x copies and the derived bold weights
# ---------------------------------------------------------------------------------------------------------------
_SIZE_ATTR = re.compile(r'(<svg\b[^>]*?)\swidth="[^"]*"\s+height="[^"]*"')


def normalize_icon(text: str) -> str:
    """The same SVG declared 256 x 256 (4 x 64); the viewBox, so the art, is unchanged."""
    out, n = _SIZE_ATTR.subn(r'\1 width="256" height="256"', text, count=1)
    if n != 1:
        raise ValueError("icon SVG without width/height on its root")
    return out


def derive_bold(name: str, text: str) -> str:
    def widen(m):
        return f'stroke-width="{BOLD_STROKE.get(m.group(1), m.group(1))}"'
    body = re.sub(r'stroke-width="([0-9.]+)"', widen, text)
    note = (f"<!-- DERIVED by tools/asset_src/glass/glass_svg.py (TG-ART): the BOLD weight of '{name}', which the "
            f"spec draws in one weight; strokes x1.5 (1.5->2.25, 1.35->2.0, 1.6->2.4, 2.6->3.9), a solid glyph "
            f"unchanged. Use below 30 px (UI_SPEC_GLASS 3.5). -->")
    body = re.sub(r"<!--.*?-->", note, body, count=1, flags=re.S)
    return body


def build_icons():
    out = []   # (stem, text)
    sources = sorted(fn[:-4] for fn in os.listdir(ICON_SRC) if fn.endswith(".svg"))
    for stem in sources:
        text = open(os.path.join(ICON_SRC, stem + ".svg"), encoding="utf-8").read()
        out.append((stem, normalize_icon(text)))
        if stem in SINGLE_WEIGHT:
            out.append((stem + "_bold", normalize_icon(derive_bold(stem, text))))
    have = {s for s, _ in out}
    for name in icon_names():
        for stem in icon_files(name):
            if stem not in have:
                raise SystemExit(f"icon '{name}' has no source for {stem}")
    return out


# ---------------------------------------------------------------------------------------------------------------
# jobs.json: replace the glass groups, keep every other byte
# ---------------------------------------------------------------------------------------------------------------

def write_jobs(new_jobs):
    text = open(JOBS, encoding="utf-8", newline="").read()
    nl = "\r\n" if "\r\n" in text else "\n"
    t = text.replace("\r\n", "\n")
    dec = json.JSONDecoder()
    start = t.index("[", t.index('"jobs"'))
    spans = []
    j = start + 1
    while True:
        while t[j] in " \n\t,":
            j += 1
        if t[j] == "]":
            close = j
            break
        obj, end = dec.raw_decode(t, j)
        spans.append((j, end, obj))
        j = end
    kept = [t[s:e] for s, e, o in spans if o.get("group") not in JOB_GROUPS]
    added = [json.dumps(job, indent=2).replace("\n", "\n    ") for job in new_jobs]
    body = ",\n".join("    " + block for block in kept + added)
    out = t[:start + 1] + "\n" + body + "\n  " + t[close:]
    json.loads(out)
    with open(JOBS, "w", encoding="utf-8", newline="") as fh:
        fh.write(out.replace("\n", nl))
    return len(kept), len(added)


def main():
    for fn in (build_panel, build_tag, build_buttons, build_tabs, build_fields, build_grabber, build_diamonds):
        fn()
    icon_dir = os.path.join(SVG_DIR, "icons")
    os.makedirs(icon_dir, exist_ok=True)
    jobs = []
    for name, text, (w, h) in RAW:
        with open(os.path.join(SVG_DIR, name + ".svg"), "w", encoding="utf-8", newline="\n") as fh:
            fh.write(text)
        jobs.append({"type": "svg", "group": "glass", "src": SVG_RES + name + ".svg",
                     "out": RAW_RES + name + ".png", "width": w * SS, "height": h * SS, "supersample": 1})
    for stem, text in build_icons():
        with open(os.path.join(icon_dir, stem + ".svg"), "w", encoding="utf-8", newline="\n") as fh:
            fh.write(text)
        jobs.append({"type": "svg", "group": "glass_icons_raw", "src": SVG_RES + "icons/" + stem + ".svg",
                     "out": RAW_RES + "icons/" + stem + ".png", "width": 64 * SS, "height": 64 * SS,
                     "supersample": 1})
    # TG-CLOSE: the OptionButton arrow, chevron_down's bold weight rendered at its 18 px display size (x4).
    jobs.append({"type": "svg", "group": "glass_icons_raw", "src": SVG_RES + "icons/chevron_down_bold.svg",
                 "out": RAW_RES + "icons/chevron_down_bold_18.png", "width": 18 * SS, "height": 18 * SS,
                 "supersample": 1})
    kept, added = write_jobs(jobs)
    print(f"svgs: {len(RAW)} kit layers + {added - len(RAW)} icons; jobs.json: kept {kept}, appended {added}")


if __name__ == "__main__":
    main()
