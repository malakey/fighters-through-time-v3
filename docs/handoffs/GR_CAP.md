# GR-CAP: the nine Resonance capstones (rulings 3a+++ and 3a++++, Q1)

**Nobody has played this, or looked at it.** Every claim below is a headless filtered suite. No bot batch was run, and no capture of the ten-chip grids was taken; the fit tests at UI scale 1.0 and 1.4 are the only evidence of the layout.

## What shipped

Every hero's grid has a tenth node: `Type = Major`, `Tier = 4`, `UnlockCost = 375`, `PrerequisiteMode` All of two of that hero's Majors. A grid now costs **1,350**. All effects are Story only, each behind `HasStoryPerk`.

| Hero | Node ID (perk key) | Prerequisites | `GatedAbilityID` | What it does in the build |
|---|---|---|---|---|
| Joan | `joan_banner_of_orleans` (`banner_of_orleans`) | Unstoppable Crusade, Shield of Orleans | special1 | Righteous Smite's wave drops a pooled zone every 50 px. Each ticks 2 damage and Radiant Burn. |
| Einstein | `einstein_unified_field` (`unified_field`) | Event Horizon, Quantum Entanglement | special2 | Warp's arrival calls the Rift's own `TryCollapse` at its centre, wherever he lands. |
| Leonardo | `leonardo_codex_atlanticus` (`codex_atlanticus`) | Master Stroke, Clockwork Overdrive | special2 | Synced bolts fly on through the line, level; every turret holds one more charge. |
| Lincoln | `lincoln_emancipation` (`emancipation`) | Executive Order, Kinetic Splitting | movement | When a Rail Charge ends, a `StoryGroundWave` leaves its start, runs to where he stopped and 120 px past. 12 damage, grounded only. |
| Cleopatra | `cleopatra_dynastys_curse` (`dynastys_curse`) | Asp's Bite, Quicksand Grip | none | A mob that dies poisoned passes its Venom to the 4 nearest living mobs within 3 units, for 3 s, at its own intensity. |
| Tesla | `tesla_world_system` (`world_system`) | Resonant Overdrive, Lorentz Attraction | special2 | The discharge also reaches charged coils within 8 units of a coil already in it. A coil with no target pours into the last enemy struck. A landed discharge refunds a pip, once per Pulse. |
| Shakespeare | `shakespeare_curtain_call` (`curtain_call`) | Macbeth's Curse, Midsummer Gust | movement | The gust leaves a windbox halfway along its path for 90 frames, radius 2 units. It pushes, and opens mobs to hazards while they are in it and for 30 frames after. |
| Mozart | `mozart_symphony_no_41` (`symphony_no_41`) | Virtuoso Drift, Requiem Crescendo | special2 | A different Special within 6 s adds a stack, up to 3, each +10 % to his ability damage. 6 s with no Special clears them. |
| Tubman | `tubman_freedom_line` (`freedom_line`) | Safe Passage, North Star Ward | special2 | Each Foresight counter refunds a pip and grants a Resonance Barrier, whether or not the answer reaches anyone. |

Respec and `CalculateSpentDust` needed no change. `GridProgress` is a node-ID list, so there is no migration. The dormant gate reads `GatedAbilityID` whatever the tier.

## API

- `FTT.Combat.CapstoneRules` (pure, new): the nine perk-key constants, `Tier` and the numbers below. Also `SymphonyStack` (`NoteSpecial(slot)`, `Step()`, `Stacks`, `DamageScale`).
- `PlayerController.Capstones.cs` (new partial): `SymphonyStacks`, `NoteSymphonySpecial(slot)`, `RefundMomentumPip()`, `StepCapstonesForTest(frames)`, and the private `CapstoneDamageLane` and `StepCapstones`.
- `EnemyController.Capstones.cs` (new partial): `ArmHazardVulnerability(frames)` and the private `SpreadVenomOnDeath()`. `IsHazardVulnerable` also reads the windbox opening.
- `StoryShieldEffect.FreedomLine = 7` (appended), a Resonance Barrier source.
- `ConstructStrikes.TurretPiercedBoltHitboxID`.
- `PlaceholderProjectile.PiercesTargets` and `StopsOnLandedContact`.
- `LeonardoTurretNode.Initialize(..., bool codexAtlanticus = false)`.
- Test seams: `JoanRighteousSmite.BannerStripPieces`, `LincolnRailCharge.EmancipationWave`, `ShakespeareProsperosFlight.CurtainWindboxFramesRemaining` and `CurtainWindboxCentre`.
- `ShakespeareTheTempest.FindBody` went from private to internal.
- Hunks outside the kits: `PlayerController.cs` (one factor in `StoryAbilityHitboxDamage`), `PlayerController.Momentum.cs` (one call in `RegenMomentum`), `EnemyController.cs` (one call in `Die`, one reset line in `ResetGrabAndSlamState`), `EnemyController.Hazard.cs`, `Hitbox.cs` (`IsProjectileHitbox`), `StoryDefense.cs`.

