"""Cut the four placed-construct props out of RAW_ROOT/_constructs/constructs.png.

usage: py construct_pack.py

Writes assets/sprites/constructs/<name>.png at 2x texel density (drawn at 0.5
scale with mipmaps, like the retro v2 characters) and points each construct
scene's Visual Sprite2D at it, replacing the tinted graybox tile. Sizes follow
the collision footprints: grounded props stand with their base on the scene
origin, the Sonata staff platform is centred on its one-way collision.
"""

from __future__ import annotations

import re
from pathlib import Path

from PIL import Image

from generate import RAW_ROOT, REPO
from segment import segment

# name -> (scene, world-pixel size along the fitted axis, fitted axis, anchor)
PROPS = {
    "tesla_coil": ("TeslaCoil", 88.0, "height", "base"),
    "clockwork_turret": ("ClockworkTurret", 66.0, "width", "base"),
    "serpent_nest": ("SerpentNest", 100.0, "width", "base"),
    "sonata_platform": ("SonataPlatform", 132.0, "width", "centre"),
}
TEXEL_SCALE = 2.0


def main() -> None:
    source = RAW_ROOT / "_constructs" / "constructs.png"
    figures = segment(str(source), [4])
    out_dir = REPO / "assets/sprites/constructs"
    out_dir.mkdir(parents=True, exist_ok=True)
    for (name, (scene, size, axis, anchor)), figure in zip(PROPS.items(), figures):
        image = figure.image
        current = image.height if axis == "height" else image.width
        scale = size * TEXEL_SCALE / current
        resized = image.convert("RGBa").resize(
            (round(image.width * scale), round(image.height * scale)), Image.LANCZOS).convert("RGBA")
        resized.save(out_dir / f"{name}.png", optimize=True)
        world_h = resized.height / TEXEL_SCALE
        y = -world_h / 2 if anchor == "base" else 0.0
        patch_scene(REPO / "scenes/constructs" / f"{scene}.tscn", name, y)
        print(f"{name}: {resized.width}x{resized.height} texels, world "
              f"{resized.width / TEXEL_SCALE:.0f}x{world_h:.0f}, Visual y {y:.1f}")


def patch_scene(path: Path, name: str, y: float) -> None:
    text = path.read_text(encoding="utf-8")
    text = text.replace('path="res://assets/placeholders/graybox_tile.svg" id="5_visual"',
                        f'path="res://assets/sprites/constructs/{name}.png" id="5_visual"')
    block = re.search(r'\[node name="Visual" type="Sprite2D" parent="\."\]\n(?:[^\[\n][^\n]*\n)*', text)
    if block is None:
        raise SystemExit(f"{path}: no Visual node")
    replacement = (
        '[node name="Visual" type="Sprite2D" parent="."]\n'
        'texture_filter = 4\n'
        'texture = ExtResource("5_visual")\n'
        f'position = Vector2(0, {y:g})\n'
        f'scale = Vector2({1 / TEXEL_SCALE:g}, {1 / TEXEL_SCALE:g})\n\n'
    )
    text = text[:block.start()] + replacement + text[block.end():]
    path.write_text(text, encoding="utf-8")


if __name__ == "__main__":
    main()
