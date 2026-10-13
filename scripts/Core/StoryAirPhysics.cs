using System;

namespace FTT.Core {

    /// <summary>
    /// Which gravity scale a Story airborne tick runs at, chosen from the
    /// velocity before the step (first match wins; see
    /// <see cref="StoryAirPhysics.SelectBand"/>).
    /// </summary>
    public enum StoryGravityBand {
        /// <summary>Rising, or at rest, under normal control: 1.0.</summary>
        Rise = 0,
        /// <summary>Moving down under normal control: <see cref="UniversalMovementRules.FallGravityMultiplier"/>.</summary>
        Fall = 1,
        /// <summary>Moving down in a launch state: <see cref="StoryAirPhysics.LaunchFallGravityMultiplier"/>.</summary>
        LaunchFall = 2,
        /// <summary>Near the apex of a jump: <see cref="UniversalMovementRules.ApexHangGravityMultiplier"/>.</summary>
        ApexHang = 3,
        /// <summary>Rising with Jump released: <see cref="StoryAirPhysics.ShortHopGravityMultiplier"/>.</summary>
        ShortHop = 4,
        /// <summary>A movement ability's float window: <see cref="StoryAirPhysics.FloatGravityMultiplier"/>.</summary>
        Float = 5,
        /// <summary>Down held while airborne: <see cref="UniversalMovementRules.FastFallGravityMultiplier"/>.</summary>
        FastFall = 6
    }

    /// <summary>
    /// The arc of one integrated Story jump, from the jump tick until the feet
    /// come back to the take-off height (or a requested landing height).
    /// </summary>
    public readonly struct StoryJumpArc {
        /// <summary>Highest point the feet reach above the take-off height, in pixels.</summary>
        public readonly float Rise;
        /// <summary>The tick (1-based, the jump tick is 1) on which that highest point is reached.</summary>
        public readonly int RiseFrames;
        /// <summary>
        /// Airborne ticks including the jump tick and the landing tick, or -1 when
        /// the arc never comes down to the landing height inside the frame cap
        /// (or never rises to it, for a landing above the take-off).
        /// </summary>
        public readonly int AirtimeFrames;

        public StoryJumpArc(float rise, int riseFrames, int airtimeFrames) {
            Rise = rise;
            RiseFrames = riseFrames;
            AirtimeFrames = airtimeFrames;
        }
    }

    /// <summary>
    /// The Story hero's air physics: the single home of Story's own movement
    /// constants, the per-tick vertical step <c>PlayerController.ApplyGravity</c>
    /// runs, and jump-arc helpers that integrate that same step in the
    /// controller's per-tick order, so level checks, the playtest bots and the
    /// content tests measure the real arc instead of a hand-copied closed form.
    /// Plain C# with no Godot types: pure-C# suites call it without an engine.
    ///
    /// <para>The cross-mode jump-arc shape lives in
    /// <see cref="UniversalMovementRules"/> (user direction 2026-10-08, ledger
    /// <c>USER-MOVEMENT-FEEL</c>; every number provisional): the apex hang, the
    /// fall multiplier, the terminal and the ramped fast-fall. Story keeps its
    /// own weight-coupled base gravity (intended for Story by the 2026-10-09
    /// ruling), converted at
    /// <see cref="PixelsPerUnit"/>.</para>
    ///
    /// <para><b>Normal control</b> (every airborne state except the launch
    /// states) picks one gravity band per tick from the velocity before the
    /// step: fast-fall, then the float window, then the short hop, then the apex
    /// hang (only once the hero has jumped this airtime — a walk-off, a
    /// drop-through or a ledge drop gets none; a ledge or rope jump-off gets
    /// it), then the fall, else 1.0. The fall is then capped at
    /// <see cref="TerminalFallSpeedPixels"/> (<see cref="FastFallSpeedPixels"/>
    /// while fast-falling); a descent already above the cap eases down by one
    /// base-gravity step per tick instead of snapping. <b>Launch states</b>
    /// (Stunned and its knockdown, Thrown, Dazed) keep the pre-2026-10-08
    /// gravity exactly: <see cref="LaunchFallGravityMultiplier"/> and a hard
    /// <see cref="LaunchTerminalFallSpeedPixels"/> clamp, so launch arcs, DI and
    /// tech timing do not move.</para>
    /// </summary>
    public static class StoryAirPhysics {
        /// <summary>Story pixels per world unit (the loadout projection's convention).</summary>
        public const float PixelsPerUnit = FTT.Combat.KitMotionRules.StoryPixelsPerUnit;

