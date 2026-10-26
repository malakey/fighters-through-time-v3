"""ICN-B icon geometry (UI_SPEC Appendix A, 32 x 32 grid) and the extra glyphs the
spec left undrawn (Beacon anchor and pips, marker, prompt circle).

Layer vocabulary (Appendix A): F fill shapes (white here; the HUD tints them) with a
black outline 2.6 painted under; X extras (paper fill, black stroke 1.6);
K solid black; D detail strokes (black, 2.1, round).  The extra keys:
  FS  white stroked paths (no fill), list of (d, width)       -- defy seal, cross
  TS  black strokes painted on top, list of (d, width)         -- defy core ring
  outline=False  the F shapes carry no black outline          -- flat glyphs
"""
from __future__ import annotations

from pulp_svg import PAPER, f

INK = "#000000"


def circ(cx, cy, r):
    return f"M{f(cx - r)},{f(cy)} A{f(r)},{f(r)} 0 1 0 {f(cx + r)},{f(cy)} A{f(r)},{f(r)} 0 1 0 {f(cx - r)},{f(cy)} Z"


ANCHOR_F = [
    circ(16, 6.2, 3.6),
    "M14.4,9 L17.6,9 L17.6,12 L23,12 L23,14.8 L17.6,14.8 L17.6,26.6 L14.4,26.6 L14.4,14.8 L9,14.8 L9,12 L14.4,12 Z",
    "M4,18.6 L9.2,20 C10.3,23.4 12.8,25.6 16,26.2 C19.2,25.6 21.7,23.4 22.8,20 L28,18.6 C27.4,25 22.4,29.2 16,29.4 C9.6,29.2 4.6,25 4,18.6 Z",
    "M2.8,16 L8.6,19.8 L4,22.2 Z",
    "M29.2,16 L23.4,19.8 L28,22.2 Z",
]

ICONS: dict = {
    # --- statuses
    "dilation": dict(F=[circ(16, 16, 12)], X=["M16,4 A12,12 0 0 1 16,28 Z"], D=["M16,9 V16 L21,19"]),
    "venom": dict(
        F=["M7.5,15 C7.5,9 11,4.5 16,4.5 C21,4.5 24.5,9 24.5,15 C24.5,18.2 23,20.2 21.2,21.2 V25.5 H10.8 V21.2 C9,20.2 7.5,18.2 7.5,15 Z"],
        K=[circ(12.6, 14.9, 2.5), circ(19.4, 14.9, 2.5)],
        D=["M14,25.5 V21.5", "M16,25.5 V21.5", "M18,25.5 V21.5", "M15.2,18.4 L16,16.8 L16.8,18.4 Z"]),
    "static": dict(F=["M18.5,2.5 L7.5,18 H14.8 L12.8,29.5 L24.5,13 H17.2 Z"]),
    "burn": dict(
        F=["M16,2.5 L18.7,11 L27,8 L21.8,15 L29.5,16 L21.8,17 L27,24 L18.7,21 L16,29.5 L13.3,21 L5,24 L10.2,17 L2.5,16 L10.2,15 L5,8 L13.3,11 Z"],
        D=[circ(16, 16, 4.5)]),
    "root": dict(
        F=["M16,29 C15,24 15,18 16,12 C17,18 17,24 16,29 Z", "M16,20 C10,20 6.5,16 6,8 C12,8.5 15.5,12 16,20 Z",
           "M16,20 C22,20 25.5,16 26,8 C20,8.5 16.5,12 16,20 Z", "M16,12 C13.5,10 13,6.5 16,3 C19,6.5 18.5,10 16,12 Z"],
        D=["M16,29 L11,31", "M16,29 L21,31"]),
    "suppress": dict(F=[circ(16, 16, 12)], D=["M8.5,8.5 L23.5,23.5", "M23.5,8.5 L8.5,23.5"]),
    # --- HUD
    "shield": dict(F=["M16,3 L27,7 V15 C27,22 22,27 16,29.5 C10,27 5,22 5,15 V7 Z"], D=["M16,7 V25", "M9.5,12 H22.5"]),
    "hourglass": dict(
        F=["M8,4 H24 V7.5 C24,12 19.5,14 16,16 C19.5,18 24,20 24,24.5 V28 H8 V24.5 C8,20 12.5,18 16,16 C12.5,14 8,12 8,7.5 Z"],
        D=["M11,7.5 H21", "M13,25 H19"]),
    "freeze": dict(F=[circ(16, 16, 13)],
                   D=["M16,6.5 V25.5", "M7.5,11.2 L24.5,20.8", "M7.5,20.8 L24.5,11.2", "M13,8.4 L16,11 L19,8.4", "M13,23.6 L16,21 L19,23.6"]),
    "dust": dict(F=["M16,3.5 L25.5,12 L16,28.5 L6.5,12 Z"],
                 X=["M26,2.5 L27.2,5.8 L30.5,7 L27.2,8.2 L26,11.5 L24.8,8.2 L21.5,7 L24.8,5.8 Z"],
                 D=["M6.5,12 H25.5", "M11.2,12 L16,3.5 L20.8,12 L16,28.5 Z"]),
    "star4": dict(F=["M16,2 L19.5,12.5 L30,16 L19.5,19.5 L16,30 L12.5,19.5 L2,16 L12.5,12.5 Z"]),
    "flag": dict(F=["M7,3 H9.2 V29 H7 Z", "M9.2,4.5 H26 L21.5,10.5 L26,16.5 H9.2 Z"]),
    # --- slot locks (flat, no outline, no shadow in the spec; a _sh layer is still written for symmetry)
    "dormant": dict(F=["M16,3.5 L19.2,12.8 L28.5,16 L19.2,19.2 L16,28.5 L12.8,19.2 L3.5,16 L12.8,12.8 Z"], outline=False),
    "cross": dict(FS=[("M7,7 L25,25", 2.6), ("M25,7 L7,25", 2.6)], outline=False),
    # --- Defy seal (rings are strokes in the tint; the ready core has a black ring)
    "defy_ready": dict(FS=[(circ(16, 16, 11), 3.4), ("M16,1.5 V5", 3.2), ("M16,27 V30.5", 3.2), ("M1.5,16 H5", 3.2), ("M27,16 H30.5", 3.2)],
                       F=[circ(16, 16, 5.2)], TS=[(circ(16, 16, 5.2), 2.0)], outline=False),
    "defy_notready": dict(FS=[(circ(16, 16, 11), 3.4)], outline=False),
    "defy_spent": dict(FS=[("M16,5 A11,11 0 0 1 27,16", 3.4), ("M24,25 A11,11 0 0 1 8,25", 3.4), ("M5,16 A11,11 0 0 1 9,8", 3.4)], outline=False),
    "defy_barred": dict(FS=[(circ(16, 16, 11), 3.4), ("M8.5,23.5 L23.5,8.5", 3.4)], outline=False),
    # --- player shapes (flat)
    "slot_p1": dict(F=["M16,4.8 L27.9,25.8 H4.1 Z"], outline=False),
    "slot_p2": dict(F=[circ(16, 16, 11.2)], outline=False),
    # --- prompt circle: tinted ring (_fill); black disc + outer ring (_ink) for the key-cap mode.  The letter is a live Label.
    "prompt_circle": dict(FS=[(circ(16, 16, 13), 2.0)], K=[circ(16, 16, 12.0)], TS=[(circ(16, 16, 15.0), 2.0)], outline=False),
    # --- Act III Beacon anchor icon and pips (agent design, pulp style; the spec left them undrawn)
    "anchor": dict(F=ANCHOR_F, K=[circ(16, 6.2, 1.3)]),
    "anchor_hollow": dict(FS=[(d, 2.0) for d in ANCHOR_F], outline=False, hollow=True),
    "anchor_pip": dict(F=["M16,3 L29,16 L16,29 L3,16 Z"], D=["M16,9 L21,16"]),
    "anchor_pip_cracked": dict(F=["M16,3 L29,16 L16,29 L3,16 Z"], D=["M16,3 L12.5,11 L19,16.5 L13.5,22 L16,29"]),
    # --- button marker triangle (20 x 28)
    "marker": dict(F=["M0,0 L20,14 L0,28 Z"], outline=False, view=(0, 0, 20, 28)),
}


