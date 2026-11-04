# GR-LV1: Levels 1 and 3, traversal stretches, routes and swarm rows

**Nobody has played any of this.** Every claim below is a headless suite, a real-frames script or four bot runs. Nobody has looked at the Rooftop Run, the Skyway, the swarms or the tooltip.

Branch `gr/lv1`. Workstream of the gameplay review package (plan Phase B2, §1.11, §1.14, §1.15; rulings 7a, 7b, 7c, 7c+, 7d, 1b, 1b+, 1b++, 4a, Q3).

## What shipped

### Level 1, Florence (standalone)

| Piece | Where |
|---|---|
| Stretch `level_01.rooftop_run` | 4450–5780, in the unlocked gap between the piazza and the print shop. **No lock moved** (see deviation 1) |
| Room key | `florence_room_rooftops` ("The Rooftop Run"). Its camera rect is the whole level, as Florence's camera always was |
| Trench | Floor y 1020, ramps 4610→4770 and 5430→5590, 160 px run. A spike strip at 4950–5050 on its floor |
| Line | Deck 900 → 4650–4850/top 782 → crumbling 4900–5120/top 708 → one-way 5190–5390/top 712 → 5410–5590/top 712 → wall 5670–5710/top 690 with a ledge grab → deck 900 |
| Off the line | One-way 5130–5330/top 880, the way up out of the trench |
| Route `level_01.chimney_line` | One-way 5190–5390 → 5090–5210/top 520 → 4850–5010/top 410 → crumbling 5070–5290/top 310 → 5515–5735/top 310. Skill route, no shortcuts |
| Reward | `florence_extractor_0`, moved from (4700, 860) to (5625, 238) |
| Swarm | `level_01.swarm1@8` on the workshop lock: 8 on Normal and Hard, 5 on Easy |
| Warm pool | `florence_pool_config` `standard_enemy` 12 → 18, `MaxWarmUpInstances` 210 → 216 (the 206 warmed would otherwise pass 210). Warm combatants 22, under 30 |
| Par | None: Level 1 has no clock |

Removed: the platform at 5000/650, and the floor under the trench span.

### Level 3, Chicago

| Piece | Where |
|---|---|
| Carve | `MidwayLockMaxX` 3820 → 2500, so the Midway lock is 1800 wide. ID and table unchanged |
| Moved | Third Midway drone 2900 → 2150, inside the new area. Platforms 2650/1220 and 3200/1060 went |
| Stretch `level_03.skyway` | 2520–3820, room key `chicago_room_skyway` ("The Skyway"), camera 0–3820 |
| Trench | Floor y 1520, ramps 2680→2840 and 3500→3660 |
| Line | Deck 1400 → 2700–2900/top 1282 → crumbling 2950–3130/top 1212 → one-way 3200–3400/top 1212 → 3420–3620/top 1212 → wall 3700–3740/top 1190 → deck 1400 |
| Off the line | One-way 3160–3360/top 1380; arc lamp lane `level_03.arc_skyway` at 3430 on the trench floor, with a mantlet (0.9 / 0.8 / 2.4 s, 14 damage, copied from `MidwayArcA`) |
| Route `level_03.ferris_line` | One-way 3200–3400 → 3050–3170/top 1020 → 2810–2970/top 910 → 3050–3230/top 810 → 3475–3695/top 810 |
| Burst shortcut | 2810–2970/top 910 → the end: gap 505, rise 100 |
| Rise shortcut | 3420–3620/top 1212 → the end: rise 402 |
| Reward | `level_03_extractor_0`, moved from (2100, 998) to (3585, 738) |
| Swarm | `level_03.swarm1` on the Electricity lock, along its floor: 8 on Normal and Hard, 5 on Easy |
| Warm pool | `level_03_pool_config` `standard_enemy` 10 → 16 (cap 220 still holds) |
| Par | 540 → 570 (`TraversalRules.ParWithStretches(540f, 1)`) |

Unmoved: Middle 6000, PreBoss 8500, font 8760, arena 9000, the secret at (1000, 997), level width, baselines, every manifest pool.

### Florence's steam geyser, and the 7d showcase

**GR-HAZ's open question is answered:** `SteamGeyser` is a scene node, an instance of `CyclicHazardTemplate` in `scenes/campaign/Level_01_Florence.tscn` at (9920, 890), `HazardID` `level_01.steam_geyser`. It is a `StoryCyclicHazard`, so GR-HAZ's hunk reaches it. It has its default timing, 2 s cooldown, 1 s warning, 1 s active.

**Spike fix.** `Level01Controller.HitLaunchedMobsStandingInSpikes` asks every Florence spike strip each tick, in `_PhysicsProcess`, and calls `ApplyHazardImpact` on what overlaps. So a mob launched while already standing in a strip is hit. A walker is refused without spending the strip, and one launch pays once whichever path sees it first. It costs nothing when a strip is empty (`HasOverlappingBodies`). This covers Florence's strips only: the base's `BuildHazardSpikes` is frozen and keeps the limit GR-HAZ recorded.

