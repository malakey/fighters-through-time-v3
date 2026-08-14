#!/usr/bin/env python3
"""Install generated enemy/boss atlases and author their Godot resources.

Run from the V3 project root after copying the staged retro_enemy_sprites folder
to a readable location. The script is deterministic and only updates the
SpriteFramesResource line plus the required ext_resource/load_steps metadata in
each EnemyData/BossData file.
"""

from __future__ import annotations

import argparse
import re
import shutil
from pathlib import Path

from PIL import Image


ENEMY_ROWS = ("idle", "patrol", "attack", "elite_attack", "hitstun", "death")
BOSS_ROWS = ("idle", "move", "melee_attack", "ranged_attack", "phase_transition", "death")


def ability_ids(project: Path, kind: str, owner: str) -> list[str]:
    data_text = (project / "resources" / kind / f"{owner}.tres").read_text(encoding="utf-8")
    relative_paths = re.findall(
        rf'path="res://resources/{kind}/Abilities/{re.escape(owner)}/([^"]+\.tres)"',
        data_text,
    )
    root = project / "resources" / kind / "Abilities" / owner
    ids: list[str] = []
    for relative_path in relative_paths:
        path = root / relative_path
        match = re.search(r'^PresentationEventID\s*=\s*"([^"]+)"', path.read_text(encoding="utf-8"), re.M)
        if match:
            ids.append(match.group(1))
    if ids:
        return ids
    return [f"{owner}.basic"]


def sprite_frames(texture_path: str, rows: list[str] | tuple[str, ...], cell_w: int,
                  cell_h: int, loop_rows: set[str]) -> str:
    blocks: list[str] = [
        f'[gd_resource type="SpriteFrames" load_steps={2 + len(rows) * 3} format=3]',
        "",
        f'[ext_resource type="Texture2D" path="{texture_path}" id="1_atlas"]',
        "",
    ]
    for row_index, name in enumerate(rows):
        for frame in range(3):
            frame_id = f"frame_{row_index}_{frame}"
            blocks += [
                f'[sub_resource type="AtlasTexture" id="{frame_id}"]',
                'atlas = ExtResource("1_atlas")',
                f'region = Rect2({frame * cell_w}, {row_index * cell_h}, {cell_w}, {cell_h})',
                "",
            ]
    animations: list[str] = []
    for row_index, name in enumerate(rows):
        frame_refs = ", ".join(
            f'{{"duration": 1.0, "texture": SubResource("frame_{row_index}_{frame}")}}'
            for frame in range(3)
        )
        animations.append(
            "{\n"
            f'"frames": [{frame_refs}],\n'
            f'"loop": {str(name in loop_rows).lower()},\n'
            f'"name": &"{name}",\n'
            f'"speed": {8.0 if name in loop_rows else 10.0}\n'
            "}"
        )
    blocks += ["[resource]", "animations = [", ", ".join(animations), "]", ""]
    return "\n".join(blocks)


def patch_data(path: Path, resource_path: str) -> None:
    text = path.read_text(encoding="utf-8")
    existing = re.search(
        rf'^\[ext_resource type="SpriteFrames" path="{re.escape(resource_path)}" id="(\d+)"\]$',
        text, re.M,
    )
    if existing:
        resource_id = int(existing.group(1))
    else:
        match = re.search(r"load_steps=(\d+)", text)
        if not match:
            raise ValueError(f"No load_steps in {path}")
        text = text[:match.start(1)] + str(int(match.group(1)) + 1) + text[match.end(1):]
        ext_ids = [int(value) for value in re.findall(r'id="(\d+)"', text)]
        resource_id = max(ext_ids, default=1) + 1
        ext = f'[ext_resource type="SpriteFrames" path="{resource_path}" id="{resource_id}"]\n'
        insert = text.index("\n\n[resource]")
        text = text[:insert] + "\n" + ext + text[insert:]
    line = f'SpriteFramesResource = ExtResource("{resource_id}")'
    if re.search(r"^SpriteFramesResource\s*=.*$", text, re.M):
        text = re.sub(r"^SpriteFramesResource\s*=.*$", line, text, flags=re.M)
    else:
        marker = re.search(r"^(EnemyID|BossID)\s*=.*$", text, re.M)
        if not marker:
            raise ValueError(f"No identity field in {path}")
        text = text[:marker.end()] + "\n" + line + text[marker.end():]
    path.write_text(text, encoding="utf-8", newline="\n")


