# GR-LV2: Levels 4, 5 and 6, and the Vulcan's eruption

**Nobody has played any of this.** Every claim below is a headless suite, a real-frames script or a bot run. Nobody has looked at the Glacis, the Well Deck, the Ashfall, the three routes, the vents or the debris.

## What shipped

Each level has a stretch carved out of its widest usable lock, a shortcut-only route, a swarm row and +30 s of par. Every lock keeps its ID and wave table apart from the swarm row. Nothing moved east of a stretch.

| | Level 4 | Level 5 | Level 6 |
|---|---|---|---|
| Lock carved | Gatehouse 300–3200 → 300–2000 | Staircase 380–3400 → 380–2380 | Ash Road 5660–8500 → 6720–8500 |
| Stretch | The Glacis, 2020–3200 | The Well Deck, 2400–3400 | The Ashfall, 5680–6700 |
| Deck, trench floor | 900, 1020 | 1140, 1260 | 900, 1020 |
| Line | 2170 → barricade 2500 → crumbling 2790 → one-way 3000 → wall 3150 | 2560 → crumbling 2810 → one-way 3060 → down to D2 | 5830 → crumbling 6090 → one-way 6350 → 6580 → down to the road |
| In the trench | The moved spike strip, a one-way up. West ramp walks out; the wall stops the east one, as on Level 2 | Hull step 1's water, a dry one-way up. Both ramps walk out | The moved `AshGeyserRoadA`, a one-way up. Both ramps walk out |
| Route, reward | Battlement line, the secret, moved to (3035, 215) | Davit line, the secret, moved to (2560, 535) | Roofline, `level_06.extractor_ashroad`, moved to (6580, 238) |
| Burst shortcut | Gap 505, rise 100 | Gap 505, rise 100, flown west | Gap 505, rise 100 |
| Rise shortcut | 402 | 402 | 402 |
| Swarm row (8; Easy 5) | `level_04.swarm1`, gatehouse | `level_05.swarm1`, boiler casing, each mob on its own deck | `level_06.swarm1`, Forum |
| Warm `standard_enemy` | 12 → 18 | 14 → 20 | 12 → 18 |
| Par | 360 → 390 | 270 → 300 | 360 → 390 |
| Spawns moved | Rioter 2700 → 1450 | Slashers 2500 → 1950, 3100 → 2330 | Slasher 6200 → 7300 |

Removed from the freed spans: Level 4's crest 3000/560; Level 5's one-ways 2600/980 and 3050/880; Level 6's platforms 6100/760 and 6600/700.

Unmoved, and pinned by the new suites:
- Level 4: the tower gap's puzzle span (4760–6120), both later anchors, the font, the Tribunal's pit and its fall-net rule.
- Level 5: every deck, the flood's tables, the Eraser debut wave, both later anchors and the font.
- Level 6: the vault and its rockfall (x 5390–5660 holds nothing new), the Middle anchor (5600), the run's ends, the font and the arena.

### The Vulcan's eruption (ruling 14)

`scripts/Environment/Level06Controller.Vulcan.cs`, on the Duke's pattern:
- Five cyclic vents, built disabled, inside `BossArenaBounds`, at x 9370, 9610, 9850, 10090, 10330. Each strip is 120 wide, with 120 px of safe floor between two. The strips top out at y 680, so the two ledges (top 632) stay clear.
- `PhaseEntered` starts them, hopping off the physics flush when it has to. `RaiseVulcanEruption()` is public and idempotent. They stop on `BossDefeated`.
- Debris falls every 2 s from Phase 2 (`EruptionDebrisIntervalSeconds`), near the hero, inside the arena and off the PreBoss anchor, with at most 2 airborne.
- A world hold or a paused clock suspends the rain and holds pieces already in the air. Nothing else holds debris, so the level does it (`FallingDebris.SetFrozen` had no caller before this).
- The vents hit launched and thrown mobs through GR-HAZ, and a suite confirms it. Debris stays hero-only.
- `lava_vent_field` stays, with an arena row added in `BossPhaseRuleMatrixTests`. Ruling 4a keeps boss kits, and the pilot did the same with `mortar_barrage`.

### API

