# GU-L8 handoff: encounter locks for Levels 8–11, gameplay update 2026-10-12

Workstream GU-L8 of Phase B of `docs/GAMEPLAY_UPDATE_2026-10-12_PLAN.md` (§2.3, §3.2, §5). User request 3: "you enter rooms or areas that you have to clear of enemies … You no longer have the option of jumping past them." Story only. Branch `worktree-wf_6f019357-a19-3`, based on `7aaae7d` (main with Phase A and its review fixes; suite 3125).

**Nobody has played or looked at any of this.** The evidence is headless: the suites below, a 300-frame smoke run of each level, and one `play` bot run per level (§6).

Every level follows GU-E's authoring guide and Level 2's pattern: locks built in `BuildLevel` through `BuildEncounterLock`, one lock wave per manifest `RequiredWaves` row, every checkpoint listing the locks before it, the old wave triggers / latches / `SpawnInitialEnemies` / `SpawnWave` / `MarkEncounterCleared` / wave encounter IDs deleted, `MarkWavesClearedThrough` kept for non-encounter world state, and each Middle / PreBoss anchor outside every area and out of basic-swing reach from inside the locks its baseline lists.

## 1. Shipped

### Level 8 — Alexandria (`Level08Controller`, `Level_08_Egypt.tscn` untouched)

| Lock (const) | ID | Area | Entry barrier | Exit barrier | Wave (row, trigger) | Conditions |
|---|---|---|---|---|---|---|
| `LockDunes` | `level_08_egypt_lock_dunes` | x 300–3200, full height | 280–300 | 3200–3220 | `level_08.dune`, OnBuild | — |
| `LockSiegeLine` | `level_08_egypt_lock_siege_line` | x 3220–5460, full height | 3200–3220 | 5460–5480 (surface, sky to y 700) | `level_08.siege_line`, **OnEngage** | — |
| `LockTomb` | `level_08_egypt_lock_tomb` | x 5720–7000, full height (the shaft and the west corridor) | 5700–5720 (sky to the tomb floor 1620) | 7000–7020 | `level_08.tomb`, OnBuild | — |
| `LockVault` | `level_08_egypt_lock_vault` | x 7020–8310, **y 1040–1800** (the corridor band only) | 7000–7020 | at the vault door (8310–8330), y 1000–1620 under the gate column | `level_08.vault`, OnBuild | `RequiredPuzzleIDs = { level_08.hieroglyph_lock }` |

- **The shaft is no escape** (ruling): the siege line's exit barrier stands on the surface 240 px short of the shaft lip (5700); the anchor between them is the Middle.
- **The vault's area is the corridor band** (from the ceiling's underside, `VaultLockTopY` 1040, down), so a hero who climbs onto the corridor's roof from the shaft can never engage the vault out of reach of its members.
- **The vault needs the seal** (ruling): the door still opens on the solve (`OpenVault`); the vault's barrier opens on the clear, which needs the solve and the ward detail dead. Its objective is the level's own (the seal's progress); the HUD counts the ward.
- Baselines: cp0 none; cp1 dunes + siege line; cp2 all four. `MarkWavesClearedThrough(cp1)` now sets nothing (the Middle is on the surface, before the descent); cp2 still sets `_descended`. The room 3 trigger now sets `_descended` (the tomb wave trigger used to). `RestoreLevelObjective()` → `RefreshObjective()`.
- Moved / added (all constants are public on the controller):
  - **Middle anchor 6520 → `MiddleCheckpointX` 5640** (the shaft lip, y 650; it stood inside the tomb fight). It is now built in room 2.
  - **PreBoss anchor 8420 → `PreBossCheckpointX` 8490** (swing reach from the vault).
  - **Absence trigger 8500 → `AbsenceTriggerX` 8560**, so the PreBoss respawn body (8470–8510) never stands in it.
  - **Carved relief (6700, 1180) → `CarvedReliefPosition` (7280, 1360)**, inside the vault lock and still west of the glyph wall. Once the vault engages its entry shuts, and a relief left in the tomb would leave only brute force.
  - **Tomb spawns:** slasher 6900 → 6800 (patrol 6680–6960, inside the area), javelin ward 7320 → 6450 (the foot of the shaft).
  - **New wall `ShaftWestWallX` (5680–5700, y 700–1660)** closing the tomb corridor's west end under the surface. The corridor floor started at 5700 with nothing west of it, so a hero walking or knocked off its end fell out of the level (a pre-existing hole the tomb lock's open entry would have exposed after the clear).
  - **`Room3CameraBounds` x 5760 → 5680** (width 2640), so a hero dropping off the lip into the shaft is not off screen at the 1.5 zoom before the tomb lock takes the camera.
  - The siege-line room trigger 3200 → 3260 (`SiegeLineLockMinX + 40`).

