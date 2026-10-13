# GU-L4 handoff: encounter locks for Levels 4–7, gameplay update 2026-10-12

Workstream GU-L4 of `docs/GAMEPLAY_UPDATE_2026-10-12_PLAN.md` (Phase B, §3.2, §5). User request 3: "you enter rooms or areas that you have to clear of enemies … You no longer have the option of jumping past them. The same thing could be done for puzzles in a room." Story only. Based on `7aaae7d` (main with Phase A and its review fixes merged).

**Nobody has played or looked at any of this.** The evidence is headless: the suites below, a 300-frame smoke run of each level, and one `play` bot run per level.

## 1. Shipped

Every area of Levels 4–7 that holds enemies is now an `EncounterLock` built in `BuildLevel` through `StoryLevelControllerBase.BuildEncounterLock`, following GU-E's authoring guide and Level 2. Every level dropped its `SpawnInitialEnemies`, `SpawnWave`, wave triggers, wave latches and `*_wave` encounter IDs. Each anchor's F10 baseline now lists every lock whose area ends before it, so the Room 1 waves of Levels 4 and 6 no longer respawn behind a resumed hero. Every barrier is 20 px wide, stands on the floor under it, hugs its area, and is centred 10 px outside the area edge unless stated otherwise.

### Level 4 — Paris (`scripts/Environment/Level04Controller.cs`)

| Lock (const) | ID suffix | Area x | Exit barrier | Wave (row, trigger) | Condition | Objective while engaged |
|---|---|---|---|---|---|---|
| `LockGatehouse` | `_lock_gatehouse` | 300–3200 | 3210 | `level_04.room1`, OnBuild | — | `encounter_lock_objective_clear` |
| `LockCorridor` | `_lock_corridor` | 3220–4760 | 4770 | `level_04.room2`, OnEngage | — | `encounter_lock_objective_clear` |
| `LockCells` | `_lock_cells` | 6120–8100 | 8110 | `level_04.room3`, OnBuild | `RequiredPuzzleIDs = { level_04.free_prisoners }` | the level's own (prisoners n/2) |

- **The walkway rioter moved** from (5600, 250) to the corridor floor at (4250, 850). It used to stand on the upper walkway, 600 px over a floor route that walked straight under it, and it never came down.
- **The last cell-block slasher moved** from (8350, 850) to (7400, 850), between the cells. The old spot was beside the PreBoss anchor and east of the new barrier.
- **The cell-B rioter (7900)** stays sealed in cell B with prisoner B (plan ruling, "keep it as a member reachable once lock B breaks"). Once engaged it presses against lock B until the hero breaks it.
- Patrols are the old ±200 px, clamped 40 px inside each lock (`LockWave`).
- **The ruling for the courtyard:** the courtyard gate still opens the moment both prisoners are free (`OpenCourtyardGate`, unchanged). The cells lock's barrier opens only on "prisoners freed AND guards cleared".
- **Moved:** the corridor room trigger, 3200 → 3260 (`CorridorLockMinX + 40`). The wave triggers `Room2WaveTrigger` and `Room3WaveTrigger` were deleted.
- **Not moved:** the Middle anchor (`MiddleCheckpointX` = 5000) and the PreBoss anchor (`PreBossCheckpointX` = 8480), now named constants. The font stays at 8200. The searchlights are unchanged (ruling: "searchlights stay"). The tower climb and the swept walkway (4780–6080) hold no enemy and no lock.
- `RestoreLevelObjective()` recomputes the objective from live state: completion, then courtyard, then the prisoner count. Before, it re-posted the last line, which could be a searchlight alarm.
- **Baselines:** `checkpoint_0` → none; `checkpoint_1` → gatehouse and corridor; `checkpoint_2` → all three. `Room2WaveEncounter` and `Room3WaveEncounter` were deleted.
- **New public read surface:**
  - the lock IDs, row IDs and min/max x constants;
  - `MiddleCheckpointX` and `PreBossCheckpointX`;
  - `GatehouseLock`, `CorridorLock` and `CellsLock`.

### Level 5 — Titanic (`Level05Controller.cs`, `EraserDebutTrigger.cs`, `level_05_rewards.tres`)

