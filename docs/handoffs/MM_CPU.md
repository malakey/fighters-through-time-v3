# MM-CPU handoff (meter and Defy update, Phase B2): the CPU's pips and Defy, and the Mirror clone

Branch `mm/cpu`, worktree `D:/Projects/ftt-wt/mm-cpu`, cut from `e8355b19`. One code commit plus this handoff.

## 1. Shipped (exact names)

`CpuDecisionObservation` (additive; every default keeps a hand-built observation as it was)
- `MomentumKnown` (P24, default 0: the gauge is ignored), `SelfMomentumUnits`, `SelfDefyFrames`, `SelfUltimateCaptured`, `TargetUltimateActivationPhase`, `TargetUltimateActivationPhaseFrames`.
- `SpecialOneCooldownFrames` / `SpecialTwoCooldownFrames` keep their names (see Decisions); they carry the reuse delay.
- `CpuVerbState` gained `UltimateCaptured`, `MomentumKnown`, `MomentumUnits`, `DefyFrames`, `UltimateActivationPhase`, `UltimateActivationPhaseFrames`; `FighterSimulationWorldObserver.TryGetVerbState` fills them from 310 `MomentumUnits`, 323 `DefyFrames` and component 321 (`IsCaptured`, `Phase`, `PhaseFrames`). `FillVerbLayer` copies the self half into the observation (`MomentumKnown` is set there, so the static `Observe` with no world observer leaves it 0) and the target's activation phase.

`FighterCpuController`
- `SpecialPipReady(observation, slot)` replaces the four cooldown tests (ladder close Special 1 / Special 2, ranged Special 1, and `MobilitySpecialReady`): `(MomentumKnown == 0 || SelfMomentumUnits >= BasicComboRules.MomentumSpecialCostUnits) && reuse <= band minimum reaction delay`. The `NextPercent()` draws stay where they were, behind the ready test.
- `CpuBandTuning.DefyStringPercent`, `DefyMinStringHits`, `DefyCornerEscape`, `DefyBeatsUltimateWindup` (code-owned): Easy and `EasyBoss` 0 / - / no / no; Medium (`Normal`) 35 / 3 / no / no; Hard 100 (no roll) / 2 / yes / yes.
- `FighterCpuController.DefyStringGapFrames` (8): the string rule of P25.
- `UpdateDefyReflex` (called from `ApplyHitstunDefense` after its own two draws, on the `newInstance` edge): counts the instance into `_stringHits` (gap of at most 8 actionable frames), and if the band gate passes (`DefyStringPercent > 0`, `_stringHits >= DefyMinStringHits`, `DefyLegalForSelf`) fires the reflex; the `NextPercent()` draw happens only when `0 < percent < 100` and only after the gate passes (Easy never draws; Hard never).
- `DefyLegalForSelf`: live match, alive, not dazed, Influence >= `BasicComboRules.DefyInfluenceCost`, `SelfDefyFrames == 0`, not captured, not an A02 caster.
- `TryDecideNeutralDefy` (in `Decide`, after the off-stage branch): Hard A02 wind-up (target in `PhaseWindup`, within 3.0 u, with at least `DefyBlastFrame` + reaction maximum frames left) and corner (self within `CorneredMargin` of a wall, target on the open side, within 3.0 u). `Defy` joined `EdgeButtons`, so it pulses on the schedule ring with the band reaction delay.
- `ApplyDefyOutput` (in `Sample`, after `ApplyHitstunDefense`): the reflex or a scheduled Defy edge, re-checked against `DefyLegalForSelf` every tick, returns `GameplayButtons.Defy` alone (exclusive, like the Block hold, and it ends a jump sustain); an edge that is no longer legal is stripped.

`MirrorParadoxDecisionAdapter` (`MirrorParadoxController.cs`)
- `Observe()` sets `MomentumKnown = 1`, `SelfMomentumUnits = round(MomentumPips * 7200)`, `SelfDefyFrames` (`31 - DefyFrame` while `IsDefying`). The reuse fields already read the Story slot timers, which are the 2 s reuse delays after MM-SMOM.
- The clone CPU counts a string, but a Story body stays `Stunned` straight through a chain, so one continuous stun would read as a single hit. `Bind` subscribes to the clone `PlayerController.DamageTaken`, and `Sample` advances a projected hitstun (`BasicComboRules.HitstunFrames[0]` on each new damage event, counting down, 1 minimum while `Stunned`), which `Observe` reports as `HitstunFrames`. A direct `Observe()` outside `Sample` reads the last projected value (1 when never struck).
- The clone emits Defy through the existing `InputManager.SetInputSource` path: the Hard band frame already carries the bit. Its cost is the Story one (unscaled, 50 at acceptance). The hero has no A02 and Story has no stage bounds, so the wind-up and corner triggers cannot fire there: the clone Defy is the string trigger only.

## 2. Decisions (one line each)