        /// <summary>
        /// Story base gravity: px/s added per 60 Hz frame before the weight
        /// coupling (<see cref="GravityPerFrame"/>), so 1,080 px/s² × the coupling.
        /// </summary>
        public const float BaseGravity = 18f;
        /// <summary>The weight coupling's constant term: gravity × (0.8 + 0.4 × Weight).</summary>
        public const float WeightCouplingBase = 0.8f;
        /// <summary>The weight coupling's per-Weight term.</summary>
        public const float WeightCouplingPerWeight = 0.4f;

        /// <summary>Gravity scale while rising with Jump released (the short hop). Story only; the sim has none.</summary>
        public const float ShortHopGravityMultiplier = 2.5f;

        /// <summary>Gravity scale while a launch state falls (the pre-2026-10-08 Story fall multiplier, kept for launches).</summary>
        public const float LaunchFallGravityMultiplier = 1.8f;
        /// <summary>The launch states' hard terminal, in px/s (10 u/s).</summary>
        public const float LaunchTerminalFallSpeedPixels = 600f;

        /// <summary>Gravity scale inside a movement ability's float window (Relativity Warp, Sonata Drift).</summary>
        public const float FloatGravityMultiplier = 0.15f;
        /// <summary>The float window's descent cap, in px/s, applied before gravity.</summary>
        public const float FloatMaxDescentPixels = 60f;

        /// <summary>Jump launch speed per unit of <c>MaxJumpForce</c>, in px/s.</summary>
        public const float JumpLaunchPixelsPerForce = 54f;
        /// <summary>Ledge (and swinging-rope) jump-off launch speed per unit of <c>MaxJumpForce</c>, in px/s.</summary>
        public const float LedgeJumpPixelsPerForce = 45f;

        /// <summary>Frames the air ramp takes to build full speed (the designed constant the sim shares, audit M-17).</summary>
        public const float AirAccelerationFrames = 4f;
        /// <summary>Frames the air ramp takes to bleed full speed. Story only.</summary>
        public const float AirDecelerationFrames = 8f;

        /// <summary>
        /// Relative allowance on every ramp step, so float rounding across the
        /// N steps can never leave a ramp one frame short of its target
        /// (MoveToward clamps at the target, so the allowance never overshoots it).
        /// </summary>
        public const float RampStepRoundingAllowance = 1.0001f;

        /// <summary><see cref="UniversalMovementRules.ApexHangSpeedBand"/> in px/s (120).</summary>
        public const float ApexHangSpeedBandPixels = UniversalMovementRules.ApexHangSpeedBand * PixelsPerUnit;
        /// <summary><see cref="UniversalMovementRules.TerminalFallSpeed"/> in px/s (720).</summary>
        public const float TerminalFallSpeedPixels = UniversalMovementRules.TerminalFallSpeed * PixelsPerUnit;
        /// <summary><see cref="UniversalMovementRules.FastFallSpeed"/> in px/s (900).</summary>
        public const float FastFallSpeedPixels = UniversalMovementRules.FastFallSpeed * PixelsPerUnit;

        /// <summary>Frame cap for the arc helpers (well past any authored arc at the lowest gravity scale).</summary>
        private const int ArcFrameCap = 3600;

        /// <summary>Gravity at multiplier 1 and environment scale 1, in px/s added per 60 Hz frame.</summary>
        public static float GravityPerFrame(float weight) =>
            BaseGravity * (WeightCouplingBase + WeightCouplingPerWeight * weight);

        /// <summary>The jump launch speed (px/s) for an effective jump force.</summary>
        public static float JumpLaunchSpeed(float jumpForce) => jumpForce * JumpLaunchPixelsPerForce;

        /// <summary>The ledge jump-off launch speed (px/s) for an effective jump force.</summary>
        public static float LedgeJumpLaunchSpeed(float jumpForce) => jumpForce * LedgeJumpPixelsPerForce;

