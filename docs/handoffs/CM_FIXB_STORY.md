# CM-FIXB-STORY handoff: Story stance clocks in Time Freeze, the hazard invulnerability gate, Down+Jump out of a swing, and the movement-ability apex hang (Phase B fix round)

Combat and movement update 2026-10-16, Phase B fix round. Branch `worktree-wf_a15de89f-42d-3`, based on `main` at `740d712` (Phase A + Phase B merged, 3490/3490). The worktree was created at a stale commit and reset to `740d712` before any edit.

The findings are the STORY array of `D:/Projects/genre_review_2026-10-13/fixB_inputs.json`: six entries, **B4, B6, B7, B21, B10 and B11**. B7 and B21 are the same defect found by two reviewers, so there are five fixes. Where the verifiers' corrected fix differed from the finder's, the corrected fix was used. No fix changes a user ruling. One agent-written plan choice is reversed: the Time Freeze pause of the Story perfect-block window (plan §2.3.2, `CM_BLOCK.md` line 44). See §1.2 for why that is not a ruling and what the closeout owes.

Every new case was written first and run against the unfixed code: **6 of 6 failed**, each on the defect it pins (messages in §2). With the fixes, the six cases and the ten touched suites pass 124/124.

Commits:
- `10161ca`: five fixes and six regression cases
- this handoff

## 1. Findings fixed

### 1.1 B4: a Time Freeze stopped the Story shieldstun timer, so a blocked discharge locked the hero in the stance

**Cause.** `BlockSystem._PhysicsProcess` returned on `_owner.TimeFrozen` *before* it decremented `_shieldStunTimer`. A freeze therefore froze shieldstun as well as regen. Before decision 7.4 almost nothing blockable could reach the hero during their own freeze. Since 7.4, the Chronal Extractor's discharge is blockable through `ApplyEnvironmentalHit`, and extractors are deliberately absent from `TimeFreezeController.FrozenTimeGroups`, so they keep discharging. An ordinary block of a discharge set an 11-frame shieldstun (`ShieldstunFramesFor(20)`) that never counted down. `ProcessBlocking` then refused release, roll, jump, grab and drop-through until the thaw, wasting up to about 4 s of the 45 s-cooldown escape verb. Falling debris from the Collapse Tremor hit the same lock.

