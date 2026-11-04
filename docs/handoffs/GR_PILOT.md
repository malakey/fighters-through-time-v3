# GR-PILOT: traversal stretches, optional routes, Level 2, the Duke's steam, the rank block

**Nobody has played any of this.** Every claim below is a headless suite, a real-frames script or two bot runs. Nobody has looked at the Rampart run, the Parapet line or the steam.

This file is also the authoring guide for GR-LV1 to GR-LV5. Part 1 is what you follow. Part 2 is what shipped and what is owed.

---

# Part 1: the authoring guide

## The rules (plan §1.11, §1.14, §1.15)

- Every lock stays, with the same ID and wave table apart from the swarm row.
- **Carve by default.** Shrink the widest lock to 1,700–2,000 px and use the freed span or an existing gap. Insert width only for a stretch over 1,300 px.
- Base-kit pieces only: no new verb and **no new ropes**.
- Gaps at most 280 px and rises at most 240 px per hop on the line. The numbers live in `FTT.Environment.TraversalRules`.
- Trenches are non-lethal with walkable exits. No `StoryKillBoundary` in a stretch (`VERIFY-STORY-PITS`).
- A rope-free path to every Middle and PreBoss anchor (`AnchorApproachContentTests`).
- The route's reward is an **existing** Extractor or the secret. No new dust; the manifest pools do not move.
- Par: `TraversalRules.ParWithStretches(seed, stretches)`. The orchestrator's measured pass corrects it upward later.
- No boss HP, Levels 1–4 numbers or wave retuning beyond the swarm row.

## The helpers, in call order

All are in `StoryLevelControllerBase.Stretches.cs`, and all are `protected`. Each throws at build when a call cannot obey the rules. So a bad layout fails on the first load of your scene.

| Call | What it does |
|---|---|
| `BeginStretch(stretchID, roomID, minX, maxX, deckY, westDeckEndX, eastDeckStartX, cameraWestX)` | Records the stretch and builds its room trigger at `minX + 40`. Camera rect `cameraWestX..maxX`. Call it between the rooms it joins, so rooms meet `FindRoomTriggerContaining` west to east |
| `BuildStretchTrench(stretch, depth = 120, rampRun = 160)` | Floor `depth` under the deck from `westDeckEndX` to `eastDeckStartX`, with a walkable ramp each end. Depth 100–150; ramp at most 40.6° |
| `BuildStretchStep(stretch, x, y, width, kind = Solid, id = "")` | The next surface of the line, recorded in call order. Kinds `Solid`, `OneWay`, `Crumbling`. Returns its `TraversalSurface` |
| `BuildStretchWallStep(stretch, westX, topY, bottomY, width = 40)` | A solid block whose top is the next surface of the line, with a `LedgeGrabPoint` on its west corner |
| `BuildCyclicHazardLane(hazardID, x, floorY, warning, active, cooldown, damage, startEnabled = true, withCover = false)` | The cyclic hazard template on a floor (a strip 120 wide and 220 tall). `withCover` adds a mantlet. `startEnabled: false` is the arena-change form: dormant, and a rewind leaves it alone |
| `SwarmWave(rowID, y, params float[] spawnXs)` | A swarm row's lock wave: all `chrono_swarmling`, `WhenAliveAtMost(2)`. Throws outside 6–10 mobs |
| `BeginOptionalRoute(routeID, rewardSourceID, rewardPosition, entry)` | Records the route. `entry` is the surface on the main path it leaves from |
| `BuildRouteStep(route, x, y, width)` | The next surface of the base-kit line, a one-way platform |
| `route.MarkBurstShortcut(from, to)`, `route.MarkRiseShortcut(from, to)` | Which hops are the two shortcuts |

The records are `TraversalStretch` and `OptionalRoute`: child nodes, in the groups `story_traversal_stretch` and `story_optional_route`. The sweeps find them by walking the scene.

