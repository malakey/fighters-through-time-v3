"""Generate retro v2 sprite sheets through Codex's built-in image_gen tool.

usage:
  py generate.py <character> model_sheet [--variants N]
  py generate.py <character> <job> [<job> ...]
  py generate.py <character> all
  py generate.py <character> missing

Raw generations are kept outside the repository, beside the earlier retro
passes' source records (RAW_ROOT/<character>/raw). Each run writes its exact
session prompt and the Codex log next to the images it produced.
"""

from __future__ import annotations

import argparse
import datetime as dt
import subprocess
import sys
from pathlib import Path

import json

from plan import legacy_sidecar, plan_sheets, sheet_sidecar, sidecar_path
from prompts import codex_session, image_prompt, model_sheet_prompt, sheet_prompt
from roster_spec import JOBS

REPO = Path(__file__).resolve().parents[2]
DOCS = Path("D:/Projects/fighters-through-time-docs-2/mockups")
RAW_ROOT = DOCS / "implementation_assets_v3" / "retro_roster_v2"
STYLE_AUTHORITY = DOCS / "level_art_styles" / "04_chicago_tesla_retro.png"
STYLE_SETS = {
    "joan": DOCS / "character_style_sets/retro/joan_orleans_retro.png",
    "leonardo": DOCS / "character_style_sets/retro/leonardo_florence_retro.png",
    "lincoln": DOCS / "character_style_sets/retro/lincoln_gettysburg_retro.png",
    "cleopatra": DOCS / "character_style_sets/retro/cleopatra_alexandria_retro.png",
    "mozart": DOCS / "character_style_sets/retro/mozart_bastille_retro.png",
    "shakespeare": DOCS / "character_style_sets/retro/shakespeare_globe_retro.png",
    "einstein": DOCS / "level_art_styles/07_florence_einstein_base_retro_pulp.png",
    "tesla": DOCS / "level_art_styles/04_chicago_tesla_retro.png",
}
JOB_NAMES = [job["job"] for job in JOBS]


def character_dir(character: str) -> Path:
    path = RAW_ROOT / character / "raw"
    path.mkdir(parents=True, exist_ok=True)
    return path


def model_sheet_path(character: str) -> Path:
    return RAW_ROOT / character / f"{character}_model_sheet.png"


def portrait_path(character: str) -> Path:
    return REPO / "assets/sprites/characters/portraits" / f"{character}_portrait.png"


def run_codex(workdir: Path, prompt: str, references: list[Path], tag: str) -> int:
    stamp = dt.datetime.now().strftime("%Y%m%d_%H%M%S")
    (workdir / f"session_{tag}_{stamp}.txt").write_text(prompt, encoding="utf-8")
    command = [
        "codex", "exec", "--skip-git-repo-check", "-s", "workspace-write",
        "-c", "model_reasoning_effort=low", "-C", str(workdir),
    ]
    for ref in references:
        command += ["-i", str(ref)]
    command.append("-")
    with open(workdir / f"session_{tag}_{stamp}.log", "w", encoding="utf-8") as log:
        result = subprocess.run(command, input=prompt, text=True, stdout=log,
                                stderr=subprocess.STDOUT, encoding="utf-8")
    return result.returncode


def generate_model_sheet(character: str, variants: int) -> None:
    out = RAW_ROOT / character
    out.mkdir(parents=True, exist_ok=True)
    refs = []
    if character != "tubman" and portrait_path(character).exists():
        refs.append(portrait_path(character))
    if character in STYLE_SETS:
        refs.append(STYLE_SETS[character])
    refs.append(STYLE_AUTHORITY)
    assets = []
    for v in range(variants):
        name = f"{character}_model_sheet.png" if variants == 1 else f"{character}_model_sheet_v{v + 1}.png"
        assets.append((name, model_sheet_prompt(character)))
    ref_lines = []
    for i, ref in enumerate(refs, start=1):
        if ref == STYLE_AUTHORITY:
            ref_lines.append(f"Image {i}: the game's retro art-style authority mockup - match its linework and rendering.")
        elif ref == portrait_path(character):
            ref_lines.append(f"Image {i}: the character's portrait - the identity and costume anchor; match face, hair and costume exactly.")
        else:
            ref_lines.append(f"Image {i}: a retro gameplay mockup featuring this character - match how characters are drawn in it.")
    prompt = (
        "Use the imagegen skill with its built-in image_gen tool (do NOT use the CLI fallback "
        f"script). Make exactly {len(assets)} image_gen call(s), one per asset below. After each "
        "call, copy the generated PNG from $CODEX_HOME/generated_images into the current working "
        "directory under that asset's file name (byte-for-byte; never edit pixels). Then print one "
        "line per asset: \"<file name> <- <source path>\". Do nothing else.\n\n"
        "Attached reference images (references only, never edit targets):\n"
        + "\n".join(f"- {line}" for line in ref_lines) + "\n\n"
        "Shared style: retro pulp adventure-comic game art matching the attached mockups: "
        "confident hand-inked black contour lines, fine engraved hatching, subtle halftone print "
        "texture, rich painted colour with warm highlights, crisp readable silhouette. Not pixel "
        "art, not 3D, not anime, not chibi. Realistic adult proportions (about seven heads tall).\n\n"
        + "\n\n".join(f"Asset {n} -> {name}\n{text}" for n, (name, text) in enumerate(assets, start=1))
    )
    code = run_codex(out, prompt, refs, "model_sheet")
    print(f"{character} model sheet: codex exit {code}")
    for name, _ in assets:
        print(f"  {name}: {'ok' if (out / name).exists() else 'MISSING'}")