## Provisional numbers (for `PROVISIONAL-CAPSTONE-EFFECTS`)

The user accepted the effects (Q1), not these numbers:

- **Banner:** spacing 50 px, radius 30 px, lifetime 1.5 s, tick 0.5 s, 2 damage.
- **Codex:** +1 charge.
- **Emancipation:** 12 damage, 900 px/s, 120 px overshoot, 0.33 s hitstun, knockback (2, 0).
- **Dynasty's Curse:** 3 units, 3 s, 4 targets, the dying mob's intensity.
- **World System:** chain range 8 units (reads `DischargeOwnerRangeUnits`), one pip.
- **Curtain Call:** 90 frames, 2 units, 30-frame opening, the Tempest's push per frame.
- **Symphony:** 360 frames, +10 % a stack, 3 stacks.
- The nine prerequisite pairs are the dossier's. The nine `GatedAbilityID` choices and every `LayoutPosition` are mine.

Agent readings of the wording:

- "A Venom kill" means any death while poisoned, not only a Venom tick.
- Unified Field fires wherever he lands, not only inside the Rift.
- Curtain Call's windbox stands at the path's midpoint and pushes away from its centre, not toward a hazard.
- The Symphony lane covers everything through `StoryAbilityHitboxDamage`, so the Ultimate and constructs too, but never a basic swing.
- A Hard Mirror clone mirrors these perks like any other, because it runs the same kit code.

## Tests

Declared +14, actual **+14** (`[TestCase]` attributes under `tests/`: 4165 to 4179 on this base).

| Suite | Cases | Runtime |
|---|---|---|
| `CapstoneKitTests` (new) | 9, one per hero | Engine; each has its unowned control |
| `CapstoneContentTests` (new) | 5 | Engine |

Changed in place, no count change:

- `ResonanceProgressionTests`: `GridTotalCost`, ten nodes, tiers 3/3/3/1, Majors at tier 3 and above, cost by tier. Two cases renamed: `EveryAuthoredGridHasTenNodesThreePerTierAndACapstoneAndTotals1350` and `EveryGridAuthorsExactlyFourMajorPerkKeysAndAGateSlotTheScheduleKnows`.
- `DustEconomyTests`: `NineNodeCost` 975 and `FullGridCost` 1,350. The ledger assertions read the nine nodes, and a new assertion says no ledger route reaches a capstone (`ThoroughAllRestoredMaximum` 1,094 < 1,350). GR-RANK's flagged lines are reworded; `1537 >= FullGridCost` still holds.
- `TubmanContentTests` (10 nodes, 1,350).
- `ResonanceGridSceneTests` (11 children, 10 positions, 14 edges of which 12 Any-of).
- `ResonanceGridFitTests` (ten chips).

**Seen failing:**

- Every capstone perk reader forced off: all 9 kit cases failed.
- The nine old grids: 9 failed of 54 (3 content cases, plus `DustEconomyTests`, `TubmanContentTests`, `ResonanceGridFitTests`, `ResonanceGridSceneTests` and 2 in `ResonanceProgressionTests`).
- A tier 4 mark drawn differently, a `FighterSim` file naming `CapstoneRules`, and the `RegenMomentum` call removed: the two remaining content cases and the Symphony case failed.
- The fit test also failed for real while four capstones were authored at x 0.92 and 0.93: those chips left the area at 1.0. They are at 0.9 now.

**Final filtered run:** `Failed: 0, Passed: 391, Total: 391`, 28 filter terms, with the translation recompiled locally, against 391 `[TestCase]` attributes counted for the same case-insensitive filter. The full suite is the orchestrator's.

## Hash, save and protocol effects

