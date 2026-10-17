# CM-FIXB-SIM handoff: the Phase B fix round, Fighter sim half (combat and movement update, 2026-10-16)

Fix round B of `docs/COMBAT_MOVEMENT_UPDATE_2026-10-16_PLAN.md`, workstream **CM-FIXB-SIM**: the seven verified findings under the `SIM` key of `D:/Projects/genre_review_2026-10-13/fixB_inputs.json` (B1, B2, B5, B8, B19, B12, B23; B8 and B19 are one defect found twice and are fixed once). Branch `worktree-wf_a15de89f-42d-1`, worktree `D:\Projects\Fighters Through Time - V3\.claude\worktrees\wf_a15de89f-42d-1`.

**Base.** The worktree was created at `d2e52e0` (the 2026-10-07 baseline), not at merged Phase B. It was clean, so before any edit the branch was reset to `740d712` (main = Phase A + Phase B, 3490 tests), the commit the task names.

Commits, in order:
- `aa77531`: `CM-FIXB-SIM: sim fix round (tumble, Defy motion, perfect block, ledge grant, Foresight answer)` (production and tests);
- the commit that adds this handoff (the branch head).

**Fighter hashes move** on runs that hit the changed cases: a non-launching hit on a tumbling fighter, a Defy proc, a hit on a fighter whose block stance was up, an early exit from a first ledge grab, and a projectile caught by Foresight with Block held. No Klotho component, field, snapshot layout, protocol field, save field or `en.csv` key changed. Component 323 gained no bit. The only state written is existing state: 310 `Tumble`, the velocity, 323 `BlockTiming`, the shared `InvulnerabilityFrames` counter and 323's existing bit 0. No new `KLOTHO_DET002`; all the new arithmetic is integer.

**The one Story edit:** `TubmanForesight.IsAnswerPending` plus its read in `PlayerController.ProcessUsingSpecial`, which the task assigned to this workstream. `FighterCpuController.cs` was not touched.

**Nobody has played it.** Every claim below comes from a headless suite.

## 1. Findings fixed

| # | Finding | Fix |
|---|---|---|
| B1 (medium) | A non-launching hit ended a sim tumble only after the launch's hitstun had run out (value 2), never during it (value 1). Story ends it in both cases. | `ResolveFighterHit`: inside the hitstun write, `if (!(launches && hasImpulse)) targetVerb.Tumble = TumbleNone;` |
| B2 (medium) | A Defy-zeroed hit still applied its signed vertical knockback, doubled by the 1-HP missing-HP scale. A scalar launcher also zeroed the survivor's drift. | The impulse block now requires `!defied` |
| B5 (low) | A hit-2 Block escape on the first tick after a hit kept the perfect-block window that was armed before the hit | `FighterBlockTimingRules.BeginTick` lowers a tick that starts in hitstun or daze |
| B8 / B19 (high / medium) | The first-grab ledge intangibility carried off the hang into the climb jump and every get-up, which removed their vulnerable frames | `FighterLedgeRules.EndFirstGrabIntangibility` runs at every hang exit |
| B12 (medium / low) | A held Block on the first sidestep tick cancelled the counter strike Foresight still owed | Both cancels wait while the answer is owed, in both modes |
| B23 (low) | No end-to-end test that the ledge attack ends respawn and Defy protection and keeps its own climb frames | New merged-seam test, plus a test seam |

### How

**B1** (`FighterEntitySystems.cs`). I used the second verifier's condition. `launches && hasImpulse` is the sim's form of Story's `launches && hit.Knockback != Vector2.Zero`. It reads the hit's own launch flag, not `PendingLaunchActive`, so a stash left over from an earlier launch cannot keep the tumble alive.
- The clear sits inside `if (target.HitstunFrames > 0)` in the impulse block. Hyper-armoured and Defy-zeroed hits skip that block, matching Story's `!hasHyperArmor && !defySuppressed` gate.
- An owed Down-Air bounce is then dropped on the next tick by `FighterSlamRules.ClearIfStale`, as Story's `_slamBouncePending` rewrite does.
- `UpdateTumble`'s hitstun branch is now only a backstop and is left in place. Its doc comment was corrected.
- Sim throws still launch and tumble. That kept divergence (§2.3.1) is unchanged.
- Consequence for F09 (No Juggling): a juggled victim hit by a non-launcher, such as the air strike or a coil arc, now lands in that hit's ordinary hitstun with no tech or knockdown protection. M05 intends this, and Story already behaves this way.

