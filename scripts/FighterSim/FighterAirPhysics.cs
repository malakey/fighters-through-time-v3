using FTT.Core;
using xpTURN.Klotho.Deterministic.Math;

namespace FTT.FighterSim {

    /// <summary>
    /// The Fighter sim's vertical air step and its grounded ramp step, as pure
    /// fixed-point functions (user direction 2026-10-08, ledger
    /// <c>USER-MOVEMENT-FEEL</c>). <see cref="FighterMovementSystem"/> calls
    /// them for every airborne tick, and every input-side consumer that has to
    /// predict an arc — the CPU's pit-risk probe, <see cref="CpuRecoveryProfile"/>'s
    /// jump and lift estimates, the recovery test harnesses — integrates with
    /// the same calls instead of keeping a second copy of the physics.
    ///
    /// <para><b>Two variants.</b> <see cref="StepMovement"/> is the air model
    /// under normal control (jumps, walk-offs, drops, aerial swings, the air
    /// dodge, the kit phases that keep gravity): one gravity multiplier per tick
    /// chosen from the velocity before the step — fast-fall, then the float
    /// glide, then the apex hang (only after a jump this airtime), then the fall
    /// multiplier — and a fall cap that a faster fall eases down to by one
    /// base-gravity step per tick instead of snapping. <see cref="StepLaunch"/> is
    /// hitstun and daze: the sim's flat −30 u/s² with the
    /// <see cref="UniversalMovementRules.LaunchTerminalFallSpeed"/> clamp, exactly
    /// as before the feel pass, so launch arcs, DI and tech timing do not move.</para>
    ///
    /// <para>Every number is a <see cref="UniversalMovementRules"/> constant
    /// (shared with Story) converted once at type initialization; the base
    /// gravity stays the sim's own flat −30 (Story's is weight-coupled; each is
    /// intended for its mode by the 2026-10-09 ruling).
    /// Nothing here runs float math at tick time and nothing here is state:
    /// fast-fall, the float window and the jumped-this-airtime test are all
    /// derived by the caller from fields the snapshot already carries.</para>
    /// </summary>
    public static class FighterAirPhysics {
        /// <summary>One simulation tick, in seconds.</summary>
        public static readonly FP64 FixedDelta = FP64.One / FP64.FromInt(60);

        /// <summary>The sim's flat base gravity, Y up: intended for Fighter Mode, apart from Story's weight-coupled base (2026-10-09 ruling).</summary>
        public static readonly FP64 Gravity = FP64.FromInt(-30);

        /// <summary>One base-gravity tick: the launch step, and the ease step past a fall cap.</summary>
        private static readonly FP64 GravityStep = Gravity * FixedDelta;

        /// <summary>The float glide (a glide, the post-warp window): one fifth of gravity, unchanged.</summary>
        private static readonly FP64 FloatGravityStep = GravityStep / FP64.FromInt(5);

        private static readonly FP64 ApexHangGravityStep =
            GravityStep * FP64.FromDouble(UniversalMovementRules.ApexHangGravityMultiplier);

        private static readonly FP64 FallGravityStep =
            GravityStep * FP64.FromDouble(UniversalMovementRules.FallGravityMultiplier);

        private static readonly FP64 FastFallGravityStep =
            GravityStep * FP64.FromDouble(UniversalMovementRules.FastFallGravityMultiplier);

        private static readonly FP64 ApexHangBand = FP64.FromDouble(UniversalMovementRules.ApexHangSpeedBand);

        /// <summary>Signed normal-control fall cap, −<see cref="UniversalMovementRules.TerminalFallSpeed"/>.</summary>
        public static readonly FP64 TerminalFallVelocity = -FP64.FromDouble(UniversalMovementRules.TerminalFallSpeed);

        /// <summary>Signed fast-fall cap, −<see cref="UniversalMovementRules.FastFallSpeed"/>.</summary>
        public static readonly FP64 FastFallVelocity = -FP64.FromDouble(UniversalMovementRules.FastFallSpeed);

        /// <summary>Signed hitstun/daze clamp, −<see cref="UniversalMovementRules.LaunchTerminalFallSpeed"/>.</summary>
        public static readonly FP64 LaunchTerminalFallVelocity =
            -FP64.FromDouble(UniversalMovementRules.LaunchTerminalFallSpeed);

        /// <summary>
        /// The apex hang's discriminator, identical in both modes: a fighter that
        /// has spent a jump this airtime. Walk-offs, drop-throughs, ledge drops
        /// and launches from the ground have not, and get no hang.
        /// </summary>
        public static bool HasJumpedThisAirtime(int remainingJumps, int maxJumpCount) =>
            remainingJumps < maxJumpCount;

        /// <summary>
        /// One airborne tick under normal control. Returns the new Y-up vertical
        /// velocity. <paramref name="fastFalling"/> is Down held (the caller has
        /// already zeroed the float window for it); <paramref name="floating"/> is
        /// a live float window.
        /// </summary>
        public static FP64 StepMovement(
            FP64 velocityY, bool fastFalling, bool floating, bool jumpedThisAirtime) {
            FP64 step;
            if (fastFalling) {
                step = FastFallGravityStep;
            } else if (floating) {
                step = FloatGravityStep;
            } else if (jumpedThisAirtime && FP64.Abs(velocityY) < ApexHangBand) {
                step = ApexHangGravityStep;
            } else if (velocityY < FP64.Zero) {
                step = FallGravityStep;
            } else {
                step = GravityStep;
            }
            FP64 next = velocityY + step;
            FP64 limit = fastFalling ? FastFallVelocity : TerminalFallVelocity;
            if (next >= limit) return next;
            // Past the cap. A fall that reached it on this tick stops on it; one
            // that was already faster (a fast-fall just released, a launch that
            // just ended) eases toward it by one base-gravity step per tick.
            if (velocityY >= limit) return limit;
            FP64 eased = velocityY - GravityStep;
            return eased < limit ? eased : limit;
        }

        /// <summary>
        /// One airborne tick in hitstun or daze: flat base gravity, clamped at
        /// the launch terminal. Unchanged by the 2026-10-08 feel pass.
        /// </summary>
        public static FP64 StepLaunch(FP64 velocityY) {
            FP64 next = velocityY + GravityStep;
            return next < LaunchTerminalFallVelocity ? LaunchTerminalFallVelocity : next;
        }

        /// <summary>
        /// The per-tick step that covers <paramref name="span"/> in exactly
        /// <paramref name="frames"/> <c>MoveToward</c> steps: the span divided by
        /// the frame count, rounded <b>up</b> to the next raw unit when the
        /// division is inexact. Plain truncation (7 / 6 at the default move
        /// speed) left the ramp one raw unit short after its last frame, a
        /// frame late; rounding up makes the grounded run reach full speed on
        /// exactly <see cref="UniversalMovementRules.RunAccelerationFrames"/> and
        /// stop on exactly <see cref="UniversalMovementRules.RunDecelerationFrames"/>
        /// for every move speed. Integer arithmetic on the raw value only.
        /// </summary>
        public static FP64 RampStep(FP64 span, int frames) {
            long raw = span.RawValue < 0 ? -span.RawValue : span.RawValue;
            if (frames <= 1) return FP64.FromRaw(raw);
            return FP64.FromRaw((raw + frames - 1) / frames);
        }
    }
}