**Level 1 does not extend the base.** It has its own private builders and cannot call these helpers. Call `TraversalStretch.Create(...)` and `OptionalRoute.Create(...)` directly, `AddChild` them, and feed them through `AddLineSurface`, `RecordTrench` and `AddBaseLineSurface` as you build with your own methods. Mark no shortcuts: it has no movement ability yet. Level 1 is in `TraversalContentKit.LevelsWithoutShortcuts` already.

## The pattern: Level 2's diff

**1. The carve.** In `Level02Controller`:
- `VanguardLockMaxX` 3200 → 2000. The lock's barriers follow it.
- Third archer 2600 → 1900. The spawn must stand inside the new area.
- `Room1CameraBounds` now ends at the deck's end, 2060.
- The floor stops at the deck's end: `BuildFloor(Room1StartX, GroundY, RampartWestDeckEndX - Room1StartX)`.
- The old platforms in the freed span (2400/660, 2850/520) went. 1900/540 stayed.

**2. The stretch.** `BuildRampartRun`, called between rooms 1 and 2 in `BuildLevel`:

```csharp
TraversalStretch rampart = BeginStretch(RampartStretchID, "orleans_room_rampart",
    RampartMinX, RampartMaxX, GroundY, RampartWestDeckEndX, RampartEastDeckStartX, Room1StartX);
BuildRoomDecoration(RampartMinX, "orleans_room_rampart", new Color(0.85f, 0.6f, 0.3f));
BuildStretchTrench(rampart);
// The deck resumes under the next lock's entry barrier: that barrier needs floor under it.
BuildFloor(RampartEastDeckStartX, GroundY, RampartMaxX - RampartEastDeckStartX);

BuildStretchStep(rampart, 2170, 790, 200);
BuildStretchStep(rampart, 2430, 720, 180, StretchStepKind.Crumbling, "level_02.crumbling_rampart");
TraversalSurface middleStep = BuildStretchStep(rampart, 2700, 722, 200, StretchStepKind.OneWay);
TraversalSurface eastStep = BuildStretchStep(rampart, 2970, 720, 200);
BuildStretchWallStep(rampart, 3150f, 690f, 940f);

// Off the line: a way back up out of the trench, and a lane to cross down there.
BuildOneWayPlatform(2640, 870, 200);
BuildCyclicHazardLane("level_02.mortar_rampart", 2850f, rampart.TrenchFloorY, 1f, 0.6f, 2.6f, 18, withCover: true);
```

Why `cameraWestX` is the west edge of the room behind: it keeps that room in view. A hero who turns back for a secret stays on screen, and entering the stretch moves only the east limit, so the camera does not pan.

**3. The route.** It leaves the line, climbs west and back east, and ends on a wide platform that holds the reward:

```csharp
OptionalRoute parapet = BeginOptionalRoute(ParapetRouteID, ExtractorPlacements[0].ID,
    ExtractorPlacements[0].Position, middleStep);
BuildRouteStep(parapet, 2560, 530, 120);
TraversalSurface burstFrom = BuildRouteStep(parapet, 2340, 420, 160);
BuildRouteStep(parapet, 2590, 320, 180);
TraversalSurface end = BuildRouteStep(parapet, ParapetEndX, 320, 220);
parapet.MarkBurstShortcut(burstFrom, end);
parapet.MarkRiseShortcut(eastStep, end);
```

The courtyard Extractor's placement moved to `(ParapetEndX, 238)`, 72 px over the platform's top, as Extractors stand. An Extractor's ID and manifest row do not change. To pay the secret instead, override `SecretCachePosition`.

**4. The swarm row**, on the lock that suits it. Level 2 uses the siege line, which is wider than 2,800 px, so ten mobs:

```csharp
Waves = new[] {
    EncounterLockWave.FromTable(Room2RowID, Room2Spawns, EncounterWaveTrigger.OnEngage),
    SwarmWave(SwarmRowID, EnemyGroundY, SwarmSpawnXs)
},
```

Then two data edits:
- `level_02_rewards.tres`: add `"level_02.swarm1:chrono_swarmling,…"` to `RequiredWaves`, the same count. `RequiredEncounterPool` does not change.
- `level_02_pool_config.tres`: `standard_enemy` `WarmUpCount` 10 → 16 (`SwarmWaveRules.WarmUpBonus`). Check `MaxWarmUpInstances` still holds, and that the warm combatants stay at or under 30.