### Level 9 — Hakata Bay (`Level09Controller`, `Level_09_Berlin.tscn` untouched, `level_09_rewards.tres`)

| Lock (const) | ID | Area | Entry barrier | Exit barrier | Wave (row, trigger) | Conditions |
|---|---|---|---|---|---|---|
| `LockSeawall` | `level_09_berlin_lock_seawall` | x 300–2860 | 280–300 | 2860–2880 | `level_09.wave1`, OnBuild | — |
| `LockLanternWatch` | `level_09_berlin_lock_lantern_watch` | x 3150–6350 (one room wide) | 3130–3150 | the boarding gate (6350–6370), y 300–1200 under its column | `level_09.wave2`, OnBuild | `RequiredPuzzleIDs = { level_09.cut_feeds }` |
| `LockStormAnchor` | `level_09_berlin_lock_storm_anchor` | x 6620–8560 | 6600–6620 | 8560–8580, y 800–1200, **under the flagship's partial wall** | `level_09.wave3`, **OnEngage** | — |

- **The Lantern Watch clears on "every line cut AND its watch down"** (ruling), through the feed puzzle as a required puzzle. **The lanterns stay live** through the fight (nothing changed in the beams). The first cover pocket (2890–3130) stays a breather between the seawall and the Watch. The Lantern Watch's objective is the level's own (the cut count).
- **The near-capture fires before the ship's lock can engage** (ruling): `NearCaptureTrigger` now runs the level's full height (`NearCaptureTriggerSize` 60 × 1400, centred at y 700; it was 400 tall), so no jump carries the hero over it, and it lies wholly west of the lock (6470–6530 against 6620).
- **The flagship's partial wall is closed below y 800 by the lock's exit barrier** (ruling). The wall moved 8620 → **`FlagshipWestWallX` 8560** to make an antechamber for the PreBoss anchor and the Font.
- `LockWaveFor(int wave, trigger)` builds a lock wave from `SpawnTable` (shape unchanged, so `EncounterCompositionTests` still reads it), with the ±220 patrol legs (`PatrolHalfSpanPixels`). `WaveRowID(wave)` = `level_09.wave{n}`.
- **Manifest:** `level_09.wave2` gains **`chrono_warden`** as its fifth entry. The Warden was spawned on Normal and Hard since Package 11 A7a but stood past the end of the 4-entry row, so it paid nothing; the lock content rule requires each wave to be exactly its row. Spawn counts are unchanged on every difficulty (Easy 3, Normal 5, Hard 5); the required pool (15) and every budget row are unchanged; only the per-source split moves (the Warden now takes an elite share where it spawns).
- Baselines: cp0 none; cp1 seawall + Lantern Watch; cp2 all three. `MarkWavesClearedThrough` now forces the gate open and the strip dark for **cp1 as well as cp2** (the Middle stands past the gate, and its baseline lists the Lantern Watch, which needs every line cut). `RestoreLevelObjective()` re-posts the cut count, or "reach the flagship" once the gate is open.
- Moved (all constants public):
  - **Middle anchor 5200 → `MiddleCheckpointX` 6570** (just past the boarding gate and east of the snare; it stood inside the lantern fight). Built in room 3.
  - **PreBoss anchor 8480 → `PreBossCheckpointX` 8740** (the flagship's antechamber; it stood inside the ship's fight). Built in room 4.
  - **Font (8150, 1200) → (8610, 1200)** (antechamber, between the ship's exit barrier and the PreBoss anchor).
  - **Spawns:** the Lantern Watch trooper 4100 → 4420 (it overlapped the west relay's cover slab, 3960–4280); the walkway archer (5900, 460), 688 px up the tower climb, → the deck at the tower's foot (5650, 1150); the gantry archer (7350, 570), whose patrol ran off the gantry, → the deck under it (7350, 1150).
  - The Lantern Watch room trigger 2880 → 2920.

### Level 10 — The Globe (`Level10Controller`, `Level_10_Globe.tscn` untouched)

| Lock (const) | ID | Area | Entry barrier | Exit barrier | Wave (row, trigger) |
|---|---|---|---|---|---|
| `LockYard` | `level_10_globe_lock_yard` | x 300–2540 | 280–300 | 2540–2560 | `level_10.room1`, OnBuild |
| `LockStage` | `level_10_globe_lock_stage` | x 2560–5740, full height (**the cellar included**) | 2540–2560 | 5740–5760, sky to **y 1180** (the east cellar wall's top) | `level_10.room2`, **OnEngage** |
| `LockGalleries` | `level_10_globe_lock_galleries` | x 6080–8330 (to the bridging span's east end) | 6060–6080 | 8330–8350, sky to the tiring-house boards (**the bridge is the exit**) | `level_10.room3`, OnBuild |

- **Stage fights over cycling trapdoors** (ruling): the stage lock includes the cellar, whose walls (2540, 5740) close the area's ends below the boards, and the traps keep cycling. Its exit barrier stands on the east cellar wall and runs down to that wall's top, so the tiring-house stair out of the cellar (5560–5750) stays inside the lock. Nothing is lost by falling: the hero is still inside; a mob that falls is fought in the cellar (reachable through the traps and the stair) or, if melee and still, relocated by the stall watchdog.
- **The audience's stillness clock pauses while a lock is engaged** (ruling; level code, not the framework): `AudienceHeldByEncounterLock` (= `ActiveEncounterLock != null`). While it holds, `TickAudience` neither builds nor resets the idle seconds (the position baseline still follows the hero) and resumes from the paused value once the lock clears or reverts. A throw already telegraphed still resolves.
- **Galleries actors on the route** (ruling): the upper-tier slasher (7940, 370) and the middle-tier prompt-boy (7000, 610, 468 px up, past the weakest double-jump rise of 265) moved to the yard floor (7400 and 6900); the floor prompt-boy 5960 → 6300 (into the area). The lower-tier slasher (6620, 830; 248 px up) stays.
- **No gate column** (ruling: lock barriers are not gates); `ArenaGeometryContentTests`' expected count for Level 10 stays 0.
- Baselines: cp0 none; cp1 yard + stage; cp2 all three. The test seams `Room2WaveLatched` / `Room3WaveLatched` and the constants `Room2WaveEncounter` / `Room3WaveEncounter` are deleted. `RestoreLevelObjective()` → `RefreshObjective()`.
- Moved (constants public): **Middle anchor 5400 → `MiddleCheckpointX` 6040** (the galleries' floor under the lower tier, east of the ground-level step at 5800–6000 so the respawn body clears it; it stood in the stage fight). **PreBoss anchor 8420 → `PreBossCheckpointX` 8460** (swing reach from the yard floor against the yard wall). Room triggers: the stage's 2560 → 2600, the tiring-house's 8350 → 8390.

### Level 11 — Gettysburg (`Level11Controller`, `Level_11_Gettysburg.tscn` untouched)

| Lock (const) | ID | Area | Entry | Exit | Waves (row, trigger) | Conditions |
|---|---|---|---|---|---|---|
| `LockSeminaryRidge` | `level_11_gettysburg_lock_seminary_ridge` | x 300–3200 | 280–300 | 3200–3220 | `level_11.room1`, OnBuild | — |
| `LockWheatfield` | `level_11_gettysburg_lock_wheatfield` | x 3220–6090 | 3200–3220 | 6090–6110 (west of the 6200 sandbags) | `level_11.room2`, OnBuild | `RequiredBreakables = { ShieldArrayAlpha }` |
| `LockAngle` | `level_11_gettysburg_lock_angle` | x 6400–8890 | 6380–6400 | the breastwork (8890–8910), y 440–900 under its column | `level_11.room3` OnBuild; `level_11.angle` **WhenAliveAtMost(1)**; `level_11.elite` **WhenAliveAtMost(1)** | `RequiredBreakables = { Alpha, Bravo }` |

- **The breastwork opens on the arrays; the lock's barrier on the arrays AND the clear** (ruling): `OpenBreastwork` is unchanged; the Angle's exit barrier fills the breastwork gate and needs both arrays and every member down.
- **Alpha is the Wheatfield's condition** because the Angle needs both arrays and shuts its entry behind the hero: without it a run that left Alpha standing would be shut out of it behind the Angle's closed entry (a soft-lock). A resume at the Middle anchor (past the Wheatfield) breaks Alpha (`MarkWavesClearedThrough(cp1)` → `ForceArrayDestroyed(_arrayAlpha)`, the Level 2 generator precedent).
- **The Angle escalates:** the ridge line is pre-placed; the stone wall pair comes over the wall when the line is down to one; the Cyber-Cavalry Commander rides in when the wall is down to one. Both spawn-ins arrive engaged (with the spawn tear).
- **Artillery stays live** (ruling): nothing changed in the columns.
- **Spawns that overlapped cover moved clear of it** (ruling), each to the gap just behind (east of) the cover it stood in, short of the next blast column: ridge rifle 1500 → 1320 (patrol 1240–1390); Wheatfield rifles 3900 → 4080 (patrol 4050–4110), 4500 → 4620, slasher 5000 → 5170 (patrol 5145–5210), rifle 5600 → 5730; Angle rifle 6900 → 7040; the Angle slasher's patrol 7150–7370 (into the 7425 fence) → 7225–7300.
- **GU-C's widened railcut perch (340 px) stays** (ruling).
- Baselines: cp0 none; cp1 ridge + Wheatfield; cp2 all three. `RestoreLevelObjective()` → `RefreshObjective()`.
- Moved (constants public): **Middle anchor 6250 → `MiddleCheckpointX` 6340** (off the 6200 sandbags, out of the Wheatfield fight, clear of every blast column). **PreBoss anchor 9060 → `PreBossCheckpointX` 9080** (more than a few pixels of swing clearance). Room triggers: the Wheatfield's 3200 → 3260, the Angle's 6420 → 6460.

## 2. Files changed

- `scripts/Environment/Level08Controller.cs`, `Level09Controller.cs`, `Level10Controller.cs`, `Level11Controller.cs`
- `resources/Content/reward_manifests/level_09_rewards.tres` (the Warden in `level_09.wave2`)
- `tests/ContentValidation/Level08ContentTests.cs`, `Level09ContentTests.cs`, `Level10ContentTests.cs`, `Level11ContentTests.cs`
- `tests/ContentValidation/LevelLockSwingReach.cs` (+ `.uid`), new: a non-suite static helper (the roster's longest basic swing, from Level02ContentTests' private helper) the four level suites share
- Shared test files, my levels' rows and cases only: `tests/Unit/EncounterBaselineTests.cs` (the Globe cases), `tests/Unit/NearCaptureBeatTests.cs` (the Level 9 placement case), `tests/ContentValidation/RewardManifestTests.cs` (one new Level 9 case)
- **Outside a pure row edit:** `tests/ContentValidation/EncounterLockContentTests.cs` — a per-lock row table `EntryFloorByLock` (one entry: the Globe's stage, its boards at y 1120) and a one-line lookup in the post-height rule. See §7 and the framework issue.
- No scene file, no `en.csv` (every lock reuses `encounter_lock_objective_clear` or the level's own objective), no save payload, nothing under `scripts/FighterSim/`, no framework file.

## 3. Decisions (Provisional, for `PROVISIONAL-ENCOUNTER-LOCK-RULES` / a GU-L8 row)

1. **Lock layouts and wave triggers** as in the tables (OnBuild for posted defenders, OnEngage for the siege line, the stage cast and the ship's crew, WhenAliveAtMost(1) for the Angle's reinforcements).
2. **Level 8's vault area is a y-band** (1040–1800) so the corridor roof never engages it.
3. **The carved relief moved into the vault lock** so the answer is reachable once the vault has shut the way back.
4. **The Warden is ledgered** in `level_09.wave2` (it was spawned but unpaid); spawn counts unchanged, the split moves.
5. **Level 9's Middle anchor sits after the Lantern Watch** (6570), not before it (3010 was the other option). It is nearer the 0.45 remaining-route fraction and closes the level's centerpiece; a Collapse inside the Lantern Watch now restarts from the entry anchor.
6. **The near-capture trigger is full height** (60 × 1400).
7. **The flagship's partial west wall moved 60 px west** (8620 → 8560) to fit the PreBoss anchor and the Font between the ship's barrier and the arena while keeping both 30 px outside the arena and the anchor out of swing reach.
8. **The Globe's stage lock includes its cellar**, with traps cycling (the ruling), rather than holding the traps shut or cutting the cellar out of the area (either of which leaves a member that falls in warded-or-unreachable).
9. **The audience's clock pauses (not resets) while engaged**, and an armed throw still resolves.
10. **Level 11's Wheatfield requires Alpha**, and a Middle resume breaks it.
11. **Every Middle / PreBoss anchor clears the longest basic swing** from inside the locks it lists by at least 14 px; the respawn body of every anchor clears every room trigger, snare and absence trigger it could fire on load.

## 4. Deviations from the plan

- **`EncounterLockContentTests` was edited beyond a pure row** (one lookup line plus a one-entry table). Its post-height rule measures from an area's lowest surface; the ruling's stage-with-cellar lock is the case it cannot express. The alternative was gaming the rule (a 200 px-thick cellar floor) or abandoning the ruling.
- **Level 9's Middle moved past the Lantern Watch** rather than into a split of it: the plan allowed a split at ~4300, but both lantern beams cover the only ground between 3143 and 5016 apart from the cover pocket at 3960–4280, and no anchor there clears both 1280 px-minimum halves and the swing rule.
- **Level 8's Middle moved up to the surface** (the shaft lip) — the tomb and vault together are 2620 px, so two 1280 px locks leave no gap for it in the corridor.
- **Two pre-existing geometry bugs fixed in passing**: Level 8's open tomb-corridor end (the wall) and Level 8's room 3 camera starting 60 px east of the shaft.

## 5. Tests

New cases (+17):
- `Level08ContentTests` (+4): `TheFourFightsAreEncounterLocksOneWavePerManifestRow`, `TheMiddleAndPreBossAnchorsStandOutsideEveryLockBeyondSwingReach`, `TheTombCorridorIsClosedAtItsWestEnd`, `TheVaultClearsOnlyOnceItsSealIsSolvedAsWellAsItsWardDown` (real scene, real frames: the roof engages nothing; the corridor engages the vault; dead ward + unsolved seal holds; the solve clears).
- `Level09ContentTests` (+5): `TheThreeFightsAreEncounterLocksOneWavePerManifestRow`, `EveryMemberStandsOnTheDeckOrALowRubblePostAndNoneInsideACoverSlab`, `TheMiddleAndPreBossAnchorsAndTheFontStandOutsideEveryLockBeyondSwingReach`, `ResumingAtTheMiddleOpensTheGateDarkensTheStripAndLeavesTheShipArmed`, `TheLanternWatchClearsOnlyOnceEveryLineIsCutAsWellAsItsWatchDown` (real frames; the lanterns stay live while the watch holds).
- `Level10ContentTests` (+3): `TheAudienceStillnessClockPausesWhileALockIsEngaged`, `TheThreeFightsAreEncounterLocksOneWavePerManifestRow`, `TheMiddleAndPreBossAnchorsStandOutsideEveryLockBeyondSwingReach`.
- `Level11ContentTests` (+4): `TheThreeFightsAreEncounterLocksOneWavePerManifestRow`, `NoSpawnOrPatrolPointStandsInsideACoverPiece`, `TheMiddleAndPreBossAnchorsStandOutsideEveryLockBeyondSwingReach`, `TheAngleEscalatesAndOpensOnlyOnBothArraysDownAndEveryMemberDown` (real frames: the waves arrive at "alive at most 1", the dead Angle holds while Bravo stands, the breastwork and both barriers open after).
- `RewardManifestTests` (+1): `HakataBaysLockWavesAreItsRowsAndPayExactlyFifteenIncludingTheWarden` (every difficulty, the real issue/claim path).

Rewritten in place (no count change):
- `EncounterBaselineTests`: `ReloadingBeforeAndAfterACheckpointAppliesTheNamedNonstandardBaseline`, `HardsInertMiddleNoLongerShiftsThePreBossBaseline`, `TheResumeReaderUsesTheCommittedRecordAndRejectsAStaleLayoutWhileClaimsAreRetained`, `ActivationCommitsTheAnchorsBaselineWithItsLayoutVersion` — the Globe's wave encounter IDs and latch seams → its lock IDs and `RestoredCleared` / `State` (a new private `GlobeLocks()` string).
- `NearCaptureBeatTests.TheBeatIsArmedInTheStormAnchorShipBeforeItsWaveAndIsNotAnEnemy` → `TheBeatIsArmedInTheStormAnchorShipBeforeItsLockAndIsNotAnEnemy` (pinned against the lock's area and the full-height trigger, not the retired room-3 wave trigger).
- `Level11ContentTests.EveryArtilleryGapHoldsCoverAndNoCoverStandsInsideABlastColumn` (checkpoint x read from the controller constants) and `ResumingPastTheBreastworkNeverResealsTheRailcut` (a Middle resume now finds Alpha broken, Bravo standing, the breastwork sealed; it asserted no array broken).
- `EncounterLockContentTests.EveryMemberSpawnStandsInsideItsAreaOnAFloorTheSlowestHeroReaches` (the `EntryFloorByLock` row).

Runs (each holding `D:/Projects/.ftt-gdunit-lock`, `GODOT_BIN` set, warm cache, orphans checked):
- My eight suites first: 104/105 (the stage post-height rule, above), then `EncounterLockContentTests` 9/9 after the row.
- 29 affected suites (the four level suites, `EncounterLock*`, `EncounterBaselineTests`, `EncounterCompositionTests`, `NearCaptureBeatTests`, `RewardManifestTests`, `DustEconomyTests`, `SecretCachePlacementTests`, `StoryHealingLoopTests`, `ArenaGeometryContentTests`, `PuzzleResetPlacementTests`, `AbsenceBeatContentTests`, `ScenePoolConfigTests`, `PendulumRopeBarTests`, `Level10BossRelocationTests`, `BossArenaFallNetTests`, `StoryLevelControllerBaseTests`, `ThoroughRunCollectionWalkTests`, `SceneSmokeTests`, `CampaignRouteTests`, `StageLightingRigTests`, `ResonanceRespecTests`, `BossPhaseRuleMatrixTests`, `PostBossChainTests`, `LevelDeathRewindDiagnosticTests`, `StoryCameraRigTests`): **282/282**.
- **Full suite, twice** (the second after the last code edit): `Passed! - Failed: 0, Passed: 3142, Skipped: 0, Total: 3142` (1 m 35 s, then 1 m 39 s). **3142 = 3125 + 17**; the `[TestCase]` count under `tests/` is 3142.
- Build: 0 errors; only the three pre-existing warnings (`CS8632`, `CS0649`, `KLSG_ECS004`).

## 6. Measurements

- The roster's longest Story basic swing (widest reach profile, every AttackRange minor bought) reaches ≈156 px past the pivot; the weakest roster double-jump rise (the content rule's post limit) is **265 px**.
- Swing clearances (strike surface west edge minus the reach): L8 Middle 14 px, L8 PreBoss 14, L9 Middle 54, L9 PreBoss 14, L10 Middle 134, L10 PreBoss 14 (from the yard floor against the yard wall; a hero on the bridge is 450 px above the anchor), L11 Middle 84, L11 PreBoss 24.
- Smoke: `Level_08_Egypt`, `Level_09_Berlin`, `Level_10_Globe`, `Level_11_Gettysburg` `--quit-after 300` all exit 0 with no script error; the at-exit "6 ObjectDB instances / 3 resources in use" line appears nondeterministically on all four and equally on untouched Level 7 in control runs (Level 12 printed none), as CLAUDE.md describes.
- Bots: see §9.

## 7. Not delivered / open

- **Nobody has played any of it.** Pacing is untested by a person: the Lantern Watch fought inside live lantern sweeps, the Globe's stage fight over cycling traps with the cellar in the lock, the Angle's three-step escalation under artillery, the 3200 px Lantern Watch area waking five members at once.
- **The bots** (GU-BOT) do not know about locks in this branch; a stall at a lock is informative, not a failure (§9).
- **Level 9's Collapse inside the Lantern Watch restarts at the entry anchor** (Decision 5).
- **`EncounterLockContentTests`' stacked-area row** is a stopgap; a general rule (measure a post from the floor the hero engages on) is the orchestrator's call.
- **Level 10's audience still runs in the boss arena** (unchanged; not a lock).
- **The Galleries' lower-tier slasher stays on its tier**, the foot of the climb. While the hero stays on the floor, the stall watchdog returns it to the same post every 10 s. That is harmless, but each return logs an `[encounter-lock] … (stall)` line (the Level 10 bot run).
- **A Middle anchor walked past is refused from inside the next lock** until that lock clears. This is the framework guard's "hero inside an uncleared lock" half. Every Middle in my four levels stands 40–80 px short of the next area, so a hero who does not strike it first cannot strike it again until the next fight is over (the Level 10 bot looped on exactly this).
- The swing-reach rule is measured per level suite, not in `EncounterLockContentTests`.

## 8. For the closeout

**AGENTS.md:** the Level 8–11 facts the locks moved (the Middle and PreBoss anchors, Level 9's Font, the flagship wall, the Globe's audience pause, Level 11's Wheatfield needing Alpha, the Warden in Level 9's ledger); the baseline count (3142 after this branch alone).

**Design-master lines owed:** the per-level encounter descriptions for Alexandria (the shaft no longer an escape; the vault seal solved inside a lock), Hakata Bay (the Lantern Watch cleared on lines cut and watch down; the near-capture before the ship's fight), the Globe (the audience pauses during a lock fight; the stage fight over the cellar), Gettysburg (Alpha before leaving the Wheatfield; the Angle's escalation) — in `design-godot.md` Sections 2–3 and the Level 8–11 entries the recon indexes.

**Ledger rows:** a `PROVISIONAL-*` row (or `PROVISIONAL-ENCOUNTER-LOCK-RULES` amendments) carrying Decisions 1–11; amend `VERIFY-PAR-SECONDS` (Levels 8–11 now fight every room); note the Level 9 Warden's ledger entry under the F05 dust rows (`DustEconomyTests` totals unchanged).

## 9. Bot runs

One `play` run per level, Joan, Normal, `--max-seconds=420`, seed 0, tag `gul8` (`run_playtests.ps1 -Levels 8,9,10,11 -Characters joan -Bots play -Difficulty normal -Parallel 4 -MaxSeconds 420 -Tag gul8`), the lock held. **The bots on this branch do not know about locks (GU-BOT is teaching them in parallel), so a stall at a lock is informative, not a failure.**

| Level | End | Seconds | Checkpoints | Boss | Deaths | Killed / seen | Where it stopped and why |
|---|---|---|---|---|---|---|---|
| 8 | `level_complete` | 131.5 | 3 | down | 3 (boss 265 of 371 damage) | 11 / 11 | All four locks cleared, the vault's seal solved inside its lock, `passed_while_alive` 0. |
| 9 | `left_level:collapse(Death)` | 151.2 | 2 | — | 4 | 14 / 15 (all but the boss) | All three locks cleared. Three deaths came in the storm-anchor ship at x 6656–6780 to the Thunderbomb Engineer (184 damage), whose crew spawns engaged on entry as wave 3 always did. The fourth came at the boss. The bot targeted the PreBoss anchor (8740) at frame 7621 but switched to the boss before striking it, so it had no refill for the last death. |
| 10 | `timeout` | 420 | 1 | — | — | — | Stalled in the Galleries with the lower-tier slasher (6620, 830) alive. The bot never climbs to fight it, and the stall watchdog returns it to its post every 10 s. The bot also looped on striking the Middle anchor (6040) from inside the engaged Galleries, where the guard refuses it: it had walked past the anchor before engaging. |
| 11 | `timeout` | 420 | 2 | — | 0 | 7 / 11 | Both arrays broken, then stood at the Angle's exit barrier (8870) with the ridge line alive. The bot passed the three riflemen instead of fighting them, so the stone wall pair and the commander never spawned. |

Reports: `%APPDATA%/Godot/app_userdata/Fighters Through Time/playtest/L0{8,9}_joan_play_normal_gul8.json`, `L1{0,1}_…`; summary `summary_normal_gul8.csv`. The Level 10 and 11 stalls are the GU-BOT gap (lock members targeted like any mob, with the 620 px cap and the 720-frame give-up), not unreachable content: the Level 10 slasher stands 248 px above the yard floor (the roster's weakest double jump rises 265, and the ground-level step at 5900/1000 makes it a single jump), and the Level 11 riflemen stand on the floor.
