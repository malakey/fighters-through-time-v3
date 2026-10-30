"""Pack retro v2 generations into a character's 2x sprite atlases.

usage: py pack.py <character> [--allow-missing]

Reads every RAW_ROOT/<character>/raw/<character>__*.png that has a sidecar
(<image>.plan.json, see plan.py), segments it, scales each sheet by its
reference figure so a standing figure reaches the character's target height,
derives the poses plan.REQUIREMENTS builds in code, anchors every frame's pivot
at its cell's horizontal centre and its feet on the cell's feet line, and
writes:

  assets/sprites/characters/<id>/retro/<id>_<sheet>_atlas.png   (2x texels)
  tools/retro_sprites/manifests/<id>.json                        (regions)
  RAW_ROOT/<character>/review/*.png                              (QA sheets)

With --allow-missing only the review sheets are written (a partial set).

Per-frame corrections live in tools/retro_sprites/overrides/<id>.json:
  {"basic_attack_1": {"dx": [0, -6, 0], "dy": [0, 0, 4], "feet": "lowest",
                      "source": {"1": "p03#5"}, "no_register": true},
   "down_air": {"feet_at": {"1": {"source": "<id>__p06#6", "y": 162}}},
   "reject": ["<id>__p03#9"],
   "sheets": {"<id>__p08": {"cuts": [1136, {"x": 1188, "y0": 740, "y1": 840},
                                     {"y": 669, "x0": 1100, "x1": 1460}]}}}
dx/dy shift a frame's art inside its cell (texels, after scaling); feet_at
pins the body's lowest point of one frame (texels in the scaled frame) when a
weapon, beam or swirl below the body would otherwise be taken for the feet;
reject drops a figure; sheet cuts separate figures an effect joins (see
segment.segment).
"""

from __future__ import annotations

import argparse
import copy
import json
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

from generate import RAW_ROOT
from plan import REQUIREMENTS, load_sidecars, rejected
from roster_spec import (CHARACTERS, IDLE_FPS, IDLE_POSES, RUN_FLIGHT_FRAMES, idle_loop_frames,
                         idle_loop_order)
from segment import segment

REPO = Path(__file__).resolve().parents[2]
TOOLS = Path(__file__).resolve().parent
TEXEL_SCALE = 2.0      # texels per legacy 96x128-cell pixel
PAD = 12               # transparent margin around art on every side, under the feet
                       # too: the status outline reaches 2.5 legacy px = 5 texels at 2x
                       # (GlowState clamps at 5 px = 10 texels) and the shader cannot
                       # draw past the quad, so a 3-texel feet margin clipped it
GROUND_SNAP = 0.06     # a grounded frame within this share of the figure height
                       # of its row's ground line stands on that line
# A hanging body's origin sits below the ledge (Story LedgeGrabPointTemplate
# HangAnchor 42 px, the sim's HangDepth 1 u = 60 px), so ledge_hang is anchored
# by its hands, not its feet: the grip sits this many world pixels above the
# origin (between the two modes), and the dangling legs hang below it.
HANG_GRIP_WORLD_PX = 52.0
WORLD_SCALE = 0.845    # CharacterFactory.CharacterSpriteWorldScale

SHEETS: dict[str, list[str]] = {
    "locomotion": ["idle", "run", "jump", "fall", "skid", "crouch"],
    "traversal": ["roll_startup", "roll", "roll_recovery", "ledge_hang", "ledge_pull_up", "ledge_drop"],
    "combat": ["basic_attack_1", "basic_attack_2", "basic_attack_3", "block", "hitstun", "grab", "throw"],
    "directional_attacks": ["up_attack", "down_air"],
    "states": ["dazed", "death", "respawn", "victory", "defeat"],
    "abilities": ["special_1", "special_2", "movement_ability", "ultimate"],
}

# (frames per second, loops). Looping locomotion carries more frames (the idle
# is a ping-pong of roster_spec.IDLE_POSES[character] drawn poses, 2(N-1)
# frames, at IDLE_FPS since 2026-10-28; run 8 since 2026-10-20); every other
# animation keeps three key poses. The run's speed is
# replaced per character by run_fps(); the fall plays once and holds its last
# pose (2026-10-20: a looping fall pumped the legs until landing).
PLAYBACK: dict[str, tuple[float, bool]] = {
    "idle": (IDLE_FPS, True), "run": (14.0, True), "jump": (8.0, False), "fall": (8.0, False),
    "skid": (12.0, False), "crouch": (4.0, True),
    "roll_startup": (12.0, False), "roll": (12.0, False), "roll_recovery": (12.0, False),
    "ledge_hang": (3.0, True), "ledge_pull_up": (8.0, False), "ledge_drop": (8.0, False),
    "basic_attack_1": (8.0, False), "basic_attack_2": (8.0, False), "basic_attack_3": (7.0, False),
    "block": (3.0, True), "hitstun": (8.0, False), "grab": (8.0, False), "throw": (8.0, False),
    "up_attack": (8.0, False), "down_air": (8.0, False),
    "dazed": (4.0, True), "death": (6.0, False), "respawn": (8.0, False),
    "victory": (6.0, True), "defeat": (4.0, True),
    "special_1": (8.0, False), "special_2": (8.0, False),
    "movement_ability": (10.0, False), "ultimate": (8.0, False),
}
FRAME_COUNT = {"run": 8}   # the idle's is per character: expected_frames()


