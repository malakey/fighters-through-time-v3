# GR-INTEG-B2: Phase B2 integration (Levels 1-12)

Worktree `D:/Projects/ftt-wt/gr-integ`, branch `gr/integ`, from `160353c5`. Each merge `--no-ff`, in plan order. Every build: 0 errors, `CS8632` the only warning, no `KLOTHO_DET002`. Every full run exit 0, lock held and released, no truncated run.

| # | Branch | Merge SHA | Translation commit | Result line |
|---|---|---|---|---|
| 1 | gr/lv1 | 80c218e9 | 0368bf13 | Failed: 0, Passed: 4284, Total: 4284 |
| 2 | gr/lv2 | 9f5e6726 | aca10d06 | Failed: 0, Passed: 4297, Total: 4297 |
| 3 | gr/lv3 | 95455e57 | 3cb95115 | Failed: 0, Passed: 4314, Total: 4314 |
| 4 | gr/lv4 | 3a567453 | cfdd119c | Failed: 0, Passed: 4331, Total: 4331 |
| 5 | gr/lv5 | e8ed1003 | dfabc5e5 | Failed: 0, Passed: 4340, Total: 4340 |

Every total equals the plan table; no count change beyond the branches' own.

## Conflicts
- lv1: none.
- lv2, lv3, lv4: `localization/en.csv` tail hunk, union (GR-LV1, GR-LV2, GR-LV3, GR-LV4 blocks in order). `TraversalContentKit.LevelsWithTraversal`: union, ending `{ 1..12 }` (each branch listed 2 plus its own levels; the union is the sorted set of all).
- lv3: `BossPhaseRuleMatrixTests`: kept `vulcan_decimator` (Level 6, lv2) and added `dread_admiral` (L7) and `iron_chancellor` (L9).
- lv4: `BossController.cs` merged without conflict; its seam kept.
- lv5: `en.csv` tail (union, GR-LV5 block last) and `TraversalContentKit.LevelsWithTraversal`, resolved to `{ 1..15 }`. The three owed keys (`void_room_fragment_stair`, `neo_earth_room_breach_catwalk`, `alexandria_room_portico_run`) are in `en.csv`; the suite's translation checks pass.

## Translation
Regenerated with `--import` under the lock after each merge, committing only `localization/en.en.translation`. All eleven owed keys are in `en.csv`.

## GR-INTEG fixes
### `PROVISIONAL-L13-RIFT-SNAP-SLOWEST-HERO` (orchestrator's ruling, older than this work)
Level 13's three rift pockets (circle radius 120, 240 px, plus the 40 px hero body) halve the walker's speed, so a walking Lincoln (3.8 u/s = 228 px/s, 114 px/s in a pocket) needs about 2.46 s to cross, and the snap fired at 2.0 s: he looped until he died (since the 2026-10-12 speed cut). The rule that no level may require a specific hero was broken. Fix: one constant, `Level13Controller.RiftSnapDelaySeconds` = 3.0 s (margin about 0.54 s, rule at least 0.4 s), applied to all three pockets in `CollectAuthoredNodes`; the scene no longer authors `SnapDelay`. The pockets keep their purpose: standing or fighting in one for 3 s still snaps. New suite `Level13RiftPocketTests`: the delay clears (slowest hero read from the character data) crossing + 0.4 s, and Lincoln walks through each pocket in real frames with no snap. Both failed first on the 2.0 s code ("snap 2.00s, lincoln needs 2.46s + 0.4s"; "lincoln was snapped back in level_13.rift_threshold"). `Level13ContentTests` now compares with the constant. Suite +2.

## Measured par pass (plan 1.14)
Batch tag `grpar`: Levels 2-15, all nine heroes, `play` bot, Normal and Easy, seed 0, `--trace-every=6`, `-Parallel 12`, `-MaxSeconds 0`, run in this worktree under the lock (252 reports). A stretch's crossing time is the trace's first frame at or past the stretch's `MaxX` minus its first frame at or past `MinX` (0.1 s resolution). One seed means a hero's "median" is its single sample; the slowest hero is the one with the longest crossing. Rule: `par_new = ceil30(seeded par + max(30, 1.25 x T))`, upward only, never below the authored value.

