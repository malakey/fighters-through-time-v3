# CM-FIXB-CPU handoff: the Phase B fix round, Fighter CPU half (combat and movement update, 2026-10-16)

This is fix round B of `docs/COMBAT_MOVEMENT_UPDATE_2026-10-16_PLAN.md`, workstream **CM-FIXB-CPU**. It covers the verified findings under the `CPU` key of `D:/Projects/genre_review_2026-10-13/fixB_inputs.json`: B0 and B20 (one defect, found twice), B3, B9 and B13. Where a checker's corrected fix narrowed the finder's, the corrected fix was preferred.

- Branch: `worktree-wf_a15de89f-42d-2`
- Worktree: `D:\Projects\Fighters Through Time - V3\.claude\worktrees\wf_a15de89f-42d-2`

**Base.** The worktree was created clean at `d2e52e0`, not at merged Phase B. Before any edit it was reset to `740d712` (main = Phase A + Phase B, 3490 tests), the commit the task names.

**Commits, in order:**
- `f6cf1cc` — `CM-FIXB-CPU: the CPU's hitstun hold, Defy reserve, hang DI and in-cast recovery presses` (production code and tests)
- the commit that adds this handoff (the branch head)

**Scope.** Ruling 9.3 leaves the CPU's AI alone, so every fix is a delivery defect: an input the CPU's own planner had already decided, which its delivery then destroyed or charged wrongly. No new behaviour was added.

What changed:
- input-side code in `FighterCpuController`;
- one new field on the mode-neutral `CpuVerbState`, filled by `FighterSimulationWorldObserver`;
- one pure predicate split out of `FighterDefenseRules.CanDefy`, so the CPU and both damage paths read one Defy rule. It is hash-neutral: `CanDefy` still evaluates the same three terms.

What did not change: no Klotho component, snapshot field, protocol field, save field or `en.csv` key, and component 323's bits are untouched. No Story file changed. The Level 13 Mirror adapter is unchanged: it shares the controller, but it has no verb block and no stage bounds.

**CPU-driven Fighter hashes move.** Human-only matches are bit-identical, because the sim rules did not change.

**Nobody has played it.** Every claim below comes from a headless suite.

## 1. Findings fixed

| ID | Finding | Fix |
|---|---|---|
| B0 / B20 (medium) | The hitstun-defense Block hold (Normal 45 %, Hard 85 %) stays **exclusive** until the CPU is grounded. Every recovery Jump and movement-ability press is eaten, and so is a hanging CPU's climb jump. A defended CPU whose hitstun ends over a pit falls into the blast zone. One that the ledge catches after hitstun hangs, holding Block only, until the 300-frame auto-release. The eaten activation was still charged to Easy's and Medium's one per episode. | Hitstun over and airborne or hanging → `Block \| (held & (Jump \| MovementAbility))`. Activation charged at emission, not delivery. |
| B3 (low) | `SelfDefyAvailable` read only the spent flag. In Sudden Death, F22 *bars* Defy without spending it, so Hard kept declining Echo Step to reserve a full meter for a Defy that can never fire. This breaks `CPU_COMBAT_POLICY.md:35`, which says a mode-disabled Defy "has no reserve value". | `CpuVerbState.DefyBarred` from component 312. `SelfDefyAvailable = FighterDefenseRules.IsDefyUnspentAndUnbarred(...)`. |
| B9 (medium / low) | The fa18442 climb-jump-only override zeroed the stick on every `IsLedgeHanging` tick. A hit on a hanging fighter leaves the hang set through its whole hitstop, so the thaw tick's DI read (`ResolvePendingLaunch`) got 0, and an edge-guard launch resolved with no DI. This was a regression from fa18442. | The override now runs only when `HitstunFrames <= 0 && DazeFrames <= 0`. |
| B13 (medium / low) | The recovery planner's movement branch did not read the CPU's own Special cast. A press delivered inside the cast was discarded by the sim, but still charged to the episode, so Medium had only its jumps left once the cast ended. | `movementReady` also requires `CastIdleAtDelivery`. A planned recovery movement-ability or jump press delivered while `CastHoldsThisTick` holds is dropped, uncharged. |

### How (all in `scripts/FighterSim/FighterCpuController.cs` unless named)

**B0 / B20.** This follows the orchestrator's wording: "keep Block for the tech; once hitstun is over let the planner's Jump / movement-ability edges and the jump sustain through, keep masking BasicAttack and Roll".
- **The pass-through.** `ApplyHitstunDefense` gains one branch ahead of the exclusive hold. While the hold is active and `hitstun <= 0 && IsGrounded == 0`, it returns `Block | (held & PostHitstunAirborneButtons)` (`Jump | MovementAbility`) and leaves the sustained hop running.
  - It covers hanging as well as airborne, because `FighterLedgeRules.Process` sets `IsGrounded = 0` on every hang tick.
  - Jump is read before the Block-gated ledge roll and ledge attack, so a hanging CPU climb-jumps.
  - BasicAttack and Roll stay masked, because Block plus either one is the grab or Echo Step chord. Specials stay masked as before.
  - During hitstun, and through the grounded 45-frame escape linger, the hold stays exclusive and unchanged (the hit-2 escape and the tech).
