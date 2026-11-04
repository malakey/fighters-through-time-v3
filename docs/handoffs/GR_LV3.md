# GR-LV3: Levels 7, 8 and 9, traversal stretches, routes, swarm rows and two arenas

**Nobody has played any of this.** Every claim below is a headless suite, a real-frames script or a bot run. Nobody has looked at the Careening Yard, the Dune Crossing, the Beach Run, the listed deck, the cannonballs or the General's lanterns.

Branch `gr/lv3`. Workstream of the gameplay review package (plan Phase B2, §1.11, §1.14, §1.15; rulings 7a, 7b, 7c, 7c+, 1b, 1b+, 1b++, 14, 4a).

## What shipped

### Level 7, Nassau

| Piece | Where |
|---|---|
| Carve | **From the west end:** `QuayLockMinX` 380 → 1720, so the quay lock is 1720–3580, 1860 wide. ID and tables unchanged |
| Why west | The Ranger channel's haul-out (3400) must land inside its lock, and the launch yard and rope channel stay as they were (deviation 1) |
| Moved | Wave 1: 1200 → 2250, 1750 → 1950. Scene `MortarQuayA` (900, 900) → (900, 1020), onto the dock's floor |
| Removed | Platforms 500/760, 900/640, 1400/740, 1650/680 |
| Stretch `level_07.careening_yard` | 400–1700, room key `nassau_room_careening` ("The Careening Yard"), camera 0–1700 |
| Trench | Floor y 1020, ramps 560→720 and 1380→1540 |
| Line | Deck 900 → 580–780/top 782 → crumbling 830–1010/top 712 → one-way 1080–1280/top 712 → 1300–1500/top 712 → wall 1580–1620/top 690 → deck 900 |
| Off the line | One-way 1040–1240/top 880; `MortarQuayA` as the lane |
| Route `level_07.crane_line` | One-way 1080–1280 → 930–1050/top 520 → 690–850/top 410 → 930–1110/top 310 → 1355–1575/top 310 |
| Reward | `level_07.secret`, moved from (900, 537) to (1465, 215) |
| Shortcuts | Burst 690–850/top 410 → the end: gap 505, rise 100. Rise 1300–1500/top 712 → the end: rise 402 |
| Swarm | `level_07.swarm1` on the quay lock: 8 on Normal and Hard, 5 on Easy. It comes after the Ranger's crew, because waves spawn in order |
| Warm pool | `standard_enemy` 12 → 18 |
| Par | 630 → 660 |

No rope was added. Unmoved: all three ropes, the launch yard, wave 2 (the marksman on the yard too), the Middle 5650, the PreBoss 8680, the font 9100, the arena 9350, the level's width, baselines and every manifest pool.

### Level 8, Alexandria

| Piece | Where |
|---|---|
| Carve | `DuneLockMaxX` 3200 → 2000, so the dunes lock is 1700 wide. ID and table unchanged |
| Moved | Slasher 2100 → 1650 (patrol 1500–1800), ward 2900 → 1900. Scene `DuneB` (2300, 660) → (2590, 780), on the trench floor |
| Removed | Platforms 2200/450, 2600/520, 3000/600 |
| Stretch `level_08.dune_crossing` | 2020–3200, room key `egypt_room_crossing` ("The Dune Crossing"), camera `Room1CameraBounds`, so it keeps to the surface band |
| Trench | Floor y 820, ramps 2060→2220 and 2940→3100. The drift covers 2270–2910 of it, so a hero who falls wades out |
| Line | Deck 700 → 2070–2270/top 582 → crumbling 2340–2520/top 512 → one-way 2600–2800/top 512 → 2860–3060/top 512 → wall 3140–3180/top 490 → deck 700 |
| Off the line | One-way 2540–2740/top 660 |
| Route `level_08.obelisk_line` | One-way 2600–2800 → 2500–2620/top 320 → 2260–2420/top 210 → 2500–2680/top 110 → 2925–3145/top 110 |
| Reward | `level_08.extractor_dune_ridge`, moved from (2600, 460) to (3035, 38) |
| Shortcuts | Burst 2260–2420/top 210 → the end: gap 505, rise 100. Rise 2860–3060/top 512 → the end: rise 402 |
| Swarm | `level_08.swarm1` on the siege line: 8 on Normal and Hard, 5 on Easy |
| Warm pool | `standard_enemy` 12 → 18 |
| Par | 360 → 390 |

Unmoved: the Middle 5640, the PreBoss 8490, the font 8660, the arena 8900, the secret at (3950, 327), width, baselines, pools.

