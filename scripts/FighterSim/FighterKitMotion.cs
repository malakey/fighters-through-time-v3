using FTT.Combat;
using xpTURN.Klotho.Deterministic.Math;

namespace FTT.FighterSim {

    /// <summary>
    /// Package 12 W4 — the deterministic half of the two kit moves that
    /// translate their caster over several frames: Tesla's Lightning Blink
    /// (6 startup / 12 translation / 10 recovery, projectile pass-through
    /// during the translation only) and Pocahontas's Spirit Strike (12 startup,
    /// then a 15-frame 45° carry while the eagle dives).
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
    /// Story) or from the normalized loadout (<c>MovementDistance</c>,
    /// <c>MovementDurationFrames</c>). Nothing here reads a Story modifier:
    /// Long Blink never reaches the sim.</para>
    /// </summary>
    public static partial class FighterKitMotion {
        public const int FirstPhase = 16;
        public const int BlinkStartup = 16;
        public const int BlinkTravel = 17;
        public const int BlinkRecovery = 18;
        public const int SpiritStartup = 19;
        public const int SpiritCarry = 20;
        private const int LastPhase = 20;
        /// <summary>
        /// Package 13 W7a — Einstein's Relativity Warp fold startup (E04) and
        /// Shakespeare's Prospero gust (A08). Codes 30–31, deliberately clear of
        /// the Package 12 block (16–20) and of anything W7b appends above it.
        /// </summary>
        public const int WarpFoldStartup = 30;
        public const int GustBurst = 31;

        private static readonly FP64 FixedDelta = FP64.One / FP64.FromInt(FighterSimulation.TickRate);
        private static readonly FP64 InverseSqrtTwo = FP64.FromDouble(0.70710678118654752);
        /// <summary>Per-axis carry per frame (units): (3 / √2) / 15.</summary>
        public static readonly FP64 SpiritAxisStep = FP64.FromDouble(KitMotionRules.SpiritStrikeAxisStepUnits);
        private static readonly FP64 SpiritAxisSpeed = SpiritAxisStep * FP64.FromInt(FighterSimulation.TickRate);
        private static readonly FP64 EagleStartForward = FP64.FromDouble(KitMotionRules.SpiritEagleStartForwardUnits);
        private static readonly FP64 EagleStartUp = FP64.FromDouble(KitMotionRules.SpiritEagleStartUpUnits);
        public static readonly FPVector2 EagleHalfExtents = new(
            FP64.FromDouble(KitMotionRules.SpiritEagleHalfExtentUnits),
            FP64.FromDouble(KitMotionRules.SpiritEagleHalfExtentUnits));
        /// <summary>The sim's fixed Special hitstun (see <c>DEFER-SIM-ABILITY-HITSTUN</c>).</summary>
        public const int SpiritStrikeHitstunFrames = 18;

        public static bool IsKitPhase(int state) =>
            (state >= FirstPhase && state <= LastPhase) || state is WarpFoldStartup or GustBurst
            // Package 13 W7b: Joan / Cleopatra / Lincoln / Mozart phases 24–29
            // (FighterKitMotion.Reach.cs).
            || IsReachPhase(state);

        /// <summary>
        /// The Lightning Blink pass-through window: the translation, and only
        /// the translation. <c>FighterProjectileSystem</c> reads this after the
        /// movement system has advanced the phase for the tick.
        /// </summary>
        public static bool PassesThroughProjectiles(in FighterRuntimeComponent runtime) =>
            runtime.UniversalMovementState == BlinkTravel;

        /// <summary>Startup hovers and the translation/carry fly straight: no gravity.</summary>
        public static bool SuspendsGravity(in FighterRuntimeComponent runtime) =>
            runtime.UniversalMovementState is BlinkStartup or BlinkTravel or SpiritCarry
                or WarpFoldStartup or GustBurst
            || ReachSuspendsGravity(in runtime);

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