**B2** (`FighterEntitySystems.cs`). The impulse block is now `if (carriesImpulse && !defied && (hyper-armour rule))`. A defied hit writes neither velocity axis nor `IsGrounded`, for any impulse form: vector or scalar, launcher or not, with or without hitstop. This matches Story's `defySuppressed`. The Defy branch already zeroes hitstun, the stash and the tumble, so skipping the block loses nothing.
- `launches` was not cleared. The hitstop that follows is max-assigned under Defy's own 12-frame freeze, and the launch bonus caps at 11, so clearing it would change nothing.
- `FighterUniversalMovementRules.Cancel`, guarded by `carriesImpulse`, still runs on a defied hit. A survivor's own roll or air dodge is still cancelled by the blow. Story has no such cancel. That case was left alone because it is not forced motion, and changing it would be a separate decision; see §4.

**B5** (`FighterMovementSystem.Block.cs`). The change is `if ((timing & ObservedBit) == 0 || fighter.HitstunFrames > 0 || fighter.DazeFrames > 0) timing = LowerOneTick(timing);`, as both verifiers proposed.
- The hit resolves in the combat pass, after that tick's `Observe`. So the first unfrozen tick after the hit, including the thaw tick after a hitstop, now finalizes the broken stance as dropped. That clears the window and sets the lowered age to 1, as Story's `TickPerfectBlock` does when it sees `Stunned`.
- The escape's `Observe` then sees a rising edge with `EscapeTickBit` set, so it does not arm.
- Side effects: the lowered age after any hit, or after a shatter (daze), starts one tick earlier, which now matches Story. Every caller of `ResolveFighterHit` is covered, including the ones that pass a throwaway 323.

**B8 / B19** (`FighterLedgeRules.cs`, `FighterSimulationSystems.cs`). These use no new state. I followed the guard pattern CM-CANCEL used for the Foresight sidestep.
- `EndFirstGrabIntangibility(ref fighter, in verb, ref action, int hangTicks)` acts only when `LedgeGrabsThisAirtime == 1`. If the counter is positive and no larger than `LedgeFirstGrabIntangibleFrames − hangTicks`, it zeroes the counter and clears 323 bit 0.
  - `hangTicks` is the number of hang ticks whose invulnerability decrement has run.
  - Inside `Process`, before the hang-frame increment, that is `LedgeStateFrames + 1`, because the movement system decrements before the hang branch.
  - In the auto-release, after the increment, and in the trump, which runs after every fighter's tick, it is `LedgeStateFrames`.
