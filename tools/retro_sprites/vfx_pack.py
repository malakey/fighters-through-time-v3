"""Pack retro v2 ability-VFX sheets into 2x atlases (384 px cells).

usage: py vfx_pack.py <character> [<character> ...] [--review-only]

Each RAW_ROOT/_vfx/<character>__vfx.png is a 4-row x 3-column grid (rows:
special_1, special_2, movement, ultimate). Every component is assigned to the
grid cell holding most of its pixels, so loose sparks stay with their frame.
Each row is then mapped onto the old 192 px atlas's footprint for that
ability (scaled x2): the union box of the three new frames is fitted into the
union box of the three old frames, which keeps the effect where gameplay and
earlier passes expect it while the frames keep their own motion. A footprint
smaller than MIN_EXTENT of the cell is grown to it (several old first frames
were near-empty).
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

from generate import RAW_ROOT, REPO
from segment import clean_alpha

TOOLS = Path(__file__).resolve().parent
CELL = 384
OLD_CELL = 192
TEXEL_SCALE = 2.0
ROWS, COLS = 4, 3
MIN_EXTENT = 0.55
MAX_EXTENT = 0.94


def _gutters(occupancy: np.ndarray, parts: int) -> list[int]:
    """Cut positions between ``parts`` equal bands, each moved to the emptiest
    line within an eighth of the extent of its nominal place. Wide effects and
    glow particles often bridge neighbouring cells, so cells are separated at
    their sparsest line rather than by connected components."""
    extent = len(occupancy)
    window = extent // 8
    cuts = [0]
    for k in range(1, parts):
        nominal = round(k * extent / parts)
        lo, hi = max(cuts[-1] + 1, nominal - window), min(extent - 1, nominal + window)
        segment_ = occupancy[lo:hi + 1]
        best = np.flatnonzero(segment_ == segment_.min()) + lo
        cuts.append(int(best[np.argmin(np.abs(best - nominal))]))
    cuts.append(extent)
    return cuts


def _assign(rgba: np.ndarray) -> list[list[np.ndarray]]:
    """Per grid cell, the mask of pixels belonging to it."""
    height, width = rgba.shape[:2]
    present = rgba[..., 3] > 24
    row_cuts = _gutters(present.sum(axis=1), ROWS)
    masks = [[np.zeros((height, width), dtype=bool) for _ in range(COLS)] for _ in range(ROWS)]
    for r in range(ROWS):
        y0, y1 = row_cuts[r], row_cuts[r + 1]
        col_cuts = _gutters(present[y0:y1].sum(axis=0), COLS)
        for c in range(COLS):
            x0, x1 = col_cuts[c], col_cuts[c + 1]
            masks[r][c][y0:y1, x0:x1] = present[y0:y1, x0:x1] | (rgba[y0:y1, x0:x1, 3] > 0)
    return masks


OLD_DIR = RAW_ROOT / "_vfx" / "old_192"


def snapshot_old_atlases(characters: list[str]) -> None:
    """Keep the pre-v2 192 px atlases, the footprint reference, outside the repo."""
    OLD_DIR.mkdir(parents=True, exist_ok=True)
    for character in characters:
        target = OLD_DIR / f"{character}_ability_vfx_atlas.png"
        source = REPO / "assets/sprites/vfx" / character / f"{character}_ability_vfx_atlas.png"
        if not target.exists() and source.exists() and Image.open(source).width == COLS * OLD_CELL:
            target.write_bytes(source.read_bytes())


def _old_footprint(character: str, row: int) -> tuple[float, float, float, float] | None:
    path = OLD_DIR / f"{character}_ability_vfx_atlas.png"
    if not path.exists():
        return None
    atlas = np.array(Image.open(path).convert("RGBA"))
    if atlas.shape[0] < (row + 1) * OLD_CELL:
        return None
    boxes = []
    for col in range(COLS):
        cell = atlas[row * OLD_CELL:(row + 1) * OLD_CELL, col * OLD_CELL:(col + 1) * OLD_CELL, 3]
        ys, xs = np.nonzero(cell > 16)
        if ys.size:
            boxes.append((xs.min(), ys.min(), xs.max() + 1, ys.max() + 1))
    if not boxes:
        return None
    x0 = min(b[0] for b in boxes) * TEXEL_SCALE
    y0 = min(b[1] for b in boxes) * TEXEL_SCALE
    x1 = max(b[2] for b in boxes) * TEXEL_SCALE
    y1 = max(b[3] for b in boxes) * TEXEL_SCALE
    return x0, y0, x1, y1


def pack_character(character: str, review_only: bool) -> None:
    source = RAW_ROOT / "_vfx" / f"{character}__vfx.png"
    rgba = clean_alpha(np.array(Image.open(source).convert("RGBA")))
    height, width = rgba.shape[:2]
    ch, cw = height / ROWS, width / COLS
    masks = _assign(rgba)
    atlas = Image.new("RGBA", (COLS * CELL, ROWS * CELL), (0, 0, 0, 0))
    for row in range(ROWS):
        frames = []
        for col in range(COLS):
            art = rgba.copy()
            art[~masks[row][col]] = 0
            ys, xs = np.nonzero(art[..., 3] > 0)
            if ys.size == 0:
                frames.append(None)
                continue
            box = (xs.min(), ys.min(), xs.max() + 1, ys.max() + 1)
            # Frame art in slot-relative coordinates.
            frames.append((Image.fromarray(art[box[1]:box[3], box[0]:box[2]], "RGBA"),
                           box[0] - col * cw, box[1] - row * ch))
        present = [f for f in frames if f is not None]
        if not present:
            print(f"  {character} row {row}: empty")
            continue
        ux0 = min(f[1] for f in present)
        uy0 = min(f[2] for f in present)
        ux1 = max(f[1] + f[0].width for f in present)
        uy1 = max(f[2] + f[0].height for f in present)
        uw, uh = ux1 - ux0, uy1 - uy0
        footprint = _old_footprint(character, row) or (CELL * 0.15, CELL * 0.15, CELL * 0.85, CELL * 0.85)
        fx0, fy0, fx1, fy1 = footprint
        fw = fx1 - fx0
        # The shipped effects span their cell's width and rest on its bottom
        # (the VFX scenes draw the cell centred on the spawn point, and earlier
        # passes checked that placement in live scenes), so each new row keeps
        # the old row's width and bottom line; its height follows its own
        # aspect, capped to the cell.
        scale = fw / uw
        scale = min(scale, MAX_EXTENT * CELL / uh)
        scale = max(scale, MIN_EXTENT * CELL / max(uw, uh))
        cx = (fx0 + fx1) / 2
        ox = min(max(cx - uw * scale / 2, 4), CELL - 4 - uw * scale)
        oy = min(max(fy1 - uh * scale, 4), CELL - 4 - uh * scale)
        for col, frame in enumerate(frames):
            if frame is None:
                continue
            image, rx, ry = frame
            size = (max(1, round(image.width * scale)), max(1, round(image.height * scale)))
            scaled = image.convert("RGBa").resize(size, Image.LANCZOS).convert("RGBA")
            x = round(ox + (rx - ux0) * scale)
            y = round(oy + (ry - uy0) * scale)
            cell = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
            cell.alpha_composite(scaled, (max(0, x), max(0, y)))
            atlas.alpha_composite(cell, (col * CELL, row * CELL))
    review = RAW_ROOT / "_vfx" / "review"
    review.mkdir(parents=True, exist_ok=True)
    preview = Image.new("RGBA", atlas.size, (58, 64, 78, 255))
    preview.alpha_composite(atlas)
    draw = ImageDraw.Draw(preview)
    for r in range(ROWS):
        for c in range(COLS):
            draw.rectangle((c * CELL, r * CELL, (c + 1) * CELL - 1, (r + 1) * CELL - 1),
                           outline=(100, 110, 130, 255))
    preview.resize((preview.width // 2, preview.height // 2), Image.LANCZOS).save(
        review / f"{character}_vfx_review.png")
    if review_only:
        return
    out = REPO / "assets/sprites/vfx" / character / f"{character}_ability_vfx_atlas.png"
    atlas.save(out, optimize=True)
    manifest = TOOLS / "manifests" / f"{character}_vfx.json"
    manifest.parent.mkdir(parents=True, exist_ok=True)
    manifest.write_text(json.dumps({"character": character, "cell": CELL,
                                    "texel_scale": TEXEL_SCALE}, indent=1), encoding="utf-8")
    print(f"{character}: wrote {out.name}")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("characters", nargs="+")
    parser.add_argument("--review-only", action="store_true")
    args = parser.parse_args()
    snapshot_old_atlases(args.characters)
    for character in args.characters:
        pack_character(character, args.review_only)


if __name__ == "__main__":
    main()
