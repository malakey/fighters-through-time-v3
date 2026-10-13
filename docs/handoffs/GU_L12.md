# GU-L12 handoff: encounter locks for Levels 12–15 (gameplay update 2026-10-12, Phase B)

Workstream GU-L12 of `docs/GAMEPLAY_UPDATE_2026-10-12_PLAN.md` (§2.3, §3.2, §5): Levels 12 (Lunar), 13 (Chronal Void), 14 (Severed Bastion, `Level_14_NeoEarth`) and 15 (Meridian Founding, `Level_15_Alexandria`). User request 3: "you enter rooms or areas that you have to clear of enemies … You no longer have the option of jumping past them." Story only. Branch `worktree-wf_6f019357-a19-4`, based on `7aaae7d` (main with Phase A and its review fixes).

**Nobody has played or looked at any of this.** The evidence is headless: the suites below, a 300-frame smoke run of each scene, and one `play` bot run per level, made with the Phase A bots, which do not yet know about locks.

## 1. Shipped

All four levels follow GU-E's Level 2 recipe:
- `BuildEncounterLocks()` runs at the end of `BuildLevel`.
- Each level has three approach locks, one lock wave per manifest `RequiredWaves` row (same `RowID`, same IDs in the same order).
- Each lock wave is built from the level's existing `SpawnTable` by a private `WaveFromTable(rowID, wave, trigger, aliveAtMost)`. The table keeps its `(EnemyID, Wave, Position)` shape, so `EncounterCompositionTests` reads it unchanged.
- Every level now has explicit lock baselines: checkpoint 0 lists nothing, checkpoint 1 lists the first two locks, checkpoint 2 lists all three.

Removed from all four levels:
- `SpawnInitialEnemies`, `SpawnWave`, `_wavesSpawned` and `HasWaveSpawned`.
- `MarkEncounterCleared` and `NumberedWaveBaselines`.

The room-seam handlers now only post the level's objective. The base class re-posts that objective when a lock clears or reverts.

Every lock posts `encounter_lock_objective_clear` while it is engaged. There are no new `en.csv` keys.

### Pre-boss garrisons fold into the last approach lock (Levels 12, 14, 15)

The space between each level's last approach area and its boss arena is narrower than a lock (1280 px minimum, `EncounterLockRules.MinAreaWidthPixels`):

| Level | Gap | Width |
|---|---|---|
| 12 | curtain to arena | 1216 px |
| 14 | Security Core apron | 900 px |
| 15 | rotunda apron | 350 px |

So each level's wave 4 (the pre-boss garrison, which used to spawn on that apron or inside the arena) is now the **second wave of the last approach lock**. It is `AfterPreviousCleared`: the garrison arrives through Severed tears once the first wave is down, a true second wave rather than a pile-on (§6 has why this was not `WhenAliveAtMost`). The PreBoss anchor, the pre-boss beat, the font and the arena keep their places. No member spawns in, or west of, a boss arena's reveal.

### Salted hunters are manifest sources now

`EncounterLock` can only register members that come from a lock wave, and a wave must equal its manifest row (`EncounterLockContentTests`). So each salted elite was appended to its wave's row, as the tail entry:
- the Erasers in Levels 12–15;
- Level 14's Chrono-Warden.

Effects of the fold:
- **Per-level dust totals are unchanged.** The required pools stay 25/35/35/35. The hunter now draws an elite-weight share of the pool, which is what `DUST_ECONOMY.md` §1 and §2 intend ("required encounter … including elite/Eraser ambushes", weight 5). Before, the salted Erasers paid nothing.
- **Easy keeps its count.** The per-row prefix still drops every hunter on Easy, because each wave's Easy prefix was already shorter than its hunter's index.
- **The ledger now matches the spawns.** The old 4-entry rows under-counted what the 5-entry spawn tables really spawned on Easy, and the new rows fix that.

