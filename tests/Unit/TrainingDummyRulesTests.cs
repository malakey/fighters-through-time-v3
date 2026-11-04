using FTT.Core;
using FTT.UI;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// GR-TRAIN (9c). The training dummy's behaviour as a pure function of its three session-only settings. Pure C#: no
/// engine API is touched.
/// </summary>
[TestSuite]
public class TrainingDummyRulesTests {

    private static TrainingDummyContext Context(int frame = 0, bool canAct = true, bool hitstun = false, int sinceHit = TrainingReadoutTracker.NoHit) =>
        new() { Frame = frame, CanAct = canAct, InHitstun = hitstun, FramesSinceHit = sinceHit, CentreDirection = -1f };

    [TestCase]
    public void TheDefaultDummyStandsStillAndADummyThatCannotActDoesNothingAtAll() {
        var settings = new TrainingDummySettings();
        TrainingDummyCommand idle = TrainingDummyRules.Step(in settings, Context());
        AssertThat((int)idle.Held).IsEqual(0);
        AssertThat(idle.MoveX).IsEqual(0f);

        var everything = new TrainingDummySettings {
            Block = TrainingBlockMode.Always, Jump = TrainingJumpMode.Always, Hitstun = TrainingHitstunResponse.HoldBlock };
        TrainingDummyCommand locked = TrainingDummyRules.Step(in everything, Context(canAct: false, hitstun: true));
        AssertThat((int)locked.Held).IsEqual(0);
        AssertThat(locked.MoveX).IsEqual(0f);
    }

    [TestCase]
    public void BlockHoldsAlwaysOrOnlyInTheWindowAfterAHit() {
        var always = new TrainingDummySettings { Block = TrainingBlockMode.Always };
        AssertThat(TrainingDummyRules.Step(in always, Context()).Held).IsEqual(GameplayButtons.Block);

        var afterHit = new TrainingDummySettings { Block = TrainingBlockMode.AfterHit };
        AssertThat((int)TrainingDummyRules.Step(in afterHit, Context()).Held).IsEqual(0);
        AssertThat(TrainingDummyRules.Step(in afterHit, Context(sinceHit: 0)).Held).IsEqual(GameplayButtons.Block);
        AssertThat(TrainingDummyRules.Step(in afterHit, Context(sinceHit: TrainingDummyRules.BlockAfterHitFrames - 1)).Held)
            .IsEqual(GameplayButtons.Block);
        AssertThat((int)TrainingDummyRules.Step(in afterHit, Context(sinceHit: TrainingDummyRules.BlockAfterHitFrames)).Held).IsEqual(0);
    }

    [TestCase]
    public void JumpHoldsForTheStartOfEachCycleAndWinsOverABlockHold() {
        var always = new TrainingDummySettings { Jump = TrainingJumpMode.Always };
        AssertThat(TrainingDummyRules.Step(in always, Context(frame: 0)).Held).IsEqual(GameplayButtons.Jump);
        AssertThat(TrainingDummyRules.Step(in always, Context(frame: TrainingDummyRules.JumpHoldFrames - 1)).Held).IsEqual(GameplayButtons.Jump);
        AssertThat((int)TrainingDummyRules.Step(in always, Context(frame: TrainingDummyRules.JumpHoldFrames)).Held).IsEqual(0);
        AssertThat(TrainingDummyRules.Step(in always, Context(frame: TrainingDummyRules.AlwaysJumpCycleFrames)).Held).IsEqual(GameplayButtons.Jump);

        var timed = new TrainingDummySettings { Jump = TrainingJumpMode.Timed };
        AssertThat((int)TrainingDummyRules.Step(in timed, Context(frame: TrainingDummyRules.AlwaysJumpCycleFrames)).Held).IsEqual(0);
        AssertThat(TrainingDummyRules.Step(in timed, Context(frame: TrainingDummyRules.TimedJumpCycleFrames)).Held).IsEqual(GameplayButtons.Jump);

        var both = new TrainingDummySettings { Jump = TrainingJumpMode.Always, Block = TrainingBlockMode.Always };
        AssertThat(TrainingDummyRules.Step(in both, Context(frame: 0)).Held).IsEqual(GameplayButtons.Jump);
        AssertThat(TrainingDummyRules.Step(in both, Context(frame: TrainingDummyRules.JumpHoldFrames)).Held).IsEqual(GameplayButtons.Block);
    }

    [TestCase]
    public void HitstunResponseHoldsBlockOrTheStickTowardTheCentreAndTheSourceDerivesRealEdges() {
        var holdBlock = new TrainingDummySettings { Hitstun = TrainingHitstunResponse.HoldBlock, Block = TrainingBlockMode.Never };
        AssertThat(TrainingDummyRules.Step(in holdBlock, Context(hitstun: true)).Held).IsEqual(GameplayButtons.Block);
        var di = new TrainingDummySettings { Hitstun = TrainingHitstunResponse.DirectionalInfluence };
        TrainingDummyCommand toward = TrainingDummyRules.Step(in di, Context(hitstun: true));
        AssertThat(toward.MoveX).IsEqual(-1f);
        AssertThat((int)toward.Held).IsEqual(0);
        var none = new TrainingDummySettings();
        AssertThat(TrainingDummyRules.Step(in none, Context(hitstun: true)).MoveX).IsEqual(0f);

        // The source turns a held bit into one press edge, then a plain hold.
        var source = new TrainingDummySource();
        source.SetCommand(new TrainingDummyCommand { Held = GameplayButtons.Jump });
        PlayerInputFrame first = source.Sample(1, default);
        PlayerInputFrame second = source.Sample(2, first);
        AssertThat(first.Pressed).IsEqual(GameplayButtons.Jump);
        AssertThat((int)second.Pressed).IsEqual(0);
        AssertThat(second.Held).IsEqual(GameplayButtons.Jump);
    }
}
