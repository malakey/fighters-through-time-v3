# GR-LV4: Levels 10, 11 and 12, the Siege Cannon's railcut and the Gravity Overseer's pad

**Nobody has played any of this.** Every claim below is a headless suite, a real-frames script or a bot run. Nobody has looked at the Stage Front, the Sunken Road, the Mare Crossing, the three routes, the railcut fences, the warp, the gravity zones or the gust vents.

## What shipped

Each level has a stretch carved out of a lock, a shortcut-only route and +30 s of par. Levels 10 and 11 have swarm rows; Level 12 has none (ruling 1b++). Every lock keeps its ID and wave table apart from the swarm rows. Nothing moved east of a stretch.

| | Level 10 | Level 11 | Level 12 |
|---|---|---|---|
| Lock carved | Yard 300–2540 → 300–2000; Stage 2560–5740 → 2900–5740 | Seminary Ridge 300–3200 → 300–2000 | Tranquility 320–3000 → 320–2000 |
| Stretch | The Stage Front, 2020–2880 | The Sunken Road, 2020–3200 | The Mare Crossing, 2020–3000 |
| Deck, trench floor | 1120, 1240 | 900, 1020 | 1900, no trench |
| Line (tops) | deck → 2170/1002 → crumbling 2430/932 → one-way 2650/930 → boards | Level 2's Rampart line: 2170/782 → crumbling 2430/712 → one-way 2700/712 → 2970/712 → wall 3150–3190/690 | deck → 2170/1782 → crumbling 2430/1660 → one-way 2650/1660 → deck |
| Hazard | Winch lane in the trench, 2450 (the cellar's numbers) | Shell lane in the trench, 2850, with a mantlet (the artillery's numbers) | The scene's `VentRegolith` (2400) and a new vent, 2720; the line's tops stand above both strips |
| Route, reward | Gallery Rail, the secret moved to (2140, 505) | Battery Line, `level_11.extractor_ridge_battery` moved to (3035, 238) | Survey Line, the secret moved to (2140, 1285) |
| Burst shortcut | gap 450, rise 100, flown west | gap 505, rise 100 | gap 450, rise 100, flown west |
| Rise shortcut | 402 | 402 | 402 |
| Swarm | `level_10.swarm1`, 8 on the Galleries' floor (Easy 5) | `level_11.swarm1`, 10 in the Wheatfield (Easy 7); `level_11.swarm2`, 8 in the Angle (Easy 5) | none |
| Warm `standard_enemy` | 12 → 18 | 14 → 20 | 16, unchanged |
| Par | 360 → 390 | 360 → 390 | 540 → 570 |

Moved by the carves:
- **Level 10:** the yard's last holo_page 2280 → 1880; the stage's first slasher 2830 → 3530; the yard platform 1980 → 1860; the one-way 2280/740 and its secret perch went; the first stage balcony 2900 → 3160; the cellar's west wall now stands under the stage's entry barrier (2880), from the boards' underside to the cellar floor, and the cellar floor starts there. The traps, the rigging, the stair, both later anchors, the font, the arena and its recess walls did not move.
- **Level 11:** the slasher 2150 → 1760 and the last rifleman 2900 → 1975; the 2600 sandbags → 3470, inside the same 1900/3650 gap, so every gap still holds cover; the ridge battery's one-way and platform went. The ten artillery columns did not move.
- **Level 12:** two diggers 1900 → 1480 and 2600 → 1900; the lander 2050 → 1700. The lifts, the gravity tiles, the curtain, both later anchors and the font did not move. The spire lift is still held by its lock.

Level 10's swarm stands on the Galleries' floor, far from the Yard, and the audience's stillness clock pauses through that lock as through the others. The stage front has no lock, so the audience watches a hero who stands still on a step.

### The Siege Cannon's railcut (ruling 14)

