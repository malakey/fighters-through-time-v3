# MM-DOCS-REPO handoff (meter and Defy update, Phase D)

Branch `mm/docsrepo` (worktree `D:/Projects/ftt-wt/mm-docsr`), cut from main `544ce84c` (every code phase merged). Documentation only: no code, no tests, no Godot launch. The design master, mirror and `docs/design-contracts/*.md` other than the ledger are MM-DOCS-DESIGN's (`docs/handoffs/MM_DOCS_DESIGN.md`).

## 1. Shipped

- **Ledger** `docs/design-contracts/DESIGN_BUILD_DEVIATIONS.md` (CRLF kept): the section "Confirmed open deviations — Momentum gauge, active Defy and meter rates, 2026-10-31" and the index "The 2026-10-31 meter and Defy update — index and design lines edited", between the compact-HUD index and "Historical leads — dispositioned by Package 11". **61 rows added**: 33 `USER-*` (each quoting its ruling verbatim with its round or ruling ID), 19 `PROVISIONAL-*`, 5 `VERIFY-*`, 3 `DEFER-*`, 1 `FIX-*`. **15 existing rows amended, 3 of them struck through** (the record stays): struck `USER-PER-HIT-MOMENTUM`, `USER-DEFY-ENDS-ON-OFFENCE`, `VERIFY-DEFY-CANCELS-OWN-ROLL`; amended `PROVISIONAL-HIT-CONFIRM-RULES`, `USER-ABILITY-LOCKOUT`, `USER-NOT-YET-FLASH`, `FIX-SIM-VENOM-DEFY-BARRED` and `FIX-CPU-DEFY-RESERVE-IN-SUDDEN-DEATH` (already struck; moot notes), `USER-STORY-METER-SCALE`, `PROVISIONAL-STORY-METER-SCALE-RULES`, `PROVISIONAL-STORY-METER-GAIN-SCOPE`, `FIX-GRID-SCOPED-COOLDOWN-INVERTED`, `USER-STORY-HUD-COMPACT`, `PROVISIONAL-STORY-HUD-COMPACT`, `USER-BOT-COMBAT-TELEMETRY`. The index lists the cross-referenced rows left as they are (`PROVISIONAL-ABILITY-LOCKOUT-RULES`, `USER-HIT-CONFIRM-CANCEL`, `USER-FREEZE-SPECIAL-BUFFER`, `PROVISIONAL-FREEZE-SPECIAL-BUFFER`) and that the design-line list is in `MM_DOCS_DESIGN.md`.
- **AGENTS.md**: Klotho inventory (305, 310, 312, 323 rows; 306 and the highest ID 323 in the intro paragraph; `KLSG_ECS004` gone); the lockout bullet (the D2 per-hit paragraph replaced by the Momentum gauge paragraph); the Fighter Special and FSM sentences; the Ultimate bullet (A02 against Defy); the D01-D04 bullet (D04 superseded, resolution order); two new bullets (the active Defy; the CPU and the Mirror clone); the Rally bullet; Sudden Death; the meter bullet (x0.5, flats, 1/2, the historical bot note); the state list (`Defying`); the Level 0 sentence; `KillPlayerNonHit`; the F10 level-start reset; Time Freeze's suspensions; the save-schema bullet (no derivation, dead Defy fields); the `InputManager` bullet (Defy, R3, the Dialogue Log on Back, the repair); the Story HUD and glass bullets (pips, Starved, notch, seal retired, 30 icons); protocol v5 and no free wire bit; the baseline paragraph with `<CLOSEOUT-TOTAL>` and `<CLOSEOUT-RUNS>`; and a new known-gaps subsection "The 2026-10-31 meter and Defy update".
- **CLAUDE.md**: the build-warning sentence (`KLSG_ECS004` gone, `CS8632` alone); the baseline paragraph (same placeholders); signature 8's spent-Defy note replaced by the new seams; the bot section (the `play` Defy policy with 1 foe, the `spam` sentence, the refusal labels `reuse_delay` and `no_momentum`, a new paragraph for the `defy` and `momentum` blocks, the CSV, `compare_runs.py`, `trace.py`, comparability and the `mmbefore*` / `mmafter*` pair); the capture names; the Where-to-start rows (Character ability, Ultimate, Fighter simulation, Fighter CPU, Saves, HUD, Input remapping, Defence/Defy).
- **docs/PERFORMANCE_BASELINE.md**: a "Re-measure, 2026-10-31 - owed at the closeout" section with MM-SIM's interim figures, marked not comparable; the official re-measure is owed.