- `Level04Controller`: `GlacisStretchID`, `BattlementRouteID`, `SwarmRowID`, `GlacisMinX`, `GlacisMaxX`, `GlacisWestDeckEndX`, `GlacisEastDeckStartX`, `BattlementEndX`, `SwarmSpawnXs`.
- `Level05Controller`: `WellDeckStretchID`, `DavitRouteID`, `SwarmRowID`, `WellDeckMinX`, `WellDeckMaxX`, `WellDeckWestEndX`, `WellDeckEastStartX`, `DavitEndX`, `SwarmSpawns`.
- `Level06Controller`: `AshfallStretchID`, `RooflineRouteID`, `SwarmRowID`, `AshRoadExtractorID`, `AshfallMinX`, `AshfallMaxX`, `AshfallWestDeckEndX`, `AshfallEastDeckStartX`, `RooflineEndX`, `SwarmSpawnXs`, `PreBossCheckpointX`, `VulcanVentXs`, `VulcanVentWarningSeconds`, `VulcanVentActiveSeconds`, `VulcanVentCooldownSeconds`, `VulcanVentDamage`, `VulcanVentStaggerSeconds`, `EruptionDebrisIntervalSeconds`, `VulcanVents`, `VulcanEruptionRaised`, `EruptionDebrisLaunched`, `EruptionDebrisAirborne`, `RaiseVulcanEruption()`, `EruptionTargetX(x)`.

## Measured: bot probes

`play`, Easy, one run each, under the lock (tags `lv2after`, `lv2after6`, `lv2before6`). "Stretch" is the time from first to last trace sample inside the span.

| Run | End | Stretch | Took | Rank |
|---|---|---|---|---|
| L4 Joan | Complete, 109.5 s, 0 deaths | 8.2 s | The line | B, 64.2 |
| L4 Lincoln | Complete, 157.3 s, 0 deaths | 6.4 s | The moat floor, taking the spikes (30 damage) | D, 33.2 |
| L5 Joan | Complete, 78.5 s, 0 deaths | 2.2 s | The line | B, 61.5 |
| L5 Lincoln | Complete, 95.6 s, 0 deaths | 4.3 s | The line | C, 36.4 |
| L6 Joan | Complete, 102.6 s, 0 deaths | 3.2 s | The line | C, 38.5 |
| L6 Lincoln | Complete, 142.4 s, 1 death | 6.3 s | The sewer floor | D, 34.2 |

**Every run that reached a stretch crossed it.** That includes Level 6's Cleopatra (4.7 s) and Mozart (3.7 s). Cleopatra took the moved geyser's lane.

**No bot took a route or struck a moved reward.** Bots do not use shortcuts (risk 3).

**Level 6, the vault (ruling 12), same heroes before and after:**

| Hero | Before (`e9dd639e`) | After |
|---|---|---|
| Einstein, Leonardo, Shakespeare, Tesla, Tubman | `stuck_lock:level_06_pompeii_lock_vault` | The same |
| Joan | Complete, 81.3 s, 0 deaths | Complete, 102.6 s, 0 deaths |
| Lincoln | Complete, 111.6 s, 1 death | Complete, 142.4 s, 1 death |
| Cleopatra | Complete, 157.8 s, 0 deaths | Complete, 185.8 s, 2 deaths |
| Mozart | Complete, 147.2 s, 1 death | Complete, 187.3 s, 3 deaths |

The stall did not get worse: the same five heroes stall and the same four finish. I did not investigate the stall.

The four completions took 21–40 s longer. Cleopatra and Mozart died more, always at the boss, in single runs. The vents fired: `vulcan_vent_3`, the one nearest the boss's anchor, took 60 damage from Joan, 50 from Mozart and 18 from Lincoln, the only vent to hit a bot. I report this and tuned nothing (Q2).

**Other readings:**
- Each Level 6 run shows one hazard kill.
- Every swarm cleared in every run.
- Integrity at the end stayed at 84–91 %.
- Normal and Hard were not probed. No difficulty's clock was re-measured.

## Tests

**Declared +13, actual +13.**

| Suite | Cases |
|---|---|
| `Level04TraversalStretchTests` | 3 |
| `Level05TraversalStretchTests` | 3 |
| `Level06TraversalStretchTests` | 3 |
| `Level06VulcanArenaTests` | 4 |

Changed in place, no count change:
- `Level04ContentTests`, `Level06ContentTests`: five rooms, the arena's room index, the carved ranges, the swarm wave.
- `Level05ContentTests`: the carved range, the casing's second wave, and the flood case's D2 position (x 3000 is air now).
- `Level06ContentTests.ClearingTheRoadAgainstItsEntryBarrierStillStartsTheFrontBehindTheHero`: the carve moved the barrier east of the old floor's reach, so it now pins `EscapeStartFor` directly and that the hero stands against the barrier.
- `RewardManifestTests`: the Titanic's wave count.
- `BossPhaseRuleMatrixTests`: one row.
- `TraversalContentKit.LevelsWithTraversal`: adds 4, 5, 6.

**Seen failing,** by mutating the build and reverting, two runs:
- Run 1, 12 of 13 new cases failed: par unchanged, a wall in each trench, the row's trigger changed, the stretch moved into the vault's span, vents built on, no hop off the flush, and a vent moved so no safe floor remained.
- Run 2 (after I rewrote the freeze case, which run 1 showed was vacuous): the defeat left unwired and the hold ignored. Both cases failed, the second reading "The first piece came 61 frames after the thaw, not 120".
- Not mutated: the shared sweeps, which only gained three levels.

