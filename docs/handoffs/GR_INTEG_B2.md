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