| Level | Seeded par | Current par | Slowest hero, crossing s (Normal) | Heroes crossing | New par | Source |
|---|---|---|---|---|---|---|
| 2 | 360 | 390 | cleopatra 11.8 | 9/9 | 390 | Normal |
| 3 | 540 | 570 | shakespeare 11.7 | 9/9 | 570 | Normal |
| 4 | 360 | 390 | leonardo 6.6 | 9/9 | 390 | Normal |
| 5 | 270 | 300 | einstein 4.5 | 9/9 | 300 | Normal |
| 6 | 360 | 390 | lincoln 5.4 | 4/9 | 390 | Normal |
| 7 | 630 | 660 | lincoln 6.0 | 9/9 | 660 | Normal |
| 8 | 360 | 390 | lincoln 9.1 | 9/9 | 390 | Normal |
| 9 | 360 | 390 | shakespeare 7.5 | 9/9 | 390 | Normal |
| 10 | 360 | 390 | lincoln 3.8 | 9/9 | 390 | Normal |
| 11 | 360 | 390 | lincoln 18.4 | 9/9 | 390 | Normal |
| 12 | 540 | 570 | lincoln 6.8 | 9/9 | 570 | Normal |
| 13 | 450 | 480 | lincoln 7.9 | 9/9 | 480 | Normal |
| 14 | 540 | 570 | tubman 6.2 | 9/9 | 570 | Normal |
| 15 | 540 | 570 | lincoln 4.2 | 9/9 | 570 | Normal |

**Result: no par changes.** The slowest crossing on Normal is 18.4 s (Level 11, Lincoln), so 1.25 x T is at most 23 s, under the +30 minimum, and every level keeps `ceil30(seeded + 30)`. No controller or test changed. Easy gives at most 9.1 s, so it would not change anything either. Normal reached every stretch, so Normal is the source everywhere; Level 6 is the exception: 4 of 9 heroes on Normal and 3 of 9 on Easy crossed Ashfall (5680-6700), the rest `stuck_lock` at the vault lock before it (the known Level 6 vault bot gap), so Level 6's slowest hero may be unmeasured.

Suspicious, not par: outliers inflated by deaths in the stretch (Level 2 Cleopatra 11.8 s, Level 3 Shakespeare 11.7 s, Level 11 Lincoln 18.4 s, medians about 4 s); on Normal most bots end `collapse(Death)` after the stretch (Levels 4, 5, 9 for nearly every hero), which is combat difficulty, not the stretch; Level 7 Lincoln `stuck_lock:level_07_nassau_lock_quay` on Normal (the known quay stall); and on Level 10 Easy Cleopatra and Lincoln downed the boss but ended `collapse(Timer)` at about 1180 s, a post-boss stall rather than par (the other seven completed in 100-260 s). Reports and CSVs: `playtest/*grpar*` in the app user data; script `D:/Projects/ftt-scratch/par_analyze.py`.

## Final verification
Build: 0 errors, `CS8632` only. Full suite after Step 2 and the par pass: `Failed: 0, Passed: 4342, Total: 4342` (4340 + 2 from `Level13RiftPocketTests`), 2 m 55 s, lock held.

## Vulcan defeat retires outstanding eruption debris

Codex finding (major): `LowerVulcanEruption` only unfroze pieces, so debris already overhead kept falling and could damage the hero after the defeat. `FallingDebris.Retire()` (idempotent: resolves nothing, goes to the pool through `PoolManager.Release`, which defers in physics callbacks) is now called on every outstanding piece from `LowerVulcanEruption`, and the list is cleared. Other arenas checked: the Duke's steam, Admiral's cannonballs, Overseer gusts (cyclic hazards: `Enabled=false` returns before any damage), the General's searchlights (disabled, player removed) and the Siege Cannon (fences stop breaking; boss projectiles die with the boss) have no leftover-damage gap. New case `Level06VulcanArenaTests.APieceInTheAirWhenTheVulcanFallsNeverHitsTheHero` fails without the retire (hero HP drops); full suite 4343.
