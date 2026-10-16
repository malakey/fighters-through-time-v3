# CM-BOT handoff: the second-source re-hit counter in the Story bot telemetry (combat and movement update 2026-10-16, Phase A)

**Decision 7.2** ("No post-hit mercy invulnerability in Story [CMB-14]: **Measure first**: a bot counter for second-source re-hits within 30 frames, then decide"), plan dossier **2.7.2**. Diagnostics only: no gameplay rule changed, no Fighter hash moved, no save, protocol, scene, resource or `en.csv` change. **Nobody played the build**; the evidence is headless suites and one headless bot run. The decision itself waits for the closeout's after-batch (§4.3).

Branch `worktree-wf_408a1af8-cf8-5`, worktree `D:\Projects\Fighters Through Time - V3\.claude\worktrees\wf_408a1af8-cf8-5`, based on the plan commit `735296f`.

## 1. Shipped

### The observation (`scripts/Characters/PlayerController.DamageObservation.cs`, new partial)

- `public event Action<PlayerDamageObservation> PlayerController.DamageTaken`, observation only. Nothing in the game subscribes.
- `PlayerDamageObservation` (readonly struct): `Kind` (`DamageObservationKind.Hit = 0 | Hazard = 1 | Status = 2`), `SourceActorId`, `AttackID`, `HitboxID`, `AttackerIndex`, `Delivery` (`HitDelivery`), `Damage` (the HP actually removed, always > 0).
- Raised once per positive HP loss, right after the HP is written (and after `ApplyDamage` has raised `OnPlayerHPChanged`, and `OnPlayerDied` for a lethal hit), before the rest of that hit's pipeline (stun, knockback, Rally, meter). Three call lines in `PlayerController.cs`, nothing else in the file changed:
  - `ResolveIncomingHit`, after `if (damageApplied <= 0) return 0f;` (≈ :1700): `ObserveDamageTaken(in hit, damageApplied)` — the payload names its source;
  - `ApplyEnvironmentalDamage`, after `if (damageApplied <= 0) return damageApplied;`: `ObserveDamageTaken(DamageObservationKind.Hazard, damageApplied)`;
  - `ApplyPersistentDamage` (the Venom tick): its `return ApplyDamage(...)` became `int tickApplied = ApplyDamage(...); ObserveDamageTaken(DamageObservationKind.Status, tickApplied); return tickApplied;`.
- `KillPlayerNonHit` applies no damage and raises nothing.

### The boss stamp (`scripts/Enemies/BossController.cs`)

- `ResolveNodes` sets `_attackHitbox.SourceActorIdOverride = GetInstanceId()` right after resolving the hitbox (one line plus a comment). The executor already stamped a boss's pooled shots (`EnemyAbilityExecutor.cs:718`); the boss's own melee hitbox was the gap.
- Gameplay-neutral: the only hero-side reader of a payload's `SourceActorId` is `PlayerController.ReportEnemyAttack`, which acts only on an `EnemyController` owner. The other reader, `EnemyController.cs:807`, handles hits **on** an enemy, which a boss hitbox never delivers. The executor's `ActivateHitbox` does not touch the field.

### The pure tracker (`scripts/Diagnostics/PlaytestCombatStats.cs`, `RehitTracker`)

- `Record(int frame, string source, string kind, int damage, string label = "")` books one entry; zero damage books nothing; frames must not decrease.
- For each non-status entry H at frame F:
  - **cross-source re-hit** (`CrossSource30`): an earlier non-status entry from a **different** source in [F − 30, F);
  - **same-source re-hit** (`SameSource30`, the contrast): an earlier non-status entry from the **same** source in [F − 30, F); a hit can be both;
  - the 20-frame figures (`CrossSource20`, `SameSource20`): the same tests with a 20-frame window;
  - `CrossSourceSameFrame`: a different-source entry recorded earlier on the same frame (the half-open window leaves it out; see Decision 3);
  - `CrossSourceDamage30`: the damage the cross-source re-hits dealt;
  - `CrossGapHistogram`: per gap (1..30) to the latest different-source entry;
  - `ByKind`: per kind of the entry (the second hit), `Hits`, `Damage`, `Cross30`, `Cross20`, `Same30`, `Same20`, `CrossDamage30`.