        /// <summary>
        /// One grounded (or air) ramp step: <paramref name="maxSpeed"/> spread over
        /// <paramref name="rampFrames"/> 60 Hz frames, scaled by the tick's
        /// <paramref name="dt"/>, with <see cref="RampStepRoundingAllowance"/>.
        /// </summary>
        public static float RampStep(float maxSpeed, float rampFrames, float dt) =>
            maxSpeed / rampFrames * dt * 60f * RampStepRoundingAllowance;

        /// <summary>
        /// The gravity band for one tick, from the velocity BEFORE the step
        /// (screen space: positive is down). First match wins; see the class
        /// summary. A launch ignores fast-fall, the apex hang and the
        /// normal-control fall, exactly as before 2026-10-08.
        /// </summary>
        public static StoryGravityBand SelectBand(float vy, bool fastFalling, bool floatActive,
            bool jumpHeld, bool jumpedThisAirtime, bool launch) {
            if (launch) {
                if (floatActive) return StoryGravityBand.Float;
                if (vy > 0f) return StoryGravityBand.LaunchFall;
                if (!jumpHeld && vy < 0f) return StoryGravityBand.ShortHop;
                return StoryGravityBand.Rise;
            }
            if (fastFalling) return StoryGravityBand.FastFall;
            if (floatActive) return StoryGravityBand.Float;
            if (vy < 0f && !jumpHeld) return StoryGravityBand.ShortHop;
            if (jumpedThisAirtime && Math.Abs(vy) < ApexHangSpeedBandPixels) return StoryGravityBand.ApexHang;
            if (vy > 0f) return StoryGravityBand.Fall;
            return StoryGravityBand.Rise;
        }

        /// <summary>The gravity scale a band applies.</summary>
        public static float Multiplier(StoryGravityBand band) => band switch {
            StoryGravityBand.Fall => UniversalMovementRules.FallGravityMultiplier,
            StoryGravityBand.LaunchFall => LaunchFallGravityMultiplier,
            StoryGravityBand.ApexHang => UniversalMovementRules.ApexHangGravityMultiplier,
            StoryGravityBand.ShortHop => ShortHopGravityMultiplier,
            StoryGravityBand.Float => FloatGravityMultiplier,
            StoryGravityBand.FastFall => UniversalMovementRules.FastFallGravityMultiplier,
            _ => 1f
        };

        /// <summary>
        /// One airborne tick of vertical velocity (px/s, positive down).
        /// <paramref name="dt"/> is the tick's delta in seconds, already scaled by
        /// any time scale (an aerial Special's startup and active frames run
        /// gravity at half time). <paramref name="environmentScale"/> is the
        /// gravity-field product (<c>EnvironmentGravityScale</c>).
        /// <paramref name="jumpedThisAirtime"/> is <c>PlayerController.JumpedThisAirtime</c>:
        /// the hero has jumped since the last floor contact or ledge grab (a ledge
        /// or rope jump-off counts), which is what the sim's
        /// <c>RemainingJumps &lt; MaxJumpCount</c> reads.
        /// <paramref name="launch"/> selects the unchanged launch-state gravity.
        /// </summary>
        public static float StepVertical(float vy, float weight, float environmentScale, float dt,
            bool fastFalling, bool floatActive, bool jumpHeld, bool jumpedThisAirtime, bool launch) {
            StoryGravityBand band = SelectBand(vy, fastFalling, floatActive, jumpHeld, jumpedThisAirtime, launch);
            if (band == StoryGravityBand.Float && vy > 0f) vy = Math.Min(vy, FloatMaxDescentPixels);
            float gravityStep = GravityPerFrame(weight) * environmentScale;
            float next = vy + gravityStep * Multiplier(band) * dt * 60f;
            if (launch) return Math.Min(next, LaunchTerminalFallSpeedPixels);

            float limit = band == StoryGravityBand.FastFall ? FastFallSpeedPixels : TerminalFallSpeedPixels;
            if (next <= limit) return next;
            // Reaching the cap this tick lands on it; a descent already above it
            // (a fast-fall just released, a launch that just ended) eases down by
            // one unscaled base-gravity step per tick instead of snapping.
            if (vy <= limit) return limit;
            return Math.Max(limit, vy - gravityStep * dt * 60f);
        }

