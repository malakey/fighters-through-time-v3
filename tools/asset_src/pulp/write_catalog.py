"""Final step of the pulp kit build: merges the SVG-built entries (catalog.json from build_pulp_images.py)
with the icons, ability icons and hero poses, and writes the machine-readable kit catalog to
assets/sprites/ui/pulp/pulp_kit.json (read by UR-THEME and by PulpKitContentTests).

usage (repo root): py tools/asset_src/pulp/write_catalog.py
"""
import json
import os

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
KIT = os.path.join(REPO, "assets", "sprites", "ui", "pulp")

TINT = {
    "dilation": ("control status", "#8FC7FF", "shown 25-28 px inside a 40-44 badge"),
    "venom": ("damage status", "#FF7A5C", "badge glyph"),
    "static": ("control status", "#8FC7FF", "badge glyph"),
    "burn": ("damage status", "#FF7A5C", "badge glyph"),
    "root": ("control status", "#8FC7FF", "badge glyph"),
    "suppress": ("Suppression", "#38F2FF (cold, the Severed)", "status glyph and empty-slot ghost"),
    "shield": ("block pip", "#DFE8FF; off = opacity .28 + grayscale", "30 Story / 24 Fighter"),
    "hourglass": ("rewind counter, dialogue advance caret", "#FFE39A; low = #FF5A5A", "29 in a 46 badge; caret 34 in #FFF0B0"),
    "freeze": ("Time Freeze", "#FFE39A", "29 in a 46 badge, ring accent"),
    "dust": ("Chronal Dust", "#F3C95B", "38"),
    "star4": ("Fighter stock", "P1 #00F0FF / P2 #FF3366; off = opacity .22", "28"),
    "flag": ("objective", "#FFF0B0", "22"),
    "dormant": ("dormant Legacy slot", "#8A8374 @0.9", "16% inset of the slot; flat, no ink, no shadow (spec 3.4)"),
    "cross": ("suppressed slot", "#38F2FF + 6 px glow", "flat; no ink, no shadow"),
    "defy_ready": ("Defy seal ready", "#FFF0B0", "34 Story / 32 Fighter; the black core ring is in _ink"),
    "defy_notready": ("Defy seal not ready", "#8A8374", ""),
    "defy_spent": ("Defy seal spent", "#8A8374", "three arcs"),
    "defy_barred": ("Defy seal unavailable", "#FF5A5A", "ring + slash"),
    "slot_p1": ("player slot shape (triangle)", "P1 #00F0FF", "30 box, 22 glyph; 28 in card badges"),
    "slot_p2": ("player slot shape (circle)", "P2 #FF3366", "same"),
    "prompt_circle": ("A / B prompt circle", "_fill tint = #FFF3D8 (or the label colour inside a button)", "34-36; _ink = black disc + outer ring (omit it inside a focused button); the letter is a live Label"),
    "anchor": ("Act III Beacon anchor icon", "#F3C95B (gold)", "24 px; agent design (UI_SPEC left it undrawn)"),
    "anchor_hollow": ("Beacon anchor, last stand (zero charges)", "#8A8374 or #FF5A5A", "24 px; fill layer is an outline stroke, so the middle is see-through"),
    "anchor_pip": ("Beacon anchor pip", "#F3C95B", "8-12 px diamond"),
    "anchor_pip_cracked": ("Beacon anchor pip, spent by a Snap", "#F3C95B, fade with alpha", "cracked diamond"),
    "marker": ("button marker triangle (20 x 28 at 2x)", "ink3 #8A8374 normal / #0C0A10 focus / #E0412F selected / #4A4333 disabled", "texture is 40x56; the hard shadow is the _sh layer at (2,2)"),
}


def main():
    cat = json.load(open(os.path.join(HERE, "catalog.json"), encoding="utf-8"))
    icons_dir = os.path.join(KIT, "icons")
    for name in sorted(f[len("ico_"):-len("_fill.png")] for f in os.listdir(icons_dir) if f.endswith("_fill.png")):
        role, tint, note = TINT[name]
        size = list(Image.open(os.path.join(icons_dir, f"ico_{name}_fill.png")).size)
        cat[f"ico_{name}"] = {"files": [f"icons/ico_{name}_{l}.png" for l in ("fill", "ink", "sh")], "size": size, "mipmaps": True,
                              "serves": role, "tint": tint, "notes": note,
                              "layers": "fill (white, tint by modulate) < ink (black outline ring + details + paper extras, drawn over the fill) ; sh (black silhouette, drawn under the fill at +2,+2 screen px)"}
    cat["star_badge"] = {"files": [f"star_badge_{l}.png" for l in ("fill", "ink", "sh")], "size": list(Image.open(os.path.join(KIT, "star_badge_fill.png")).size), "mipmaps": True,
                         "serves": cat.get("star_badge", {}).get("serves", ""), "layers": "sh (shadow offset baked) < fill (tint with the player colour) < ink (outline ring)"}
    ab = json.load(open(os.path.join(KIT, "ability_icons", "ability_icons.json"), encoding="utf-8"))
    cat["ability_icons"] = {"files": sorted(v["file"] for v in ab.values()), "size": [144, 144], "mipmaps": True,
                            "serves": "Story / Fighter / select ability slot art: <id>_<special_1|special_2|movement|ultimate>.png, cropped from the retro v2 VFX atlas (scripts/UI/AbilityIconLibrary.cs)"}
    heroes = json.load(open(os.path.join(KIT, "cover_heroes", "cover_heroes.json"), encoding="utf-8"))
    cat["cover_heroes"] = {"files": sorted(v["file"] for v in heroes.values()), "mipmaps": True, "poses": heroes,
                           "serves": "MENU-B cover hero poses (Joan, Einstein, Cleopatra victory frames); feet_px_from_bottom and pivot_x per pose"}
    for k in list(cat):
        if k == "btn_*":
            cat["btn_note"] = cat.pop(k)
    out = json.dumps(cat, indent=1, sort_keys=True)
    for p in (os.path.join(HERE, "catalog.json"), os.path.join(KIT, "pulp_kit.json")):
        with open(p, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(out + "\n")
    print("catalog entries:", len(cat))


if __name__ == "__main__":
    main()