def generate_portrait(character: str) -> None:
    """A 512x512 roster portrait in the shared retro framing (used for Tubman, whose
    shipped portrait was a flat vector placeholder)."""
    out = RAW_ROOT / character
    refs = [model_sheet_path(character), portrait_path("joan"), portrait_path("tesla"), STYLE_AUTHORITY]
    entry = __import__("roster_spec").CHARACTERS[character]
    prompt = (
        "Use the imagegen skill with its built-in image_gen tool (do NOT use the CLI fallback "
        "script). Make exactly 2 image_gen calls (two variants of the same asset). After each call, "
        "copy the generated PNG from $CODEX_HOME/generated_images into the current working "
        f"directory as {character}_portrait_v1.png and {character}_portrait_v2.png (byte-for-byte). "
        "Then print one line per file: \"<file name> <- <source path>\". Do nothing else.\n\n"
        "Attached reference images (references only, never edit targets):\n"
        f"- Image 1: {entry['name']}'s character model sheet - the exact design to match.\n"
        "- Image 2 and Image 3: two existing roster portraits - match their framing, crop, "
        "lighting and rendering exactly (head and shoulders, three-quarter view, transparent "
        "background).\n"
        "- Image 4: the game's retro art-style authority mockup.\n\n"
        "Use case: stylized-concept\n"
        "Asset type: square character-select portrait for a 2D fighting game roster\n"
        "Canvas: square 1:1.\n"
        f"Subject: {entry['identity']}; head and shoulders, three-quarter view turned toward "
        "screen-right, calm and resolute expression, the warm glow of her brass lantern lighting "
        "her face from below-right.\n"
        "Style: retro pulp adventure-comic portrait matching Images 2 and 3: confident hand-inked "
        "black contour lines, fine engraved hatching, subtle halftone print texture, rich painted "
        "colour. Not pixel art, not 3D, not anime.\n"
        "Backdrop: fully transparent background (a real alpha channel), no colour fill.\n"
        "Constraints: no text, no border, no frame, no watermark; the bust fills the canvas like "
        "Images 2 and 3."
    )
    code = run_codex(out, prompt, refs, "portrait")
    print(f"{character} portrait: codex exit {code}")


def references(character: str) -> list[Path]:
    sheet = model_sheet_path(character)
    if not sheet.exists():
        sys.exit(f"{character}: model sheet missing at {sheet}")
    return [sheet, portrait_path(character), STYLE_AUTHORITY]


def generate_jobs(character: str, jobs: list[str]) -> None:
    """The first, one-animation-per-sheet job list (still used for idle and run)."""
    workdir = character_dir(character)
    refs = references(character)
    by_name = {job["job"]: job for job in JOBS}
    assets = [(f"{character}__{name}.png", image_prompt(character, by_name[name])) for name in jobs]
    code = run_codex(workdir, codex_session(character, assets, [str(r) for r in refs]), refs,
                     "_".join(jobs) if len(jobs) <= 3 else f"{jobs[0]}_plus{len(jobs) - 1}")
    print(f"{character} {jobs}: codex exit {code}")
    for name, _ in assets:
        ok = (workdir / name).exists()
        print(f"  {name}: {'ok' if ok else 'MISSING'}")
        if ok:
            write_legacy_sidecar(character, name.split("__", 1)[1][:-4])


def write_legacy_sidecar(character: str, job_name: str) -> None:
    image = character_dir(character) / f"{character}__{job_name}.png"
    if image.exists():
        sidecar_path(image).write_text(
            json.dumps(legacy_sidecar(character, job_name), indent=1), encoding="utf-8")


def generate_vfx(characters: list[str]) -> None:
    """One portrait VFX sheet per character (vfx_spec.py), all in one session."""
    from vfx_spec import vfx_prompt
    workdir = RAW_ROOT / "_vfx"
    workdir.mkdir(parents=True, exist_ok=True)
    refs = [STYLE_AUTHORITY]
    assets = []
    for character in characters:
        old = REPO / "assets/sprites/vfx" / character / f"{character}_ability_vfx_atlas.png"
        name = __import__("roster_spec").CHARACTERS[character]["name"]
        assets.append((f"{character}__vfx.png", vfx_prompt(character, name)
                       + f"\nReference: the attached image `{old.name}` is the old low-resolution version "
                         "of these same four effects in the same row order; keep each effect's idea "
                         "and framing but redraw it at much higher quality in the retro pulp style "
                         "(ignore any flat vector look in it)."))
        refs.append(old)
    prompt = (
        "Use the imagegen skill with its built-in image_gen tool (do NOT use the CLI fallback "
        f"script). Make exactly {len(assets)} image_gen calls, one per asset below, in order, each "
        "with a transparent background. After each call, copy the generated PNG from "
        "$CODEX_HOME/generated_images into the current working directory under that asset's file "
        "name (byte-for-byte). Then print one line per asset: \"<file name> <- <source path>\". "
        "Do nothing else.\n\nAttached reference images (references only, never edit targets): "
        "Image 1 is the game's retro art-style authority mockup; each later image is the old VFX "
        "atlas named in its asset.\n\n"
        + "\n\n".join(f"Asset {n} -> {file}\n{text}" for n, (file, text) in enumerate(assets, start=1))
    )
    code = run_codex(workdir, prompt, refs, "vfx_" + "_".join(characters))
    print(f"vfx {characters}: codex exit {code}")
    for file, _ in assets:
        print(f"  {file}: {'ok' if (workdir / file).exists() else 'MISSING'}")