`Level11Controller.Railcut.cs`, `RailcutFence.cs` and the seam `BossController.AfterVolley.cs`:
- Two `RailcutFence`s (the split-rail profile, 200 × 90, on Environment) stand inside `BossArenaBounds` at 10200 and 11200, either side of the spawn. Through Phase 1 they are cover: a shell stops on one and nothing breaks it.
- `PhaseEntered` (deferred off the flush) runs `RaiseRailcutSecondAct()`, public and idempotent: the fences become breakable (3 strikes) and `BossController.AbilityAfterVolley` is set to the kit's own `rail_warp`.
- In `ProcessAttacking`, when an attack ends, `TryBeginAbilityAfterVolley()` raises `VolleyEnded` after any projectile ability and starts the warp at once, ahead of the rest window. The warp arms its own cooldown, does not chain to itself, and never answers a target outside the arena. A boss with no rule set behaves as before.
- Each volley spends a strike of the first standing fence between the Cannon and the hero. A warp stops short of a standing fence (the relocation rule), so the railcar reaches the hero's side only once that fence falls.
- Everything stops on `BossDefeated`. Fences are not puzzle objects, so no bot goes to break one. `rail_warp.tres` is unchanged; it simply has a second way in.

### The Gravity Overseer's pad (rulings 14, 14+)

`Level12Controller.Overseer.cs`:
- **Phase 2:** two marked zones, 10000–10400 and 10650–11050, cycle between light and heavy on a 1 s telegraph, one starting heavy and one light. No inversion. The pad's dampers tile the arena and `EnvironmentPlayerModifiers` multiplies every live source, so each zone cycles at the ruled scale ÷ 0.48: a hero reads exactly 0.15 or 2.5, with no seam to straddle. The tint is red while heavy and blue while light.
- **Phase 3:** the zones stand down and four gust vents open (inner edges 10350, 10050, 10850, 11150). Each vent's strip is shifted outward of its node, so `StoryCyclicHazard`'s push-away-from-node sends everything in it toward the arena edge on its side. No new modifier.
- `RaiseOverseerFields()` and `OpenOverseerGusts()` are public and idempotent. All of it is built off, inside the arena, and is `story_hazard`, so a hold freezes it. Vents opened inside a hold do not start until the thaw. All of it stops on `BossDefeated`.
- `inverted_gravity_field` left `gravity_overseer.tres` (the kit is now four abilities, `AbilityMinPhase [0, 0, 0, 2]`). Its `.tres` stays on disk, because its display-name key and its VFX row still name it.

`BossPhaseRuleMatrixTests.ArenaRules` gains `siege_cannon` phase 2 and `gravity_overseer` phases 2 and 3.

### API

- `Level10Controller`: `StageFrontStretchID`, `GalleryRailRouteID`, `StageFrontMinX`, `StageFrontMaxX`, `StageFrontWestDeckEndX`, `StageFrontEastDeckStartX`, `GalleryRailEndX`, `CellarWestWallX`, `SwarmRowID`, `SwarmSpawnXs`; `YardLockMaxX` 2000, `StageLockMinX` 2900.
- `Level11Controller`: `SunkenRoadStretchID`, `BatteryLineRouteID`, `RidgeBatteryExtractorID`, `SunkenRoadMinX`, `SunkenRoadMaxX`, `SunkenRoadWestDeckEndX`, `SunkenRoadEastDeckStartX`, `BatteryLineEndX`, `WheatfieldSwarmRowID`, `AngleSwarmRowID`, `WheatfieldSwarmSpawnXs`, `AngleSwarmSpawnXs`, `RailcutFenceXs`, `RailWarpAbilityID`, `RailcutFenceHitsToBreak`, `CannonEncounter`, `RailcutFences`, `RailcutSecondActRaised`, `RaiseRailcutSecondAct()`, `FenceBetween(fromX, toX)`; `SeminaryRidgeLockMaxX` 2000.
- `RailcutFence : DestructibleBlock`: `Breakable`, `Create(objectID, hitsToBreak, position, size, fill)`.
- `BossController`: `AbilityAfterVolley`, `event Action<BossController> VolleyEnded`.
- `Level12Controller`: `MareCrossingStretchID`, `SurveyLineRouteID`, `MareCrossingMinX`, `MareCrossingMaxX`, `MareCrossingWestDeckEndX`, `MareCrossingEastDeckStartX`, `SurveyLineEndX`, `OverseerLightGravityScale`, `OverseerHeavyGravityScale`, `OverseerFieldTelegraphSeconds`, `OverseerFieldCycleSeconds`, `OverseerFieldSpans`, `OverseerGustVents`, `OverseerGust*` timings, damage and `OverseerGustKnockback`, `OverseerEncounter`, `OverseerFields`, `OverseerGusts`, `OverseerFieldsCycling`, `OverseerGustsOpen`, `RaiseOverseerFields()`, `OpenOverseerGusts()`; `TranquilityLockMaxX` 2000; `BuildGravityField` gained an optional first-cycle length.
- Test kit: `tests/ContentValidation/GrLevelFixture.cs` (not a suite): `GrLevelFixture<T>`, `GrLevelSteps.Settle`, `EnterTheArena`, `Frames`, `DoubleJump`.

