"""TG-ART: writes assets/sprites/ui/glass/glass_kit.json, the Temporal Glass kit's machine-readable catalog.

The shape follows the retired pulp kit's catalog (deleted with the pulp kit by TG-CLOSE, 2026-10-27): one object
per stem with its file (relative to assets/sprites/ui/glass/), size, nine-slice margins, expand margins,
content-margin hint, axis stretch, mipmap flag and what it serves. The definitions come from glass_svg.py (KIT,
ICON_TABLE, GRADIENTS) and select_heroes.json; sizes are read back from the written PNGs and must match the
definitions. Every stem carries a glass prefix (glass_, glass_ico_, hero_); scripts/UI/Pulp/PulpKit.cs reads
this catalog.

Read by: PulpKit.cs (Texture, Plate, Expand, SharedPlate, GlassIconTexture), tools/ui_theme/build_ftt_theme.py
(TG-THEME step 2 wires the nine-slices), GlassKitContentTests.cs.
usage (repo root): py tools/asset_src/glass/write_catalog.py
"""
from __future__ import annotations

import json
import os
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import glass_svg as G  # noqa: E402

KIT_DIR = os.path.join(G.REPO, "assets", "sprites", "ui", "glass")


def png_size(rel):
    with Image.open(os.path.join(KIT_DIR, rel)) as im:
        return list(im.size)


def kit_entries():
    out = {}
    for stem, k in G.KIT.items():
        size = png_size(k["file"])
        if size != list(k["size"]):
            raise SystemExit(f"{stem}: {k['file']} is {size}, the definition says {list(k['size'])}")
        e = {"file": k["file"], "size": size, "mipmaps": True, "serves": k["serves"]}
        spec = k.get("spec")
        if spec:
            e["nine_slice_margins_LTRB"] = list(spec["margins"])
            if any(spec["expand"]):
                e["expand_margins_LTRB"] = list(spec["expand"])
            e["spec_6_4"] = {"size": list(spec["size"]), "nine_slice_margins_LTRB": list(spec["margins"]),
                             "expand_margins_LTRB": list(spec["expand"])}
        if "content" in k:
            e["content_margins_hint_LTRB"] = list(k["content"])
        if k.get("stretch"):
            e["axis_stretch"] = k["stretch"]
        if k.get("notes"):
            e["notes"] = k["notes"]
        if k.get("deviation"):
            e["deviation"] = k["deviation"]
        out[stem] = e
    return out


def icon_entries():
    out = {}
    names = G.icon_names()
    for name in names:
        serves, tint, shown, glow = G.ICON_TABLE[name]
        kind, s_reg, s_bold, strength, rgb = glow
        for weight, sigma, suffix in (("regular", s_reg, ""), ("bold", s_bold, "_bold")):
            rel = f"icons/{name}{suffix}.png"
            size = png_size(rel)
            if size != [64, 64]:
                raise SystemExit(f"{rel} is {size}, not 64 x 64")
            if kind == "none":
                g = "none"
            else:
                colour = "white, tinted by modulate" if rgb is None else \
                    "rgb(%d, %d, %d) baked, so the tint's modulate lands it on the spec colour" % tuple(round(v) for v in rgb)
                g = f"{kind}: Gaussian sigma {sigma} bake px at {strength:.2f}, {colour}"
            out[f"glass_ico_{name}{suffix}"] = {
                "file": rel, "size": size, "mipmaps": True, "icon": name, "weight": weight,
                "tint": tint, "shown_at": shown, "glow": g, "serves": serves,
                "source": f"tools/asset_src/glass/svg/icons/{G.icon_files(name)[0 if weight == 'regular' else 1]}.svg",
            }
    out["glass_icons"] = {
        "names": names,
        "regular_stem": "glass_ico_<name>", "bold_stem": "glass_ico_<name>_bold",
        "rule": "UI_SPEC_GLASS 3.5 / AC23: the bold weight below 30 px display size, the regular at 30 px and above",
        "notes": "One white 64 x 64 texture per glyph and weight (a 32-unit grid baked at 2x), its glow baked in, "
                 "tinted by modulate. Glyphs the spec draws in one weight have a derived bold (strokes x1.5; a solid "
                 "glyph's bold is its regular art). shield_off is the block pip with no glow (the CSS off pip). The "
                 "slot-lock overlays are lock_dormant and lock_suppressed (dormant and cross are the plain glyphs). "
                 "Sources: tools/asset_src/glass/icons_src/ (the spec's 52 SVGs plus beacon_anchor_hollow, plan 0.4 P4).",
    }
    return out