        /// <summary>Starts Spirit Strike; the direction int is ±1 (facing), ±2 once the eagle has struck.</summary>
        public static void StartSpiritStrike(ref FighterRuntimeComponent runtime, int facing) {
            runtime.UniversalMovementState = SpiritStartup;
            runtime.UniversalMovementFramesRemaining = KitMotionRules.SpiritStrikeStartupFrames;
            runtime.UniversalMovementDirection = facing >= 0 ? 1 : -1;
        }

        /// <summary>
        /// E04: starts the Relativity Warp fold — the destination direction is
        /// latched from the held stick now (packed like the blink), the ghost
        /// shows through the 10-frame startup, and the relocation happens when it
        /// ends. No invulnerability; a hit cancels it through the ordinary
        /// universal-movement cancel sites (the cooldown is already spent).
        /// </summary>
        public static void StartWarpFold(ref FighterRuntimeComponent runtime, int facing) {
            int directionX = runtime.MoveX > 30 ? 1 : runtime.MoveX < -30 ? -1 : 0;
            int directionY = runtime.MoveY < -30 ? 1 : runtime.MoveY > 30 ? -1 : 0;
            if (directionX == 0 && directionY == 0) directionX = facing;
            runtime.UniversalMovementState = WarpFoldStartup;
            runtime.UniversalMovementFramesRemaining = KitMotionRules.RelativityWarpStartupFrames;
            runtime.UniversalMovementDirection = (directionX + 1) + 3 * (directionY + 1);
        }

        /// <summary>A08: starts Prospero's gust burst along facing (direction int ±1).</summary>
        public static void StartGust(ref FighterRuntimeComponent runtime, int facing) {
            runtime.UniversalMovementState = GustBurst;
            runtime.UniversalMovementFramesRemaining = KitMotionRules.ProsperoGustFrames;
            runtime.UniversalMovementDirection = facing >= 0 ? 1 : -1;
        }

        private static readonly FP64 DefaultWarpDistance = FP64.FromDouble(KitMotionRules.RelativityWarpDistanceUnits);
        private static readonly FP64 DefaultGustForward = FP64.FromDouble(KitMotionRules.ProsperoGustForwardUnits);
        private static readonly FP64 GustRiseSpeed = FP64.FromDouble(
            KitMotionRules.ProsperoGustRiseUnits * FighterSimulation.TickRate / KitMotionRules.ProsperoGustFrames);

        /// <summary>
        /// E04 fold destination for a fighter at <paramref name="position"/>: the
        /// held direction's full distance, shortened to the farthest point with
        /// full-body clearance inside the stage. Pure; the movement system and
        /// the presentation ghost read the same answer.
        /// </summary>
        public static FPVector2 WarpFoldDestination(
            FighterStageGeometry geometry, in FPVector2 position, int packedDirection, FP64 distance) {
            FP64 resolved = distance > FP64.Zero ? distance : DefaultWarpDistance;
            FPVector2 offset = FighterKitGeometry.ClampedDisplacement(
                geometry, in position, BlinkDirectionX(packedDirection), BlinkDirectionY(packedDirection), resolved);
            FPVector2 destination = position + offset;
            // The shortened ratio is exact to one raw step; pin the result inside
            // the box so a diagonal fold never lands a hair past a wall.
            FP64 left = geometry?.LeftWall ?? FP64.FromInt(-10);
            FP64 right = geometry?.RightWall ?? FP64.FromInt(10);
            FP64 ceiling = geometry?.Ceiling ?? FP64.FromInt(9);
            FP64 floor = position.y < FP64.Zero ? position.y : FP64.Zero;
            return new FPVector2(
                FP64.Clamp(destination.x, left, right),
                FP64.Clamp(destination.y, floor, ceiling));
        }

        public static int BlinkDirectionX(int packed) => packed % 3 - 1;
        public static int BlinkDirectionY(int packed) => packed / 3 - 1;
        public static int SpiritFacing(int packed) => packed >= 0 ? 1 : -1;
        public static bool SpiritEagleStruck(int packed) => packed == 2 || packed == -2;
        public static void MarkSpiritEagleStruck(ref FighterRuntimeComponent runtime) =>
            runtime.UniversalMovementDirection = SpiritFacing(runtime.UniversalMovementDirection) * 2;

