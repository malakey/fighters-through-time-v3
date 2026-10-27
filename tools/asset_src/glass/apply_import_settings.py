"""TG-ART: turns mipmaps on in the .png.import sidecar of every glass kit PNG (UI_SPEC_GLASS 6.7 item 5: mipmaps on
every UI texture; the HUDs are transform-scaled, and the Steam Deck shows the layout at 0.667).

A first `--import` creates the sidecars with Godot's defaults; this rewrites `mipmaps/generate`, and a second
`--import` re-imports with it. Idempotent; it touches no file outside assets/sprites/ui/glass/.
Run: godot --headless --import ; py tools/asset_src/glass/apply_import_settings.py ; godot --headless --import
"""
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
KIT = os.path.join(REPO, "assets", "sprites", "ui", "glass")


def main():
    cat = json.load(open(os.path.join(KIT, "glass_kit.json"), encoding="utf-8"))
    files = sorted({e["file"] for e in cat.values()
                    if isinstance(e, dict) and e.get("mipmaps") and e.get("file", "").endswith(".png")})
    changed = missing = 0
    for f in files:
        p = os.path.join(KIT, f + ".import")
        if not os.path.exists(p):
            print("missing sidecar (run --import first):", f)
            missing += 1
            continue
        text = open(p, encoding="utf-8", newline="").read()
        new = re.sub(r"mipmaps/generate=\w+", "mipmaps/generate=true", text)
        if new != text:
            with open(p, "w", encoding="utf-8", newline="") as fh:
                fh.write(new)
            changed += 1
    print(f"mipmaps on: {changed} sidecars changed, {len(files) - changed - missing} already on, {missing} missing "
          f"(of {len(files)} PNGs)")
    return 1 if missing else 0


if __name__ == "__main__":
    sys.exit(main())
