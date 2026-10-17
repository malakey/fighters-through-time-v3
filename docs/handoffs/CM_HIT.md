# CM-HIT handoff — hit reaction and protection (combat and movement update, Phase B)

Combat and movement update 2026-10-16 (`docs/COMBAT_MOVEMENT_UPDATE_2026-10-16_PLAN.md`), workstream **CM-HIT**: decisions 3.2 (the Smash tumble rule, both modes, the Story hero included; Story mobs stay out), 5.3 (the air dodge refreshed when hit, both modes), 4.2 (Fighter respawn protection; the Q8 ruling: Fighter only, both levers), 4.3 (Defy protection ends on the first attack or Special, both modes) and 1.1(4) (a Sudden Death Venom tick respects `DefyBarred`); plan dossiers 2.3.1, 2.5.3, 2.4.2, 2.4.3 and 2.1.4. Plus the fix-round extra from `CM_FIXA_SIM.md` §5 (the grabber's throw-animation invulnerability) and the optional `CM_FIXA_PRES.md` §4 gate (the gold hyper-armour outline on a fallen fighter).

Branch `worktree-wf_a70ba1d0-06f-1`, worktree `D:\Projects\Fighters Through Time - V3\.claude\worktrees\wf_a70ba1d0-06f-1`, base `2d44fa9` (merged Phase A plus its review fixes, **3367** tests).

**History.** A first agent with this brief was stopped by a usage limit; the orchestrator committed its uncommitted tree as one WIP checkpoint (`1b2586f`). I reviewed that diff against every dossier item, built it (0 errors), ran the CM-HIT suite list on it (green), added two in-place/extra test changes, then `git reset --soft 2d44fa9` and recommitted the same tree in three logical commits. `1b2586f` is no longer on the branch.

| # | Commit | Content |
|---|---|---|
| 1 | `2b9214c` | Fighter sim: the tumble, the air-dodge refresh, the respawn curve and record, Defy's early end, `CanDefy`, the throw grant; sim tests; `systems_card_tech_body` |
| 2 | `eddc3bd` | Fighter presentation: spawn glow and "Respawning" from the record, the tumble pose, no gold outline on a fallen fighter; `FighterPresentationSyncTests` |
| 3 | `0f705e2` | Story hero: the tumble latch and its landing, the air-dodge refresh, Defy's early end; Story tests |
| 4 | (this file) | The handoff |

Each of commits 1–3 builds on its own (0 errors).

**Nobody has played the build.** Every claim below is a headless suite or a headless smoke run.

**Fighter hashes move.** No Klotho component or field was added (the plan's reserved values only: 310 `Tumble` value 2 and 323 `ActionFlags` bit 0), and no protocol, save-schema or snapshot-layout change. No suite pins an absolute hash.

## 1. Shipped

### 3.2 — tumble outlives hitstun (both modes)

**Sim** (`scripts/FighterSim/FighterVerbRules.cs`, new `scripts/FighterSim/FighterMovementSystem.HitReaction.cs`, `FighterActionRules.cs`):
- 310 `Tumble` now reads three values: `FighterVerbRules.TumbleNone = 0`, `TumbleInHitstun = 1` (set by `StashPendingLaunch`, as before), `TumbleAfterHitstun = 2`. `FighterVerbRules.IsTumbling(in verb)` is `Tumble != 0`.
- `FighterVerbRules.UpdateTumble(in fighter, ref verb)`, run by `ApplyHitReaction` at the top of every unfrozen movement tick: with hitstun over, a tumble stays a tumble while airborne (value 2) and ends on the ground (0); with hitstun running, a value-2 tumble is ended (a fresh hitstun that re-stashed no launch is a non-launching hit — the M05 rule that a non-launcher on an airborne victim is no tumble). A fresh launcher re-stashes value 1 at hit time. It replaces the old `verb.Tumble = 0` at the hitstun-end block.
- The landing resolution keys on the tumble, not the hitstun: `TryLandingTech`, `FighterKnockdownRules.TryBeginKnockdown`, `FighterSlamRules.ClearIfStale` and `FighterSlamRules.TryBounce` read `IsTumbling` (they read `HitstunFrames > 0 && Tumble == 1`). So a passive post-hitstun fighter lands in the bounce → tech → knockdown chain, and an owed Down-Air bounce survives the end of the slam's hitstun while the fighter tumbles.
- `FighterVerbRules.ExitTumbleOnAction(in fighter, ref verb)`: called first in `FighterActionRules.OnActionAccepted` for **every** `FighterActionKind` (the swing, Special, Ultimate, grab, jumps, roll, air dodge, movement ability, Echo Step, ledge grab, ledge get-up). Keyed on `HitstunFrames <= 0`, so the tick hitstun reaches 0 inside `TickCounters` (still value 1 until the next tick's `UpdateTumble`) exits too. Drift and fast-fall are not actions and keep it.
- A Defy proc on the Venom-tick path (`ApplyUnattributedDamage`) now also clears the tumble, as the hit path's proc already did.
- Gravity is untouched: hitstun keeps `StepLaunch`; after hitstun the fighter is on the normal-control step, as before.

**Story hero** (new `scripts/Characters/PlayerController.Tumble.cs`; call lines in `PlayerController.cs`):
- `_tumbleAfterHitstun`, set at `ProcessStunned`'s expiry when `_stunTumble && !IsOnFloor()`; the hero returns to `Airborne` with control, so Defy's actionable test starts on time as in the sim. Seam: `PlayerController.IsTumblingAfterHitstun`.
- `TryResolveTumbleLanding()`, the first thing `ProcessAirborne` does on ground contact (before the buffered jump, which it clears): the owed slam bounce first (`_stunTumble` re-armed and `TryResolveSlamBounce`, then `Stunned`), else `TransitionTo(Stunned)` and held Block → `BeginLandingTech()` (the 12-frame tech, now one body shared with `ProcessStunned`'s tech) or `BeginKnockdown()`.
- The latch clears in `TransitionTo` to any state but `Airborne` and `Stunned` (so the air dodge, the air strike, a Special, the Ultimate, the movement ability, a ledge grab and death all clear it), in `PerformJump`, in `ApplyStun` (a launcher re-tumbles through the hitstun path), on a Defy proc and on a rewind restore. It animates as `hitstun`.
- Story mobs are untouched (`VERIFY-M05-KNOCKDOWN-SCOPE`). Story throws still never tech (F23): a thrown hero never arms `_stunTumble`, so never the latch. Sim throws launch and tumble; the divergence is kept.

**Presentation:** `FighterSimulationDriver.ResolvePresentationAnimation` plays `hitstun` for an airborne value-2 tumble.

**Copy:** `systems_card_tech_body` (`localization/en.csv`, under `# CM-HIT`): "Hold Block as a launch carries you into the ground for a 12-frame invulnerable recovery; land doing nothing and you are knocked down. Once the hitstun ends you can jump, dodge or attack out of the tumble instead." `en.en.translation` regenerated with `--import`.

### 5.3 — the air dodge refreshes when hit (both modes)

- Sim: `ApplyHitReaction` calls `FighterAirDodgeRules.Refresh(ref knockdown)` whenever `fighter.HitstunFrames > 0` on an unfrozen tick (stateless). Any applied hitstun counts, a throw's and a zone tick's included; a blocked, absorbed, armoured or Defy-zeroed hit applies none.
- Story: `ApplyStun` sets `_airDodgeUsed = false`.

### 4.2 — Fighter respawn protection: 120 frames shrinking to 60, ended by the first offensive action

`scripts/FighterSim/FighterMatchFlowRules.cs` (the flat `RespawnInvulnerabilityFrames = 180` is **deleted**):
- `RespawnInvulnerabilityMaxFrames = 120`, `RespawnInvulnerabilityMinFrames = 60`, `RespawnInvulnerabilityShrinkFrames = RespawnPlatformFrames − RespawnPlatformGraceFrames` (270).
- `RespawnInvulnerabilityFor(int waited) = max(60, 120 − 60 × clamp(waited − 30, 0, 270) / 270)`, integer arithmetic.
- `ProcessRespawnPlatform` (now takes 323) computes `waited = RespawnPlatformFrames − RespawnFramesRemaining` **after** the drop tick's decrement (31 at the first legal input drop → 120; 300 at the auto-drop → exactly 60) and calls `GrantRespawnProtection(ref fighter, ref action, waited)`, which writes the counter and sets 323 `ActionFlagRespawnProtection` (bit 0). On the platform the fighter holds `RespawnFramesRemaining + 120`; with no stocks left, 120 (`FighterSimulationRules.ApplyStockLoss`).
- `IsRespawnProtected(in fighter, in action)` = bit 0 set and `InvulnerabilityFrames > 0`. `ExpireRespawnProtection` drops the bit once the shared counter is 0 (run at the top of each unfrozen movement tick by `ApplyHitReaction`).
- `EndRespawnProtectionOnOffence(ref fighter, ref action)`: called by `OnActionAccepted` for the offensive kinds (`Swing`, `Special`, `Ultimate`, `Grab`; `FighterActionRules.IsOffensive`) while bit 0 is set — zeroes `InvulnerabilityFrames` and clears the bit. Movement verbs keep it (the jumps, roll, air dodge, movement ability, Echo Step, ledge grab, ledge get-up). A ledge get-up's own climb grant must be max-assigned **after** its chokepoint call (CM-MOVE; the `OnActionAccepted` doc says so).
- The Sudden Death setup already zeroes 323, so it clears the bit.
- Presentation: `FighterSimulationDriver.ShowsSpawnProtection(in state, in action)` (pure, public) = on the platform, or `IsRespawnProtected`. Both the "Respawning" label (`GetStateLabel`) and `glow.SetSpawnInvulnerability` read it; a roll, air-dodge, tech, knockdown, throw or Foresight grant no longer reads "Respawning" or lights the spawn aura.

### 4.3 — Defy protection ends on the first attack or Special (both modes)

- Sim: `FighterDefenseRules.EndOnOffence(ref defense)` zeroes `DefyProtectionAwaitControl`, `DefyProtectionFrames` and `DefyProcIdentity`; `OnActionAccepted` calls it for the offensive kinds. `DefyHistoryUsed` stays spent. The `FighterDefenseRules.Tick` doc and the D04 comment in the defense tick were updated.
- Story: `ClearDefyProtection()` is called in `ArmSwingLockout` (every swing start: each chain hit, the air strike, the Up-Attack, the Down-Air), `ArmAbilityLockout` (Special 1, Special 2 and the Ultimate acceptance) and a successful `TryStartGrab`.

### 1.1(4) — a Sudden Death Venom tick respects `DefyBarred`

`FighterDefenseRules.CanDefy(in fighter, in verb, in defense)` = `DefyHistoryUsed == 0 && DefyBarred == 0 && Influence >= FighterMeterRules.Cap`. `ResolveFighterHit`'s Defy condition and `FighterDamageRules.ApplyUnattributedDamage` both read it (the latter lacked the bar). The stale "Sudden Death pre-marks it used" comment is replaced.

### The extra: the grabber's throw-animation invulnerability ends with the throw

`ApplyHitReaction` decrements `InvulnerabilityFrames` while the fighter's `GrabPhase == FighterGrabRules.PhaseThrowAnimation` (the landing tech's pattern; the grab-busy branch still `continue`s past the normal decrement). The 12-frame grant made on the animation's first tick T now reads 12 on T, 11 … 1 on T+1 … T+11, and 0 on T+12, the tick the throw resolves — invulnerable to every combat pass of the animation, hittable from the throw's own tick. Before, it sat frozen at 12 through the animation and ran out about 12 ticks after the throw.

### The optional gate: no gold outline on a fallen fighter

`SyncPresentationFeedback`: `hyperArmor = state.HyperArmorFrames > 0 && !FighterStockLossRules.IsFallen(in state)`. `ApplyStockLoss` leaves `HyperArmorFrames` set, so a fighter knocked out in its armoured Ultimate wind-up wore the gold outline on the KO tick and on the platform.

## 2. Files changed

Production, inside CM-HIT's ownership or declared sites:
- `scripts/FighterSim/FighterActionRules.cs` — the chokepoint body.
- `scripts/FighterSim/FighterVerbRules.cs` — the tumble values, `UpdateTumble`, `ExitTumbleOnAction`; tech, knockdown and slam keyed on the tumble.
- `scripts/FighterSim/FighterMovementSystem.HitReaction.cs` (+ `.uid`, new) — `ApplyHitReaction`.
- `scripts/FighterSim/FighterMatchFlowRules.cs` — the respawn curve, record, grant, expiry and early end.
- `scripts/FighterSim/FighterSimulationSystems.cs` — the one `ApplyHitReaction` call at the hitstun-end block (replacing `verb.Tumble = 0`); `ProcessRespawnPlatform`; `ApplyStockLoss`'s two respawn constants; two comments (the D04 tick, the respawn platform).
- `scripts/FighterSim/FighterEntitySystems.cs` — `FighterDefenseRules.EndOnOffence` / `CanDefy`, the `Tick` doc, the Defy lines in `ResolveFighterHit`, `ApplyUnattributedDamage`.
- `scripts/FighterSim/FighterSimulationDriver.cs` — `GetStateLabel`'s Respawning line, `ShowsSpawnProtection`, the tumble case in `ResolvePresentationAnimation`.
- `scripts/Characters/PlayerController.Tumble.cs` (+ `.uid`, new); `scripts/Characters/PlayerController.cs` — the `ProcessStunned` expiry (and its tech body, now `BeginLandingTech`), the `ProcessAirborne` landing check and animation, `ApplyStun`, `PerformJump`, the `TransitionTo` clear, the Defy proc and rewind-restore clears, and one line each in `ArmSwingLockout`, `ArmAbilityLockout` and `TryStartGrab`.
- `localization/en.csv` (`# CM-HIT`) and `localization/en.en.translation`.

Outside the declared regions (minimal, listed per §1.12):
- `FighterSimulationDriver.SyncPresentationFeedback` — the spawn-glow line (2.4.2 requires `SetSpawnInvulnerability` to read the record, but §3.2 names only `GetStateLabel` and the tumble case) and the adjacent hyper-armour line (the optional gate). CM-BLOCK adds one call line in the same method; the two lines are not adjacent to any obvious call-line spot, so a union merge should be clean.
- The throw-grant decrement lives in `ApplyHitReaction` (my partial), not in the grab-busy branch `CM_FIXA_SIM.md` suggested, so no shared line moved.

Not changed, by decision: `CalibrationDrillRunner.Sample` (its `StudentTumble = verb.Tumble` already reads value 2 as a tumble, because `ObserveLandingTech` tests `!= 0`), and `FighterCpuController.ApplyHitstunDefense` (its Block hold already lasts until grounded, so a CPU techs a post-hitstun tumble; `CpuRecoveryMatrixTests`, `FighterCpuHitstunDefenseTests` and `FighterCpuBehaviorTests` are green unchanged).

## 3. Decisions (provisional; ledger rows named)

`PROVISIONAL-TUMBLE-EXIT-ACTIONS`:
1. Every `FighterActionKind` exits a post-hitstun tumble (the ledge grab and Echo Step included); in Story, every state but `Airborne`/`Stunned`. Drift and fast-fall keep it.
2. The 1 → 2 conversion runs at the top of the next unfrozen tick, so the landing on the tick hitstun reaches 0 still resolves as a tumble landing; the exit keys on `HitstunFrames <= 0`, not on the value.
3. Any fresh hitstun without a fresh launch ends a value-2 tumble (sim), and Story's `ApplyStun` clears the latch for every hit (a launcher re-arms `_stunTumble`). Shield, barrier, armour and Defy absorptions apply no hitstun, so they keep it.
4. Story's tumble landing order is bounce → tech → knockdown, and it clears a buffered jump; a Defy proc ends the tumble in both modes (Venom path included).

`PROVISIONAL-RESPAWN-PROTECTION-CURVE` (the plan's numbers): 120 / 60 / shrinking over the 270 frames after the 30-frame grace; `waited` read after the drop tick's decrement; the platform hold is `remaining + 120`; the record drops at the top of the first unfrozen tick that finds the counter at 0.

`USER-AIR-DODGE-REFRESH-ON-HIT`: any applied hitstun refreshes (throws, zone and status stuns that apply hitstun); the sim refresh is per unfrozen tick while hitstun runs, stateless.

`USER-DEFY-ENDS-ON-OFFENCE`: the offensive set is the same as 4.2's; `EndOnOffence` also zeroes the proc identity (Tick does so at expiry anyway); Story's Echo Step, movement ability, roll, air dodge, jump and block keep it.

The throw grant (new row, e.g. `FIX-SIM-THROW-GRANT-OUTLIVES-THROW`): the grabber is invulnerable from the grant tick through the animation's last tick and hittable on the throw's own tick; a larger unrelated grant on the same counter now also counts down through the animation instead of freezing.

## 4. Deviations from the plan

- **One hit-reaction call, at the top of the unfrozen tick.** The plan placed the 5.3 refresh "before `TickCounters`" and the tumble change at the hitstun-end block. Both, plus the respawn-record expiry and the throw-grant decrement, are one `ApplyHitReaction` call at the hitstun-end block, before the platform hold and before every branch that runs `TickCounters` (the A02 capture, knockdown, tech, grab-busy and normal paths). The effect matches the plan's text on every branch.
- **The driver's `SyncPresentationFeedback`** is touched for 2.4.2's spawn glow and the optional hyper-armour gate (§2).
- **No change to `CalibrationDrillRunner.Sample` or `FighterCpuController`** (§2); the drill rule is pinned by an in-place extension instead.
- **Three tests beyond the declared +20**: the throw grant (the brief's extra, with its regression test), the hyper-armour gate, and a Story owed-bounce case mirroring the sim's (the plan's four Story cases had no bounce row).
- **Known limit (presentation only).** The respawn record survives while a roll or air dodge started inside the protection's last ≤ 8 frames extends the shared counter by max-assignment, so the spawn glow and "Respawning" can outlast the respawn grant by up to that verb's invulnerability. No gameplay effect: those verbs are action-locked while their grant runs, so no offensive action can be accepted (and zero the counter) in the overlap. Fixing it needs a second record or a call after the shared decrement, which is outside my sites.

## 5. Tests

**Declared +20, delivered +23** (+20 declared, +3 extra): **3367 → 3390**. The `[TestCase]` attribute count under `tests/` is 3390 (3367 at `2d44fa9`).

New cases:
- `FighterTumbleTests` (new suite, pure C#) **6**: `APassiveTumbleWhoseHitstunEndsInTheAirKnocksDownAndBlockTechsIt`, `TumbleReadsOneInHitstunAndTwoBetweenTheHitstunEndAndTheLanding`, `EachExitActionLeavesTheTumbleAndLandsPlainly` (air jump, air dodge, air strike, Special 1), `DriftAndFastFallKeepTheTumble`, `AnOwedSlamBounceSurvivesTheHitstunEndAndIsStillForced`, `ADepthSevenRollbackReplaysThePostHitstunTumbleAndItsExit`.
- `StoryTumbleTests` (new suite, Godot runtime) **5** (4 declared + 1 extra): `APassiveHeroWhoseHitstunEndsInTheAirLandsInAKnockdown`, `HoldingBlockTechsTheTumbleLanding`, `TheAirJumpTheAirDodgeTheAirStrikeAndASpecialEachLeaveTheTumble`, `ADefyMidTumbleClearsIt`, and the extra `AnOwedSlamBounceSurvivesTheHitstunEndAndTheNextPassiveLandingKnocksDown`.
- `FighterAirDodgeTests` +1: `BeingHitRefreshesTheAirDodgeSoItCanDodgeAgainAfterHitstun`.
- `StoryAirDodgeTests` +1: `BeingHitRefreshesTheAirDodgeSoItCanDodgeAgainAfterHitstun`.
- `FighterMatchFlowTests` +3: `TheRespawnProtectionShrinksFromOneTwentyAtTheFirstLegalDropToSixtyAtTheAutoDrop`, `ASwingOrASpecialEndsTheRespawnProtection`, `AJumpAnAirDodgeOrARollKeepsTheRespawnProtection`.
- `FighterPresentationSyncTests` +2 (1 declared + 1 extra): `TheSpawnGlowAndTheRespawningLabelShowOnlyForTheRespawnGrant`, and the extra `AFighterKnockedOutInItsArmouredWindupShowsNoGoldOutline`.
- `DefyProtectedRecoveryTests` +2: `TheSurvivorsFirstSwingOrSpecialEndsTheProtectedWindow`, `ARollAJumpOrAnyOtherMovementVerbKeepsTheProtectedWindow`.
- `StoryDefenceOrderTests` +1: `AStorySurvivorsFirstSwingEndsTheDefyWindowButARollDoesNot`.
- `FighterHitPipelineTests` +1: `ALethalVenomTickInSuddenDeathRespectsTheDefyBar`.
- `FighterGrabTests` +1 (extra): `TheGrabbersThrowInvulnerabilityEndsWithTheThrow`.

Rewritten or extended in place (no count change):
- `FighterMatchFlowTests`: `AnyInputAfterTheGraceWindowDropsTheFighterAndArmsTheThreeSecondWindow` → `AnyInputAfterTheGraceWindowDropsTheFighterAndArmsTheFullRespawnProtection` (120 and the record); `AnExpiredPlatformDropsTheFighterAndArmsTheSameThreeSecondWindow` → `AnExpiredPlatformDropsTheFighterAndArmsTheShortestRespawnProtection` (60, then the record drops); `RollInvulnerabilityStaysDistinctFromSpawnInvulnerability` (compares against the 60 floor); `TheFighterIsInvulnerableForTheWholeDissolve` (≥ 120).
- `FighterMatchFlowContentTests.MatchFlowTimingsMatchTheDesignDocument` — pins 2 × and 1 × `TickRate` instead of 3 ×.
- `FighterHazardBehaviorTests.ParisBeamCannotDrainMeterFromAnInvulnerableRespawningFighter` — the knockout is scripted from tick 1700 instead of 1560, so the 300-frame platform hold alone covers the beam's crossing window now that the post-drop window is 60 frames at the auto-drop.
- `DownAirSlamTests.TheBounceNeverRepeatsAndIsDroppedWhenTheTumbleEnds` — a value-2 tumble still owes the bounce; it drops when the tumble ends.
- `CalibrationDrillScriptTests.CatchTheGroundPassesOnATechAndCountsADroppedTumbleAsAMiss` — a 1 → 2 → 0 grounded tumble is a dropped tech; a value-2 tumble that ends airborne (an exit action) and then lands is not.

Shared test files touched (§3.2 map): `CalibrationDrillScriptTests` (in place; CM-BLOCK adds +1), `StoryDefenceOrderTests` (+1; CM-BLOCK in place), `FighterHitPipelineTests` (+1; CM-BLOCK audits), `FighterPresentationSyncTests` (+2). The plan's other in-place candidates needed no change and are green as they stand: `FighterKnockdownTests`, `FighterVerbLayerTests` (`LandingTechEndsTumble…`), `FighterCombatContractW3Tests`, `FighterKnockbackScalingTests`, `StoryKnockdownTests`, `StoryCombatContractW3Tests`, `StoryDownAirSlamTests`, `TutorialCalibrationV76Tests`, `CpuRecoveryMatrixTests`, `FighterCpuHitstunDefenseTests`, `FighterCpuBehaviorTests`, `FighterLedgeTests`, `EnemyControllerTests`, `BossControllerTests`, `MirrorParadoxTests`, `GlowPresentationControllerTests`.

Runs (each holding `D:/Projects/.ftt-gdunit-lock`, `testhost` / `Godot*_console` leftovers stopped first):
- `--headless --import` (after confirming `en.csv`): exit 0; `en.en.translation` unchanged from the WIP's regeneration.
- Filtered, on the WIP tree — `FighterTumble`, `StoryTumble`, `FighterKnockdown`, `FighterVerbLayer`, `DownAirSlam`, `FighterAirDodge`, `StoryAirDodge`, `FighterMatchFlow`, `DefyProtectedRecovery`, `StoryDefenceOrder`, `FighterHitPipeline`, `CalibrationDrillScript`, `TutorialCalibrationV76`, `CpuRecoveryMatrix`, `FighterCpuHitstunDefense`, `FighterCpuBehavior`, `FighterLedge`, `EnemyController`, `BossController`, `MirrorParadox`, `GlowPresentationController`, `FighterPresentationSync`, `FighterCombatContractW3`, `FighterKnockbackScaling`, `StoryKnockdown`, `StoryCombatContractW3`, `FighterGrab`, `FighterHazardBehavior`: `Passed! - Failed: 0, Passed: 385, Total: 385`.
- **Full run** at `0f705e2` (the final code; this handoff is docs only): **`Passed! - Failed: 0, Passed: 3390, Skipped: 0, Total: 3390, Duration: 1 m 32 s`**, reconciled exactly against 3367 + 23.
- `dotnet build`: 0 errors; only the pre-existing CS8632 and `KLSG_ECS004` on `FighterTuningComponent`; no `KLOTHO_DET002`, no `KLSG_ECS004` on 323.
- Smoke (`--headless --quit-after`): `res://scenes/arenas/TestArena.tscn` (600 frames) exit 0, no script error, no leak line; `res://scenes/fighter/FighterStage_Paris.tscn` (300) exit 0, no script error, but `6 ObjectDB instances were leaked` / `3 resources still in use at exit`. **Control:** the same Paris smoke twice at this branch's head and twice with `2d44fa9`'s `scripts/` and `tests/` checked out and rebuilt in this worktree printed the identical two lines every time, so it is pre-existing noise, not this change.

## 6. Measurements

- The respawn curve: `RespawnInvulnerabilityFor` 0 → 120, 31 → 120, 165 → 90, 299 → 61, 300 → 60 (pinned); a match drop after 31 / 165 / 299 platform frames grants exactly those values (pinned).
- The throw grant: 12 on the animation's first tick, counting down 1 per tick, 0 on the throw's tick (12 animation ticks; pinned).
- The tech after a post-hitstun tumble is the same 13-tick grant / 12-tick lock as `CM_FIXA_SIM.md`'s (it runs through the same `TryLandingTech`).

## 7. Not delivered / open

- The presentation limit in §4 (the spawn glow outlasting the respawn grant by a late roll or air dodge).
- Story mobs still have no tumble or knockdown (`VERIFY-M05-KNOCKDOWN-SCOPE`, unchanged by ruling).
- CM-MOVE owns the post-hitstun-rise × short-hop interaction case (plan §2.5.1, risk row 5); with the Q5 own-jump gate a post-hitstun rise is never cut, but that is not pinned here.
- CM-MOVE's ledge get-ups must call `OnActionAccepted(LedgeGetUp)` **before** max-assigning their climb intangibility, or the ledge attack (`Swing`) would zero it.
- The Story Mirror clone runs the same tumble rule (it is a `PlayerController`); `MirrorParadoxTests` is green, and nobody has watched it.

## 8. For the closeout

**Ledger** (write or amend): `USER-TUMBLE-AFTER-HITSTUN` (PFC-04; amends M05/M07 and `VERIFY-M05-KNOCKDOWN-SCOPE`'s text: the Story hero is in, mobs out); `PROVISIONAL-TUMBLE-EXIT-ACTIONS` (§3 items 1–4; the sim/Story throw divergence kept); `USER-AIR-DODGE-REFRESH-ON-HIT` (amends A03); `USER-RESPAWN-PROTECTION` (FGS-14, PFC-19; with the Q8 ruling) and `PROVISIONAL-RESPAWN-PROTECTION-CURVE`, noting the §4 presentation limit; `USER-DEFY-ENDS-ON-OFFENCE` (amends D04); `FIX-SIM-VENOM-DEFY-BARRED` (FGS-18, PFC-21); a row for the grabber's throw-animation invulnerability (found by `CM_FIXA_SIM.md` §5, fixed here); and `VERIFY-FIGHTER-KO-HYPERARMOR-GLOW-TICK` (`CM_FIXA_PRES.md` §8's optional row) recorded as found and fixed.

**Design master and mirror** (`design-godot.md` line numbers at `e2c655a`; none is a `DOC-MASTER-UNBOUND-BINDING` line):
- :1286 (Landing Tech) and :1287 (Launch Flag and Knockdown): tumble outlives hitstun; acting after hitstun leaves it; a passive landing techs or knocks down; an owed slam bounce lasts as long as the tumble.
- FSM :819–820 (the tumble rows): add the post-hitstun airborne tumble (control back; the landing still resolves as a tumble).
- :777 and :829 (the air dodge, "once per airtime"), :996 (hang refills): also refreshed when hit.
- :1323 (Defy, "attacking does not cancel it"): it ends on the survivor's first attack, Special, Ultimate or grab.
- :2147–2149 (Spawn Invincibility, "3.0-second … countdown"): 120 frames at the first legal drop, shrinking linearly to 60 at the 5-second auto-drop, ended by the first attack, Special, Ultimate or grab; FSM :799 ("its separate three-second invulnerability timer"); the glow table :3874 ("Fighter: 3 s after platform release").

**Contracts** (edit both copies, repo and docs-3; all three are identical pairs today except where noted in plan §1.1):
- `DEFENSIVE_EFFECTS.md` :238 ("attacking does not cancel or refresh it") and :264's D04 validation item ("attack without cancelling protection").
- `COMBAT_VALIDATION.md` :32 (the air dodge row's "refresh on landing/ledge"; the repo copy is CRLF, and the pair differs at :37–38 only).
- `FIGHTER_MATCH_RULES.md` does not restate the 3 s window; nothing owed there.

**AGENTS.md:**
- The universal-movement bullet: the air dodge is "once per airtime (refreshed by landing, a ledge grab or a stock loss)" → also by any applied hitstun, in both modes.
- The D01–D04 bullet: D04's 60 active ticks now end early on the survivor's first offensive action (both modes).
- The M05/M07 bullet: tumble outlives hitstun (310 `Tumble` value 2; Story's `_tumbleAfterHitstun`); "Only a launch grants DI, tumble and the landing tech" stays true.
- The 310 inventory row: `Tumble` carries 0 / 1 / 2.
- Known gaps, the Fighter match-flow bullet: "a three-second post-drop invulnerability window" → 120 frames shrinking to 60, ended by the first offensive action; the "Respawning" label and spawn glow read the 323 record.
- The 323 component description (plan §1.5): `ActionFlags` bit 0 `RespawnProtection` is live.
- The test baseline: 3367 + 23 (this branch), with the suites named in §5.

**`docs/handoffs/CM_FIXA_SIM.md` §5** (the throw grant) is superseded by this handoff; that file is historical and was not edited.
