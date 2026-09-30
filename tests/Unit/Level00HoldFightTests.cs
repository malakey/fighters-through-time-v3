using System.IO;
using FTT.Environment;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 13 W3 (S02): the rules of Level 0's Part 1 hold fight — beat 5, "The
/// hold". The 3-hit string against the first locals, then a telegraphed swing
/// that repeats until it is blocked, then the last local falls and the beam
/// breaks. It cannot be lost (HP floor 1; the floor itself is pinned on a live
/// controller in <c>Level00FlowSceneTests</c>). Pure C#: <see cref="Level00HoldFight"/>
/// has no engine dependency (failure signature 7).
/// </summary>
[TestSuite]
public class Level00HoldFightTests {

    [TestCase]
    public void TheHoldRunsStringThenGuardThenFinishInThatOrder() {
        var hold = new Level00HoldFight();
        AssertThat(hold.Step).IsEqual(HoldFightStep.Opening);
        // Nothing counts before the voice has spoken.
        AssertThat(hold.RegisterLocalDefeated()).IsEqual(HoldFightStep.Opening);
        AssertThat(hold.BeginFight()).IsTrue();
        AssertThat(hold.BeginFight()).IsFalse();

        for (int index = 0; index < Level00HoldFight.LocalsBeforeGuard - 1; index++) {
            AssertThat(hold.RegisterLocalDefeated()).IsEqual(HoldFightStep.FightOff);
        }
        AssertThat(hold.RegisterLocalDefeated()).IsEqual(HoldFightStep.Guard);

        // The last local cannot be killed past its lesson.
        AssertThat(hold.RegisterLocalDefeated()).IsEqual(HoldFightStep.Guard);
        AssertThat(hold.LocalsDefeated).IsEqual(Level00HoldFight.LocalsBeforeGuard);

        AssertThat(hold.RegisterLocalDefeated() == HoldFightStep.Done).IsFalse();
    }

    [TestCase]
    public void TheGuardSwingRepeatsUntilOneIsBlocked() {
        var hold = ReadyForGuard();
        AssertThat(hold.RegisterGuardSwing(blocked: false)).IsFalse();
        AssertThat(hold.RegisterGuardSwing(blocked: false)).IsFalse();
        AssertThat(hold.Step).IsEqual(HoldFightStep.Guard);
        AssertThat(hold.RegisterGuardSwing(blocked: true)).IsTrue();
        AssertThat(hold.Step).IsEqual(HoldFightStep.Finish);
        AssertThat(hold.GuardAttempts).IsEqual(3);
        // A swing outside the lesson is ignored.
        AssertThat(hold.RegisterGuardSwing(blocked: true)).IsFalse();

        AssertThat(hold.RegisterLocalDefeated()).IsEqual(HoldFightStep.Done);
        AssertThat(hold.LocalsDefeated).IsEqual(Level00HoldFight.LocalCount);
    }

    [TestCase]
    public void AMissingLocalNeverStrandsTheStoryBeat() {
        var hold = ReadyForGuard();
        AssertThat(hold.ForceComplete()).IsTrue();
        AssertThat(hold.Step).IsEqual(HoldFightStep.Done);
        AssertThat(hold.ForceComplete()).IsFalse();
    }

    [TestCase]
    public void EveryHeroFightsLocalsOfAnAuthoredEraMobAndTheHoldIsUnlosable() {
        AssertThat(Level00HoldFight.HeroHPFloor).IsEqual(1);
        AssertThat(Level00HoldFight.LocalCount >= 2).IsTrue();
        foreach (string hero in new[] {
                     "einstein", "joan", "leonardo", "tesla", "mozart", "cleopatra",
                     "shakespeare", "lincoln", "pocahontas", "tubman", "", "not_a_hero" }) {
            string mob = Level00HoldFight.LocalMobFor(hero);
            AssertThat(File.Exists($"resources/Enemies/{mob}.tres"))
                .OverrideFailureMessage($"{hero}'s hold local '{mob}' has no EnemyData resource.").IsTrue();
        }
        // The home-era mapping for the shared-level heroes (design §6 era roster).
        AssertString(Level00HoldFight.LocalMobFor("leonardo")).IsEqual("cyber_guard");
        AssertString(Level00HoldFight.LocalMobFor("joan")).IsEqual("laser_archer");
        AssertString(Level00HoldFight.LocalMobFor("lincoln")).IsEqual("laser_rifle_infantry");
        AssertString(Level00HoldFight.LocalMobFor("not_a_hero")).IsEqual(Level00HoldFight.FallbackLocalMob);
    }

    private static Level00HoldFight ReadyForGuard() {
        var hold = new Level00HoldFight();
        hold.BeginFight();
        for (int index = 0; index < Level00HoldFight.LocalsBeforeGuard; index++) hold.RegisterLocalDefeated();
        return hold;
    }
}
