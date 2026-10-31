using FTT.Diagnostics;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// MM-BOTS (meter and Defy update 2026-10-31, plan 2.13, O24, P26): when the bots press Defy.
/// Pure C#; the bot feeds <see cref="PlaytestDefyPolicy"/> the hero's readable state, so no scene is needed.
/// </summary>
[TestSuite]
public class PlaytestDefyPolicyTests {
    private static PlaytestDefyPolicy WithTwoHits(int frame) {
        var policy = new PlaytestDefyPolicy();
        policy.NoteHit(frame - 40);
        policy.NoteHit(frame - 10);
        return policy;
    }

    [TestCase]
    public void PlayPressesDefyOnlyInHitstunInAStringWithAFoeNearAndNotWithinThreeHundredFramesOfTheLast() {
        const int now = 1000;
        PlaytestDefyPolicy policy = WithTwoHits(now);
        AssertThat(policy.ShouldDefy(FightStyle.Play, now, canDefy: true, inHitstun: true, enemiesNear: 2)).IsTrue();

        // Each condition on its own.
        AssertThat(policy.ShouldDefy(FightStyle.Play, now, canDefy: false, inHitstun: true, enemiesNear: 2))
            .OverrideFailureMessage("The hero cannot Defy (under 50 Influence, a refused state).").IsFalse();
        AssertThat(policy.ShouldDefy(FightStyle.Play, now, canDefy: true, inHitstun: false, enemiesNear: 2))
            .OverrideFailureMessage("Not in hitstun.").IsFalse();
        AssertThat(policy.ShouldDefy(FightStyle.Play, now, canDefy: true, inHitstun: true, enemiesNear: 0))
            .OverrideFailureMessage("No foe within 250 px.").IsFalse();
        AssertThat(policy.ShouldDefy(FightStyle.Play, now, canDefy: true, inHitstun: true, enemiesNear: 1))
            .OverrideFailureMessage("One foe near is enough.").IsTrue();

        // A string is two hits in 60 frames: one hit, or two hits 61 apart, is not.
        var oneHit = new PlaytestDefyPolicy();
        oneHit.NoteHit(now - 5);
        AssertThat(oneHit.ShouldDefy(FightStyle.Play, now, true, true, 3)).IsFalse();
        var farApart = new PlaytestDefyPolicy();
        farApart.NoteHit(now - 70);
        farApart.NoteHit(now - 5);
        AssertThat(farApart.ShouldDefy(FightStyle.Play, now, true, true, 3)).IsFalse();
        var edge = new PlaytestDefyPolicy();
        edge.NoteHit(now - 60);
        edge.NoteHit(now - 5);
        AssertThat(edge.ShouldDefy(FightStyle.Play, now, true, true, 3)).OverrideFailureMessage("60 frames apart is still a string.").IsTrue();

        // Spacing: pressed at `now`, 299 frames later is too soon even in a fresh string, 300 is allowed.
        policy.NoteDefyPressed(now);
        policy.NoteHit(now + 280);
        policy.NoteHit(now + 295);
        AssertThat(policy.ShouldDefy(FightStyle.Play, now + 299, true, true, 2)).IsFalse();
        AssertThat(policy.ShouldDefy(FightStyle.Play, now + 300, true, true, 2)).IsTrue();
    }

    [TestCase]
    public void SpamBasicsAndBypassNeverPressDefyWhateverTheSituation() {
        const int now = 1000;
        foreach (FightStyle style in new[] { FightStyle.Spam, FightStyle.Basics, FightStyle.Bypass }) {
            AssertThat(PlaytestDefyPolicy.StyleDefies(style)).IsFalse();
            AssertThat(WithTwoHits(now).ShouldDefy(style, now, canDefy: true, inHitstun: true, enemiesNear: 5))
                .OverrideFailureMessage($"{style} must never Defy.").IsFalse();
        }
        AssertThat(PlaytestDefyPolicy.StyleDefies(FightStyle.Play)).IsTrue();
    }
}
