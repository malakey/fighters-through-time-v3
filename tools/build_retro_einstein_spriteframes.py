from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SPRITE_FRAMES = ROOT / "resources" / "SpriteFrames"


CHARACTER_SHEETS = {
    "locomotion": "res://assets/sprites/characters/einstein/retro/einstein_locomotion_atlas.png",
    "traversal": "res://assets/sprites/characters/einstein/retro/einstein_traversal_atlas.png",
    "combat": "res://assets/sprites/characters/einstein/retro/einstein_combat_atlas.png",
    "states": "res://assets/sprites/characters/einstein/retro/einstein_states_atlas.png",
    "abilities": "res://assets/sprites/characters/einstein/retro/einstein_abilities_atlas.png",
}

CHARACTER_ANIMATIONS = [
    ("idle", "locomotion", 0, 6.0, True),
    ("run", "locomotion", 1, 12.0, True),
    ("jump", "locomotion", 2, 8.0, False),
    ("fall", "locomotion", 3, 8.0, True),
    ("skid", "locomotion", 4, 12.0, False),
    ("crouch", "locomotion", 5, 4.0, True),
    ("roll_startup", "traversal", 0, 12.0, False),
    ("roll", "traversal", 1, 12.0, False),
    ("roll_recovery", "traversal", 2, 12.0, False),
    ("ledge_hang", "traversal", 3, 3.0, True),
    ("ledge_pull_up", "traversal", 4, 8.0, False),
    ("ledge_drop", "traversal", 5, 8.0, False),
    ("basic_attack_1", "combat", 0, 8.0, False),
    ("basic_attack_2", "combat", 1, 8.0, False),
    ("basic_attack_3", "combat", 2, 7.0, False),
    ("block", "combat", 3, 3.0, True),
    ("hitstun", "combat", 4, 8.0, False),
    ("dazed", "states", 0, 4.0, True),
    ("death", "states", 1, 6.0, False),
    ("respawn", "states", 2, 8.0, False),
    ("victory", "states", 3, 6.0, True),
    ("defeat", "states", 4, 4.0, True),
    ("special_1", "abilities", 0, 8.0, False),
    ("special_2", "abilities", 1, 8.0, False),
    ("movement_ability", "abilities", 2, 10.0, False),
    ("ultimate", "abilities", 3, 8.0, False),
]

VFX_SHEET = "res://assets/sprites/vfx/einstein/einstein_ability_vfx_atlas.png"
VFX_ANIMATIONS = [
    ("mass_energy_projectile", 0, 12.0, False),
    ("relativity_rift", 1, 8.0, True),
    ("relativity_warp", 2, 12.0, False),
    ("cosmological_constant", 3, 8.0, True),
]


def animation_block(name: str, frame_ids: list[str], speed: float, loop: bool) -> str:
    frames = ", ".join(
        f'{{"duration": 1.0, "texture": SubResource("{frame_id}")}}'
        for frame_id in frame_ids
    )
    return (
        "{\n"
        f'"frames": [{frames}],\n'
        f'"loop": {str(loop).lower()},\n'
        f'"name": &"{name}",\n'
        f'"speed": {speed:.1f}\n'
        "}"
    )


def write_character_resource() -> None:
    lines = ["[gd_resource type=\"SpriteFrames\" load_steps=84 format=3]", ""]
    sheet_ids = {sheet: str(index + 1) for index, sheet in enumerate(CHARACTER_SHEETS)}
    for sheet, path in CHARACTER_SHEETS.items():
        lines.append(
            f'[ext_resource type="Texture2D" path="{path}" id="{sheet_ids[sheet]}_{sheet}"]'
        )
    lines.append("")

    frame_ids: dict[str, list[str]] = {}
    for animation, sheet, row, _, _ in CHARACTER_ANIMATIONS:
        frame_ids[animation] = []
        for column in range(3):
            frame_id = f"{animation}_{column}"
            frame_ids[animation].append(frame_id)
            lines.extend(
                [
                    f'[sub_resource type="AtlasTexture" id="{frame_id}"]',
                    f'atlas = ExtResource("{sheet_ids[sheet]}_{sheet}")',
                    f"region = Rect2({column * 96}, {row * 128}, 96, 128)",
                    "",
                ]
            )

    lines.extend(["[resource]", "animations = ["])
    blocks = [
        animation_block(animation, frame_ids[animation], speed, loop)
        for animation, _, _, speed, loop in CHARACTER_ANIMATIONS
    ]
    lines.append(", ".join(blocks))
    lines.append("]")
    (SPRITE_FRAMES / "einstein_frames.tres").write_text("\n".join(lines) + "\n", encoding="utf-8")


def write_vfx_resource() -> None:
    lines = [
        "[gd_resource type=\"SpriteFrames\" load_steps=14 format=3]",
        "",
        f'[ext_resource type="Texture2D" path="{VFX_SHEET}" id="1_vfx"]',
        "",
    ]
    frame_ids: dict[str, list[str]] = {}
    for animation, row, _, _ in VFX_ANIMATIONS:
        frame_ids[animation] = []
        for column in range(3):
            frame_id = f"{animation}_{column}"
            frame_ids[animation].append(frame_id)
            lines.extend(
                [
                    f'[sub_resource type="AtlasTexture" id="{frame_id}"]',
                    'atlas = ExtResource("1_vfx")',
                    f"region = Rect2({column * 192}, {row * 192}, 192, 192)",
                    "",
                ]
            )
    lines.extend(["[resource]", "animations = ["])
    blocks = [
        animation_block(animation, frame_ids[animation], speed, loop)
        for animation, _, speed, loop in VFX_ANIMATIONS
    ]
    lines.append(", ".join(blocks))
    lines.append("]")
    (SPRITE_FRAMES / "einstein_ability_vfx_frames.tres").write_text(
        "\n".join(lines) + "\n",
        encoding="utf-8",
    )


write_character_resource()
write_vfx_resource()