| Manifest | Row change |
|---|---|
| `level_12_rewards.tres` | `level_12.wave4` += `unbound_eraser` |
| `level_13_rewards.tres` | `level_13.wave3` += `unbound_eraser,unbound_eraser` |
| `level_14_rewards.tres` | `level_14.wave3` += `chrono_warden`; `level_14.wave4` += `unbound_eraser` |
| `level_15_rewards.tres` | `level_15.wave4` += `unbound_eraser` |

### Level 12 (`Level12Controller.cs`)

| Lock (const) | ID | Area x | Entry barrier | Exit barrier | Waves (row, trigger) |
|---|---|---|---|---|---|
| `LockTranquility` | `level_12_lunar_lock_tranquility` | 320–3000 | 300–320 | 3000–3020 | `level_12.wave1`, OnBuild |
| `LockVentField` | `level_12_lunar_lock_vent_field` | 3020–5860 | 3000–3020 | 5860–5880 | `level_12.wave2`, **OnEngage** |
| `LockRelaySpire` | `level_12_lunar_lock_relay_spire` | 6220–7540 (the spire base) | 6200–6220 | 7540–7560, full height. It stands in the 80 px gap between the mast head (ends 7500) and the highline ferry's west stop (deck from 7580), so it cuts no platform and crosses no mover's path. | `level_12.wave3` OnBuild; `level_12.wave4` AfterPreviousCleared; GO points **Up** |