**The row's spawns** (`SwarmSpawnXs`, 9130–10300) stand either side of the strips at 8990–9090 and 9500–9580 and the geyser, none inside a hazard.

The real-scene case shows a swarmling walking safely in a strip and in the live geyser, and dying when launched from inside a strip, when flown into a strip, and when flown into the live geyser. Its kills pay no dust.

### The `special_momentum` tooltip (orchestrator's decision)

- `FirstUseTooltips.ShowsOnEverySlot(id)` is true for `SpecialMomentum` alone. `ShouldShow` lets it through on a full-calibration slot.
- `FirstUseTooltipPresenter._PhysicsProcess` offers it before its skipped-calibration return. Every other polled trigger stays behind that return.
- Once per slot as before, through `ShownFirstUseTooltips`. A slotless run shows nothing.

## API

`Level01Controller`: `RooftopStretchID`, `RooftopRouteID`, `RooftopRoomKey`, `RooftopRouteRewardID`, `RooftopMinX`, `RooftopMaxX`, `RooftopWestDeckEndX`, `RooftopEastDeckStartX`, `RooftopTrenchDepth`, `RooftopRampRun`, `RooftopRouteEndX`, `RooftopRouteRewardPosition`, `RooftopRun`, `ChimneyLine`, `SwarmRowID`, `SwarmSpawnXs`.

`Level03Controller`: `SkywayStretchID`, `FerrisRouteID`, `SkywayRoomKey`, `SkywayMinX`, `SkywayMaxX`, `SkywayWestDeckEndX`, `SkywayEastDeckStartX`, `FerrisLineEndX`, `SwarmRowID`, `SwarmSpawnXs`.

`FirstUseTooltips.ShowsOnEverySlot`. `TraversalContentKit.LevelsWithTraversal` gains 1 and 3.

## A bug the tests found

My first line ended its last step flush with the hero's body in the deck pocket, on both levels. A hero double-jumping from that pocket bonked the step's underside at 107 px and could not clear the wall. The math sweeps all passed, and only the real-frames wall jump failed.

Each last step now ends 80 px short of the wall, and both levels' suites pin the jump. **Worth knowing for GR-LV2 to LV5:** a wall step on the deck needs clear air over the pocket beside it.

## Measured: bot probes

`play`, Easy, tag `lv1A`, one run each, under the lock, `--trace-every=3 --nav-dump=1`. **The bot crossed the stretch and completed the level in all four runs, with 0 deaths.**

| Run | Stretch | Level | Hits on the line | Trench | Swarm |
|---|---|---|---|---|---|
| L1 Lincoln | 7.8 s | Complete, 125.8 s | None | Not entered | 5 mobs, cleared in 245 frames, 10 damage |
| L1 Joan | 3.5 s | Complete, 90.7 s | None | Not entered | 5 mobs, 391 frames, 10 damage |
| L3 Lincoln | 6.2 s | Complete, 155.5 s | None | Not entered | 5 mobs, 182 frames, 0 damage |
| L3 Joan | 3.5 s | Complete, 120.8 s | None | Not entered | 5 mobs, 646 frames, 15 damage |

What the bots did **not** exercise:
- The trenches, the trench spike strip, the arc lamp lane, the ledges and the routes.
- The showcase: `hazard_kills` read 0 in all four runs, because bots do not aim launches. The only evidence that a launched mob dies on a Florence hazard is the real-scene test, which launches through `LaunchThrown`. A real finisher into a hazard was not run on this scene; `HazardMobVulnerabilityTests` holds that flag's half.

Stretch seconds count from the first to the last trace sample inside the span, and they include any fighting at its ends.

**For the orchestrator, not changed here (Q3, flags only):** both Level 1 runs ranked D, with `damage_percent` 320 (Lincoln, lowest HP 4) and 178 (Joan). Level 1 ranks on damage alone. Lincoln took 60 from spikes and 30 from the geyser outside the stretch. Level 3 ranked C both times, 40 and 44.7 points.

## Provisional numbers (`PROVISIONAL-TRAVERSAL-RULES`)

The user ruled only the 280 / 240 limits, the swarm row's trigger and sizes, and the +30.

