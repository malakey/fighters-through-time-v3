# GU-L0 handoff: encounter locks for Levels 0, 1 and 3 (gameplay update 2026-10-12, Phase B)

Workstream GU-L0 of `docs/GAMEPLAY_UPDATE_2026-10-12_PLAN.md` (§2.3, §3.2, §5): user request 3 ("you enter rooms or areas that you have to clear of enemies … You no longer have the option of jumping past them. The same thing could be done for puzzles in a room") applied to the Tutorial, Florence and Chicago. Story only. Branch `worktree-wf_6f019357-a19-1`, based on `7aaae7d` (main with Phase A and FIX-E merged; suite 3125).

**Nobody has played or looked at any of this.** The evidence is headless: the suites below, a 300-frame smoke run of each scene and the bot runs in §6.

## 1. Shipped

### Level 0 — the Tutorial (`Level00Controller`, standalone)

The Combat Trial is **one standalone `EncounterLock` over the bay's east end**, built when the trial begins (as the trial's checkpoint and font always were), replacing the global kill counter.

| Item | Value |
|---|---|
| Lock ID | `Level00Controller.TrialLockID` = `level_00_tutorial_lock_combat_trial` |
| Area | `TrialLockMinX` (2580) to `CalibrationBayEastWallX` (4780), 2200 px; `EncounterLockSpec.Span(…, BayHeight)` |
| Entry barrier | centre 2570 (2560–2580), floor 900; the gap between the 2300 and 2700 platforms, so it cuts neither |
| Exit barrier | none: the bay's east wall (4780–4800) closes the far side |
| Wave 1 | `TrialWave1ID` (`level_00.trial_wave_1`), `TrialWave1Spawns`: five `hologram_drone`s at 2950, 3350, 3750 (patrol 3550–3950), (4150, 700), 4500; **OnEngage**; Easy 3, Hard 5 (as before) |
| Wave 2 | `TrialWave2ID` (`level_00.trial_wave_2`), `TrialWave2Spawns`: drones at 3000, (3500, 600), 4000 and a `chrono_slasher` at 4300 (patrol 4100–4500); **AfterPreviousCleared**; Easy 2, Hard 4 (as before) |
| Clear | `OnTrialLockStateChanged` → `CompleteTutorial()` (from the lock's own tick, no longer from inside a kill's physics flush) |
| GO direction | `None` (the tutorial ends on the clear) |
| Objective | `RefreshTrialObjective()` (polled while the phase is `CombatTrial`, posted on change): `tutorial_objective_progress` (defeated / wave 1 planned), then `tutorial_objective_wave_two` once wave 2 has spawned — the tutorial's own two keys; the HUD's lock counter shows the running total beside them |

- New public surface: `BayWidth` / `BayHeight` (4800 / 1080), `RollGatePosition`, `TrialLockID`, `TrialLockMinX`, `TrialWave1ID`, `TrialWave2ID`, `TrialWave1Spawns`, `TrialWave2Spawns`, `TrialLock` (test and bot seam), `GrabLessonDrone` (test seam). Internal test seams: `BeginGrabLessonForTesting()`, `CompleteGrabLessonForTesting()` (beside the existing `BeginCombatTrialForTesting()`).
- **The grab lesson's drone is released after its lesson** (`ProcessGrabDroneDismissal`, polled every physics frame): once the throw resolves it is handed back to the pool as soon as it is out of its thrown flight (cap `GrabDroneDismissFrames` = 90), unfrozen and stripped of the lesson's guard visual first. A drone the throw killed needs nothing. It used to stay behind as a live hostile, and the trial's kill counter counted it.
- `OnEnemyKilled` now serves the hold only. `_enemiesKilled`, `_totalEnemies`, `_waveTwoActive`, `SpawnWave1`, `SpawnWave2` and `UpdateWaveObjective` are deleted.
- **The hold stays scripted** and **the Time Freeze drill stays exempt** (its blocker is unkillable by design and stands at 1180–1420, far west of the lock). The Skip path never builds the lock.

### Level 1 — Florence (`Level01Controller`, standalone)

Four standalone locks, built in `BuildLevel` → `BuildEncounterLocks()` (after the rooms, extractors and font), resume baseline applied and armed in `_Ready` → `ArmEncounterLocks()` right after `SpawnPlayer()`.

