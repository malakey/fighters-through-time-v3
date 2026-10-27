"""TG-ART: builds the Temporal Glass kit's PNGs and gradient resources from the 4x rasters.

Reads tools/asset_src/glass/raw/ (written by tools/generate_assets.gd from glass_svg.py's sources), downsamples
4:1 in premultiplied space (exact area coverage: a hairline keeps its colour, no dark fringe), and composites what
ThorVG cannot draw (UI_SPEC_GLASS 6.3): the panel's soft drop shadow, the 14 px focus glows, the slider grabber
and diamond glows, and every icon's glow (7.2). It solves the pressed button box against the focus overlay
(plan 0.4 P5), writes assets/sprites/ui/glass/*.png and icons/*.png, and writes the GradientTexture2D resources of
7.3. CSS blur radius B is a Gaussian of sigma B / 2. Deterministic: the same rasters give the same bytes.

usage (repo root): py tools/asset_src/glass/build_glass_images.py
"""
from __future__ import annotations

import math
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import glass_svg as G  # noqa: E402

RAW = os.path.join(HERE, "raw")
OUT = os.path.join(G.REPO, "assets", "sprites", "ui", "glass")
WHITE = (255, 255, 255)
FOCUS_GLOW_SIGMA = 7.0     # 0 0 14px rgba(glow,.55)
FOCUS_GLOW_ALPHA = 0.55
TAB_HOVER_GLOW_ALPHA = 0.30   # TG-CLOSE: the hovered tab's glow, quieter than the focus glow


# ---- raster helpers --------------------------------------------------------------------------------------------