## Measured: bot probes

`play`, one run each, under the lock, tags `lv4a` (Easy) and `lv4n` (Normal). The controls are the last batches on these levels with the same heroes, `gra11d` (Easy) and `gra11lg` (Normal), which predate this work. "Crossing" is the first contiguous stay in the stretch.

| Run | End | Crossing | Control end |
|---|---|---|---|
| L10 Joan Easy | Complete, 154 s, 0 deaths | 2.3 s, through the trench | Complete, 148 s, 1 death |
| L10 Lincoln Easy | Collapse (Death), 1190 s, 6 deaths | 3.8 s, the line | Complete, 196 s, 0 deaths |
| L11 Joan Easy | Complete, 199 s, 0 deaths | 3.1 s | Complete, 170 s |
| L11 Lincoln Easy | Complete, 214 s, 0 deaths | 4.0 s, the line | Complete, 170 s |
| L12 Joan Easy | Complete, 284 s, 4 deaths | 6.0 s, the floor | Collapse (Death), 400 s, 6 deaths |
| L12 Lincoln Easy | Complete, 292 s, 3 deaths | 9.1 s, the floor | Complete, 293 s, 1 death |
| L10 Joan Normal | Complete, 190 s, 3 deaths | 2.1 s | Complete, 174 s, 3 deaths |
| L10 Lincoln Normal | Collapse (Death), 241 s, 4 deaths | 3.8 s | Collapse (Death), 201 s, 4 deaths |
| L11 Joan Normal | Complete, 345 s, 4 deaths | 3.6 s | Complete, 263 s, 1 death |
| L11 Lincoln Normal | Complete, 353 s, 3 deaths | 18.4 s | Complete, 272 s, 1 death |
| L12 Joan Normal | Collapse (Death), 154 s, 4 deaths | 3.2 s | Collapse (Death), 197 s, 4 deaths |
| L12 Lincoln Normal | Collapse (Death), 245 s, 4 deaths | 6.8 s | Collapse (Death), 238 s, 4 deaths |

**Every run crossed its stretch**, Lincoln included. No bot took a route or struck a moved reward (bots do not use shortcuts). On Level 12 both heroes walked the floor under the vents rather than the line.

Readings, reported and not tuned (Q2):
- **Level 10 Lincoln Easy's collapse is the known recess gap, not the stretch.** The boss fell at frame 9704; he then stood in the arena trapdoor recess (x 9550–9650, y 1260) for about 18 minutes, and the audience killed him six times while he stood still. The control batch shows the same stall for Mozart and Shakespeare. The recess walls and the relocation rule are untouched. The rope notes at the last gate did not stop either hero here; Joan completes on Normal and Lincoln on Normal dies where the control does, in the arena.
- **Level 11 is slower and deadlier with two swarm rows.** Both heroes complete, but runs took 30–45 s longer on Easy and 80 s longer on Normal, and Normal deaths rose from 1 to 3–4. On Normal the Wheatfield row took 41–50 s to clear and the Angle row 27–37 s. On Normal the Cannon fight took 61 s (Joan) and 89 s (Lincoln), with 2 and 3 deaths in the arena; on Easy 37 s and 45 s with none.
- **Level 12:** the mid-level deaths (the mast head vent, the cache vents, the extractors) are in the control too. In the arena Joan died twice and Lincoln once on Easy, one of Lincoln's to an inner gust vent; both completed. Normal collapses before or in the arena exactly as the control does. Nothing suggests the zones made the bot hopeless, but **the bots' flight model reads only the dampers' static 0.48** (`PlaytestNav.GravityScaleAt` takes the first zone listed and caches it), so a bot plans every arena jump at 0.48 and re-plans when a zone has shifted. I did not trace whether its jumps failed in the zones.
- The Level 10 swarm cleared in 4–5 s. Ranks read D throughout (the rank block is live on these slotless runs).

