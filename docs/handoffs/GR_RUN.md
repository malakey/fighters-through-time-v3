# GR-RUN handoff: the running attack, both modes (rulings 9b and 15)

**Nobody has played or looked at this.** Every claim below is a headless suite. Nobody has judged the lunge, the 9-frame wind-up at a run, whether `basic_attack_2` reads as a running strike on each hero, or how often players will now get it instead of hit 1.

## What shipped

Attack pressed while running is one committed lunge outside the three-hit string, in both modes, with no new input bit.

- **Selection.** `BasicComboRules.SelectAttackVariant(upHeld, downHeld, airborne, running = false)` returns `VariantRunAttack = 4`. Priority: Up > Down-Air > air strike > run attack (grounded, running, Down not held) > chain. The grab chord is tested before the swing in both modes, so it still wins.
- **Who states "running".** Only a fresh press from neutral. A D3 recovery cancel and a chain-hold press pass nothing, so held movement still never breaks the string.
  - Story: `PlayerController.IsRunningForAttack` reads `BasicComboRules.IsRunningForAttack(inRunningState, speedAlongFacing, maximumRunSpeed)`: the `Running` state and speed along the facing at 60 % or more of `EffectiveMoveSpeed × StatusMovementMultiplier`.
  - Sim: `FighterBasicAttackRules.IsRunningForAttack(in fighter, in runtime, maximumSpeed)`: grounded, `MoveX` beyond 30 toward the facing, velocity along the facing at 60 % or more of `MoveSpeed × status × speed buff`, read before the tick's movement. **This is the exception ruling 15 records: the sim has no Running state.**
- **The strike.** A single strike like the air strike: `FlagRunAttack = 1024` in `VariantMask` on component 305's `AttackFlags` (confirmed free by grep), so no chain, no buffer, no hold and `NextChainStep` 0. `StartSwing` / `StartComboHit` arm the lockout and clear the D1 latch as every swing does; the recovery cancel set applies.
- **Motion.** The startup steers like any swing. On the ground the active frames write the lunge speed along the facing, whatever the stick says, and the recovery brakes on the stop ramp with the stick ignored. Facing is locked from the first active tick. Off a ledge the motion stops being forced (Story `ApplyAirControl`, sim air control).
- **Box.** String hit 1's with the far edge out by 0.4 u and the near edge unchanged: `FighterCombatSystem.ResolveRunAttackBox` and `PlayerController.ResolveRunAttackBoxPixels`, pinned equal at × 60.
- **Hit.** Sim `FighterCombatSystem.ApplyRunAttackSwing` (`FighterCombatSystem.RunAttack.cs`) on the air strike's model; the construct sweep reads its damage and box. Story `StartRunAttack` (`PlayerController.RunAttack.cs`, hitbox ID `BasicComboRules.RunAttackHitboxID`, `run_attack`).
- **Story mobs.** `IsBasicStringHitbox` includes it, so S4, the 8.1 floor, D1, the freeze buffer, Momentum, the synced strikes and S2's chain decay read it. S5: `BasicStringStunFloorFrames` gives it hit 1's floor. There is no finisher carry and no wall bounce.
- **Momentum.** `MomentumGainUnitsFor(VariantRunAttack, 0)` and `MomentumGainUnitsForHitbox("run_attack")` both give `MomentumDirectionalGainUnits`, by name, so GR-MOM's values flow through.
- **Pose.** `BasicComboRules.RunAttackPoseStringStep` = 1 (`basic_attack_2`) in `StartRunAttack` and `FighterSimulationDriver`.
- **Move list.** A `RunAttack` row, key `movelist_run_attack`, in both modes.
- **Telemetry.** `PlaytestCombatMath.HitVariant` returns `run_attack`, so `swings.by_variant` does not book it as `other`.
- **Plumbing.** `ResolveSpecialCastRegion` now returns true when a D3 cancel fired; `ProcessBasicAttackPhase` gained `bool running = false`; `SelectVariant` gained the same.

The CPU is unchanged. It gets a run attack only when it presses at a run, and its melee band still reads hit 1.

## Provisional numbers (`PROVISIONAL-RUN-ATTACK-RULES`)

From the plan: startup template 9, Joan 7, Lincoln 11, Einstein and Shakespeare 10; active 6; recovery 22; 1.2× damage; hitstun 30; non-launching; knockback × 1.5; lunge 0.5 u; reach +0.4 u; 60 % speed floor; Momentum +0.05.

Chosen by this workstream:

| Choice | Value | Why |
|---|---|---|
| Stick threshold (sim) | `RunAttackStickThreshold` 30 | The quantized scale's "Up held" threshold |
| Lunge shape | 5 u/s for the 6 active ticks (derived, `RunAttackLungeSpeedUnits`) | Exactly 0.5 u. Below a full run for Joan's and most kits' run speeds; a slow scales it, Root stops it |
| Committed from the first active tick | Stick ignored, facing locked, recovery brakes | The dossier's "then brake" |
| S5 floor | Hit 1's, 34 frames | A run attack plus a whole string charges 136 frames of poise, inside the 139.5 budget. The S5 formula with its margin would give 40 and trip it at the finisher |
| Hitstun escape | Block-cancelable (`blockCancelableHitstun: true`; Story's gate keys on `combo_1`) | As every other single strike |
| Knockback vertical (Story) | −1, hit 1's | A shove, not a launch |
| Reach bonus and scales | Flat in the profile's width; scaled by Story's AttackRange minor | So a minor still moves the far edge by its full percentage |

## Test delta: declared +25, actual +26

New suites and cases:

| Suite | Cases |
|---|---|
| `RunAttackRulesTests` (new, pure) | 5 |
| `FighterRunAttackTests` (new, pure) | 7 |
| `StoryRunAttackTests` (new) | 8 |
| `FighterSpecialRecoveryCancelTests` | +1 |
| `FighterPresentationSyncTests` | +1 |
| `FighterConstructAttackTests` | +1 (the plan did not declare a construct case; it is the extra one) |
| `StringHitboxGeometryTests` | +1 |
| `BasicStringProfileTests` | +2 |

Changed in place, no count change:
- `FighterBasicStringParityTests.GroundedSwingKeepsSteeringAndFacingFollowsTheHeldDirectionMidString`: it pressed at a full run, so it had silently become a run-attack test. It now presses after 3 ticks of a push, under the floor, and asserts the opener.
- `MoveListScreenTests.TheDisplayedHitOneStartupEqualsTheRulebookProfile`: it also pins the new row.

Every new case was seen failing, against mutations of the finished code rather than before it was written. Selector off, the constants and motion removed, the priority and predicates broken, the grab gates removed, a chain-hold press selecting the run attack, and the construct damage flattened each failed the cases that claim them. One limit: `ABasicOutOfALandedRecoveryIsNeverTheRunningAttack` only fails with the D3 guard removed **and** the sim's speed term removed, because a cast's brake already leaves the speed under the floor. The guard is a second lock, and the case does not prove it alone.

## The fixtures the plan expected (10–20)

Only one needed changing on this base. I logged every run-attack start with the calling test across 1830 cases (the Determinism namespace plus Story, Mirror, Calibration, Playtest, Enemy, Feel and Reach suites), then removed the log.

| Caller | Starts | Judgement |
|---|---|---|
| `RollbackReadinessTests` (4 cases) | 2580 | Spam scripts; more coverage, no assertion on the variant |
| `RollbackProtocolTests.DelayedPeerInputsRollbackAndConvergeWithinSevenFrames` | 33 | Same |
| `FighterSimulationTests` (2 hash-identity cases) | 26 | Same |
| `CpuRecoveryMatrixTests.CpuLedgeDrill` | 1 | The CPU pressed at a run in a drill; the assertions are about recovery |
| `FighterBasicStringParityTests` | 1 | The fixture above, fixed |

Not sure of: suites outside that filter (the level content suites, and anything driven by engine frames, where no test appears in the stack). They need the orchestrator's full run. **GR-MOM, GR-TRAITS and the others are not on this base**, so any fixture they add that runs and presses will show up only at merge. Two tests expect the Momentum names to differ only by value; neither pins a number.

## Effects

- **Hashes:** every Fighter run in which a fighter presses at a run moves. `AttackFlags` is hashed.
- **Save and protocol:** none. Schema v8, protocol v5, no component ID. `KLOTHO_DET002`: none; a full rebuild shows `CS8632` alone.
- **Bots:** the `play` and `spam` bots press while running, so they now throw run attacks. Batches from before and after are not comparable. `swings.by_hit` and `by_variant` gain `run_attack`; `compare_runs.py` was not touched.

## Deviations from the plan

1. +26 against +25: the construct case.
2. `scripts/Diagnostics/PlaytestCombatStats.cs` gets one line, outside the plan's file list, so the telemetry names the strike.
3. No hunk in `EnemyController.cs`: S4 and S2 read it through `IsBasicStringHitbox`, and S5 through the rule.
4. The S5 floor is hit 1's rather than the formula's 40 (see the table).

## What did not ship

- No new art. The pose is a reuse, and the visual check per hero is owed.
- No Mirror or CPU awareness beyond what inheritance gives.
- No Story D3 test of its own: Story's D3 calls `StartBasicSwing` with no `running` and is never in the `Running` state.

## What the docs owe (GR-DOCS)

- **Ledger:** `USER-RUNNING-ATTACK` (9b); `PROVISIONAL-RUN-ATTACK-RULES` (the tables above); the sim predicate as a recorded exception under `USER-STORY-LEADS-UNTIL-ONLINE`.
- **`AGENTS.md`:** the selection order and a running attack sentence in the basic-combo bullets; 305's `AttackFlags` 1024 in the component table; the test arithmetic.
- **`CLAUDE.md`:** bot batches across this change are not comparable; `HitVariant` gained `run_attack`.
- **`design-godot.md`:** a running attack row wherever the air strike and directional strikes are stated.
- **Orchestrator:** `movelist_run_attack` in `localization/en.csv` needs compiling into `en.en.translation`. `ScriptTranslationKeyTests` and `MoveListScreenTests`' display fail on a raw key until then.