- Every platform position, both trenches' depth 120 and ramp run 160, each wall's top and width, the route shapes.
- The Skyway lane's timing and damage. The Level 1 trench strip's position (its 15 damage is Florence's own).
- Both swarms' spawn positions and both lock choices.
- `florence_pool_config`'s `MaxWarmUpInstances` 216.
- Display names "The Rooftop Run" and "The Skyway".
- Route and stretch IDs.

## Tests

**Declared +7 (2 × 3 plus the tooltip case), actual +7.**

| Suite | Cases | Note |
|---|---|---|
| `Level01TraversalStretchTests` | 3 | New. The stretch and what moved; Lincoln out of the trench, over the wall and across the route's longest hop; the swarm and the showcase |
| `Level03TraversalStretchTests` | 3 | New. The carve and par; Lincoln through the trench, over the wall and across the longest hop; the swarm paying nothing |
| `FirstUseTooltipEverySlotTests` | 1 | New. A full-calibration slot on Level 1 sees the tooltip once across two presenters, and nothing else. Uses a scratch slot, restored in `finally` |

Changed in place, no count change:
- `FirstUseTooltipsTests`: `AFullCalibrationSlotNeverShowsATooltip` became `AFullCalibrationSlotShowsNoneOfTheSkippedLessons`; the special tooltip's case now pins once on a full slot.
- `Level01ContentTests`: the workshop's second wave.
- `Level03ContentTests`: the carved lock, the Electricity lock's second wave, and `PlannedTotal` counting the swarm.
- `TraversalContentKit`: the level list.

**Last filtered run**, 26 filters including every shared sweep in the guide's checklist and the Level 2, `HazardMob`, `LegacyUnlockSchedule` and `EncounterLockTests` suites, with the keys compiled locally: `Passed! - Failed: 0, Passed: 234, Skipped: 0, Total: 234`. No full run: the orchestrator owns those.

**Seen failing.** Two mutation runs, reverted:
- Run 1, 4 failures of 25: par 540; the Level 3 wave removed (`'10' but is '2'`); `florence_pool_config` warm 12 (`EverySwarmLevelWarms…`); the presenter's gate restored (`"" but is "special_momentum"`). The same run also had the spike poll removed and did **not** catch it, because that case re-teleported the mob into the strip. I rewrote it.
- Run 2, the poll removed, against the rewritten case: `A swarmling launched from inside the spike strip did not die to the hazard: at (9570, 899.9977), state Stunned, HP 10.`
- The wall jump failed on real geometry, before the pocket fix. Both pure tooltip pins were red before their in-place edits, because the rule had changed under them.

## Effects

No save field, schema, protocol, component or Fighter hash change. Nothing under `scripts/FighterSim/` changed. F05 is exact: fodder weight 0 and the pools unchanged, and `TraversalStretchContentTests` sums the ledger on all three difficulties. No boss, wave or kit number changed beyond the swarm rows. `StoryLevelControllerBase.cs` is untouched.

## Deviations from the plan and brief

1. **Level 1's stretch uses the existing gap, not a carve.** The brief says "carved out of a lock"; plan §1.14 allows "the freed span or an existing gap", and the dossier names this gap. It is 1330 wide with no lock change, so I kept the locks. Level 3 is a true carve.
2. **Level 1's stretch camera rect is the whole level,** and its trigger sets no `CameraPath`. Florence has never narrowed its camera, so the sweep's rule (holds the stretch, at least one view wide) holds by the level's own bounds.
3. **The spike poll is an addition,** on the brief's point that spikes miss a mob launched from inside the strip.
4. **Each wall stands on the deck, not on the east ramp,** unlike the pilot's. Both east ramps stay walkable, and a hero who falls in climbs out either way, then clears the wall.
5. **`florence_pool_config` `MaxWarmUpInstances` rose.** The guide says to check it; it did not hold.
6. **The Level 1 route has a crumbling step,** the "skill" half: on the base kit, with a hop of 225 off it.

## Did not ship

- No bot exercised the trench, the lane, the ledge or either route. No Normal or Hard probe.
- No art: Level 1's new platforms use its own palette, and its crumbling steps are the template's.
- No `FontHazardClearance` change was needed; both fonts are far from the new hazards.
- Par is the authored +30, not measured. The slowest measured crossing here was 6.2 s on Level 3, with Lincoln on Easy.

## What the docs owe (GR-DOCS)

- Ledger: `USER-LEVEL-TRAVERSAL` and `USER-OPTIONAL-ROUTES-SHORTCUT-ONLY` for Levels 1 and 3; `PROVISIONAL-TRAVERSAL-RULES` (the list above); `VERIFY-PAR-SECONDS` (Level 3 570); the `special_momentum` every-slot rule against the documented "a Full-Calibration slot never shows one".
- AGENTS.md:
  - The Midway lock's 700–2500 and 3820 → 2500 in the Level 3 text.
  - Florence's Extractor placement (the entry hall's far end is gone) and the removed 5000/650 platform.
  - The first-use tooltip paragraph ("a Full-Calibration slot never shows one", "for it this node is inert").
  - Florence's spike poll beside GR-HAZ's limit.
  - The answer to "what builds the geyser" (it is scene-authored).
  - The new suites and baseline arithmetic.
- `design-godot.md` master and mirror: the first-use tooltip rule if it states it; Florence's and Chicago's layout notes.
- `docs/DUST_ECONOMY.md` and its contract: nothing; no pool moved.

## For the orchestrator

- **`en.csv` keys to compile: `florence_room_rooftops`, `chicago_room_skyway`.** Until you do, the level content suites' key checks can fail on this branch. I did not commit `en.en.translation`.
- `TraversalContentKit.LevelsWithTraversal` is a one-line additive edit that every B2 agent makes, so expect that conflict. Its union is the fix.
- Expect the `en.csv` tail conflict too. No other shared test file changed.
- `FirstUseTooltips` and its presenter are now touched by GR-SCHED, GR-TF and me. I moved the `DefyButton` offer below the new return in the presenter's poll.