def load(name: str) -> np.ndarray:
    """A raw 4x raster as a premultiplied float array (H, W, 4) at 1x."""
    im = Image.open(os.path.join(RAW, name + ".png")).convert("RGBA")
    a = np.asarray(im, dtype=np.float64) / 255.0
    pm = np.concatenate([a[..., :3] * a[..., 3:4], a[..., 3:4]], axis=-1)
    h, w = pm.shape[:2]
    return pm.reshape(h // G.SS, G.SS, w // G.SS, G.SS, 4).mean(axis=(1, 3))


def over(top: np.ndarray, bottom: np.ndarray) -> np.ndarray:
    return top + bottom * (1.0 - top[..., 3:4])


def flat(alpha: np.ndarray, rgb) -> np.ndarray:
    c = np.asarray(rgb, dtype=np.float64)[:3] / 255.0
    return np.concatenate([alpha[..., None] * c, alpha[..., None]], axis=-1)


def gauss(sigma: float) -> np.ndarray:
    r = int(math.ceil(4.0 * sigma))
    x = np.arange(-r, r + 1, dtype=np.float64)
    k = np.exp(-x * x / (2.0 * sigma * sigma))
    return k / k.sum()


def blur(a: np.ndarray, sigma: float) -> np.ndarray:
    """A true separable Gaussian with zero padding (not PIL's box approximation)."""
    if sigma <= 0:
        return a.copy()
    k = gauss(sigma)
    r = (len(k) - 1) // 2
    p = np.pad(a, ((0, 0), (r, r)))
    a1 = np.lib.stride_tricks.sliding_window_view(p, len(k), axis=1) @ k
    p = np.pad(a1, ((r, r), (0, 0)))
    return np.lib.stride_tricks.sliding_window_view(p, len(k), axis=0) @ k


def window(shape, k: float = 3.0) -> np.ndarray:
    """1 inside, ramping to ~0 over the outer k px: a glow or shadow tail clipped by the canvas fades out
    instead of ending on a hard line."""
    h, w = shape
    y = np.minimum(np.arange(h) + 0.5, h - np.arange(h) - 0.5)
    x = np.minimum(np.arange(w) + 0.5, w - np.arange(w) - 0.5)
    return np.clip(np.minimum(y[:, None], x[None, :]) / k, 0.0, 1.0)


def glow_of(sil: np.ndarray, sigma: float, strength: float) -> np.ndarray:
    return strength * blur(sil, sigma) * window(sil.shape)


def un_over(target: np.ndarray, top: np.ndarray) -> np.ndarray:
    """X such that `top` over X equals `target` (premultiplied), clamped to the valid range."""
    oa = top[..., 3:4]
    denom = np.maximum(1.0 - oa, 1e-9)
    xa = np.clip((target[..., 3:4] - oa) / denom, 0.0, 1.0)
    xrgb = np.clip((target[..., :3] - top[..., :3]) / denom, 0.0, None)
    return np.concatenate([np.minimum(xrgb, xa), xa], axis=-1)


WRITTEN = []


def save(pm: np.ndarray, rel: str, bg=WHITE) -> None:
    """Writes straight-alpha RGBA; a fully transparent pixel takes `bg`, the colour around it, so filtering and
    mipmaps never pull a dark fringe in from (0, 0, 0, 0)."""
    a = np.clip(pm[..., 3:4], 0.0, 1.0)
    rgb = np.clip(np.where(a > 1e-9, pm[..., :3] / np.maximum(a, 1e-9), 0.0), 0.0, 1.0)
    a8 = np.round(a * 255.0)
    rgb8 = np.round(rgb * 255.0)
    rgb8[a8[..., 0] == 0] = np.asarray(bg[:3], dtype=np.float64)
    out = np.concatenate([rgb8, a8], axis=-1).astype(np.uint8)
    path = os.path.join(OUT, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    Image.fromarray(out, "RGBA").save(path, optimize=False, compress_level=9)
    WRITTEN.append(rel)


def crop(pm: np.ndarray, box) -> np.ndarray:
    x, y, w, h = box
    return pm[y:y + h, x:x + w]


# ---- the nine-slices -------------------------------------------------------------------------------------------

def build_panel():
    slab = load("panel_slab")
    sil = slab[..., 3]
    shifted = np.zeros_like(sil)
    shifted[14:, :] = sil[:-14, :]                     # drop-shadow(0 14px 22px #00020C @0.60)
    shadow = G.SHADOW[3] * blur(shifted, 11.0) * window(sil.shape)
    save(over(slab, flat(shadow, G.SHADOW)), "glass_panel.png", bg=G.SHADOW)


def build_tag():
    save(load("tag"), "glass_tag.png", bg=G.GLOW)


def build_buttons():
    sil = load("btn_sil")[..., 3]
    inner = load("btn_inner")[..., 3]
    glow = glow_of(sil, FOCUS_GLOW_SIGMA, FOCUS_GLOW_ALPHA)
    glow_all, glow_out = flat(glow, G.GLOW), flat(glow * (1.0 - sil), G.GLOW)

    normal, selected = load("btn_normal"), load("btn_selected")
    focus_css, pressed_css, pressed_focus_css = load("btn_focus_css"), load("btn_pressed_css"), load(
        "btn_pressed_focus_css")
    overlay = over(load("btn_overlay_parts"), glow_out)          # P5: lit edge + focus wash + glow outside only
    hover = over(focus_css, glow_all)                            # the full CSS focus look

    target = over(pressed_focus_css, glow_all)                   # CSS: focused + pressed
    solved = un_over(target, overlay)
    m = inner[..., None]
    pressed = solved * m + pressed_css * (1.0 - m)               # interior solved, edge band edge-dim

    rebuilt = over(overlay, pressed)                             # what Godot draws: pressed, then focus over it
    interior = inner > 0.999
    err = np.abs(rebuilt - target)[interior].max()
    fx = over(overlay, normal)                                   # what Godot draws: normal, then focus over it
    dev = np.abs(fx - over(focus_css, glow_all))[interior].max()
    print(f"  pressed under focus vs CSS focused-pressed, interior max error {err * 255:.2f}/255")
    print(f"  normal under focus vs CSS focus, interior max deviation {dev * 255:.2f}/255")

    box = G.BTN_BOX
    save(crop(normal, box), "glass_btn_normal.png", bg=G.GLOW)
    save(crop(selected, box), "glass_btn_selected.png", bg=G.GLOW)
    save(crop(pressed, box), "glass_btn_pressed.png", bg=G.GLOW)
    save(overlay, "glass_btn_focus.png", bg=G.GLOW)
    save(hover, "glass_btn_hover.png", bg=G.GLOW)
    save(load("btn_disabled"), "glass_btn_disabled.png", bg=G.GLOW)


def build_tabs():
    sil = load("tab_sil")[..., 3]
    glow = glow_of(sil, FOCUS_GLOW_SIGMA, FOCUS_GLOW_ALPHA)
    selected = load("tab_selected")
    save(crop(load("tab_normal"), G.TAB_BOX), "glass_tab.png", bg=G.GLOW)
    save(crop(selected, G.TAB_BOX), "glass_tab_selected.png", bg=G.GLOW)
    save(flat(glow * (1.0 - sil), G.GLOW), "glass_tab_focus.png", bg=G.GLOW)
    # TG-CLOSE: the quieter hover, the unselected tab lit (fill rgba(glow,.16)) with the glow outside it at .30, so a
    # hovered tab no longer reads as the selected one (TG-ART drew the selected look plus the glow).
    quiet = glow_of(sil, FOCUS_GLOW_SIGMA, TAB_HOVER_GLOW_ALPHA)
    save(over(load("tab_hover_fill"), flat(quiet * (1.0 - sil), G.GLOW)), "glass_tab_hover.png", bg=G.GLOW)


def build_fields():
    sil = load("field_sil")[..., 3]
    glow = glow_of(sil, FOCUS_GLOW_SIGMA, FOCUS_GLOW_ALPHA)
    save(crop(load("field_normal"), G.FIELD_BOX), "glass_field.png", bg=G.GLOW)
    save(over(load("field_ring_focus"), flat(glow * (1.0 - sil), G.GLOW)), "glass_field_focus.png", bg=G.GLOW)
    save(over(load("field_focus_css"), flat(glow, G.GLOW)), "glass_field_hover.png", bg=G.GLOW)


def build_grabber():
    box = load("grabber_box")[..., 3]
    glow = glow_of(box, 6.4, 1.0)                              # 0 0 16px accent, under the .8 scale
    save(over(load("grabber_art"), flat(glow, G.ACCENT)), "glass_slider_grabber.png", bg=G.ACCENT)


def build_diamonds():
    for side, sigma in ((11, 7.0), (12, 6.0)):
        d = load(f"diamond_{side}")
        save(over(d, flat(glow_of(d[..., 3], sigma, 1.0), WHITE)), f"glass_diamond_{side}.png")


# ---- icons -----------------------------------------------------------------------------------------------------

def build_icons():
    for name in G.icon_names():
        kind, s_reg, s_bold, strength, rgb = G.ICON_TABLE[name][3]
        for src, sigma, suffix in zip(G.icon_files(name), (s_reg, s_bold), ("", "_bold")):
            art = load("icons/" + src)
            colour = rgb or WHITE
            if kind != "none":
                art = over(art, flat(strength * blur(art[..., 3], sigma) * window(art.shape[:2], 4.0), colour))
            save(art, f"icons/{name}{suffix}.png", bg=colour)


def build_option_arrow():
    """TG-CLOSE: the OptionButton arrow, chevron_down's bold weight at its 18 px display size (an OptionButton draws its
    arrow at the texture's size), with the generic icon glow scaled to 18 px. Baked in ink2 / ink: the theme tints it by
    the field's font colour (modulate_arrow), which lands it on ink2 at rest."""
    kind, s_reg, s_bold, strength, rgb = G.ICON_TABLE["chevron_down"][3]
    art = load("icons/chevron_down_bold_18")
    sigma = s_bold * 18.0 / 64.0
    art = over(art, flat(strength * blur(art[..., 3], sigma) * window(art.shape[:2], 1.0), WHITE))
    ink, ink2 = (0xF3, 0xF0, 0xE6), (0xC0, 0xBA, 0xA6)
    tint = np.asarray([b / a for a, b in zip(ink, ink2)], dtype=np.float64)
    art = np.concatenate([art[..., :3] * tint, art[..., 3:4]], axis=-1)
    save(art, "icons/chevron_down_18.png", bg=tuple(round(255 * t) for t in tint))


# ---- gradient resources (7.3) ----------------------------------------------------------------------------------

def _fmt(v: float) -> str:
    s = f"{v:.6f}".rstrip("0").rstrip(".")
    return s if s not in ("-0", "") else "0"


def build_gradients():
    for stem, spec in G.GRADIENTS.items():
        stops = G.premult_stops(spec["stops"], sub=8)
        offsets = ", ".join(_fmt(t) for t, _ in stops)
        colors = ", ".join(", ".join(_fmt(v) for v in (c[0] / 255.0, c[1] / 255.0, c[2] / 255.0, c[3]))
                           for _, c in stops)
        w, h = spec["size"]
        text = ("[gd_resource type=\"GradientTexture2D\" load_steps=2 format=3]\n\n"
                f"[sub_resource type=\"Gradient\" id=\"Gradient_{stem}\"]\n"
                f"offsets = PackedFloat32Array({offsets})\n"
                f"colors = PackedColorArray({colors})\n\n"
                "[resource]\n"
                f"gradient = SubResource(\"Gradient_{stem}\")\n"
                f"width = {w}\nheight = {h}\nfill = {spec['fill']}\n"
                f"fill_from = Vector2({_fmt(spec['frm'][0])}, {_fmt(spec['frm'][1])})\n"
                f"fill_to = Vector2({_fmt(spec['to'][0])}, {_fmt(spec['to'][1])})\n")
        with open(os.path.join(OUT, spec["file"]), "w", encoding="utf-8", newline="\n") as fh:
            fh.write(text)
        WRITTEN.append(spec["file"])


def main():
    os.makedirs(OUT, exist_ok=True)
    for fn in (build_panel, build_tag, build_buttons, build_tabs, build_fields, build_grabber, build_diamonds,
               build_icons, build_option_arrow, build_gradients):
        fn()
    print(f"wrote {len(WRITTEN)} files into {os.path.relpath(OUT, G.REPO)}")


if __name__ == "__main__":
    main()
