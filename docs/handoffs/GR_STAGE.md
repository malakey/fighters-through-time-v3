# GR-STAGE: Globe and Hakata Seawall become Open stages (gameplay review 2026-11-03, 9a / 9a+ / 9a++)

Branch `gr/stage`. **Nobody has played, looked at or balanced any of it.** Every acceptance claim is a headless suite, a scene smoke run or a model; the pit widths are the user's ruling (3.0 on both) but everything else is an agent choice (`PROVISIONAL-OPEN-STAGE-GEOMETRY`). The blast-zone KO share on the two stages is unmeasured: the matrix tool (GR-CPU, 8b) measures it afterwards.

## What shipped

| Stage | Before | After |
|---|---|---|
| Globe Theatre (`globe_theatre`) | Sealed, no floor segments | Open: a centred 3.0 pit [-1.5, 1.5]. `FloorSegments` = centre -5.25 / half 3.75 and centre +5.25 / half 3.75 (floor [-9, -1.5] and [1.5, 9]). `RespawnPlatformPosition` (-4, 3), as Paris. Two true ledges, anchors `(3 + 0) * 2 + 1 = 7` and `(3 + 1) * 2 + 0 = 8` (the stage has three platforms). |
| Hakata Seawall (`berlin_wall`, ID retained) | Sealed | Open: a one-sided 3.0 gap [7, 10] against the east wall. `FloorSegments` = centre -1.5 / half 8.5 (floor [-10, 7]). East balcony platform and its orb 6.5 -> 5.5 (balcony spans 4.2..6.8, short of the gap). Default respawn platform (centre, 3) kept: the centre is solid. One true ledge, anchor `(2 + 0) * 2 + 1 = 5`. |

Unchanged on purpose: bottom blast zone -5, spawns +-4, hazard anchors (Globe -5, 0, 5; Hakata -4, 0, 4), the Globe canopy platform (0, 4.0) and orb (0, 4.5) (both now hang over the pit), pools, audio, hazard controllers, previews (placeholder plates).

Files:
- `scripts/FighterSim/FighterStageGeometry.cs`: `Berlin` and `Globe` gain `floorSegments` (Globe also `respawnPlatformPosition`); Berlin's east platform and orb move; doc comments. No new field, no component, no snapshot change.
- `scenes/fighter/FighterStage_Globe.tscn`: the single `Geometry/Ground` is split into `GroundLeft` (x 621.875 px) and `GroundRight` (x 1278.125 px), each a 468.75 px solid body with its own `TopLine`, `PlankSeam`, `LedgeCue` and `LedgeUndercut`; a `Presentation/CentrePit` ColorRect over the gap (856.25..1043.75 px, opening down from y 700).
- `scenes/fighter/FighterStage_Berlin.tscn`: `Geometry/Ground` shortened to centre 856.25 px / 1062.5 px wide, with a `LedgeCue` and `LedgeUndercut` at its east end; `GuardTowerEast` and the `EastBalcony` orb marker 1356.25 -> 1293.75 px; `Presentation/SeawallGap` and `SeawallGapSwell` over the washed-out span (1387.5..1575 px). Visuals are code-free ColorRect/Polygon2D placeholders in the Paris/Nassau idiom.
- `resources/FighterStages/stage_catalog.tres`: `IsOpenStage = true` on the Berlin and Globe rows (the stage select and Holodeck badge them Open; `FighterStageData` reads it).
- `localization/en.csv`: **two existing values rewritten in place** (`stage_berlin_layout`, `stage_globe_layout`) because the old copy ("An open wooden stage with tiered side galleries") no longer described the floor. No key added or removed. The compiled `localization/en.en.translation` is **not** committed: it needs `--headless --import` in `gr-integ`, by the orchestrator.

## Consumers audited (no code change needed)

