using FTT.Core;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace FTT.FighterSim {

    /// <summary>
    /// Deterministic ledge grab (gameplay-feel plan §2.11), the sim's first slice
    /// of audit M-16. Only one-way platform ends are grabbable: a stage's base
    /// floor spans wall to wall and its side walls are solid, so there is no other
    /// edge to catch. The whole feature is three snapshotted ints in
    /// <see cref="FighterRuntimeComponent"/> — the hang position is re-derived from
    /// the stage geometry every tick, since geometry is constant for the match and
    /// never enters a snapshot.
    /// </summary>
    internal static class FighterLedgeRules {
        /// <summary><see cref="FighterRuntimeComponent.LedgeAnchor"/> for "not hanging".</summary>
        public const int NoAnchor = -1;
        /// <summary>Hang ends on its own after five seconds, with the regrab lockout.</summary>
        public const int AutoReleaseFrames = 300;
        /// <summary>Half a second of no regrab after a Down release or an auto-release.</summary>
        public const int RegrabLockoutFrames = 30;

        /// <summary>Capture box half-width around the platform end.</summary>
        public static readonly FP64 CaptureHalfWidth = FP64.FromDouble(0.5);
        /// <summary>Capture box depth below the platform surface.</summary>
        public static readonly FP64 CaptureDepth = FP64.FromDouble(1.2);
        /// <summary>How far outside the platform the hang position sits.</summary>
        public static readonly FP64 HangOutwardOffset = FP64.FromDouble(0.25);
        /// <summary>How far below the surface the hang position sits.</summary>
        public static readonly FP64 HangDepth = FP64.One;
        /// <summary>
        /// Vertical-speed ceiling for a grab. Positive so a fighter still rising
        /// slowly near the apex catches the ledge they jumped up to; anything
        /// faster than this is a jump that clears the edge, not a grab.
        /// </summary>
        public static readonly FP64 MaxGrabVerticalSpeed = FP64.FromInt(2);
        /// <summary>The climb jump is nine-tenths of a ground jump.</summary>
        public static readonly FP64 ClimbJumpMultiplier = FP64.FromDouble(0.9);

        private const int JumpButton = 1 << 0;
        private const int DownButton = 1 << 1;

        public static bool IsHanging(in FighterRuntimeComponent runtime) => runtime.LedgeAnchor >= 0;

        public static void ClearHang(ref FighterRuntimeComponent runtime) {
            runtime.LedgeAnchor = NoAnchor;
            runtime.LedgeStateFrames = 0;
        }

        /// <summary>
        /// The fighter-state half of the grab test. Geometry is the caller's job
        /// (<see cref="FighterStageGeometry.TryFindLedge"/>) so the two halves stay
        /// independently testable. V7.3 regrab cap: only
        /// <see cref="FTT.Combat.BasicComboRules.LedgeRegrabsPerAirtime"/> grabs
        /// per airtime — the next is refused until grounding (or a stock loss)
        /// resets the budget.
        /// </summary>
        public static bool CanGrab(
            in FighterStateComponent fighter,
            in FighterRuntimeComponent runtime,
            in FighterVerbComponent verb) =>
            fighter.Stocks > 0
            && fighter.IsGrounded == 0
            && fighter.RespawnFramesRemaining <= 0
            && fighter.DropThroughFrames == 0
            && fighter.HitstunFrames <= 0
            && fighter.DazeFrames <= 0
            && runtime.LedgeRegrabLockoutFrames <= 0
            && verb.LedgeGrabsThisAirtime < FTT.Combat.BasicComboRules.LedgeRegrabsPerAirtime
            && !IsHanging(in runtime)
            && fighter.Velocity.y <= MaxGrabVerticalSpeed;

        /// <summary>
        /// Latches the hang: pinned position, zeroed velocity, refilled jumps, and
        /// a cancelled swing/roll. Gravity is skipped for as long as the anchor is
        /// set, so nothing else has to know about the state.
        /// </summary>
        public static void Grab(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            ref FighterVerbComponent verb,
            in FighterTuningComponent tuning,
            int anchor,
            in FPVector2 hangPosition) {
            // V7.3 regrab budget: every latched grab spends one of the
            // per-airtime allowance CanGrab enforces.
            verb.LedgeGrabsThisAirtime++;
            runtime.LedgeAnchor = anchor;
            runtime.LedgeStateFrames = 0;
            fighter.Position = hangPosition;
            fighter.Velocity = FPVector2.Zero;
            // Jumps refill on the grab, which is what makes the climb jump the
            // ground-jump equivalent rather than an air jump.
            fighter.RemainingJumps = tuning.MaxJumpCount;
            // The fighter faces the stage they are about to climb onto: side 0 is
            // the platform's left edge, so the platform is to their right.
            fighter.FacingRight = anchor % 2 == 0 ? 1 : 0;
            FighterUniversalMovementRules.Cancel(ref runtime);
            FighterBasicAttackRules.CancelString(ref runtime);
            runtime.FloatFrames = 0;
        }

        /// <summary>
        /// Advances one hang tick. Returns true while the fighter is still hanging,
        /// which is the movement system's signal to skip gravity, movement, and the
        /// attack phase machine entirely — the same short-circuit hitstun gets.
        /// </summary>
        public static bool Process(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            in FighterTuningComponent tuning,
            in FPVector2 hangPosition) {
            fighter.Position = hangPosition;
            fighter.Velocity = FPVector2.Zero;
            fighter.IsGrounded = 0;

            // Jump climbs. It does not spend a jump: the grab refilled them and the
            // climb is the ground-jump equivalent, so the fighter leaves with the
            // same budget a jump off the floor would have left.
            if ((runtime.PressedButtons & JumpButton) != 0) {
                ClearHang(ref runtime);
                fighter.Velocity.y = tuning.JumpSpeed * ClimbJumpMultiplier;
                fighter.RemainingJumps = tuning.MaxJumpCount - 1;
                return false;
            }

            // Down drops off. Fast-fall (§2.9) never fights this: release wins, and
            // because Down is still held the fighter fast-falls immediately after,
            // which is the intended "drop fast off the ledge" input.
            if ((runtime.HeldButtons & DownButton) != 0) {
                Release(ref fighter, ref runtime);
                return false;
            }

            runtime.LedgeStateFrames++;
            if (runtime.LedgeStateFrames >= AutoReleaseFrames) {
                Release(ref fighter, ref runtime);
                return false;
            }
            return true;
        }

        /// <summary>Drops off into a fall and arms the regrab lockout.</summary>
        public static void Release(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime) {
            ClearHang(ref runtime);
            runtime.LedgeRegrabLockoutFrames = RegrabLockoutFrames;
            fighter.Velocity = FPVector2.Zero;
            fighter.IsGrounded = 0;
        }
    }
}
