# FU-KNOCK - Fighter finisher x2 travel, wall bounce, hitstun shove (Fighter mode only)

Workstream FU-KNOCK of the 2026-10-25 feel and UI update (`docs/FEEL_UI_UPDATE_2026-10-25_PLAN.md` section 2.4; recon `docs/recon-2026-10-25/D_fighter_knockback_slide_walls.md`). Branch `fu/knock`, cut from `c30d7cb8`.

## 1. Summary in user terms

- **The Fighter finisher flies twice as far (R1).** The third hit's launch is now twice as fast sideways and unchanged upward, so the arc height and airtime are the same and the distance doubles (measured 2.0-2.1x on every kit that does not reach a wall). Story's finisher is untouched.
- **Wall bounce (R2).** A launched fighter driven into a side wall at 5 u/s or more rebounds at half speed. The bouncing fighter alone freezes for 6 ticks, an impact burst plays at the wall with a 6 / 0.15 s camera shake, and it happens once per launch. No damage, no hitstun change, no meter. Lincoln's finisher (and most finishers at about 50 % victim HP) now reaches a wall from mid-stage and bounces.
- **The hitstun slide is a short shove (R3).** A grounded victim of a non-launching hit now moves 0.2-0.4 units in about 12 ticks, 90 % of it in the first 12 ticks of motion, and is planted for the rest of the stun (before: a constant-speed glide of 0.6-1.3 units for the whole 30 / 40-frame stun, then a dead stop). Launches, airborne non-launchers and the guard-break push are unchanged.

## 2. What shipped

Files (all under the worktree):

| File | Change |
|---|---|
| `scripts/Combat/BasicComboRules.cs` | New section "Fighter knockback feel (2026-10-25)" appended at the END (after `IsBasicStringHitbox`). The single home of every number below. |
| `scripts/FighterSim/FighterKnockbackFeelRules.cs` (new, + `.uid`) | Public static rules: FP64 conversions of the constants, `StepShove`, `TryWallBounce`, `RearmBounce`, `HasBounced`, `BounceCount`. |
| `scripts/FighterSim/FighterSimulationSystems.cs` | (a) `ApplyBasicSwing` finisher: `knockback *= FinisherHorizontalScale` and `hit2VerticalScale` becomes `FinisherVerticalScale` for step 2 (~line 2533 and ~2543). (b) Movement hitstun branch (~623-640): the shove step. (c) Position integrate (~806-815): captures `unclampedX`, calls `TryWallBounce` after the wall clamp. |
| `scripts/FighterSim/FighterEntitySystems.cs` | `ResolveFighterHit` (~937-967): `RearmBounce(ref targetAction)` in the `if (launches)` branch; `target.Velocity.x *= ShoveScale` in the grounded non-launching branch. |
| `scripts/FighterSim/FighterMovementSystem.HitReaction.cs` | `ApplyHitReaction`: clears the bounce flag whenever `Tumble == 0`. |
| `scripts/FighterSim/FighterSpecialCast.cs` | `ActionFlagWallBounced = 16` and the `ActionFlags` doc comment. |
| `scripts/FighterSim/FighterSimulationDriver.WallBounce.cs` (new, + `.uid`) | Presentation partial `SyncWallBounce`; one hook line in `FighterSimulationDriver.cs` at the end of `SyncPlayer` (after `SyncCoreMechanicCues`). |

Constants (all in `BasicComboRules`, doubles converted with `FP64.FromDouble`):