None. Nothing under `scripts/FighterSim/` changed or names this; `CapstoneContentTests` scans for it. Save schema v8, protocol v5, no component ID. No Fighter hash moves.

## Deviations from the plan

1. **The base synced bolt changed.** Story's synced bolt never stopped on contact: it flew through everything and paid its 0.05 pip per body, so "synced bolts pierce" would have meant nothing. A plain synced bolt now stops at its first landed contact, as a sim projectile does (`FighterEntitySystems` destroys it on a hit). This is a change for every Leonardo at every grid state.
2. **A contact has to land to spend the bolt.** The bolt spawns inside its own turret's hurtbox, which refuses it with 0 damage, so `DetonateOnImpact` killed it on frame 2. `StopsOnLandedContact` ignores any contact that did not land. So a bolt also flies on through a warded mob, a dormant mob or a block.
3. **Codex bolts aim level.** A hurtbox's position is its body's feet, so a bolt aimed there meets the floor just past its target and could never reach a second. Codex bolts aim at muzzle height over the target's feet. Plain bolts keep the old aim.
4. **`PlayerController.cs` hunk, outside the matrix row.** The lane has to sit in the one composition, and only the build-time half keeps basics out of it. One factor added.
5. **`EnemyController.Hazard.cs` hunk, GR-HAZ's file.** The windbox has to reach the 7d seam. `HazardMobRules` is unchanged: the opening is passed in as part of the thrown flag. An opening counts as a new launch for the once-per-source rule.
6. **New shield source.** `StoryShieldEffect.FreedomLine` is appended rather than reusing `NorthStarWard`, because a duplicate grant is keyed on source and event, and a Leap's ID could collide with a counter's.
7. **`ResonanceTierMark` has no code change.** Tier 4 already drew tier 3's art through `tier >= 3`. A test pins it, and the plan's "reuses" is met.
8. **"Each construct +1 charge" means the turret.** It is Leonardo's only construct.

## Known limits

- **The turret's own shot still flies through bodies.** Only the synced bolt changed. That is pre-existing and outside this package's rulings.
- **The Symphony window** steps beside the Momentum regen, so it shares its pauses (own hitstop, tree pause).
- **The Curtain Call opening** runs on `Engine.GetPhysicsFrames()`, so a paused tree spends it. It is reset on a pool cycle.
- **Curtain Call and the world:** the windbox holds while the world is held and ends with its owner's death. It has no Time Freeze hook, which is dormant anyway.
- **Stacks are not saved** and are not cleared by a respec; a live stack lapses within 6 s.
- **No new art.** The strip and the windbox are placeholder zones, the wave has no visual of its own, and the HUD shows no Symphony stacks.
- **`ResonanceChipLayout`:** a chip clamped hard against the right edge fails the fit test's area check at 1.0. I authored around it (x at most 0.9) and did not trace it.

## What did not ship

- A stack readout, per-capstone VFX or audio.
- Any CPU or bot awareness of these nodes.
- `docs/DUST_ECONOMY.md`: not edited. It is a mirror of the contract, and the plan gives both to GR-DOCS.

## What the orchestrator owes

- **Compile `localization/en.en.translation`** after merging: 18 new keys. Until then `ResonanceProgressionTests.EveryAuthoredNodeCarriesLocalizedCopyAndAUniqueLayoutPosition` and `ResonanceGridFitTests` fail, because raw keys widen the chips. `--import` skips the file unless `en.csv`'s mtime changed: `touch` it first.

## What the docs owe (GR-DOCS)

- **Ledger:** `USER-GRID-CAPSTONE` and `PROVISIONAL-CAPSTONE-EFFECTS` with the numbers and readings above; a row for the synced bolt's stop (deviations 1 to 3).
- **Contracts:** `DUST_ECONOMY.md` and its mirror (975 becomes 1,350, the capstone sits above the F05 ledger, and the upgrade pacing table's 975 row); A15 `ABILITY_DATA.md` (the nine effects).
- **`design-godot.md`, master and mirror:** the grid statement (9 nodes, 975) and each hero's grid.
- **AGENTS.md:** the Resonance grid bullet (9 nodes at 3/3/3, 50/75/200 = 975 becomes 10 nodes and 1,350), `StoryShieldEffect`, the map entries for the new partials and rules, the baseline arithmetic, and the Leonardo bullet (a synced bolt stops at its first landed contact).
