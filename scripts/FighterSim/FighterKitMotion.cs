using FTT.Combat;
using xpTURN.Klotho.Deterministic.Math;

namespace FTT.FighterSim {

    /// <summary>
    /// Package 12 W4 — the deterministic half of the kit moves that
    /// translate their caster over several frames: Tesla's Lightning Blink
    /// (6 startup / 12 translation / 10 recovery, projectile pass-through
    /// during the translation only) and, since Package 13 W5, Harriet Tubman's
    /// North Star Leap (an 18-frame guided leap in any of eight directions,
    /// then one settle frame, with the extended ledge snap).
    ///
    /// <para><b>No new state and no component ID.</b> Both moves run as
    /// sim-local phases of the existing universal-movement slot on
    /// <see cref="FighterRuntimeComponent"/> —
    /// <c>UniversalMovementState</c> / <c>UniversalMovementFramesRemaining</c> /
    /// <c>UniversalMovementDirection</c>, the three ints the roll already uses.
    /// That buys everything the moves need for free: the slot is snapshotted and
    /// hashed, a hit that carries impulse or any hitstun cancels it through the
    /// existing <c>FighterUniversalMovementRules.Cancel</c> sites, and
    /// <c>IsCombatLocked</c> keeps attacks, specials and grabs out while it runs.
    /// The phase codes start at <see cref="FirstPhase"/> so they can never
    /// collide with the shared <c>UniversalMovementPhase</c> enum (0–4, with the
    /// retired Dash ordinal reserved).</para>
    ///
    /// <para>Every number comes from <see cref="KitMotionRules"/> (shared with
    /// Story) or <see cref="TubmanKitRules"/>, or from the normalized loadout
    /// (<c>MovementDistance</c>, <c>MovementDurationFrames</c>). Nothing here
    /// reads a Story modifier: Long Blink and Star Guide never reach the sim.</para>
    ///
    /// <para><b>Retired codes.</b> 19 and 20 were Pocahontas's Spirit Strike
    /// (startup, carry). They left with her in the Package 13 roster swap and
    /// are never reused; the leap takes 21 and 22.</para>
    /// </summary>
    public static class FighterKitMotion {
        public const int FirstPhase = 16;
        public const int BlinkStartup = 16;
        public const int BlinkTravel = 17;
        public const int BlinkRecovery = 18;
        // 19 (SpiritStartup) and 20 (SpiritCarry) are retired: never reuse.
        /// <summary>North Star Leap: the guided 8-way travel.</summary>
        public const int LeapTravel = 21;
        /// <summary>North Star Leap: one settle frame at the end (velocity 0, gravity still off) in which the extended ledge snap can catch.</summary>
        public const int LeapEnd = 22;
        private const int LastPhase = 22;

        private static readonly FP64 FixedDelta = FP64.One / FP64.FromInt(FighterSimulation.TickRate);
        private static readonly FP64 InverseSqrtTwo = FP64.FromDouble(0.70710678118654752);
        /// <summary>
        /// The leap's ledge-snap bonus: <see cref="TubmanKitRules.NorthStarLeapLedgeSnapBonusUnits"/>
        /// added to <c>FighterLedgeRules.CaptureHalfWidth</c> while the leap flies and on its settle frame.
        /// </summary>
        public static readonly FP64 LeapLedgeSnapBonus = FP64.FromDouble(TubmanKitRules.NorthStarLeapLedgeSnapBonusUnits);

        public static bool IsKitPhase(int state) => state >= FirstPhase && state <= LastPhase;

        /// <summary>
        /// The Lightning Blink pass-through window: the translation, and only
        /// the translation. <c>FighterProjectileSystem</c> reads this after the
        /// movement system has advanced the phase for the tick.
        /// </summary>
        public static bool PassesThroughProjectiles(in FighterRuntimeComponent runtime) =>
            runtime.UniversalMovementState == BlinkTravel;

        /// <summary>Startup hovers and the translation/carry fly straight: no gravity.</summary>
        public static bool SuspendsGravity(in FighterRuntimeComponent runtime) =>
            runtime.UniversalMovementState is BlinkStartup or BlinkTravel or LeapTravel or LeapEnd;