        // === Jump arcs =========================================================
        // Integrated in PlayerController's exact per-tick order: on the jump tick
        // the hero is grounded (ApplyGravity is a no-op), PerformJump sets the
        // launch speed and MoveAndSlide moves at it; every later tick runs
        // ApplyGravity first, then the jump check (an air jump overwrites the
        // velocity that tick), then MoveAndSlide moves at vy / 60. Jump is held
        // throughout (no short hop) and the hero has jumped, so the apex hang
        // applies. The best double jump presses on the first tick whose gravity
        // step would stop the rise, so no height is lost at the first apex.

        /// <summary>
        /// Integrates a held jump of <paramref name="launchSpeed"/> px/s with
        /// <paramref name="jumps"/> presses (each extra one at the apex), until
        /// the feet come back down to <paramref name="landBelowPixels"/> below the
        /// take-off (negative: above it, met on the way down).
        /// </summary>
        public static StoryJumpArc SimulateJump(float launchSpeed, float weight, float gravityScale = 1f,
            int jumps = 1, float landBelowPixels = 0f) {
            float dt = 1f / 60f;
            float vy = -launchSpeed;
            float height = launchSpeed * dt; // the jump tick moves at the full launch speed
            float peak = height;
            int peakFrame = 1;
            int airJumps = Math.Max(0, jumps - 1);
            for (int frame = 2; frame <= ArcFrameCap; frame++) {
                vy = StepVertical(vy, weight, gravityScale, dt,
                    fastFalling: false, floatActive: false, jumpHeld: true, jumpedThisAirtime: true, launch: false);
                if (airJumps > 0 && vy >= 0f) {
                    vy = -launchSpeed;
                    airJumps--;
                }
                height -= vy * dt;
                if (height > peak) {
                    peak = height;
                    peakFrame = frame;
                }
                if (vy > 0f && height <= -landBelowPixels) {
                    return new StoryJumpArc(peak, peakFrame, peak >= -landBelowPixels ? frame : -1);
                }
            }
            return new StoryJumpArc(peak, peakFrame, -1);
        }

        /// <summary>The highest a single held jump lifts the feet, in pixels.</summary>
        public static float SingleJumpRise(float launchSpeed, float weight, float gravityScale = 1f) =>
            SimulateJump(launchSpeed, weight, gravityScale, 1).Rise;

        /// <summary>The highest a double jump lifts the feet, the second press at the first apex.</summary>
        public static float DoubleJumpRise(float launchSpeed, float weight, float gravityScale = 1f) =>
            SimulateJump(launchSpeed, weight, gravityScale, 2).Rise;

        /// <summary>The best rise with <paramref name="jumps"/> presses (1 = single, 2 = double, ...).</summary>
        public static float MultiJumpRise(float launchSpeed, float weight, int jumps, float gravityScale = 1f) =>
            SimulateJump(launchSpeed, weight, gravityScale, Math.Max(1, jumps)).Rise;

        /// <summary>
        /// Airborne ticks from the jump tick to the landing tick, landing
        /// <paramref name="landBelowPixels"/> below the take-off; -1 when the arc
        /// never meets that height.
        /// </summary>
        public static int AirtimeFrames(float launchSpeed, float weight, int jumps = 1, float gravityScale = 1f,
            float landBelowPixels = 0f) =>
            SimulateJump(launchSpeed, weight, gravityScale, Math.Max(1, jumps), landBelowPixels).AirtimeFrames;

        /// <summary>
        /// Horizontal distance a jump covers back to its take-off height while the
        /// hero holds <paramref name="runSpeedPixels"/> the whole way (every
        /// airborne tick moves vx / 60, the landing tick included).
        /// </summary>
        public static float JumpDistance(float launchSpeed, float weight, float runSpeedPixels, int jumps = 1,
            float gravityScale = 1f) {
            int frames = AirtimeFrames(launchSpeed, weight, jumps, gravityScale);
            return frames < 0 ? 0f : frames * runSpeedPixels / 60f;
        }
    }
}
