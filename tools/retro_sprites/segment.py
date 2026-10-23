"""Segment a generated retro sprite sheet into individual figures.

The 2026-10-18 retro v2 pass generates each animation as one RGBA image with a
real alpha channel: a few full-body figures laid out in one row (``row``), two
rows (``rows``) or a 2x2 grid (``grid``). This module finds the figures,
assigns loose effect particles to the nearest figure, and measures what the
packer needs: the figure's bounding box, its feet line and its body pivot.

numpy + Pillow only, so the repo tooling stays dependency-light.
"""

from __future__ import annotations

from collections import deque
from dataclasses import dataclass, field

import numpy as np
from PIL import Image

ALPHA_FLOOR = 24        # alpha below this is generator haze, cleared to 0
ALPHA_SOLID = 236       # alpha at or above this is snapped to fully opaque
MASK_ALPHA = 48         # presence threshold for segmentation
DOWNSAMPLE = 4          # segmentation grid cell, in source pixels
JOIN_CELLS = 1          # dilation radius (cells) that joins sparks to a figure
FEET_MIN_PIXELS = 6     # a row needs this many solid pixels to count as feet
MIN_BODY_CELLS = 120    # smallest component (in grid cells) that can be a body
EDGE = 2                # a figure this close to the canvas border was probably cut off
MIN_BODY_SHARE = 0.15   # a body is at least this share of the median body's area


@dataclass
class Figure:
    index: int
    bbox: tuple[int, int, int, int]          # x0, y0, x1, y1 (exclusive), source px
    feet_y: int                              # lowest solid row, source px
    lowest_y: int                            # lowest row with any alpha, source px
    pivot_x: float                           # body pivot column, source px
    area: int
    image: Image.Image = field(repr=False)   # RGBA crop of exactly bbox
    slot: tuple[int, int] = (0, 0)           # (row, column) in the authored layout
    cropped: bool = False                    # touches the canvas edge (likely cut off)


def clean_alpha(rgba: np.ndarray) -> np.ndarray:
    out = rgba.copy()
    a = out[..., 3]
    a[a < ALPHA_FLOOR] = 0
    a[a >= ALPHA_SOLID] = 255
    out[..., 3] = a
    out[a == 0, :3] = 0
    return out


def _label(mask: np.ndarray) -> tuple[np.ndarray, int]:
    """4-connected component labelling of a small boolean grid."""
    labels = np.zeros(mask.shape, dtype=np.int32)
    h, w = mask.shape
    current = 0
    for sy, sx in zip(*np.nonzero(mask)):
        if labels[sy, sx]:
            continue
        current += 1
        labels[sy, sx] = current
        queue = deque([(sy, sx)])
        while queue:
            y, x = queue.popleft()
            for ny, nx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
                if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not labels[ny, nx]:
                    labels[ny, nx] = current
                    queue.append((ny, nx))
    return labels, current


def _dilate(mask: np.ndarray, radius: int) -> np.ndarray:
    out = mask.copy()
    for _ in range(radius):
        grown = out.copy()
        grown[1:, :] |= out[:-1, :]
        grown[:-1, :] |= out[1:, :]
        grown[:, 1:] |= out[:, :-1]
        grown[:, :-1] |= out[:, 1:]
        out = grown
    return out


def load_rgba(path: str) -> np.ndarray:
    return clean_alpha(np.array(Image.open(path).convert("RGBA")))