def expected_frames(character: str, anim: str) -> int:
    """Frames the finished animation plays (the idle: its ping-pong loop, 2(N-1))."""
    if anim == "idle":
        return idle_loop_frames(character)
    return FRAME_COUNT.get(anim, 3)

# Loops show one pose breathing or cycling, so their frames are registered to
# each other (upper-body profile correlation) instead of trusting each frame's
# own pivot estimate: a few texels of disagreement read as jitter in a loop.
REGISTERED = {"idle", "run", "fall"}
# The bands of the figure's height (from the feet up) that registration lines
# up, with weights. A stance bobbing in place must keep its soles planted, but
# lined up on the shoes alone the drawn heads swayed 6 texels between frames,
# so the idle weighs its shoes twice its upper body (about 2 texels of slide
# against 4 of sway). Everything else lines up on its upper body.
REGISTER_BANDS = {"idle": (((0.0, 0.12), 2.0), ((0.50, 1.0), 1.0))}
# Atlas shape limits (see shelf_pack).
MAX_ATLAS_SIDE = 4096
AREA_SLACK = 0.03

# The deepest an idle bob may dip, as a share of the stance's height (see the
# "bob" rule in derive); about 5 world pixels on a 100-pixel figure.
BOB_MAX_DIP = 0.14

# The run's frame rate follows the hero's run speed so a stride covers about
# the ground the body does (a fixed rate made Lincoln's feet skate forward and
# Joan's backward): fps = MaxMoveSpeed (world units/s) x RUN_FPS_PER_UNIT,
# rounded and clamped. Read from the CharacterData resource at pack time, so
# the speed keeps one canonical value; a speed change needs a re-pack.
RUN_FPS_PER_UNIT = 2.6
RUN_FPS_RANGE = (10.0, 16.0)


def run_fps(character: str) -> float:
    data = REPO / "resources/Characters" / f"{character}_data.tres"
    for line in data.read_text(encoding="utf-8").splitlines():
        if line.startswith("MaxMoveSpeed = "):
            speed = float(line.split("=", 1)[1])
            return float(min(max(round(speed * RUN_FPS_PER_UNIT), RUN_FPS_RANGE[0]), RUN_FPS_RANGE[1]))
    raise SystemExit(f"{character}: no MaxMoveSpeed in {data}")


class Frame:
    def __init__(self, image: Image.Image, pivot: float, feet: float, source: str):
        self.image = image          # scaled RGBA crop
        self.pivot = pivot          # scaled pivot x inside the crop
        self.feet = feet            # scaled feet line y inside the crop
        self.source = source
        self.dx = 0
        self.dy = 0

    def copy(self) -> "Frame":
        return copy.copy(self)


def _premul_resize(image: Image.Image, size: tuple[int, int]) -> Image.Image:
    return image.convert("RGBa").resize(size, Image.LANCZOS).convert("RGBA")


def _ceil(value: float, step: int = 8) -> int:
    return int(math.ceil(value / step) * step)


# ---------------------------------------------------------------------------
# Reading generated sheets
# ---------------------------------------------------------------------------

