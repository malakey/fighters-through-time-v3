using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Pins the Story half of V7.1's recoverable-health layer: Rally echo accrual
/// with the deferred-meter guardrail, reclaim through the
/// <c>AddInfluenceFromDamageDealt</c> chokepoint (constructs opt out), and
/// Defy History (a full Ultimate Meter refuses one lethal hit per level,
/// firing before the Chronal Rewind would). Fractions and the drain window
/// come from <see cref="BasicComboRules"/> — the same numbers the Fighter
/// sim's chokepoint reads.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryRallyTests {

    [TestCase]
    public void RallyEchoAccruesOnAHitAndDrainsIntoDeferredMeter() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(player);
        try {
            player.GetNode<Hurtbox>("Hurtbox").TakeHit(Hit(damage: 20f));
            float expectedFraction = BasicComboRules.EchoFractionBase
                + BasicComboRules.EchoFractionSlope * (20f / player.MaximumHP);
            AssertThat(player.EchoPool)
                .OverrideFailureMessage("A survivable hit must stash its echo fraction.")
                .IsEqualApprox(20f * expectedFraction, 0.05f);
            // Deferred-meter guardrail: only the permanent portion accrued now.
            AssertThat(player.CurrentUltimateMeter < 20f * UltimateMeter.PointsPerDamageTaken)
                .OverrideFailureMessage("The echo portion's meter must not accrue at hit time.")
                .IsTrue();

            // Run the hitstop freeze plus the full 150-frame drain: the pool
            // empties and the deferred meter completes to exactly damage x 0.25.
            HoldInput(player, GameplayButtons.None,
                frames: BasicComboRules.HitstopFrames(20) + BasicComboRules.EchoDrainFrames + 10);
            AssertThat(player.EchoPool).IsEqual(0f);
            AssertThat(player.CurrentUltimateMeter)
                .OverrideFailureMessage("Drained echo must complete the meter to damage/4.")
                .IsEqualApprox(20f * UltimateMeter.PointsPerDamageTaken, 0.1f);
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
        }
    }

    [TestCase]
    public void DirectHitsReclaimTheEchoButConstructHitsDoNot() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(player);
        try {
            player.GetNode<Hurtbox>("Hurtbox").TakeHit(Hit(damage: 40f));
            AssertThat(player.EchoPool > 0f).IsTrue();
            int hpAfterHit = player.CurrentHP;
            float meterBefore = player.CurrentUltimateMeter;

            // Construct damage: meter accrues, the pool stays.
            player.AddInfluenceFromDamageDealt(10f, collectsEcho: false);
            AssertThat(player.EchoPool > 0f)
                .OverrideFailureMessage("Construct hits must never reclaim the echo.")
                .IsTrue();
            AssertThat(player.CurrentHP).IsEqual(hpAfterHit);
            AssertThat(player.CurrentUltimateMeter > meterBefore).IsTrue();

            // A direct hit: the remaining pool converts to real HP, no extra
            // meter for the reclaim itself (only the damage-dealt credit).
            float pool = player.EchoPool;
            player.AddInfluenceFromDamageDealt(10f);
            AssertThat(player.EchoPool).IsEqual(0f);
            AssertThat(player.CurrentHP)
                .OverrideFailureMessage("A landed direct hit must reclaim the echo as real HP.")
                .IsEqual(hpAfterHit + Mathf.RoundToInt(pool));
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
        }
    }

    [TestCase]
    public void DefyHistorySavesOneLethalHitPerLevelAtFullMeter() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(player);
        try {
            // Fill the meter through the ordinary chokepoint (no reclaim: the
            // pool is empty anyway, and the tutorial grant passes false too).
            player.AddInfluenceFromDamageDealt(UltimateMeter.MaxValue, collectsEcho: false);
            AssertThat(player.CurrentUltimateMeter).IsEqual(UltimateMeter.MaxValue);

            int applied = player.ApplyDamage(player.MaximumHP);
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("A lethal hit against a full meter must not kill.")
                .IsNotEqual(CharacterState.Dead);
            AssertThat(player.CurrentHP).IsEqual(1);
            AssertThat(applied).IsEqual(player.MaximumHP - 1);
            AssertThat(player.CurrentUltimateMeter)
                .OverrideFailureMessage("Defy History shatters the meter to zero.")
                .IsEqual(0f);
            AssertThat(player.StoryDefyHistoryUsed).IsTrue();

            // Once per level: even with the meter refilled, the next lethal
            // hit kills normally (the Chronal Rewind takes over from there).
            player.AddInfluenceFromDamageDealt(UltimateMeter.MaxValue, collectsEcho: false);
            player.ApplyDamage(player.MaximumHP);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Dead);
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
        }
    }

    // ---- Harness -------------------------------------------------------------

    private static void HoldInput(PlayerController player, GameplayButtons buttons, int frames) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, buttons));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        for (int frame = 0; frame < frames; frame++) player._PhysicsProcess(1.0 / 60.0);
    }

    /// <summary>An impulse-free, stun-free test hit so state stays Idle.</summary>
    private static HitPayload Hit(float damage) => new() {
        AttackerIndex = 1,
        TargetIndex = 0,
        AttackID = "test.hit",
        HitboxID = "primary",
        AttackClass = AttackClass.Basic,
        Damage = damage,
        Knockback = Vector2.Zero,
        HitstunDuration = 0f,
        HitOrigin = new Vector2(-20f, 0f),
        AttackerFacingRight = true
    };
}
