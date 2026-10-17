using FTT.Core;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace FTT.FighterSim {

    internal static class FighterUniversalMovementRules {
        public static bool IsCombatLocked(in FighterRuntimeComponent runtime) {
            UniversalMovementPhase phase = (UniversalMovementPhase)runtime.UniversalMovementState;
            return phase is UniversalMovementPhase.RollStartup
                or UniversalMovementPhase.RollTravel
                or UniversalMovementPhase.RollRecovery
                // A03 (Package 13 W1): the air dodge is a Rolling sub-phase.
                or UniversalMovementPhase.AirDodgeStartup
                or UniversalMovementPhase.AirDodgeInvulnerable
                or UniversalMovementPhase.AirDodgeRecovery
                // Package 12 W4 / 13 W5: a blink or a North Star Leap in flight is an action.
                || FighterKitMotion.IsKitPhase(runtime.UniversalMovementState)
                // CM-MOVE (decision 5.2): a ledge climb (values 8–10) is an action
                // too — no swing, Special, Ultimate or movement ability starts in it.
                || FighterLedgeRules.IsGetUpClimb(runtime.UniversalMovementState);
        }

        public static void Cancel(ref FighterRuntimeComponent runtime) {
            runtime.UniversalMovementState = (int)UniversalMovementPhase.None;
            runtime.UniversalMovementFramesRemaining = 0;
            runtime.UniversalMovementDirection = 0;
        }
    }

    /// <summary>
    /// A03 (Package 13 W1): the deterministic air dodge — Roll pressed while
    /// airborne. It runs in the existing universal-movement slot of component
    /// 305 as phases 5–7 (<see cref="UniversalMovementPhase.AirDodgeStartup"/> …
    /// <see cref="UniversalMovementPhase.AirDodgeRecovery"/>), so hits cancel it
    /// and it snapshots for free; the only new state is the once-per-airtime
    /// latch <c>AirDodgeUsed</c> on component 320 (plan D7).
    ///
    /// <para>No speed is added: gravity, momentum and normal air control keep
    /// running (the movement system still integrates them), and a held
    /// direction contributes only a positional shift of
    /// <see cref="UniversalMovementRules.AirDodgeShiftUnits"/> spread evenly over
    /// the eight invulnerable frames. The invulnerable frames also switch the
    /// pushbox off. Landing does not cancel the dodge — the remaining recovery
    /// plays out grounded, decelerating like a roll recovery.</para>
    ///
    /// <para><see cref="FighterRuntimeComponent.UniversalMovementDirection"/>
    /// carries the held direction as <c>(dx + 1) * 3 + (dy + 1)</c>, dx and dy in
    /// {-1, 0, 1} (4 = neutral).</para>
    /// </summary>
    internal static class FighterAirDodgeRules {
        private const int NeutralDirectionCode = 4;
        private static readonly FP64 AxisShiftPerFrame = FP64.FromDouble(
            UniversalMovementRules.AirDodgeShiftUnits / UniversalMovementRules.AirDodgeInvulnerableFrames);
        private static readonly FP64 DiagonalShiftPerFrame = FP64.FromDouble(
            UniversalMovementRules.AirDodgeShiftUnits / UniversalMovementRules.AirDodgeInvulnerableFrames
                * 0.70710678118654752);

        public static bool IsAirDodge(int universalMovementState) =>
            universalMovementState is (int)UniversalMovementPhase.AirDodgeStartup
                or (int)UniversalMovementPhase.AirDodgeInvulnerable
                or (int)UniversalMovementPhase.AirDodgeRecovery;

        /// <summary>The invulnerable, pushbox-free window.</summary>
        public static bool IsInvulnerablePhase(in FighterRuntimeComponent runtime) =>
            runtime.UniversalMovementState == (int)UniversalMovementPhase.AirDodgeInvulnerable;

        /// <summary>
        /// Starts an air dodge if one is legal: airborne, not already dodged
        /// this airtime, not rooted (A05), no other universal movement running.
        /// The caller has already excluded hitstun, daze, hitstop, grab states,
        /// the ledge hang and any live swing (an air strike's recovery, or a
        /// dropped-through string's chain window, is cancelled by the same Roll
        /// press just before this).
        /// </summary>
        public static bool TryStart(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            ref FighterKnockdownComponent knockdown,
            bool rooted) {
            if (rooted
                || fighter.IsGrounded != 0
                || knockdown.AirDodgeUsed != 0
                || runtime.UniversalMovementState != (int)UniversalMovementPhase.None) return false;
            int dx = runtime.MoveX > 30 ? 1 : runtime.MoveX < -30 ? -1 : 0;
            // World Y is up; a negative MoveY stick value is "up held".
            int dy = runtime.MoveY < -30 ? 1 : runtime.MoveY > 30 ? -1 : 0;
            knockdown.AirDodgeUsed = 1;
            runtime.UniversalMovementState = (int)UniversalMovementPhase.AirDodgeStartup;
            runtime.UniversalMovementFramesRemaining = UniversalMovementRules.AirDodgeStartupFrames;
            runtime.UniversalMovementDirection = (dx + 1) * 3 + (dy + 1);
            return true;
        }

        /// <summary>
        /// Advances one dodge tick. Returns true when the dodge owns this tick's
        /// horizontal motion (a grounded recovery, decelerating); false while
        /// airborne, so normal air control still runs — with jumps withheld.
        /// </summary>
        public static bool Advance(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            FP64 groundDecelStep) {
            UniversalMovementPhase phase = (UniversalMovementPhase)runtime.UniversalMovementState;
            if (phase == UniversalMovementPhase.AirDodgeInvulnerable) {
                if (runtime.UniversalMovementFramesRemaining == UniversalMovementRules.AirDodgeInvulnerableFrames
                    && fighter.InvulnerabilityFrames < UniversalMovementRules.AirDodgeInvulnerableFrames) {
                    fighter.InvulnerabilityFrames = UniversalMovementRules.AirDodgeInvulnerableFrames;
                }
                ApplyShift(ref fighter, runtime.UniversalMovementDirection);
            }

            runtime.UniversalMovementFramesRemaining--;
            if (runtime.UniversalMovementFramesRemaining <= 0) {
                switch (phase) {
                    case UniversalMovementPhase.AirDodgeStartup:
                        runtime.UniversalMovementState = (int)UniversalMovementPhase.AirDodgeInvulnerable;
                        runtime.UniversalMovementFramesRemaining = UniversalMovementRules.AirDodgeInvulnerableFrames;
                        break;
                    case UniversalMovementPhase.AirDodgeInvulnerable:
                        runtime.UniversalMovementState = (int)UniversalMovementPhase.AirDodgeRecovery;
                        runtime.UniversalMovementFramesRemaining = UniversalMovementRules.AirDodgeRecoveryFrames;
                        break;
                    default:
                        FighterUniversalMovementRules.Cancel(ref runtime);
                        break;
                }
            }

            if (fighter.IsGrounded == 0) return false;
            // Landed mid-dodge: the rest plays out grounded, bleeding speed off.
            if (fighter.Velocity.x > FP64.Zero) {
                fighter.Velocity.x = FP64.Max(fighter.Velocity.x - groundDecelStep, FP64.Zero);
            } else if (fighter.Velocity.x < FP64.Zero) {
                fighter.Velocity.x = FP64.Min(fighter.Velocity.x + groundDecelStep, FP64.Zero);
            }
            return true;
        }

        private static void ApplyShift(ref FighterStateComponent fighter, int directionCode) {
            if (directionCode == NeutralDirectionCode) return;
            int dx = directionCode / 3 - 1;
            int dy = directionCode % 3 - 1;
            FP64 step = dx != 0 && dy != 0 ? DiagonalShiftPerFrame : AxisShiftPerFrame;
            fighter.Position.x += step * FP64.FromInt(dx);
            fighter.Position.y += step * FP64.FromInt(dy);
        }

        /// <summary>Landing, a ledge grab or a stock loss refreshes the once-per-airtime dodge.</summary>
        public static void Refresh(ref FighterKnockdownComponent knockdown) => knockdown.AirDodgeUsed = 0;
    }
}