def process_image(character: str, image: Path, side: dict, overrides: dict,
                  log: list[str]) -> dict[str, dict[int, Frame]]:
    # A sheet the image model did not lay out as planned (a figure left out,
    # an extra one drawn) is restated in overrides/<id>.json under
    # "sheets": {"<stem>": {"figures": [...], "counts": [...], "scale_ref": n}},
    # where scale_ref names the (1-based) figure whose height is the
    # character's standing height times "scale_ratio" (default 1).
    fix = overrides.get("sheets", {}).get(image.stem)
    if fix:
        side = {**side, **{k: v for k, v in fix.items() if k in ("figures", "counts")}}
    figures = segment(str(image), side["counts"], cuts=(fix or {}).get("cuts"))
    plan = side["figures"]
    if len(figures) != len(plan):
        raise ValueError(f"{image.name}: {len(figures)} figures for {len(plan)} planned")
    target = CHARACTERS[character]["target_height"]

    anchors = {f.slot[0]: f for f, p in zip(figures, plan) if p["anim"] == "anchor"}
    if fix and "scale_ref" in fix:
        ref = figures[int(fix["scale_ref"]) - 1]
        ratio = float(fix.get("scale_ratio", 1.0))
        anchors = {}
        default_scale = target * ratio / (ref.feet_y - ref.bbox[1])
        ref_h = ref.feet_y - ref.bbox[1]
    elif anchors:
        first = anchors[min(anchors)]
        default_scale = target / (first.feet_y - first.bbox[1])
        ref_h = first.feet_y - first.bbox[1]
    else:  # no reference figure (the first, breathing idle sheets): the tallest figure
        heights = [f.feet_y - f.bbox[1] for f in figures]
        default_scale = target / max(heights)
        ref_h = max(heights)
    per_row = side.get("anchor_per_row", False)

    ground: dict[int, float] = {}
    for row in sorted({f.slot[0] for f in figures}):
        if row in anchors:
            ground[row] = anchors[row].feet_y
            continue
        feet = [f.feet_y for f, p in zip(figures, plan)
                if f.slot[0] == row and p["anim"] not in ("anchor", "discard")
                and side["feet"].get(p["anim"], "ground") == "ground"]
        if feet:
            ground[row] = float(np.median(feet))
    # "cycle" frames (a run) take their floor from their own row, not the
    # reference figure: the model draws the reference's feet a few pixels off
    # the cycle's, which floated a whole run. The floor is the median sole of
    # the row's frames that have a foot down.
    row_ground: dict[int, float] = {}
    for row in sorted({f.slot[0] for f in figures}):
        feet = [f.feet_y for f, p in zip(figures, plan)
                if f.slot[0] == row and p["anim"] not in ("anchor", "discard")
                and (overrides.get(p["anim"], {}).get("feet") or side["feet"].get(p["anim"])) == "cycle"
                and p.get("index") not in RUN_FLIGHT_FRAMES]
        if feet:
            row_ground[row] = float(np.median(feet))

    out: dict[str, dict[int, Frame]] = {}
    bad = rejected(character)
    for number, (figure, p) in enumerate(zip(figures, plan), start=1):
        anim = p["anim"]
        if anim in ("anchor", "discard"):
            continue
        if f"{image.stem}#{number}" in bad:
            continue
        if figure.cropped:
            log.append(f"  {image.stem}#{number} ({anim}{p.get('index', '')}) touches the canvas edge")
        row = figure.slot[0]
        scale = anchors_scale(anchors, row, target) if per_row and row in anchors else default_scale
        mode = overrides.get(anim, {}).get("feet") or side["feet"].get(anim, "ground")
        feet_y = figure.feet_y
        if mode == "ground" and row in ground and abs(figure.feet_y - ground[row]) <= GROUND_SNAP * ref_h:
            feet_y = ground[row]
        elif mode == "cycle" and p.get("index") in RUN_FLIGHT_FRAMES and row in row_ground:
            # A flight frame keeps the lift it was drawn with above the row's
            # floor (further than GROUND_SNAP allows), and never sinks below
            # it; a frame with a foot down stands on its own soles, which
            # irons out the model's few-pixel baseline wobble between frames.
            feet_y = max(row_ground[row], figure.feet_y)
        x0, y0 = figure.bbox[0], figure.bbox[1]
        size = (max(1, round(figure.image.width * scale)), max(1, round(figure.image.height * scale)))
        frame = Frame(_premul_resize(figure.image, size), (figure.pivot_x - x0) * scale,
                      (feet_y - y0) * scale, f"{image.stem}#{number}")
        out.setdefault(anim, {})[p["index"]] = frame
    return out


def anchors_scale(anchors: dict, row: int, target: int) -> float:
    a = anchors[row]
    return target / (a.feet_y - a.bbox[1])


# ---------------------------------------------------------------------------
# Poses built in code (plan.REQUIREMENTS "derive")
# ---------------------------------------------------------------------------

def _stretch(frame: Frame, factor: float) -> Frame:
    """Breathing: stretch vertically about the feet line."""
    out = frame.copy()
    height = max(1, round(frame.image.height * factor))
    out.image = _premul_resize(frame.image, (frame.image.width, height))
    out.feet = frame.feet * (height / frame.image.height)
    return out


def _rotate(frame: Frame, degrees: float, cx: float, cy: float) -> Frame:
    """Rotate about (cx, cy) in the frame's own pixels, keeping that point fixed in the cell."""
    margin = int(max(frame.image.size) * 0.25)
    padded = Image.new("RGBA", (frame.image.width + 2 * margin, frame.image.height + 2 * margin))
    padded.alpha_composite(frame.image, (margin, margin))
    rotated = padded.convert("RGBa").rotate(degrees, resample=Image.BICUBIC,
                                            center=(cx + margin, cy + margin)).convert("RGBA")
    box = rotated.getbbox()
    out = frame.copy()
    if box is None:
        return out
    out.image = rotated.crop(box)
    # The rotation centre keeps its padded coordinates; express pivot/feet so
    # the centre lands on the same cell position as in the source frame.
    out.pivot = frame.pivot + margin - box[0]
    out.feet = frame.feet + margin - box[1]
    return out