        /// <summary>
        /// North Star Leap's extended ledge snap window: the travel and the
        /// settle frame. <c>FighterMovementSystem.TryGrabLedge</c> widens the
        /// capture box by <see cref="LeapLedgeSnapBonus"/> while this holds.
        /// </summary>
        public static bool HasLedgeSnapBonus(in FighterRuntimeComponent runtime) =>
            runtime.UniversalMovementState is LeapTravel or LeapEnd;

        // --- activation -------------------------------------------------------

        /// <summary>
        /// Starts the blink with its direction latched from the held stick
        /// (world Y up, so a negative MoveY is an upward blink). The direction is
        /// packed into the one int the roll uses: (x + 1) + 3 × (y + 1).
        /// </summary>
        public static void StartBlink(ref FighterRuntimeComponent runtime, int facing) {
            int directionX = runtime.MoveX > 30 ? 1 : runtime.MoveX < -30 ? -1 : 0;
            int directionY = runtime.MoveY < -30 ? 1 : runtime.MoveY > 30 ? -1 : 0;
            if (directionX == 0 && directionY == 0) directionX = facing;
            runtime.UniversalMovementState = BlinkStartup;
            runtime.UniversalMovementFramesRemaining = KitMotionRules.LightningBlinkStartupFrames;
            runtime.UniversalMovementDirection = (directionX + 1) + 3 * (directionY + 1);
        }

        /// <summary>
        /// Starts North Star Leap with its direction latched from the held stick
        /// (any of eight directions; a neutral stick leaps along facing), packed
        /// like the blink's. There is no startup: the leap travels from the next
        /// tick for <paramref name="travelFrames"/> frames (the loadout's
        /// <c>MovementDurationFrames</c>).
        /// </summary>
        public static void StartNorthStarLeap(ref FighterRuntimeComponent runtime, int facing, int travelFrames) {
            int directionX = runtime.MoveX > 30 ? 1 : runtime.MoveX < -30 ? -1 : 0;
            int directionY = runtime.MoveY < -30 ? 1 : runtime.MoveY > 30 ? -1 : 0;
            if (directionX == 0 && directionY == 0) directionX = facing;
            runtime.UniversalMovementState = LeapTravel;
            runtime.UniversalMovementFramesRemaining = travelFrames > 0
                ? travelFrames
                : TubmanKitRules.NorthStarLeapTravelFrames;
            runtime.UniversalMovementDirection = (directionX + 1) + 3 * (directionY + 1);
        }

        public static int BlinkDirectionX(int packed) => packed % 3 - 1;
        public static int BlinkDirectionY(int packed) => packed / 3 - 1;

        // --- per-tick advance (FighterMovementSystem) --------------------------

        /// <summary>
        /// Advances a kit phase by one tick and writes the velocity the movement
        /// system then integrates. A phase whose frame count reached zero on the
        /// previous tick hands over at the start of this one, so every phase
        /// lasts exactly its authored frame count and the pass-through window is
        /// exactly the twelve ticks that move. Returns false once the move has
        /// ended, so ordinary movement resumes on that same tick.
        /// </summary>
        public static bool Process(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            in FighterAbilityModeComponent modes,
            FP64 decelStep) {
            switch (runtime.UniversalMovementState) {
                case BlinkStartup:
                    if (runtime.UniversalMovementFramesRemaining <= 0) {
                        runtime.UniversalMovementState = BlinkTravel;
                        runtime.UniversalMovementFramesRemaining = BlinkTravelFrames(in modes);
                        goto case BlinkTravel;
                    }
                    fighter.Velocity = FPVector2.Zero;
                    runtime.UniversalMovementFramesRemaining--;
                    return true;

                case BlinkTravel:
                    if (runtime.UniversalMovementFramesRemaining <= 0) {
                        runtime.UniversalMovementState = BlinkRecovery;
                        runtime.UniversalMovementFramesRemaining = KitMotionRules.LightningBlinkRecoveryFrames;
                        fighter.Velocity = FPVector2.Zero;
                        goto case BlinkRecovery;
                    }
                    ApplyBlinkVelocity(ref fighter, in runtime, in modes);
                    runtime.UniversalMovementFramesRemaining--;
                    return true;

                case BlinkRecovery:
                    if (runtime.UniversalMovementFramesRemaining <= 0) {
                        FighterUniversalMovementRules.Cancel(ref runtime);
                        return false;
                    }
                    fighter.Velocity.x = MoveToward(fighter.Velocity.x, FP64.Zero, decelStep);
                    runtime.UniversalMovementFramesRemaining--;
                    return true;

                case LeapTravel:
                    if (runtime.UniversalMovementFramesRemaining <= 0) {
                        runtime.UniversalMovementState = LeapEnd;
                        runtime.UniversalMovementFramesRemaining = 1;
                        goto case LeapEnd;
                    }
                    ApplyLeapVelocity(ref fighter, in runtime, in modes);
                    runtime.UniversalMovementFramesRemaining--;
                    return true;

                case LeapEnd:
                    if (runtime.UniversalMovementFramesRemaining <= 0) {
                        FighterUniversalMovementRules.Cancel(ref runtime);
                        return false;
                    }
                    // The leap ends where it ends: no carried momentum, and one
                    // weightless frame in which the extended ledge snap can catch.
                    fighter.Velocity = FPVector2.Zero;
                    runtime.UniversalMovementFramesRemaining--;
                    return true;

                default:
                    FighterUniversalMovementRules.Cancel(ref runtime);
                    return false;
            }
        }

