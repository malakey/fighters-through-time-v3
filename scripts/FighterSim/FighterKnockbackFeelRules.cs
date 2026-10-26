using xpTURN.Klotho.Deterministic.Math;

namespace FTT.FighterSim {

    /// <summary>
    /// FU-KNOCK (feel and UI update 2026-10-25, Fighter Mode only): the fixed-point
    /// statement of the three knockback-feel rules - the finisher's doubled
    /// horizontal travel (R1), the launched-fighter wall bounce (R2) and the
    /// grounded hitstun shove (R3). The numbers live once in
    /// <c>FTT.Combat.BasicComboRules</c> ("Fighter knockback feel") as doubles; this
    /// class converts them with <c>FP64.FromDouble</c> and holds the pure
    /// state steps. Story reads none of it (its finisher is the profile tenths,
    /// its wall bounce the D9 rule in the Story enemy rulebook).
    /// <para>
    /// State: the bounce uses component 323's <c>ActionFlags</c> bit 4
    /// (<see cref="FighterActionStateComponent.ActionFlagWallBounced"/>, once per
    /// launch) and bits 8-11 (a wrapping 4-bit bounce counter the driver
    /// edge-detects for the impact effect). No new component.
    /// </para>
    /// </summary>
    public static class FighterKnockbackFeelRules {

        /// <summary>R1: horizontal launch scale of the finisher.</summary>
        public static readonly FP64 FinisherHorizontalScale =
            FP64.FromDouble(FTT.Combat.BasicComboRules.FighterFinisherHorizontalScale);

        /// <summary>R1: the vertical scale that keeps the finisher's vertical at the force on the doubled impulse.</summary>
        public static readonly FP64 FinisherVerticalScale = FP64.One / FinisherHorizontalScale;

        /// <summary>R2: minimum |vx| (u/s) into a wall to bounce.</summary>
        public static readonly FP64 WallBounceMinSpeed =
            FP64.FromDouble(FTT.Combat.BasicComboRules.FighterWallBounceMinSpeed);

        /// <summary>R2: the fraction of the speed the rebound keeps.</summary>
        public static readonly FP64 WallBounceRestitution =
            FP64.FromDouble(FTT.Combat.BasicComboRules.FighterWallBounceRestitution);

        /// <summary>R2: the bouncing fighter's own freeze, in ticks.</summary>
        public const int WallBounceHitstopFrames = FTT.Combat.BasicComboRules.FighterWallBounceHitstopFrames;

        /// <summary>R3: grounded non-launching hit speed multiplier.</summary>
        public static readonly FP64 ShoveScale =
            FP64.FromDouble(FTT.Combat.BasicComboRules.FighterHitstunShoveScale);

        /// <summary>R3: per-tick speed multiplier during plain grounded hitstun.</summary>
        public static readonly FP64 ShoveDecay =
            FP64.FromDouble(FTT.Combat.BasicComboRules.FighterHitstunShoveDecay);

        /// <summary>R3: the speed below which the shove snaps to zero.</summary>
        public static readonly FP64 ShoveSnapSpeed =
            FP64.FromDouble(FTT.Combat.BasicComboRules.FighterHitstunShoveSnapSpeed);

        private const int BounceCounterShift = 8;
        private const int BounceCounterMask = 0xF << BounceCounterShift;

        /// <summary>The 4-bit wrapping bounce count (0..15) in component 323.</summary>
        public static int BounceCount(in FighterActionStateComponent action) =>
            (action.ActionFlags & BounceCounterMask) >> BounceCounterShift;

        /// <summary>True once the current launch has bounced.</summary>
        public static bool HasBounced(in FighterActionStateComponent action) =>
            (action.ActionFlags & FighterActionStateComponent.ActionFlagWallBounced) != 0;

        /// <summary>A new launch re-arms the bounce.</summary>
        public static void RearmBounce(ref FighterActionStateComponent action) =>
            action.ActionFlags &= ~FighterActionStateComponent.ActionFlagWallBounced;

        /// <summary>
        /// R3: one tick of the grounded shove - multiply by the decay and snap
        /// to zero below <see cref="ShoveSnapSpeed"/>.
        /// </summary>
        public static FP64 StepShove(FP64 vx) {
            FP64 next = vx * ShoveDecay;
            return FP64.Abs(next) < ShoveSnapSpeed ? FP64.Zero : next;
        }

        /// <summary>
        /// R2: after the tick's move, bounce a launched fighter that crossed a
        /// side wall at speed. <paramref name="unclampedX"/> is the position
        /// before the wall clamp. No damage, hitstun or meter change: the speed
        /// reverses at <see cref="WallBounceRestitution"/>, the fighter alone
        /// freezes <see cref="WallBounceHitstopFrames"/> ticks, the once-per-
        /// launch flag is set and the counter advances. Returns true on a bounce.
        /// </summary>
        public static bool TryWallBounce(
            ref FighterStateComponent fighter,
            ref FighterVerbComponent verb,
            ref FighterActionStateComponent action,
            FP64 unclampedX,
            FP64 leftWall,
            FP64 rightWall) {
            if (verb.Tumble == FighterVerbRules.TumbleNone) return false;
            if (HasBounced(in action)) return false;
            bool intoRight = unclampedX > rightWall && fighter.Velocity.x >= WallBounceMinSpeed;
            bool intoLeft = unclampedX < leftWall && fighter.Velocity.x <= -WallBounceMinSpeed;
            if (!intoRight && !intoLeft) return false;

            fighter.Velocity.x = -fighter.Velocity.x * WallBounceRestitution;
            if (verb.HitstopFrames < WallBounceHitstopFrames) verb.HitstopFrames = WallBounceHitstopFrames;
            int count = (BounceCount(in action) + 1) & 0xF;
            action.ActionFlags = (action.ActionFlags & ~BounceCounterMask)
                | (count << BounceCounterShift)
                | FighterActionStateComponent.ActionFlagWallBounced;
            return true;
        }
    }
}