The freeze case runs two real Post-Landing Holds through `BeginPlacementHold`, which the pilot's did not.

## Effects

No save field, schema, protocol, component or Fighter hash change. Nothing under `scripts/FighterSim/` changed. `StoryLevelControllerBase.cs` is untouched. F05 is exact: fodder has weight 0, no pool moved, and the sweep sums each ledger on three difficulties.

## Provisional numbers (`PROVISIONAL-TRAVERSAL-RULES`, `PROVISIONAL-BOSS-ARENA-RULES`)

The user ruled only the 280 / 240 limits, the swarm trigger and sizes, and the +30 minimum.
- Every platform, trench, ramp and route position above, and the spawn moves.
- Swarm spawn positions.
- The choice of lock for each swarm row.
- Vents: x positions, 1.0 s warning, 0.9 s active, 2.4 s cooldown, 14 damage, 0.86 s stagger.
- Debris: one piece every 2 s, read as twice the Tremor's first rate.
- `paris_room_glacis` "The Glacis", `titanic_room_well_deck` "The Well Deck", `pompeii_room_ashfall` "The Ashfall".

## Deviations from the plan and dossier

1. **Level 6 carves the Ash Road's west end, not the Forum.** The road is the widest lock (§1.14), and the stretch then sits past the Middle anchor and clear of 5390–5660. Heroes stalled at the vault therefore never reach it, which is why only four heroes exercised it.
2. **"Double debris" has no base rate to double.** The Tremor retires at the PreBoss lock, so Phase 1 has no debris. Phase 2 rains at 2 s against the Tremor's first-stage 4 s, and Phase 1 stays as it was. A question for the session.
3. **Level 5's Davit line flies west,** because the stretch is 1000 px wide and its rise shortcut stands over the first step. Its numbers match Level 2's.
4. **Level 5's and Level 6's trenches have no wall step,** so both ramps walk out. On Level 5 the trench floods.
5. **Level 4 and Level 5 pay the secret, moved.** The old perches went with the carve. `level_04.extractor_barricade` stays on the barricade, which is now a line step.
6. **The swarm comes at once on Easy on Levels 4 and 6.** The trigger is `WhenAliveAtMost(2)` and Easy's prefix of a 3-mob table is 2, so the row spawns as the hero engages (frame 24–35), not after a first kill. Level 2's siege line has the same shape, so I kept the ruled trigger. Level 5's casing, with 4 on the table, does not do this. A question for the session.
7. **Lincoln still has no shortcut on these levels** (the pilot's deviation 6).
8. **The vents do not read `IsBossDefeated` beyond the raise.** They stop through the `BossDefeated` event, as the Duke's steam does.

## Did not ship

- No bot took a route or struck a moved reward.
- No bot run on Normal or Hard.
- The Level 5 wade was driven by a test, with the flood raised by hand. No bot walked through the water.
- No art: platforms are each level's palette.
- The stall at the vault is unchanged and uninvestigated (ruling 12).
- Par is the authored +30. Bots spent 2–8 s in the stretches, so the measured pass should not need to raise it for the stretch alone. The 21–40 s longer Level 6 runs come mostly from the swarm and the boss.

## What the docs owe (GR-DOCS)

- Ledger: `PROVISIONAL-TRAVERSAL-RULES` and `PROVISIONAL-BOSS-ARENA-RULES` (the lists above); `VERIFY-BOSS-PHASE-ARENAS` narrowed by the Vulcan; `VERIFY-PAR-SECONDS` (Levels 4, 5, 6: 390, 300, 390); question rows for the debris base rate and for the Easy swarm arriving at engage.
- AGENTS.md: the new lock ranges (Level 4 gatehouse 300–2000, Level 5 stair decks 380–2380, Level 6 Ash Road 6720–8500), the three stretches and routes, and the "Road's first civilian at 7050" wording, which is already stale. The bot note "Level 6's vault" stands.
- Design master and mirror: the Vulcan's arena row, built, with the debris reading above.

## For the orchestrator

- **`en.csv` keys to compile: `paris_room_glacis`, `titanic_room_well_deck`, `pompeii_room_ashfall`.** The tail of `en.csv` and `LevelsWithTraversal` will conflict with GR-LV1, LV3, LV4 and LV5; both are unions.
- A first `dotnet test` once reported `Rebuilding Godot Project ends with exit code: -1073741819` and matched no test (signature 1). The same command ran clean on retry, with no orphans listed.
- `.import` churn reached `addons/` and `audio/` as well as `assets/`. I discarded all of it with `git checkout -- .`.