| Lock | ID suffix | Area x | Barriers stand on | Waves (row, trigger) |
|---|---|---|---|---|
| `LockStaircase` | `_lock_staircase` | 380–3400 | entry D0 (1300), exit D2 (1140) | `level_05.wave1`, OnBuild |
| `LockBoilerCasing` | `_lock_boiler_casing` | 3420–5880 | entry D2, exit E2 (940) | `level_05.wave2`, OnEngage |
| `LockEDeck` (area 3a) | `_lock_e_deck` | 6160–7520 | entry E2, exit boat deck (800) | `level_05.wave3`, OnBuild; then `level_05.eraser_debut` (unscaled), `WhenAliveAtMost(1)` |
| `LockBoatDeck` (area 3b) | `_lock_boat_deck` | 7540–8820 | entry boat deck, exit stern rail (800) | `level_05.wave4`, OnBuild |

- **Room 1 has a room trigger now** (`titanic_grand_staircase` at x 300, camera `Room1CameraBounds` = 0–3400). Until now its camera was the whole level and its mobs had no leash room.
- **The old wave 3 is split in two.** `SpawnTable` now has waves 1–4 (`WaveCount = 4`, `WaveRowID(n)`). Wave 3 is the E-deck pair (6600, 7000); wave 4 is the boat-deck pair (7800, 8300) and the `tech_enforcer` (8500).
  - The manifest row `level_05.wave3` (5 entries) became `level_05.wave3` (2 slashers) and `level_05.wave4` (2 slashers and the elite).
  - Per-difficulty counts are unchanged: Easy 7 standards and the Eraser; Normal and Hard 12 standards, the elite and the Eraser.
  - The 55 / 25 / 30 row is unchanged.
- **The hull flood rises on lock clears, not on x triggers.** The room-2 entry step and the room-3 entry step are gone, and so is the `BoilerSurgeTrigger` at x 5000.
  - The staircase's clear arms step 1, the boiler casing's clear arms step 2 (the boiler surge), and the E-deck's clear arms step 3.
  - An armed step (`PendingHullStep`) rises only once the hero's feet are east of that step's first dry deck (`HullStepDryFromX` = D2 2300, E2 5500, boat deck 7500) and 16 px above the new line (`HullStepClearancePixels`). That is the rule `TryRaisePendingHullStep`, polled in `_PhysicsProcess`.
  - The boat deck's clear starts the 14 s repeating timer to step 4, exactly as the room-3 entry used to.
  - A lock restored Cleared by a resume raises nothing; `RestoreFloodForCheckpoint` still sets the anchor's step.
- **The Eraser debut stays scripted, and the debut Eraser is a lock member.** It is area 3a's second wave, the `@0` row `level_05.eraser_debut`, never difficulty-scaled.
  - It spawns at `EraserDebutLockSpawn` (6850, 830), 110 px above E2, once one E-deck slasher is left.
  - `EraserDebutTrigger` keeps its placement (6900, 740), its ID, its bark and its F12 reconstruction record. The level sets the new `RouteTriggerSpawns = false`, so crossing the trigger spawns nothing.
  - When the lock registers the Eraser, `AdoptLockSpawnedEraserDebut` hands the body to the new `EraserDebutTrigger.AdoptEraserDebut(Node)`. That raises the same bark (`DebutBegan` → `eraser_debut_bark`) and sets `EncounterLive`, `BarkSeen` and `SpawnCount` as `SpawnEraserDebut` would.
  - Area 3a keeps the level's objective (empty `ObjectiveKey`), so the bark stays readable.
- **Moved:**
  - PreBoss anchor 8650 → **9060** (`PreBossCheckpointX`). It was inside the boat-deck fight and under step 4. It now stands on the stern rail, past the hull flood and the boat deck's barrier.
  - Font 9100 → **9200**.
  - Stern-rail room trigger 8800 → **8880** (`SternRailTriggerX`).
  - Pre-boss beat trigger 8720 → **8960** (`PreBossTriggerX`).
  - Boiler-casing room trigger 3400 → **3480**.
  - The Middle anchor stays at 6100 (`MiddleCheckpointX`).
- `RestoreLevelObjective()`: completion, then the boat-deck objective (`_boatDeckReached`), then the initial objective.
- **Baselines:**
  - `checkpoint_1` → staircase and boiler casing;
  - `checkpoint_2` → all four locks plus `level_05_titanic_eraser_debut` (unchanged).