**Fix (the verifiers' corrected fix).** The shieldstun decrement now sits between the hitstop return and the Time Freeze return. Shieldstun is the hero's own stance lock, so it runs on the hero's clock in the hero's own freeze. Charge regen and the shatter lockout still hold under the freeze, as the contract's "passive recovery pauses" requires (`TimeFreezeTests.PassiveRecoveryAndStatusTimersAreSuspendedWhileMovementKeepsRunning` stays green). The rejected alternative, making freeze-time discharges and debris unblockable, would contradict 7.4's source list.

### 1.2 B6: the Story perfect-block bookkeeping was suspended during Time Freeze

**Cause.** `TickPerfectBlock` is the only code that counts the window down, clears it on a stance drop, records the stance and ages the 20-tick rearm count. It ran inside `_PhysicsProcess`'s `if (!TimeFrozen)` block. `CheckBlockInput`, `NoteBlockStanceRaised` and `OpenOwedPerfectBlockWindow` have no freeze gate. So during a freeze:
- a window opened by a fresh press stayed at 5 for the whole freeze;
- a drop did not clear it, so a later raise inherited it;
- the lowered age never moved, so every re-press re-armed, defeating the anti-mash guard.

Every blockable discharge or debris hit in the rest of the freeze was then a free perfect block.

**Fix.** `TickPerfectBlock()` moved out of the `!TimeFrozen` block. It now runs once per tick after the hitstop gate. Hitstop still pauses the window (the early return); a Time Freeze no longer does.

**Why this choice and not the split one.** One verifier proposed keeping the countdown paused (the plan's choice) and moving only the drop and rearm observation out. That leaves a stance held through a freeze perfect-blocking real-time hits for up to 300 ticks. User ruling 3.4 is "Block raised within about 4–6 frames before a hit". The pause was an agent choice in plan §2.3.2, reasoned against hitstop and frozen enemies only. It did not consider the 7.4 sources that keep running in a freeze. Frozen enemies are already harmless during a freeze, so the pause protected nothing. The finder and two of the four verifier notes chose to tick the countdown. That makes Story match the ruling in a freeze, and the 7.4 sources keep their real-time timing.

**Owed (closeout).** This reverses the documented choice. `CM_BLOCK.md` line 44 ("Time Freeze pause the window and the lowered age"), plan §2.3.2 line 913, and the `PROVISIONAL-PERFECT-BLOCK-RULES` row need the new rule: hitstop pauses the window and the lowered age, and the hero's own Time Freeze does not.

### 1.3 B7 / B21: the Story hazard block ran before the post-rewind invulnerability gate

**Cause.** `ApplyEnvironmentalHit` ran D04 (Defy), then `ResolveProtectionLayers` (Aegis, barrier), then `TryBlockEnvironmentalHit`. The invulnerability gates (post-rewind, roll, landing tech, knockdown) existed only inside `ApplyDamage`, below the block. Since 7.4 moved the cyclic hazards, the extractor discharge, the searchlight strike and the spikes onto this entry point, a post-rewind-protected hero holding Block spent a charge, took shieldstun and could shatter on a tick it is immune to. The verifiers corrected the finder's scenario: a cyclic hazard hits once per cycle, so one vent gives one tick inside the 120-frame window. The realistic case is a hero who died with one charge, lands beside a vent or a discharging extractor, and shatters (daze, 300-frame lockout) on the first tick. D01 puts eligibility and invulnerability above every layer. The sim refuses an invulnerable fighter first (`ResolveFighterHit`).

**Fix.** A new private `RefusesEnvironmentalHit` is read at the top of `ApplyEnvironmentalHit`, before the Defy window. It is exactly the refusal set `ApplyDamage(int)` applies: Dead or Respawning, `_rollInvulnerable`, and `IsConfirmInvulnerable` (post-rewind, tech, knockdown). A refused hit returns `EnvironmentalHitResult.Protected` with 0 damage. Every caller checks only `== Blocked` / `!= Blocked`, so knockback and the extractor's meter drain behave exactly as before 7.4. Because the gate is above `ResolveProtectionLayers`, an invulnerable hero also no longer spends Temporal Aegis or the barrier on a blockable hazard tick (D01). The enum and class docs say so.

**Scope kept narrow.** Only the 7.4 hazard entry point moved. Not changed, and still the old order (the open `VERIFY-STORY-INVULN-LAYER-ORDER` row):
- `ResolveIncomingHit`, the hurtbox path: an enemy hit on a post-rewind-protected, blocking hero still spends a charge (and shields, as the row already says).
- `ApplyEnvironmentalDamage`, the unblockable penalties (drowning, the haul-out, the rift snap, the escape catch): an invulnerable hero still spends Aegis or the barrier there.

**Owed (closeout).** Widen `VERIFY-STORY-INVULN-LAYER-ORDER`: the hazard half of its acceptance is met at `ApplyEnvironmentalHit`; the hurtbox path also spends block charges, not only shields; `ApplyEnvironmentalDamage` still has the old order. Amend plan §2.7.4's layer list to begin with the invulnerability step.

### 1.4 B10: Down+Jump out of a swing dropped through but left the hero in Attacking, and was ignored in startup and active frames

**Cause.** CM-MOVE's Down+Jump branch in `CheckJumpInput` calls `TryDropThrough()`. `TryDropThrough` deliberately skips `TransitionTo(Airborne)` in `Attacking`, because the double tap keeps the swing. In `Attacking`, `CheckJumpInput` is reached only through `TryRecoveryCancel` (recovery frames and the chain hold). That reads `true` as "cancelled", calls `CancelActiveAttack` and `ResetComboChain`, and makes no transition. So the hero fell through in `Attacking`, with no recovery flag, so no air jump, air dodge, block, movement ability or ledge grab. A drop in the finisher's early recovery also re-read the leftover frames against hit 1's timeline and re-armed the melee box in the air with the finisher's payload. In startup and active frames Story never read Down+Jump at all.

**What the double tap does, and what the fix matches.** The Story double tap drops in a swing's startup, active and recovery frames and keeps the swing running. Its Down press is no cancel verb. Jump is a cancel verb in recovery and the chain hold (design 752/3080), and the sim cancels the string on that same Jump press (`ProcessBasicAttackPhase`) and then drops a free fighter. In startup and active frames the sim drops with the swing running (`allowDropThrough` stays true while attacking). The fix follows the finder and both verifiers:
- **Recovery frames and the chain hold.** `CheckJumpInput`'s drop branch now transitions `Attacking → Airborne` after a successful drop. `TryRecoveryCancel` then ends the swing as for any other cancel. The hero is a free `Airborne` hero on the drop tick, as in the sim, and the leftover frames are never read again, so no box re-arms. The `Attacking` exemption inside `TryDropThrough` stays, for the double tap.
- **Startup and active frames.** `ProcessAttacking` now calls a new `TryDownJumpDropThroughKeepingTheSwing()` beside `CheckDropThrough()`, when the double tap did not already drop and the swing is not yet in recovery. It applies the same gates as `CheckJumpInput`'s branch (Root refuses it; Jump pressed, Down held, on the floor) and keeps the swing, as the double tap does.

So both inputs drop in every grounded-swing phase where the double tap does. They differ only where Jump itself is a cancel verb, which matches the sim press for press.

**Observed and not fixed (not in the findings).** The Story double tap is not read in the chain hold: `CheckDropThrough()` sits after `ProcessAttacking`'s hold dispatch. The sim's double tap does drop there. Down+Jump now works in the hold.

### 1.5 B11: Story movement-ability gravity read Jump as held, so the apex hang applied with Jump released

**Cause.** `ApplyMovementAbilityGravity` calls `ApplyGravity(dt, ignoreShortHop: true)`, which passed `jumpHeld: ignoreShortHop || _jumpHeld`, always true. Before CM-MOVE that only kept the short-hop band off. Since 6.1 gated the apex hang on `jumpHeld`, it also switched the ×0.5 hang on for every movement-ability gravity tick after a jump (`JumpedThisAirtime` survives the cast). The sim's kit phases read the real 323 latch (`JumpHeldForGravity`), so Story and Fighter diverged. Example: Tesla jumps, releases Jump, blinks; every recovery tick hung (×0.50 measured) instead of rising, then falling at ×2.2.

**Fix (the verifiers' corrected fix).** With `ignoreShortHop`, `ApplyGravity` now passes the live `CurrentInputFrame.IsHeld(Jump)` for the hang and `riseFromOwnJump: false` for the short hop. The `_jumpHeld` field is not refreshed during a cast, so it is not used. The short hop stays off for a movement ability's boost, as CM-STORY 1.1(1) intended. With Jump held the hang is unchanged (the regression case's control half). Leonardo's peak and glide-sink cases, which cast from the ground, are unaffected and stay green.

**Owed (closeout).** Plan §2.1.1 line 337 ("That matches the sim, where every kit step runs as a held jump") is false in the merged sim. Record movement-ability gravity parity under `USER-APEX-HANG-HELD` and `PROVISIONAL-MOVEMENT-ABILITY-GRAVITY-SCOPE`: Story's hang reads Jump live, like the sim's latch for `Create`-built streams.

## 2. Tests

All six new cases drive the hero with `PlayerInputFrame.Create`-built edges. The B4, B6, B10 and B11 cases run in **real physics frames** (`await ToSignal(PhysicsFrame)`), so the hero, its `BlockSystem` and its ability nodes tick in the engine's order. The B4 and B6 cases use the real `TimeFreezeController`, with its own clock stopped. The B7 case is a direct-call layer-order check, like its sibling CM-BLOCK cases in that suite: the defect is instantaneous resolution order and has no frame dependence.

| Suite | New case | Finding | Red run on the unfixed code |
|---|---|---|---|
| `StoryPerfectBlockTests` | `AStanceHeldIntoATimeFreezeBlocksADischargeAndItsShieldstunRunsOutOnTheHerosClock` | B4 | "still Blocking after 71 ticks (shieldstun 11)" |
| `StoryPerfectBlockTests` | `APerfectWindowRaisedInATimeFreezeClosesOnTheHerosClockAndADropEndsItInsideTheRearmGuard` | B6 | "The window must close 5 live ticks after the raise, freeze or not; 5 frame(s) remain." |
| `StoryEnvironmentalDamageTests` | `APostRewindProtectedHeroSpendsNoChargeAegisOrGuardOnAHazardTick` | B7 / B21 | "An invulnerable hero's raised block must not spend a charge on a hazard tick." |
| `FeelPassStoryTests` | `DownJumpInASwingsRecoveryOrChainHoldCancelsTheSwingIntoAFreeDrop` | B10 | "hit 1 recovery: Down+Jump must cancel the swing into a free drop." |
| `FeelPassStoryTests` | `DownJumpInASwingsStartupOrActiveFramesDropsThroughKeepingTheSwing` | B10 | "startup: Down+Jump in a swing must drop through, feet at -2410.004 vs top -2410." |
| `StoryMovementAbilityGravityTests` | `AMovementAbilityRecoveryHangsAtItsApexOnlyWhileJumpIsHeld` | B11 | "tesla, Jump released: a recovery gravity tick took x0.50 (vy 0.0 -> 10.3)" |

What each case pins:
- **B4.** A stance pressed and held past the window into a freeze; a discharge is an ordinary block (one charge, shieldstun on). With Block released at once, the stance holds for exactly the earned shieldstun (11 < held ticks ≤ 13), then drops to Idle while still frozen, and a Roll press then rolls. Regen still holds.
- **B6.** Two halves, each on a fresh hero and freeze. (1) A fresh press in a freeze arms the window; it is closed five live ticks later, and a discharge then spends a charge. (2) Press, drop, re-press four ticks later inside the 20-tick rearm guard: no window opens or is inherited, and the discharge spends a charge.
- **B7 / B21.** Post-rewind protection (`ArmRecoveryProtection`), one charge left, a front-facing ordinary block, a cyclic tick: no HP, no charge, no shatter (still Blocking, no shieldstun, no lockout). Then, with Aegis granted, a direct `ApplyEnvironmentalHit` returns `Protected` with 0 applied and Aegis is still held.
- **B10, recovery and hold.** Einstein on a one-way platform, in three scenarios: hit 1's first recovery tick; six ticks into hit 1's chain hold (asserted to be the hold through its locomotion pose); hit 3's first recovery tick. Down+Jump puts the hero in `Airborne` on the drop tick with vy ≥ 0. For hit 1, an air jump two ticks later is accepted. For the hold and hit 3, the hero never re-enters `Attacking`, the melee box stays shut for 24 ticks (the phantom finisher box), and the body falls below the platform.
- **B10, startup and active.** Down+Jump on the tick after the press (startup) and on hit 1's first active tick: the state stays `Attacking` (the box still open in the active case), the body falls below the platform, and the kept swing still strikes on the way down.
- **B11.** Open air, an air jump (so `JumpedThisAirtime`), then the cast. Tesla's blink with Jump released, Tubman's leap with Jump released, and Tesla's blink with Jump held. Each gravity-on recovery tick's measured band multiplier is `Δvy / GravityPerFrame(weight)`. Released: never the ×0.5 hang, always ≥ 0.95. Held: at least one hang tick.

**Rewritten or changed existing cases: none.**

**Delta: +6, 3490 → 3496.** `StoryPerfectBlockTests` 4 → 6, `StoryEnvironmentalDamageTests` 10 → 11, `FeelPassStoryTests` 25 → 27, `StoryMovementAbilityGravityTests` 6 → 7. The `[TestCase]` attribute count under `tests/` is 3496.

**Validation.**
- `dotnet build`: 0 errors, the two pre-existing warnings only (`KLSG_ECS004` on `FighterTuningComponent`, `CS8632` in the vendored GdUnit API). No `KLOTHO_DET002`.
- Filtered run over the ten touched suites (`StoryPerfectBlockTests`, `StoryEnvironmentalDamageTests`, `StoryMovementAbilityGravityTests`, `FeelPassStoryTests`, `TimeFreezeTests`, `StoryBlockModelTests`, `StoryDefenceOrderTests`, `PostLandingHoldTests`, `StoryMovementFeelTests`, `StoryAirPhysicsTests`): 124/124, which equals their summed `[TestCase]` count.
- Full run (2026-10-16, lock held, 1 min 37 s): `Passed! - Failed: 0, Passed: 3496, Skipped: 0, Total: 3496`. That is exactly 3490 + 6, with a runner exit of 0 and no aborted or crashed host.

Every run held `D:/Projects/.ftt-gdunit-lock`. Leftover `testhost` and `Godot*_console` processes were drained only while holding it, and it was released in a `finally`. No content was changed, so no `--import` rerun was needed after the first.

## 3. Fighter hashes

**No Fighter hash moves.** No file under `scripts/FighterSim/`, `scripts/Networking/` or the CPU was touched. No component, field, ID or protocol changed. Every change is in `PlayerController` (and its `EnvironmentalBlock`, `PerfectBlock` and `MovementAbilityGravity` partials) and the Story half of `BlockSystem` (`_PhysicsProcess`, which the sim does not use). The Level 13 Mirror clone is a `PlayerController`, so it inherits B10's and B11's behaviour, which moves it toward the sim.

## 4. Not fixed, and why

- **The rest of `VERIFY-STORY-INVULN-LAYER-ORDER`** (§1.3): the hurtbox path and the unblockable environmental penalties keep their order. That open row's acceptance asks for a pin in each mode, or a ruling that the Story order is intended. Both are wider than the 7.4 regression this finding is about.
- **The Story double tap in the chain hold** (§1.4): not in the findings. Recorded for the closeout as a possible follow-up for decision 6.3's "both inputs everywhere".
- **Aerial Special gravity** still reads the stale `_jumpHeld` latch during a cast. This is CM-MOVE's recorded decision 3 and is not a movement ability.

## 5. What the closeout owes (docs and ledger; this workstream edits neither)

- `PROVISIONAL-PERFECT-BLOCK-RULES`: hitstop pauses the Story window and lowered age, and the hero's own Time Freeze does not (§1.2). Also correct `CM_BLOCK.md` line 44 and plan §2.3.2 line 913.
- Time Freeze and the Story stance (B4): shieldstun runs on the hero's clock in a freeze; regen and the shatter lockout hold. `TEMPORAL_STATE_CONTRACT.md` is unaffected: shieldstun is not passive recovery.
- `VERIFY-STORY-INVULN-LAYER-ORDER`: widen it as §1.3 says, and amend plan §2.7.4's order to begin with the invulnerability step.
- `USER-DROP-THROUGH-BOTH-INPUTS`: Story Down+Jump drops in every grounded-swing phase. Startup and active frames keep the swing; recovery frames and the chain hold cancel it into `Airborne`, as in the sim. Note the double tap's chain-hold gap (§1.4).
- `USER-APEX-HANG-HELD` and `PROVISIONAL-MOVEMENT-ABILITY-GRAVITY-SCOPE`: a movement ability's hang reads Jump live. Correct plan §2.1.1 line 337.
- The baseline: **3496** after this branch alone (3490 + 6). Reconcile it with the other three Phase B fix branches' deltas at merge.
