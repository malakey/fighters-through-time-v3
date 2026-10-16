# CM-FIXA-PRES handoff: presentation and telemetry fixes (combat and movement update, Phase A fix round, 2026-10-16)

The Phase A fix round's presentation and telemetry workstream for `docs/COMBAT_MOVEMENT_UPDATE_2026-10-16_PLAN.md`. It covers the three `PRES` findings in `D:/Projects/genre_review_2026-10-13/fixA_inputs.json`:

- the Fighter wind-up telegraph on a knocked-out caster (decision 2.1, dossier 2.2.1, CM-ULT);
- the Ultimate telegraph tint on a Hard Mirror clone (decision 2.1, CM-ULT Decision 1);
- the re-hit counter's periodic ticks (decision 7.2, dossier 2.7.2, CM-BOT Decision 6).

**Branch:** `worktree-wf_a50df6ba-c2b-4`, in worktree `D:\Projects\Fighters Through Time - V3\.claude\worktrees\wf_a50df6ba-c2b-4`. The harness created the branch at the old `d2e52e0`; I fast-forwarded it to `main` at `5434151` (the merged, green Phase A, 3332) before any edit.

**Commits, in order:**

| Commit | Content |
|---|---|
| `e36b4e7` | The Fighter wind-up telegraph clears on a knocked-out caster. |
| `1999de3` | The wind-up telegraph tint is never warmed by the hero aura. |
| `c12f93d` | Periodic ticks are not re-hits; a new combat-only re-hit figure. |
| (head) | This handoff. |

**What changed overall:**

- **No gameplay change.** No sim rule, Klotho component, snapshot field, hash input, save field, protocol field, scene, resource or `en.csv` key changed.
- The telegraph fixes are presentation only.
- The counter fix is diagnostics only. Its one gameplay-file edit is an observation-only optional parameter on the environmental chokepoint, which changes no value.
- **Fighter hashes:** none moved. The only file under `scripts/FighterSim/` is the driver's presentation partial.

**Nobody has played or looked at any of it.** The evidence is the headless suite, a failing-first run and one full run.

## 1. Findings fixed

### 1.1 The Fighter wind-up telegraph stayed on a caster KO'd mid-wind-up

**The bug:**

- `FighterSimulationDriver.IsUltimateActivationWindup` read only component 321's `Phase`.
- The sim clears a knocked-out caster's 321 one tick late. `AdvanceUltimate` runs near the top of `FighterCombatSystem`, before the basic swing, intent, projectile, zone or hazard that lands the KO. `ApplyStockLoss` leaves 321 alone.
- So on the KO tick 321 still reads `PhaseWindup`, and that tick's sync showed the red tint and the X on the fighter at the respawn anchor.
- The same picture then held for:
  - the 12-frame stock-loss freeze, during which no sync runs;
  - on a final stock, the 0.5 s decisive-KO hit-freeze.
- The critical wind-up cue shared the predicate.

**The fix** (`scripts/FighterSim/FighterSimulationDriver.UltimateTelegraph.cs`): `IsUltimateActivationWindup` now also requires a live, free caster, using `AdvanceUltimate`'s own clear conditions:
- `Simulation.TryGetFighter(playerID, out caster)`;
- `!FighterStockLossRules.IsFallen(in caster)`, which means stocks are left and the caster is off the respawn platform;
- and not `verb.BeingHeld` (included for symmetry with `AdvanceUltimate`; a verifier noted it is not needed for the KO scenario, because `AdvanceGrabs` runs first and clears a hold set during combat the same tick).

Both `SyncUltimateTelegraph` and `SyncUltimateWindupCue` read the predicate, so a caster KO'd on the wind-up's first tick also plays no cue.

**The test pins the lag it covers.** On the KO tick, `ACasterKnockedOutMidWindupDropsTheTelegraphOnTheKnockoutTickAndThroughTheFreeze` first asserts that 321 is still `PhaseWindup` and `RespawnFramesRemaining > 0`, then that nothing shows.

### 1.2 On a Hard Level 13 Mirror the wind-up tint was gold-warmed, not the canonical red

**The bug:**

- `MirrorParadoxController` builds the Hard clone with `applyStoryProgression: MirrorsPurchasedPerks` (true on Hard).
- `CharacterFactory.BuildVisual` maps that flag to `SetHeroAura(true)`.
- `GlowPresentationController.EffectiveTint` applied the aura warm after choosing the telegraph colour, giving about (1, 0.38, 0.25). That leans toward the Guard-Crush orange and disagrees with the glyph's pure `TelegraphGlyph.UnblockableColor` (1, 0.25, 0.2).