| Lock (const) | ID | Area x | Entry barrier | Exit barrier | Wave (row, trigger) | Extra |
|---|---|---|---|---|---|---|
| `LockStreets` | `level_01_florence_lock_streets` | 720–2330 | 700–720 | 2330–2350 | `level_01.room1a` (`Room1aSpawns`), OnBuild | |
| `LockPiazza` | `level_01_florence_lock_piazza` | 2350–4430 | 2330–2350 (shared slot) | 4430–4450 | `level_01.room1b` (`Room1bSpawns`), OnBuild | |
| `LockPrintShop` | `level_01_florence_lock_print_shop` | 5800–8630 | 5780–5800 | the workshop door, 8630–8650, y 500–900 under the door column | `level_01.room2` (`Room2Spawns`, `AuthoredCount` 3), **OnEngage** | `RequiredPuzzleIDs = { "florence_gear_puzzle" }` |
| `LockWorkshop` | `level_01_florence_lock_workshop` | 8900–10460 | 8880–8900 | 10460–10480 | `level_01.room3` (`Room3Spawns`), **OnEngage** | |

- Every lock edge sits in a gap between platforms, so no barrier cuts a platform (see the one platform trim below).
- Room 1 stays pre-placed (it always spawned at load); rooms 2 and 3 spawn as the hero walks in, as their wave triggers did. The print shop's door still opens on the gear solve (`OpenWorkshopDoor`); its barrier opens on the clear, which needs both.
- **Baselines** (`Level01Controller.EncounterBaselines`, public, registered with `StoryManager.RegisterEncounterBaseline(…, StoryLevelControllerBase.ExplicitEncounterBaselineVersion)` in `BuildEncounterLocks`, so `CheckpointTrigger.IsHeldByEncounterLock` holds the fractures and the F10 record carries the lock IDs):
  - `florence_checkpoint_0` → none;
  - `florence_checkpoint_1` → streets, piazza, print shop;
  - `florence_checkpoint_2` → all four.
  A resume (`ResumedCheckpointID`, set in `RestoreSavedCheckpoint`) builds the listed locks Cleared before `Arm()`; a pre-seal load (boss already down) builds all four Cleared. Room 1 used to respawn behind the hero on every resume.
- Objective: `RefreshObjective()` on every lock edge and on the door opening — an engaged print shop with the gears unsolved posts `florence_objective_gears`, any other engaged lock `encounter_lock_objective_clear`, otherwise `florence_objective_reach_boss`; it stands down once the boss intro has played.
- The `Room2WaveTrigger` / `Room3WaveTrigger`, their latches, `SpawnRoom1/2/3Enemies` and Florence's private `BuildWaveTrigger` are deleted.
- New public surface: `LevelID`, `LockStreets` / `LockPiazza` / `LockPrintShop` / `LockWorkshop`, `EntryCheckpointID`, `MiddleCheckpointID`, `GearPuzzleID`, `Room1aRowID` / `Room1bRowID` / `Room2RowID` / `Room3RowID`, `EntryCheckpointX`, `MiddleCheckpointX`, the eight lock min/max x constants, `Room1aSpawns` / `Room1bSpawns` / `Room2Spawns` / `Room2AuthoredCount` / `Room3Spawns`, `EncounterLocks`, `StreetsLock` / `PiazzaLock` / `PrintShopLock` / `WorkshopLock`, `ResumedCheckpointID`, `EncounterBaselines`.

### Level 3 — Chicago (`Level03Controller`, the base class)

Three locks, built in `BuildLevel` → `BuildEncounterLocks()` after the extractors, following Level 2.

