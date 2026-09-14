using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 11 A1c. Echo Step's <b>exact-or-no-teleport</b> destination policy
/// (<c>docs/design-contracts/TEMPORAL_STATE_CONTRACT.md</c> steps 3-5).
///
/// <para>Before this, <c>TryStartEchoStep</c> spent the meter and armed the cooldown
/// unconditionally and <c>AdvanceEchoStep</c> wrote the destination straight into
/// <c>fighter.Position</c> with only a "y &gt; 0 means airborne" fix-up: a
/// destination inside terrain or outside the stage teleported there. The contract's
/// two rules are that an invalid target is refused <b>before</b> anything is spent,
/// and that a target which becomes blocked during the wind-up cancels with the
/// committed cost <b>retained</b> — never a nearby substitute.</para>
/// </summary>
[TestSuite]
public class EchoStepDestinationTests {

    /// <summary>
    /// A position the fighter legitimately occupied is legitimate to return to. The
    /// floor plane and both walls are inclusive, because those are exactly the
    /// coordinates the movement system clamps a fighter to.
    /// </summary>
    [TestCase]
    public void OrdinaryOccupiablePositionsAreClear() {
        FighterStageGeometry stage = FighterStageGeometry.Florence;
        AssertThat(Clear(stage, FP64.Zero, FP64.Zero)).IsTrue();
        AssertThat(Clear(stage, stage.LeftWall, FP64.Zero)).IsTrue();
        AssertThat(Clear(stage, stage.RightWall, FP64.Zero)).IsTrue();
        AssertThat(Clear(stage, FP64.Zero, stage.Ceiling)).IsTrue();
    }

    /// <summary>
    /// Airborne destinations remain legal and support is never required — a jump
    /// arc is a valid Echo Step target, which is most of the verb's point.
    /// </summary>
    [TestCase]
    public void AirborneDestinationsAreLegalAndNeedNoSupport() {
        FighterStageGeometry stage = FighterStageGeometry.Florence;
        AssertThat(Clear(stage, FP64.Zero, FP64.FromInt(4)))
            .OverrideFailureMessage("Mid-air destinations must stay legal.")
            .IsTrue();

        // Over an Open stage's pit, above the floor plane, is still just air.
        FighterStageGeometry open = FirstOpenStage();
        FP64 pitX = FirstPitX(open);
        AssertThat(Clear(open, pitX, FP64.FromInt(3)))
            .OverrideFailureMessage("Air above a pit is a legal airborne destination.")
            .IsTrue();
    }

    /// <summary>
    /// Solid terrain and the stage bounds refuse: inside the floor, past a side
    /// wall, above the ceiling, and below the blast zone.
    /// </summary>
    [TestCase]
    public void TerrainAndStageBoundsRefuse() {
        FighterStageGeometry stage = FighterStageGeometry.Florence;
        AssertThat(Clear(stage, FP64.Zero, -FP64.One))
            .OverrideFailureMessage("Inside the solid floor must refuse.")
            .IsFalse();
        AssertThat(Clear(stage, stage.LeftWall - FP64.One, FP64.Zero)).IsFalse();
        AssertThat(Clear(stage, stage.RightWall + FP64.One, FP64.Zero)).IsFalse();
        AssertThat(Clear(stage, FP64.Zero, stage.Ceiling + FP64.One)).IsFalse();
        AssertThat(Clear(stage, FP64.Zero, stage.BottomBlastZone - FP64.One))
            .OverrideFailureMessage("An authored kill region must refuse.")
            .IsFalse();
    }

    /// <summary>
    /// A destination <b>inside</b> one of A9's authored pits — below the floor
    /// plane at an x with no floor segment — is refused. Its only exit is the blast
    /// zone, so returning there is a teleport into a kill region rather than a
    /// recovery.
    /// </summary>
    [TestCase]
    public void AFloorSegmentPitDestinationIsRejected() {
        FighterStageGeometry open = FirstOpenStage();
        FP64 pitX = FirstPitX(open);
        AssertThat(open.HasFloorSupport(pitX))
            .OverrideFailureMessage("Harness: the chosen x must genuinely be over a pit.")
            .IsFalse();
        AssertThat(Clear(open, pitX, -FP64.One))
            .OverrideFailureMessage("A destination inside an authored pit must be rejected.")
            .IsFalse();
    }

    /// <summary>
    /// The history gate: fewer than 31 real samples means no exact <c>t - 30</c>
    /// exists, so the activation is refused before it can reach the meter.
    /// </summary>
    [TestCase]
    public void TheLookbackGateNeedsAFullWindowOfRealSamples() {
        AssertThat(FighterEchoStepRules.HasLookbackSample(0)).IsFalse();
        AssertThat(FighterEchoStepRules.HasLookbackSample(BasicComboRules.EchoStepLookbackFrames)).IsFalse();
        AssertThat(FighterEchoStepRules.HasLookbackSample(FighterEchoStepRing.SampleCount)).IsTrue();
        AssertThat(FighterEchoStepRules.HasLookbackSample(FighterEchoStepRing.SampleCount + 5)).IsTrue();
    }

    // === Helpers ===

    private static bool Clear(FighterStageGeometry stage, FP64 x, FP64 y) =>
        FighterEchoStepRules.IsDestinationClear(stage, new FPVector2(x, y));

    private static FighterStageGeometry FirstOpenStage() {
        foreach (FighterStageGeometry geometry in FighterStageGeometry.AllAuthored) {
            if (geometry.IsOpenStage) return geometry;
        }
        AssertThat(false)
            .OverrideFailureMessage("Harness: no Open stage is authored; A9 landed three.")
            .IsTrue();
        return FighterStageGeometry.Default;
    }

    /// <summary>An x inside the first gap between two authored floor segments.</summary>
    private static FP64 FirstPitX(FighterStageGeometry geometry) {
        for (int index = 0; index + 1 < geometry.FloorSegments.Length; index++) {
            FP64 gapLeft = geometry.FloorSegments[index].EdgeX(1);
            FP64 gapRight = geometry.FloorSegments[index + 1].EdgeX(0);
            if (gapRight > gapLeft) return (gapLeft + gapRight) / FP64.FromInt(2);
        }
        AssertThat(false)
            .OverrideFailureMessage($"Harness: {geometry.StageID} authors no gap between floor segments.")
            .IsTrue();
        return FP64.Zero;
    }
}