def generate_constructs() -> None:
    from vfx_spec import construct_prompt
    workdir = RAW_ROOT / "_constructs"
    workdir.mkdir(parents=True, exist_ok=True)
    prompt = (
        "Use the imagegen skill with its built-in image_gen tool (do NOT use the CLI fallback "
        "script). Make exactly 1 image_gen call with a transparent background, copy the generated "
        "PNG from $CODEX_HOME/generated_images into the current working directory as "
        "constructs.png (byte-for-byte), and print \"constructs.png <- <source path>\". Do nothing "
        "else.\n\nAttached reference image: the game's retro art-style authority mockup.\n\n"
        + construct_prompt()
    )
    code = run_codex(workdir, prompt, [STYLE_AUTHORITY], "constructs")
    print(f"constructs: codex exit {code}; {'ok' if (workdir / 'constructs.png').exists() else 'MISSING'}")


def next_sheet_number(character: str) -> int:
    numbers = [int(p.stem.split("__p")[1]) for p in character_dir(character).glob(f"{character}__p*.png")
               if p.stem.split("__p")[1].isdigit()]
    return max(numbers, default=0) + 1


def generate_planned(character: str, count: int, canvas: str, size_note: str) -> None:
    sheets = plan_sheets(RAW_ROOT, character)
    own = [s["own"] for s in sheets if "own" in s]
    dense = [s for s in sheets if "own" not in s]
    if own:
        generate_jobs(character, own[:count])
        count -= len(own[:count])
    if count <= 0 or not dense:
        return
    workdir = character_dir(character)
    number = next_sheet_number(character)
    assets, sidecars = [], {}
    for sheet in dense[:count]:
        name = f"{character}__p{number:02d}.png"
        side = sheet_sidecar(character, sheet)
        side["canvas"] = canvas
        sidecars[name] = side
        assets.append((name, sheet_prompt(character, side, canvas)))
        number += 1
    refs = references(character)
    code = run_codex(workdir, codex_session(character, assets, [str(r) for r in refs], size_note),
                     refs, f"planned_{assets[0][0][:-4].split('__')[1]}_n{len(assets)}")
    print(f"{character} planned x{len(assets)}: codex exit {code}")
    for name, _ in assets:
        ok = (workdir / name).exists()
        print(f"  {name}: {'ok' if ok else 'MISSING'}")
        if ok:
            sidecar_path(workdir / name).write_text(json.dumps(sidecars[name], indent=1),
                                                    encoding="utf-8")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("character")
    parser.add_argument("jobs", nargs="+")
    parser.add_argument("--variants", type=int, default=1)
    parser.add_argument("--canvas", default="landscape 3:2")
    parser.add_argument("--size-note", default="")
    args = parser.parse_args()
    if args.jobs == ["model_sheet"]:
        generate_model_sheet(args.character, args.variants)
        return
    if args.jobs == ["portrait"]:
        generate_portrait(args.character)
        return
    if args.jobs == ["legacy_sidecars"]:
        for job in JOB_NAMES:
            write_legacy_sidecar(args.character, job)
        return
    if args.jobs == ["plan"]:
        for sheet in plan_sheets(RAW_ROOT, args.character):
            if "own" in sheet:
                print(f"  own sheet: {sheet['own']}")
            else:
                poses = ", ".join(f"{a}{i}" for a, i, _ in sheet["poses"])
                print(f"  {sheet['shape']} ({len(sheet['poses'])}): {poses}")
        return
    if args.jobs[0] == "next":
        generate_planned(args.character, int(args.jobs[1]), args.canvas, args.size_note)
        return
    if args.jobs == ["vfx"]:
        generate_vfx(args.character.split(","))
        return
    if args.character == "constructs":
        generate_constructs()
        return
    jobs = args.jobs
    if jobs == ["all"]:
        jobs = JOB_NAMES
    elif jobs == ["missing"]:
        workdir = character_dir(args.character)
        jobs = [j for j in JOB_NAMES if not (workdir / f"{args.character}__{j}.png").exists()]
    unknown = [j for j in jobs if j not in JOB_NAMES]
    if unknown:
        sys.exit(f"unknown jobs: {unknown}")
    if jobs:
        generate_jobs(args.character, jobs)


if __name__ == "__main__":
    main()
