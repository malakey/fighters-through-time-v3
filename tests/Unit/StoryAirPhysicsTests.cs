using System;
using FTT.Core;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// The Story air-physics rulebook (user direction 2026-10-08, ledger
/// <c>USER-MOVEMENT-FEEL</c>): the gravity band order, the per-tick step with its
/// caps and the eased descent, the unchanged launch gravity, the ramp step that
/// cannot fall a frame short, and the integrated jump-arc helpers the bots and
/// the content tests read. Pure C#: nothing here touches the Godot runtime.
/// </summary>
[TestSuite]
public class StoryAirPhysicsTests {

    private const float Dt = 1f / 60f;

    [TestCase]
    public void TheSharedArcNumbersConvertAtSixtyPixelsPerUnit() {
        AssertThat(StoryAirPhysics.ApexHangSpeedBandPixels).IsEqual(120f);
        AssertThat(StoryAirPhysics.TerminalFallSpeedPixels).IsEqual(720f);
        AssertThat(StoryAirPhysics.FastFallSpeedPixels).IsEqual(900f);
        AssertThat(StoryAirPhysics.Multiplier(StoryGravityBand.ApexHang)).IsEqual(0.5f);
        AssertThat(StoryAirPhysics.Multiplier(StoryGravityBand.Fall)).IsEqual(2.2f);
        AssertThat(StoryAirPhysics.Multiplier(StoryGravityBand.FastFall)).IsEqual(3f);
        // The launch states keep the pre-2026-10-08 Story numbers.
        AssertThat(StoryAirPhysics.Multiplier(StoryGravityBand.LaunchFall)).IsEqual(1.8f);
        AssertThat(StoryAirPhysics.LaunchTerminalFallSpeedPixels).IsEqual(600f);
        AssertThat(StoryAirPhysics.GravityPerFrame(1f)).IsEqualApprox(21.6f, 0.0001f);
    }

    [TestCase]
    public void TheFirstMatchingBandWinsInTheDesignedOrder() {
        // Fast-fall beats the float window, the short hop and the hang.
        AssertThat(Band(-50f, fastFalling: true, floatActive: true, jumpHeld: false, jumped: true))
            .IsEqual(StoryGravityBand.FastFall);
        AssertThat(Band(-50f, floatActive: true, jumpHeld: false, jumped: true)).IsEqual(StoryGravityBand.Float);
        // A released rise is the short hop, even inside the hang band.
        AssertThat(Band(-50f, jumpHeld: false, jumped: true)).IsEqual(StoryGravityBand.ShortHop);
        // Inside the band, either side of the apex, a jump hangs...
        AssertThat(Band(-119f, jumped: true)).IsEqual(StoryGravityBand.ApexHang);
        AssertThat(Band(119f, jumped: true)).IsEqual(StoryGravityBand.ApexHang);
        AssertThat(Band(0f, jumped: true)).IsEqual(StoryGravityBand.ApexHang);
        // ... a walk-off or a ledge drop does not.
        AssertThat(Band(60f, jumped: false)).IsEqual(StoryGravityBand.Fall);
        AssertThat(Band(0f, jumped: false)).IsEqual(StoryGravityBand.Rise);
        // Outside the band.
        AssertThat(Band(120f, jumped: true)).IsEqual(StoryGravityBand.Fall);
        AssertThat(Band(-120f, jumped: true)).IsEqual(StoryGravityBand.Rise);
    }

    [TestCase]
    public void ALaunchIgnoresFastFallTheHangAndTheNormalFall() {
        AssertThat(Band(200f, fastFalling: true, jumped: true, launch: true)).IsEqual(StoryGravityBand.LaunchFall);
        AssertThat(Band(50f, jumped: true, launch: true)).IsEqual(StoryGravityBand.LaunchFall);
        AssertThat(Band(-50f, jumped: true, launch: true)).IsEqual(StoryGravityBand.Rise);
        AssertThat(Band(-50f, jumpHeld: false, launch: true)).IsEqual(StoryGravityBand.ShortHop);
        AssertThat(Band(50f, floatActive: true, launch: true)).IsEqual(StoryGravityBand.Float);
    }