**Measured on the Moon** (`Level12TraversalStretchTests`, `StoryAirPhysics` at 0.36): double rises run from Lincoln's 724 px to Cleopatra's 1172 px, and reach 100 px up runs from 942 px to 2134 px. So **on Level 12 every hero clears both shortcuts on the base kit**: the shortcuts only save a climb, and gate nobody. Lincoln's real-frames double jump on the Moon clears 410 px. The shared sweeps measure at normal gravity, where the route obeys the same numbers as Levels 2, 5 and 10.

## Tests

**Declared +17, actual +17.**

| Suite | Cases |
|---|---|
| `Level10TraversalStretchTests` | 3 |
| `Level11TraversalStretchTests` | 3 |
| `Level12TraversalStretchTests` | 3 |
| `Level11CannonArenaTests` | 4 |
| `Level12OverseerArenaTests` | 4 |

Changed in place, no count change:
- `Level10ContentTests`: five rooms (renamed `TheLevelIsFiveConfinedRooms…`), the arena and galleries rooms' indices, the carved ranges, the galleries' swarm wave.
- `Level11ContentTests`: five rooms (renamed `TheLinearAssaultIsFiveCameraConfinedRooms…`), the shell lane skipped in the artillery table, the carved range, both swarm waves in the wave pins, and the Angle's kill loop 12 → 30 rounds.
- `Level12ContentTests`: Tranquility's carved range.
- `BossPhaseRuleMatrixTests`: three `ArenaRules` rows.
- `TraversalContentKit.LevelsWithTraversal`: adds 10, 11, 12.

Last filtered run, 30 suite filters including every shared sweep, with the keys compiled locally: `Passed! - Failed: 0, Passed: 295, Skipped: 0, Total: 295`. No full run: the orchestrator owns those.

**Seen failing,** by mutating the build and reverting, two runs:
- Run 1, 16 of 17 failed: par unchanged on each level, a wall in Level 10's trench, a Level 11 wall too high to clear, both swarm rows removed, the Moon at normal gravity, fences breakable from the build, the Cannon's raise inside the flush, no warp after a volley, the defeat unwired on both levels, the zones built on, the Overseer's raise inside the flush, the gust strips unshifted, vents started inside a hold.
- The one pass, `TheLineClearsTheVentsAndTheFloorDoesNot`, was vacuous: its hero had not landed on the lowered step when the vent fired. It now waits to rest on the step (`1fe4016d`).
- Run 2, 6 of 17 failed: the audience clock not paused in a lock, no fence erosion, mobs never hazard-vulnerable, the zone scales not divided by the dampers', the zones out of `story_hazard` (no freeze), and the lowered step (the fixed case).

## Effects

No save field, schema, protocol, component or Fighter hash change. Nothing under `scripts/FighterSim/` changed. `StoryLevelControllerBase.cs` is untouched. F05 is exact: fodder has weight 0, no pool moved, and the sweep sums each ledger on three difficulties. `BossController.cs` gains one call line; the rule lives in a new partial.

## Provisional numbers (`PROVISIONAL-TRAVERSAL-RULES`, `PROVISIONAL-BOSS-ARENA-RULES`)

The user ruled only the 280 / 240 limits, the swarm trigger and sizes, the +30 minimum, and the Overseer's 0.15 / 2.5 scales and 1 s telegraph.
- Every carve point, platform, trench, ramp and route position above, the spawn moves, the sandbags' new x, the new Level 12 vent (1.1 / 0.9 / 2.6 s, 15 damage, the mare vents' knockback) and the trench lanes' copied numbers.
- Swarm spawn positions and the choice of lock for each row; Level 11's second row.
- Railcut: the fence positions and size, 3 strikes, one strike per volley across a fence, the warp ahead of the rest window.
- Overseer: the zone spans, the 4 s cycle step, light-or-heavy first, the tints; the vents' positions, 1.0 / 0.5 / 3.0 s, 8 damage, knockback (1500, −260) (at 620 a vent moved a still hero only 45 px), the 1.8 s stagger of the outer pair, and the zones standing down at Phase 3.
- `globe_room_stage_front` "The Stage Front", `gettysburg_room_sunken_road` "The Sunken Road", `lunar_room_mare_crossing` "The Mare Crossing".