        private static int BlinkTravelFrames(in FighterAbilityModeComponent modes) =>
            modes.MovementDurationFrames > 0 ? modes.MovementDurationFrames : KitMotionRules.LightningBlinkTravelFrames;

        private static void ApplyBlinkVelocity(
            ref FighterStateComponent fighter,
            in FighterRuntimeComponent runtime,
            in FighterAbilityModeComponent modes) {
            int directionX = BlinkDirectionX(runtime.UniversalMovementDirection);
            int directionY = BlinkDirectionY(runtime.UniversalMovementDirection);
            FP64 distance = modes.MovementDistance > FP64.Zero
                ? modes.MovementDistance
                : FP64.FromDouble(KitMotionRules.LightningBlinkDistanceUnits);
            // distance over the translation frames, per second.
            FP64 speed = distance * FP64.FromInt(FighterSimulation.TickRate)
                / FP64.FromInt(BlinkTravelFrames(in modes));
            if (directionX != 0 && directionY != 0) speed *= InverseSqrtTwo;
            fighter.Velocity = new FPVector2(speed * FP64.FromInt(directionX), speed * FP64.FromInt(directionY));
            if (directionY > 0) fighter.IsGrounded = 0;
        }

        private static int LeapTravelFrames(in FighterAbilityModeComponent modes) =>
            modes.MovementDurationFrames > 0 ? modes.MovementDurationFrames : TubmanKitRules.NorthStarLeapTravelFrames;

        private static void ApplyLeapVelocity(
            ref FighterStateComponent fighter,
            in FighterRuntimeComponent runtime,
            in FighterAbilityModeComponent modes) {
            int directionX = BlinkDirectionX(runtime.UniversalMovementDirection);
            int directionY = BlinkDirectionY(runtime.UniversalMovementDirection);
            FP64 distance = modes.MovementDistance > FP64.Zero
                ? modes.MovementDistance
                : FP64.FromDouble(TubmanKitRules.NorthStarLeapDistanceUnits);
            // distance over the travel frames, per second.
            FP64 speed = distance * FP64.FromInt(FighterSimulation.TickRate)
                / FP64.FromInt(LeapTravelFrames(in modes));
            if (directionX != 0 && directionY != 0) speed *= InverseSqrtTwo;
            fighter.Velocity = new FPVector2(speed * FP64.FromInt(directionX), speed * FP64.FromInt(directionY));
            if (directionY > 0) fighter.IsGrounded = 0;
        }

        private static FP64 MoveToward(FP64 current, FP64 target, FP64 maximumDelta) {
            if (current < target) {
                FP64 next = current + maximumDelta;
                return next > target ? target : next;
            }
            if (current > target) {
                FP64 next = current - maximumDelta;
                return next < target ? target : next;
            }
            return current;
        }
    }
}