- **Removed:** `HasWaveSpawned` and `_wavesSpawned`.

### Level 6 — Pompeii (`Level06Controller.cs`, `Level_06_Pompeii.tscn`, `level_06_rewards.tres`)

| Lock | ID suffix | Area x | Exit barrier | Wave (row, trigger) | Condition |
|---|---|---|---|---|---|
| `LockForum` | `_lock_forum` | 300–2880 | 2890 | `level_06.room1`, OnBuild | — |
| `LockVault` | `_lock_vault` | 2900–5390 | **at the rockfall (5400), y 280–900**, under its gate column | `level_06.room2`, OnBuild | `RequiredPuzzleIDs = { level_06.clear_rockfall }` |
| `LockAshRoad` | `_lock_ash_road` | 5660–8500 | 8510 | `level_06.room3`, OnEngage | — |

- **The winch is a condition of the vault lock.** The rockfall door still opens on the solve; the barrier opens on "solved AND cleared". The Reset Puzzle station (3700) sits inside this puzzle lock, as allowed. The vault keeps the level's rockfall objective.
- **The Ash Road is fought before the lava front.** The road room trigger no longer starts the front or spawns the crowd.
  - The road lock's clear (from Engaged) calls `BeginEscape()`.
  - The front starts `EscapeHeadStartPixels` (**240**) behind the hero. `EscapeStartFor(heroX)` clamps it to [`EscapeStartX` 5700, `EscapeEndX` 8800]. The front runs at 224 px/s against Lincoln's 228.
  - A death rewind that lands the hero behind the front's start moves the start back (`OnStoryRewind`), so a reset front never begins over or ahead of the hero.
- **The road's second civilian** (`CivilianAshRoad`, scene) moved 7050 → **8700** (`AshRoadCivilianPosition`). It now waits in the run between the road lock and the finish (8880), so rescuing it still costs seconds against the front.
- **The Chrono-Warden is a road member**, so the lock's member clamp keeps its Phase Skip inside the road (ruling).
  - Its manifest row now carries it: `level_06.room3` gained `chrono_warden` as its last entry. It stays the table's last entry, so Easy's prefix still leaves it out.
  - It **now pays dust** (elite weight 5 of 15). Before, it paid none because no row listed it. The 15 / 25 / 20 row is unchanged.
- The road legionnaire's east patrol end moved 8560 → 8460, inside the lock.
- **Moved:** the vault room trigger, 2880 → 2940. `Room2WaveTrigger` was deleted.
- **Not moved:** the Middle (5600) and PreBoss (9060) anchors and the font (8920).
- `RestoreLevelObjective()` → `RefreshObjective()`.
- **Baselines:** `checkpoint_1` → forum and vault; `checkpoint_2` → all three. `Room2WaveEncounter` and `Room3WaveEncounter` were deleted.

### Level 7 — Nassau (`Level07Controller.cs`, `level_07_rewards.tres`)

Both channels are water, so the two locks sit on the decks and every barrier stands on deck.

| Lock | ID suffix | Area x | Barriers | Waves (row, trigger) |
|---|---|---|---|---|
| `LockQuay` | `_lock_quay` | 380–3580 | entry 370; exit 3590, on the Ranger's waist at the launch yard's lip | `level_07.wave1`, OnBuild; `level_07.wave2`, `WhenAliveAtMost(1)` |
| `LockBoarding` | `_lock_boarding` | 5780–8480, from the Bonnet's waist across the boarding channel to the ramp | entry 5770 (Bonnet), exit 8490 (ramp) | `level_07.wave3`, OnBuild; `level_07.wave4`, `AfterPreviousCleared` |

- **The rows were re-partitioned to the locks.** The per-level 10 standards / 2 elites / 12 entries and the 15 / 25 / 20 row are unchanged.

  | Row | Entries |
  |---|---|
  | `wave1` | deckhand, slasher, deckhand (unchanged) |
  | `wave2` | slasher 3000, the yard deckhand (3450, 642) |
  | `wave3` | deckhand 5900 (the old wave-2 Bonnet deckhand, moved from 5780), slasher 6420, deckhand 6180 |
  | `wave4` (new) | deckhand 8080, slasher 8380, `overcharged_cannon_master` 8230, **`unbound_eraser` 8150** |