def segment(path: str, counts: list[int], rgba: np.ndarray | None = None,
            cuts: list[int] | None = None) -> list[Figure]:
    """Return the figures of one generated sheet in authored reading order.

    ``counts`` is the number of figures expected in each row, top to bottom
    (a 2x2 grid is ``[2, 2]``, a dense 3x4 sheet of eleven ``[4, 4, 3]``).
    """
    if rgba is None:
        rgba = load_rgba(path)
    height, width = rgba.shape[:2]
    alpha = rgba[..., 3]
    present = alpha > MASK_ALPHA

    gh, gw = -(-height // DOWNSAMPLE), -(-width // DOWNSAMPLE)
    padded = np.zeros((gh * DOWNSAMPLE, gw * DOWNSAMPLE), dtype=bool)
    padded[:height, :width] = present
    grid = padded.reshape(gh, DOWNSAMPLE, gw, DOWNSAMPLE).any(axis=(1, 3))
    # Forced cuts (from a sheet override) clear one cell of the joined mask,
    # separating figures that an effect (a beam, a trail, a glow halo)
    # connects; the cut cells' pixels belong to nobody. An int is a
    # full-height column at that source x, {"x", "y0", "y1"} a column limited
    # to those source rows, and {"y", "x0", "x1"} a row limited to those
    # source columns.
    joined = _dilate(grid, JOIN_CELLS)
    for cut in cuts or []:
        if isinstance(cut, dict) and "y" in cut:
            row = int(cut["y"]) // DOWNSAMPLE
            joined[row, int(cut.get("x0", 0)) // DOWNSAMPLE:-(-int(cut.get("x1", width)) // DOWNSAMPLE)] = False
        else:
            limits = cut if isinstance(cut, dict) else {"x": cut}
            column = int(limits["x"]) // DOWNSAMPLE
            joined[int(limits.get("y0", 0)) // DOWNSAMPLE:-(-int(limits.get("y1", height)) // DOWNSAMPLE), column] = False
    labels, count = _label(joined)

    comps = []
    for label in range(1, count + 1):
        member = labels == label
        area = int((member & grid).sum())
        if area == 0:
            continue
        ys, xs = np.nonzero(member)
        comps.append({
            "label": label, "area": area,
            "x0": int(xs.min()), "x1": int(xs.max()) + 1,
            "y0": int(ys.min()), "y1": int(ys.max()) + 1,
        })

    expected = sum(counts)
    next_label = count + 1
    bodies: list[dict] = []
    for _ in range(expected + 6):
        bodies = _pick_bodies(comps, expected)
        if len(bodies) == expected and not _stacked(bodies):
            break
        # Touching figures merged into one component. A body far taller than
        # its siblings is two figures stacked (cut across its thinnest row);
        # otherwise the widest body is two side by side (cut down its thinnest
        # column).
        target = _stacked(bodies) or max(bodies, key=lambda c: c["x1"] - c["x0"], default=None)
        if target is None:
            break
        axis = 0 if target is _stacked(bodies) else 1
        split = _split(labels, grid, target, next_label, axis)
        if split is None:
            break
        comps.remove(target)
        comps.extend(split)
        next_label += 1
        count = next_label - 1
    if len(bodies) != expected:
        raise ValueError(f"{path}: expected {expected} figures, found {len(bodies)}")

    # Rows follow the image, not the plan: the model sometimes draws four
    # figures in a row planned for three. Bodies sorted top to bottom start a
    # new row when their centre drops more than half a median body height below
    # the current row's mean centre; reading order is then rows top to bottom,
    # left to right, and only the total count is checked against the plan.
    by_y = sorted(bodies, key=lambda c: (c["y0"] + c["y1"]) / 2)
    heights = sorted(c["y1"] - c["y0"] for c in bodies)
    threshold = 0.5 * heights[len(heights) // 2]
    figures_by_row: list[list[dict]] = []
    for body in by_y:
        centre = (body["y0"] + body["y1"]) / 2
        if figures_by_row:
            row = figures_by_row[-1]
            mean = sum((c["y0"] + c["y1"]) / 2 for c in row) / len(row)
            if centre - mean <= threshold:
                row.append(body)
                continue
        figures_by_row.append([body])
    taken = {c["label"] for c in bodies}
    loose = [c for c in comps if c["label"] not in taken]
    owner = {c["label"]: c["label"] for c in bodies}
    for comp in loose:
        def gap(body, comp=comp):
            dx = max(body["x0"] - comp["x1"], comp["x0"] - body["x1"], 0)
            dy = max(body["y0"] - comp["y1"], comp["y0"] - body["y1"], 0)
            return dx * dx + dy * dy
        owner[comp["label"]] = min(bodies, key=gap)["label"]

    # Full-resolution ownership: every source pixel inherits its cell's label.
    cell_labels = np.repeat(np.repeat(labels, DOWNSAMPLE, 0), DOWNSAMPLE, 1)[:height, :width]
    remap = np.zeros(count + 1, dtype=np.int32)
    for label, body in owner.items():
        remap[label] = body
    pixel_owner = remap[cell_labels]
    _return_split_islands(pixel_owner, present, [c["label"] for c in bodies])

    ordered: list[Figure] = []
    for row_index, row_bodies in enumerate(figures_by_row):
        for column, body in enumerate(sorted(row_bodies, key=lambda c: c["x0"])):
            mine = (pixel_owner == body["label"]) & (alpha > 0)
            ys, xs = np.nonzero(mine)
            x0, x1, y0, y1 = int(xs.min()), int(xs.max()) + 1, int(ys.min()), int(ys.max()) + 1
            crop = rgba[y0:y1, x0:x1].copy()
            crop[~mine[y0:y1, x0:x1]] = 0
            # Feet: the lowest row of solid, non-glowing ink, so a blade trail,
            # lightning bolt or spark burst under the feet never lifts the body.
            solid_rows = ((crop[..., 3] >= 160) & ~_glow(crop)).sum(axis=1) >= FEET_MIN_PIXELS
            feet = y0 + (int(np.nonzero(solid_rows)[0].max()) if solid_rows.any() else y1 - y0 - 1)
            ordered.append(Figure(
                index=len(ordered),
                bbox=(x0, y0, x1, y1),
                feet_y=feet,
                lowest_y=y1 - 1,
                pivot_x=_pivot(crop, x0, y0, feet),
                area=int(mine.sum()),
                image=Image.fromarray(crop, "RGBA"),
                slot=(row_index, column),
                cropped=x0 <= EDGE or y0 <= EDGE or x1 >= width - EDGE or y1 >= height - EDGE,
            ))
    return ordered


def _run_label(mask: np.ndarray) -> tuple[np.ndarray, list[int]]:
    """8-connected component labels of a full-resolution mask, by row runs
    (union-find over horizontal runs, fast enough in plain Python)."""
    parent: list[int] = [0]

    def find(a: int) -> int:
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a

    runs_by_row: list[list[tuple[int, int, int]]] = []
    for y in range(mask.shape[0]):
        row = mask[y]
        edges = np.flatnonzero(np.diff(np.concatenate(([0], row.view(np.int8), [0]))))
        runs = []
        above = runs_by_row[-1] if runs_by_row else []
        for start, end in zip(edges[::2], edges[1::2]):
            label = len(parent)
            parent.append(label)
            for a_start, a_end, a_label in above:
                # 8-connectivity: runs touch if they overlap or meet diagonally.
                if a_start <= end and start <= a_end:
                    ra, rb = find(a_label), find(label)
                    if ra != rb:
                        parent[max(ra, rb)] = min(ra, rb)
            runs.append((int(start), int(end), label))
        runs_by_row.append(runs)
    out = np.zeros(mask.shape, dtype=np.int32)
    sizes: dict[int, int] = {}
    for y, runs in enumerate(runs_by_row):
        for start, end, label in runs:
            root = find(label)
            out[y, start:end] = root
            sizes[root] = sizes.get(root, 0) + end - start
    return out, [sizes.get(i, 0) for i in range(len(parent))]


def _return_split_islands(pixel_owner: np.ndarray, present: np.ndarray, bodies: list[int]) -> None:
    """Give back pieces a cut left on the wrong side.

    A merged pair is cut down a straight column (``_split``), so a prop that
    reaches past the cut (a rail tip, a sword point) lands in the neighbour's
    figure as a stub. After ownership is assigned, any piece of one figure
    that is not connected to its main body but touches another figure's ink
    goes to that figure. Loose sparks touch nobody and stay where they are."""
    height, width = pixel_owner.shape
    for label in bodies:
        mine = (pixel_owner == label) & present
        if not mine.any():
            continue
        ys, xs = np.nonzero(mine)
        # The figure's box plus one pixel, so a touching neighbour is in view;
        # the slices are views, so writes land in pixel_owner.
        y0, y1 = max(0, ys.min() - 1), min(height, ys.max() + 2)
        x0, x1 = max(0, xs.min() - 1), min(width, xs.max() + 2)
        owner, solid, own = pixel_owner[y0:y1, x0:x1], present[y0:y1, x0:x1], mine[y0:y1, x0:x1]
        comp, sizes = _run_label(own)
        main = int(np.argmax(sizes))
        for piece in np.unique(comp):
            if piece in (0, main):
                continue
            island = comp == piece
            grown = island.copy()
            grown[1:, :] |= island[:-1, :]
            grown[:-1, :] |= island[1:, :]
            grown[:, 1:] |= island[:, :-1]
            grown[:, :-1] |= island[:, 1:]
            touched = owner[grown & solid & ~island]
            others = touched[(touched != label) & (touched != 0)]
            if others.size:
                # The piece's faint anti-aliased rim goes with it.
                for _ in range(2):
                    grown[1:, :] |= grown[:-1, :]
                    grown[:-1, :] |= grown[1:, :]
                    grown[:, 1:] |= grown[:, :-1]
                    grown[:, :-1] |= grown[:, 1:]
                rim = grown & ~solid & (owner == label)
                owner[island | rim] = int(np.bincount(others).argmax())


def _pick_bodies(comps: list[dict], expected: int) -> list[dict]:
    """The ``expected`` largest components that are big enough to be a figure.

    A body must also be at least MIN_BODY_SHARE of the median picked body, so
    a dropped prop or a detached spark cluster never stands in for a figure
    the image model left out (that sheet then fails the count instead of
    silently shifting every label by one)."""
    ranked = sorted((c for c in comps if c["area"] >= MIN_BODY_CELLS), key=lambda c: -c["area"])
    picked = ranked[:expected]
    if len(picked) >= 3:
        median = sorted(c["area"] for c in picked)[len(picked) // 2]
        picked = [c for c in picked if c["area"] >= MIN_BODY_SHARE * median]
    return picked


def _stacked(bodies: list[dict]) -> dict | None:
    """A body at least 1.6x the median body height: two figures, one above the other."""
    if len(bodies) < 3:
        return None
    heights = sorted(c["y1"] - c["y0"] for c in bodies)
    median = heights[len(heights) // 2]
    tall = [c for c in bodies if c["y1"] - c["y0"] > 1.6 * median]
    return max(tall, key=lambda c: c["y1"] - c["y0"]) if tall else None


def _split(labels: np.ndarray, grid: np.ndarray, comp: dict, new_label: int, axis: int = 1):
    """Cut a merged component at the emptiest line of its middle 70 %.

    ``axis`` 1 cuts down a column (figures side by side), 0 across a row
    (figures stacked)."""
    member = labels == comp["label"]
    lo_edge, hi_edge = (comp["x0"], comp["x1"]) if axis == 1 else (comp["y0"], comp["y1"])
    occupancy = (member & grid).sum(axis=1 - axis)[lo_edge:hi_edge]
    span = hi_edge - lo_edge
    lo, hi = int(span * 0.15), int(span * 0.85)
    if hi - lo < 2:
        return None
    window = occupancy[lo:hi]
    # Side by side, two touching figures meet at a thin junction; a single wide
    # pose (a body lying flat) has no thin waist, and cutting it would invent
    # a figure. Stacked figures (a body far taller than its siblings) are cut
    # at their emptiest row regardless: feet over a raised arm is not thin.
    filled = occupancy[occupancy > 0]
    if axis == 1 and (filled.size == 0 or window.min() > 0.25 * float(np.percentile(filled, 75))):
        return None
    best = np.flatnonzero(window == window.min())
    cut = lo_edge + lo + int(best[len(best) // 2])
    second = member.copy()
    if axis == 1:
        second[:, :cut] = False
    else:
        second[:cut, :] = False
    labels[second] = new_label
    pieces = []
    for label in (comp["label"], new_label):
        piece = labels == label
        area = int((piece & grid).sum())
        if area == 0:
            continue
        ys, xs = np.nonzero(piece)
        pieces.append({
            "label": label, "area": area,
            "x0": int(xs.min()), "x1": int(xs.max()) + 1,
            "y0": int(ys.min()), "y1": int(ys.max()) + 1,
        })
    return pieces if len(pieces) == 2 else None


def _glow(crop: np.ndarray) -> np.ndarray:
    """Very bright, saturated pixels: weapon trails, energy and light effects."""
    rgb = crop[..., :3].astype(np.float32) / 255.0
    mx = rgb.max(axis=2)
    mn = rgb.min(axis=2)
    sat = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1e-6), 0)
    return (mx > 0.92) & (sat > 0.35)


def _pivot(crop: np.ndarray, x0: int, y0: int, feet_y: int) -> float:
    """Body pivot: centre of the thickest vertical mass between hips and chest.

    In the band 35-80 % of the figure's height above its feet, count solid ink
    per column. The torso is the tallest vertical block there; swinging limbs,
    blades and canes cross the band diagonally and add little per column, so
    the pivot stays on the torso from frame to frame. Very bright, saturated
    pixels (weapon trails, glows) are ignored.
    """
    alpha = crop[..., 3]
    ys = np.nonzero(alpha.any(axis=1))[0]
    top = int(ys.min())
    feet_local = feet_y - y0
    figure_h = max(1, feet_local - top)
    lo = int(feet_local - 0.80 * figure_h)
    hi = int(feet_local - 0.35 * figure_h)
    band = crop[max(0, lo):max(1, hi)]
    rgb = band[..., :3].astype(np.float32) / 255.0
    mx = rgb.max(axis=2)
    mn = rgb.min(axis=2)
    sat = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1e-6), 0)
    glow = (mx > 0.92) & (sat > 0.35)
    body = (band[..., 3] >= 160) & ~glow
    coverage = body.sum(axis=0).astype(np.float64)
    if coverage.max() < 10:
        cols = np.nonzero(alpha >= 160)[1]
        return float(x0 + np.median(cols))
    width = max(3, int(0.05 * figure_h))
    smooth = np.convolve(coverage, np.ones(width) / width, mode="same")
    columns = np.flatnonzero(smooth >= 0.7 * smooth.max())
    weights = smooth[columns]
    return float(x0 + (columns * weights).sum() / weights.sum())