## 2. Decisions

- The ledger rows quote the user's words from `DECISIONS_meter.md`; the file has no separate "build log" section (the brief mentioned one), so the three build facts were taken from the handoffs and the brief: the Fighter Defy ring fires on the blast frame (`ab532365`, `MM_INTEG_B1.md`), the grid minors are named "Minor Momentum" (MM-FIXB1), and the `play` bot needs one foe (`632c393b`; `PlaytestDefyPolicy.MinEnemiesNear` = 1).
- `FIX-SIM-DIRECT-HIT-CAST-ATTRIBUTION` is written open ("a fix is scheduled; the orchestrator closes this row after it lands"), merging MM-SWEEP's suggested `VERIFY-DIRECT-HIT-ATTRIBUTION-OVERLAP` into it; the four slots (Einstein S1 314, Leonardo S1 312, Tesla S2 314, Mozart S1 209 frames) are listed.
- `VERIFY-DEFY-MUTUAL` carries MM-SIM's correction of plan P5 (a later Defy catches the earlier one only from 9 ticks later).
- Ledger rows say "Unassigned" for the owner of every built row (the repository convention), and pin `544ce84c`.

## 3. Deviations from the brief

- The index's design-line column says "see `docs/handoffs/MM_DOCS_DESIGN.md`" (the brief allowed it); `MM_DOCS_DESIGN.md` did not exist in this worktree.
- `docs/PERFORMANCE_BASELINE.md` has no closeout figures, so it carries a marked-owed section rather than a table.

## 4. Test arithmetic (reconciled; no test was run)

3771 + RULES 7 = 3778; + INPUT 7 = 3785; + TEXT 3 = 3788; + FX 7 = 3795; + HUD 10 = 3805; + SMOM 17 = 3822; + SDEFY 12 = 3834; + SIM 17 = 3851 (equals the integration record's Phase B1 total); + FIXB1 9 = 3860; + BOTS 4 = 3864; + CPU 10 = 3874; + SWEEP (-1) = **3873** (equals MM-SWEEP's full run, `Failed: 0, Passed: 3873`). The sum of the twelve deltas is +102. Against the plan's +92 (3863) the differences are HUD +1 (the plan counted a second seal case the file lacked), SIM -1 (a passive-save case the plan did not list), FIXB1 +9 (not in the plan) and SWEEP +1 (the plan said -2; a review nit added one case). One caution: MM-CPU's handoff reads "3860 to 3870" and MM-BOTS "+4" separately; they were cut from `e8355b19` in parallel and merge to 3874 together, which the brief's per-phase figures and MM-SWEEP's 3873 confirm. No mismatch remains.

## 5. Placeholders left for the closeout

`<CLOSEOUT-TOTAL>` (twice in AGENTS.md, in the baseline paragraph and the `[TestCase]` count sentence, and once in CLAUDE.md) and `<CLOSEOUT-RUNS>` (once in each file, for the three-run sentence with durations, orphans drained and the lock). The rollback re-measure in `docs/PERFORMANCE_BASELINE.md`, the after bot batch numbers and the captures are the closeout report's. The ledger section is complete at the pinned commit apart from closing `FIX-SIM-DIRECT-HIT-CAST-ATTRIBUTION`.

Nobody played the build.