- **The Eraser moved** from (7900, 850), over the open boarding channel (it fell forever whenever skiff C was not under it), onto the ramp deck at (8150, 850).
  - It is the last entry of wave 4, so Easy still has no cannon master and no Eraser.
  - It **now pays dust**, because a row lists it. Before, it paid none.
- `SpawnTable` now has waves 1–4 (`WaveCount = 4`, `WaveRowID(n)`).
- **Sarah's first-Eraser bark** fires when the boarding lock registers the Eraser: `_PhysicsProcess` checks only when `RegisteredCount` changes.
- **Movers:** no skiff envelope and no rope swing crosses a barrier, so none needs holding. The plan's mover-hold query (`IsHolding`) is therefore not wired in Level 7 (§4). The quay's exit barrier blocks the launch toward rope A until the quay clears.
- **The sea haul-out:**
  - Rope channel → (3400, 860), inside the quay lock. The hero can only reach that water after the quay has cleared.
  - Boarding channel → (6350, 860), inside the boarding lock, which stays engaged.
  - Neither lands the hero past an uncleared exit.
- **Moved:** the Middle anchor 5800 → **5700** and the PreBoss anchor 8420 → **8680**. The PreBoss anchor was on the ramp beside both elites; it is now on the flagship's west deck, past the boarding lock's exit.
  - `CheckpointPlacements` now drives both `BuildCheckpoint` calls.
  - `SolidDeckSpans` gained the flagship west deck (8500–9220 at 880, `FlagshipWestDeckEndX`).
  - Font 8700 → **9100**, east of FlagshipFireA. Its channel circle and the hero's body keep 30 px from the fire column, and the circle ends 160 px short of the arena.
- The 2800 trigger pair no longer spawns wave 2, and the 6000 pair no longer spawns wave 3. They move the camera and post the flagship objective.
- `RestoreLevelObjective()` re-posts the flagship objective once it has been sighted.
- **Baselines:** `checkpoint_1` → quay; `checkpoint_2` → quay and boarding.
- **Removed:** `HasWaveSpawned` and `_wavesSpawned`.

### `EraserDebutTrigger` (additive, two members)

- `[Export] public bool RouteTriggerSpawns = true`: false turns the route trigger's own spawn off.
- `public bool AdoptEraserDebut(Node body)`: the same guards and bookkeeping as `SpawnEraserDebut`, with a body something else spawned.
- With the defaults, behaviour is unchanged.

## 2. Files changed