def install_owner(project: Path, stage: Path, owner: str, kind: str) -> None:
    boss = kind == "Bosses"
    cell_w, cell_h = (192, 256) if boss else (96, 128)
    body_rows = BOSS_ROWS if boss else ENEMY_ROWS
    source = stage / owner
    body = source / f"{owner}_animation_atlas.png"
    vfx = source / f"{owner}_ability_vfx_atlas.png"
    if not body.exists() or not vfx.exists():
        raise FileNotFoundError(f"Missing final atlases for {owner}")
    expected_body = (cell_w * 3, cell_h * 6)
    with Image.open(body) as image:
        if image.size != expected_body or image.mode != "RGBA":
            raise ValueError(f"{body}: expected RGBA {expected_body}, got {image.mode} {image.size}")
        validate_alpha(image, body)

    events = ability_ids(project, kind, owner)
    expected_vfx = (576, 192 * len(events))
    with Image.open(vfx) as image:
        if image.size != expected_vfx or image.mode != "RGBA":
            raise ValueError(f"{vfx}: expected RGBA {expected_vfx}, got {image.mode} {image.size}")
        validate_alpha(image, vfx)

    art_dir = project / "assets" / "sprites" / kind.lower() / owner / "retro"
    art_dir.mkdir(parents=True, exist_ok=True)
    shutil.copy2(body, art_dir / body.name)
    shutil.copy2(vfx, art_dir / vfx.name)

    frames_dir = project / "resources" / "SpriteFrames" / kind
    frames_dir.mkdir(parents=True, exist_ok=True)
    body_res = frames_dir / f"{owner}_frames.tres"
    body_texture = f"res://assets/sprites/{kind.lower()}/{owner}/retro/{body.name}"
    body_res.write_text(sprite_frames(body_texture, body_rows, cell_w, cell_h,
                                      {"idle", "patrol", "move"}), encoding="utf-8", newline="\n")

    vfx_res = frames_dir / f"{owner}_ability_vfx_frames.tres"
    vfx_texture = f"res://assets/sprites/{kind.lower()}/{owner}/retro/{vfx.name}"
    vfx_res.write_text(sprite_frames(vfx_texture, events, 192, 192, set()),
                       encoding="utf-8", newline="\n")

    data_root = project / "resources" / kind
    patch_data(data_root / f"{owner}.tres", f"res://resources/SpriteFrames/{kind}/{owner}_frames.tres")


def validate_alpha(image: Image.Image, path: Path) -> None:
    alpha = image.getchannel("A")
    low, high = alpha.getextrema()
    if low != 0 or high != 255:
        raise ValueError(f"{path}: invalid alpha extrema {(low, high)}")
    corners = (
        alpha.getpixel((0, 0)), alpha.getpixel((image.width - 1, 0)),
        alpha.getpixel((0, image.height - 1)),
        alpha.getpixel((image.width - 1, image.height - 1)),
    )
    if any(value != 0 for value in corners):
        raise ValueError(f"{path}: non-transparent corner alpha {corners}")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project", required=True, type=Path)
    parser.add_argument("--stage", required=True, type=Path)
    args = parser.parse_args()
    project, stage = args.project.resolve(), args.stage.resolve()

    enemies = sorted(path.stem for path in (project / "resources" / "Enemies").glob("*.tres"))
    bosses = sorted(path.stem for path in (project / "resources" / "Bosses").glob("*.tres") if path.stem != "mirror_paradox")
    for owner in enemies:
        install_owner(project, stage, owner, "Enemies")
    for owner in bosses:
        install_owner(project, stage, owner, "Bosses")
    print(f"Installed {len(enemies)} enemies and {len(bosses)} bosses ({len(enemies) + len(bosses)} identities).")


if __name__ == "__main__":
    main()
