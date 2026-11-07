# GR-LV5: Levels 13, 14 and 15, traversal stretches, routes and the Level 15 swarm row

**Nobody has played any of this.** Every claim below is a headless suite, a real-frames script or a bot run. Nobody has looked at the Fragment Stair, the Breach Catwalk, the Portico run or the three routes.

Branch `gr/lv5`. Workstream of the gameplay review package (plan Phase B2, §1.11, §1.14, §1.15; rulings 7a, 7b, 7c, 7c+, 1b, 1b+, 1b++, 4a, Q3). No arena work, no boss, wave or kit number changed beyond Level 15's swarm row.

## What shipped

### Level 13, the Chronal Void

| Piece | Where |
|---|---|
| Carve | **From the west end:** `ThresholdLockMinX` 320 → 1880, so the Threshold is 1880–3200, 1320 wide. ID and table unchanged |
| Why west and why 1880 | Deviations 1 and 2 |
| Moved | Wave 1's west phantom 1180 → 2100. `level_13_extractor_0` (a severed conduit) from the Alexandria cornice (2380, 890) to the route's end (1350, 996) |
| Stretch `level_13.fragment_stair` | 320–1860, room `void_fragment_stair`, key `void_room_fragment_stair` ("The Fragment Stair"), camera −60–1860 |
| Floor | No trench (deviation 3). The shelf (y 1900) runs under the whole stretch; the Threshold rift pocket (1500, unmoved) taxes it |
| Line | Shelf 1900 → Orleans 640–880/top 1750 → Paris 950–1170/top 1610 → Pompeii 1250–1470/top 1470 → Berlin 1550–1770/top 1330 → shelf. These are the authored era motifs, registered as the line; none moved, so the `Ladders` contract is untouched |
| New room | `void_threshold_lock` at 1940, camera 0–3300, not one-shot: the carved lock needs a room of its own once the stair's camera ends at 1860 |
| Route `level_13.drift_line` | Paris → 850–970/top 1418 → 640–760/top 1308 → 575–735/top 1168 → 815–995/top 1068 → 1240–1460/top 1068 |
| Reward | `level_13_extractor_0` at (1350, 996) |
| Shortcuts | Burst 575–735/top 1168 → the end: gap 505, rise 100. Rise Pompeii (top 1470) → the end: rise 402 |
| Swarm | None (ruling 1b++) |
| Par | 450 → 480 |

Unmoved: the Florence gear's ride (it now starts just inside the carved lock, 1900–2140, past the entry barrier at 1860–1880), the Drift and approach locks, the Middle 5290, the PreBoss 7100, the font 6970, the secret (4160, 1375), the Mirror's arena from 7200, every gravity field, both rift pockets, the level's width, baselines and every manifest pool.

### Level 14, the Severed Bastion

