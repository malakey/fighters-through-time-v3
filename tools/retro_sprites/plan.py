"""Sheet planning for the retro v2 pass.

Image generation is metered (about two dozen images per usage window on the
Codex plan), so sheets are planned from what is still missing: every pose a
character needs is either generated (``gen``) or derived in code from another
pose (``derive``), and the generated ones are packed densely into sheets of a
few rows, each opened by one scale-reference figure.

Every generated image carries a sidecar ``<image>.plan.json`` naming each
figure, which is all ``pack.py`` needs to read it back.
"""

from __future__ import annotations

import json
from pathlib import Path

from roster_spec import JOBS, feet_mode
from prompts import figure_plan, row_counts

# How each contract animation is produced. ``gen`` frames are drawn by the
# image model; ``derive`` builds the rest from drawn poses (see pack.py).
REQUIREMENTS: dict[str, dict] = {
    # Four drawn bends of one fighting stance, replayed as a gentle bob
    # (pack.py "bob").
    "idle": {"gen": 4, "own_sheet": True, "derive": "bob"},
    "run": {"gen": 8, "own_sheet": True},
    "jump": {"gen": 3},
    # The fall settles into one held pose instead of looping (pack.py "settle").
    "fall": {"gen": 3, "derive": "settle"},
    "skid": {"gen": 3},
    "crouch": {"gen": 1, "derive": "breathe"},
    "roll_startup": {"gen": 3},
    "roll": {"gen": 3},
    "roll_recovery": {"gen": 2, "derive": "then_idle"},
    "ledge_hang": {"gen": 1, "derive": "sway_from_hands"},
    "ledge_pull_up": {"gen": 3},
    "ledge_drop": {"derive": "ledge_drop"},
    "block": {"gen": 1, "derive": "breathe"},
    "hitstun": {"gen": 3},
    "dazed": {"gen": 1, "derive": "wobble"},
    "death": {"gen": 3},
    "respawn": {"derive": "respawn"},
    "victory": {"gen": 3},
    "defeat": {"gen": 1, "derive": "breathe"},
    **{name: {"gen": 3, "wide": True} for name in (
        "basic_attack_1", "basic_attack_2", "basic_attack_3", "up_attack", "down_air",
        "grab", "throw", "special_1", "special_2", "movement_ability", "ultimate")},
}

# Sheet shapes on the standard 1536x1024 canvas: (rows, columns).
COMPACT = (3, 4)
WIDE = (3, 3)

FEET: dict[str, str] = {
    "jump": "lowest", "fall": "lowest", "ledge_hang": "lowest", "ledge_pull_up": "lowest",
    "down_air": "lowest",
}


def raw_dir(raw_root: Path, character: str) -> Path:
    return raw_root / character / "raw"


def sidecar_path(image: Path) -> Path:
    return image.with_suffix(".plan.json")


def load_sidecars(raw_root: Path, character: str) -> list[tuple[Path, dict]]:
    out = []
    for image in sorted(raw_dir(raw_root, character).glob(f"{character}__*.png")):
        side = sidecar_path(image)
        if side.exists():
            out.append((image, json.loads(side.read_text(encoding="utf-8"))))
    return out


def legacy_sidecar(character: str, job_name: str) -> dict:
    """Sidecar for a sheet generated with the first (per-animation) job list."""
    job = next(j for j in JOBS if j["job"] == job_name)
    figures = []
    for anim, index in figure_plan(job):
        if anim == "anchor":
            figures.append({"anim": "anchor"})
        elif anim == "run_check":
            figures.append({"anim": "discard"})
        else:
            figures.append({"anim": anim, "index": index})
    anims = {f["anim"] for f in figures} - {"anchor", "discard"}
    return {
        "character": character,
        "counts": row_counts(job),
        "figures": figures,
        "feet": {a: feet_mode(job, a, character) for a in anims},
        # Rows of the first layout carried their own reference figure.
        "anchor_per_row": job["layout"] in ("rows",),
    }


def rejected(character: str) -> set[str]:
    """Figures ruled unusable on review ("<image stem>#<figure number>"),
    listed in overrides/<character>.json under "reject"."""
    path = Path(__file__).resolve().parent / "overrides" / f"{character}.json"
    if not path.exists():
        return set()
    return set(json.loads(path.read_text(encoding="utf-8")).get("reject", []))


def covered(raw_root: Path, character: str) -> dict[str, set[int]]:
    have: dict[str, set[int]] = {}
    bad = rejected(character)
    for image, side in load_sidecars(raw_root, character):
        for number, figure in enumerate(side["figures"], start=1):
            if "index" in figure and f"{image.stem}#{number}" not in bad:
                have.setdefault(figure["anim"], set()).add(figure["index"])
    return have


def needed(raw_root: Path, character: str) -> list[tuple[str, int, bool]]:
    have = covered(raw_root, character)
    out = []
    for anim, req in REQUIREMENTS.items():
        for index in range(req.get("gen", 0)):
            if index not in have.get(anim, set()):
                out.append((anim, index, bool(req.get("wide"))))
    return out


def plan_sheets(raw_root: Path, character: str, shapes=(COMPACT, WIDE)) -> list[dict]:
    """Pack the missing generated poses into sheets. Animations stay contiguous;
    compact poses fill compact sheets, wide poses wide sheets, and a short tail
    of compact poses rides along in the first wide sheet's spare slots."""
    todo = needed(raw_root, character)
    own = [t for t in todo if REQUIREMENTS[t[0]].get("own_sheet")]
    compact = [t for t in todo if not t[2] and not REQUIREMENTS[t[0]].get("own_sheet")]
    wide = [t for t in todo if t[2]]
    sheets: list[dict] = []
    for anim in dict.fromkeys(a for a, _, _ in own):
        sheets.append({"own": anim})
    compact_shape, wide_shape = shapes
    c_cap = compact_shape[0] * compact_shape[1] - 1
    w_cap = wide_shape[0] * wide_shape[1] - 1
    # Wide poses need wide sheets; their spare slots take the compact tail.
    n_wide = -(-len(wide) // w_cap) if wide else 0
    spare = n_wide * w_cap - len(wide)
    tail = compact[len(compact) - spare:] if spare and compact else []
    compact = compact[:len(compact) - len(tail)]
    for chunk in _balanced(compact, c_cap):
        sheets.append({"shape": compact_shape, "poses": chunk})
    for chunk in _balanced(tail + wide, w_cap):
        sheets.append({"shape": wide_shape, "poses": chunk})
    return sheets


def _balanced(items: list, capacity: int) -> list[list]:
    """Split into the fewest chunks of at most ``capacity``, as even as possible."""
    if not items:
        return []
    count = -(-len(items) // capacity)
    size = -(-len(items) // count)
    return [items[i:i + size] for i in range(0, len(items), size)]


def sheet_sidecar(character: str, sheet: dict) -> dict:
    rows, cols = sheet["shape"]
    figures = [{"anim": "anchor"}] + [{"anim": a, "index": i} for a, i, _ in sheet["poses"]]
    counts = []
    remaining = len(figures)
    for _ in range(rows):
        counts.append(min(cols, remaining))
        remaining -= counts[-1]
    counts = [c for c in counts if c > 0]
    anims = {f["anim"] for f in figures} - {"anchor"}
    return {
        "character": character,
        "shape": [rows, cols],
        "counts": counts,
        "figures": figures,
        "feet": {a: FEET.get(a, "lowest" if a == "movement_ability" and character in
                                ("joan", "leonardo", "shakespeare", "mozart", "tubman") else "ground")
                 for a in anims},
        "anchor_per_row": False,
    }
