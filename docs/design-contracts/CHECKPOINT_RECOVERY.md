# Checkpoint recovery budgets — F11 Option A

> **Amended 2026-09-28 (story review S27):** the per-character Legacy Level (4A) is retired, and with it F04 (the Nexus Resonance Source) and F12's two-checkpoint 4A structure. Every rule below that applies only to 4A, F04 or the Legacy checkpoint contract is history, not design of record; the shared-level rules are unchanged. The ending counts Levels 2–15 (14 IDs).

Decision: 2026-09-12. Timer-collapse recovery uses an authored minimum for each checkpoint and difficulty, based on the remaining mandatory route plus a safety margin. **25% remains the lower bound.** This supplements [Timeline Integrity](../design-godot-v7.md) and [F10 persistence](STORY_PERSISTENCE.md). It does not change the normal F01 drain formula.

## Calculation

For each enabled recovery anchor, including the entrance, author:

- `remainingRouteSeconds`: conservative live time from that anchor to the pre-boss clock lock using F10's reconstructed encounter/puzzle state.
- `safetyMarginFraction = 0.20`: provisional initial tuning value, adding 20% to that route time. Increase where measured retry variability warrants it; reductions require evidence.
- `budgetVersion`: version of the level geometry, encounter layout, par and recovery assumptions used for validation.

Use the existing full-level `parSeconds` and difficulty multiplier (Easy 2.0, Normal 1.5, Hard 1.2). **V01a:** shared-level par is the slowest eligible hero's median Normal required-route time with the normally unlocked kit and no purchased upgrades, rounded upward; each distinct 4A route has its own benchmark. See [campaign route validation](CAMPAIGN_VALIDATION.md). This shared Normal par does not replace the separately measured remaining-route time for each checkpoint/difficulty or its depleted-resource baseline:

```text
allAliveDrainPerSecond = 100 / (parSeconds * difficultyMultiplier)
requiredRecovery = ceil(allAliveDrainPerSecond * remainingRouteSeconds
                        * (1 + safetyMarginFraction))
recoveryMinimum = max(25, requiredRecovery)
timerRecoveryIntegrity = max(checkpointIntegrity, recoveryMinimum)
```

All gauge values are percentage points; round the calculated requirement upward to a whole point. Require positive par, nonnegative route time, and a finite nonnegative margin. **A calculated minimum above 100 is an invalid content budget**, not a value to silently clamp to 100 and declare safe. Adjust the measured route/checkpoint/timer budget and revalidate before sign-off. Missing measurements are pending coverage, never an implicit approved 25%.

The all-alive normalized rate is an upper bound under F01: persistent broken Extractors and found secrets can only reduce future drain. Consequently optional detours are not required to make this recovery budget work, and load does not recalculate the fixed starting-Extractor denominator. Retaining a saved checkpoint gauge above the minimum never lowers it.

## What counts as the remaining route

Include required traversal, combat, mandatory puzzles, waits for moving mechanisms, and Collapse Tremor delays. Measure the reconstructed encounters a retry actually faces, including respawned enemies whose rewards are already claimed. Use a conservative reachable unsolved-puzzle baseline; previously solved gates may shorten the actual retry but cannot invalidate the budget's required access mechanisms.

Exclude optional exploration and the boss fight itself; the endpoint is the pre-boss lock, including any required approach/activation time. Exclude frozen menus/dialogue/death presentations. Time spent travelling during Time Freeze counts as live time because Integrity drains during it.

Validate with the level's normally unlocked kit, no purchased grid perks, zero Ultimate meter, spent one-time healing, and unavailable Time Freeze/other cooldowns as permitted by the F10 recovery state. Include any mandatory cooldown wait in the route time. F04's required Ultimate puzzle uses its Nexus source. A legal grid build must not make the required route impossible; use the slowest relevant character/route result for shared levels and each character's own 4A route. This does not promise completion after unlimited extra mistakes or waiting.

