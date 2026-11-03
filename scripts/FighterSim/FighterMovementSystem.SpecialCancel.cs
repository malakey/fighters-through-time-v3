using FTT.Core;

namespace FTT.FighterSim {

    /// <summary>
    /// Decision 11.1 (combat and movement update 2026-10-16, plan §2.11.1;
    /// workstream CM-CANCEL): the movement system's half of D3 in Fighter — the
    /// cast region's exits, the buffer feeds and the firing. The rules are
    /// <see cref="FighterSpecialCancelRules"/>.
    /// </summary>
    public sealed partial class FighterMovementSystem {

        /// <summary>
        /// The cast region (plan §2.1.6, CM-SIM; D3, CM-CANCEL), called once per
        /// unfrozen tick after the grab branch and before <c>TickCounters</c>.
        /// Order, matching Story's <c>ProcessUsingSpecial</c>:
        /// <list type="number">
        /// <item>the D3 buffer takes this tick's press (it ages first);</item>
        /// <item>a busy cast in its recovery — a clocked Special's, or Foresight's
        /// whiff recovery or sidestep on component 322 — with the grounded Block
        /// stance up is Block-cancelled: the cast (and Foresight's stance) ends,
        /// the buffer is emptied, and the tick's presses are masked to Block
        /// (fix round A, ruling (d)), so the stance rises and nothing else starts
        /// — D3 never fires on a tick the Block cancel won, and the dropped presses
        /// are not re-fed;</item>
        /// <item>otherwise a landed cast's buffered verb fires once its frame gate
        /// is open and it can start (<see cref="TryFireSpecialCancel"/>): the
        /// tick's input becomes that verb alone and the cast ends;</item>
        /// <item>otherwise the busy caster's input is discarded (the cast lock).</item>
        /// </list>
        /// Not busy: only the stale buffer is zeroed. Returns true when a D3 cancel
        /// fired this tick (GR-RUN: its basic press is never a running attack).
        /// </summary>
        private static bool ResolveSpecialCastRegion(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            in FighterVerbComponent verb,
            ref FighterActionStateComponent action,
            ref FighterCounterComponent counter,
            in FighterKnockdownComponent knockdown) {
            FighterSpecialCancelRules.Feed(ref action, in fighter, in runtime, hitstopTick: false);
            if (!FighterSpecialCastRules.IsBusy(in action)) return false;
            // CM-FIXB-SIM (B12): while Foresight's counter strike is still owed
            // (a caught projectile, answered by this tick's combat pass) neither
            // cancel may end the stance — ending it would drop the answer. The
            // tick's input is discarded as in any locked cast tick; the D3 press
            // stays buffered and the Block cancel is open from the next tick.
            bool inRecovery = FighterSpecialCancelRules.TryGetRecoveryFrame(in action, in counter, out int recoveryFrame)
                && !FighterForesightRules.IsAnswerOwed(in counter);
            if (inRecovery && FighterBasicAttackRules.IsBlockStance(in fighter, in runtime, in verb)) {
                EndCastForCancel(ref fighter, ref action, ref counter);
                runtime.PressedButtons &= BlockButton;
                return false;
            }
            if (inRecovery && TryFireSpecialCancel(ref fighter, ref runtime, in verb, ref action, in knockdown, recoveryFrame)) {
                EndCastForCancel(ref fighter, ref action, ref counter);
                return true;
            }
            FighterUltimateActivationRules.DiscardInput(ref runtime);
            return false;
        }

        /// <summary>
        /// D3: fires the buffered cancel when the cast landed a direct hit, the
        /// press is still buffered, the verb's frame gate is open at
        /// <paramref name="recoveryFrame"/> and the verb can start now. A verb that
        /// cannot start yet stays buffered until it ages out. On firing the tick's
        /// input is rewritten to the verb alone; the caller ends the cast.
        /// </summary>
        private static bool TryFireSpecialCancel(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            in FighterVerbComponent verb,
            ref FighterActionStateComponent action,
            in FighterKnockdownComponent knockdown,
            int recoveryFrame) {
            if (!FighterSpecialCancelRules.LandedDirectHit(in action)) return false;
            int cancelVerb = FighterSpecialCancelRules.BufferedVerb(in action);
            if (cancelVerb == FighterSpecialCancelRules.VerbNone) return false;
            if (!FTT.Combat.BasicComboRules.SpecialCancelBufferHolds(FighterSpecialCancelRules.BufferedAge(in action))) return false;
            if (!FighterSpecialCancelRules.FrameGateOpen(cancelVerb, recoveryFrame)) return false;
            if (!CancelVerbCanStart(cancelVerb, in fighter, in runtime, in verb, in knockdown)) return false;
            FighterSpecialCancelRules.RewriteInput(
                ref runtime,
                cancelVerb,
                FighterSpecialCancelRules.BufferedUpHeld(in action),
                FighterSpecialCancelRules.BufferedDownHeld(in action));
            return true;
        }

        /// <summary>
        /// Whether the fired verb's ordinary path would start it this tick — the
        /// same gates <c>TryStartUniversalMovement</c>, <c>ApplyNormalMovement</c>,
        /// <c>ProcessBasicAttackPhase</c> and the ability system's movement branch
        /// read, so a fired cancel never ends the cast for a verb that then does
        /// nothing.
        /// </summary>
        private static bool CancelVerbCanStart(
            int cancelVerb,
            in FighterStateComponent fighter,
            in FighterRuntimeComponent runtime,
            in FighterVerbComponent verb,
            in FighterKnockdownComponent knockdown) {
            bool rooted = runtime.StatusType == (int)StatusType.Root;
            bool movementIdle = runtime.UniversalMovementState == (int)UniversalMovementPhase.None;
            switch (cancelVerb) {
                case FighterSpecialCancelRules.VerbRoll:
                    if (rooted || !movementIdle) return false;
                    return fighter.IsGrounded != 0
                        ? verb.ShieldStunFrames <= 0
                        : knockdown.AirDodgeUsed == 0;
                case FighterSpecialCancelRules.VerbJump:
                    if (rooted || !movementIdle || verb.ShieldStunFrames > 0) return false;
                    return fighter.IsGrounded != 0 || fighter.RemainingJumps > 0;
                case FighterSpecialCancelRules.VerbBasic:
                    return runtime.AttackPhase == FighterBasicAttackRules.PhaseNone
                        && !FighterUniversalMovementRules.IsCombatLocked(in runtime);
                case FighterSpecialCancelRules.VerbMovement:
                    return !rooted
                        && runtime.MovementCooldownFrames <= 0
                        && runtime.AttackPhase == FighterBasicAttackRules.PhaseNone
                        && !FighterUniversalMovementRules.IsCombatLocked(in runtime);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Ends a cast for its Block cancel or its D3 cancel: the cast and the
        /// buffer, and for Foresight (Delegated) the component-322 stance, whose
        /// sidestep invulnerability is cut and nothing else
        /// (<see cref="FighterForesightRules.CancelStance"/>). The cooldown, the
        /// lockout and what the cast put into the world stand.
        /// </summary>
        private static void EndCastForCancel(
            ref FighterStateComponent fighter,
            ref FighterActionStateComponent action,
            ref FighterCounterComponent counter) {
            if (FighterSpecialCastRules.IsDelegated(in action)) {
                FighterForesightRules.CancelStance(ref counter, ref fighter);
            }
            FighterSpecialCastRules.Clear(ref action);
            action.SpecialCancelBuffer = 0;
        }
    }
}