    /// <summary>
    /// The launch variant is the old ApplyGravity, number for number: the float
    /// window (0.15, descent capped at 60 first), the 1.8 fall, the 2.5 short hop
    /// on a released rise, and a hard 600 px/s clamp.
    /// </summary>
    [TestCase]
    public void TheLaunchStepIsThePreChangeGravityExactly() {
        foreach (float weight in new[] { 0.7f, 1f, 1.6f }) {
            foreach (float vy in new[] { -700f, -300f, -10f, 0f, 10f, 200f, 590f, 600f, 900f }) {
                foreach (bool held in new[] { true, false }) {
                    foreach (bool floating in new[] { true, false }) {
                        float expected = OldLaunchGravity(vy, weight, held, floating);
                        float actual = StoryAirPhysics.StepVertical(vy, weight, 1f, Dt,
                            fastFalling: true, floatActive: floating, jumpHeld: held, jumpedThisAirtime: true, launch: true);
                        AssertThat(actual)
                            .OverrideFailureMessage($"Launch step at vy {vy}, weight {weight}, held {held}, float {floating}.")
                            .IsEqualApprox(expected, 0.0001f);
                    }
                }
            }
        }
    }

    [TestCase]
    public void AFallReachingTheTerminalLandsOnItAndAFasterOneEasesDown() {
        float g = StoryAirPhysics.GravityPerFrame(1f);
        // Below the cap: the 2.2 fall.
        AssertThat(Step(300f)).IsEqualApprox(300f + g * 2.2f, 0.0001f);
        // Crossing the cap this tick: exactly the cap.
        AssertThat(Step(710f)).IsEqual(720f);
        AssertThat(Step(720f)).IsEqual(720f);
        // Already above it (a fast-fall just released): one unscaled base step.
        AssertThat(Step(900f)).IsEqualApprox(900f - g, 0.0001f);
        AssertThat(Step(725f)).IsEqual(720f);
        // Fast-falling: 3x gravity toward the 900 cap, with the same rules.
        AssertThat(Step(-300f, fastFalling: true)).IsEqualApprox(-300f + g * 3f, 0.0001f);
        AssertThat(Step(880f, fastFalling: true)).IsEqual(900f);
        AssertThat(Step(900f, fastFalling: true)).IsEqual(900f);
        // A launch that just ended above even the fast-fall cap eases toward it.
        AssertThat(Step(1000f, fastFalling: true)).IsEqualApprox(1000f - g, 0.0001f);
    }

    [TestCase]
    public void AHalvedTimeScaleHalvesEveryGravityStep() {
        float g = StoryAirPhysics.GravityPerFrame(1f);
        float halved = StoryAirPhysics.StepVertical(300f, 1f, 1f, Dt * 0.5f,
            fastFalling: false, floatActive: false, jumpHeld: true, jumpedThisAirtime: true, launch: false);
        AssertThat(halved).IsEqualApprox(300f + g * 2.2f * 0.5f, 0.001f);
        float lowGravity = StoryAirPhysics.StepVertical(300f, 1f, 0.36f, Dt,
            fastFalling: false, floatActive: false, jumpHeld: true, jumpedThisAirtime: true, launch: false);
        AssertThat(lowGravity).IsEqualApprox(300f + g * 0.36f * 2.2f, 0.001f);
    }

    /// <summary>
    /// Every ramp reaches its target on exactly its Nth step, whatever the speed:
    /// the rounding allowance covers float error across the N additions, and
    /// MoveToward clamps at the target so it never overshoots.
    /// </summary>
    [TestCase]
    public void ARampReachesItsTargetOnExactlyItsLastFrame() {
        int[] ramps = { UniversalMovementRules.RunAccelerationFrames, UniversalMovementRules.RunDecelerationFrames };
        AssertThat(ramps[0]).IsEqual(6);
        AssertThat(ramps[1]).IsEqual(4);
        foreach (int frames in ramps) {
            for (int sample = 0; sample < 400; sample++) {
                // Run speeds from slowed (status 0.5x) to boosted (perks) heroes.
                float maxSpeed = 120f + sample * 1.37f + sample * 0.0137f;
                float step = StoryAirPhysics.RampStep(maxSpeed, frames, Dt);
                float v = 0f;
                for (int frame = 1; frame <= frames; frame++) {
                    v = MoveToward(v, maxSpeed, step);
                    if (frame < frames && v >= maxSpeed) {
                        AssertThat(false).OverrideFailureMessage($"{maxSpeed} px/s arrived early on frame {frame} of {frames}.").IsTrue();
                    }
                }
                AssertThat(v).OverrideFailureMessage($"{maxSpeed} px/s fell short of a {frames}-frame ramp.").IsEqual(maxSpeed);
            }
        }
    }