- Call sites: the climb jump, `StartGetUp` (first thing, before `ClearHang`, the chokepoint and the climb's max-assign), the Down release, the auto-release (a no-op at 300 frames, kept for completeness), and `ResolveLedgeTrump`, for whichever hanger it releases.
- **Respawn protection** rides the same counter:
  - A respawn grant longer than 30 frames at the grab was never raised by `Grab`. The counter then exceeds the ledge remainder, so it is left whole and keeps its 323 record. The get-ups and the climb jump do not end it (4.2); the ledge attack's `Swing` chokepoint still does.
  - A respawn grant shorter than 30 frames at the grab was merged into the ledge grant by `Grab`'s max-assign. It now ends with the hang. Bit 0 is cleared at the same moment, so the respawn record never claims a later climb's frames.
  - At most 29 frames of a nearly expired respawn grant are lost this way, and only when the fighter reaches a ledge with less than 30 frames of it left. See §4.
- The struck-off-ledge branch was not changed: it cannot run while the grant is live.

**B12**:
- **Sim** (`FighterMovementSystem.SpecialCancel.cs`, `FighterCounter.cs`). `FighterForesightRules.IsAnswerOwed(counter)` is `Phase == PhaseSidestep && AnswerPending != 0`. The cast region ANDs `!IsAnswerOwed` into `inRecovery`, so neither the Block cancel nor D3 fires on that tick. Input is discarded as on any locked cast tick, and the D3 press stays buffered.
  - As the first verifier corrected, only a caught **projectile** reaches this state. `FighterProjectileSystem` runs after `FighterCombatSystem`, so its answer is owed into the next tick. A caught strike is answered on its own tick.
  - The cancel is delayed by exactly one tick.
- **Story** (`TubmanAbilities.cs`, `PlayerController.cs`). `TubmanForesight.IsAnswerPending` (`_answerDelayFrames > 0`) is now exposed. `ProcessUsingSpecial`'s Recovery branch is skipped while the executing ability is a Foresight with the answer pending. The controller's tick runs before the ability node's, so the node lands the answer that same tick, and both cancels open on the next one.
  - In Story this was older than Phase B and covered caught strikes too, because Story always defers the answer by one tick.
  - The Level 13 Mirror clone (a `PlayerController`) inherits the fix.

**B23** (`FighterSimulation.cs`). Added the test seam `SeedFighterDefyProtectionForTest(playerID)`, which calls `FighterDefenseRules.BeginDefyProtection`. It writes no other state and spends no Defy use.

## 2. Tests

**Declared +8, delivered +8** (3490 → 3498). No existing case was rewritten or re-timed.

New cases:
1. `FighterTumbleTests.ANonLaunchingHitEndsTheTumbleInsideOrAfterTheLaunchsHitstunAndTheLandingIsPlain` (B1).
   - Covers the air strike, and a zero-knockback, hitstop-free construct bite, each inside the launch hitstun (value 1) and after it (value 2).
   - It runs the production pipeline, then the movement system's own rules in tick order: `UpdateTumble`, `ClearIfStale`, `TryBounce`, `TryLandingTech`, `TryBeginKnockdown`.
   - It checks that the tumble ends where the hit lands, the owed slam bounce is dropped, and the landing is plain. A second launch still re-tumbles.
   - Pure C#. The file gains the non-suite fixture struct `JuggleFixture`.
2. `StoryTumbleTests.ANonLaunchingHitEndsTheTumbleInsideOrAfterTheLaunchsHitstun` (B1). The Story parity pin, at both timings, in real ticks.
3. `DefyProtectedRecoveryTests.ADefiedHitLeavesTheSurvivorsMotionExactlyAsItWas` (B2).
   - An airborne survivor drifting at (1.5, −2) takes five lethal hits: the Down-Air slam vector, an upward vector Special, a vector non-launcher, a scalar launcher, and a hitstop-free zone pulse.
   - After each: velocity, airborne state, hitstun, tumble, stash and slam bounce are all unchanged.
   - The fixture gains `HitWith(...)`.
4. `FighterPerfectBlockTests.AStanceAHitFromBehindBreaksKeepsNoWindowThroughTheHitTwoEscape` (B5).
   - A fresh raise, then a block-cancelable bite from behind through `ResolveFighterHit` with the real 323, then the hit-2 escape on the next tick through `CanBlockEscapeHitstun`.
   - Checks: window 0, and a front contact afterwards spends a charge and sets shieldstun. These are the movement system's clock calls in its order.
5. `FighterLedgeGetUpTests.EveryExitFromAFirstGrabEndsItsIntangibilityAndKeepsOnlyItsOwnFrames` (B8/B19). All on a first grab with no `HangingReady` wait:
   - the climb jump and the Down release on hang tick 1 are hittable on their own tick;
   - the ledge attack on hang tick 1 is intangible for exactly its 8 climb frames, then the strike's startup is hittable;
   - the ledge roll on hang tick 1 is intangible for its 8 climb frames, then its roll-startup ticks are hittable;
   - the neutral climb at its earliest legal tick (hang tick 7) is vulnerable on frames 13–20;
   - a hanger that player two trumps inside its first 30 frames falls with the counter at 0. The harness is `TrumpOffAFirstGrab`, a deterministic search over player one's start delay.
6. `FighterLedgeGetUpTests.TheLedgeAttackEndsRespawnAndDefyProtectionAndKeepsOnlyItsOwnClimbFrames` (B23).
   - A hang seeded with the 323 respawn record, 100 frames of grant and a live Defy window.
   - The ledge attack clears the record and the Defy window on the press tick, and the counter reads exactly 8. It reads 0 if the chokepoint ran after the max-assign. The climb refuses the probe on frames 1–8 and the strike takes it.
   - Control: the ledge roll and the climb jump keep the record, the respawn grant (one tick shorter) and the Defy window.
7. `FighterSpecialRecoveryCancelTests.AHeldBlockWaitsForTheAnswerForesightOwesAPointBlankProjectile` (B12, sim).
   - Tubman and Einstein spawn two units apart. Tubman holds Block from the tick after her press.
   - Einstein's E=mc² is caught, the 20-damage answer lands on the catch tick + 1, and the Block cancel then ends the stance with the stance raised.
   - A probe run first finds the shot's contact tick.
8. `StorySpecialRecoveryCancelTests.AHeldBlockWaitsForTheAnswerForesightOwes` (B12, Story).
   - Block is held through the window and a strike is caught.
   - On the first sidestep tick the answer lands (`LandedDirectHitThisExecution`, the mob is damaged) and the state is still `UsingSpecial`. The Block cancel then reaches `Blocking`.

**Each fix was reproduced first.** All six production fixes were neutralized in place with the new APIs kept. Under that build, the six fix-dependent cases (1, 3, 4, 5, 7, 8) failed for the reasons described above:
- Velocity moved to (0, −9.95) on the slam and to (0, 0) on the scalar launcher.
- "Read 1" plus a knockdown on value 1.
- The window survived.
- The climb jump kept the remainder.
- The answer was dropped in both modes.

Cases 2 and 6 passed on that build, as expected: Story already behaved correctly, and B23 is a coverage gap over correct code. Case 7's first draft placed the fighters four units apart, because `spawnDistance` is a half-gap, so it failed for the wrong reason. It was corrected to `spawnDistance: 1`, then re-proved: it fails with only the sim B12 fix neutralized and passes with it.

**Validation.**
- `dotnet build`: a full `--no-incremental` rebuild has 0 errors and the two pre-existing warnings (CS8632 in GdUnit4's API, KLSG_ECS004 on `FighterTuningComponent`). There is no `KLOTHO_DET002`.
- Filtered run of the eight touched suites (`FighterTumbleTests`, `StoryTumbleTests`, `DefyProtectedRecoveryTests`, `FighterPerfectBlockTests`, `FighterLedgeGetUpTests`, `FighterSpecialRecoveryCancelTests`, `StorySpecialRecoveryCancelTests`, `TubmanKitTests`): 79/79.
- One full run, holding the lock, after draining orphans: `Passed! - Failed: 0, Passed: 3498, Skipped: 0, Total: 3498` (1 m 38 s). That is exactly 3490 + 8. The `[TestCase]` attribute count under `tests/` is 3498.

## 3. Fighter hashes

They move, as above. No suite pins an absolute hash value. Every rollback and replay suite stayed green, which means the deterministic replay of all of it holds.

## 4. Not fixed, and what the closeout owes

Not fixed. Each item is narrower than its finding:
- **The movement cancel on a defied hit (B2).** `FighterUniversalMovementRules.Cancel` still runs, so a survivor's own roll or air dodge is cut by the blow it defied. Story does not cancel the survivor's motion. This is not "forced motion", so it was left for a ruling.
- **The respawn glow during a merged hang (B8).** When a respawn grant shorter than 30 frames is merged into the first-grab grant, 323 bit 0 stays set through the hang. The driver therefore shows the spawn glow and the "Respawning" label for the ledge frames until the hang ends, and the ledge attack's chokepoint treats the merged grant as respawn protection. The optional fix, clearing the bit inside `Grab` when it raises a respawn-protected counter, needs `Grab`'s signature to take component 323. It was not done.
- **No Story mirror for B5.** Story was already correct. The optional `StoryPerfectBlockTests` parity case was not added.

Closeout owes, in docs and the ledger (none of these files were edited here):
- **`PROVISIONAL-TUMBLE-EXIT-ACTIONS` item 3:** any fresh stunning hit without a fresh launch ends the tumble, value 1 or 2, in both modes. Add the F09 note that a juggled victim hit by a non-launcher loses its tech or knockdown protection.
- **D04 / `USER-DEFY-ENDS-ON-OFFENCE`:** a defied hit writes no velocity in either mode. Decide the movement-cancel item above.
- **`PROVISIONAL-PERFECT-BLOCK-RULES`:** a tick that starts in hitstun or daze finalizes the stance as dropped. As a consequence, the lowered age after a hit or a shatter starts one tick earlier, matching Story. CM_BLOCK.md's bit-17 description ("A tick that ends unobserved…") should mention it.
- **`PROVISIONAL-LEDGE-GETUP-FRAMES` / `USER-FIGHTER-LEDGE-GETUPS`:**
  - The first-grab intangibility ends at every hang exit.
  - A respawn grant shorter than 30 frames at the grab ends with it.
  - Plan §2.4.2's claim ("The hang's remaining first-grab frames do not [survive]") is now true without the respawn bit.
  - Pad-checklist row 17 ("too safe") should be judged on the corrected frames.
  - Every first-grab CPU recovery now leaves with an ordinary vulnerable climb jump. Before this fix it had about 20–25 intangible airborne frames. The CPU suites stayed green, but bot or CPU recovery numbers from before and after are not comparable.
- **`PROVISIONAL-SPECIAL-RECOVERY-CANCEL-RULES` item (9) / plan §2.11.1:** neither recovery cancel may end Foresight while its answer is owed, in both modes. The cancel is delayed one tick: in the sim only after a projectile catch, in Story after any catch.
- **AGENTS.md:** the baseline moves 3490 → 3498, with the eight cases above.