### Level 9, Hakata Bay

| Piece | Where |
|---|---|
| Carve | `SeawallLockMaxX` 2860 → 2000, so the seawall is 1700 wide. The stretch also uses the old gap up to the Lantern Watch |
| Moved | Trooper 2450 → 1640. Spikes 2050 → 1850. The Lantern Watch's room trigger 2920 → 3190, so the stretch keeps its camera up to the entry barrier |
| Removed | Platforms 2300/980 and 2650/860 |
| Stretch `level_09.beach_run` | 2020–3130, room key `berlin_room_beach_run` ("The Beach Run"), camera (0, 320, 3130, 1080), room 1's bottom-anchored band |
| Trench | Floor y 1320, ramps 2060→2220 and 2720→2880 |
| Line | Deck 1200 → 2070–2270/top 1082 → crumbling 2340–2520/top 1012 → one-way 2590–2770/top 1012 → 2790–2950/top 1012 → deck 1200. No wall (deviation 3) |
| Off the line | One-way 2540–2740/top 1180; lane `level_09.thunderbomb_beach` at 2450 on the trench floor, with a mantlet (1.0 / 0.6 / 2.6 s, 18 damage) |
| Route `level_09.mast_line` | One-way 2590–2770 → 2390–2510/top 820 → 2090–2250/top 710 → 2330–2510/top 610 → 2755–2975/top 610 |
| Reward | `level_09.extractor_ruins`, moved from (2300, 940) to (2865, 538) |
| Secret | Moved from (2650, 757) to (3040, 857), on a new perch 2960–3120/top 952 over the first cover pocket. The old perch would have bonked the line |
| Shortcuts | Burst 2090–2250/top 710 → the end: gap 505, rise 100. Rise 2790–2950/top 1012 → the end: rise 402 |
| Swarm | `level_09.swarm1` on the seawall: 8 on Normal and Hard, 5 on Easy. One row, not two (deviation 4) |
| Warm pool | `standard_enemy` 14 → 20 |
| Par | 360 → 390 |

**The stealth and the near-capture are untouched:** the Lantern Watch's area and single wave, its three beams and three signal lines, the first cover pocket, `NearCaptureTriggerX` 6500, the Middle 6570, the PreBoss 8740, the font 8640, the arena 8800.

### The Dread Admiral's list (Level 7, Phase 2)

- **One static slab** (`ListedDeckSlab`, `ListedDeckShape`), built hidden and disabled, from the west deck's corner (9220, 880) to the stern wall's foot (10580, 960): 3.4°. No tilt physics.
- **Two rolling cannonballs** (`RollingCannonball`), rail 9410, end 10540, 300 and 240 px/s, 1 s warning, cooldowns 2.5 and 3.5 s, 14 damage, one hit a roll, blockable. Each has an Area2D hurt trigger, follows `ListedDeckTopAt`, and is a `StoryCyclicHazard`, so phases, block, freeze and rewind policy are that class's.
- `ListAdmiralDeck()` is public and idempotent, runs off the physics flush, starts ball A on its warning and ball B after a cooldown, and starts no telegraph while `IsWorldHeld`. It lifts the hero and the boss onto the slab, which rises at most 29 px over the old deck.
- `StopAdmiralCannonballs` runs on `BossDefeated`. **The list stays** after he falls; the balls stop.
- `loose_cannon_roll` stays (ruling 4a). `BossPhaseRuleMatrixTests` gains one arena row.

### The Thunderbomb General's lanterns (Level 9, Phase 2)

- **Three `SearchlightZone`s** in delayed-strike mode (`GeneralLanterns`), built dark with `Monitoring` off, at x 9180, 9720 and 10230, y 680, so the cone ends on the deck.
- Sweep periods 4.7, 5.9 and 5.3 s, arc 30°, grace 1.5 s, 20 damage per strike (`GeneralLanternStrikeDamage`, the figure dossier C gives for `artillery_barrage`).
- Every sweep footprint is inside `BossArenaBounds`. They are separate from `Searchlights`, so no signal line feeds them and a catch posts no objective.
- `LightGeneralLanterns()` is public and idempotent and runs off the flush. `DouseGeneralLanterns` runs on `BossDefeated`.
- `artillery_barrage` stays (ruling 4a). `BossPhaseRuleMatrixTests` gains one arena row.

## API