No row on Levels 0, 12, 13 or 14. `SwarmWaveRules.RowMobsForLockWidth` gives the count.

**5. Par.** `public override float ParSeconds => TraversalRules.ParWithStretches(360f, 1);`

**6. A boss arena change, where you own one** (`Level02Controller`, "the Duke's boiler rupture"):
- Build the pieces up front with `startEnabled: false`, inside `BossArenaBounds`.
- Subscribe after `BuildBossEncounter`: `duke.PhaseEntered += OnDukePhaseEntered`.
- Hop off the flush: `if (PhysicsCallbackGuard.IsInPhysicsCallback) Callable.From(Raise).CallDeferred(); else Raise();`
- `Raise` is public and idempotent. It sets `Enabled`, and starts the telegraph only when `ChronalRewindManager.IsWorldHeld` is false.
- Stop it on `BossDefeated`: the hero still has to walk to the sealing anchor.
- Add your row to `BossPhaseRuleMatrixTests.ArenaRules`.

## Things that bit the pilot

- **Fonts.** A hazard counts dormant or not. Keep its shape at least channel range + 20 + 20 px (130 px on Level 2) from the font's x, or `SecretCachePlacementTests` fails. The Duke's west strip moved 10 px in for this.
- **Room rects.** `FindRoomTriggerContaining` returns the **first** trigger whose rect holds an x. Do not let a stretch's rect run past its own east end, or a landing in the next room confines to the stretch's camera.
- **Hazard strips rise 220 px.** Keep the line above them, or accept that a lane hits it.
- **One-way under a solid.** A hero rising from a one-way under a solid step bumps its underside. Level 2's middle step is one-way so the trench's way up works.
- **A wave added to a lock moves its tests.** `PlannedTotal` now counts the swarm, so `LivingMemberCount == PlannedTotal` on engage stops being true, and "kill every member" must loop. Level 2's suite has `KillEveryMember`.
- **Heredocs.** On this machine a Bash heredoc holding an apostrophe breaks the shell. Write your edit scripts with the Write tool.
- **Compiled translation.** `--import` does not re-run for a csv it has already compiled once. If you discard `en.en.translation` and need it back, change the csv's bytes.

## The checklist: what a level must pass

Add your level to `TraversalContentKit.LevelsWithTraversal` first (additive). The sweeps then fail if it authors no stretch or route.

| Suite | What it checks |
|---|---|
| `TraversalStretchContentTests` (5) | Stretch outside every lock, own room and camera; line obeys the hop rule and stands on real bodies; floor under every column, ramps walkable, nothing lethal; no rope, route surfaces real; reward is an existing source standing at the line's end, ledger sums to the pools, both shortcuts |
| `OptionalRouteReachTests` (15) | Each of nine heroes crosses each stretch and reaches each reward on its own jump numbers; every air burst and rise kit lands its shortcut in real frames; the shortcuts are longer than the slow base kits; the kit measurements |
| `SwarmWaveContentTests` | Row shape, trigger, warm count |
| `EncounterLockContentTests`, `EncounterLockObjectiveContentTests` | Spawns inside areas, barriers with floor, wave equals row, baselines |
| `AnchorApproachContentTests` | Every Middle and PreBoss anchor reached before the next lock, by ordinary means |
| `ArenaGeometryContentTests` | Crawlspaces, gates, floors |
| `SecretCachePlacementTests` | Secret, sealing anchor and font rules |
| `ScenePoolConfigTests`, `DustEconomyTests`, `RewardManifestTests` | Pools and F05 |
| `BossPhaseRuleMatrixTests` | Your arena row |
| `LevelNNContentTests` | Your own pins; these change in place |