        /// <summary>0-based carry frame for the tick the movement system just advanced.</summary>
        public static int SpiritCarryFrameIndex(in FighterRuntimeComponent runtime) =>
            KitMotionRules.SpiritStrikeCarryFrames - runtime.UniversalMovementFramesRemaining - 1;

        /// <summary>The eagle's hitbox centre for a carry frame, relative to the caster's position.</summary>
        public static FPVector2 EagleCenter(in FPVector2 casterPosition, int facing, int carryFrame) {
            FP64 up = EagleStartUp - FP64.FromInt(2) * SpiritAxisStep * FP64.FromInt(carryFrame < 0 ? 0 : carryFrame);
            return casterPosition + new FPVector2(EagleStartForward * FP64.FromInt(facing), up);
        }

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
            FP64 decelStep,
            FighterStageGeometry geometry = null) {
            if (IsReachPhase(runtime.UniversalMovementState)) {
                return ProcessReach(ref fighter, ref runtime, in modes);
            }
            switch (runtime.UniversalMovementState) {
                case WarpFoldStartup:
                    if (runtime.UniversalMovementFramesRemaining <= 0) {
                        // Instant relocation: no travel frames, so gaps, platforms,
                        // hazards and attacks in between are simply crossed.
                        FPVector2 destination = WarpFoldDestination(
                            geometry, in fighter.Position, runtime.UniversalMovementDirection, modes.MovementDistance);
                        if (destination.y > fighter.Position.y) fighter.IsGrounded = 0;
                        fighter.Position = destination;
                        fighter.Velocity = FPVector2.Zero;
                        runtime.FloatFrames = KitMotionRules.RelativityWarpFloatFrames;
                        FighterUniversalMovementRules.Cancel(ref runtime);
                        return false;
                    }
                    // The visible ghost's startup: Einstein holds in place.
                    fighter.Velocity = FPVector2.Zero;
                    runtime.UniversalMovementFramesRemaining--;
                    return true;

                case GustBurst:
                    if (runtime.UniversalMovementFramesRemaining <= 0) {
                        // A burst, not a glide: it ends with its momentum spent
                        // and the fall is the ordinary one.
                        fighter.Velocity = FPVector2.Zero;
                        FighterUniversalMovementRules.Cancel(ref runtime);
                        return false;
                    }
                    {
                        FP64 forward = modes.MovementDistance > FP64.Zero ? modes.MovementDistance : DefaultGustForward;
                        FP64 forwardSpeed = forward * FP64.FromInt(FighterSimulation.TickRate)
                            / FP64.FromInt(KitMotionRules.ProsperoGustFrames);
                        fighter.IsGrounded = 0;
                        fighter.Velocity = new FPVector2(
                            forwardSpeed * FP64.FromInt(SpiritFacing(runtime.UniversalMovementDirection)),
                            GustRiseSpeed);
                    }
                    runtime.UniversalMovementFramesRemaining--;
                    return true;

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

                case SpiritStartup:
                    if (runtime.UniversalMovementFramesRemaining <= 0) {
                        runtime.UniversalMovementState = SpiritCarry;
                        runtime.UniversalMovementFramesRemaining = KitMotionRules.SpiritStrikeCarryFrames;
                        goto case SpiritCarry;
                    }
                    fighter.Velocity.x = MoveToward(fighter.Velocity.x, FP64.Zero, decelStep);
                    runtime.UniversalMovementFramesRemaining--;
                    return true;

                case SpiritCarry:
                    if (runtime.UniversalMovementFramesRemaining <= 0) {
                        // The carry is a forced dash: it ends with its momentum spent.
                        fighter.Velocity = FPVector2.Zero;
                        FighterUniversalMovementRules.Cancel(ref runtime);
                        return false;
                    }
                    fighter.IsGrounded = 0;
                    fighter.Velocity = new FPVector2(
                        SpiritAxisSpeed * FP64.FromInt(SpiritFacing(runtime.UniversalMovementDirection)),
                        SpiritAxisSpeed);
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
