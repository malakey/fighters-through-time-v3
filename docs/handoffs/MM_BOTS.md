# MM-BOTS handoff (meter and Defy update, Phase B2): bots and telemetry

Branch `mm/botsdiag` (worktree `D:/Projects/ftt-wt/mm-botsdiag`), cut from `e8355b19`. Scope: `scripts/Diagnostics/`, `tools/playtest/`, and the Playtest tests. Plan 2.13, O24, P26; ledger `PROVISIONAL-BOT-DEFY-POLICY` (amends `USER-BOT-COMBAT-TELEMETRY`).

**Bot batches from before and after this package are NOT comparable** (the Momentum gauge replaced the Special cooldowns, Story meter gain is x0.5, Defy is an active burst the `play` bot presses, the old `defy_procs` is gone). The reference pair runs in the clean worktree `D:/Projects/ftt-wt/mm-bots` (plan 5.2).

## 1. Shipped (exact names)
- `PlaytestDefyPolicy` (new, pure C#, `scripts/Diagnostics/PlaytestDefyPolicy.cs`): `StyleDefies(FightStyle)` (only `Play`), `NoteHit(frame)`, `NoteDefyPressed(frame)`, `HitsInWindow(frame)`, `ShouldDefy(style, frame, canDefy, inHitstun, enemiesNear)`; constants `MinHitsInString` 2, `StringWindowFrames` 60, `MinEnemiesNear` 2 (changed to 1 by the orchestrator in `632c393b`, so the `play` bot exercises Defy with one foe near; the current value is 1), `EnemyRadiusPixels` 250, `SpacingFrames` 300.
- `StoryBot.PressDefyIfNeeded` (first thing in `Decide`): the `play` bot taps `GameplayButtons.Defy` when `PlaytestWorld.CanDefy` (the hero's own 50 Influence and state refusals), `CharacterState.Stunned`, 2+ discrete hits in 60 frames (hero `DamageTaken` observations, `HitDelivery.Tick` excluded), 2+ awake foes within 250 px, 300+ frames since its last press. `spam`, `basics`, `bypass`, `feel` never.
- `PlaytestWorld` "Hero gates" region: `SpecialReady`, `SpecialReuseRemaining`, `MomentumPips`, `SeedMomentum`, `MomentumRegenPerTick`, `CanDefy`, `DefyExecutions`, `SubscribeDefyExecuted`, instance `FoesWithin`. These are the only places in `scripts/Diagnostics/` that name the gauge and Defy members.
- Timer reads replaced: `StoryBot.TryConfirmSpecial` and the Specials phase read `SpecialReady` (a pip AND the 2 s reuse delay clear); the phase moves on when neither Special is ready (so a starved gauge no longer waits 120 frames). `FeelProbeBot` seeds the gauge to 4 pips at `_t == 0` (`SeedMomentum`) and presses on `SpecialReady`; the lockout segment (702-715) is unchanged and still measures the lockout.
- `PlaytestCombatStats.cs`: `PressSnapshot.CooldownSeconds` became `ReuseSeconds` plus `MomentumPips`; `PlaytestCombatMath.CooldownTickSeconds` became `ReuseTickSeconds`, new `PipSlack`, `SpecialCostPips`; `RefusalLabels` = hitstop, time_freeze, suppressed, lockout, dormant, `reuse_delay`, `no_momentum`, meter, executing (`cooldown` dropped). `MeterTracker` lost `DefyProcs` and the `defyUsed` parameters. New `DefyTracker` (executions, hits and deaths as frames, `Resolve(endFrame)`) and `MomentumTracker`.
- `PlaytestTelemetry`: `defy` and `momentum` report blocks; `ability_presses` refusal labels; `defy_procs` removed from `meter`; trace samples carry `mo` (pips); `PLAYTEST_RESULT.combat` gains `defies`, `defy_died3s`, `starved`, `momentum0`, `specials_per_min`.
- `defy` block: `policy`, `window_frames` 180, `pressed`, `executed`, `refused_by`, `died_within_3s`, `hits_before`, `hits_after`, `targets_blasted`, `healed`, and `log` (per execution: `f`, `meter_before`, `targets`, `healed`, `hits_before`, `hits_after`, `died_within_3s`, `after_truncated`).
- `momentum` block: `pips_start` (2.0 on a fresh level), `pips_end`, `gained`, `gained_by` (`regen`, or the landing swing's hitbox ID, `other`), `spent.special1/special2`, `specials_cast`, `specials_per_min`, `starved_presses` (the `no_momentum` refusals), `frames_starved` (under one pip), `frames_full`, `tracked_frames`.
- `tools/playtest`: `compare_runs.py` prints a `defy` and a `momentum` table per tag (and drops `defy/run`); `run_playtests.ps1` CSV gains `defies`, `defy_died3s`, `starved`, `momentum0`, `specials_per_min`; `trace.py` prints the `defy` block with one line per execution, the `momentum` block, and `mo=` in each trace line.

## 2. Decisions
- Spends come from the Specials' execution serials, not from a negative step, so a spend and a gain in one tick both book. This counts the freeze-buffered casts that `ability_presses` books as refused under `hitstop` (the known limit), so `specials_cast` can exceed `ability_presses` accepted (Level 6 run: 19 against 13).
- Gain split: up to one tick of regen is `regen`, the rest goes to the swing that first landed that tick (its hitbox ID), else `other`. A tick holding both mis-books at most 1/720 pip; a gain lost to the cap is invisible.
- `frames_starved` is "under one whole pip", not "exactly zero": that is the state in which a Special cannot be paid.
- The `defy` block reads `policy: never` for non-`play` bots, so tables show zeros rather than missing keys.
- Hits for the Defy windows are the hero's discrete `DamageTaken` observations (ticks excluded), on the same frame clock as the re-hit counter.

## 3. Deviations and cross-edits
None from the plan. No file outside `scripts/Diagnostics/`, `tools/playtest/`, `tests/Unit/Playtest*` was touched.

## 4. Tests: +4
- `PlaytestCombatStatsTests` 9 to 11: `TheDefyTrackerCountsAnExecutionItsHitsBeforeAndAfterAndADeathWithinThreeSeconds`, `TheMomentumBlockStartsAtTwoPipsAndBooksGainsBySourceAndSpendsBySpecial`. Rewritten in place: the refusal case (labels `reuse_delay` / `no_momentum`, the Ultimate never reading the gauge, a 0.99999-pip gauge still paying) and the meter case (the `DefyProcs` lines removed).
- New `PlaytestDefyPolicyTests` 2: `PlayPressesDefyOnlyInHitstunInAStringAmongTwoFoesAndNotWithinThreeHundredFramesOfTheLast`, `SpamBasicsAndBypassNeverPressDefyWhateverTheSituation`.
- Seen failing: one mutation batch on the committed code (spacing 300 to 299, `StyleDefies` always true, the "after" window including the Defy's own frame, the spend add-back removed, the `no_momentum` label removed) failed 5 of 13: both new policy cases, both new tracker cases and the refusal case; reverted.
- Run: `dotnet build` 0 errors. Under the lock, after one `--import`, filtered `PlaytestCombatStats|PlaytestDefyPolicy|PlaytestEncounterTelemetry|PlaytestFlightModel|PlaytestRehit|PlaytestBotStrikeRange`: `Failed: 0, Passed: 38, Total: 38` (11 + 2 + 15 + 2 + 7 + 1, equal to the `[TestCase]` count of those files). No full suite.

## 5. The gate run: `--level=2 --character=joan --bot=play --difficulty=normal --tag=mmbotscheck`
Exit 0, no script errors (only the known `HudCapsule` triangulation ERROR lines). Result: `left_level:collapse(Death)`, 172.8 s, 4 deaths (the Siegemaster Duke alone did 309 of 415 damage taken). Level 2 has no Special unlocked for Joan (Special 1 comes after Level 2), so the Specials are 0 by design; the Ultimate is locked too, so the bar sat full from frame 1575. The bot never Defied: its deaths came from the lone boss (one foe, no string), which the P26 conditions exclude.

```
"defy":     {"policy":"play","window_frames":180,"pressed":0,"executed":0,"refused_by":{},"died_within_3s":0,"hits_before":0,"hits_after":0,"targets_blasted":0,"healed":0,"log":[]}
"momentum": {"pips_start":2,"pips_end":2,"gained":2,"gained_by":{"combo_1":0.2,"combo_2":0.2,"combo_3":0.4,"regen":1.2},"spent":{"special1":0,"special2":0},"specials_cast":0,"specials_per_min":0,"starved_presses":0,"frames_starved":0,"frames_full":9448,"tracked_frames":10368}
```

That run exercises no Special and no Defy, so two more runs on the same build:
- Level 6 (Joan, Normal, `play`, tag `mmbotscheck6`, unchanged policy): `level_complete`, 139 s. `defy` all zeros (the conditions never held). `momentum`: `pips_start 2`, `gained 17.52` (`regen 10.321`, `combo_3 4.0`, `combo_1 1.7`, `combo_2 1.5`), `spent {special1 13, special2 6}`, `specials_per_min 8.2`, `starved_presses 0`, `frames_starved 3509` of 8345.
- Level 6 with the policy thresholds temporarily relaxed to 1 hit and 1 foe (tag `mmbotscheck6d`; **verification only, reverted, not committed**): `defy {"pressed":4,"executed":4,"refused_by":{},"died_within_3s":0,"hits_before":4,"hits_after":0,"targets_blasted":4,"healed":46}` with four `log` rows (meter before 52.7, 97.9, 89.5, 100; healed 11, 11, 11, 13). This shows the press, the execution and the report end to end. `compare_runs.py` prints both new tables from these reports.

## 6. For the merge and the docs
- Merge MM-BOTS then MM-CPU (plan 6): MM-CPU owns none of these files.
- The after bot batch (closeout gate 5) reads the new tables; `died_3s%` in the `defy` table is the user's original complaint. Expect few Defies from the `play` bot at the P26 thresholds (the unmodified Level 6 run had none): `defies/run` near 0 is the policy, not a broken path.
- Ledger: `PROVISIONAL-BOT-DEFY-POLICY` (O24, P26); amend `USER-BOT-COMBAT-TELEMETRY` (cooldown label dropped, `defy_procs` replaced).
- CLAUDE.md "Automated Story playtests" section (docs workstream): the `defy` and `momentum` blocks, the new CSV columns, the new refusal labels, the comparability note.
- Nobody played the build.