- **The charge.**
  - At delivery, a `SpendsMovementActivation` press no longer increments `_episodeMovementActivations`. Instead it sets `_movementActivationAwaitingEmission`, and `MovementActivationsAllowed()` counts that flag like the planned marker, so the cap still holds while the press waits.
  - `AccountMovementActivationEmission(held, previousFrame)` runs after the final output buttons are formed. It charges the activation on the tick MovementAbility goes out as a pressed edge.
  - It releases the flag uncharged if the press leaves `_activeEdges | _pendingEdges` without going out: masked by the hold, or overwritten by a later delivery.
  - Episode start and end clear the flag.

**B3.**
- `scripts/FighterSim/FighterEntitySystems.cs`: `FighterDefenseRules.IsDefyUnspentAndUnbarred(int defyHistoryUsed, int defyBarred)`, which `CanDefy` now calls for its first two terms.
- `scripts/FighterSim/CpuDecisionObservation.cs`: `CpuVerbState.DefyBarred`, which `TryGetVerbState` fills through `TryGetFighterDefense`. A missing component reads 0, unbarred.
- `FillVerbLayer` sets `SelfDefyAvailable` from the shared predicate. The meter term is left out because `TryDecideEchoStep` already tests the meter separately.

**B9.** The hang override in `Sample` gains `observation.HitstunFrames <= 0 && observation.DazeFrames <= 0`. This is safe: a fighter in hitstun or daze never reaches the hang branch, because the movement system's struck-off-the-ledge path runs first on the first unfrozen tick, and the hitstop branch never reads get-ups. So nothing a struck CPU holds can start a neutral climb, ledge roll or ledge attack.

**B13.**
- `DecideRecovery`: `movementReady` also requires `CastIdleAtDelivery(in observation)`. This is the band-minimum predicate the Special and Ultimate gates already read, and it adds no RNG draw.
- At delivery, inside the existing `CastHoldsThisTick` strip: a `SpendsMovementActivation` delivery loses MovementAbility, and a `RecoveryJump` delivery loses Jump. The jump strip stops a dead edge from occupying `_pendingEdges` over a pending movement edge.
- The stripped press is never charged (see the B0 charge), and its marker is released, so the planner can plan it again once the cast ends.
- Stripped rather than kept for a D3 cancel, as the checkers advised: an uncounted D3-fired activation would break Medium's cap, and deliberate D3 is outside 9.3.

## 2. Tests

**+6, none rewritten** (3490 → 3496). All six fail on `740d712`. This was checked by restoring the three pre-fix sources over the new tests, building, and running the three suites.

The pre-fix failure messages:
- *"movement ability pressed inside the CPU's own cast on ticks 56"*
- *"A Defy Sudden Death bars has no reserve value, but the observation read it as available."*
- *"no Jump or movement-ability press after hitstun … the CPU fell into the blast zone"*. Eight of nine runs lost the stock; the ninth drifted back onto the floor without a press.
- *"left the ledge after 300 frames without a climb jump"* ×3
- *"The thaw tick's stick was 0"*
- *"The masked press was charged to the episode while hitstun still ran."*

