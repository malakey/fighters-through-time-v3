"""Package 12 W3 one-shot: author the M08 hit-contract fields on all 36 ability .tres.

Values are derived from what the build does today (see docs/handoffs/P12_W3.md):
  HitstunFrames = round(HitstunDuration * 60) (0.2 s default -> 12)
  BlockClass    = Unblockable for the Ultimate slot, Special otherwise (BaseSpecial's slot rule)
  Origin        = Ultimate for the Ultimate slot, Special otherwise
  Delivery      = Construct for PersistentObject executions, Tick for the two
                  PlaceholderZone-tick abilities, DirectHit otherwise
  Launches      = true where the authored knockback vector is non-zero (today every
                  knockback hit launches); W3b owns the semantics and the pruning.
Idempotent: re-running rewrites the same lines.
"""
import pathlib
import re

ROOT = pathlib.Path(__file__).resolve().parents[1] / "resources" / "Abilities"
TICK_ABILITIES = {"einstein_relativity_rift", "shakespeare_the_tempest", "cleopatra_sandstorm_vortex"}
# BlockClass / HitDelivery / HitOrigin ordinals (scripts/Combat/HitboxSystem.cs).
BLOCK_SPECIAL, BLOCK_UNBLOCKABLE = 1, 3
DELIVERY_DIRECT, DELIVERY_TICK, DELIVERY_CONSTRUCT = 0, 1, 2
ORIGIN_SPECIAL, ORIGIN_ULTIMATE = 1, 2
NEW_KEYS = ("HitstunFrames", "BlockClass", "Launches", "Delivery", "Origin")


def field(text, name, default=None):
    m = re.search(rf"^{name} = (.+)$", text, re.M)
    return m.group(1).strip() if m else default


def main():
    files = sorted(ROOT.glob("*/*.tres"))
    assert len(files) == 36, len(files)
    for path in files:
        text = path.read_text(encoding="utf-8")
        ability_id = field(text, "AbilityID").strip('"')
        slot = int(field(text, "Slot", "0"))
        execution = int(field(text, "ExecutionType", "0"))
        hitstun_seconds = float(field(text, "HitstunDuration", "0.2"))
        authored_frames = field(text, "HitstunFrames")
        knockback = field(text, "KnockbackForce", "Vector2(3, -2)")
        inner = knockback[knockback.index("(") + 1:knockback.rindex(")")]
        kx, ky = (float(v) for v in inner.split(","))

        ultimate = slot == 3
        values = {
            # A re-run keeps an already-authored frame count (the seconds field is gone by then).
            "HitstunFrames": authored_frames or str(round(hitstun_seconds * 60)),
            "BlockClass": str(BLOCK_UNBLOCKABLE if ultimate else BLOCK_SPECIAL),
            "Launches": "true" if (kx != 0 or ky != 0) else "false",
            "Delivery": str(DELIVERY_CONSTRUCT if execution == 3
                            else DELIVERY_TICK if ability_id in TICK_ABILITIES
                            else DELIVERY_DIRECT),
            "Origin": str(ORIGIN_ULTIMATE if ultimate else ORIGIN_SPECIAL),
        }

        lines = [ln for ln in text.splitlines()
                 if not ln.startswith("HitstunDuration = ")
                 and not any(ln.startswith(k + " = ") for k in NEW_KEYS)]
        # Insert the block straight after DamageTickIntervalFrames (or HitCount).
        anchor = next((i for i, ln in enumerate(lines) if ln.startswith("DamageTickIntervalFrames = ")), None)
        if anchor is None:
            anchor = next(i for i, ln in enumerate(lines) if ln.startswith("BaseDamage = "))
        block = [f"{k} = {values[k]}" for k in NEW_KEYS]
        lines[anchor + 1:anchor + 1] = block
        eol = "\r\n" if "\r\n" in text else "\n"
        with open(path, "w", encoding="utf-8", newline="") as handle:
            handle.write(eol.join(lines) + eol)
        print(f"{ability_id:40s} {values}")


if __name__ == "__main__":
    main()