def layers(spec: dict) -> dict:
    """Returns {'fill': svg body, 'sil': ..., 'top': ...} for one glyph."""
    outline = spec.get("outline", True)
    fill, sil, top = [], [], []
    for d in spec.get("F", []):
        fill.append(f'<path fill="#FFFFFF" d="{d}"/>')
        if outline:
            sil.append(f'<path fill="{INK}" stroke="{INK}" stroke-width="2.6" stroke-linejoin="round" d="{d}"/>')
        else:
            sil.append(f'<path fill="{INK}" d="{d}"/>')
    for d, w in spec.get("FS", []):
        fill.append(f'<path fill="none" stroke="#FFFFFF" stroke-width="{f(w)}" stroke-linecap="round" stroke-linejoin="round" d="{d}"/>')
        grow = 2.6 if (outline and spec.get("hollow")) else 0
        sil.append(f'<path fill="none" stroke="{INK}" stroke-width="{f(w + grow)}" stroke-linecap="round" stroke-linejoin="round" d="{d}"/>')
        if spec.get("hollow"):
            pass
    for d in spec.get("X", []):
        sil.append(f'<path fill="{INK}" stroke="{INK}" stroke-width="1.6" stroke-linejoin="round" d="{d}"/>')
        top.append(f'<path fill="{INK}" stroke="{INK}" stroke-width="1.6" stroke-linejoin="round" d="{d}"/>')
        top.append(f'<path fill="{PAPER}" d="{d}"/>')
    for d in spec.get("K", []):
        sil.append(f'<path fill="{INK}" d="{d}"/>')
        top.append(f'<path fill="{INK}" d="{d}"/>')
    for d in spec.get("D", []):
        top.append(f'<path fill="none" stroke="{INK}" stroke-width="2.1" stroke-linecap="round" stroke-linejoin="round" d="{d}"/>')
    for d, w in spec.get("TS", []):
        top.append(f'<path fill="none" stroke="{INK}" stroke-width="{f(w)}" stroke-linecap="round" stroke-linejoin="round" d="{d}"/>')
        sil.append(f'<path fill="none" stroke="{INK}" stroke-width="{f(w)}" d="{d}"/>')
    return {"fill": "".join(fill), "sil": "".join(sil) or "", "top": "".join(top)}