- `RollingCannonball` (new, `scripts/Environment/`): `Create`, `StartX`, `EndX`, `RollSpeed`, `Radius`, `DeckTopAt`.
- `Level07Controller`: `YardStretchID`, `CraneRouteID`, `YardRoomKey`, `YardMinX`, `YardMaxX`, `YardWestDeckEndX`, `YardEastDeckStartX`, `CraneLineEndX`, `SwarmRowID`, `SwarmSpawnXs`, `ListedDeckHighJoint`, `ListedDeckLowJoint`, `ListedDeckTopAt`, `CannonballRailX`, `CannonballEndX`, `AdmiralDeckListed`, `ListedDeckSlab`, `ListedDeckShape`, `Cannonballs`, `ListAdmiralDeck()`. `FlagshipMidDeckWestX` and `FlagshipMidDeckEndX` became public.
- `Level08Controller`: `CrossingStretchID`, `ObeliskRouteID`, `CrossingRoomKey`, `CrossingMinX`, `CrossingMaxX`, `CrossingWestDeckEndX`, `CrossingEastDeckStartX`, `ObeliskLineEndX`, `SwarmRowID`, `SwarmSpawnXs`.
- `Level09Controller`: `BeachRunStretchID`, `MastRouteID`, `BeachRunRoomKey`, `BeachRunMinX`, `BeachRunMaxX`, `BeachRunWestDeckEndX`, `BeachRunEastDeckStartX`, `MastLineEndX`, `SecretPerchX`, `SwarmRowID`, `SwarmSpawnXs`, `GeneralLanternPlan`, `GeneralLanternY`, `GeneralLanternStrikeDamage`, `GeneralLanternsLit`, `GeneralLanterns`, `LightGeneralLanterns()`.
- `TraversalContentKit.LevelsWithTraversal` gains 7, 8 and 9.

## A bug the tests found

My first listed deck was two slabs joined at the mid deck's corner. The first ran on 16 px past the joint (`BuildRampBetweenJoints`' buried end), and because it was shallower than the second it stood 0.34 px proud of it. Lincoln, walking west uphill, stopped dead at that lip in real frames. The math sweeps had nothing to say about it. It is now one slab. **Worth knowing for GR-LV4:** two slabs meeting at a joint leave a lip when the upper one is the shallower.

## Measured: bot probes

`play`, under the lock. **The bot crossed every stretch in every run that reached it.** Stretch seconds count from the first to the last trace sample inside the span, and they include any fighting at its ends.

| Run (Easy, seed 0) | Stretch | Level | Lowest point in the span |
|---|---|---|---|
| L7 Lincoln | 6.0 s | `stuck_lock:boarding` at 154 s, past the stretch and the ropes | Never in the trench |
| L7 Joan | 4.3 s | Complete, 100 s, 0 deaths, rank C | Never in the trench |
| L8 Lincoln | 9.1 s | Complete, 115 s, 0 deaths, rank C | **Reached the trench floor (y 820), and left it** |
| L8 Joan | 3.9 s | Complete, 88 s, 0 deaths, rank B | Never on the floor (y 689) |
| L9 Lincoln | 4.5 s | Complete, 125 s, 1 death, rank C | Never in the trench |
| L9 Joan | 3.0 s | Complete, 108 s, 1 death, rank C | **Reached the trench floor (y 1320), and left it** |

**Further samples:**

| Level, difficulty | This branch | Base `160353c5` |
|---|---|---|
| L7 Lincoln, Easy | Complete 1 of 3, `stuck_lock:boarding` 2 | Complete 1 of 3, `stuck_lock:boarding` 2 |
| L7 Joan, Easy | Complete 2 of 2 | Not run |
| L7 Joan, Normal | Complete 2 of 4, `stuck_lock:quay` 1, Death collapse 1 | Complete 2 of 2 |
| L7 Lincoln, Normal | `stuck_lock:quay` 1 of 1 | Not run in the control; earlier gate batches read the same stall |
| L8 Lincoln, Normal | Complete, 2 deaths | Not run |
| L8 Joan, Normal | Complete, 3 deaths | Not run |
| L9 Lincoln, Normal | Death collapse at the boss | Complete, 4 deaths |
| L9 Joan, Normal | Death collapse at the boss | Death collapse at the boss |

These samples are thin, and seed-0 runs are not frame-identical across launches.

