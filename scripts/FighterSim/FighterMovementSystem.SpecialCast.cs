using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace FTT.FighterSim {

    /// <summary>
    /// Decision 1.2 (combat and movement update 2026-10-16, plan §2.1.6): the
    /// movement system's half of the Fighter Special cast — the clock, the
    /// active-entry lunge and the cast's motion rules. The acceptance and the
    /// fire pass live in <see cref="FighterAbilityEntitySystem"/>, the melee
    /// contact in <see cref="FighterCombatSystem"/>.
    /// </summary>
    public sealed partial class FighterMovementSystem {

        /// <summary>
        /// One tick of the cast clock, called after the hitstop gate. The
        /// interrupt set ends a busy cast first; a Delegated cast (Foresight)
        /// ends when its component-322 stance does. On the active-entry tick
        /// (T+S) Divine Piercing's lunge starts here, before this tick's kit
        /// phase runs, so its moving ticks are exactly the 12 active ticks (a
        /// lunge started by the ability system's fire pass would first move on
        /// T+S+1, because this system integrates before that one runs).
        /// </summary>
        private void AdvanceSpecialCast(
            ref Frame frame,
            EntityRef entity,
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            in FighterVerbComponent verb,
            ref FighterActionStateComponent action,
            in FighterCounterComponent counter,
            bool captured,
            bool knockedDown) {
            if (!FighterSpecialCastRules.IsBusy(in action)) return;
            if (FighterSpecialCastRules.ClearIfInterrupted(ref action, in fighter, in verb, captured, knockedDown)) return;
            if (FighterSpecialCastRules.IsDelegated(in action)) {
                if (!FighterForesightRules.IsBusy(in counter)) FighterSpecialCastRules.Clear(ref action);
                return;
            }
            FighterAbilityHitData contract = _contracts.For(fighter.PlayerID, action.SpecialSlot);
            bool wasActive = FighterSpecialCastRules.IsActive(in action);
            bool enteredActive = FighterSpecialCastRules.Advance(ref action, in contract);
            // Fix round A (CM-FIXA-SIM): the Piercing lunge ends with the active
            // window. Its 12th move (the last active tick) spends its last frame,
            // but its kit step cancels the 305 slot only on its next call — after
            // this tick's cast region has already read the stance — so the stale
            // lunge state refused the recovery Block cancel on recovery frame 0.
            // Ending it here, on the Active → Recovery tick, is exactly what that
            // kit step does later in the same tick (cancel, X 0), so motion is
            // unchanged; the final thrust already resolved on the last active tick.
            if (wasActive && !FighterSpecialCastRules.IsActive(in action)
                && runtime.UniversalMovementState == FighterKitMotion.PiercingLunge
                && runtime.UniversalMovementFramesRemaining <= 0) {
                FighterUniversalMovementRules.Cancel(ref runtime);
                fighter.Velocity.x = FP64.Zero;
            }
            if (!enteredActive) return;
            ref readonly FighterAbilityModeComponent modes = ref frame.GetReadOnly<FighterAbilityModeComponent>(entity);
            if (FighterSpecialCastRules.IsPiercingLunge(in fighter, in modes, action.SpecialSlot)
                && !FighterSpecialCastRules.HasFlag(in action, FighterSpecialCastRules.FlagEffectFired)) {
                FighterKitMotion.StartPiercingLunge(
                    ref runtime, fighter.FacingRight != 0 ? 1 : -1, contract.ActiveFrames);
                action.SpecialFlags |= FighterSpecialCastRules.FlagEffectFired;
            }
        }

        /// <summary>
        /// The Q1 ruling (2026-10-16): an AIRBORNE cast keeps its horizontal
        /// drift — no stick (it is discarded) and no added friction — so the
        /// normal air control step is skipped. A grounded cast runs the normal
        /// step with its discarded (zero) stick, which is the run-stop ramp:
        /// the same per-tick step <c>ApplyNormalMovement</c> takes for a released
        /// stick, to zero within <c>UniversalMovementRules.RunDecelerationFrames</c>.
        /// A kit motion (the Piercing lunge) owns the tick before either.
        /// </summary>
        private static bool CastKeepsAirDrift(in FighterActionStateComponent action, in FighterStateComponent fighter) =>
            FighterSpecialCastRules.IsCasting(in action) && fighter.IsGrounded == 0;

        /// <summary>
        /// A cast that started airborne falls at half gravity through its startup
        /// and active frames (design :1097), through the one shared constant
        /// <c>UniversalMovementRules.AerialSpecialGravityMultiplier</c>; in
        /// recovery, and for a grounded cast that left the floor, the step is the
        /// normal one. Fix round A (CM-FIXA-SIM): a Delegated cast (Foresight)
        /// does the same through its component-322 startup and window — Story's
        /// Startup and Active — and its sidestep and whiff recovery fall on the
        /// normal step.
        /// </summary>
        private static bool CastHalvesGravity(in FighterActionStateComponent action, in FighterCounterComponent counter) =>
            FighterSpecialCastRules.HasFlag(in action, FighterSpecialCastRules.FlagStartedAerial)
            && (FighterSpecialCastRules.IsStartup(in action)
                || FighterSpecialCastRules.IsActive(in action)
                || (FighterSpecialCastRules.IsDelegated(in action)
                    && (counter.Phase == FighterForesightRules.PhaseStartup
                        || counter.Phase == FighterForesightRules.PhaseWindow)));
    }
}