    [TestCase]
    public void TheApexHangLiftsAJumpAndADoubleJumpIsTwoSingles() {
        foreach (float weight in new[] { 0.7f, 1f, 1.6f }) {
            float launch = StoryAirPhysics.JumpLaunchSpeed(12f);
            float single = StoryAirPhysics.SingleJumpRise(launch, weight);
            float g = StoryAirPhysics.GravityPerFrame(weight) * 60f;
            // Against the SAME discrete integration with the hang switched off (a
            // closed form v^2 / 2a is beaten by the discrete launch tick alone, so
            // it could not tell a missing hang). Continuously the hang adds
            // band^2 / 2a; the discrete lift is 3.5-4.6 px against the 2.3-3.1 px
            // half of that asserted here, and zero without the hang.
            float noHang = NoHangDiscreteRise(launch, weight);
            float band = StoryAirPhysics.ApexHangSpeedBandPixels;
            AssertThat(single - noHang)
                .OverrideFailureMessage($"weight {weight}: the hang lifted the jump only {single - noHang} px.")
                .IsGreater(0.5f * band * band / (2f * g));
            float twice = StoryAirPhysics.DoubleJumpRise(launch, weight);
            AssertThat(twice).IsBetween(2f * single - 1f, 2f * single + 1f);
            AssertThat(StoryAirPhysics.MultiJumpRise(launch, weight, 1)).IsEqual(single);
            AssertThat(StoryAirPhysics.MultiJumpRise(launch, weight, 2)).IsEqual(twice);
        }
    }

    [TestCase]
    public void TheArcHelpersReportAirtimeDistanceAndUnreachableLandings() {
        float launch = StoryAirPhysics.JumpLaunchSpeed(12.5f);
        StoryJumpArc arc = StoryAirPhysics.SimulateJump(launch, 1f);
        AssertThat(arc.RiseFrames).IsGreater(0);
        AssertThat(arc.AirtimeFrames).IsGreater(arc.RiseFrames);
        AssertThat(StoryAirPhysics.AirtimeFrames(launch, 1f)).IsEqual(arc.AirtimeFrames);
        AssertThat(StoryAirPhysics.JumpDistance(launch, 1f, 405f)).IsEqualApprox(arc.AirtimeFrames * 405f / 60f, 0.001f);
        // A deeper landing takes longer; a landing above the apex is never met.
        AssertThat(StoryAirPhysics.AirtimeFrames(launch, 1f, landBelowPixels: 300f)).IsGreater(arc.AirtimeFrames);
        AssertThat(StoryAirPhysics.AirtimeFrames(launch, 1f, landBelowPixels: -(arc.Rise + 1f))).IsEqual(-1);
        // A double jump stays up longer and lands later.
        AssertThat(StoryAirPhysics.AirtimeFrames(launch, 1f, jumps: 2)).IsGreater(arc.AirtimeFrames);
        // Low gravity lifts the same jump far higher.
        AssertThat(StoryAirPhysics.SingleJumpRise(launch, 1f, 0.36f)).IsGreater(arc.Rise * 2.5f);
    }

    /// <summary>
    /// CM-MOVE (decision 6.1, 2026-10-16; <c>USER-APEX-HANG-HELD</c>): the apex
    /// hang applies only while Jump is held. A released jump inside the band
    /// falls at the heavier fall gravity past the apex and takes base gravity at
    /// it; a held one still hangs on both sides.
    /// </summary>
    [TestCase]
    public void TheApexHangNeedsJumpHeld() {
        AssertThat(Band(60f, jumpHeld: false, jumped: true)).IsEqual(StoryGravityBand.Fall);
        AssertThat(Band(119f, jumpHeld: false, jumped: true)).IsEqual(StoryGravityBand.Fall);
        // At the apex itself (vy 0) a released jump takes base gravity, not the hang.
        AssertThat(Band(0f, jumpHeld: false, jumped: true)).IsEqual(StoryGravityBand.Rise);
        // A released rise inside the band is the short hop, never the hang.
        AssertThat(Band(-60f, jumpHeld: false, jumped: true)).IsEqual(StoryGravityBand.ShortHop);
        // Held, the hang is unchanged either side of the apex.
        AssertThat(Band(60f, jumpHeld: true, jumped: true)).IsEqual(StoryGravityBand.ApexHang);
        AssertThat(Band(-60f, jumpHeld: true, jumped: true)).IsEqual(StoryGravityBand.ApexHang);
        AssertThat(Band(0f, jumpHeld: true, jumped: true)).IsEqual(StoryGravityBand.ApexHang);
    }