**What the stalls are:**
- **L7's boarding stall (Lincoln, Easy) is not this work.** It reproduces at the base at the same rate, and no geometry east of 3600 moved. The bot stands on a piling reading `wait-board` while the last two members stand on the ramp, the known skiff-channel gap.
- **L7's quay stall on Normal** is the marksman on the launch yard's west end at (3330, 672), `PROVISIONAL-BOT-FLIGHT-MODEL`. Lincoln's loop matches the known one.
- **Joan's one quay stall is new against that note**, 1 of 4 runs here and 0 of 2 at the base. She stood on the 3200/760 step at (3278, 752), directly under him, at 7 HP, swinging and blocking for 120 s. A wider sample is needed to say whether the swarm's damage brings this on. That marksman did not move. It is a bot gap, not a content block: the route up is unchanged.

**L8's last-gate drop, as asked, reported and not tuned:** 4 of 4 of my probes completed Level 8 (2 Easy, 2 Normal). **`anti_air_strikes` read 0 in all of them, on all three levels**, so these runs give no sign of the Anti-air trait at work. Most damage was the Jackal Priest (Normal 280 and 241). That says nothing about the gate's nine-hero number: only Lincoln and Joan ran.

**The arenas in the probes:** the cannonballs hit in every L7 run that reached the second phase, 14–28 damage a ball a run. The lanterns barely registered: one strike in six L9 runs (13 damage after the block rule's arithmetic, Lincoln on Easy). The Normal L9 collapses are mostly the General himself (238 and 258).

**Swarms:** all cleared, 182–733 frames, 0–48 hero damage. On Easy the seawall's row spawns almost at once (frames 24–35): Easy's prefix of its first wave is already at or under two alive when the lock engages, so nothing delays it.

**Not exercised by a bot:** the ledges, the walls, the routes and the shortcuts (bots do not use them), the L9 lane, and a hazard kill (`hazard_kills` 0 throughout).

## Provisional numbers (`PROVISIONAL-TRAVERSAL-RULES`, `PROVISIONAL-BOSS-ARENA-RULES`)

The user ruled only the 280 / 240 limits, the swarm row's trigger and sizes, the +30 minimum, and the Admiral's "at most 8 degrees".

- Every platform position, the trenches' depth 120 and ramp run 160, each wall's top and width, the route shapes, the new secret perch and both moved secret positions.
- The L9 lane's timing and 18 damage, copied from the Rampart lane.
- Swarm spawn positions and lock choices. Wave spawns moved by the carves.
- Display names "The Careening Yard", "The Dune Crossing", "The Beach Run". Stretch and route IDs.
- The list: 3.4°, the balls' speeds, warning, cooldowns, 14 damage, 22 px radius, rail and end.
- The lanterns: positions, periods, arcs, 20 damage. The arc is chosen so a stationary hero under a beam's centre stays lit past the grace (1.72, 2.16 and 1.94 s at the centre, inferred from the sweep; the real-frames case measures lantern A's strike).

## Tests

**Declared +17, actual +17.**

| Suite | Cases |
|---|---|
| `Level07TraversalStretchTests` | 3 |
| `Level08TraversalStretchTests` | 3 |
| `Level09TraversalStretchTests` | 3 |
| `Level07AdmiralArenaTests` | 4 |
| `Level09GeneralArenaTests` | 4 |

Changed in place, no count change:
- `Level07ContentTests`: the carved lock, the quay's third wave, the 1920 floor relaxed to the view's width for the Careening Yard alone.
- `Level08ContentTests`: five rooms, the carved lock, the siege line's second wave.
- `Level09ContentTests`: five rooms, the arena room's index, the carved lock, the seawall's second wave.
- `BossPhaseRuleMatrixTests`: two `ArenaRules` rows. `TraversalContentKit`: the level list.

Last filtered run, 29 suite filters including every shared sweep, with the keys compiled locally: `Passed! - Failed: 0, Passed: 273, Skipped: 0, Total: 273`. No full run: the orchestrator owns those.

**Seen failing.** The new suites cannot compile against the old code, so I mutated the build and reverted, in two runs. All 17 failed at least once:
- Run 1, 14 of 17: par unchanged, an unguarded flush, the slab built visible, defeat unwired, no lift, a raise after defeat allowed, six-mob swarms, a ball ignoring its freeze and leaving the deck, the drift out of the trench, the camera bands removed, the lanterns' mode dropped and a sweep leaving the arena.
- Run 2, the other 3: a wall too high to clear, a wall across the trench floor, the secret's perch out of reach, `SearchlightZone` ignoring its freeze.

## Effects

No save field, schema, protocol, component or Fighter hash change. Nothing under `scripts/FighterSim/` changed. F05 is exact: fodder weight 0, pools unchanged, `TraversalStretchContentTests` sums the ledger on all three difficulties. No boss HP, wave or kit number changed beyond the swarm rows.

