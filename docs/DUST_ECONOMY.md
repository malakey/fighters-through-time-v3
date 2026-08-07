# Chronal Dust economy model (Package 3 balance pass)

Authored 2026-08-07. This document is the campaign progression model behind the authored
dust numbers in `resources/Enemies/*.tres`, `resources/Bosses/*.tres`,
`resources/Resonance/*_grid.tres`, and `scripts/Environment/ChronalExtractor.cs`.
`tests/ContentValidation/DustEconomyTests.cs` asserts the invariants below — update the
tests and this document together when retuning.

Authority: `design-godot.md` Section 2 ("Currency: Chronal Dust", "Node Costs",
"Preliminary Economy Estimate"). The design marks the drop rates as "baseline
placeholders ... balanced and fine-tuned in a later phase of development" and defers
final economy tuning to the balance phase; this pass is that phase. Explicit design
numbers were kept wherever they form a coherent curve; the two deviations are flagged
below.

## 1. Authored reward and cost values

| Source | Design value | Authored value | Status |
|---|---|---|---|
| Standard mob kill | 1–2 | 1 (`hologram_drone`), 2 (`chrono_slasher`, `cyber_guard`) | Kept |
| Elite mob kill | 20 | **10** (`steam_automaton`, `tech_enforcer`) | **Tuned — see conflict C1** |
| Level boss | 50 | 50 (`BossData.ChronalDustDrop`, now resource-authored) | Kept |
| Chronal Extractor | 25 | **15** (`ChronalExtractor.DustReward`) | **Tuned — see conflict C2** |
| Tier 1 minor node | 50 | 50 (all nine grids) | Kept |
| Tier 2 minor node | 75 | 75 (all nine grids) | Kept |
| Tier 3 major perk node | 200 | 200 (all nine grids) | Kept |
| Full 9-node grid | 975 | 975 = 3×50 + 3×75 + 3×200 | Kept |
| Timeline Collapse penalty | 20% of carried dust | 20% (`StoryManager.CalculateTimelineCollapseDust`) | Kept, out of scope |

Dust rewards are **not** difficulty-scaled per kill. Difficulty affects income only
through `StoryDifficultyTuning.ScaleEncounterCount` (mob spawn counts ×0.7 Easy /
×1.0 Normal / ×1.25 Hard). Boss and extractor income is identical on all difficulties.

## 2. Campaign progression model (levels 0–15, Normal authored counts)