| Lock (const) | ID | Area x | Entry barrier | Exit barrier | Wave (row, trigger) | Extra |
|---|---|---|---|---|---|---|
| `LockMidway` | `level_03_chicago_lock_midway` | 700–3820 | 680–700 | 3820–3840 | `level_03.midway`, **OnEngage** | camera (700, 520, 3120, 1080), the room camera's framing; objective `encounter_lock_objective_clear` |
| `LockElectricity` | `level_03_chicago_lock_electricity` | 3840–5780 | 3820–3840 (shared slot) | 5780–5800 | `level_03.electricity`, OnBuild | derived camera (the full-height climb); objective `encounter_lock_objective_clear` |
| `LockRoutingHall` | `level_03_chicago_lock_routing_hall` | 6115–8290 | 6095–6115 | the Court of Honor door, 8290–8310, y 680–1400 under the column | `level_03.routing_hall`, OnBuild | `RequiredPuzzleIDs = { "level_03.beam_routing" }`; camera (6115, 520, 2175, 1080); no lock objective (the level's `chicago_objective_reroute` stands) |

- **Baselines** (`EncounterBaselineMap`): `_checkpoint_0` → none; `_checkpoint_1` → midway, electricity; `_checkpoint_2` → all three. The old `_midway_wave` / `_electricity_wave` / `_routing_wave` encounter IDs, their latches, the three wave triggers, `MarkEncounterCleared` and `SpawnWave` are deleted. (The old CP1 baseline cleared only the Midway, leaving the Electricity wave dormant behind a hero resumed at 6180.)
- `RestoreLevelObjective()` → `RefreshApproachObjective()`: the Court (`chicago_objective_reach_boss`) once the door and the routing hall are open; `encounter_lock_objective_clear` if the beam opened the door while the hall still holds; `chicago_objective_reroute` once the Electricity Building is cleared; else the fairgrounds. `OpenCourtDoor(announce)` posts `encounter_lock_objective_clear` instead of "press on" while the routing lock holds. A mid-level resume starts on the refreshed objective (`OnLevelReady`).
- New public surface: `LockMidway` / `LockElectricity` / `LockRoutingHall`, `MidwayRowID` / `ElectricityRowID` / `RoutingHallRowID`, the six lock min/max x constants, `CourtDoorX` (8300), `MiddleCheckpointX` (6000), `PreBossCheckpointX` (8500), `MidwayLock` / `ElectricityLock` / `RoutingHallLock`.

## 2. Files changed

All inside my ownership:
- `scripts/Environment/Level00Controller.cs`
- `scripts/Environment/Level01Controller.cs`
- `scripts/Environment/Level03Controller.cs`
- `resources/Content/reward_manifests/level_01_rewards.tres` (the room-1 row split)
- `tests/Integration/Level00FlowSceneTests.cs`
- `tests/ContentValidation/Level01ContentTests.cs`
- `tests/ContentValidation/Level03ContentTests.cs`
- shared, my levels' cases only, additive: `tests/Unit/EncounterBaselineTests.cs`, `tests/ContentValidation/RewardManifestTests.cs`
- this handoff

No scene file changed (every move is in the controllers). No `en.csv` change: every key used already exists (`encounter_lock_objective_clear`, the tutorial and level objectives). No framework file changed.

## 3. Every move (for the ledger)

**Level 0**
- Roll gate 3100 → **2150** (`RollGatePosition`; 2050–2250), west of the lock, past the double-jump steps.
- `l00_checkpoint_trial` 3400 → **2300** (`TrialCheckpointPosition`), beside the roll gate, where the trial begins.
- Restoration Font 4300 → **2450** (`RestorationFontPosition`), 150 px past the trial anchor, its footprint 100 px west of the entry barrier.
- Trial wave spawns: wave 1 from 1200, 1800, 2400 (patrol), (3000, 700), 3600 → 2950, 3350, 3750 (patrol), (4150, 700), 4500; wave 2 from 1500, (2000, 550), 2800, slasher 3200 → 3000, (3500, 600), 4000, slasher 4300 — all inside the lock (most of the old ones spawned behind a hero standing at 3100).

**Level 1**
- **West wall added** at x −20..0, full height (`BuildWall(-20, 0, LevelHeight)`).
- `florence_checkpoint_0` 2800 → **300** (still the self-activating entry; before the streets lock).
- `florence_checkpoint_1` 7160 (in the print shop) → **8840** (`MiddleCheckpointX`), past the workshop door, between the print shop and the workshop locks.
- Restoration Font 9720 (inside the workshop gauntlet) → **10530**, past the gauntlet's exit; `florence_extractor_2` 10340 → **10150** to keep the font's 320 px.
- Room 1 slasher 800 → **1000** (patrol 600–1000 → 800–1200), off the streets' entry; the room-1 guard's patrol 2000–2400 → 2000–**2300** (ends at the lock's exit).
- Room 3 first slasher 9040 → **9140** (it stood on the 8990–9090 spike strip at the gauntlet's mouth).
- Room 3 platform at x 8940/y 700 trimmed 8840–9040 → **8910–9040** (centre 8975, 130 wide) so the workshop's entry barrier (8880–8900) cuts no platform.
- Manifest: `level_01.room1@5` → `level_01.room1a@3:chrono_slasher,chrono_slasher,cyber_guard` + `level_01.room1b@2:cyber_guard,steam_automaton`. Same enemies on every difficulty (Easy two slashers and a guard, Normal and Hard all five), same 25 required dust; the source IDs are new (`level_01.room1a#n`, `level_01.room1b#n`; a save's claim on an old `level_01.room1#n` simply never matches).

**Level 3**
- Electricity's crown slasher (5240, 340, no patrol; ~1060 px above the floor, outside its aggro, never came down) → **(5400, 1350)**, patrol ±250, on the floor under the descent.
- `level_03_chicago_checkpoint_1` 6180 (inside the routing hall's area) → **6000**.
- `level_03_chicago_checkpoint_2` 8420 → **8500**, out of swing reach from inside the routing hall.
- Wave triggers at 760, 3990 and 6560 removed (the locks own the waves).
- **Not moved:** the Inventor's Phase 2 coil 0 (8580, outside the arena). Boss arenas are not locks in this package and no content rule asks for it; `Level03ContentTests` pins it between the court's west edge and the boss.

**Swing-reach clearances** (the FIX-E rule; the roster's longest basic hit, every AttackRange minor bought, ≈156 px past the pivot of a hero pressed against a closed exit; pinned by the tests from the live data):
- Florence CP1 surface 8810–8870 vs a reach to ≈8766 from the print shop (pivot 8610) — 44 px;
- Florence CP2 (unmoved, 10660) surface 10630–10690 vs ≈10596 from the workshop (pivot 10440) — 34 px;
- Chicago CP1 surface 5970–6030 vs ≈5916 from the Electricity lock (pivot 5760) — 54 px;
- Chicago CP2 surface 8470–8530 vs ≈8426 from the routing hall (pivot 8270) — 44 px.

## 4. Decisions (Provisional; for `PROVISIONAL-ENCOUNTER-LOCK-RULES` or a GU-L0 row)

1. **Level 0's lock is built when the trial begins, not at level build.** The bay is shared with the hold, the calibration and the mobility gates, and an Armed lock would engage on a hero crossing the bay during the lessons. Consequence: the load-time content sweep (`EncounterLockContentTests`) does not see it; `Level00FlowSceneTests` pins its layout instead (area width, barrier hugging and on the floor, no exit barrier, spawns inside, anchors outside).
2. **Level 0's waves name no manifest row.** Level 0 awards no persistent dust and ships an explicitly empty manifest that `DustEconomyTests.LevelZeroAndTrainingAwardNoPersistentDust` pins (no awards, no enemy queues), so adding rows would break that rule. The wave IDs (`level_00.trial_wave_1/2`) are labels only.
3. **Level 0's trial waves:** wave 1 OnEngage, wave 2 AfterPreviousCleared — the old trial's own "wave 2 when wave 1 is down" structure; the lock replaces the kill counter that also counted the stray grab drone. GO direction None.
4. **The roll gate moved west (3100 → 2150)** rather than shrinking the lock: the trial then opens with the hero outside the lock beside its new anchor and font, and the lock keeps the ruling's whole bay end (2200 px).
5. **Grab-drone release:** after the throw resolves, released on its first tick out of the thrown flight, capped at 90 frames (`GrabDroneDismissFrames`); unfrozen and stripped of the guard visual before it returns to the pool. No visual effect (a hologram simply leaves; the Severed tear would break the "two kinds of door" grammar inside the Warden bay).
6. **Florence's entry room is two locks** (streets 720–2330, piazza 2350–4430) rather than one 3200 px area, for pacing; its manifest row is split so each lock is one row and the per-difficulty composition and the dust total are exact (pinned).
7. **Florence's Middle anchor stands past the workshop door (8840)** rather than before the print shop: the halfway resume point after the puzzle room. **Florence's font moved to the end of the workshop (10530)**, the last heal before the boss room, which needed `florence_extractor_2` to move west (10150).
8. **Spawn styles** keep each level's old behaviour: Florence room 1 pre-placed, rooms 2–3 spawn on engage; Chicago Midway spawns on engage ("nothing spawns at load"), Electricity and the routing hall pre-placed (guards standing at their posts).
9. **Every lock edge avoids platforms** (Florence's and Chicago's barriers sit in platform gaps; one 60 px trim at Florence's 8940 platform).
10. **Chicago's ground-room locks pass explicit camera rects** that keep the room cameras' bottom-anchored framing (y 520–1600); the Electricity lock derives its camera (the full-height climb).
11. **Florence posts its own objectives** from `RefreshObjective()` (a standalone level gets no base-class objective hook); Chicago overrides `RestoreLevelObjective` and keeps "press on" until the routing lock clears.
12. **The Inventor's coil 0 stays at 8580** (see §3).

## 5. Deviations from the plan

- **Level 0's lock is not seen by `EncounterLockContentTests`** (Decision 1) and **its waves name no manifest row** (Decision 2). The plan's rulings hold (one lock over the bay with its two waves, the hold scripted, the drill exempt, the drone released); only the content sweep's coverage differs.
- **Level 0's area starts at 2580, not ~2400** (the gap before the 2700 platform, and room for the moved anchor and font west of it).
- **Florence's room 3 is one lock, not two**: 1920 px of room cannot hold two ≥1280 px areas plus a checkpoint corridor; the 1560 px area holds the seven (Easy four) with the director's spacing.
- The Florence manifest row split (allowed by §5) is the only manifest change.

## 6. Tests

New cases (+10):
- `Level03ContentTests` 15 → 18:
  - `TheThreeApproachRoomsAreEncounterLocksOneWavePerManifestRow`
  - `TheMiddleAndPreBossAnchorsStandBetweenTheLocksOutOfSwingReach`
  - `TheMidwaySpawnsOnEngageAndTheRoutingHallNeedsTheBeamAsWellAsItsGuards` (real frames, real scene)
- `Level01ContentTests` 3 → 5:
  - `FlorenceIsFourEncounterLocksOneWavePerManifestRow`
  - `ThePrintShopHoldsUntilItsGuardsAreDownAndTheGearsAligned` (real frames, real scene)
- `Level00FlowSceneTests` 3 → 5:
  - `TheCombatTrialIsOneLockOverTheBayWhoseClearEndsTheTutorial` (real frames, real scene; restores the campaign position, `TutorialComplete`, the wallet, the session and the pause)
  - `TheGrabLessonsDroneIsReleasedAfterItsLesson`
- `EncounterBaselineTests` 6 → 8:
  - `ResumingChicagoPastItsLocksBuildsThemClearedAndLeavesTheNextOneArmed`
  - `ResumingFlorencePastItsLocksBuildsThemClearedAndLeavesTheNextOneArmed` (with a generic `ResumedSceneFixture<T>` beside the Orléans one)
- `RewardManifestTests` 5 → 6:
  - `FlorencesSplitEntryRowsKeepTheEntryRoomsCompositionOnEveryDifficulty`

Rewritten in place (no count change):
- `Level01ContentTests.FlorenceAuthorsOneRestorationFontInTheWorkshopBeforeTheBossRoom`: also fails on a font inside any lock.
- `Level00FlowSceneTests.TheTutorialFontAppearsOnlyWithTheCombatTrialPastItsAnchor`: also asserts the font and the trial anchor stand west of the trial lock's entry barrier.

Runs (lock held for each, `testhost` drained first):
- Before the Florence manifest split and the new cases: `Level03ContentTests|Level01ContentTests|Level00FlowSceneTests|EncounterLockContentTests|ArenaGeometryContentTests|EncounterBaselineTests|SecretCachePlacementTests|BossArenaLeashTests`: 73/74. The one failure was the expected `EncounterLockContentTests.EveryLockWaveIsExactlyItsManifestRow` (Florence's `room1a` / `room1b` waves named no row yet). Every other content rule — anchors outside areas, spawns inside and on reachable floor, barriers hugging their areas with floor under them, the checkpoint hold, the respawn/room-trigger rule, the arena-geometry sweeps — passed for Florence and Chicago on the first run.
- After: `Level03ContentTests` 18, `Level01ContentTests` 5, `Level00FlowSceneTests` 5, `EncounterLockContentTests` 9, `EncounterBaselineTests` 8, `RewardManifestTests` 6, `DustEconomyTests` 8: **59/59**.
- **Full suite: `Passed! - Failed: 0, Passed: 3135, Skipped: 0, Total: 3135` (1 m 35 s) = 3125 + 10.** The `[TestCase]` attribute count under `tests/` is 3135. `EncounterCompositionTests`, `StoryHealingLoopTests`, `PuzzleResetPlacementTests`, `ArenaGeometryContentTests`, `SecretCachePlacementTests`, `BossArenaLeashTests`, `ArenaEnemyAttackGeometryTests`, `AbsenceBeatContentTests`, `LevelStartResourceResetTests`, `StoryCameraRigTests`, `SceneSmokeTests`, `ScenePoolConfigTests`, the Level 0 meter suites and `UnusedTranslationKeyTests` (every suite that boots or reads my three scenes) passed in it unchanged.
- `dotnet build`: 0 errors, the three pre-existing warnings only (`CS8632`, `CS0649`, `KLSG_ECS004`).

## 7. Measurements

**Smoke runs** (`--headless … --quit-after 300`, lock held): `Level_00_Tutorial`, `Level_01_Florence` and `Level_03_Chicago` all exit 0 with no script error. Level 0 and Level 3 print `6 ObjectDB instances leaked` / `3 resources still in use at exit`; Florence prints neither. An untouched control (`Level_02_Orleans`, same build) prints the same 6 / 3, and a second Level 3 run printed it again, so it is the known at-exit noise (CLAUDE.md), not a leak of this change. Level 0's 6 / 3 is the figure GU-B and AGENTS.md already record for it.

**Bot runs** (`play`, Joan, Normal, seed 0, `--max-seconds=420`, tag `gul0`; the bots do not know about locks yet — GU-BOT):
- **Level 1:** `timeout` at 420 s, progress 8400 px, 1 checkpoint, 0 deaths, 15 damage taken. It cleared the streets, the piazza and the print shop's guards (killed 8: 3 + 2 + 3 on Normal, `passed_while_alive` 0, `bot_gave_up_unreachable` 0) and then stood at the workshop door (x 8600) from about frame 5400 to the end with 0 interactions: it cannot solve the gear puzzle. That is the stop `ARENA-FLORENCE-DOORWAY` already records for the `play` bot ("it stops at the gear-puzzle workshop door (x 8600), a bot limitation"); the locks did not add a new one.
- **Level 3:** `left_level:collapse(Death)` at 187 s, progress 5520 px, 4 deaths, 3 rewinds. It cleared the Midway (3) and the Electricity Building's two slashers, then gave up on the Electricity drone (`bot_gave_up_unreachable` 6) and stood against the lock's closed exit barrier (x 5760) while the drone, hovering at about (5394, 1381) — floor height, reachable — shot it from 366 px (booked as `env` because no enemy was within the telemetry's 320 px; 240 of 276 damage). This is the bot's 720-frame give-up meeting a lock it does not know; GU-BOT's lock targeting is the fix. No level change.
- **Level 0:** `timeout` at 420 s in the Part 1 hold (killed 2 of 3 locals, damage from the era locals): the bot does not perform the hold's scripted Block lesson, so it never reaches the Combat Trial. Pre-existing; the hold is unchanged.

**Geometry facts** (for the ledger): Level 0's lock is 2200 px; Florence's are 1610 / 2080 / 2830 / 1560 px; Chicago's 3120 / 1940 / 2175 px — all within `MinAreaWidthPixels` 1280 and `MaxAreaWidthPixels` 3200. Swing clearances are in §3.

## 8. Not delivered / open

- **Nobody has played these levels with locks.** Pacing (seven Florence workshop members on Normal in 1560 px, the Chicago Midway's 3120 px area), the drone trial and the moved Tutorial gates are untested by a person.
- `EncounterLockContentTests` never sees Level 0's lock (built at trial time); its rules for Level 0 are pinned by `Level00FlowSceneTests` only.
- The HUD shows the "GO" text (with no arrow) after Level 0's clear, because the lock's GoDirection is None and the HUD shows the prompt regardless (framework, reported).
- Florence's and Chicago's moving and crumbling pieces (the scene's lever toy and rotating platforms in the print shop, Florence's crumbling scaffolds, Chicago's crowd arcs) cross no barrier, so no mover hold was needed.
- The bots (GU-BOT) are being taught about locks in parallel; §7 records where they stopped.

## 9. For the closeout

**Ledger rows:** a GU-L0 section under `PROVISIONAL-ENCOUNTER-LOCK-RULES` (Decisions 1–12 and every move in §3); amend `PROVISIONAL-FONT-PLACEMENT` (Florence 9720 → 10530 with `florence_extractor_2` 10340 → 10150; the Tutorial 4300 → 2450; the Florence rule now also forbids a font inside a lock); amend `ARENA-FLORENCE-DOORWAY` (the play bot's old stop at the workshop door now also has a lock barrier in it); record the Florence manifest row split under the F05 / `DUST_ECONOMY` mirror (Level 1's rows are `room1a@3`, `room1b@2`, `room2@3`, `room3@7`; totals unchanged).

**AGENTS.md:** the Level 0 bullet (the Combat Trial is one lock over the bay from 2580, built at trial start; the roll gate at 2150; the grab drone released); the Restoration Font bullet (Florence (10530, 900), the Tutorial (2450, 900)); the Florence description in the "Tutorial, Florence … flows" bullet (four standalone locks, the west wall, its checkpoints at 300 / 8840 / 10660); "Levels 2–15 are built on `StoryLevelControllerBase`" (Chicago's three locks); the baseline count.

**design-godot.md lines owed:** :406 (the Combat Trial as a cleared bay, not a kill count); the Level 1 layout (the entry room's two fights, the print shop's lock and the gears, the workshop lock, the Middle anchor past the door); the Level 3 layout (the Midway, Electricity and routing-hall locks; the crown slasher on the floor); the Story Mode Healing Loop's font placements for Levels 0 and 1.

**For GU-BOT:** Level 0 and Florence are not `StoryLevelControllerBase` levels, so their locks are found through the group `story_encounter_lock` (also `Level00Controller.TrialLock`, built only when the trial begins, and `Level01Controller.EncounterLocks`). Florence's print shop needs the gear puzzle (`florence_gear_puzzle`, two Interact-rotated gears at 7760 and 8110) and Chicago's routing hall the beam puzzle; the `play` bot solves neither today.

**Framework issues (none blocking; no framework file was edited):**
1. **A lock with `GoDirection.None` still shows the GO prompt** — the text "GO" with an empty arrow — for 3 s after its clear (`StoryHUD.EncounterLock.cs`, `OnEncounterLockChanged` → `ShowEncounterGoPrompt` ignores the direction). Repro: clear Level 0's trial lock (GoDirection None); `StoryHUD.EncounterGoPromptVisible` is true. Suggested fix: skip `ShowEncounterGoPrompt` when `payload.GoDirection == EncounterGoDirection.None`.
2. **`EncounterLockContentTests` only sees locks that exist at scene load.** A lock built later (Level 0's trial) escapes every content rule; its level must pin them itself (`Level00FlowSceneTests` does). Also, its manifest-row rule cannot hold for a level whose manifest is pinned empty by design (Level 0; `DustEconomyTests.LevelZeroAndTrainingAwardNoPersistentDust`). A per-lock opt-out (e.g. a spec flag "no persistent dust") would let Level 0 build its lock at load and be swept.
3. **`EncounterLock.SuppressForTesting` builds even a late-created lock Cleared in `_Ready`**, raising `StateChanged(Armed → Cleared)`; a level that completes on its lock's clear (Level 0) therefore completes the moment the trial begins under the seam. Consistent with the seam's meaning; noted so no suite is surprised.