def _hologram(frame: Frame, solid: float) -> Frame:
    """Respawn: a cyan chronal hologram blended toward the solid figure."""
    a = np.array(frame.image).astype(np.float32) / 255.0
    rgb, alpha = a[..., :3], a[..., 3:]
    lum = (0.30 * rgb[..., 0] + 0.59 * rgb[..., 1] + 0.11 * rgb[..., 2])[..., None]
    cyan = np.array([0.35, 0.95, 1.0], dtype=np.float32)
    holo = np.clip(cyan * (0.30 + 1.05 * lum) + 0.10, 0.0, 1.0)
    rows = np.arange(a.shape[0])[:, None, None]
    scan = np.where((rows // 3) % 2 == 0, 1.0, 0.62)
    holo_alpha = alpha * 0.78 * scan
    out_rgb = holo * (1 - solid) + rgb * solid
    out_alpha = holo_alpha * (1 - solid) + alpha * solid
    # A soft cyan rim so the silhouette reads against dark stages.
    rim = np.array(Image.fromarray((alpha[..., 0] * 255).astype(np.uint8)).filter(
        __import__("PIL.ImageFilter", fromlist=["MaxFilter"]).MaxFilter(5))).astype(np.float32) / 255.0
    rim = np.clip(rim - alpha[..., 0], 0, 1)[..., None] * (1 - solid) * 0.8
    out_rgb = out_rgb * (1 - rim) + cyan * rim
    out_alpha = np.clip(out_alpha + rim, 0, 1)
    merged = np.concatenate([out_rgb, out_alpha], axis=2)
    result = frame.copy()
    result.image = Image.fromarray((merged * 255).round().astype(np.uint8), "RGBA")
    return result


def _figure_height(frame: Frame) -> float:
    """Feet line to the topmost solid ink, in texels."""
    alpha = np.array(frame.image)[..., 3]
    rows = np.nonzero((alpha >= 160).any(axis=1))[0]
    return frame.feet - float(rows.min()) if rows.size else 0.0


def select_bob(heights: list[float], n: int, max_dip: float = BOB_MAX_DIP) -> dict:
    """The ping-pong bob's pose selection, on figure heights alone.

    ``heights`` are the drawn poses' figure heights in drawn order; ``n`` is
    how many poses the character should have (IDLE_POSES). Returns ``kept``
    (drawn indices, tallest first: a model that drew the poses out of order
    still plays correctly, and equal heights, rounded to a texel, keep their
    drawn order), ``order`` (indices into ``kept``: 0..k-1 then k-2..1),
    ``warnings`` and ``complete`` (all ``n`` poses survived)."""
    warnings: list[str] = []
    ranked = sorted(range(len(heights)), key=lambda i: (-round(heights[i]), i))
    if ranked != list(range(len(heights))):
        warnings.append("poses were drawn out of order (tallest to shortest by drawn index "
                        f"{[i + 1 for i in ranked]}); played tallest first")
    tallest = heights[ranked[0]] if ranked else 0.0
    kept = []
    for i in ranked:
        dip = (tallest - heights[i]) / tallest if tallest else 0.0
        if dip > max_dip:
            warnings.append(f"pose {i + 1} dropped: dips {dip * 100:.1f}% below the tallest "
                            f"(limit {max_dip * 100:.0f}%)")
        else:
            kept.append(i)
    if len(kept) > n:
        warnings.append(f"{len(kept)} usable poses drawn, more than the {n} wanted; "
                        f"kept the {n} tallest, dropped pose(s) {[i + 1 for i in kept[n:]]}")
        kept = kept[:n]
    complete = len(kept) == n
    if not complete:
        warnings.append(f"incomplete: {len(kept)} usable poses of {n}")
    return {"kept": kept, "order": idle_loop_order(len(kept)), "warnings": warnings,
            "complete": complete}


def derive(frames: dict[str, dict[int, Frame]], log: list[str], character: str,
           orders: dict[str, list[int]] | None = None) -> dict[str, list[Frame]]:
    out: dict[str, list[Frame]] = {}
    for anim, req in REQUIREMENTS.items():
        have = frames.get(anim, {})
        gen = [have[i] for i in sorted(have)]
        rule = req.get("derive")
        try:
            if rule is None:
                if gen:
                    out[anim] = gen
            elif rule == "breathe" and gen:
                base = gen[0]
                out[anim] = [base, _stretch(base, 1.014), _stretch(base, 1.007)]
            elif rule == "bob" and gen:
                # N drawn poses of the stance (IDLE_POSES), played as a
                # ping-pong bob (select_bob): tallest first, over-deep poses
                # dropped, the unique poses packed once and the play order
                # kept in ``orders`` (the manifest repeats their regions).
                heights = [_figure_height(f) for f in gen]
                picked = select_bob(heights, IDLE_POSES[character])
                out[anim] = [gen[i] for i in picked["kept"]]
                if orders is not None:
                    orders[anim] = picked["order"]
                log.extend(f"  idle: {w}" for w in picked["warnings"])
            elif rule == "settle" and len(gen) >= 3:
                # Played once and held (PLAYBACK): the knees-bent pose leads out
                # of the jump's apex tuck, then the two legs-extended poses, the
                # last of which holds until landing. Looping these pumped the legs.
                out[anim] = [gen[2], gen[0], gen[1]]
            elif rule == "then_idle" and len(gen) >= 2 and out.get("idle"):
                out[anim] = gen[:2] + [out["idle"][0].copy()]   # the tallest idle pose
            elif rule == "sway_from_hands" and gen:
                base = gen[0]
                top = np.nonzero(np.array(base.image)[..., 3].any(axis=1))[0].min()
                base.feet = top + HANG_GRIP_WORLD_PX * TEXEL_SCALE / WORLD_SCALE
                out[anim] = [base, _rotate(base, 2.0, base.pivot, top), _rotate(base, -2.0, base.pivot, top)]
            elif rule == "wobble" and gen:
                base = gen[0]
                out[anim] = [_rotate(base, 1.8, base.pivot, base.feet), base,
                             _rotate(base, -1.8, base.pivot, base.feet)]
            elif rule == "ledge_drop" and frames.get("ledge_hang") and len(frames.get("fall", {})) >= 3:
                out[anim] = [frames["ledge_hang"][0].copy(), frames["fall"][0].copy(),
                             frames["fall"][2].copy()]
            elif rule == "respawn" and out.get("idle"):
                base = out["idle"][0]                           # the tallest idle pose
                out[anim] = [_hologram(base, 0.0), _hologram(base, 0.5), base.copy()]
        except (KeyError, IndexError) as error:
            log.append(f"  derive {anim}: {error}")
    return out


# ---------------------------------------------------------------------------
# Registration and cells
# ---------------------------------------------------------------------------

def _upper_profile(frame: Frame, band: tuple[float, float]) -> np.ndarray:
    alpha = np.array(frame.image)[..., 3]
    top = int(np.nonzero(alpha.any(axis=1))[0].min())
    height = max(1.0, frame.feet - top)
    lo = int(frame.feet - band[1] * height)
    hi = int(frame.feet - band[0] * height)
    region = alpha[max(0, lo):max(1, hi)] >= 160
    return region.sum(axis=0).astype(np.float64)


def _best_shift(frame: Frame, ref: np.ndarray, ref_pivot: float, band: tuple[float, float],
                limit: int) -> int:
    """The shift that best lays ``frame``'s profile in ``band`` over ``ref``."""
    prof = _upper_profile(frame, band)
    best, best_shift = -1.0, 0
    base = int(round(ref_pivot - frame.pivot))
    for s in range(base - limit, base + limit + 1):
        lo = max(0, -s)
        hi = min(len(prof), len(ref) - s)
        if hi <= lo:
            continue
        score = float((prof[lo:hi] * ref[lo + s:hi + s]).sum())
        if score > best:
            best, best_shift = score, s
    return best_shift


def register(frames: list[Frame], bands=((0.50, 1.0), 1.0)) -> None:
    """Shift frames 1..n so they line up with frame 0.

    ``bands`` is one (band, weight) pair or a tuple of them: each band (a share
    of the figure's height above its feet) is lined up on its own and the
    shifts are averaged by weight, so a bobbing stance can keep its soles
    nearly planted without its head swaying on every frame."""
    if len(frames) < 2:
        return
    pairs = (bands,) if isinstance(bands[0][0], float) else bands
    ref_pivot = frames[0].pivot
    limit = int(0.15 * frames[0].image.height)
    refs = [(_upper_profile(frames[0], band), band, weight) for band, weight in pairs]
    total = sum(weight for _, _, weight in refs)
    for frame in frames[1:]:
        shift = sum(weight * _best_shift(frame, ref, ref_pivot, band, limit)
                    for ref, band, weight in refs) / total
        frame.pivot = ref_pivot - shift


def build_cells(frames: list[Frame]) -> tuple[list[Image.Image], int, int, int]:
    """Lay every frame of one animation into identical cells; returns (cells, w, h, feet gap)."""
    half = max(max(f.pivot - f.dx, f.image.width - f.pivot + f.dx) for f in frames) + PAD
    above = max(f.feet - f.dy for f in frames) + PAD
    below = max(0.0, max(f.image.height - f.feet + f.dy for f in frames))
    gap = PAD + int(math.ceil(below))
    # Rounding up adds room above the head; the feet line stays `gap` texels up.
    width = _ceil(2 * half)
    height = _ceil(above + gap)
    cells = []
    for f in frames:
        cell = Image.new("RGBA", (width, height), (0, 0, 0, 0))
        x = round(width / 2 - f.pivot + f.dx)
        y = round(height - gap - f.feet + f.dy)
        cell.alpha_composite(f.image, (max(0, x), max(0, y)))
        cells.append(cell)
    return cells, width, height, gap


def shelf_pack(items: list[tuple[str, int, int, int]]) -> tuple[int, int, dict]:
    """Place (anim, index, w, h) cells on shelves, tallest first, at the atlas
    width (a multiple of 8, no narrower than the widest cell) that gives the
    smallest atlas. One row per animation left the atlases about 40 % empty,
    and texture memory is width x height whatever the art covers.

    The smallest area alone made strips (one shelf of equal-height cells came
    out 9384 x 280), past the 8192 texels some GL3-class GPUs allow, so no side
    may exceed MAX_ATLAS_SIDE, and among layouts within AREA_SLACK of the
    smallest the squarest wins."""
    order = sorted(items, key=lambda it: (-it[3], -it[2], it[0], it[1]))
    widest = max(it[2] for it in items)
    total = sum(it[2] for it in items)
    layouts = []
    for width in range(widest, max(widest, total) + 8, 8):
        x = y = shelf = 0
        places = {}
        for anim, index, w, h in order:
            if x + w > width:
                x, y, shelf = 0, y + shelf, 0
            places[(anim, index)] = (x, y)
            x += w
            shelf = max(shelf, h)
        layouts.append((width, y + shelf, places))
    fitting = [l for l in layouts if max(l[0], l[1]) <= MAX_ATLAS_SIDE]
    if not fitting:
        tallest = max(it[3] for it in items)
        raise ValueError(f"no shelf layout fits within {MAX_ATLAS_SIDE} texels a side "
                         f"(widest cell {widest}, tallest {tallest}, {len(items)} cells)")
    smallest = min(w * h for w, h, _ in fitting)
    near = [l for l in fitting if l[0] * l[1] <= smallest * (1 + AREA_SLACK)]
    width, height, places = min(near, key=lambda l: (max(l[0], l[1]), l[0] * l[1], l[0]))
    return width, height, places


# ---------------------------------------------------------------------------
# Packing
# ---------------------------------------------------------------------------

def collect(character: str, overrides: dict, log: list[str]) -> dict[str, dict[int, Frame]]:
    frames: dict[str, dict[int, Frame]] = {}
    by_source: dict[str, Frame] = {}
    for image, side in load_sidecars(RAW_ROOT, character):
        try:
            found = process_image(character, image, side, overrides, log)
        except ValueError as error:
            log.append(f"  segmentation failed: {error}")
            continue
        for anim, indexed in found.items():
            for index, frame in indexed.items():
                by_source[frame.source] = frame
                # A later sheet for the same pose wins unless an override pins one.
                frames.setdefault(anim, {})[index] = frame
    for anim, spec in overrides.items():
        if anim in ("reject", "sheets") or not isinstance(spec, dict):
            continue
        for index, source in spec.get("source", {}).items():
            if source in by_source:
                frames.setdefault(anim, {})[int(index)] = by_source[source]
            else:
                log.append(f"  override source {source} for {anim}[{index}] not found")
        # An airborne strike's weapon, beam or swirl below the body would
        # otherwise be read as the feet and lift the body above its hurtbox:
        # "feet_at" pins the body's lowest point (texels in the scaled frame),
        # keyed by source so a regenerated sheet never inherits a stale value.
        for index, pin in spec.get("feet_at", {}).items():
            frame = frames.get(anim, {}).get(int(index))
            if frame is None or frame.source != pin["source"]:
                log.append(f"  feet_at for {anim}[{index}] names {pin['source']}, "
                           f"frame is {frame.source if frame else 'missing'}; ignored")
                continue
            frame.feet = float(pin["y"])
    return frames


def pack(character: str, allow_missing: bool) -> dict:
    overrides_path = TOOLS / "overrides" / f"{character}.json"
    overrides = json.loads(overrides_path.read_text()) if overrides_path.exists() else {}
    log: list[str] = []
    orders: dict[str, list[int]] = {}
    animations = derive(collect(character, overrides, log), log, character, orders)

    def played(a: str) -> int:
        return len(orders[a]) if a in orders else len(animations[a])

    missing = [a for a in PLAYBACK if a not in animations
               or played(a) != expected_frames(character, a)]
    if missing and not allow_missing:
        raise SystemExit(f"{character}: incomplete animations {missing}")

    built: dict[str, tuple[list[Image.Image], int, int, int]] = {}
    for anim, anim_frames in animations.items():
        spec = overrides.get(anim, {})
        for i, frame in enumerate(anim_frames):
            frame.dx = (spec.get("dx") or [0] * len(anim_frames))[i]
            frame.dy = (spec.get("dy") or [0] * len(anim_frames))[i]
        if anim in REGISTERED and not spec.get("no_register"):
            register(anim_frames, REGISTER_BANDS.get(anim, ((0.50, 1.0), 1.0)))
        built[anim] = build_cells(anim_frames)

    manifest = {"character": character, "texel_scale": TEXEL_SCALE, "animations": {}, "sheets": {}}
    out_dir = REPO / "assets/sprites/characters" / character / "retro"
    for sheet, anims in SHEETS.items():
        present = [a for a in anims if a in built]
        if not present:
            continue
        items = [(anim, i, built[anim][1], built[anim][2])
                 for anim in present for i in range(len(built[anim][0]))]
        try:
            width, height, places = shelf_pack(items)
        except ValueError as error:
            raise SystemExit(f"{character} {sheet} atlas: {error}") from error
        atlas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
        for anim in present:
            cells, w, h, gap = built[anim]
            unique = []
            for i, cell in enumerate(cells):
                x, y = places[(anim, i)]
                atlas.alpha_composite(cell, (x, y))
                unique.append([x, y, w, h])
            # A ping-pong repeats cells: the manifest lists the play order, each
            # entry naming the one packed region (no duplicate pixels).
            order = orders.get(anim, list(range(len(unique))))
            regions = [list(unique[i]) for i in order]
            speed, loop = PLAYBACK[anim]
            if anim == "run":
                speed = run_fps(character)
            manifest["animations"][anim] = {
                "sheet": sheet, "regions": regions, "feet_gap": gap, "speed": speed, "loop": loop,
                "sources": [animations[anim][i].source for i in order],
            }
        manifest["sheets"][sheet] = [width, height]
        if not allow_missing:
            out_dir.mkdir(parents=True, exist_ok=True)
            atlas.save(out_dir / f"{character}_{sheet}_atlas.png", optimize=True)
    if not allow_missing:
        manifest_path = TOOLS / "manifests" / f"{character}.json"
        manifest_path.parent.mkdir(parents=True, exist_ok=True)
        manifest_path.write_text(json.dumps(manifest, indent=1), encoding="utf-8")
    review(character, built)
    for line in log:
        print(line)
    if missing:
        print(f"  incomplete: {missing}")
    return manifest


# ---------------------------------------------------------------------------
# Review sheets
# ---------------------------------------------------------------------------

def _font(size: int):
    for name in ("arial.ttf", "segoeui.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            pass
    return ImageFont.load_default()


def review(character: str, built: dict) -> None:
    """QA sheets: every animation's cells at half size with feet line, pivot and an idle ghost."""
    out = RAW_ROOT / character / "review"
    out.mkdir(parents=True, exist_ok=True)
    order = [a for anims in SHEETS.values() for a in anims if a in built]
    ghost = None
    if "idle" in built:
        cells, w, h, gap = built["idle"]
        a = np.array(cells[0])
        g = np.zeros_like(a)
        g[..., :3] = (120, 255, 160)
        g[..., 3] = (a[..., 3] > 0) * 70
        ghost = (Image.fromarray(g, "RGBA"), w, h, gap)
    scale, label_w = 0.5, 170
    rows = [(anim, *built[anim]) for anim in order]
    total_h = sum(int(h * scale) + 8 for _, _, _, h, _ in rows) + 40
    total_w = label_w + max(int(len(c) * (w * scale + 6)) for _, c, w, _, _ in rows) + 10
    sheet = Image.new("RGBA", (total_w, total_h), (36, 40, 50, 255))
    draw = ImageDraw.Draw(sheet)
    font = _font(14)
    draw.text((8, 8), f"{character} retro v2 - cells at 50% (feet line green, pivot red, idle ghost)",
              fill=(240, 220, 160), font=_font(18))
    y = 40
    for anim, cells, w, h, gap in rows:
        sw, sh = int(w * scale), int(h * scale)
        draw.text((8, y + 4), f"{anim}\n{w}x{h} gap {gap}", fill=(190, 210, 230), font=font)
        for i, cell in enumerate(cells):
            x = label_w + int(i * (sw + 6))
            draw.rectangle((x, y, x + sw - 1, y + sh - 1), fill=(72, 80, 96, 255))
            if ghost is not None:
                g, gw, gh, ggap = ghost
                gx = int(x + (sw - gw * scale) / 2)
                gy = int(y + (sh - gap * scale) - (gh - ggap) * scale)
                sheet.alpha_composite(g.resize((int(gw * scale), int(gh * scale))), (gx, gy))
            sheet.alpha_composite(_premul_resize(cell, (sw, sh)), (x, y))
            feet_y = y + sh - int(gap * scale)
            draw.line((x, feet_y, x + sw - 1, feet_y), fill=(90, 220, 120, 255))
            draw.line((x + sw // 2, y, x + sw // 2, y + sh - 1), fill=(230, 80, 80, 160))
            draw.rectangle((x, y, x + sw - 1, y + sh - 1), outline=(110, 120, 140, 255))
        y += sh + 8
    for old in out.glob(f"{character}_review_*.png"):
        old.unlink()
    page_h = 2400
    for p in range(max(1, math.ceil(sheet.height / page_h))):
        sheet.crop((0, p * page_h, sheet.width, min(sheet.height, (p + 1) * page_h))).save(
            out / f"{character}_review_{p + 1}.png")


def idle_preview(character: str, caps: list[float]) -> None:
    """Review only: an animated GIF and a pose strip of the idle bob per dip cap.

    Runs the real pack's idle path (collect, select_bob, register, build_cells)
    and writes only to RAW_ROOT/<character>/review/."""
    overrides_path = TOOLS / "overrides" / f"{character}.json"
    overrides = json.loads(overrides_path.read_text()) if overrides_path.exists() else {}
    log: list[str] = []
    have = collect(character, overrides, log).get("idle", {})
    gen = [have[i] for i in sorted(have)]
    if not gen:
        raise SystemExit(f"{character}: no idle frames")
    heights = [_figure_height(f) for f in gen]
    tallest = max(heights)
    out = RAW_ROOT / character / "review"
    out.mkdir(parents=True, exist_ok=True)
    print("drawn dips: " + ", ".join(f"pose {i + 1} {(tallest - h) / tallest * 100:.1f}%"
                                     for i, h in enumerate(heights)))
    spec = overrides.get("idle", {})
    for cap in caps:
        picked = select_bob(heights, IDLE_POSES[character], cap)
        kept = [gen[i].copy() for i in picked["kept"]]
        for i, frame in enumerate(kept):
            frame.dx = (spec.get("dx") or [0] * len(kept))[i]
            frame.dy = (spec.get("dy") or [0] * len(kept))[i]
        if not spec.get("no_register"):
            register(kept, REGISTER_BANDS["idle"])
        cells, w, h, gap = build_cells(kept)
        tag = f"{round(cap * 100):02d}"
        bg = (128, 128, 128, 255)

        def flat(cell: Image.Image) -> Image.Image:
            base = Image.new("RGBA", cell.size, bg)
            base.alpha_composite(cell)
            return base.resize((cell.width * 2, cell.height * 2), Image.NEAREST)

        frames = [flat(cells[i]).convert("RGB") for i in picked["order"]]
        gif = out / f"{character}_idle_preview_dip{tag}.gif"
        frames[0].save(gif, save_all=True, append_images=frames[1:], duration=round(1000 / IDLE_FPS),
                       loop=0, disposal=2)
        strip = Image.new("RGBA", (w * 2 * len(cells), h * 2), bg)
        for i, cell in enumerate(cells):
            strip.alpha_composite(flat(cell), (i * w * 2, 0))
        top = int(np.nonzero(np.array(cells[0])[..., 3].any(axis=1))[0].min()) * 2
        ImageDraw.Draw(strip).line((0, top, strip.width - 1, top), fill=(230, 80, 80, 255))
        png = out / f"{character}_idle_preview_dip{tag}_poses.png"
        strip.convert("RGB").save(png)
        dips = ", ".join(f"pose {i + 1} {(tallest - heights[i]) / tallest * 100:.1f}%"
                         for i in picked["kept"])
        print(f"cap {cap * 100:.0f}%: kept drawn {[i + 1 for i in picked['kept']]} ({dips}); "
              f"loop {len(frames)} frames; order {picked['order']}")
        for w_ in picked["warnings"]:
            print(f"    {w_}")
        print(f"    {gif}\n    {png}")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("character")
    parser.add_argument("--allow-missing", action="store_true",
                        help="review a partial set without writing atlases or the manifest")
    parser.add_argument("--idle-preview", action="store_true",
                        help="review only: write idle bob GIFs and pose strips to RAW_ROOT/<id>/review/")
    parser.add_argument("--max-dip", default=str(BOB_MAX_DIP),
                        help="comma-separated bob dip caps for --idle-preview (e.g. 0.06,0.10,1.0)")
    args = parser.parse_args()
    if args.idle_preview:
        idle_preview(args.character, [float(v) for v in args.max_dip.split(",")])
        return
    manifest = pack(args.character, args.allow_missing)
    for sheet, size in manifest.get("sheets", {}).items():
        print(f"{sheet}: {size[0]}x{size[1]}")


if __name__ == "__main__":
    main()