- **The observation keeps `SpecialOneCooldownFrames` / `SpecialTwoCooldownFrames`** (plan 2.12 says renamed): seven test files and the Mirror set them by name, the fields mean "reuse delay" now, and the doc comments say so. MM-SWEEP may rename them with their readers.
- **Gauge and Defy state ride `CpuVerbState`**, not a changed static `Observe` signature: the sim observer already resolves the self verb layer, and `Observe` stays callable with no world (then `MomentumKnown` is 0).
- **Corner and wind-up Defies need the target within 3.0 u** (the blast is 2.5 u): the plan lists the range only for the wind-up; a corner Defy with nobody near would waste 50 Influence.
- **A wind-up Defy needs `DefyBlastFrame` + reaction maximum (14) frames left**: a press after the reaction delay still blasts at frame 6 while the 20-frame wind-up is running.
- **The reflex tick is exclusive Defy**, not Defy beside Block: the sim ignores Block under a Defy, and an exclusive press keeps the V7.4 argument against chords forming out of the hold.
- **No Defy while dazed, captured or an A02 caster** (the sim refuses the last two); a dazed CPU stays inert as before.
- **Hard keeps no Ultimate reserve**: it Defies on every qualifying trigger (user, O23 / Q4) and may starve its Ultimate; judged in play.

## 3. Deviations from the plan

- The two reuse fields were not renamed (above).
- Corner and wind-up triggers carry the 3.0 u range and the wind-up a frames margin (above).
- Mirror: a projected hitstun from the clone damage events (not in the plan; needed for the string rule, since `Stunned` is continuous). `MirrorParadoxController.cs` is the file the matrix grants; no `PlayerController` file was touched.

## 4. Cross-edits

None outside the matrix. Files: `CpuDecisionObservation.cs`, `FighterCpuController.cs`, `MirrorParadoxController.cs`, and the tests below.

## 5. Tests (+10 against the plan +10; `[TestCase]` count 3860 to 3870)

New: `CpuDefyPolicyTests` 6 (Easy and the Easy boss never and draw nothing; Medium once per instance, 3+ hits, 50+; Hard string with and without the gap and no roll; Hard cornered; Hard wind-up within 3.0 u and banking otherwise; a real-sim case where the Hard CPU Defy cancels the A02 wind-up and, with the trigger off, the strike connects), `CpuVerbPolicyTests` +2 (a known gauge needs a pip and a clear reuse delay; a hand-built observation ignores the gauge, P24), `FighterCpuHitstunDefenseTests` +1 (the reflex wins its tick over the Block hold, once per string), `MirrorParadoxTests` +1 (a Hard clone struck twice presses Defy through `SetInputSource`, defies, and the blast staggers the hero).
In place: `FighterCpuSpecialTimingSmokeTests` records `specialPresses` and `defies` per stage (no assertion added). The other listed in-place suites (`FighterCpuBehaviorTests`, `CpuSelfActionStateTests`, `CpuMeleeBandTests`, `CpuRecoveryMatrixTests` on Paris, Vesuvius and Nassau, the Mirror projection cases) needed no change and pass as they were, because `MomentumKnown` defaults to 0.

Seen failing: one mutation run on the committed code (gauge ignored, string gap +1, the Medium draw before the gate, the corner side flipped, Defy beside the held buttons, the wind-up frames gate -99, the Mirror damage subscription removed) failed 8 of the 10 new cases; the two it did not reach (the Easy case, and the hand-built-observation case) compare frames or read a default by construction. All reverted (`cmp` against backups).

Runs (lock held, console exe):
- Filtered `Cpu|MirrorParadox`: 136/136 before the new cases; 145/146 with them, then one fixed fixture (the control had reached the cinematic phase, not Active); `CpuDefyPolicy` then 6/6.
- Full suite: `Failed: 1, Passed: 3869, Total: 3870` (2 m 14 s), the one failure being that fixture; not repeated in full after its fix, so the all-green 3870 line is not shown.
- Smoke `Level_13_ChronalVoid.tscn --headless --fixed-fps 60 --quit-after 600`: exit 0, no script error; the log shows two `Invalid polygon data, triangulation failed` lines and `3 resources still in use at exit` (known noise).
- `dotnet build`: 0 errors.

The `[CM-CANCEL smoke]` record, Hard vs Hard, 1800 ticks, both fighters per stage, Specials pressed / Defies made: florence 7 / 0, orleans 10 / 0, chicago 14 / 1, paris 8 / 0, vesuvius 4 / 0, nassau 15 / 2, alexandria 12 / 1, berlin 6 / 1, globe 12 / 0, gettysburg 9 / 1. Every stage still casts a Special through Active and replays its hashes.

## 6. Open

- Hard may starve its own Ultimate (Q4); the smoke shows 7 Defies across ten matches.
- The clone cannot meet the wind-up or corner triggers in Story; only the string trigger works there.
- The projected hitstun counts damage events only: a blocked or absorbed hit does not extend the string.

## 7. For the docs

`CPU_COMBAT_POLICY.md`: the Special ladder reads a pip and the reuse delay; Defy by band (section 1). Ledger rows: `USER-CPU-PIPS-LIKE-COOLDOWNS` (G4.4), `USER-DEFY-CPU-BY-BAND` (4.4), `PROVISIONAL-CPU-DEFY-REFLEX` (O23, P24, P25; the 3.0 u range, the 14-frame wind-up margin, the exclusive Defy tick, the Mirror projected hitstun). AGENTS.md: the CPU hitstun-defense bullet gains the Defy reflex; the test baseline is 3870.

Nobody played the build.
