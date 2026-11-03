# GR-HAZ: hazards hit launched or thrown mobs (ruling 7d, plan section 1.13)

**Nobody has played this.** Every claim below is a headless filtered suite. No bot batch was run.

## What shipped

A mob that is thrown, launched or in a finisher flight now takes an impact from a hazard. A mob on its feet stays safe.

| Mob | Impact |
|---|---|
| Standard | 30 HP, or dies if it has 30 or less |
| Fodder | Dies |
| Elite | 25 % of its scaled max HP, and one impact never kills it |
| Boss | Never: `BossController` is a separate class with no launch latches, and no hazard calls it |

- **Once per launch per source.** Each new launch or throw clears the spent sources.
- **Not a hit.** No stun, hitstop, poise, meter, Momentum, Rally or D1a. No defence reduces it. It shows a floating number and a flash.
- **Refused without spending the source:** a dormant mob, a dead mob, a `DrillInvulnerable` mob, a mob the rewind or Time Freeze has frozen, and any mob while `ChronalRewindManager.IsWorldHeld` is true.
- **Defy:** a Defy push is not a launch and ends any launch it catches. A thrown flight it catches stays thrown, because the blast does not clear `_thrownFlight`.
- **Hazards that qualify:** spikes, `StoryCyclicHazard`, `StageHazard`, the Extractor discharge, the flood and the escape front (lava and firestorm).
- **Hero-only, untouched:** the searchlight and tremor debris.
- **Kills** run under `PhysicsCallbackGuard`, pay ledger dust through the ordinary `EnemyKilled` path, count for encounter locks, and survive a rewind.

## API

- `FTT.Combat.HazardMobRules` (pure):
  - Constants `StandardImpactDamage` 30, `EliteImpactMaxHPFraction` 0.25, `FodderEnemyID` `"chrono_swarmling"`.
  - Methods `IsFodder(id)`, `IsVulnerable(thrown, wallBounceArmed, finisherFlight, dormant, alive)`, `Classify(isElite, id)`, `ImpactDamage(class, currentHP, maxHP)`.
  - Enum `HazardMobClass { Standard, Fodder, Elite }`.
- `EnemyController.Hazard.cs`: `IsHazardVulnerable`, `ApplyHazardImpact(ulong sourceId)` (returns the HP taken), `KilledByHazard`.
- `EnemyController.cs`: two call lines only, in `ResetLaunchCarry` and `ResetGrabAndSlamState`.
- `CollisionLayers.HazardContactMask` = `Player | Enemy`. No new layer.
- Telemetry: `PlaytestTelemetry.HazardKills` and `ThrownKills`, reported as `enemy_summary.hazard_kills` and `thrown_kills`.

## Provisional numbers and agent choices (for the ledger)

- 30, 25 % and the elite's rounding (half away from zero, clamped to leave 1 HP). The ruling said "big damage".
- 30 is not scaled by difficulty, so a Hard mob with scaled-up HP survives more often.
- The flood hits on the first tick a flagged mob is below the water line, with no drown grace.
- `StageHazard` pays one impact, not its damage per second.
- `VolleyCover` shelters a mob as it shelters the hero, and does not spend the source.
- An impact shows a number and a flash but no camera shake (a wall impact does shake).
- `thrown_kills` counts a mob that dies during a thrown flight, by any cause. A throw whose own damage kills the mob launches nothing and does not count.

## Tests

Declared +23, actual **+23**. The `[TestCase]` attribute count under `tests/` moved from 4028 to 4051.

| Suite | Cases | Runtime |
|---|---|---|
| `HazardMobRulesTests` | 6 | Pure C# |
| `HazardMobVulnerabilityTests` | 6 | Engine |
| `HazardMobContactTests` | 8 | Engine |
| `HazardMobCreditTests` | 3 | Engine |