## Deviations from the plan and dossier

1. **L7's stretch opens the level instead of sitting between locks.** The dossier and the brief point at the rope gap as a model, but `TraversalStretchContentTests` forbids a rope in a stretch and requires a floor, and it has neither. Carving the quay's east end would put the Ranger haul-out outside its lock. So the carve takes the west end, and the Careening Yard comes before the first lock.
2. **L7's stretch camera is 0–1700**, narrower than `Level07ContentTests`' 1920 floor and wider than the 1280 view, because it can only span the level's start to the barrier. The floor is relaxed for this room alone.
3. **L9's stretch has no wall.** The first cover pocket (2890–3130) stands where the wall would go, and the stealth level's pocket stays. Its crossing has fewer pieces: a hero can walk the trench through the lane.
4. **L9 takes one swarm row, not two.** The plan names the beach as a two-row candidate, but 1b++ allows two only on the largest locks, and after the carve the seawall is 1700 wide. The Lantern Watch is 3200 wide, but a swarm there would disturb the stealth.
5. **The Admiral's slope is 3.4°, well under the 8° limit.** A steeper slab needs a riser at one end or would bury a hero. Read it as a floor change a person may not notice: the balls are most of the rule. The slab also starts 130 px west of `BossArenaBounds`, because its high joint has to be the west deck's corner; the balls stay inside.
6. **The cannonballs are scripted, not `RigidBody2D`** as dossier C suggested: the brief asks for Area2D hurt triggers and no ship physics, and a rigid body on Environment would block heroes and the bot. A launched mob could take a ball's impact (inherited `HazardContactMask`), though no mob stands in that arena by design.
7. **The stand-ins stay** (`loose_cannon_roll`, `artillery_barrage`), as the pilot kept `mortar_barrage`, with arena rows added: ruling 4a keeps boss kits.
8. **Bot gaps, not fixed:** the marksman at L7's quay and the skiff wait, above.

## Did not ship

- No bot took a route, a shortcut, a wall or a ledge, and none used the L9 lane.
- No real Post-Landing Hold test on the balls or lanterns: as with the Duke, each case drives `SetTimeFrozen` and checks `story_hazard` is in the hold's groups.
- `PlaytestNav` caches statics, so a bot in the Admiral's Phase 2 plans on the old deck under the slab. It still walks the fight in the probes, but its flights are modelled up to 29 px off.
- No art: the slab uses the ramp colour and the balls are flat discs.
- Par is the authored +30, not measured. Lincoln's slowest stretch was 9.1 s.

## An observation outside this workstream

**Inferred from the sweep arithmetic, not measured:** the Lantern Watch's own beams (arcs 64°, 52°, 44°; periods 2.8, 3.9, 5.1 s) may rarely hold a stationary hero for the 1.5 s grace at the deck, because exposure resets when the hero leaves the cone. The first beam's centre dwell works out to about 0.4 s a pass. It may read as less threatening than the design says. I changed nothing there, since the level's stealth was to stay. A question for the session.

## What the docs owe (GR-DOCS)

- Ledger: `PROVISIONAL-TRAVERSAL-RULES` and `PROVISIONAL-BOSS-ARENA-RULES` (the lists above); `VERIFY-BOSS-PHASE-ARENAS` narrowed by the Admiral and the General; `VERIFY-PAR-SECONDS` (L7 660, L8 390, L9 390); `PROVISIONAL-BOT-FLIGHT-MODEL` (Joan's stall under the yard's marksman, and the Lincoln Easy boarding stall at 2 of 3); a question row for the Lantern Watch's dwell.
- AGENTS.md: the three stretches and routes; the three carved locks' new ranges in the encounter-lock and level text (quay 1720–3580, dunes 300–2000, seawall 300–2000); both secrets and both Extractors moved; Level 7's mortar on the dock floor; `RollingCannonball`; the new suites.
- Design master and mirror: the Admiral's and the General's arena rows, built.

## For the orchestrator

- **`en.csv` keys to compile: `nassau_room_careening`, `egypt_room_crossing`, `berlin_room_beach_run`.** Until you do, each level's localisation case fails on this branch.
- Expect merge conflicts only in `en.csv`'s tail, `TraversalContentKit.LevelsWithTraversal` and `BossPhaseRuleMatrixTests`' rows.
- Bot gates on Levels 7–9 read the carves. L7 on Normal is worth a wider Joan sample.
