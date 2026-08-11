"""Build every roster character's SpriteFrames resources from fixed-size retro atlases."""

from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SPRITE_FRAMES = ROOT / "resources" / "SpriteFrames"
SPRITE_FRAMES.mkdir(parents=True, exist_ok=True)

ROSTER_ABILITIES = {
    "einstein": (
        "mass_energy_projectile", "relativity_rift",
        "relativity_warp", "cosmological_constant",
    ),
    "joan": (
        "joan_righteous_smite", "joan_divine_piercing",
        "joan_ascendant_wings", "joan_grand_crusade",
    ),
    "leonardo": (
        "leonardo_golden_ratio", "leonardo_clockwork_turret",
        "leonardo_ornithopter_flight", "leonardo_vitruvian_matrix",
    ),
    "lincoln": (
        "lincoln_emancipator", "lincoln_splitting_strike",
        "lincoln_rail_charge", "lincoln_union_indestructible",
    ),
    "cleopatra": (
        "cleopatra_serpent_nest", "cleopatra_sandstorm_vortex",
        "cleopatra_desert_mirage", "cleopatra_wrath_of_the_nile",
    ),
    "tesla": (
        "tesla_tesla_coil", "tesla_lorentz_pulse",
        "tesla_lightning_blink", "tesla_wardenclyffe_cataclysm",
    ),
    "shakespeare": (
        "shakespeare_yoricks_lament", "shakespeare_the_tempest",
        "shakespeare_prosperos_flight", "shakespeare_all_the_worlds_a_stage",
    ),
    "mozart": (
        "mozart_requiem_chord", "mozart_fortissimo_wave",
        "mozart_sonata_drift", "mozart_symphony_of_sorrow",
    ),
    "pocahontas": (
        "pocahontas_spirit_strike", "pocahontas_vine_snare",
        "pocahontas_breeze_glide", "pocahontas_tidewater_tempest",
    ),
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


def write_character_resource(character_id: str) -> None:
    sheets = {
        sheet: f"res://assets/sprites/characters/{character_id}/retro/{character_id}_{sheet}_atlas.png"
        for sheet in ("locomotion", "traversal", "combat", "states", "abilities")
    }
    lines = ['[gd_resource type="SpriteFrames" load_steps=84 format=3]', ""]
    sheet_ids = {sheet: str(index + 1) for index, sheet in enumerate(sheets)}
    for sheet, path in sheets.items():
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
            lines.extend([
                f'[sub_resource type="AtlasTexture" id="{frame_id}"]',
                f'atlas = ExtResource("{sheet_ids[sheet]}_{sheet}")',
                f"region = Rect2({column * 96}, {row * 128}, 96, 128)",
                "",
            ])

    lines.extend(["[resource]", "animations = ["])
    lines.append(", ".join(
        animation_block(animation, frame_ids[animation], speed, loop)
        for animation, _, _, speed, loop in CHARACTER_ANIMATIONS
    ))
    lines.append("]")
    (SPRITE_FRAMES / f"{character_id}_frames.tres").write_text(
        "\n".join(lines) + "\n", encoding="utf-8"
    )


def write_vfx_resource(character_id: str, animations: tuple[str, ...]) -> None:
    sheet_path = f"res://assets/sprites/vfx/{character_id}/{character_id}_ability_vfx_atlas.png"
    lines = [
        '[gd_resource type="SpriteFrames" load_steps=14 format=3]', "",
        f'[ext_resource type="Texture2D" path="{sheet_path}" id="1_vfx"]', "",
    ]
    frame_ids: dict[str, list[str]] = {}
    for row, animation in enumerate(animations):
        frame_ids[animation] = []
        for column in range(3):
            frame_id = f"ability_{row}_{column}"
            frame_ids[animation].append(frame_id)
            lines.extend([
                f'[sub_resource type="AtlasTexture" id="{frame_id}"]',
                'atlas = ExtResource("1_vfx")',
                f"region = Rect2({column * 192}, {row * 192}, 192, 192)",
                "",
            ])
    lines.extend(["[resource]", "animations = ["])
    lines.append(", ".join(
        animation_block(animation, frame_ids[animation], 10.0, row in (1, 3))
        for row, animation in enumerate(animations)
    ))
    lines.append("]")
    (SPRITE_FRAMES / f"{character_id}_ability_vfx_frames.tres").write_text(
        "\n".join(lines) + "\n", encoding="utf-8"
    )


for roster_id, ability_animations in ROSTER_ABILITIES.items():
    write_character_resource(roster_id)
    write_vfx_resource(roster_id, ability_animations)