| Suite | Case | What it pins |
|---|---|---|
| `FighterCpuHitstunDefenseTests` (pure C#) | `AHundredPercentDefenseCpuLaunchedOverAnOpenStagePitRecoversOnceItsHitstunEnds` | Real sim on Nassau. A vector launcher sends a default Joan off the stern ledge, and her 30-frame hitstun ends airborne over the gap. A Hard CPU with 100 % defense and 0 % DI drives her through the real world observer. It must press Jump or MovementAbility after hitstun, must not lose the stock, and must land. Three launches × three seeds. |
| | `AHundredPercentDefenseCpuCaughtOnAPitFacingLedgeAfterHitstunClimbJumps` | The same harness with a shorter launch: the stern ledge catches the CPU after its hitstun, with the hold active. It must leave by a climb jump (vy > 0) within 30 frames, with no get-up. Three seeds. |
| | `AHangingCpuStruckByALauncherKeepsItsDiHoldThroughTheThaw` | B9. A scripted walk-off, then a hang held with a neutral stick until the first-grab intangibility runs out; the attacker then launches it. The 100 %-DI CPU's stick is toward the stage on every frozen tick and on the thaw tick. The resolved velocity is rotated away from the stashed launch (x 2.94 vs 4.16 in the probe). Inputs are pinned with `PlayerInputFrame.Create`. |
| | `AMovementActivationTheDefenseHoldMasksIsNotChargedToTheEpisode` | Hand-built observations. A control CPU with the same seed finds the tick its recovery movement press goes out. The CPU under test is put in hitstun on exactly that tick, so the hold masks the press. Nothing is charged while hitstun runs, a press goes out after it, and exactly one activation is charged. |
| `CpuSelfActionStateTests` (pure C#) | `ARecoveryMovementAbilityIsNeverSpentInsideTheCpusOwnCast` | B13. Real sim on Nassau. The fighter's walk, jump-out, double jump and Special 1 press are pinned (a 10 / 4 / 30 cast). A Medium CPU then drives through the real world observer. There is no movement press on any tick the cast holds, no activation is charged during it, and a movement press goes out after it. |
| `CpuVerbPolicyTests` (pure C#) | `HardHoldsNoMeterForADefyThatSuddenDeathBars` | B3. A real one-second match ties into Sudden Death, and the real world observer reads `SelfDefyAvailable` 1 in regulation and 0 in Sudden Death. With the observation turned into a full-meter whiff, Hard (admission forced to 100) Echo Steps in Sudden Death and holds its meter in regulation. |

**Runs:**
- The touched and related suites, 211/211: the six suites above, plus `CpuRecoveryMatrixTests`, `FighterCpuBehaviorTests`, `FighterCpuControllerTests`, `FighterCpuSpecialTimingSmokeTests`, `MirrorParadoxTests`, `CpuMeleeBandTests`, `FighterLedgeTests`, `FighterHitPipelineTests`, `DefyProtectedRecoveryTests`, `FighterMatchFlowTests`, `FighterTimeSystemsTests` and `CpuRecoveryProfileTests`.
- One full run with the lock held and orphans drained: `Passed! - Failed: 0, Passed: 3496, Total: 3496` (1 m 37 s), exactly 3490 + 6.
- `dotnet build`: 0 errors, with only the two pre-existing warnings (`CS8632` in the vendored GdUnit API, `KLSG_ECS004` on `FighterTuningComponent`). No `KLOTHO_DET002`.

The real-sim scenarios were tuned in a scratch console outside the repository (`%TEMP%\cmfixb_probe`, referencing the built assembly). It is not part of the branch.

## 3. Choices made, and what is not fixed

1. **The pass-through applies whenever the CPU is airborne or hanging after hitstun, not only over a gap.** This follows the orchestrator's wording; two checkers would have limited it to hanging or offstage. Over a Sealed floor, a defended CPU can now take the planner's own Jump or movement press out of a post-hitstun tumble (3.2 allows it). Block stays held, so a landing it does not act out of is still teched.
2. **The activation is charged at emission, not at delivery.** A press is usually emitted on its delivery tick, or one or two ticks later behind the edge gap, so the cap timing barely moves. What changes is that a press masked or overwritten before it goes out is no longer charged.
3. **Not fixed, out of the listed findings:**
   - **Recovery press delivered into hitstun without the hold.** A CPU that rolled no defense still has its press go out to the sim, which refuses it in hitstun, and the press is still charged. This is pre-existing, and is the same class as B0 for a non-defending Easy or Medium CPU that is edge-guarded mid-recovery.
   - **Combat movement press into the CPU's own cast.** A *combat* (non-recovery) movement-ability press can still arrive inside the CPU's own cast and be discarded uncharged. The probe saw one at x = 5.98, over floor. That press costs nothing and is a CM-CANCEL-class gap, not a charge.
   - **Hang-tick charge into a closed episode.** A recovery press planned before a ledge grab and emitted on the hang tick is charged to an episode the grab has already closed. This is harmless: the next episode starts at 0.
4. **The 45-frame linger after a defended climb.** A defended CPU that climb-jumps and lands still runs the existing 45-frame grounded escape linger, holding Block exclusively. That is V7.4 behaviour and is unchanged.

## 4. What the closeout owes

- **AGENTS.md / CLAUDE.md:** the baseline 3490 → 3496 (this branch alone; reconcile with the other fix-B branches), and a line on the CPU's post-hitstun pass-through under the V7.4 CPU hitstun-defense bullet. That bullet currently says the hold lasts "through the hitstun, the tumble, ground contact, and a 45-frame grounded linger".
- **Plan §2.3.1:** the "CPU" paragraph's premise ("its hitstun-defense Block hold already lasts until grounded, so it techs") is true but incomplete. Record that the hold now lets the planner's Jump and movement presses through once hitstun ends (CM-FIXB-CPU).
- **Ledger rows (closed):**
  - B0/B20: pre-existing since V7.4 / Package 11 A9b; exposed by 3.2 and 5.2.
  - B3: pre-existing Package 11 A9b deviation from `CPU_COMBAT_POLICY.md:35` and :39.
  - B9: a regression from fa18442.
  - B13: inside 9.3's scope.
- **Provisional rows:** the agent choices in §3 (1)–(2), and the open item in §3 (3), the uncharged-in-hitstun gap for a non-defending CPU, if the user wants it tracked.