Mine:
- `scripts/Environment/Level04Controller.cs`
- `scripts/Environment/Level05Controller.cs`
- `scripts/Environment/Level06Controller.cs`
- `scripts/Environment/Level07Controller.cs`
- `scenes/campaign/Level_06_Pompeii.tscn` (one line: the Ash Road civilian's position)
- `resources/Content/reward_manifests/level_05_rewards.tres`
- `resources/Content/reward_manifests/level_06_rewards.tres`
- `resources/Content/reward_manifests/level_07_rewards.tres`
- `tests/ContentValidation/Level04ContentTests.cs`
- `tests/ContentValidation/Level05ContentTests.cs`
- `tests/ContentValidation/Level06ContentTests.cs`
- `tests/ContentValidation/Level07ContentTests.cs`
- `tests/ContentValidation/Level06RockfallPhysicsTests.cs`
- `tests/ContentValidation/EncounterLockReach.cs` and its `.uid` sidecar (new non-suite helper)
- this handoff

Shared test files, my levels' cases only:
- `tests/ContentValidation/RewardManifestTests.cs` (the two Titanic cases)
- `tests/Unit/PostBossChainTests.cs` (the Titanic case)
- `tests/Unit/EraserDebutTriggerTests.cs` (+1 case)

**Outside my ownership:** `scripts/Environment/EraserDebutTrigger.cs`, the two additive members above. The plan gives GU-L4 "EraserDebutTrigger placement data". The ruling "the debut Eraser belongs to a lock" cannot be met without them: the read-only lock framework has no way to adopt a body it did not spawn, and the trigger's own spawn would otherwise duplicate the Eraser.

No `en.csv` change. Every lock reuses `encounter_lock_objective_clear` or the level's own objective. `localization/en.en.translation` is untouched.

## 3. Decisions (Provisional; for `PROVISIONAL-ENCOUNTER-LOCK-RULES` / per-level rows)

1. **L4: the walkway rioter goes onto the corridor floor (4250)** rather than a tower sub-lock. The walkway (top y 292, 600 px over the floor) is an optional climb under a meter-draining beam, and nothing else stands up there.
2. **L4: the cell-B rioter stays sealed in cell B** as a member. Breaking lock B is part of the puzzle, and the lock needs the puzzle anyway.
3. **L4: the corridor lock ends at 4760**, the low cover's east end. The tower and walkway beyond hold no enemy, and the Middle anchor (5000) stays outside with 74 px of swing clearance.
4. **L5: four locks, with area 3 split at the boat deck's west lip** (E-deck 6160–7520, boat deck 7540–8820). The ruling asks for step 3 after "area 3a" and the timer after "area 3". Each half is ≥ 1280 px only because the PreBoss anchor moved onto the stern rail.
5. **L5: a cleared lock's flood step waits until the hero stands on the next dry deck** (`HullStepDryFromX`, 16 px clearance).
   - Raising it at the clear would have flooded the deck the hero was still standing on whenever the last kill happened west of the next deck.
   - Worst case at Lincoln's submerged 125 px/s: a 1,700 px wade from D2 to E2 after the casing's clear, about 13 s submerged and roughly 50 HP of drowning.
   - The water still rises behind the hero on every clear, so the level is still a losing race.
6. **L5: the debut Eraser drops in when one E-deck slasher is left** (`WhenAliveAtMost(1)`), at (6850, 830), 110 px above E2. The old debut spawned it mid-air at (7260, 480); a lock spawn needs a floor within 120 px.
7. **L5: PreBoss anchor at 9060 and font at 9200**, both on the stern rail outside the hull flood (x ≥ 8800). The font keeps 140 px from the anchor and its circle ends 200 px short of the arena. The pre-boss beat and the stern-rail room trigger moved east of the boat deck's barrier.
   - The beat pauses gameplay, so it must not fire inside a lock.
   - A room trigger touched mid-fight would hand the camera to its room when the lock clears.
8. **L6: one Ash Road lock (5660–8500) with one OnEngage wave** (the four standards and the Warden). Splitting the row would change Easy's composition; keeping one row keeps Easy exactly as it was (slasher, legionnaire, slasher).
9. **L6: the front starts 240 px behind the hero (`EscapeHeadStartPixels`) when the road clears**, never west of 5700 or past 8800. The authored 5700 start would trail a hero who finished the fight down the road by up to 2,800 px and make the run a formality. 240 px is about one second at the front's speed, so the civilian stop costs the lead. A rewind can only pull the start back.
10. **L6: the road's second civilian moves to 8700**, into the run, keeping "the rescue costs seconds the player does not have".
11. **L6 and L7: the Chrono-Warden and the Level 7 Eraser now pay dust** (elite weight 5 within the unchanged required pool). A lock wave must be a manifest row, so an unlisted member cannot exist. Both are each row's last entry, so Easy is unchanged.
12. **L7: two locks spanning water** (quay 3200 px; boarding 2700 px from the Bonnet across the channel to the ramp), with barriers only on decks.
    - The Bonnet and pier deck is 900 px wide and the ramp 500 px, both under the 1280 px minimum, and a barrier needs floor.
    - The ramp crew musters (`AfterPreviousCleared`) only when the Bonnet and pier are clear, so the fight reads as a boarding action.
13. **L7: Middle anchor at 5700 and PreBoss anchor at 8680** on the flagship's west deck; the font at 9100, east of FlagshipFireA.
14. **Swing-reach clearances** use the shared helper `EncounterLockReach` (the longest Story basic hit, about 156 px), counted from a hero pressed against the exit barrier:

    | Anchor | Clearance |
    |---|---|
    | L4 Middle | 74 px |
    | L4 PreBoss | 214 px |
    | L5 Middle | 54 px |
    | L5 PreBoss | 74 px |
    | L6 Middle | 44 px |
    | L6 PreBoss | 394 px |
    | L7 PreBoss | 34 px (the tightest) |

    The L7 Middle anchor lists only the quay, which is far away.

## 4. Deviations from the plan

- **"The debut Eraser … never paying dust" (brief):** the `@0` row is unchanged and the debut still pays its allocated share. `RewardManifestTests.TheTitanicsRequiredPoolPaysExactlyFiftyFive…` requires that ("the Eraser debut paid nothing" is a failure). I read the brief's phrase as "never difficulty-scaled".
- **L5 flood steps rise "after the clear, once the hero is clear of them"**, not at the clear's instant (Decision 5).
- **L7 movers are not held, because none crosses a barrier.** The content test `NoMoverCrossesABarrierAndNoHaulOutLandsPastAnUnclearedExit` pins that. If a later layout puts a barrier in a skiff's or rope's sweep, it must wire `EncounterLock.IsHolding`.
- **`EraserDebutTrigger.cs` was edited** (§2, outside my ownership).
- **The L6 Warden and the L7 Eraser now pay dust** (Decision 11). The per-level totals are unchanged.
- **L7 Easy now spawns 7 enemies** (2 + 1 + 2 + 2), against 8 before, because the rows were re-partitioned to the locks. Normal and Hard are unchanged. L5's Easy and Hard counts are unchanged.

## 5. Tests

**New cases (+13):**

- `Level04ContentTests` 13 → 16:
  - `TheThreeApproachRoomsAreEncounterLocksOneWavePerManifestRow`
  - `TheAnchorsAndTheFontStandOutsideEveryLockAndBeyondASwingFromInside`
  - `TheCellBlockBarrierOpensOnlyOnceThePrisonersAreFreeAndItsGuardsAreDown`. Real frames: guards dead + prisoners locked = barrier shut; prisoners freed = gate open at once and barrier on the next tick; the PreBoss fracture is held until then.
- `Level05ContentTests` 14 → 17:
  - `TheFourRoomsAreEncounterLocksAndTheDebutEraserIsAMemberOfTheEDeckLock`
  - `TheAnchorsTheFontAndThePreBossBeatStandOutsideEveryLock`
  - `TheHullFloodRisesOnEachLockClearOnlyOnceTheHeroIsClearOfIt`. Real frames through all four locks: each step waits for the next dry deck; the debut Eraser joins the E-deck lock and is adopted for the bark; the boat deck's timer starts only on its clear.
- `Level06ContentTests` 14 → 17:
  - `TheForumTheVaultAndTheAshRoadAreEncounterLocksOneWavePerManifestRow`
  - `TheAnchorsAndTheFontStandOutsideEveryLockAndBeyondASwingFromInside`
  - `TheLavaFrontStartsOnlyWhenTheAshRoadLockClearsAHeadStartBehindTheHero`. Real frames: the front waits for the clear and starts behind the hero; the Warden is clamped inside the road.
- `Level07ContentTests` 21 → 24:
  - `TheTwoDecksAreEncounterLocksAndEveryMemberIncludingTheEraserSpawnsOnSolidDeck`
  - `NoMoverCrossesABarrierAndNoHaulOutLandsPastAnUnclearedExit`
  - `TheBoardingCrewMustersOnTheRampOnceTheBonnetAndThePierAreClear`. Real frames: a haul-out mid-fight stays engaged; the ramp crew musters after the pier crew; the Eraser bark fires.
- `EraserDebutTriggerTests` 8 → 9: `AnAdoptedLockSpawnedBodyBeginsTheAmbushOnceWithTheSameBark`.

**Rewritten in place (no count change):**
- `Level05ContentTests.OnlyCultistEnemiesCrewTheTitanicAndBothTiersResolve`: waves 1–3 → 1–`WaveCount`.
- `Level05ContentTests.ResumingAtAnyCheckpointRestoresASurvivableWaterState`: `HasWaveSpawned` → lock states, plus `PendingHullStep == 0`.
- `Level07ContentTests.TheNassauRosterMixesDeckhandsWithCultistsUnderOneCannonMaster`: waves 1–3 → 1–`WaveCount`.
- `Level07ContentTests.ResumingAtACheckpointKeepsClearedWavesClearedAndConfinesTheCamera`: `HasWaveSpawned` → lock states.
- `RewardManifestTests.TheTitanicManifestCarriesTheFoldedRowAndItsControllersSpawnTableAndEraserDebut` and `TheTitanicsRequiredPoolPaysExactlyFiftyFiveWhenEverySpawnedSourceIsCollected`: waves 1–3 → 1–`WaveCount`, and 4 → `WaveCount` + 1 rows.
- `PostBossChainTests.OnTheTitanicAPreBossApproachAfterTheOverseerFallsStillArmsTheAnchor`: restores the E-deck and boat-deck locks Cleared before the approach. The Middle-anchor resume rebuilds them Armed, and the PreBoss fracture is held until they clear.
- `Level06RockfallPhysicsTests` fixture: `EncounterLock.SuppressForTesting` in the constructor, restored in `Dispose`. The case stands the hero on the east pan inside the vault lock to measure the winch, not the fight.

**Suites that boot my scenes and needed no change:** `EncounterLockContentTests` (now non-trivial for 4–7), `ArenaGeometryContentTests`, `SecretCachePlacementTests`, `StoryHealingLoopTests`, `EncounterBaselineTests`, `EncounterCompositionTests` (the L6 room arrays and the L7 `SpawnTable` grouping still fit), `DustEconomyTests`, `PuzzleResetPlacementTests`, `CaptainSmithTakingTests`, `BossArenaFallNetTests`, `ArenaEnemyAttackGeometryTests`, `AbsenceBeatContentTests`, `SceneSmokeTests`, `ScenePoolConfigTests`, `StageLightingRigTests`, `PendulumRopeBarTests`, `ThoroughRunCollectionWalkTests`, `CampaignRouteTests`, `BossPhaseRuleMatrixTests`, `ActOneTwoScriptTests`, `StoryLevelControllerBaseTests`, `ActThreeChainingTests`, `EncounterLockTests`, `EncounterLockRulesTests`, `BossArenaLeashTests`, `SummonRewardProvenanceTests`, `RewardSourcePersistenceTests`.

**Runs (lock held for each, orphans checked):**
- The nine touched suites: **105/105**.
- 27 shared suites that boot levels 4–7: **204/204**.
- Full suite: **3138/3138** (§6).

**Delta: +13. Expected `Total:` = 3125 + 13 = 3138**, and the `[TestCase]` count under `tests/` is 3138.

## 6. Measurements

- **Full suite** (lock held, orphans drained): `Passed! - Failed: 0, Passed: 3138, Skipped: 0, Total: 3138` in 1 m 37 s = 3125 + 13. After the run, the one new `EraserDebutTriggerTests` case was renamed (no logic change); `EraserDebutTriggerTests` + `Level05ContentTests` then ran 26/26.
- **Smoke runs** (`--quit-after 300`): Levels 4, 5, 6 and 7 all exit 0 with no script error.
  - Levels 4 and 7 print the known at-exit noise (6 ObjectDB / 3 resources). Level 2, the Phase A control, printed the same. A `--verbose` rerun of Level 4 shows only the placeholder dialogue chirp `AudioStreamWAV` (2 / 1 on that run).
  - Levels 5 and 6 are clean.
- **Bot runs** (`play`, Joan, Normal, `--max-seconds=420`, tag `gul4a`). The bots are not yet lock-aware (GU-BOT works on that in parallel).

  | Level | End | Locks | Notes |
  |---|---|---|---|
  | 4 | `collapse(Death)` at 157.7 s, both checkpoints | all three engaged and cleared; every member killed, including the cell-B rioter after lock B broke (frame 4884) | 4 deaths in the courtyard to the Governor's Guard and its summoned rioters |
  | 5 | `collapse(Death)` at 186.4 s, progress 10451 | all four cleared, the debut Eraser included | 4 deaths to the Tidal Overseer. The bot struck neither the Middle nor the PreBoss anchor. It ran past Middle (6100) into area 3a (engaged at 6204) and passed PreBoss (9060) while approaching the boss, so it had no checkpoint to resume from. A bot targeting gap; the anchors are 74 px from the next lock's engage line, as Level 2's are |
  | 6 | `timeout` at 420 s, progress 5082 | forum and vault members all killed (6 of 6) | Stalled at the rockfall: the bot never breaks the winch's wedge (WedgeLock 45 HP all run). This is the known bot gap ("the bot does not stand on the east pan…"), not a lock fault |
  | 7 | `collapse(Death)` at 291.7 s, progress 3314 | — | 4 deaths on the launch yard (3535–3550, 672) to the yard deckhand (wave 2) standing on the same yard. The bot had given up on it (`bot_gave_up_unreachable` 9; the 720-frame give-up) and waited at the closed quay barrier for rope A. GU-BOT's lock targeting (no give-up inside an engaged lock) is the fix; the member is on the route and reachable |

## 7. Not delivered / open

- **Nobody has played any of this.** Unjudged by a person:
  - the pacing of Level 7's 2,700 px boarding lock, where four ramp members wait at the water's edge for a hero on a skiff;
  - the flood timing;
  - the front's 240 px head start;
  - the cell-B rioter pressing against lock B.
- **Mover holds are not wired anywhere** (none needed in 4–7).
- **The L5 and L7 PreBoss anchors and the L5 Middle anchor sit 54–74 px from a lock edge.** A hero who runs on without striking engages the next lock, and the anchor stays held until that lock clears. Not a soft-lock, but a bot (and maybe a player) can overrun it.
- **The bots** (GU-BOT) still give up on an engaged-lock member after 720 frames, do not strike anchors before entering a lock, and do not solve the L6 winch.
- **Old saves:**
  - A save parked mid-level with old `*_wave_N` / `*_roomN_wave` baselines falls back to the authored baseline (version 3), as GU-E designed.
  - Claimed reward sources `level_05.wave3#2..4` and `level_07.wave2#2` / `wave3#3..4` from before the re-partition no longer match a row. A pre-update mid-level save can repay up to those sources once. Recorded, not migrated: no save change was allowed.
- **L6's Warden and L7's Eraser now pay dust** (Decision 11). DUST_ECONOMY's per-level totals are unchanged.

## 8. For the closeout

**AGENTS.md:**
- The scene-architecture bullet "Levels 2–15 are built on `StoryLevelControllerBase`": Levels 4–7 author locks.
- The Level 5 facts:
  - the PreBoss anchor at 9060 and the font at 9200;
  - flood steps on lock clears (Decision 5);
  - the debut Eraser is an E-deck lock member, and `EraserDebutTrigger.RouteTriggerSpawns` / `AdoptEraserDebut` exist.
- The Level 6 facts: the escape starts at the road lock's clear, 240 px behind the hero; the civilian is at 8700; the Warden pays dust.
- The Level 7 facts:
  - Middle anchor 5700, PreBoss anchor 8680, font 9100;
  - the Eraser on the ramp in wave 4;
  - two locks spanning water;
  - the "the Level 7 Eraser … over open water" bug is fixed.
- The Level 4 facts: the walkway rioter is on the floor; the courtyard gate opens on the puzzle while the cell lock needs puzzle + clear.
- Baseline count.

**Ledger rows:**
- `PROVISIONAL-ENCOUNTER-LOCK-RULES` gains the per-level Decisions 1–14.
- A new `PROVISIONAL-L5-FLOOD-ON-LOCK-CLEAR` (Decision 5).
- A new `PROVISIONAL-L6-ESCAPE-HEAD-START` (240 px, rewind pull-back).
- A new `PROVISIONAL-L6-L7-ELITE-DUST`: the Warden and the Level 7 Eraser enter manifest rows; within-level redistribution only.
- `PROVISIONAL-FONT-PLACEMENT` amended with the L5 (9200) and L7 (9100) fonts.
- `VERIFY-PAR-SECONDS`: Level 5's 270 s par now covers four locks.
- Close the Level 7 Eraser-over-water bug (critic §3.5).

**`design-godot.md` lines owed:**
- The Level 4–7 level descriptions (§3; the recon index):
  - L5's flood escalation is per cleared area;
  - L6's Ash Road crowd is fought before the lava run, and the second civilian is in the run;
  - L7's checkpoints;
  - L4's cell-block clear needs both prisoners.
- The F05 dust table notes for L6 and L7 (the elites now carry their elite weight).

**Merge notes:**
- Only Levels 4–7 files, three manifests, one scene line, `EraserDebutTrigger.cs` (additive), and my levels' cases in three shared test files.
- No framework file touched.
- No `en.csv` change.

## Framework issues found

None blocking. One observation for the framework owner: `EncounterLock` has no public way to adopt a member it did not spawn. A level with a scripted spawner (the Level 5 debut) has to move the spawn into a lock wave and adapt the script, as done here. An `AdoptMember(EnemyController, anchor)` would let scripted spawners keep their own spawn call.