Also add your own `LevelNNTraversalStretchTests` (Level 2's is the model, including `TraversalContentKit.Fly` for a real-frames hop), and run a bot probe: `--bot=play --trace-every=3 --nav-dump=1`, under the lock.

---

# Part 2: what shipped

## API

- `FTT.Environment.TraversalRules` (pure): `MaxHopGapPixels` 280, `MaxHopRisePixels` 240, `MinTrenchDepthPixels` 100, `MaxTrenchDepthPixels` 150, `MaxRampRisePerRun` 0.857, `MinCameraWidthPixels`, `ParSecondsPerStretch` 30, `ParGranularitySeconds` 30; `ParWithStretches`, `HopGap`, `HopRise`, `RampIsWalkable`, `TrenchIsAllowed`, `ValidateLine`. `TraversalSurface(Left, Right, Top)`.
- `TraversalStretch`, `OptionalRoute` (the records).
- `StoryLevelControllerBase.Stretches.cs` (the helpers above). The base file itself is untouched.
- `Level02Controller`: `RampartStretchID`, `ParapetRouteID`, `RampartMinX`, `RampartMaxX`, `RampartWestDeckEndX`, `RampartEastDeckStartX`, `SwarmRowID`, `SwarmSpawnXs`, `DukeSteamWestX`, `DukeSteamEastX`, `DukeSteamRaised`, `DukeSteamWest`, `DukeSteamEast`, `RaiseDukeSteam()`.
- Telemetry: `PlaytestTelemetry.RankReport()` and the static `RankBlock(rank, dustPaid)`. The report and `PLAYTEST_RESULT` gain `rank`: `letter`, `points`, `damage_score`, `damage_percent`, `speed_score`, `dust_paid`. `compare_runs.py` prints a `rank` table. `run_playtests.ps1`'s CSV and `trace.py` are unchanged.
- Test kit: `tests/ContentValidation/TraversalContentKit.cs` (not a suite): `LevelsWithTraversal`, `LevelsWithoutShortcuts`, `LoadHero`, `HeroJump.ClearsOnBaseKit`, `Levels()`, `Fly`.

## Level 2 as built

| Piece | Where |
|---|---|
| Vanguard lock | 300–2000, ID and table unchanged. Archer 2600 → 1900 |
| Rampart run | 2020–3200, room `orleans_room_rampart`, camera 0–3200 |
| Trench | Floor y 1020, ramps 2060→2220 and 3000→3160, 160 px run at 36.9° |
| Line | Deck 900 → 2170/top 782 → crumbling 2430/top 712 → one-way 2700/top 712 → 2970/top 712 → wall 3150–3190/top 690 → deck 900 |
| Off the line | One-way 2640/top 860, mortar lane at 2850 with a mantlet |
| Parapet line | One-way 2700/top 712 → 2560/top 520 → 2340/top 410 → 2590/top 310 → 3035/top 310, wide 220 |
| Reward | `level_02.extractor_courtyard` at (3035, 238) |
| Burst shortcut | 2340/top 410 → the end: gap 505, rise 100 |
| Rise shortcut | 2970/top 712 → the end: rise 402 |
| Swarm | `level_02.swarm1` on the siege line: 10 on Normal and Hard, 7 on Easy |
| Par | 360 → 390 |
| Duke's steam | Two `StoryCyclicHazard` strips, centres 9370 and 10480, off until Phase 2 |

Unmoved: Middle 6500, PreBoss 9060, font 9180, arena 9300, the secret at (900, 467), level width, the baselines, every manifest pool.

## Measured

**Bot probes** (`play`, Easy, tag `pilotA`, one run each, under the lock). **The bot crossed the stretch and completed the level with both heroes.**

| Hero | Stretch | Level | Hits on the line | Rank |
|---|---|---|---|---|
| Lincoln | 4.1 s, stepping every platform, the crumbling one included, then over the wall | Complete, 101.8 s, 0 deaths | None | C, 52.4 points, speed 96, 4 dust |
| Joan | 3.2 s, skipping the crumbling step (2270 → 2600) | Complete, 97.9 s, 0 deaths | None | C, 39 points, speed 98, 4 dust |

Both took the line and never entered the trench, so **the trench and the mortar lane have not been exercised by a bot**. Nor was the route: bots do not use shortcuts (risk 3), and no bot struck the moved Extractor. The swarm spawned and cleared in both runs, about 400 frames.

The log also shows 21 `Invalid polygon data` errors from `HudCapsule._Draw` (the compact HUD, `scripts/UI/HudCapsule.cs:92`). None comes from this work; I did not check them against a control.

**Kit facts, real frames:**

| Fact | Result |
|---|---|
| Einstein's warp at a wall | Stops short (cast motion against Environment) |
| Tesla's blink at a wall | Stops at it. It passes projectiles only |
| Joan's Ascendant Wings | Within the test's 200–320 px band; with a double jump the rise flight peaked at 667 px |
| Leonardo's boost | Within the test's 45–80 px band; with a double jump the rise flight peaked at 426 px, 24 px to spare on 402 |
| Mozart, Shakespeare | Rise flights peaked at 593 and 529 px |
| Lincoln's Rail Charge | **Does not clear the burst shortcut.** It fires in the air but stops him for its 12-frame startup and drops his X on recovery. One measured flight: 43 px farther than his base jump at the same height, missing a 505 px gap |

**Who needs the ability:** the shortcuts gate nobody out and are not exclusive.
- Burst (gap 505, rise 100): on the base kit alone, with the test's margins, Einstein, Joan, Leonardo, Cleopatra, Shakespeare, Mozart and Tubman can already take it. Only Tesla needs his ability, and Lincoln cannot.
- Rise (402): on the base kit only Cleopatra can.
- Since a hop is measured from any surface, a strong jumper can also leave the base line early. Lincoln alone must walk all of it.

## Provisional numbers (`PROVISIONAL-TRAVERSAL-RULES`, `PROVISIONAL-BOSS-ARENA-RULES`)

The user ruled only the 280 / 240 limits, the swarm row's trigger and sizes, and the +30 minimum.

- Every platform position, the trench's depth 120 and ramp run 160, the wall's top 690 and width 40, the route's shape.
- The Rampart lane's 1.0 / 0.6 / 2.6 s and 18 damage, copied from lane A.
- Steam: 1.0 s warning, 1.2 s active, 1.8 s cooldown, 12 damage per cycle, blockable like every cyclic hazard.
- Swarm spawn positions.
- Test margins: gap × 1.1, rise + 10 px.
- `orleans_room_rampart` reads "The Rampart Run".

## Tests

**Declared +27, actual +29.**

| Suite | Cases | Note |
|---|---|---|
| `TraversalStretchContentTests` | 5 | Declared 5 |
| `OptionalRouteReachTests` | 15 | Declared 15: heroes 9, shortcuts 2, measurements 4 |
| `Level02TraversalStretchTests` | 4 | Declared 3; the fourth is the swarm on the real scene (a fodder kill pays nothing and drops no pickup), which GR-CROWD left unverified |
| `Level02DukeArenaTests` | 4 | Declared 4 |
| `PlaytestCombatStatsTests` | +1 | Not declared: the rank block |

Changed in place, no count change:
- `Level02ContentTests`: five rooms, the carved lock, the swarm wave, the warm count, `KillEveryMember`, the new room key.
- `BossPhaseRuleMatrixTests`: one `ArenaRules` row.

Last filtered run, 28 suite filters including every shared sweep above, with the key compiled locally: `Passed! - Failed: 0, Passed: 218, Skipped: 0, Total: 218`. No full run: the orchestrator owns those.

**Seen failing.** The new suites cannot compile against the old code, so I checked them by mutating the build and reverting, in two runs:
- Run 1, 18 failures of 218: a missing trench, a rope, a route reward with no source, par 360, an unguarded raise, an unwired defeat, steam built on, a frozen hazard ignoring its freeze, a one-mob swarm, warm count 10, no wall in the two wall tests.
- Run 2, 7 failures of 218: a stretch overlapping a lock, a line rise over the limit, a route hop out of reach, a burst shortcut too short. These cover the remaining sweep cases, the nine-hero case and the burst case.
- **Not mutated:** the Joan and Leonardo measurement bands, which only fail if a kit changes. The freeze case drives `SetTimeFrozen` and checks `story_hazard` is in the hold's groups; it does not run a real Post-Landing Hold.

## Effects

No save field, schema, protocol, component or Fighter hash change. Nothing under `scripts/FighterSim/` changed. F05 is exact: fodder weight 0, pools unchanged, and a sweep sums the ledger on all three difficulties.

## Deviations from the plan and dossier

1. **Camera rect.** Dossier C's `Rect2(2000,0,1220,1080)` is narrower than the 1280 px view and than `Level02ContentTests`' 1920 floor. The rect is 0–3200 instead, for the reasons in Part 1.
2. **Arena.** The dossier's "arena 8960–10540" is the room. `BossArenaBounds` is 9300–10540, and the strips sit inside that. The west strip stands 10 px in for the font.
3. **Wall step.** The dossier's 220 px wall at 3150 with both ramps walkable cannot both hold: a wall on the east ramp stops the walk. Here a hero in the trench walks out by the west ramp, climbs the one-way, or double-jumps the wall (about 232 px from the ramp). The wall is 210 px over the deck.
4. **Platforms.** Line and route heights differ from the worked example, because its y 700 line sat 40 px under the 2400/660 platform. That platform and 2850/520 went.
5. **Route end** is 220 wide, not the example's 220 at another x, so Shakespeare's forward flight lands on it. The longest hop is 245, not 260.
6. **Lincoln has no shortcut on Level 2.** 7c+ and §1.15 require a shortcut of each kind per level, and every hero a base-kit line, which he has. The dossier's aim "so every hero has one" is not met for him, because Rail Charge does not extend a jump (measured above). I found no way to give him one that does not hand it to every hero. A question for the session.
7. **Added:** the steam stops when the Duke falls. The plan does not say so, but the sealing anchor is in that arena.
8. **`mortar_barrage` stays,** with an arena row added, because the design says "mortars" and ruling 4a keeps boss kits. "Retune or retire the stand-in" came down to neither.
9. **The rank block** carries no tally: `RankBaseDamage` reads 0 by the time the report is written, because completion resets the attempt.

## Did not ship

- No bot exercised the trench, the lane, the ledge or the route.
- No real Post-Landing Hold test on the steam.
- No art: platforms are the level's palette, and the crumbling step's tint is a lerp toward the hazard colour.
- No `run_playtests.ps1` CSV or `trace.py` rank columns.
- Par is the authored +30, not measured. Both probes spent 3–4 s in the stretch, so the measured pass should not raise it.

## What the docs owe (GR-DOCS)

- Ledger: `USER-LEVEL-TRAVERSAL`, `USER-OPTIONAL-ROUTES-SHORTCUT-ONLY`, `PROVISIONAL-TRAVERSAL-RULES` (the list above), `PROVISIONAL-BOSS-ARENA-RULES`; `VERIFY-BOSS-PHASE-ARENAS` narrowed by the Duke; `VERIFY-PAR-SECONDS` (Level 2 390); a question row for Lincoln's missing shortcut.
- AGENTS.md: the stretch and route system, the Level 2 lock's new range (300–2000) and its 3200 → 2000 in the encounter-lock text, `TraversalRules`, the new suites, the `rank` report block.
- CLAUDE.md: the `rank` block and `compare_runs.py`'s `rank` table.
- Design master and mirror: the Duke's arena row, built.

## For the orchestrator

- **`en.csv` key to compile: `orleans_room_rampart`.** Until you do, `Level02ContentTests.TheDialogueSetResolvesAndEveryAuthoredKeyIsLocalized` fails on this branch.
- A Godot launch churns `.import` files under `addons/` as well as `assets/`. `git checkout -- assets` does not catch those.
- Expect no merge conflict beyond `en.csv`'s tail and `BossPhaseRuleMatrixTests`' row list.
- Bot gates on Level 2 from here read the carve. The `rank` block is live on Level 2 slotless runs.