## Deviations from the plan and dossier

1. **Level 10 carves two locks.** The Stage is the widest, but its three traps fix its width and its east end holds the cellar's stair, so the carve takes the Yard's east end (2540 → 2000) and the Stage's first 340 px of boards (2560 → 2900), leaving 150 px of solid boards before the first trap. The Stage stays 2840 px wide, over the carve's usual 1,700–2,000. The stretch is 860 px, narrower than the others.
2. **Level 12's stretch has no trench.** The floor is 100 px above the level's bottom edge, and the Moon has no pits; the harder way is the floor under two vents.
3. **On the Moon the shortcuts gate nobody** (measured above). The shared sweeps read normal gravity, so the route uses the same proven numbers as the other levels. A question for the session.
4. **The Overseer's zones divide by the dampers' scale.** The plan read "cycling `GravityFieldZone` at 0.15 and 2.5". Literal values would multiply with the pad's 0.48 and read 0.072 and 1.2; splitting the pad's tile instead would leave seams where a straddling hero reads a product. The zones author 0.3125 and 5.21 so the hero reads exactly 0.15 and 2.5. The zone's `0–4` export hint is an editor hint only, and nothing clamps above it.
5. **The zones stand down at Phase 3.** The design names a rule per phase; the pad's comment says the dampers are what keep the Phase 3 pull recoverable, so Phase 3 returns the pad to 0.48 while the vents gust. A question for the session.
6. **`rail_warp` follows a volley through a new `BossController` seam** (`AbilityAfterVolley`, `VolleyEnded`, one call line). The dossier says the relocation is boss-side; no ability number changed. `inverted_gravity_field` left the Overseer's kit (the task's "becomes the cycling fields"); its file stays.
7. **Level 11 takes two swarm rows,** on the Wheatfield and on the Angle (the "two-row candidate"). The Angle's row comes at two left of the ridge line, before the stone-wall pair and the commander.
8. **Lincoln has no ability shortcut on Levels 10 and 11** (the pilot's deviation 6). On Level 12 he needs none.

## Did not ship

- No bot took a route or struck a moved reward; on Level 12 no bot took the line.
- No trace of the bots' jumps inside the Overseer's zones.
- No real Post-Landing Hold on the railcut fences (they have nothing to freeze; the boss freezes as before).
- No art: platforms are each level's palette, fences are flat rectangles, zones a tinted rect.
- The Level 10 recess stall (a known bot gap) is unchanged and uninvestigated.
- Par is the authored +30, not measured.

## What the docs owe (GR-DOCS)

- Ledger: `PROVISIONAL-TRAVERSAL-RULES` and `PROVISIONAL-BOSS-ARENA-RULES` (the lists above); `VERIFY-BOSS-PHASE-ARENAS` narrowed by the Siege Cannon and the Overseer; `VERIFY-PAR-SECONDS` (Levels 10 390, 11 390, 12 570); question rows for deviations 3, 4 and 5 and Lincoln's missing shortcut.
- AGENTS.md: the three stretches and routes; the carved lock ranges (Level 10 Yard 300–2000 and Stage 2900–5740, Level 11 Seminary Ridge 300–2000, Level 12 Tranquility 320–2000) in the encounter-lock text; Level 10's cellar wall at 2880; the Siege Cannon's and the Overseer's arena rules; `BossController.AbilityAfterVolley`; the Overseer's four-ability kit; the new suites.
- CLAUDE.md: the bots read the Overseer's zones at the dampers' static scale.
- Design master and mirror: the Siege Cannon's and the Overseer's arena rows, built, with deviations 4 and 5.

## For the orchestrator

- **`en.csv` keys to compile:** `globe_room_stage_front`, `gettysburg_room_sunken_road`, `lunar_room_mare_crossing`. Until you do, `Level10ContentTests`, `Level11ContentTests` and `Level12ContentTests`' localization cases fail on this branch.
- Expect conflicts in `TraversalContentKit.LevelsWithTraversal` (this branch: `{ 2, 10, 11, 12 }`), `BossPhaseRuleMatrixTests.ArenaRules` and `en.csv`'s tail.
- `GrLevelFixture.cs` is a shared, non-suite helper; nothing else uses it yet.
- A Godot launch churns `.import` files under `addons/` and `audio/` as well as `assets/`.