| Constant | Value |
|---|---|
| `FighterFinisherHorizontalScale` | 2.0 (vertical scale = 1/2 on the doubled impulse, so vy = the force) |
| `FighterWallBounceMinSpeed` | 5.0 u/s |
| `FighterWallBounceRestitution` | 0.5 |
| `FighterWallBounceHitstopFrames` | 6 (only raised to 6, never shortens an existing freeze) |
| `FighterHitstunShoveScale` | 3.0 (applied at the hit site to a grounded non-launching hit's horizontal speed) |
| `FighterHitstunShoveDecay` | 0.79 per tick |
| `FighterHitstunShoveSnapSpeed` | 0.25 u/s (below it the shove snaps to 0) |

Presentation constants (driver partial): impact scene `res://scenes/vfx/VfxImpact.tscn`, burst at the fighter's chest (40 px up) inset 24 px toward the wall, mirrored by side; `CameraShake.Instance.Shake(6, 0.15)` (so the accessibility scale applies); `HapticFeedbackManager.OnGuardImpact`. No audio cue (FU-ECHO owns the catalog).

Component 323 bits taken (documented in `FighterSpecialCast.cs`): **`ActionFlags` bit 4 (value 16) `ActionFlagWallBounced`** (once per launch; cleared at every launch in `ResolveFighterHit` and whenever `Tumble == 0`), and **bits 8-11 (mask 0xF00) the wrapping 4-bit bounce counter**. Bits 5-7 and 12-31 stay reserved. No component ID allocated.

How the rules work:
- Finisher: where the sim builds the intent, `knockback = BasicKnockback x tenths/10 x 2`, vertical scale 0.5. `ResolveFighterHit` already computes `force = ScaleByMissingHP(kb)/(1+weight)` and `vy = force x verticalScale`, so vx = 2F and vy = F with the HP ramp and weight intact. The knockback magnitude is read only for the impulse (Defy zeroes it wholesale; the DI stash rotates the stashed vector, so DI still bends the launch by up to 15 degrees).
- Bounce: after the tick's `Position += Velocity`, if the pre-clamp x crossed a wall, `Tumble != 0` (310: 1 in hitstun, 2 after), the bounce flag is clear and `|vx| >= 5` points into the wall: `vx = -vx/2`, `HitstopFrames = max(existing, 6)` on that fighter, flag set, counter + 1. The ceiling and the knockdown roll's clamp (~483) are left alone.
- Shove: in the hitstun branch, if grounded, `DazeFrames <= 0`, `Tumble == 0`: `vx = StepShove(vx)`. The hit-site scale is applied only on the grounded non-launching branch, so airborne non-launchers keep their vector. Hazard knockback takes the same path and gets the same shove.

## 3. Tests

Baseline 3529 + **18** = **3547** (full run: `Failed: 0, Passed: 3547, Total: 3547`, 1 m 48 s; `[TestCase]` attributes under `tests/` = 3547).

| Suite (one `[TestSuite]` per file) | New cases |
|---|---|
| `tests/Determinism/FighterFinisherKnockbackTests.cs` (`[RequireGodotRuntime]`) | 3: every roster kit launches (2F, F) at 100 % and 50 % victim HP, F from the force formula; travel is 1.9-2.1x the old figure at an unchanged airtime (Cleopatra, Leonardo, Einstein; old figures measured at `c30d7cb8`); Story's tenths and the 4.5 table are unchanged and a source scan finds no use of the new names outside `scripts/FighterSim/` and `BasicComboRules.cs` |
| `tests/Determinism/FighterWallBounceTests.cs` (pure C#) | 7: right-wall bounce (-5 from 10, 6-tick freeze, flag, counter); left wall; below threshold / moving away / not clamped / grounded slide never bounce; once per launch and a new launch re-arms; counter wraps at 16 and never shortens a longer freeze; a real launch into the Default stage wall (clamped at 10, about half speed, victim freeze 6, attacker 0, HP and hitstun untouched, frozen 5 ticks, flag clears after the tumble); a depth-7 rollback replays the bounce hash-identically |
| `tests/Determinism/FighterHitstunShoveTests.cs` (`[RequireGodotRuntime]`) | 7: light hit about 0.2 u; heavy about 0.5 u; 75 %+ in the first seven moving ticks, stopped by 16 ticks, planted for the rest of the stun (hits 1 and 2); distance tracks the hit (2.5x for a 2.5x hit; kb 2 vs 5); a launch's vx holds for 8 ticks; an airborne non-launcher keeps its exact vector; the guard-break push stays 2.0 u/s constant through daze |
| `tests/Determinism/FighterWallBouncePresentationTests.cs` (`[RequireGodotRuntime]`) | 1: a rising counter spawns exactly one impact burst, the first read and an unchanged counter spawn none |
| `tests/Determinism/KnockbackFeelTestKit.cs` | helper, not a suite |

Changed in place (no count change):
- `FighterKnockbackScalingTests.TheSameHitScalesItsImpulseLinearlyWithTheVictimsMissingHP`: it reads the grounded opener's speed straight after the hit, which now carries the shove scale, so the expectation is multiplied by `FighterKnockbackFeelRules.ShoveScale`. The ramp (1.125x / 1.25x / 1.5x) it pins is unchanged.
- Existing pins that did NOT need changes (all green): `FighterKnockbackScalingTests.TheFinisherSeparatesEveryKitBeyondMeleeRangeAtFullVictimHP`, `FighterKnockdownTests`, `FighterBasicStringParityTests`, `FighterTumbleTests`, `DownAirSlamTests`, `FighterPresentationSyncTests`, `RollbackReadinessTests`, the CPU suites and the Story finisher-carry and wall-bounce suites.

## 4. Measurements

Harness: mirror matchups (the victim is the same kit, so weights differ per kit), default stage (walls at 10), fighters meet near the centre, victim at full HP unless stated, no DI. Travel = horizontal distance from the hit to landing. `*` = the launch reached a wall and bounced, so the "after" is wall-limited and understates the unbounded 2x.

Finisher travel (units), before -> after:

| Kit | 100 % HP | 50 % HP |
|---|---|---|
| Cleopatra | 2.09 -> 4.17 (2.00x) | 3.50 -> 7.47 (2.13x) |
| Leonardo | 2.60 -> 5.25 (2.02x) | 4.05 -> 8.79 (2.17x) |
| Tesla | 2.70 -> 5.43 (2.01x) | 4.41 -> 9.36 (2.12x)* |
| Einstein | 3.17 -> 6.54 (2.06x) | 4.85 -> 8.86* |
| Shakespeare | 3.41 -> 7.10 (2.08x) | 5.21 -> 8.59* |
| Tubman | 3.25 -> 6.68 (2.06x) | 4.94 -> 8.82* |
| Mozart | 3.82 -> 8.17 (2.14x) | 5.74 -> 8.22* |
| Joan | 3.63 -> 7.62 (2.10x) | 5.58 -> 8.44* |
| Lincoln | 4.98 -> 8.85* | 8.13 -> 5.96* |

Launch vectors (u/s, 100 % HP) before -> after: Cleopatra (5.69, 5.69) -> (11.38, 5.69); Einstein (7.22, 7.22) -> (14.45, 7.22); Joan (7.98, 7.98) -> (15.95, 7.98); Lincoln (9.52, 9.52) -> (19.04, 9.52). Airborne ticks are identical before and after for every non-wall kit (e.g. Cleopatra 28, Einstein 34), confirming the arc is unchanged. Lincoln at full HP reaches the wall from mid-stage (about 19 u/s sideways) and bounces, by the ruling (no per-kit cap).

Shove (victim displacement over 40 ticks / ticks until it stops, which include about 4 impact-freeze ticks) before -> after:

| Kit | Hit 1 | Hit 2 |
|---|---|---|
| Cleopatra | 0.59 / 33 -> 0.21 / 15 | 0.89 / 43 -> 0.26 / 16 |
| Leonardo | 0.66 / 33 -> 0.24 / 15 | 1.00 / 44 -> 0.29 / 16 |
| Tesla | 0.69 / 34 -> 0.25 / 16 | 1.03 / 44 -> 0.30 / 16 |
| Mozart | 0.72 / 34 -> 0.26 / 16 | 1.08 / 44 -> 0.32 / 17 |
| Einstein / Tubman | 0.76 / 34 -> 0.28 / 16 | 1.13-1.14 / 44 -> 0.34 / 17 |
| Shakespeare | 0.80 / 34 -> 0.29 / 16 | 1.20 / 44 -> 0.36 / 17 |
| Joan | 0.85 / 34 -> 0.31 / 16 | 1.27 / 44 -> 0.38 / 17 |
| Lincoln | 0.98 / 34 -> 0.36 / 17 | hit 2 launches (unchanged, 0.77 / 26) |

90 % of every shove is done 12-13 ticks after the freeze ends. Weakest real hit 0.21 u (target about 0.2). The strongest real grounded hit is 0.36-0.38 u (target "about 0.5") because these runs use mirror weights; the recon's 0.5-0.6 figures assumed a weight-1.0 victim. Raising `FighterHitstunShoveScale` raises all of them proportionally (4.0 would give 0.28-0.51).

## 5. Provisional numbers and why

All of R1's 2.0 is the user's ruling; everything else is an agent choice:
- Bounce threshold 5 u/s: Story D9's 300 px/s. Restitution 0.5 and freeze 6 ticks: "about half speed" and "a short freeze".
- Shove scale 3.0, decay 0.79, snap 0.25: the recon's model (x3, x0.79), calibrated so the weakest real hit is about 0.2 u and 90 % is done in about 12 ticks. The snap trades 0.02 u of distance for a stop by about 15 ticks.
- Camera shake 6 / 0.15 s and the 24 px / 40 px burst offsets: placeholders in the family of the existing 3 / 0.08 s block shake.

## 6. Shared files touched and merge notes

- `scripts/Combat/BasicComboRules.cs`: one section appended after the last member (`IsBasicStringHitbox`). FU-ECHO deletes other regions; the only conflict risk is the file's final lines.
- `scripts/FighterSim/FighterSimulationSystems.cs`: three small regions only (finisher intent ~2533-2543; hitstun branch ~623-640; position integrate ~806-815). No Echo Step code touched.
- `scripts/FighterSim/FighterEntitySystems.cs`: `ResolveFighterHit` ~937-967 (two statements).
- `scripts/FighterSim/FighterSimulationDriver.cs`: two lines (a comment and `SyncWallBounce(player, playerID, in state);`) at the end of `SyncPlayer`, after `SyncCoreMechanicCues`. FU-BLOCK's hooks are inside `SyncPresentationFeedback`'s block branches, a different method.
- `tests/Determinism/FighterKnockbackScalingTests.cs`: one expectation line. FU-ECHO or others editing the same file would be a trivial conflict.
- `.uid` sidecars for the new scripts and suites are committed.

## 7. Not delivered / risks

- **Nobody has played it.** The bounce burst, shake, 6-tick freeze, the shove feel and Lincoln's range are headless-verified only.
- **Wall loops.** Lincoln (and every kit at about 50 % HP) now bounces off a wall from mid-stage. The bounce is once per launch and clears only when the tumble ends, so a victim cannot be pinballed by one launch; a second launch re-arms it. A corner-trapped victim can bounce once per finisher.
- **A bounce adds no hitstun**, but its 6-tick freeze suspends the victim's hitstun counter like any hitstop, so the stun lasts 6 ticks longer in wall-clock terms. The test pins that `HitstunFrames` only takes its normal decrement on the bounce tick.
- **Story hero still glides in its stun** (report only; recon section 8: `ProcessStunned` never decelerates `Velocity.X`). Not changed.
- Existing wall behaviour for non-launched states (grounded slides, the knockdown roll, the ceiling) is unchanged: a grounded slide never bounces.
- Every Fighter hash moved (LAN builds from before and after would desync; LAN is de-scoped). `KLOTHO_DET002`: none. The full build shows only the two pre-existing warnings (`KLSG_ECS004`, `CS8632`).
- The re-arm in `ResolveFighterHit` covers launches that go through that function (basics, directional strikes, throws, launching Specials); the slam-bounce relaunch in `FighterVerbRules` (~383) does not re-arm, so a slam bounce counts as part of the same launch. The flag also clears when the tumble ends, so this only matters inside one continuous tumble.

## 8. Drafted doc changes (not applied; the shared docs are the orchestrator's)

**AGENTS.md, Combat, the "Knockback target" bullet.** Current text begins: "Knockback target: `baseKnockback / (1 + target.Weight)`. **In Fighter Mode only** it is then scaled by the victim's missing HP after the hit ...". Append to the end of that bullet:

> **Fighter knockback feel (2026-10-25, FU-KNOCK; ledger `USER-FIGHTER-FINISHER-2X`, `USER-FIGHTER-WALL-BOUNCE`, `USER-FIGHTER-HITSTUN-SHOVE`, `PROVISIONAL-FIGHTER-KNOCKBACK-FEEL`; Fighter only, Story reads none of it).** The finisher launches with horizontal speed 2 x the force and vertical speed the force (`FighterKnockbackFeelRules.FinisherHorizontalScale`; the arc and airtime are unchanged, the travel doubles), after the missing-HP ramp and the weight term; Story's finisher (the profile tenths) is untouched. A launched fighter (310 `Tumble` != 0) clamped by a side wall at 5 u/s or more rebounds at half speed once per launch, the bouncing fighter alone freezing 6 ticks, with no damage, hitstun or meter change (323 `ActionFlags` bit 4 `ActionFlagWallBounced`, cleared by the next launch and when the tumble ends; bits 8-11 a wrapping bounce counter the driver edge-detects for the impact burst and shake, `FighterSimulationDriver.WallBounce.cs`). A grounded victim of a non-launching hit gets a short shove instead of a constant-speed glide: its speed is x3 at the hit site and decays x0.79 a tick, snapping to zero below 0.25 u/s, so it moves about 0.2-0.4 u in about 12 ticks and is planted for the rest of the stun; launches, airborne non-launchers and the guard-break push are unchanged. The numbers live in `BasicComboRules`' "Fighter knockback feel" section.

**AGENTS.md, Known prototype gaps, Combat.** Current sentence: "Grounded knockback has no friction during hitstun (an F09 question)." Replace with: "Grounded Fighter knockback has strong friction since 2026-10-25 (the hitstun shove, `PROVISIONAL-FIGHTER-KNOCKBACK-FEEL`); the Story hero still glides at constant speed through its stun (`ProcessStunned` never decelerates `Velocity.X`), while Story mobs have friction (S1)."

**AGENTS.md / CLAUDE.md, Klotho component table, row 323.** In the `ActionFlags` description, replace "bits 4-31 reserved, 0" with: "bit 4 `ActionFlagWallBounced` (FU-KNOCK: this launch has bounced off a side wall; cleared by the next launch and when the tumble ends), bits 8-11 the wrapping 4-bit wall-bounce count (read only by presentation), the other bits reserved, 0". Rules: `FighterKnockbackFeelRules`.

**Ledger rows (`DESIGN_BUILD_DEVIATIONS.md`), each Owner Unassigned:**
- `USER-FIGHTER-FINISHER-2X`: intended "Fighter finisher 2x the distance" (ruling R1, 2026-10-25; "increase the knockback effect from the final combo hit so the enemy is knocked back further"); observed at the FU-KNOCK commit: horizontal x2, vertical unchanged, 2.0-2.1x travel on non-wall launches, Story unchanged; acceptance: a person finds the finisher's distance right in a pad session at spawn distance and at low HP, and Lincoln's wall-reaching range acceptable.
- `USER-FIGHTER-WALL-BOUNCE`: intended "rebound off the wall at about half speed with an impact burst, a short freeze and camera shake, once per launch, no damage" (R2); observed: restitution 0.5, 5 u/s threshold, 6-tick freeze of the bouncing fighter, burst + 6 / 0.15 s shake, no audio; acceptance: reads as a bounce, not a stutter; whether the freeze should also hold the opponent.
- `USER-FIGHTER-HITSTUN-SHOVE`: intended "more fixed in place and just hit back a bit, no sliding" (R3, "light hits about 0.2 units, heavier about 0.5"); observed: 0.21-0.38 u in about 12 ticks, planted for the rest of the stun; acceptance: the human check that it reads planted rather than stiff, and a ruling on whether the heaviest hits should reach 0.5 (scale 4.0).
- `PROVISIONAL-FIGHTER-KNOCKBACK-FEEL`: the agent-chosen numbers in section 5 plus: the bounce threshold equals Story D9's; a bounce lengthens the stun by its freeze; the slam bounce does not re-arm; hazard knockback gets the same shove; Story hero's glide left alone.

**GDD lines (`design-godot.md`, master and mirror):** the Win Condition line (:112, "The sides and top of the screen are bounded by solid physical boundaries.") gains: "A fighter launched into a side wall at speed rebounds off it at about half speed, once per launch, with no damage." The combat section's knockback description gains: "The final string hit launches twice as far as its vertical lift suggests (horizontal speed 2x), and grounded non-launching hits shove the victim a short, strongly damped distance rather than sliding through the stun." (Exact anchor lines to be picked by the orchestrator; the master is canonical.)