Validate every enabled anchor/difficulty pair. An inert Hard middle checkpoint is not a recovery destination; evaluate the earlier actual anchor and its longer remaining route. Act III's enabled Hard middle anchors get their own budgets. The pre-boss anchor has a locked clock and no timed route requirement. Levels without an Integrity timer are not applicable. F12 defines 4A as Entry plus PreBoss on every difficulty, with no Middle. Its Entry budget covers the full required approach; PreBoss is untimed. Use the [Legacy checkpoint contract](LEGACY_CHECKPOINTS.md) and verified per-variant scene mappings.

## Application and persistence

Apply the authored minimum **only when a timer-caused Collapse actually grants recovery**. Acts I–II keep the existing fee and hub/checkpoint flow. Act III still needs an anchor charge for a Snap; with none, persist Smothered instead of applying any floor. Neither ordinary loading nor death rewind triggers this grant. Non-timer Collapse/Snap retains its existing checkpoint-gauge allowance.

Resolve the minimum and final granted gauge in F10's recovery transaction before presentation. Save the checkpoint/role, difficulty, budget version, resolved minimum and resulting Integrity; interrupted recovery loads that already-resolved result without recalculating or granting again. Later level-balance updates do not rewrite a committed recovery event. Preserve HP/rewind recovery, spent resources, claims, cooldowns and the 20% dust fee exactly as F10 defines.

A timer recovery begins at or above 25%, so it starts above the 20% Tremor threshold. It can enter Tremor later; the route measurement must include that phase. This is an allowance for paid recovery, not an Extractor time refill or a new in-combat time source.

The existing final-gauge tier, dust bonus and ending-average rules remain. A larger recovery grant can improve final Integrity, just as checkpoint-gauge restoration already can; this decision introduces no separate scoring penalty. Compare deliberate-collapse routes against clean runs before economy/ending sign-off and bring any exploit back for a separate design choice. Do not claim these rewards are neutral to the recovery change merely because the dust fee remains.

## Worked arithmetic — illustrative, not authored level values

Suppose full-level par is 600 seconds and a checkpoint has 240 seconds of remaining mandatory route, using a 20% margin (288 budgeted seconds):

| Difficulty | All-alive drain per second | Calculated requirement | Minimum | Recovery if checkpoint saved 10% |
|---|---:|---:|---:|---:|
| Easy | 0.083333 points | 24 points | 25% | 25% |
| Normal | 0.111111 points | 32 points | 32% | 32% |
| Hard | 0.138889 points | 40 points | 40% | 40% |

If that checkpoint saved 60%, all three recover to 60%. With all machines alive, each listed minimum covers at least the 288-second allowance. Optional destruction only adds slack. A Hard route of 700 seconds at the same par would require 117 points after margin and rounding: invalid, requiring a content/budget fix.

## Authoring record and acceptance

Maintain one row per enabled level/variant/checkpoint/difficulty, recording:

| Required field | Purpose |
|---|---|
| Level/variant, checkpoint ID and role, difficulty, budget version | Resolve the exact supported recovery destination |
| Full-level par, remaining-route measurement and safety margin | Reproduce the calculation |
| Encounter/puzzle baseline, kit/grid/resources and initial machine count | Make retry assumptions reviewable |
| Calculated minimum and valid/pending/invalid status | Reject missing or above-100 budgets |
| Measured completion time, final gauge, Tremor delays and replay/build reference | Verify the route under actual gameplay |
| Deliberate-collapse versus clean-route tier/dust/ending results | Detect unintended incentive to fail |

**No actual checkpoint timings or approved per-level minima are supplied by this document.** Scene manifests and runtime measurements are still required.

Acceptance requires each supported retry to reach the pre-boss lock with positive Integrity under the recorded assumptions; test slow valid routes, depleted resources, relevant roster variants, all-alive and destroyed-machine states, and repeated timer failures. Exercise recovery before/after durable commit and with the last/no Act III anchor. Confirm no floor on ordinary load, no double grant, no boss-clock unlock, correct rounding, and rejection of invalid or missing authored budgets. Review the margin against observed variance rather than presenting it as a universal guarantee.

Status: design rule and illustrative arithmetic specified; authoring, implementation, route validation and scoring-impact tests pending.