Everything that reads the floor goes through `HasFloorSupport` / `FloorSegments` / `IsOpenStage`, so nothing treats Globe or Berlin as flat in production code: ledge capture and the anchor encoding (`TryFindLedge`, `TryGetHangPosition`, `TryGetStandPosition`; segment order is left-to-right so anchors are stable), `FighterCpuController` (`HasFloorSegments`, nearest floor edge, recovery detection), the hazard and orb anchors (X-only conformance), `FighterMatchFlowRules` respawn, `FighterStageConformance` (matches scene floor bodies to segments by centre and half-width), stage select and the Holodeck (the Open badge), the Mirror adapter (no stage), rollback sweeps (generic over `AllAuthored`). The Globe heckle has no damaging region, so its anchor at x = 0 hanging over the pit leaves no residue (Vesuvius's case was a ground residue pool).

## Tests

**Declared +25, actual +25** (verified from the diff: 25 added `[TestCase]` lines against the base `a8866cac`, none removed; no full-suite run was done here, the orchestrator owns those).

New:
- `FighterStageGlobeTests` +9: open centred 3.0 pit and floor support, spawns / galleries / respawn over floor, the two ledges and anchor encoding, scene floor bodies, painted pit and ledge cues, canopy and orb over the pit with the centre heckler anchor kept, walking into the pit (a stock, respawn at (-4, 3), drop to the left floor), ledge catch and climb back, two-simulation hash identity through a fall.
- `FighterStageBerlinTests` +10: one-sided gap, east balcony and orb moved with the west side untouched, spawns / anchors / respawn over floor, the one ledge, scene floor body ending at the gap, painted gap and cue, walking off the east end (a stock, respawn at centre), ledge catch and climb back, the west half unbroken, two-simulation hash identity through a fall.
- `CpuRecoveryMatrixTests` +6: Easy / Medium / Hard on Globe (drop at -1.0, 0.5 inside the left ledge) and on Hakata (drop at +8.0, 1.0 past the ledge). Each also runs the Sealed-stage false-trigger check and the climb-jump-only ledge exit check on the new geometry.

Changed in place (0 delta): `FighterStageGeometryTests` (dossier rows for both stages, pit drills 36 -> 63 in two places, Open count 3 -> 5), `FighterMatchFlowTests` (Open stages 3 -> 5 in two sweeps, Sealed stages 7 -> 5), `FighterStageCatalogTests` (pins the five Open IDs and Florence Sealed, in the existing case), `FighterStageGlobeTests` (solid body count 3 -> 4; the pacing input now walks west first, since a 40-frame run east from x = -4 ends in the pit), `FighterStageBerlinTests` (the hazard determinism run raised to 25 stocks: its jittering input drifts east into the gap and ended the match before the searchlight's cycle, which the test's own assertion caught; header comment), `FighterHazardBehaviorTests` (the Globe heckle test paces west first, same reason).

Fail-first: with the pre-change `FighterStageGeometry.cs` restored (scenes and tests kept), 31 of 114 cases in the touched suites failed, including all six matrix cases and every behavioural new case. Cases that cannot fail on old geometry are scene- or catalog-shaped only.

Filtered runs on the final tree (all `Failed: 0`):
- `FighterStageGlobe|FighterStageBerlin|FighterStageCatalog|FighterStageConformance|FighterStageGeometry`: Total 62 (before the two fixes above: 2 failed, both mine and fixed).
- `FighterStageBerlin|FighterMatchFlow|FighterHazardBehavior|FighterLedge`: Total 98.
- `CpuRecoveryMatrix|CpuRecoveryProfile`: Total 26.
- `FighterCpuSpecialTimingSmoke|RollbackReadiness`: Total 24 (they sweep every authored stage).
- `FighterStage*|CharacterSelect*|HolodeckConsole|TranslationCsvShape|ScenePoolConfig|FighterRoute`: Total 195.
- Headless scene smoke of both scenes (`--quit-after 120`): exit 0, no script or parse error (the usual "N resources still in use at exit" noise only).

## Hash and save effects

Every Fighter hash on `globe_theatre` and `berlin_wall` moves (floor, respawn anchor, Berlin's platform and orb anchors); no suite pins an absolute hash. No save schema (v8), protocol (v5), Klotho component or snapshot field changed; no component ID allocated. Seeded CPU outcomes on those two stages move, as do any bot or matrix numbers taken on them.

## Deviations and provisional values

No deviation from the plan. Provisional (agent-chosen, `PROVISIONAL-OPEN-STAGE-GEOMETRY`): the matrix drop points (-1.0 and +8.0); the painted pit and gap colours, the ledge-cue colours and the extra placeholder nodes; keeping the Globe canopy platform and orb over the pit; keeping the Globe heckler anchor at x = 0; Berlin's east balcony at 5.5 (the plan allowed "keep it as a lip island", which would have put it over the gap).

## What did not ship

Preview plates (still the placeholder SVGs, as ruled); production art for the pits; the blast-zone KO-share measurement (GR-CPU's tool, afterwards); any tuning of Hakata's hazard (the Fleet Lantern anchors are over floor, untouched). No headless proof exists that a human reads the Globe pit against the canopy platform and orb above it, or the Hakata gap beside the 24 px wall; `VERIFY-GAMEPLAY-REVIEW-HANDS-ON` covers it.

## What the docs owe (GR-DOCS)

- AGENTS.md: "Three of the ten stages are **Open** (Paris, Vesuvius, Nassau)" -> five (add Globe, Hakata); the Open/Sealed stage bullet ("**Three stages are Open**", "the seven Sealed stages", the Package 11 A9 text, the `FloorSegments` description), the repository map line for `scenes/fighter/`, the baseline arithmetic (+25), and the footgun note on walking across `FloorSegments` (two more stages need it; `HazardHarness.StepTowardOnFloor` already clamps).
- CLAUDE.md: the verified baseline; "(Paris/Vesuvius/Nassau are the three Open ones)" in the useful-scene-targets line.
- Ledger: `USER-OPEN-GLOBE-HAKATA` (rulings 9a / 9a+ / 9a++, closes with the matrix measurement) and `PROVISIONAL-OPEN-STAGE-GEOMETRY` (the values above); `VERIFY-PARIS-DEEP-PIT-RECOVERY` is the neighbour (both new pits are 3.0 wide, narrower than Paris's 5.0).
- Design master and mirror: the Open stages "three of ten" lines; the stage dossier rows for the Globe and Hakata; `docs/design-contracts` stage text if any states the Sealed set (`CPU_RECOVERY.md` says "every authored Open stage").
- `docs/PACKAGE6_FIGHTER_PLAN.md` / `PACKAGE11` history needs no edit (history), but a dated pointer is reasonable.
- `localization/en.csv` copy: the two rewritten layout values are a user-facing copy question (`VERIFY-P13-AUTHORED-COPY` style); the orchestrator must run `--import` and commit the translation.

## Next workstream / merge notes

No `en.csv` key was added (two values edited, so the translation must be recompiled). Expect a textual conflict only if another workstream edits the same two `en.csv` lines, `FighterStageGeometryTests`' dossier or `FighterMatchFlowTests`' stage counts (GR-CPU's matrix-tool move touches `CpuRecoveryMatrixTests`' neighbours, not these lines). Any test added later that walks a fighter across Globe or Hakata must respect `FloorSegments` (see AGENTS.md's footgun note).