def gradient_entries():
    out = {}
    for stem, g in G.GRADIENTS.items():
        if not os.path.exists(os.path.join(KIT_DIR, g["file"])):
            raise SystemExit(f"{stem}: {g['file']} was not written")
        out[stem] = {"file": g["file"], "type": "GradientTexture2D", "size": list(g["size"]),
                     "fill": "radial" if g["fill"] == 1 else "linear", "fill_from": list(g["frm"]),
                     "fill_to": list(g["to"]), "serves": g["serves"],
                     "notes": "Engine resource, no script: load it with GD.Load / ResourceLoader (it is a Texture2D), "
                              "not AuthoredResources. Stops interpolated in premultiplied space as the CSS does "
                              "(sub-stops in the Gradient)."}
    return out


def hero_entries():
    doc = json.load(open(os.path.join(KIT_DIR, "select_heroes", "select_heroes.json"), encoding="utf-8"))
    out = {}
    for hero in doc["heroes"]:
        for n, fr in enumerate(hero["frames"]):
            if png_size(fr["file"]) != fr["size"]:
                raise SystemExit(f"{fr['file']} does not match select_heroes.json")
            out[f"hero_{hero['id']}_idle_{n}"] = {
                "file": fr["file"], "size": fr["size"], "mipmaps": True, "pivot_x": fr["pivot_x"],
                "feet_y": fr["feet_y"], "fps": hero["fps"],
                "serves": f"CARD-A select hero sprite: {hero['id']}'s idle frame {n} of 4 (UI_SPEC_GLASS 3.7)"}
    out["hero_select_index"] = {
        "file": "select_heroes/select_heroes.json", "heroes": [h["id"] for h in doc["heroes"]],
        "select_scale": doc["select_scale"], "select_feet_y": doc["select_feet_y"],
        "serves": "the select hero layer's index: per hero the four frames, pivot_x and feet_y (image px from the "
                  "top-left), the idle frame rate (6 fps) and the loop flag; the roster comes from "
                  "content_manifest.csv, so a new member gets its frames on the next build"}
    return out


def main():
    cat = {"glass_kit_about": {
        "note": "TG-ART Temporal Glass kit (UI_SPEC_GLASS, plan docs/UI_GLASS_2026-10-26_PLAN.md 2.3). Files are "
                "relative to res://assets/sprites/ui/glass/. Every PNG is imported with mipmaps. Nine-slices draw at "
                "1:1 corners; `focus` textures are overlays drawn over the state box (plan 0.4 P5). Rebuild: "
                "tools/asset_src/glass/build_all.ps1 (holding D:/Projects/.ftt-gdunit-lock)."}}
    for part in (kit_entries(), icon_entries(), gradient_entries(), hero_entries()):
        for stem, e in part.items():
            if stem in cat:
                raise SystemExit("duplicate stem " + stem)
            cat[stem] = e
    for stem in cat:
        if not (stem.startswith("glass_") or stem.startswith("hero_")):
            raise SystemExit("unprefixed glass stem " + stem)
    with open(os.path.join(KIT_DIR, "glass_kit.json"), "w", encoding="utf-8", newline="\n") as fh:
        fh.write(json.dumps(cat, indent=1, sort_keys=True) + "\n")
    print("catalog entries:", len(cat))


if __name__ == "__main__":
    main()