- **Status ticks** (`KindStatus`) are counted under their own kind and kept out of every re-hit figure, as re-hit and as predecessor.
- `Log`: the first `LogCap` (500) non-status entries, each with its verdicts, `CrossGap` and `CrossKind` (the latest different-source predecessor's kind).
- Constants: `WindowFrames` 30, `ShortWindowFrames` 20, `LogCap` 500; kinds `standard`, `elite`, `boss`, `mirror`, `projectile`, `hazard`, `status`, `other`.
- Engine-free: it reads nothing but its arguments.

### The telemetry (`scripts/Diagnostics/PlaytestTelemetry.cs`, `PlaytestWorld.cs`)

- **Subscription:** `PlaytestWorld.SubscribeDamageTaken(PlayerController, Action<PlayerDamageObservation>)` in the "Hero gates" region (the one place in `scripts/Diagnostics/` that names `DamageTaken`; the region comment says so). The telemetry subscribes in `Attach` and on every hero rebuild (`TrackPlayerInstance`), and unsubscribes in `FinishPending` and `Detach`.
- **Frame mapping** (`ObservationFrame`): a Godot physics frame runs the flush (hurtbox contacts) before the physics callbacks (the telemetry `Tick`, the hero's status ticks, hazard updates). So an observation made before this physics frame's `Tick` is stamped `Frame + 1` (the melee-contact convention) and one made after it `Frame`, keyed on `Engine.GetPhysicsFrames()` against the frame the last `Tick` ran in. Every observation in one physics frame gets one frame number.
- **Source classification** (`RehitSourceOf`):
  - a hit with a `SourceActorId` is keyed `actor:<id>`; an `EnemyController` owner is `standard` / `elite` by tier, or `projectile` when the payload's `HitboxID` is `projectile`; a `BossController` owner is `boss` (its shots too); a `PlayerController` in `MirrorParadoxController.MirrorGroup` is `mirror` (its shots too); an owner freed since it fired keeps its key, kind `projectile` or `other`, label = the attack ID;
  - a hit with no owner is keyed `attack:<AttackID>`: `boss` when the attack ID starts `boss.` (the Jackal Priest's decoy bursts), `projectile` for a shot, else `other`;
  - a hazard is keyed `hazard:<id>` from the existing `HazardSourceAt` (the hot cyclic hazard, the escape front, an Extractor discharge, a searchlight), `hazard:env` when none is recognised;
  - every status tick shares the key `status`.
- **Report:** a new `rehits` block: `window_frames`, `short_window_frames`, `hits`, `hit_damage`, `status_ticks`, `xsrc_30`, `xsrc_20`, `same_30`, `same_20`, `xsrc_same_frame`, `xsrc_damage_30`, `xsrc_pct`, `unobserved_hits`, `xsrc_gap_histogram`, `by_kind` and `log` (`f`, `kind`, `src`, `dmg`, `x`, `x20`, `s`, `sf`, `xgap`, `xk`).
- `unobserved_hits` (`UnobservedHeroHits`) is a self-check: HP-loss events the bus carried for the hero (`player.hits_taken`) minus the observations heard. It is 0 unless a damage path bypasses the three raise sites; a lethal pit (`KillPlayerNonHit`) is the one expected cause, and no level authors one.
- `PLAYTEST_RESULT`'s `combat` summary gains `xsrc_rehits` (= `xsrc_30`).
- **Unchanged on purpose:** `damage_by_source`, the per-enemy `hits_on_player` / `damage_to_player` and `deaths_detail.recent_damage` keep their nearest-enemy attribution, so the closeout's before and after batches compare on them (7.4 reads `damage_by_source`).

### Tools

- `tools/playtest/compare_runs.py`: a `rehits` table per tag and bot: `hits/run`, `xsrc30`, `xsrc30%`, `xsrc20%`, `same30%`, `same20%`, `sf` (same-frame doubles), `xsrc_dmg%`, `xgap_med` (pooled from the gap histograms), `top_kinds` (the three kinds with the most cross-source re-hits as the second hit), `status/run`, `unobserved`. Raw counts are pooled; reports without the block print `-`. Docstring updated.
- `tools/playtest/trace.py`: one `rehits` header line (the summary without the log and histogram, then `kind=hits/xsrc30` per kind). Docstring updated.
- `tools/playtest/run_playtests.ps1`: CSV columns `xsrc_rehits` and `hits_taken` (`player.hits_taken`, every HP loss including status ticks). Help text updated.

## 2. Files changed

- **New:** `scripts/Characters/PlayerController.DamageObservation.cs` (+ `.uid`), `tests/Unit/PlaytestRehitTrackerTests.cs` (+ `.uid`), this handoff.
- **Changed (owned):** `scripts/Diagnostics/PlaytestCombatStats.cs`, `PlaytestTelemetry.cs`, `PlaytestWorld.cs` (the "Hero gates" seam); `tools/playtest/compare_runs.py`, `trace.py`, `run_playtests.ps1`; `tests/Unit/PlaytestCombatStatsTests.cs` (+2 cases, 3 private helpers).
- **Changed (declared shared regions only):** `scripts/Characters/PlayerController.cs` (the three raise lines; in `ResolveIncomingHit` only at ≈ :1700, clear of CM-STORY's ≈ :1799 and ≈ :1837); `scripts/Enemies/BossController.cs` (the stamp in `ResolveNodes`, clear of CM-STORY's `PresentDealtHit`).
- **Outside my ownership:** none.

## 3. Decisions (Provisional; for a `PROVISIONAL-REHIT-COUNTER-RULES` ledger row, diagnostics only)

1. **The observation also carries `HitboxID`** (the plan lists kind, `SourceActorId`, `AttackID`, `AttackerIndex`, delivery and damage). It is the only way to tell a pooled shot (`"projectile"`) from a melee hit at the chokepoint.
2. **The kind partition is by owner first, then delivery.** `projectile` means a standard or elite mob's shot; a boss's or the Mirror clone's shot stays `boss` / `mirror`. One partition then answers both "mob fire vs mob melee" and the report's "excluding bosses and the Mirror" variant (`REPORT_1_PLATFORMERS.md` :540). Kinds `status` and `other` are added to the plan's six.
3. **Same-frame doubles are counted apart** (`xsrc_same_frame`), not inside `xsrc_30`. The plan's window is [F − 30, F), which excludes F itself; two mobs connecting on one frame would be invisible otherwise, and a mercy window opened by the first would refuse the second.
4. **A hit can be both cross-source and same-source**, and each figure is "exists a predecessor", not a pair count.
5. **Status ticks are neither re-hit nor predecessor**, and are not logged (they have their `by_kind` row). The log's 500 slots go to hits.
6. **Hazards are sources and are in the headline** (`hazard:<id>`, with every unrecognised hazard sharing `hazard:env`). A mercy window would normally cover a hazard contact too; `by_kind.hazard` lets the reader take them out.
7. **The frame mapping** (`ObservationFrame`) puts every observation of one physics frame on one telemetry frame, whether it came from the flush or a physics callback.
8. **Extra report keys beyond the brief:** `xsrc_damage_30` (what a 30-frame mercy window could have refused), `xsrc_pct`, `xsrc_gap_histogram`, the log's `xgap` / `xk`, and the `unobserved_hits` self-check. The CSV also gains `hits_taken`.
9. **Attribution of `damage_by_source` is not switched** to the observation's source (Section 1, "Unchanged on purpose").

## 4. Deviations from the plan

- The observation adds `HitboxID` (Decision 1) and the report adds the keys in Decision 8. Nothing in the plan was left out.
- The test count is as declared (+7). The engine cases deliver real mob and boss **attack payloads** (`Hitbox.CreatePayload`) through `PlayerController.TakeDamage` (the `IDamageable` route into `ResolveIncomingHit`), rather than driving two mob AIs into swinging on cue; the chokepoint, the event, the seam, the frame mapping and the classification are all real.

## 5. Tests

**New suite `PlaytestRehitTrackerTests`** (`tests/Unit/`, pure C#, no `[RequireGodotRuntime]`, no engine API): **+5**.

- `TwoSourcesTenFramesApartAreOneCrossSourceReHit`: cross 1 at gap 10 (20-frame too), no same-source, the damage, the histogram, and the per-kind row booking the second hit.
- `TheWindowIsHalfOpenAndTheTwentyFrameFigureIsNarrower`: a 25-frame gap is in 30 but not 20; exactly 30 is in; 31 is out; with two earlier sources the gap and kind are the latest one's.
- `SameSourceReHitsAreTheContrastAndAHitCanBeBoth`: same-source is never cross-source; a hit with both kinds of predecessor counts in both; the 20-frame same-source figure.
- `StatusTicksAreCountedUnderTheirOwnKindAndKeptOutOfTheHeadline`: a status tick is never a predecessor or a re-hit, has its own kind row, and is not logged; zero damage books nothing.
- `SameFrameDoublesAreCountedApartAndTheLogIsCapped`: two sources on one frame are `xsrc_same_frame`, not `xsrc_30`; a same-frame second contact from one source is neither; the log stops at 500 while the counts go on.

**`PlaytestCombatStatsTests`** (6 → 8): **+2**.

- `TwoMobsHittingTenFramesApartReadOneCrossSourceReHitAndTheObservationChangesNothing` (real frames): two passive `chrono_slasher`s' attack payloads reach Joan 10 physics frames apart. The telemetry reads hits 2, cross 1 (30 and 20), same 0, gap 10, kind `standard`, label `chrono_slasher`, two sources, `unobserved` 0 and `combat.xsrc_rehits` 1. The same sequence on a fresh hero with **no** subscriber leaves HP, meter and the Rally pool identical after each hit and after 30 frames of drain.
- `ABossHitIsAttributedToTheBoss` (real frames): the Borgia Inquisitor's melee payload names its boss (`SourceActorId` = the boss), the telemetry books it `boss` / `borgia_inquisitor` under the boss's own key, and a mob hit 5 frames later is a cross-source re-hit at gap 5 whose predecessor kind is `boss`.

**Rewritten:** none. **Shared test files:** `PlaytestCombatStatsTests` is mine alone in Phase A.

**Mutation check:** with the boss stamp and the `ResolveIncomingHit` raise both commented out (then restored from the commit), `PlaytestCombatStats` + `PlaytestRehitTracker` read `Failed: 2, Passed: 11, Total: 13`: exactly the two new engine cases failed (`ABossHitIsAttributedToTheBoss` on "The boss's melee hitbox must name its boss.", the two-mob case on its re-hit counts), and the five pure and six older cases passed.

**Runs** (each holding `D:/Projects/.ftt-gdunit-lock`, `GODOT_BIN` set, after one `--headless --import`; the helper took the lock first, then drained `testhost` / `Godot*_console`, then ran, then released it):

| Run | Result |
|---|---|
| `PlaytestRehitTracker` + `PlaytestCombatStats` + `PlaytestEncounterTelemetry` | **`Passed! - Failed: 0, Passed: 28, Total: 28`** (5 + 8 + 15, equal to their `[TestCase]` count) |
| The mutation run above | `Failed: 2, Passed: 11, Total: 13` (as intended) |
| **Full suite** (final code, `099cef4`) | **`Failed: 1, Passed: 3234, Total: 3235`**, 1 m 33 s. `Total:` = 3228 + 7 exactly, and the `[TestCase]` attribute count under `tests/` is 3235. The one failure is below |
| `RollbackReadinessTests` alone, straight after | **`Passed! - Failed: 0, Passed: 22, Total: 22`**, the failing case included |

**The one full-run failure, case by case.** `RollbackReadinessTests.WorstCaseDepthSevenResimulationFitsTheRollbackBudget`: "'alexandria_chambers' breached the rollback budget 2 times". That stage's 300 depth-7 corrections read median 0.132 ms, p95 0.287 ms, **max 10.942 ms** against the 8.0 ms budget; the other stages printed before it had 0 breaches (medians 0.085–0.127 ms). Two isolated wall-clock spikes beside a normal median read as scheduling, not simulation cost: the machine was shared with other agents' worktrees (their lock sessions interleaved with mine all afternoon, and their builds run outside the lock), and the suite stops at the first stage that breaches, so the three later stages were not measured in that run. **This branch changes nothing the case runs:** `git diff --stat 735296f -- scripts/FighterSim scripts/Networking tests/Determinism` is empty, and the suite is pure .NET sim timing that never reaches a Story controller or the diagnostics. Run alone under the lock immediately afterwards, the class passed 22/22. I did not take a second full run, per the one-full-run instruction; the orchestrator's merge-point run is the next full reading.

`dotnet build`: 0 errors, only the three pre-existing warnings (CS8632, CS0649, `KLSG_ECS004` on `FighterTuningComponent`); no `KLOTHO_DET002`.

## 6. Measurements

**One `play` bot run, Level 2, Joan, Normal** (the brief's demonstration; tag `cmbot`, seed 0, `--max-seconds=0`, holding the lock; report `%APPDATA%/Godot/app_userdata/Fighters Through Time/playtest/L02_joan_play_normal_cmbot.json`):

- End `left_level:collapse(Death)` at 175.9 s, 2 checkpoints, 3 / 3 locks cleared, 8 / 8 members killed, 4 deaths, all to the Siegemaster Duke. That is the same end GU-BOT recorded on Level 2 (`gubot3`), so nothing here moved it.
- The `rehits` block: **27 hits, 441 damage, `xsrc_30` 0, `xsrc_20` 0, `same_30` 0, `xsrc_same_frame` 0, `status_ticks` 0, `unobserved_hits` 0** (every HP loss the bus carried was observed). By kind: `boss` 17 hits / 307 damage, `hazard` 4 / 69 (`level_02.mortar_lane_a` ×2, `level_02.mortar_lane_c`, `env`), `projectile` 5 / 55 (all `laser_archer`), `standard` 1 / 10 (`chrono_slasher`). The Duke's 17 hits land 171–517 frames apart; no two hits of any source came within 30 frames of each other on this run.
- `PLAYTEST_RESULT`'s `combat` carries `"xsrc_rehits": 0`; the CSV extraction (`run_playtests.ps1`'s parser, applied to that line) gives `xsrc_rehits` 0, `hits_taken` 27. `compare_runs.py cmbot` prints the `rehits` row and `trace.py L02_joan_play_normal_cmbot` the header line; on a pre-CM-BOT batch (`mafter`) the table prints `-` and the trace prints no line.
- **A side finding about the old attribution:** the same run's `damage_by_source` reads `laser_archer` 33 and `chrono_slasher` 32, while the observation names the archer as the owner of five shots (55) and the slasher of one hit (10). The nearest-live-enemy rule books an archer's shot to whichever mob stands nearest the hero. It is left unchanged for before/after comparability (Decision 9); the `rehits` block is the source-accurate one.
- One run is a smoke, not a measurement: Level 2 on Normal holds at most 2 slots and 1 melee attacker near the hero (`encounters.melee_max` 1), so a cross-source re-hit is rare by construction there. The closeout's after-batch (Levels 2, 6, 7, 12, 13; Joan and Lincoln; Easy and Normal; seeds 0–1) is the measurement.
- Smoke noise: the run printed `6 resources still in use at exit`, the nondeterministic `AuthoredResources` at-exit noise CLAUDE.md describes; nothing under `scripts/Diagnostics/` is loaded by a level outside the runner.

**Replay / Fighter hashes:** not applicable. Nothing under `scripts/FighterSim/` changed, and the observation changes no Story value (pinned by the two-mob case).

## 7. Not delivered / open

- **The decision itself** (a Story-only 20–30 frame mercy window, or none) waits for the closeout's after-batch `rehits` table (§4.3), as the plan says. One Level 2 run is a smoke, not a measurement.
- **Hazard identity is a heuristic.** `HazardSourceAt` recognises active cyclic hazards, the escape front, a discharging Extractor within 240 px and a striking searchlight; every other environmental source (falling debris, rising water, rift snaps, Level 7's undertow, Level 1's spikes, `StageHazard`) shares the key `hazard:env`, so two different unrecognised hazards read as one source.
- **The Jackal Priest's decoy bursts** carry no owner; they are `boss` by their attack-ID prefix and keyed apart from the boss body.
- **A hit absorbed whole by a shield or barrier, a blocked hit and an invulnerable refusal** take no HP and are not observed; the counter measures damage taken, which is what a mercy window would refuse.
- **Status ticks are not attributed** to the status's applier (the tick receives none).
- **`RollbackReadinessTests`' depth-7 timing budget is wall-clock and can spike on a shared machine** (Section 5): one breach pair on `alexandria_chambers` in my full run, green alone. Not this workstream's code. A pre-existing fragility for the orchestrator (outside my ownership, not changed): `MeasureWorstCaseDepthSeven` (`tests/Determinism/RollbackReadinessTests.cs` ≈ :676–686) says "Fail only well outside the budget so ordinary CI variance cannot flap the gate" and checks p95 against 2 × the budget, but then also asserts `budgetBreaches.Count == 0`, so any single correction over 8.0 ms, one preemption, fails the run. Worth watching at the merge-point runs, since CM-SIM's phase machine will move that suite's numbers anyway.
- **One bot run, one hero, one difficulty.** Level 2 Normal had no cross-source re-hit at all (27 hits, nearest pair 62 frames apart); Levels 6, 7, 12 and 13, Easy, Lincoln and the second seed are the closeout's batch.

## 8. For the closeout

**CLAUDE.md** ("Automated Story playtests (bots)"):
- The "Since 2026-10-11 the report also carries" list: a `rehits` bullet (Section 1's keys; status ticks out of the headline; same-frame doubles apart; `unobserved_hits` should be 0).
- The `PLAYTEST_RESULT` / CSV paragraph: `combat.xsrc_rehits`; CSV `xsrc_rehits` and `hits_taken`.
- The `compare_runs.py` / `trace.py` paragraph: the `rehits` table and header line.
- That `damage_by_source` and `hits_on_player` keep their nearest-enemy attribution.

**AGENTS.md:** the repository map's `scripts/Diagnostics/` line ("… and a rehits block: hits on the hero by source and the cross-source re-hits within 30 / 20 frames, CM-BOT"); the Scene-scoped systems table or the `PlayerController` notes may mention the observation-only `PlayerController.DamageTaken` event; the baseline (+7: `PlaytestRehitTrackerTests` 5, `PlaytestCombatStatsTests` 6 → 8).

**Ledger:**
- New `USER-STORY-MERCY-MEASURE-FIRST` (decision 7.2, CMB-14): the counter is built; the ruling waits for the after-batch `rehits` table.
- New `PROVISIONAL-REHIT-COUNTER-RULES` (Decisions 1–9).
- Amend `USER-BOT-COMBAT-TELEMETRY`: the `rehits` block, `combat.xsrc_rehits`, the CSV columns, the `compare_runs.py` table; suites `PlaytestRehitTrackerTests` (5), `PlaytestCombatStatsTests` (8). Add to its known limits: `damage_by_source` (and `hits_on_player`) book a hit to the nearest live enemy, so a ranged mob's shots can land on a nearer melee mob's line (Section 6: archer 33 / slasher 32 against the observed 55 / 10); `rehits` carries the true owner.

**Design-master lines owed:** none. The bot telemetry is diagnostics and is not in `design-godot.md`. **Contracts:** none.
