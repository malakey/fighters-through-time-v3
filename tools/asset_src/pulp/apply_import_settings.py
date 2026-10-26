"""Sets mipmaps/generate=true in the .png.import sidecar of every kit file the catalog flags
`mipmaps` (assets shown scaled or tilted), after a first `--import` created the sidecars.
Run: godot --headless --import ; py tools/asset_src/pulp/apply_import_settings.py ; godot --headless --import
"""
import json
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
KIT = os.path.join(REPO, "assets", "sprites", "ui", "pulp")
cat = json.load(open(os.path.join(KIT, "pulp_kit.json"), encoding="utf-8"))
files = set()
for e in cat.values():
    if not isinstance(e, dict) or not e.get("mipmaps"):
        continue
    if "file" in e and "{" not in e["file"]:
        files.add(e["file"])
    for f in e.get("files", []):
        files.add(f)
n = 0
for f in sorted(files):
    p = os.path.join(KIT, f + ".import")
    if not os.path.exists(p):
        print("missing sidecar", p)
        continue
    t = open(p, encoding="utf-8", newline="").read()
    t2 = re.sub(r"mipmaps/generate=\w+", "mipmaps/generate=true", t)
    if t2 != t:
        open(p, "w", encoding="utf-8", newline="").write(t2)
        n += 1
print("mipmaps enabled on", n, "of", len(files), "files")
