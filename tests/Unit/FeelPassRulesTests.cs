using FTT.Combat;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// 2026-10-04 playtest feel pass, workstream FEEL — the engine-free rules both
/// modes read: the provisional hitstop weight (F6,
/// <c>PROVISIONAL-HITSTOP-WEIGHT</c>), the shared chain-continuation rule (F3)
/// and the retro three-pose mapping (F1). Pure C#: nothing here touches the
/// Godot runtime.
/// </summary>
[TestSuite]
public class FeelPassRulesTests {

    [TestCase]
    public void HitstopFloorIsFourAndALauncherAddsThreeCappedAtEleven() {
        AssertThat(BasicComboRules.HitstopMinFrames).IsEqual(4);
        AssertThat(BasicComboRules.HitstopMaxFrames).IsEqual(8);
        AssertThat(BasicComboRules.LaunchHitstopBonusFrames).IsEqual(3);
        AssertThat(BasicComboRules.HitstopCeilingFrames).IsEqual(11);
        AssertThat(BasicComboRules.BlockedHitstopFrames)
            .OverrideFailureMessage("The blocked freeze is not part of the provisional change.")
            .IsEqual(2);

        // The damage curve: the floor at <=5, linear to 8 at >=25 (integer floor).
        AssertThat(BasicComboRules.HitstopFrames(0, launches: false)).IsEqual(4);
        AssertThat(BasicComboRules.HitstopFrames(5, launches: false)).IsEqual(4);
        AssertThat(BasicComboRules.HitstopFrames(10, launches: false)).IsEqual(5);
        AssertThat(BasicComboRules.HitstopFrames(15, launches: false)).IsEqual(6);
        AssertThat(BasicComboRules.HitstopFrames(25, launches: false)).IsEqual(8);
        AssertThat(BasicComboRules.HitstopFrames(400, launches: false)).IsEqual(8);

        // A launcher adds the flat bonus on top, capped at the ceiling.
        AssertThat(BasicComboRules.HitstopFrames(5, launches: true)).IsEqual(7);
        AssertThat(BasicComboRules.HitstopFrames(15, launches: true)).IsEqual(9);
        AssertThat(BasicComboRules.HitstopFrames(25, launches: true)).IsEqual(11);
        AssertThat(BasicComboRules.HitstopFrames(400, launches: true)).IsEqual(11);

        // The one-argument form is the non-launching rule, never a second copy.
        for (int damage = 0; damage <= 40; damage++) {
            AssertThat(BasicComboRules.HitstopFrames(damage))
                .OverrideFailureMessage($"HitstopFrames({damage}) drifted from the two-argument rule.")
                .IsEqual(BasicComboRules.HitstopFrames(damage, launches: false));
            AssertThat(BasicComboRules.HitstopFrames(damage, launches: true)
                    - BasicComboRules.HitstopFrames(damage, launches: false))
                .OverrideFailureMessage($"The launch bonus at {damage} damage is not the flat bonus.")
                .IsEqual(BasicComboRules.LaunchHitstopBonusFrames);
        }
    }

    [TestCase]
    public void TheChainContinuesOnABufferOrAHeldButtonButNeverPastTheFinisher() {
        for (int step = 0; step < BasicComboRules.ComboHits - 1; step++) {
            AssertThat(BasicComboRules.ChainContinues(step, buffered: true, attackHeld: false)).IsTrue();
            AssertThat(BasicComboRules.ChainContinues(step, buffered: false, attackHeld: true))
                .OverrideFailureMessage("Holding the attack button continues the chain (design-godot.md).")
                .IsTrue();
            AssertThat(BasicComboRules.ChainContinues(step, buffered: false, attackHeld: false)).IsFalse();
        }
        int finisher = BasicComboRules.ComboHits - 1;
        AssertThat(BasicComboRules.ChainContinues(finisher, buffered: true, attackHeld: true))
            .OverrideFailureMessage("The finisher exits straight out.")
            .IsFalse();
    }

    [TestCase]
    public void TheThreeKeyPosesFollowTheAuthoredWindows() {
        AssertThat(AttackPoseRules.PoseFor(CombatFramePhase.Startup)).IsEqual(AttackPoseRules.WindUpPose);
        AssertThat(AttackPoseRules.PoseFor(CombatFramePhase.Active)).IsEqual(AttackPoseRules.StrikePose);
        AssertThat(AttackPoseRules.PoseFor(CombatFramePhase.Recovery)).IsEqual(AttackPoseRules.FollowThroughPose);
        AssertThat(AttackPoseRules.PoseFor(CombatFramePhase.Complete)).IsEqual(AttackPoseRules.FollowThroughPose);

        AssertThat(AttackPoseRules.PoseFor(AbilityPhase.Startup)).IsEqual(AttackPoseRules.WindUpPose);
        AssertThat(AttackPoseRules.PoseFor(AbilityPhase.Active)).IsEqual(AttackPoseRules.StrikePose);
        AssertThat(AttackPoseRules.PoseFor(AbilityPhase.Recovery)).IsEqual(AttackPoseRules.FollowThroughPose);
        AssertThat(AttackPoseRules.PoseFor(AbilityPhase.Cleanup)).IsEqual(AttackPoseRules.FollowThroughPose);

        // A phaseless presentation hold (the sim's on-press Specials) in thirds.
        AssertThat(AttackPoseRules.PoseForHold(0, 18)).IsEqual(AttackPoseRules.WindUpPose);
        AssertThat(AttackPoseRules.PoseForHold(5, 18)).IsEqual(AttackPoseRules.WindUpPose);
        AssertThat(AttackPoseRules.PoseForHold(6, 18)).IsEqual(AttackPoseRules.StrikePose);
        AssertThat(AttackPoseRules.PoseForHold(11, 18)).IsEqual(AttackPoseRules.StrikePose);
        AssertThat(AttackPoseRules.PoseForHold(12, 18)).IsEqual(AttackPoseRules.FollowThroughPose);
        AssertThat(AttackPoseRules.PoseForHold(17, 18)).IsEqual(AttackPoseRules.FollowThroughPose);
        AssertThat(AttackPoseRules.PoseForHold(40, 18)).IsEqual(AttackPoseRules.FollowThroughPose);
        AssertThat(AttackPoseRules.PoseForHold(3, 0)).IsEqual(AttackPoseRules.WindUpPose);

        // A sheet with fewer than three frames clamps instead of indexing past it.
        AssertThat(AttackPoseRules.ClampToFrameCount(AttackPoseRules.FollowThroughPose, 3)).IsEqual(2);
        AssertThat(AttackPoseRules.ClampToFrameCount(AttackPoseRules.FollowThroughPose, 1)).IsEqual(0);
        AssertThat(AttackPoseRules.ClampToFrameCount(AttackPoseRules.StrikePose, 0)).IsEqual(0);
        AssertThat(AttackPoseRules.ClampToFrameCount(-1, 3)).IsEqual(0);
    }
}