- **The ruling's dead end:** wave 3's three troopers on the floor east of the lift moved onto the route, inside the spire lock: digger 7600 → 7420, slasher 8220 → 6420, void_enforcer 8400 → 7230.
- **The lift hold** (the ruling's other option, taken as well): `LiftSpire` is held at its bottom stop until the spire clears.
  - The mechanism is `HoldMoverWhileLockHolds`: it sets `PathMovingPlatform.Enabled = !lock.IsHolding` and follows `StateChanged`.
  - The climb is the way out of the fight, not around it.
  - The ferry is never held.
- **The cul-de-sac stays out of the lock.** The floor beyond the lock (7560–8540: the two cache vents and the cache Extractor) is again an optional, enemy-free detour.
- **Why the first cut changed:** it spanned the whole floor to the curtain, with the ferry held. Its bot run died four times, all of them in that cul-de-sac (vents, the Extractor's discharge and the enforcer), so I narrowed the lock (§6).
- **Wave 4 moved off the pad** (9200–9900):
  - digger 6320
  - slasher 6780
  - slasher 7480
  - void_enforcer 7330
  - **Eraser 9900 → 6560**, west of the arena as ruled
- **The curtain test** still counts exactly two level-built `StaticBody2D`s within 1 px of x 8560: lock barriers are children of their lock and sit nowhere near it.
- **Unchanged:** checkpoints (6050, 8940) and the font (9400).

New public surface:
- the lock constants and properties: `TranquilityLock`, `VentFieldLock`, `RelaySpireLock`, `*LockMinX/MaxX`;
- `Wave1RowID`–`Wave4RowID` and `SpireReinforcementAliveAtMost`;
- the movers: `SpireLift`, `HighlineFerry`, `SpireLiftNodeName`, `HighlineFerryNodeName`.

### Level 13 (`Level13Controller.cs`)

| Lock | ID | Area x | Entry | Exit | Waves |
|---|---|---|---|---|---|
| `LockThreshold` | `level_13_chronal_void_lock_threshold` | 320–3200 | 300–320 | 3200–3220 | `level_13.wave1`, OnBuild |
| `LockDrift` | `level_13_chronal_void_lock_drift` | 3220–5100 | 3200–3220 | 5100–5120 | `level_13.wave2`, OnBuild |
| `LockApproach` | `level_13_chronal_void_lock_approach` | 5500–6900 | 5480–5500 | 6900–6920 | `level_13.wave3` (with the Eraser pair), **OnEngage**: the scripted ambush springs |

- **The pre-arena trigger is gone** (`PreArenaTrigger` and `PreArenaTriggerX`). Every area runs from `StoryGateRules.ColumnTopY` to the level bottom. The y-limited trigger (1150–2050) could be ridden over on the shards; the approach lock cannot.
- **Shard holds**, both at their west stops:
  - `ShardTitanicDeck` (path 4400–5200) while the Drift lock holds;
  - `ShardGlobeStage` (path 6220–6960) while the approach lock holds.

  Both paths cross the corresponding exit barrier.
- **The Eraser-pair line** (`level_13.eraser_pair`) now plays on the approach lock's engage. It plays only when that difficulty's planned prefix fields an Eraser (`ApproachFieldsAnEraser()`), so Easy, which plans three of five, plays nothing.
- **Moves:**

  | What | From | To |
  |---|---|---|
  | conduit phantom | 2380/890 | 2380/1850 (shelf) |
  | wave 2 trooper | 5560 | 4950 |
  | conduit phantom | 5980/940 | 5980/1850 |
  | elite | 7020 | 6560 |
  | Eraser | 6900/940 (mid-air) | 5780/1850 |
  | Eraser | 7180 (20 px from the Mirror seam) | 6820/1850 |
  | **Middle checkpoint** | 5180 | **5290**: corridor between the Drift and the approach. Its Warden Beacon (+90, interaction 5325–5435) stays clear of the approach's entry barrier. |
  | **Font** | 6850 | **6970**: corridor past the approach's exit, 130 px from the PreBoss anchor (7100, unchanged), 220 px from its beacon, 230 px short of the arena |

- The Mirror arena (7200–9580) is not a lock and is untouched. A rift snap is handled by the framework's revert-to-Armed.

### Level 14 (`Level14Controller.cs`)

| Lock | ID | Area x | Entry | Exit | Waves |
|---|---|---|---|---|---|
| `LockBreachGallery` | `level_14_neo_earth_lock_breach_gallery` | 320–2900 | 300–320 | 2900–2920 | `level_14.wave1`, OnBuild |
| `LockContainmentWing` | `level_14_neo_earth_lock_containment_wing` | 2920–5782 | 2900–2920 | 5782–5802, **y ColumnTopY–700, standing on the bulkhead's top**. It seals the high deck, the wing's natural single exit through the beta pocket; the wall below already seals the lab deck. GO **Up** | `level_14.wave2`, OnBuild |
| `LockFabricationSpine` | `level_14_neo_earth_lock_fabrication_spine` | 6640–9030 | 6620–6640 | 9030–9050 | `level_14.wave3` OnBuild; `level_14.wave4` AfterPreviousCleared |

- **Grid pause:** a corridor grid is paused while the lock it runs through is Engaged, and re-armed on its authored phase offsets when that lock clears or reverts.
  - The pairs are perimeter/Breach Gallery, containment/wing and core/spine.
  - `OnEncounterLockGridState` and `PauseGrid` (beams `Enabled = false`, cycle held in cooldown) do the pausing.
  - `ArmCorridorGrids` (also run on every rewind) skips a paused grid.
  - Seams: `GridPausedBy(lock)` and `IsGridPaused(pattern)`.
  - The arena grid is unchanged.
- **Wave 4 moved** off the apron and out of the arena into the spine:
  - slasher 9550 → 7980
  - drone 9950/1350 → 7420/1350
  - **drone 10450/1350 (inside the arena) → 8620/1350**
  - Eraser 10150 → 8300

  The Warden (wave 3) and the Eraser now really fight in one room, as the controller's comment always claimed.
- **PreBoss checkpoint 9180 → 9220** (`Checkpoint2Position`, now public), past swing reach of the spine's exit.
- **The spine starts at 6640**, past the Extraction Hall trigger (6540–6620), so the reveal plays before the fight.
- The cargo lift crosses no barrier and is never held. Middle checkpoint (6350) and font (9850) unchanged.

### Level 15 (`Level15Controller.cs`)

| Lock | ID | Area x | Entry | Exit | Waves |
|---|---|---|---|---|---|
| `LockPortico` | `level_15_alexandria_lock_portico` | 320–3000 | 300–320 | 3000–3020 | `level_15.wave1`, OnBuild |
| `LockStacks` | `level_15_alexandria_lock_stacks` | 3020–5900 | 3000–3020 | 5900–5920 | `level_15.wave2`, OnBuild |
| `LockCollapsingHall` | `level_15_alexandria_lock_collapsing_hall` | 6420–7900 | 6400–6420 | 7900–7920 (between the second and third shelves) | `level_15.wave3` OnBuild; `level_15.wave4` (the rotunda guard sallies) AfterPreviousCleared |

- **The firestorm starts on the hall lock's clear, not on entering the hall.**
  - The handler is `OnCollapsingHallLockStateChanged`, which ignores a `RestoredCleared` lock.
  - The front starts at `FirestormStartBehind(heroX) = clamp(heroX − FirestormHeadStartPixels, EscapeStartX, EscapeEndX)`, where `FirestormHeadStartPixels` = **432** (2.0 s of the 216 px/s front).
  - The new `BeginFirestorm(float frontStartX)` sets the escape's `StartX`, calls `ResetSequence()` and then `Begin()`. The old `BeginFirestorm()` now forwards to `EscapeStartX`.
  - The escape is the run from the lock's exit (7900) to the finish (9100), past the shelves and the third pylon (8220).
  - The hero is never shut in a lock with the front at their back.
- **The escape now completes at the finish line (a pre-existing content bug, fixed).**
  - **The bug:** the scene's finish trigger is 96 px wide and centred on `FinishX`. A walking hero enters it 20–70 px short of the line, `NotifyPlayerReachedFinish` refuses, and `BodyEntered` never fires again while the hero walks through. The front stops at `EndX` 9000 and never reaches a hero past 9100. So the run was never completed, only halted when the hero entered the rotunda.
  - **How it showed:** the `play` bot idled at 9170 forever in its `escape` task, which waits for `IsCompleted`.
  - **The fix:** `WireFirestormFinishLine` also subscribes to the escape's `FrontAdvanced` signal, which fires every running tick. The handler calls `NotifyPlayerReachedFinish(Player)`, so the run completes on the first tick the hero stands past the line.
- **Moves:**
  - wave 2 slasher 5900 → 5700;
  - wave 3: 7900 → 7650 and 8750 → 7250 (6850 unchanged);
  - wave 4 out of the arena: slashers 9620/9880/10150 → 6980/7420/7780, the guard elite 9760 → 7600, the **Eraser 10420 → 7180**.
- Checkpoints (6120, 9180), the font (9560), the pylons (all at x < 9400) and the Prime Anchor are unchanged. `EnterRotunda` still halts the front.

## 2. Files changed

Inside my ownership:
- `scripts/Environment/Level12Controller.cs`, `Level13Controller.cs`, `Level14Controller.cs`, `Level15Controller.cs`
- `resources/Content/reward_manifests/level_12_rewards.tres` … `level_15_rewards.tres`
- `tests/ContentValidation/Level12ContentTests.cs` … `Level15ContentTests.cs`
- `tests/ContentValidation/RewardManifestTests.cs`: one additive case for my levels.
- `tests/ContentValidation/LevelLockTestKit.cs` (+ `.uid`): a new **non-suite** shared helper for the four level suites (layout pins, swing reach, real-frame helpers).
- this handoff.

Outside my ownership:
- `tests/ContentValidation/ArenaGeometryContentTests.cs`, in `NoMovingPlatformEndpointOverlapsStaticGeometry` and its capture.
  - **What changed:** a mover that an encounter lock holds at its near stop (`Enabled == false` at load) may have a far stop inside a closed **lock barrier** of the same level. The new snapshot fields are `HeldMovers` and `LockBarriers`, plus one `case EncounterLockBarrier` in `Collect`.
  - **Why:** the barrier opens on the same clear that releases the mover, so the two never meet.
  - **Without the change:** Level 13's two shards fail the static endpoint check.
  - **Merge seam:** GU-L4's Level 7 skiffs are the same case, and that workstream may make a parallel edit to this function.
  - No count change, and no other level's result moves (no other level holds a mover today).

No scene file changed. There are no `en.csv` edits and no framework edits.

## 3. Decisions (Provisional; for the closeout's ledger)

1. **The pre-boss garrisons fold into the last approach lock** (Levels 12, 14, 15) as an `AfterPreviousCleared` second wave, because their aprons (1216 / 900 / 350 px) are narrower than a lock.
   - The alternative for Level 12 and Level 14 is to move the boss arena's west edge, so the pad or apron can hold a ≥ 1280 px lock plus a PreBoss anchor and font. That needs a ruling, because it reshapes a boss arena.
2. **Salted hunters are manifest sources** (§1), so they can be lock members. That means they pay an elite-weight share of the fixed pool.
3. **Lock triggers:**
   - Level 12 vent field: OnEngage (the troopers come up out of the vents).
   - Level 13 approach: OnEngage (the scripted ambush).
   - Every other first wave: OnBuild (pre-placed and dormant).
   - Every fold: AfterPreviousCleared. It keeps the peak at one wave's members (5–6 rather than 6–7) and gives a beat between waves.
4. **Mover holds:**
   - Level 12: the spire lift (the ruling);
   - Level 13: the Titanic deck and Globe stage shards (each crosses an exit barrier).

   Each is held at its first waypoint from the build and released on the clear.
5. **Level 12 takes both of the ruling's options:** the three dead-end members move onto the route, and the lift is held.
   - The spire lock ends in the mast-head/ferry gap (7540), so the vent-and-Extractor cul-de-sac stays an optional detour rather than a forced fight on two cyclic vents and a discharging Extractor.
6. **Level 14 pauses each corridor grid while its lock is engaged**, rather than keeping areas out of the corridors. Every first-room area overlaps its grid, and a 1280 px area cannot fit beside one.
7. **Level 15's front starts 432 px behind the hero** at the clear, never west of 6500. Starting at 6500 regardless would give a hero who cleared at the lock's east end a 1300 px lead.
8. **Checkpoint and swing clearance.** Every Middle and PreBoss strike surface stands past the longest basic swing (GU-E's `LongestBasicSwingPixels`, about 156 px) of a hero pressed against the closed exit of each lock its baseline lists, with at least about 24 px to spare (`LevelLockTestKit.LayoutIssues` pins it with the computed reach). In Act III, each Warden Beacon's interaction box stays outside the next lock's entry barrier.
9. **Every area spans the full height** (`EncounterLockSpec.Span` from `ColumnTopY`), including Level 13's, so no shard ride or lunar jump leaves an area upward.

## 4. Deviations from the plan

- **Level 12's wave 4 is not fought east of the curtain.** The ruling only asked to move the Eraser west of 9800. The pad between the curtain and the arena (1216 px) cannot hold a lock, so the whole garrison folds into the spire lock (Decision 1).
- **Level 14's drone at 10450 and the rest of wave 4 moved into the Fabrication Spine**, for the same reason (the 900 px apron).
- **The plan's "the debut Eraser … never paying dust" wording for Level 5 does not hold for Levels 12–15.** A lock member needs a manifest row, so the hunters now pay (Decision 2).
- **`ArenaGeometryContentTests` needed a shared-helper change** (§2).

## 5. Tests

New cases (+9):
- `Level12ContentTests` (+2):
  - `TheApproachIsThreeEncounterLocksOneWavePerManifestRow`
  - `TheSpireLiftWaitsAtItsBottomStopUntilTheSpireIsClear` (real frames)
- `Level13ContentTests` (+2):
  - `TheApproachIsThreeEncounterLocksOneWavePerManifestRow`
  - `RidingTheShardsAboveTheOldTriggerStillSpringsTheApproachLock` (real frames: the hero on the Titanic deck motif at y ≈ 990 engages the lock, and the whole wave, both Erasers and Sarah's line spring)
- `Level14ContentTests` (+2):
  - `TheApproachIsThreeEncounterLocksOneWavePerManifestRow`
  - `ACorridorGridIsPausedWhileItsLockIsEngagedAndReArmedOnTheClear` (real frames, through a rewind)
- `Level15ContentTests` (+2):
  - `TheApproachIsThreeEncounterLocksOneWavePerManifestRow`
  - `TheFirestormStartsOnTheHallLocksClearBehindTheHeroNotBefore` (real frames; also pins that a hero past the finish line completes the run)
- `RewardManifestTests` (+1): `TheLateLevelsPayEveryPlannedLockMemberTheirSaltedHuntersIncluded`. On all three difficulties, killing exactly the planned lock members of Levels 12–15 issues a source for each and pays the whole pool. Easy fields no hunter; Normal and Hard pay each one.

Rewritten in place (no count change):
- `Level12ContentTests.EveryCheckpointResumeWakesThePlayerInTheRightGravityState`, `Level13ContentTests.EveryCheckpointResumeWakesThePlayerInTheRightGravityState` and `Level14ContentTests.EveryCheckpointResumesEarthNormalWithItsEarlierWavesAlreadyCleared`: the `HasWaveSpawned` asserts became lock-state asserts (Cleared exactly for the locks behind the anchor). They also check the Level 12 lift and ferry holds, the Level 13 shard holds, and that every Level 14 grid is armed on resume.
- `Level15ContentTests.ResumingAtTheFinalCheckpointAfterTheBossStillReachesTheEnding`: all three locks are `RestoredCleared` at checkpoint 2, instead of `HasWaveSpawned(3/4)`.
- `ArenaGeometryContentTests.NoMovingPlatformEndpointOverlapsStaticGeometry`: the held-mover exemption (§2).

Runs, each holding `D:/Projects/.ftt-gdunit-lock`:
- Level 12–15 suites + `EncounterLockContentTests` + `RewardManifestTests` + `DustEconomyTests`: **123/123**.
- 16 shared suites: **128/130** on the first run, then **71/71** on the rerun after the fixes below. The 16 were `EncounterBaselineTests`, `EncounterCompositionTests`, `SecretCachePlacementTests`, `StoryHealingLoopTests`, `ArenaGeometryContentTests`, `PuzzleResetPlacementTests`, `SceneSmokeTests`, `ScenePoolConfigTests`, `ThoroughRunCollectionWalkTests`, `StageLightingRigTests`, `StoryAudioSetContentTests`, `CampaignRouteTests`, `EncounterLockTests`, `BossPhaseRuleMatrixTests`, `BossArenaFallNetTests` and `BossArenaLeashTests`.
  - The first run's two failures:
    - the endpoint check (§2);
    - the Level 13 font, which I had first put on the Mirror arena floor (7400). The font rule wants it 120 px west of the arena, so it moved to 6970.
  - The rerun covered the Arena / SecretCache / Level 13 / EncounterLockContent / StoryHealingLoop suites.
- A full run before the bot-driven fixes: `Passed! - Failed: 0, Passed: 3134, Total: 3134` (1 m 39 s).
- After the Level 12 spire narrowing, the Level 15 finish fix and the `AfterPreviousCleared` folds: Level 12 / 15 / Arena / EncounterLockContent / SecretCache **76/76**.
- **Final full suite: `Passed! - Failed: 0, Passed: 3134, Skipped: 0, Total: 3134` (1 m 34 s) = 3125 + 9.** The `[TestCase]` count rises by exactly the 9 new cases.

## 6. Measurements

- **Smoke** (`--headless --quit-after 300`): Levels 12, 13, 14 and 15 all exit 0 with no script error.
  - Level 13 sometimes prints "6 ObjectDB instances leaked / 3 resources still in use at exit" (2 of 5 runs); Levels 12, 14 and 15 printed nothing.
  - **Control:** four runs of the **base** (`7aaae7d`) Level 13 controller and manifest printed 8/4, 2/1, 2/1 and 6/3. So this is the pre-existing, nondeterministic at-exit noise CLAUDE.md describes (the Mirror clone), not a regression.
- **Bots** (one `play` run per level, Joan, Normal, `--max-seconds=420`; the Phase A bots, which are not lock-aware). Reports are in `user://playtest/`.
  - **First batch, tag `gul12`** (the first cut of Level 12, before the Level 15 finish fix):

    | Level | End | Time (s) | Progress (px) | Kills | Deaths |
    |---|---|---|---|---|---|
    | 12 | collapse | 161.7 | 8300 | 17/18 | 4 |
    | 13 | timeout | 420 | 9304 | 28/33 | 9 |
    | 14 | timeout | 420 | 11112 | 45/55 | 9 |
    | 15 | timeout | 420 | 8950 | 15/16 | 0 |

    - Level 12: all four deaths were in the cul-de-sac (cache vents, the cache Extractor, the void enforcer). That is why the spire lock was narrowed (§1).
    - Level 13: three deaths on the first approach ambush, the rest at the Mirror, then a refight after an Anchor Snap.
    - Level 14: every lock was cleared and the boss reached; then three deaths to Archive Prime, a Snap, and refights.
    - Level 15: every lock was cleared and the firestorm started on the clear, but the bot then idled at x 9170 with its `escape` task for five minutes. That is the escape-completion bug fixed in §1.
  - **Intermediate rerun, tag `gul12b`, Levels 12 and 15:**
    - Level 12: three deaths in the 10-member spire fight (then a `WhenAliveAtMost(1)` fold), which led to the `AfterPreviousCleared` folds.
    - Level 15: past the escape to the boss. Smothered after repeated deaths to the Apex Eraser.
  - **Final batch, tag `gul12c`** (this commit):

    | Level | End | Time (s) | Progress (px) | Kills | Deaths |
    |---|---|---|---|---|---|
    | 12 | timeout | 420 | 8320 | 17 of 17 planned | 2 |
    | 13 | **level_complete** | 186.8 | 9304 | 11/11 | 0 |
    | 14 | timeout | 420 | 10778 | 44 | 9 |
    | 15 | timeout | 420 | 10533 | 45 | 9 |

    - Level 12: every lock cleared, then the bot walked into the cache cul-de-sac and sat "stuck" against the curtain at 8540 until the timeout. Earlier batches in the same directory show the **pre-existing** Level 12 bot stall at progress 8320 many times (`collapse(Timer)` at 8320 on Easy), so this is a nav weakness, not a lock.
    - Level 13: no deaths and the Mirror defeated.
    - Level 14: cleared all three locks and activated checkpoint 2 by about 141 s; the rest of the run was deaths to Archive Prime and refights after Anchor Snaps.
    - Level 15: cleared all three locks and reached the boss by about 88 s; the rest of the run was deaths to the Apex Eraser.
  - **The Level 15 bot never strikes checkpoint 2.** It runs past it to the boss, so its Snaps go back to checkpoint 1. Bot-side; for GU-BOT.
- **Lock widths:**

  | Level | Areas (px) |
  |---|---|
  | 12 | 2680, 2840, 1320 |
  | 13 | 2880, 1880, 1400 |
  | 14 | 2580, 2862, 2390 |
  | 15 | 2680, 2880, 1480 |

- **Planned members on Normal:**

  | Level | Per lock | Note |
  |---|---|---|
  | 12 | 3 / 4 / 10 | the spire, folded |
  | 13 | 3 / 3 / 5 | |
  | 14 | 3 / 5 / 10 | |
  | 15 | 3 / 4 / 8 | |

  Easy plans 2 / 2 / 3 + 3 for Level 12 and the equivalent per-row prefixes for the others.

## 7. Not delivered / open

- **Nobody has played any of it.** Untested by a person:
  - the 8–10-member fold fights;
  - the spire fight with the lift held;
  - the paused Bastion grids;
  - the Level 15 escape head start.
- **The bots do not know about locks yet** (GU-BOT). Bot-side issues these runs surfaced:
  - the pre-existing Level 12 stall on the cul-de-sac floor (progress 8320) against the curtain;
  - the Level 15 bot running past checkpoint 2.
- **The hunters now pay dust.** This touches the held `VERIFY-ERASER-DUST`.
- **Pre-existing, not changed: Level 15's firestorm does not survive a death mid-escape.** `EscapeSequenceController` restores its checkpoint-captured state on a rewind (front at 6500, not running), and `FirestormTriggered` stays latched, so after a death the front stops for good.
- **Level 12 movement abilities and the mast head:** during the spire fight, a high-mobility hero might reach the mast head without the lift, from the ladder rung at 1500, in lunar gravity with a double jump plus a movement ability (not measured). It does not matter for progress: the lock's full-height exit barrier fills the mast-head/ferry gap, so the mast head is a dead end until the clear.
- **Level 13's approach ambush is the hardest first contact in these levels.** It springs five members on engage, three of them elites (two Erasers and the guard elite). The first bot run died three times there before clearing it (§6).
  - A gentler stagger would make the Erasers a separate `WhenAliveAtMost` row.
  - That needs a ruling: a two-entry row's Easy prefix is 1 (`ScaleEncounterCount` never returns 0), so Easy would field one Eraser where it fields none today.
- **Act III Resonance Hold nodes** at Level 13 5980/940 and Level 14 4070/730 and 8990/610 sit inside lock areas. A hero can channel them mid-fight; damage resets the channel.

## 8. For the closeout

**AGENTS.md**
- The Levels 2–15 bullet: Levels 12–15's locks, mover holds, the Level 14 grid pause and the Level 15 firestorm-on-clear.
- The Eraser/Warden salting paragraph: the Level 12–15 hunters are lock members and ledgered sources.
- The Level 13 facts: Middle 5290, font 6970, the pre-arena trigger gone.
- The Level 14 fact: PreBoss 9220.
- The Level 15 escape description.

**Ledger rows**
- New `PROVISIONAL-L12-15-ENCOUNTER-LOCKS`: Decisions 1–9 and the tables above.
- Amend `VERIFY-ERASER-DUST`: the Level 12–15 Erasers and the Level 14 Warden are now required sources at elite weight (pools unchanged).
- Amend `PROVISIONAL-FONT-PLACEMENT`: the Level 13 font 6850 → 6970, and the Level 12/14/15 font comments (no post-wave spawn on the apron any more).
- Amend `USER-MOVEMENT-SPEED-CUT` / the escape-front row: the Level 15 front now starts on the hall clear with a 432 px head start.
- Note under `ARENA-*`: Level 12's Eraser and Level 14's drone are no longer inside or beside their arenas.
- New `VERIFY-L15-ESCAPE-REWIND-HALT`: the pre-existing escape stop after a death mid-escape (§7).
- New, closed `FIX-L15-ESCAPE-NEVER-COMPLETED`.
  - The bug: the finish trigger is centred on the finish line, so a walking hero always entered it short of the line, and the run was only ever halted at the rotunda.
  - The fix: `WireFirestormFinishLine` now also completes the run on the front's `FrontAdvanced` tick.
  - Level 6's Pompeii escape uses the same template and finish pattern ("the Level 6 pattern"). GU-L4 should check whether it has the same bug.

**`design-godot.md` lines owed** (repo mirror numbering)
- :271 (Level 12: the spire lock and the held lift)
- :273 (Level 13: full-height locks, the shard holds)
- :274 and :290 (Level 14 and Act III: the laser grids pause while a lock is engaged)
- :275 (Level 15: the hall lock clears before the firestorm)
- :3016 (the Level 13 Eraser pair springs on the approach lock's engage)
- `DUST_ECONOMY.md` §1/§2 needs no change; the build now matches its "elite/Eraser ambushes" text for Levels 12–15.

**Merge notes**
- Only the `ArenaGeometryContentTests` helper overlaps another workstream, likely GU-L4 for the Level 7 skiffs.
- `RewardManifestTests` gained one additive case.
- `LevelLockTestKit.cs` is new and private to these suites (no collision).