| Piece | Where |
|---|---|
| Carve | `BreachGalleryLockMaxX` 2900 → 2000, so the Breach Gallery is 320–2000. ID and table unchanged |
| Moved | Wave 1's east slasher 2450 → 1660 (between the first two perimeter beams). `level_14_extractor_0` (a cradle intake valve) from the portal scaffold (1120, 1230) to the route's end (2800, 932) |
| Stretch `level_14.breach_catwalk` | 2020–2900, room `neo_earth_breach_catwalk`, key `neo_earth_room_breach_catwalk` ("The Breach Catwalk"), camera 0–2900 |
| Floor | No trench (deviation 3). The deck is the low road, timed through two beams: the perimeter grid's third (2160, unmoved) and the catwalk's own `level_14.breach_beam` (2610, the perimeter grid's 1.4 / 0.9 / 1.9 s, 16 damage, knockback (220, −120)) |
| Line | Deck 1700 → one-way 2060–2240/top 1470 → crumbling 2300–2460/top 1406 → one-way 2520–2700/top 1406 → solid 2740–2880/top 1406 → deck. Every step over a beam stands above its strip, which rises 220 px to y 1480 |
| Route `level_14.gantry_line` | One-way 2060–2240 → 2270–2390/top 1284 → 2045–2195/top 1104 → 2275–2455/top 1004 → 2700–2900/top 1004 |
| Reward | `level_14_extractor_0` at (2800, 932) |
| Shortcuts | Burst 2045–2195/top 1104 → the end: gap 505, rise 100. Rise 2740–2880/top 1406 → the end: rise 402 |
| Swarm | None (ruling 1b++) |
| Par | 540 → 570 |

The perimeter grid is still paused while the Breach Gallery's lock holds; its third beam now stands under the catwalk, so it pauses there too while the hero is shut in the gallery (deviation 4). Unmoved: the portal scaffold and the secret on it (860, 1355), the Containment Wing from 2920, the bulkhead, the pockets, the Middle 6350, the PreBoss 9220, the font 9850, the arena from 10200, width, baselines and pools.

### Level 15, the Meridian Founding

| Piece | Where |
|---|---|
| Carve | `PorticoLockMaxX` 3000 → 2000, so the Portico is 320–2000. ID and table unchanged apart from the swarm row |
| Moved | Wave 1's east trooper 2560 → 1650, clear of the portico fire (1390–1510). `level_15_extractor_0` (an anchor pylon) from the portico stair's top rung (1740, 1100) to the route's end (2830, 738) |
| Stretch `level_15.portico_run` | 2020–3000, room `alexandria_portico_run`, key `alexandria_room_portico_run` ("The Portico Run"), camera 0–3000 |
| Trench | Floor y 1520, ramps 2060→2220 and 2800→2960 |
| Line | Deck 1400 → solid 2070–2250/top 1282 → crumbling 2310–2470/top 1212 → one-way 2530–2690/top 1212 → solid 2750–2910/top 1212 → deck 1400. No wall step |
| Off the line | One-way 2520–2720/top 1360; vent `level_15.ember_vent` at 2440 on the trench floor, with a mantlet (1.0 / 0.6 / 2.6 s, 18 damage, the pilot's lane) |
| Route `level_15.reading_gallery` | Solid 2070–2250 → 2230–2350/top 1090 → 2055–2215/top 910 → 2295–2475/top 810 → 2720–2940/top 810 |
| Reward | `level_15_extractor_0` at (2830, 738) |
| Shortcuts | Burst 2055–2215/top 910 → the end: gap 505, rise 100. Rise 2750–2910/top 1212 → the end: rise 402 |
| Swarm | `level_15.swarm1` on the carved Portico: 8 on Normal and Hard, 5 on Easy (the lock is 1680 wide, under the 2800 that takes 10). Spawns 560, 700, 840, 1000, 1200, 1600, 1760, 1900 on the floor |
| Warm pool | `standard_enemy` 18 → 24. With `elite_enemy` 6 that is exactly the 30 cap |
| Par | 540 → 570 |

**Neither escape rule moved.** The run sits at 2020–3000; the firestorm starts on the Collapsing Hall lock's clear (6420–7900) behind the hero and completes past `EscapeFinishX` 9100. Nothing was added east of 3000, so no real-frames escape test was needed. Unmoved: the portico stair and fire, the Stacks from 3020, the secret (4790, 1055), the hall lock, the escape, the Middle, the PreBoss 9180, the font 9560, the arena from 9750, width, baselines and pools.

### Act III rules

- **Anchor Snap.** Every stretch, route and the new Level 13 room are built in `BuildLevel`, so the Snap's in-place rebuild builds them again. No state of them is saved.
- **Warden Beacons and every anchor live on Hard:** untouched; no anchor moved.
- **Resonance Hold nodes:** the three moved rewards are the existing drain stand-ins, so the F01 denominator, drain weight and F05 share are unchanged; each now stands 72 px over its route's end, as Extractors stand.

## API

- `Level13Controller`: `FragmentStairStretchID`, `DriftLineRouteID`, `FragmentStairRoomKey`, `FragmentStairMinX`, `FragmentStairMaxX`, `FragmentStairWestDeckEndX`, `FragmentStairEastDeckStartX`, `FragmentStairCameraWestX`, `DriftLineEndX`, `DriftLineTopY`; private `BuildFragmentStair`, `MotifSurface`.
- `Level14Controller`: `BreachCatwalkStretchID`, `GantryRouteID`, `BreachCatwalkRoomKey`, `BreachCatwalkMinX`, `BreachCatwalkMaxX`, `BreachCatwalkWestDeckEndX`, `BreachCatwalkEastDeckStartX`, `BreachCatwalkFirstTopY`, `BreachBeamID`, `BreachBeamX`, `GantryLineEndX`, `GantryLineTopY`; private `BuildBreachCatwalk`.
- `Level15Controller`: `PorticoRunStretchID`, `ReadingGalleryRouteID`, `PorticoRunRoomKey`, `PorticoRunMinX`, `PorticoRunMaxX`, `PorticoRunWestDeckEndX`, `PorticoRunEastDeckStartX`, `ReadingGalleryEndX`, `ReadingGalleryTopY`, `SwarmRowID`, `SwarmSpawnXs`; private `BuildPorticoRun`.
- `TraversalContentKit.LevelsWithTraversal` gains 13, 14 and 15 (expect a merge conflict on this one line).
- `en.csv`: block `# GR-LV5` with `void_room_fragment_stair`, `neo_earth_room_breach_catwalk`, `alexandria_room_portico_run`. **They need compiling** (`--import`); I compiled them locally and did not commit the translation.

## Measured: bot probes

`play`, seed 0, Joan and Lincoln, tags `lv5e` (Easy) and `lv5n` (Normal), `--trace-every=3 --nav-dump=1`, all six runs of a batch in one lock hold. **The bot crossed every stretch in every run.** Stretch seconds count from the first to the last trace sample inside the span on Easy; on Normal an Anchor Snap rebuilds the level and puts the hero back on an anchor, so that figure spans reloads and means nothing.

| Run (Easy) | Stretch | How | Level |
|---|---|---|---|
| L13 Joan | 4.8 s | Walked the shelf (never on the stair) | Complete, 105 s, 0 deaths, rank A |
| L13 Lincoln | **36.7 s, 1 death in the stretch** | Walked the shelf into the Threshold rift pocket, see below | Complete, 220 s, 3 deaths, rank C |
| L14 Joan | 2.9 s | Walked the deck through both beams, no damage | Complete, 127 s, 1 death (Forge Sentinel) |
| L14 Lincoln | 6.2 s | Walked the deck through both beams, no damage | Complete, 143 s, 0 deaths |
| L15 Joan | 2.6 s | Took the line, and also reached the trench floor (y 1520) and left it | Complete, 133 s, 1 death (the First Severed) |
| L15 Lincoln | 3.6 s | Stayed on the line the whole way (y 1081–1328) | Complete, 130 s, 1 death (the First Severed) |

| Run (Normal) | Level | Deaths, by where and what |
|---|---|---|
| L13 Joan | Smothered (Game Over), 544 s, 13 deaths | 8 in the Mirror's arena; 4 in the approach lock (Eraser 2, elite 2); 1 phantom |
| L13 Lincoln | **Complete**, 272 s, 5 deaths | 1 in the stretch and 2 in the locks to the rift snap loop; 1 phantom; 1 in the Mirror's arena |
| L14 Joan | Smothered, 589 s, 14 deaths | 9 to the Forge Sentinel; 5 in the locks (Eraser 2, enforcer, slasher, drone) |
| L14 Lincoln | Timeout, 1343 s, 12 deaths | 6 to the Forge Sentinel; 6 in the locks |
| L15 Joan | Smothered, 418 s, 13 deaths | 10 to the First Severed; 3 in the locks |
| L15 Lincoln | Smothered, 518 s, 12 deaths | 12 to the First Severed |

**No death on Normal fell in a stretch except Lincoln's rift snaps on Level 13.** The swarm (Level 15) spawned and cleared in both Easy runs with no damage to the hero (536 and 215 frames). On Easy the Portico's prefix fields two troopers, so the swarm comes on engage. No bot took a route or struck a moved reward (bots use no shortcuts).

**What the probes show about the open questions (reported, not tuned):**
- **Levels 14 and 15 on Normal:** the deaths are the bosses. 21 of 26 on Level 14 and 22 of 25 on Level 15 are in the arena, to the Forge Sentinel and the First Severed. The new stretches account for none.
- **Level 13's Mirror:** Joan died 8 times in the Mirror's arena and was Smothered; Lincoln beat it on his first try. One run a hero is no sample. The Mirror's hits are logged as `env`, because the report attributes damage to the nearest live enemy and the clone is not one; read the arena's x, not the label.
- **Level 13's rift pockets kill a walking Lincoln, a defect older than this work.** A pocket is a circle of radius 120 at y 1840; on the shelf a hero overlaps it for about 273 px of travel. At half speed Lincoln makes 114 px/s, so he needs about 2.4 s to cross, and the snap fires at 2.0 s (`SnapDelay`): he is put back 3 s into his past with 15 damage, walks in again and loops until he dies. Before the 2026-10-12 speed cut he made 142.5 px/s, about 1.9 s, just inside. In these runs it killed him at the Threshold pocket (1500, now under the Fragment Stair), at the Drift's (4520) and at the approach's (6250), which this work did not touch. Joan (186 px/s at half speed) crosses in about 1.5 s. A person can jump over it, or take the stair, which is exactly the line over it. I did not change the pocket: it is a hazard number in a level whose tuning waits for the session. **A ruling is owed** (a smaller radius, a longer delay, or accept that Lincoln must jump), and `Level13ContentTests.TheRiftPocketsTaxTheLowRoadWithTheAuthoredTimeLoopSnap` does not catch it.

## Provisional numbers (`PROVISIONAL-TRAVERSAL-RULES`)

The user ruled only the 280 / 240 limits, the swarm row's trigger and sizes, and the +30 minimum.
- Every platform position and width, the Level 15 trench's depth 120 and ramps 160, every route's shape (copied from the pilot's: rises 192 / 110–180 / 100, the 245 last hop, burst 505 / 100, rise 402).
- Level 13's lock edge 1880 and the stair's camera −60–1860. Level 14's lock edge 2000, the first step's top 1470 (10 px over a beam strip), the catwalk beam at 2610 with the perimeter grid's tuning and damage. Level 15's lock edge 2000.
- The Level 15 ember vent's 1.0 / 0.6 / 2.6 s and 18 damage (the pilot's lane).
- Swarm spawn positions; the three moved spawns (L13 2100, L14 1660, L15 1650).
- Room names "The Fragment Stair", "The Breach Catwalk", "The Portico Run".

## Tests

**Declared +9, actual +9.**

| Suite | Cases | What |
|---|---|---|
| `Level13TraversalStretchTests` (new) | 3 | The carve and what did not move; Lincoln walks off the stair into the Threshold in real frames and it engages only inside; Lincoln's longest Drift-line hop in real frames |
| `Level14TraversalStretchTests` (new) | 3 | The carve and what did not move; Lincoln double-jumps from the deck onto the first step (230 px) in real frames and a firing beam under it misses him, while on the deck it hits; the longest Gantry-line hop |
| `Level15TraversalStretchTests` (new) | 3 | The carve and that the run is west of the escape; Lincoln walks into and out of the trench; the Portico's swarm comes at two alive and a fodder kill pays nothing, with no firestorm |

Changed in place, no count change:
- `Level13ContentTests`: the Threshold's layout 320 → 1880.
- `Level14ContentTests`: the Breach Gallery's layout 2900 → 2000; the grid case lets the perimeter grid's third beam stand under the catwalk.
- `Level15ContentTests`: the Portico's layout 3000 → 2000 with the swarm row as its second wave (read through a table wave 5 built in the test, since the swarm has no authored table wave).
- `TraversalContentKit.LevelsWithTraversal` gains 13, 14, 15.

Last filtered run, 19 suite filters (the six level suites, the traversal and reach sweeps, `SwarmWaveContentTests`, `EncounterLockContentTests`, `EncounterLockObjectiveContentTests`, `AnchorApproachContentTests`, `ArenaGeometryContentTests`, `SecretCachePlacementTests`, `ScenePoolConfigTests`, `DustEconomyTests`, `RewardManifestTests`, `EncounterBaselineTests`, `CampaignLocalizationTests`), with the keys compiled locally: `Passed! - Failed: 0, Passed: 188, Skipped: 0, Total: 188`, which is the sum of those suites' `[TestCase]` counts. No full run: the orchestrator owns those.

**Seen failing.** The new suites cannot compile against the old code, so I mutated the build and reverted: the Level 14 step at top 1500 (inside the strip), the Threshold's area back to 1500 under the stair, Level 15 without its swarm wave and without its trench. Run: `Failed: 6, Passed: 3, Total: 9`: every case except the three route-hop flights, which have no route to fly on the old code. The first real run also caught a bug in my own climb test (it sent a fresh Jump press every frame).

## Effects

No save field, schema, protocol, component or Fighter hash change. Nothing under `scripts/FighterSim/` changed; `StoryLevelControllerBase.cs` and `.Stretches.cs` are untouched. F05 is exact: the swarm's fodder weighs 0, pools unchanged, and the sweep sums every ledger on all three difficulties. Level 15's warm combatants are 24 + 6 = 30, the cap.

## Deviations from the plan and dossier

1. **Level 13 is carved from the west.** An east carve would have put the Threshold's exit barrier through the Florence gear's ride (x 1900–2140) and the gear's board-from Berlin slab. The Level 7 precedent (GR-LV3) carves west too.
2. **Level 13's Threshold is 1320 wide, under the plan's 1,700–2,000 default** (the rule's range is 1,280–3,200). `Level13ContentTests` requires every room camera to be 1920 px wide, and a stair camera that held only 320–1480 could not be. At 1880 the stair's camera is −60–1860, exactly 1920, and the gear stays inside the lock.
3. **No trench on Levels 13 and 14.** Their decks stand 100 px over the level's floor edge (`ShelfY` 1900 of 2000, `DeckY` 1700 of 1800), and a trench needs 100–150 px, so its floor would sit at or under the camera's bottom. Instead the low road is the deck itself, made costly by the level's own hazard: Level 13's Threshold rift pocket, Level 14's two beams. Level 15 has the pilot's trench.
4. **The perimeter grid's third beam stands under the Level 14 catwalk** and is still paused while the Breach Gallery's lock holds (the grid pauses with its lock). The hero is shut in the gallery then, so nothing changes in play.
5. **Level 13's line is the existing stair**, registered as the line, plus the new route over it. Nothing new stands on the line itself; the stretch is what the carve freed from the fight.
6. **The routes' rewards moved out of their locks** to the routes' ends: each level's `extractor_0`. Their IDs and manifest rows did not change.
7. **In the void the shortcuts are shortcuts only on paper.** The Threshold field runs at 0.28–0.55 gravity, so every hero's base jump clears both of Level 13's shortcuts there. The reach tests measure at Earth-normal gravity, as the sweep does on every level.
8. **Level 15's swarm sits on the carved Portico (8), not the Stacks (10).** Either is allowed; the Portico is the lock the carve made, as on Levels 7 and 9, and it keeps the swarm off the scriptorium climb.

## Did not ship

- No bot took a route or struck a moved reward; no bot stood on the Level 13 stair or the Level 14 catwalk (they walked the low road). The line and routes are proven by the reach sweeps and the real-frames cases only.
- No real Anchor Snap test. The stretches and routes are built in `BuildLevel`, which a Snap reruns; I did not drive one in a test.
- No art: the steps take the levels' palettes.
- Par is the authored +30, not measured.
- The rift-pocket defect above is reported, not fixed.

## What the docs owe (GR-DOCS)

- Ledger: `PROVISIONAL-TRAVERSAL-RULES` (the list above); `VERIFY-PAR-SECONDS` (Levels 13 480, 14 570, 15 570); **a new `VERIFY-L13-RIFT-SNAP-LINCOLN` row** (the walk-through snap loop; acceptance: a ruling, then Lincoln walks each pocket in real frames without a snap, or the ruling accepts it); the Level 15 swarm row under `USER-SWARM-WAVES`.
- AGENTS.md: the three locks' new ranges (Level 13 Threshold 1880–3200, Level 14 Breach Gallery 320–2000, Level 15 Portico 320–2000), the moved `extractor_0` on each, the new room `void_threshold_lock`, the catwalk beam `level_14.breach_beam`, the new suites.
- Nothing for the design master beyond what the traversal rows already owe.

## For the orchestrator

- **`en.csv` keys to compile:** `void_room_fragment_stair`, `neo_earth_room_breach_catwalk`, `alexandria_room_portico_run`. Until you do, `CampaignLocalizationTests` and the level suites' key checks fail on this branch.
- **Expected conflicts:** `TraversalContentKit.LevelsWithTraversal` (one line; the union is `{ 1, 2, 3, …, 15 }` as the other branches add theirs) and `en.csv`'s tail.
- A Godot launch here churned `.import` files under `audio/`, `addons/` and `localization/` and `resources/Audio/default_bus_layout.tres`; I discarded them.