    /// <summary>
    /// CM-MOVE (the Q5 ruling, "own jump only, both modes";
    /// <c>USER-SHORT-HOP-OWN-JUMP-ONLY</c>): the ×2.5 band cuts only a rise that
    /// began with the hero's own Jump press this airtime. A released rise that
    /// is not one — a post-hitstun rise, a kit lift — takes base gravity, and
    /// the step equals a held-jump step's rise exactly; the launch branch's own
    /// short-hop clause (the launch half of <c>DEFER-H7-STORY-TERMINAL</c>) is
    /// untouched.
    /// </summary>
    [TestCase]
    public void TheShortHopCutsOnlyTheHerosOwnJump() {
        AssertThat(Band(-50f, jumpHeld: false, jumped: true, own: true)).IsEqual(StoryGravityBand.ShortHop);
        AssertThat(Band(-500f, jumpHeld: false, jumped: false, own: true)).IsEqual(StoryGravityBand.ShortHop);
        AssertThat(Band(-500f, jumpHeld: false, jumped: true, own: false)).IsEqual(StoryGravityBand.Rise);
        AssertThat(Band(-50f, jumpHeld: false, jumped: true, own: false)).IsEqual(StoryGravityBand.Rise);
        AssertThat(Band(-50f, jumpHeld: false, jumped: false, own: false)).IsEqual(StoryGravityBand.Rise);
        // Fast-fall and the float still win over everything.
        AssertThat(Band(-50f, fastFalling: true, jumpHeld: false, own: false)).IsEqual(StoryGravityBand.FastFall);
        AssertThat(Band(-50f, floatActive: true, jumpHeld: false, own: true)).IsEqual(StoryGravityBand.Float);
        // The launch branch is unchanged by the gate.
        AssertThat(Band(-50f, jumpHeld: false, launch: true, own: false)).IsEqual(StoryGravityBand.ShortHop);
        AssertThat(Band(-50f, jumpHeld: false, launch: true, own: true)).IsEqual(StoryGravityBand.ShortHop);

        foreach (float weight in new[] { 0.7f, 1f, 1.6f }) {
            float g = StoryAirPhysics.GravityPerFrame(weight);
            float cut = StoryAirPhysics.StepVertical(-500f, weight, 1f, Dt, false, false,
                jumpHeld: false, jumpedThisAirtime: true, launch: false, riseFromOwnJump: true);
            float uncut = StoryAirPhysics.StepVertical(-500f, weight, 1f, Dt, false, false,
                jumpHeld: false, jumpedThisAirtime: true, launch: false, riseFromOwnJump: false);
            float held = StoryAirPhysics.StepVertical(-500f, weight, 1f, Dt, false, false,
                jumpHeld: true, jumpedThisAirtime: true, launch: false, riseFromOwnJump: true);
            AssertThat(cut).IsEqualApprox(-500f + g * StoryAirPhysics.ShortHopGravityMultiplier, 0.0001f);
            AssertThat(uncut).IsEqualApprox(-500f + g, 0.0001f);
            AssertThat(uncut).IsEqualApprox(held, 0.0001f);
            // The omitted parameter keeps the pre-2026-10-16 short hop.
            AssertThat(StoryAirPhysics.StepVertical(-500f, weight, 1f, Dt, false, false,
                jumpHeld: false, jumpedThisAirtime: true, launch: false)).IsEqualApprox(cut, 0.0001f);
        }
        // One value in both modes: the Story constant forwards to the shared one.
        AssertThat(StoryAirPhysics.ShortHopGravityMultiplier).IsEqual(UniversalMovementRules.ShortHopGravityMultiplier);
        AssertThat(StoryAirPhysics.ShortHopGravityMultiplier).IsEqual(2.5f);
    }