**The fix** (`scripts/Combat/GlowPresentationController.cs`):

- While the telegraph flag is set and no tint override (hit flash) is running, `EffectiveTint` returns `UnblockableColor` before the aura step.
- The Suppression smother still applies to that red.
- A hit flash on top of the telegraph, and every other tint, keep the aura warm exactly as before.
- **The clone keeps its aura.** Whether a Hard clone should wear the hero aura at all is the pre-existing grammar leak (§4). I did not touch it.

### 1.3 The re-hit counter booked periodic ticks as hits and predecessors

**The bug:**

- `RehitTracker` kept only `KindStatus` entries out of the re-hit figures. Two other kinds of periodic tick were still hits and predecessors:
  - Mirror-clone zone pulses (`HitDelivery.Tick` through `ResolveIncomingHit`: Einstein's Rift, Cleopatra's Vortex, Shakespeare's Special 2);
  - every environmental loss, periodic ones included (`RisingWaterZone` drown ticks; `StageHazard` DPS payouts).
- So a boss swing after a drown tick read as a cross-source re-hit.
- CM-BOT Decision 6 said `by_kind.hazard` "lets the reader take them out". It cannot: `by_kind` books a re-hit under the second hit's kind, so a hazard predecessor stays inside the mob or boss row.

**The fix:**

1. **Ticks are not hits.** `RehitTracker.Record(..., bool tick = false)`:
   - A `tick: true` entry, and every `KindStatus` entry, is never a hit, never a re-hit and never a predecessor, and is not logged.
   - Ticks are counted apart in `Ticks`, `TickDamage` and `TicksByKind[kind]` (`Ticks`, `Damage`). `StatusTicks` stays as their Venom subset.
   - `ByKind` now holds hits only, so its `hits` sum to `Hits`.
   - The telemetry (`PlaytestTelemetry.OnHeroDamageTaken`) passes `tick: observation.Delivery == HitDelivery.Tick`.
   - `UnobservedHeroHits` is now `hits_taken − (Hits + Ticks)`.
2. **Periodic environmental ticks are marked at the source:**
   - `PlayerController.ApplyEnvironmentalDamage(int damage, bool appliesHitstop = false, bool periodicTick = false)`. The new flag is observation only: it changes no value and only selects the observation's delivery.
   - `ObserveDamageTaken(kind, damage, periodicTick)` maps it to `HitDelivery.Tick`.
   - `RisingWaterZone.TickSubmersion` and `StageHazard.ProcessActiveDamage` pass `periodicTick: true`. Every other caller is a discrete contact and is unchanged: discharges, searchlight strikes, escape catches, debris, rift snaps, cyclic hazards, spikes, Level 7's haul-out, Level 0's scripted kill.
3. **A combat-only figure (the verifiers' corrected fix):**
   - `CrossSource30Combat`, `CrossSource20Combat` and `CrossSourceDamage30Combat` over `CombatHits` (non-hazard hits), plus `KindCounts.Cross30Combat` per kind.
   - These apply the cross-source test with `KindHazard` entries left out on both sides, so they hold the mob, boss and Mirror re-hits alone.
   - **The headline is unchanged in meaning for discrete hazard contacts.** CM-BOT Decision 6 stands for them: a discrete hazard contact is still a headline hit and predecessor.
4. **Report and tools:**
   - The `rehits` block gains `ticks`, `tick_damage`, `ticks_by_kind`, `combat_hits`, `xsrc_30_combat`, `xsrc_20_combat`, `xsrc_damage_30_combat`, `xsrc_combat_pct` and `by_kind.*.xsrc_30_combat`.
   - Each log entry gains `key`, the unique source key. `src` is the label, which two mobs of one type share. With `key`, any other variant, such as "excluding bosses and the Mirror", can be recomputed exactly from the log.
   - Each log entry also gains `xc`, the combat verdict.
   - `by_kind.status` is gone; status ticks are in `ticks_by_kind.status`.
   - `PLAYTEST_RESULT`'s `combat` gains `xsrc_rehits_combat`.
   - `run_playtests.ps1`'s CSV gains `xsrc_rehits_combat`.
   - `compare_runs.py`'s `rehits` table gains `xsrc30c`, `xsrc30c%` (over the combat hits) and `ticks/run`. They read `-` on reports from before this fix.
   - `trace.py`'s `rehits` line ends with `ticks: kind=n`.
   - The tracker's and the report's doc comments and the three tool docstrings are updated.

## 2. Files changed

| File | Change |
|---|---|
| `scripts/FighterSim/FighterSimulationDriver.UltimateTelegraph.cs` | the predicate (§1.1) |
| `scripts/Combat/GlowPresentationController.cs` | `EffectiveTint` and its field comment (§1.2) |
| `scripts/Diagnostics/PlaytestCombatStats.cs` | `RehitTracker`: ticks, `TickCounts`, the combat figures, its doc |
| `scripts/Diagnostics/PlaytestTelemetry.cs` | the tick flag, `UnobservedHeroHits`, the report keys, `combat.xsrc_rehits_combat` |
| `scripts/Characters/PlayerController.cs` | `ApplyEnvironmentalDamage`'s optional `periodicTick` (signature and the one observation line) |
| `scripts/Characters/PlayerController.DamageObservation.cs` | `ObserveDamageTaken`'s delivery mapping |
| `scripts/Environment/RisingWaterZone.cs`, `scripts/Environment/StageHazard.cs` | pass `periodicTick: true` (one line each) |
| `tools/playtest/compare_runs.py`, `trace.py`, `run_playtests.ps1` | the new columns and lines, docstrings |
| `tests/Determinism/FighterPresentationSyncTests.cs` | +1 case and its private helpers, appended; no existing case or helper changed |
| `tests/Unit/StoryUltimateActivationTests.cs` | +1 case, inserted before `Run`; no existing case changed |
| `tests/Unit/PlaytestRehitTrackerTests.cs` | +2 cases, 1 rewritten in place |
| `tests/Unit/PlaytestCombatStatsTests.cs` | +1 case, inserted before `SpawnHeroAsync` |

## 3. Tests

### New (+5)

`FighterPresentationSyncTests` (11 → 12):

- `ACasterKnockedOutMidWindupDropsTheTelegraphOnTheKnockoutTickAndThroughTheFreeze`.
  - **Setup:** two drivers, one with the glow arbiter and one without. P1 is Einstein on a 1-HP pool, from a `Duplicate()` of his `CharacterData` with `MaxHP = 1`; the cached authored resource is never touched. Three stocks, hazards off.
  - **Inputs:** P2 (Joan) walks into reach and P1 accepts the Ultimate. P2 then presses and holds `BasicAttack`, built with `PlayerInputFrame.Create`, and its real string hit 1 KOs P1 inside the wind-up.
  - **On the KO tick:** 321 is still `PhaseWindup`, `RespawnFramesRemaining > 0`, no glyph shows on either driver, `HasUltimateTelegraph` is false and the tint is not red.
  - **Afterwards:** the same holds for 20 more frames (the 12-frame freeze, then the respawn platform), until the sim clears 321 (`PhaseNone`). The hashes of the two drivers match on every frame.

`StoryUltimateActivationTests` (6 → 7):

- `AHardMirrorClonesWindupTintIsTheCanonicalUnblockableRedDespiteItsHeroAura`.
  - The caster is built exactly as the Hard clone: `applyStoryProgression: true`, `applyLegacyUnlockLocks: false`, `UsesFighterUltimateActivation`.
  - Preconditions: `HasHeroAura` is true, and the aura warms the resting tint.
  - On every wind-up frame, `EffectiveTint` and the sprite's `Modulate` are exactly `UnblockableColor` and equal the glyph's colour.
  - After the wind-up, the aura-warmed resting tint returns.

`PlaytestRehitTrackerTests` (5 → 7), pure C#:

- `PeriodicZoneAndHazardTicksAreNeitherHitsNorPredecessors` covers four situations:
  - Level 5's drown tick 20 frames before the boss's swing;
  - a Mirror zone pulse between the boss and a mob;
  - a discrete hazard contact that is still a predecessor;
  - per-kind tick counts, with no hit row for a tick-only kind.
- `TheCombatFigureLeavesHazardContactsOutOnBothSides` covers:
  - a hazard-preceded mob hit, which is a headline re-hit but not a combat one;
  - a hazard after a mob, which is never a combat re-hit;
  - the boss and mob combat re-hits at 30 and 20 frames;
  - `CombatHits` 3, combat 30 / 20 = 2 / 1, combat damage 28, and the per-kind combat counts.

`PlaytestCombatStatsTests` (8 → 9), real frames:

- `ADrownTickIsAPeriodicTickAndAHazardContactIsNoCombatPredecessor`. This is the whole path: `RisingWaterZone.TickSubmersion` → chokepoint → observation → telemetry → tracker.
  - A real drown tick books one tick and no hit, and a mob hit 10 frames later is no re-hit.
  - A discrete `ApplyEnvironmentalDamage(5)` followed by a mob hit 10 frames later is one headline re-hit and zero combat re-hits.
  - `UnobservedHeroHits` is 0.
  - The `combat` summary reads `xsrc_rehits` 1 and `xsrc_rehits_combat` 0.

### Rewritten in place (no count change)

- `PlaytestRehitTrackerTests.StatusTicksAreCountedUnderTheirOwnKindAndKeptOutOfTheHeadline` became `StatusTicksAreCountedApartAsTicksAndKeptOutOfTheHeadline`.
  - Status ticks are now asserted in `TicksByKind["status"]` and `Ticks` / `TickDamage`, and `ByKind` must not hold a `status` row.
  - Every other assertion is unchanged.

### Failing first

- With fixes (a) and (b) reverse-applied, and fix (c) mutated, the four touched suites read **`Failed: 5, Passed: 30, Total: 35`**. Fix (c)'s tests need its new API to compile, so it was reverted behaviourally:
  - `tick` ignored;
  - the combat test's hazard exclusion removed;
  - `periodicTick` ignored in the observation.
- Exactly the five new cases failed, each on its target assertion:
  - "The Unblockable X must be hidden on the knockout tick" (after the "still in wind-up" precondition passed);
  - "The wind-up tint is the canonical Unblockable red, not aura-warmed";
  - the tick verdict;
  - "A mob hit whose only predecessor is a hazard contact is not a combat re-hit";
  - "The drown tick is booked as a periodic tick".
- With the fixes restored, the same suites read 35/35.

### Runs

Each run took `D:/Projects/.ftt-gdunit-lock` first, then drained `testhost` / `Godot*_console`, then ran, then released the lock. `GODOT_BIN` was the console build. One `--headless --import` ran under the lock after the `.godot` seed copy.

| Run | Result |
|---|---|
| `dotnet build FightersThroughTime.csproj --nologo` | succeeded. Only the pre-existing CS8632 (vendored GdUnit) and `KLSG_ECS004` (`FighterTuningComponent`) appeared, on an incremental build. No `KLOTHO_DET002`. |
| The failing-first run above | `Failed: 5, Passed: 30, Total: 35` (as intended) |
| Touched and related suites: `FighterPresentationSync`, `StoryUltimateActivation`, `PlaytestRehitTracker`, `PlaytestCombatStats`, `GlowPresentationController`, `StoryEnvironmentalDamage`, `PuzzleEnvironmentToolkit`, `EraMechanicToolkit`, `Level05Content`, `MirrorParadox`, `CoreMechanicCueCatalog`, `StoryDefenceOrder`, `PlaytestEncounterTelemetry` | **`Passed! - Failed: 0, Passed: 159, Total: 159`**, equal to the 13 suites' `[TestCase]` counts (12 + 7 + 7 + 9 + 10 + 4 + 10 + 20 + 17 + 32 + 6 + 10 + 15) |
| **Full suite** | **`Passed! - Failed: 0, Passed: 3337, Skipped: 0, Total: 3337`**, 1 m 29 s |

The full-suite total is **3332 + 5 = 3337** exactly, and the `[TestCase]` attribute count under `tests/` is 3337.

**Delta: +5.** `FighterPresentationSyncTests` +1, `StoryUltimateActivationTests` +1, `PlaytestRehitTrackerTests` +2 (plus 1 rewritten), `PlaytestCombatStatsTests` +1.

## 4. Not fixed, or left as is, and why

- **The decisive-KO hit-freeze is fixed but not exercised end to end.**
  - The same `IsFallen` term (`Stocks <= 0`) covers it, but no test drives a match to completion through the driver.
  - `RaiseCompletionIfNeeded` → `RecordStatistics` calls `SaveManager.SaveGlobalData()`, which would write the developer's real `global.sav`. `SaveManager` has no scratch-directory seam, and AGENTS.md says suites should not write the real saves.
  - The non-final-stock case covers the KO tick, the freeze and the respawn platform.
- **The hyper-armour glow shares the stale KO tick.**
  - `_presentedHyperArmor` reads `HyperArmorFrames`, which `ApplyStockLoss` also leaves set. So the gold hyper-armour outline (not the red tint, which this fix removes) still shows on the KO tick and through the freeze.
  - This has been so since Package 13. The verifiers marked gating it optional; I left it out to keep the fix to the telegraph.
- **The Hard Mirror clone still wears the gold hero aura.**
  - The aura is a pre-existing grammar leak: AGENTS.md says the aura says "you are the hero", and the clone is not. The verifier's "better still" option was to give the aura only to the campaign hero (`applyLegacyUnlockLocks`).
  - That changes what the Hard clone looks like outside the telegraph, so it is a presentation ruling, not a fix. It is recorded for the closeout.
- **The smother still desaturates the telegraph red.** This keeps the suggested fix's "keep the smother". Today it cannot be reached: enemies never target the clone, a Suppressed caster cannot start an Ultimate, and Fighter refuses Suppression outright.
- **Construct deliveries remain hits**: the Mirror clone's coil arcs, turret bolts and serpent-nest bites (`HitDelivery.Construct`). The scope was `HitDelivery.Tick`, and constructs are discrete attacks of a body. Note that the nest bites at 6 HP every 1 s.
- **Hazard identity is still a heuristic** (CM-BOT §7): unrecognised hazards share `hazard:env`.
- **The headline still counts discrete hazard contacts** (CM-BOT Decision 6, unchanged). Dropping them from the headline would reverse a recorded decision; the combat-only figure answers the combat-only question instead.

## 5. A harness caveat found on the way (for whoever writes the next driver test)

**`GameManager`'s boot session has `FighterOpponentType = Cpu`.** So every `FighterSimulationDriver` test harness that does not set the session runs P2 as the Normal CPU, and P2's `BufferedInputSource` is ignored. That includes the existing CM-ULT telegraph cases.

- Those existing cases still pass, because their P2 never needs to act.
- My first attempt failed for this reason: the CPU rolled away through P1 from the wind-up instead of attacking.
- The new case sets `FighterOpponentType.LocalHuman` and restores the session in `finally`.
- Worth a line in the test-authoring notes.

## 6. For the closeout (docs and ledger; I edited none of them)

**Ledger (`docs/design-contracts/DESIGN_BUILD_DEVIATIONS.md`):**
- **`PROVISIONAL-ULT-TELEGRAPH-LOOK`** (CM-ULT's new row): add that the Fighter telegraph is derived from a live, free caster (not fallen, not held), so it drops on the KO tick. Add that the telegraph tint is never warmed by the hero aura (a Hard Mirror clone's included); the smother still applies.
- **`PROVISIONAL-REHIT-COUNTER-RULES`** (CM-BOT's new row):
  - Extend Decision 5 to every periodic tick: status, zone pulse (`HitDelivery.Tick`), and periodic environmental ticks (drown, stage-hazard DPS, marked by the chokepoint's observation-only `periodicTick`).
  - **Correct Decision 6.** `by_kind` books by the second hit's kind, so it removes hazards only as the second hit, never as the predecessor. The combat-only figures (`xsrc_30_combat` etc.) remove them on both sides.
  - **Correct Decision 2's** "excluding bosses and the Mirror" claim in the same way. That variant is now recomputable exactly from the log's `key`.
  - Record the new keys (§1.3 item 4).
- **`USER-BOT-COMBAT-TELEMETRY`:** the CSV's `xsrc_rehits_combat`, `compare_runs.py`'s `xsrc30c` / `xsrc30c%` / `ticks/run`, and the suites' new counts (`PlaytestRehitTrackerTests` 7, `PlaytestCombatStatsTests` 9).
- **`USER-STORY-MERCY-MEASURE-FIRST`:** the 7.2 after-batch should read `xsrc_30_combat` / `xsrc30c%` beside the headline. Batches recorded before this fix have zone and periodic-hazard ticks inside `hits` and `xsrc_30`, so compare across the fix only on the combat figure from new reports.
- **Optional new rows:**
  - `VERIFY-FIGHTER-KO-HYPERARMOR-GLOW-TICK` (§4, the gold outline on the KO tick);
  - `VERIFY-MIRROR-CLONE-HERO-AURA` (§4, the Hard clone's aura).

**CM_BOT.md:** Decisions 2 and 6 are superseded as described above. I did not edit another workstream's handoff.

**CLAUDE.md**, "Automated Story playtests (bots)", where CM-BOT's own closeout lines are owed:
- the `rehits` bullet: periodic ticks are apart (`ticks`, `ticks_by_kind`); the combat-only figures; the log's `key` / `xc`;
- `PLAYTEST_RESULT`'s `combat.xsrc_rehits_combat` and the CSV column;
- `compare_runs.py`'s new columns and `trace.py`'s ticks line;
- optionally, §5's harness caveat near the test-authoring footguns.

**AGENTS.md:**
- the baseline: +5, so **3332 → 3337** (the suites above);
- the CM-ULT A02 bullet could say "for exactly the wind-up of a live caster, never aura-warmed";
- the `scripts/Diagnostics/` map line could mention the combat-only re-hit figure.

**Design master:** nothing owed. The telegraph colour rule already says Unblockable red; the bot telemetry is not in the GDD. **Contracts:** none.