- `HazardMobTestKit` is a non-suite helper.
- `HazardMobContactTests` has one walking-mob regression per hazard type (cyclic, base spikes, Florence spikes, stage hazard, extractor, flood, lava), plus the cover and Mirror clone case.
- Changed in place, no count change: `CollisionLayerTests` (the mask pin), `EraMechanicToolkitTests.EraMechanicTemplatesUseTheDocumentedRootTypesAndTriggerLayers` (the flood's mask).

**Seen failing:**
- Stub rule: 21 of 25 failed, 2 of those 4 survivors being the old `CollisionLayerTests` cases.
- Real rule with no hazard hunks: all seven hazard-type cases failed with `'30' but is '0'`. The other two failures were test bugs, fixed afterwards: the cover case's spot was outside the shelter, and the burn multiplied the setup damage in the elite case.
- `IsVulnerable` forced true: 15 of 23 failed, including every walking regression and the two cases the stub could not fail.
- `IsWorldHeld` half removed: the hold assertion failed with `'0' but is '30'`.

**Final filtered run:** 32 suites, `Failed: 0, Passed: 493, Total: 493`, against 493 attributes counted in those files. The full suite is the orchestrator's.

## Hash, save and protocol effects

None. Nothing under `scripts/FighterSim/` changed or names this rule. Save schema v8, protocol v5, no component ID, no translation key.

## Deviations from the plan

1. **A `Level01Controller` hunk, outside my matrix row.** Florence has its own private spike builder, so the base's `BuildHazardSpikes` hunk does not reach it, and the L1 showcase depends on this. Two changed lines and one added branch.
2. **`EraMechanicToolkitTests` changed in place.** The plan named only `CollisionLayerTests`, but this test pinned the flood's mask to Player.
3. **Masks are set in `_Ready` through the constant.** The hazard templates still author `collision_mask = 1`, and `_Ready` overrides it. This also covers hazards built with `new`.
4. **A guard for the Mirror clone, not in the plan.** The clone is a `PlayerController` on the Enemy layer, so the wider mask would have reached it and the old `body is PlayerController` checks would have hurt it. Each hazard skips `IsStoryHostile`, except Florence's spikes, which never meet the clone.
5. **The impact takes HP directly instead of going through the wall-impact `TakeDamage`.** That path's status multiplier (Radiant Burn) could kill an elite the rule has just left alive. It repeats a few lines of `TakeDamage`, but it also keeps the plan's "hazards unreduced" for GR-TRAITS with no extra work.
6. **Fodder is keyed by ID.** GR-CROWD's flags are not in this base. `HazardMobRules.IsFodder` keys on `chrono_swarmling`, the plan's section 1.11 ID. It needs no merge change as long as GR-CROWD uses that ID. If the orchestrator prefers the data flag, that one rule and `Classify`'s caller are the only places to change.

## Known limits

- **Spikes are `BodyEntered` only.** A mob already standing in a strip when it is launched is not hit until it enters again. Falling back in while still stunned does count.
- **The Florence spikes case drives the strip's own signal, not physics,** because booting that level pauses the tree on its entrance dialogue. Its mask is asserted separately.
- **`StageHazard` is in no scene today,** so that hunk has no player-visible effect yet.
- **Florence's steam geyser was not traced.** Level 1's code mentions it only in comments, and I did not find what builds it. GR-LV1 should check it before the showcase relies on it.
- **The telemetry counters have no test of their own.** They compile, and the corpse flags they read are asserted in `HazardMobCreditTests`. No bot run has exercised them. Bots never grab, so `thrown_kills` will read near zero (plan section 5).

## What did not ship

- Mob hazard damage in telemetry (the dossier suggested it; the plan names only the two kill counters).
- `compare_runs.py` and `run_playtests.ps1` columns for the counters. GR-CROWD owns `compare_runs.py`.
- Any level content. The L1 showcase is GR-LV1's.

## Merge notes

- `PlaytestTelemetry.cs` is GR-CROWD's. My hunk is its own commit (three small places: a field pair, the `KilledFrame` block, the `["killed"]` line in `enemy_summary`). Expect a conflict only if GR-CROWD edits those exact lines.
- GR-TRAITS: an Armoured mob's reduction cannot touch hazards, because the impact bypasses every reduction path.
- GR-CROWD: `chrono_swarmling` must keep that exact ID, or fodder falls back to the Standard rule silently.

## What the docs owe (GR-DOCS)

- AGENTS.md: a hazards-versus-mobs bullet under Combat, the new partial and rule in the repository map, the baseline arithmetic, and a correction to the older statement that hazards are hero-only.
- Ledger: a user row for 7d and `PROVISIONAL-HAZARD-MOB-RULES` with the numbers and choices above.
- `design-godot.md` master and mirror: the 7d rule.
- CLAUDE.md: the two new `enemy_summary` fields under "Reading a report".