    /// <summary>
    /// Decisions 5.1 and 6.1 together, integrated: a jump released on its second
    /// tick spends no tick in the hang (it drops through its peak at the fall
    /// gravity) and peaks lower than the held arc, which keeps its full hang.
    /// </summary>
    [TestCase]
    public void AReleasedHopDropsThroughItsPeakWithNoHangAndAHeldJumpKeepsIt() {
        foreach (float weight in new[] { 0.7f, 1f, 1.6f }) {
            float launch = StoryAirPhysics.JumpLaunchSpeed(12f);
            (float releasedPeak, int releasedHang) = TapArc(launch, weight, heldTicks: 2);
            (float heldPeak, int heldHang) = TapArc(launch, weight, heldTicks: int.MaxValue);
            AssertThat(releasedHang).OverrideFailureMessage($"weight {weight}: a released hop hung.").IsEqual(0);
            AssertThat(heldHang).OverrideFailureMessage($"weight {weight}: a held jump lost its hang.").IsGreater(10);
            AssertThat(releasedPeak).IsLess(heldPeak * 0.6f);
        }
    }

    /// <summary>
    /// A ground jump from launch speed <paramref name="launch"/>, Jump held on
    /// its first <paramref name="heldTicks"/> ticks (the launch tick counted),
    /// integrated through <see cref="StoryAirPhysics.StepVertical"/> as the
    /// controller does (the launch tick moves at full speed, then gravity, then
    /// the move). Returns the peak and the number of hang-band ticks.
    /// </summary>
    private static (float Peak, int HangTicks) TapArc(float launch, float weight, int heldTicks) {
        float vy = -launch;
        float height = launch * Dt;
        float peak = height;
        int hang = 0;
        for (int frame = 2; frame < 3600; frame++) {
            bool held = frame <= heldTicks;
            if (StoryAirPhysics.SelectBand(vy, false, false, held, true, false, true) == StoryGravityBand.ApexHang) hang++;
            vy = StoryAirPhysics.StepVertical(vy, weight, 1f, Dt, fastFalling: false, floatActive: false,
                jumpHeld: held, jumpedThisAirtime: true, launch: false, riseFromOwnJump: true);
            height -= vy * Dt;
            peak = Math.Max(peak, height);
            if (vy > 0f && height <= 0f) break;
        }
        return (peak, hang);
    }

    /// <summary>
    /// <see cref="StoryAirPhysics.SimulateJump"/>'s single held jump, integrated the
    /// same way (the launch tick at full speed, then gravity then the move) but
    /// with the apex hang off: what the rise would be without it.
    /// </summary>
    private static float NoHangDiscreteRise(float launch, float weight) {
        float vy = -launch;
        float height = launch * Dt;
        float peak = height;
        for (int frame = 2; frame < 3600; frame++) {
            vy = StoryAirPhysics.StepVertical(vy, weight, 1f, Dt, fastFalling: false, floatActive: false,
                jumpHeld: true, jumpedThisAirtime: false, launch: false);
            height -= vy * Dt;
            peak = Math.Max(peak, height);
            if (vy > 0f && height <= 0f) break;
        }
        return peak;
    }

    private static StoryGravityBand Band(float vy, bool fastFalling = false, bool floatActive = false,
        bool jumpHeld = true, bool jumped = false, bool launch = false, bool own = true) =>
        StoryAirPhysics.SelectBand(vy, fastFalling, floatActive, jumpHeld, jumped, launch, own);

    private static float Step(float vy, bool fastFalling = false) =>
        StoryAirPhysics.StepVertical(vy, 1f, 1f, Dt, fastFalling, floatActive: false, jumpHeld: true,
            jumpedThisAirtime: false, launch: false);

    /// <summary>PlayerController.ApplyGravity before 2026-10-08, with its own literals.</summary>
    private static float OldLaunchGravity(float vy, float weight, bool jumpHeld, bool floatActive) {
        float effectiveGravity = 18f * (0.8f + 0.4f * weight);
        float multiplier = 1f;
        if (floatActive) {
            multiplier = 0.15f;
            if (vy > 0f) vy = Math.Min(vy, 60f);
        } else if (vy > 0f) {
            multiplier = 1.8f;
        } else if (!jumpHeld && vy < 0f) {
            multiplier = 2.5f;
        }
        vy += effectiveGravity * multiplier * Dt * 60f;
        return Math.Min(vy, 600f);
    }

    private static float MoveToward(float from, float to, float delta) =>
        Math.Abs(to - from) <= delta ? to : from + Math.Sign(to - from) * delta;
}