Placeholder count assumptions (only level 1 Florence is authored today; all other
counts are this model's placeholders for future level authoring):

- **S** standard mobs: ramp 8→14 across the campaign; Florence's authored 13
  (7 `chrono_slasher` + 6 `cyber_guard`) is the fixed point. Model uses 1.5 dust per
  standard kill (midpoint of the 1–2 design range); Florence uses its exact 26.
- **E** elites: rare early, concentrated late; Florence's authored 3 `steam_automaton`
  is the fixed point.
- **B** boss: every level has one campaign boss except the tutorial (0) and the
  Chronal Void transition (13) — placeholder assumption for 13.
- **X** extractors: design's "3 to 4 hidden per side-scrolling level"; 4 on the act
  finales, 2 in the Void, 0 in the tutorial. Florence's 3 are **not yet authored**
  in `Level01Controller` — model target, flagged as a level-authoring gap.

Full = 1.5·S + 10·E + 50·B + 15·X (Florence: 26 + 30 + 50 + 45).
Expected = 90% standard clears + all elites/bosses + **50% extractor discovery**
(they are hidden), rounded.

| Lvl | Era | S | E | B | X | Full | Expected | Cum. expected |
|---|---|---|---|---|---|---|---|---|
| 0 | Tutorial | 4 | 0 | 0 | 0 | 6 | 5 | 5 |
| 1 | Florence | 13 | 3 | 1 | 3 | 151 | 126 | 131 |
| 2 | Orléans | 8 | 0 | 1 | 3 | 107 | 83 | 214 |
| 3 | Chicago | 8 | 0 | 1 | 3 | 107 | 83 | 297 |
| 4 | Paris | 10 | 0 | 1 | 3 | 110 | 86 | 383 |
| 5 | Titanic (Act I finale) | 12 | 1 | 1 | 4 | 138 | 106 | 489 |
| 6 | Pompeii | 10 | 0 | 1 | 3 | 110 | 86 | 575 |
| 7 | Nassau | 10 | 1 | 1 | 3 | 120 | 96 | 671 |
| 8 | Alexandria 30 BC | 10 | 0 | 1 | 3 | 110 | 86 | 757 |
| 9 | Berlin | 12 | 1 | 1 | 3 | 123 | 99 | 856 |
| 10 | London | 10 | 0 | 1 | 3 | 110 | 86 | 942 |
| 11 | Gettysburg | 12 | 1 | 1 | 3 | 123 | 99 | 1041 |
| 12 | Lunar (Act II finale) | 14 | 2 | 1 | 4 | 151 | 119 | 1160 |
| 13 | Chronal Void | 8 | 1 | 0 | 2 | 52 | 36 | 1196 |
| 14 | Neo-Earth | 14 | 2 | 1 | 3 | 136 | 111 | 1307 |
| 15 | Library of Alexandria | 12 | 2 | 1 | 3 | 133 | 109 | 1416 |
| **Total** | | **177** | **14** | **14** | **46** | **1787** | **1416** | |

Component check (full collection, Normal): standards 257 + elites 140 + bosses 700 +
extractors 690 = **1,787**.

### Per-difficulty totals

Mob income (standards + elites) scales with spawn count; bosses (700) and extractors
(full 690 / expected 345) do not.

| Difficulty | Full collection | Expected playthrough |
|---|---|---|
| Easy (×0.7 mobs) | 700 + 690 + 278 = **1,668** | 700 + 345 + 260 = **1,305** |
| Normal | 700 + 690 + 397 = **1,787** | 700 + 345 + 371 = **1,416** |
| Hard (×1.25 mobs) | 700 + 690 + 496 = **1,886** | 700 + 345 + 464 = **1,509** |

All land inside (Hard perfect play: slightly above) the design's preliminary
1,200–1,800 estimate. Real playthroughs also lose dust to the 20% Timeline Collapse
penalty and the 50% quit-to-menu penalty, pulling actual totals a further ~5–10% down.

## 3. Pacing milestones (Normal, expected collection)

- **First major perk (Act I finale target):** a branch beeline costs 50+75+200 = 325 —
  crossed during level 4, so a focused player buys their first major at the hub after
  Paris/Titanic. The typical spread path (all three 50s, then the 75s, then a major:
  575 total) lands the first major after level 6–7. Both bracket the Act I finale.
- **Mid-campaign choice pressure (levels 6–9):** cumulative 575–856 buys the six minor
  nodes (375) plus one, at most two, majors — never all three.
- **Full grid (975):** crossed during level 11 on Normal (level 12 Easy, level 10
  Hard); with collapse/quit friction the ninth node realistically lands at levels
  12–13 of 15. Perfect-play Normal reaches it after level 10.
- **End-of-campaign surplus:** expected 441 (full 812) — matches the design's "one
  full tree per playthrough with surplus".

The boss backbone (50 × 14 = 700 of the ~1,400 expected income) puts a hard floor of
~72 dust per boss level, which is why the full-grid crossing cannot land later than
level 11–12 without either raising major costs above the designed 200 or ramping boss
rewards — both rejected as conflicts with explicit design numbers.

## 4. Design conflicts flagged (AGENTS.md rule: surface, do not silently normalize)

- **C1 — Elite reward 20 → 10.** With `design-godot.md`'s per-source rates (elite 20,
  extractor 25, 3–4 extractors/level, boss 50), a full campaign yields ~2,800+ dust —
  far above the design's own 1,200–1,800 estimate, funding the whole grid by Act I's
  end and destroying choice pressure. The design's balance note explicitly defers
  these rates to this phase; elite 10 (5× a standard kill) restores the budget. Note
  the design was already internally inconsistent here: it lists elites as Large-sprite
  drops ("25+") while valuing them 20.
- **C2 — Extractor reward 25 → 15.** Same budget pressure, plus 25 contradicts the
  design's own sprite-tier table, which lists Chronal Extractors as Medium-sprite
  drops ("6 to 24 Dust"). 15 sits inside the Medium band as designed.
- **C3 — Hard perfect play (~1,886) slightly exceeds the 1,200–1,800 estimate.**
  Accepted: the estimate is explicitly preliminary, and expected-collection Hard
  (~1,509) is comfortably inside.
- **Gap — Florence extractors.** The design wants 3–4 hidden extractors per level;
  `Level01Controller` authors none yet. The model budgets 3 for Florence; authoring
  them is level-content work outside this balance pass.

## 5. Visual sprite tiers (resources/Drops/dust_visual_tiers.tres)

Small < 6 ≤ Medium < 25 ≤ Large. Under the authored values: standards (1–2) Small,
elites (10) and extractors (15) Medium, bosses (50) Large. `DustEconomyTests`
asserts each reward lands in its band.
