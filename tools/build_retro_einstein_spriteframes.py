"""Rebuild Einstein's SpriteFrames resources.

Superseded by ``build_retro_character_spriteframes.py`` (Package 12 W10). This
script used to carry its own 26-name animation table and its own VFX speeds,
so running it after the roster builder silently regressed Einstein's
resource: it dropped ``up_attack``/``down_air`` and the H03 ``grab``/``throw``
rows the 30-name contract requires. It now delegates to the roster builder so
there is exactly one animation table.
"""

from build_retro_character_spriteframes import build


if __name__ == "__main__":
    build(["einstein"])
