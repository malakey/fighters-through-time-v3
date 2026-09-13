using FTT.Characters;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Pins V7.6 F16 lethal Story pits (Package 11 A3).
///
/// The rule is short and absolute: <b>falling is not a hit</b>. Crossing an
/// authored kill boundary resolves one non-hit fall death regardless of HP,
/// shield, armor or any temporary invulnerability, and it must not touch Defy
/// History, the Rally echo or the Ultimate meter — a pit must never be able
/// to spend the run's one saved life.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryKillBoundaryTests {

    [TestCase]
    public void CrossingKillsAtFullHealthThroughAShieldAndThroughInvulnerability() {
        PlayerController player = NewPlayer();
        var boundary = NewBoundary();
        try {
            player.ConfigureStoryShield(500f);
            player.RechargeStoryShield(500f);
            AssertThat(player.CurrentHP).IsEqual(player.MaximumHP);
            AssertThat(player.StoryShieldPoints).IsGreater(0f);

            AssertThat(boundary.Resolve(player))
                .OverrideFailureMessage("An authored kill boundary always resolves a fall death.")
                .IsTrue();
            AssertThat(player.CurrentHP)
                .OverrideFailureMessage("A full shield and full HP do not survive a pit.")
                .IsEqual(0);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Dead);
        } finally {
            Cleanup(player, boundary);
        }
    }

    [TestCase]
    public void DefyHistoryCannotPreventItAndItsUsedFlagIsUnchanged() {
        PlayerController player = NewPlayer();
        var boundary = NewBoundary();
        try {
            // A full Ultimate meter is exactly the state in which a lethal HIT
            // would be defied. A fall is not a hit.
            player.GetNode<FTT.Combat.UltimateMeter>("UltimateMeter")
                .SetValue(FTT.Combat.UltimateMeter.MaxValue);
            player.CurrentUltimateMeter = FTT.Combat.UltimateMeter.MaxValue;
            AssertThat(player.CurrentUltimateMeter)
                .IsEqualApprox(FTT.Combat.UltimateMeter.MaxValue, 0.001f);
            AssertThat(player.StoryDefyHistoryUsed).IsFalse();

            boundary.Resolve(player);

            AssertThat(player.CurrentHP)
                .OverrideFailureMessage("Defy History must not save the player from a pit.")
                .IsEqual(0);
            AssertThat(player.StoryDefyHistoryUsed)
                .OverrideFailureMessage("A pit must never spend the run's one Defy.")
                .IsFalse();
            AssertThat(player.CurrentUltimateMeter)
                .OverrideFailureMessage("A fall shatters no meter.")
                .IsEqualApprox(FTT.Combat.UltimateMeter.MaxValue, 0.001f);
        } finally {
            Cleanup(player, boundary);
        }
    }

    [TestCase]
    public void AFallGeneratesNoRallyEchoAndNoMeterFromDamageTaken() {
        PlayerController player = NewPlayer();
        var boundary = NewBoundary();
        try {
            player.GetNode<FTT.Combat.UltimateMeter>("UltimateMeter").SetValue(0f);
            player.CurrentUltimateMeter = 0f;
            AssertThat(player.EchoPool).IsEqual(0f);

            boundary.Resolve(player);

            AssertThat(player.EchoPool)
                .OverrideFailureMessage("No damage was applied, so there is nothing to reclaim.")
                .IsEqual(0f);
            AssertThat(player.CurrentUltimateMeter)
                .OverrideFailureMessage("A fall is not damage taken; it grants no meter.")
                .IsEqual(0f);
        } finally {
            Cleanup(player, boundary);
        }
    }

    [TestCase]
    public void ACrossingRaisesTheOrdinaryDeathEventSoTheRewindStackSpendsOneCharge() {
        PlayerController player = NewPlayer();
        var boundary = NewBoundary();
        int deaths = 0;
        void Count(int _) => deaths++;
        EventBus.Instance.OnPlayerDied += Count;
        try {
            boundary.Resolve(player);
            AssertThat(deaths)
                .OverrideFailureMessage("The pit routes into the existing death flow, not a bespoke one.")
                .IsEqual(1);
            AssertThat(boundary.KillCount).IsEqual(1);

            // Already dead: a second crossing in the same fall resolves nothing.
            AssertThat(boundary.Resolve(player)).IsFalse();
            AssertThat(deaths).IsEqual(1);
        } finally {
            EventBus.Instance.OnPlayerDied -= Count;
            Cleanup(player, boundary);
        }
    }

    [TestCase]
    public void IntegrityAtZeroOnTheSameUpdateResolvesTheTimerCollapseInstead() {
        StoryManager story = StoryManager.Instance;
        PlayerController player = NewPlayer();
        var boundary = NewBoundary();
        int deaths = 0;
        void Count(int _) => deaths++;
        EventBus.Instance.OnPlayerDied += Count;
        try {
            story.BeginLevelRun();
            // A par so short the clock is already spent by the time the hero
            // crosses. F16: one collapse, and no rewind charge spent for it.
            story.BeginIntegrityClock(1f, startingExtractors: 0);
            story.ActIIICollapseOverride = () => { };
            TimelineIntegrityTests.Pump(story, seconds: 5f);
            AssertThat(story.TimelineIntegrityPercent).IsEqual(0f);

            AssertThat(boundary.Resolve(player))
                .OverrideFailureMessage("The timer collapse owns this update; the pit must stand down.")
                .IsFalse();
            AssertThat(deaths)
                .OverrideFailureMessage("The player must not be charged both a rewind and a collapse.")
                .IsEqual(0);
            AssertThat(player.CurrentState).IsNotEqual(CharacterState.Dead);
        } finally {
            EventBus.Instance.OnPlayerDied -= Count;
            story.ActIIICollapseOverride = null;
            story.ClearLevelAttemptState();
            story.StopLevelRun();
            Cleanup(player, boundary);
        }
    }

    // === Fixtures =======================================================

    private static PlayerController NewPlayer() {
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        player.GlobalPosition = Vector2.Zero;
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(player);
        return player;
    }

    private static StoryKillBoundary NewBoundary() {
        var boundary = new StoryKillBoundary {
            Name = "TestKillBoundary",
            BoundaryID = "test_kill_boundary"
        };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(boundary);
        return boundary;
    }

    private static void Cleanup(PlayerController player, StoryKillBoundary boundary) {
        if (GodotObject.IsInstanceValid(player)) player.Free();
        if (GodotObject.IsInstanceValid(boundary)) boundary.Free();
    }
}
