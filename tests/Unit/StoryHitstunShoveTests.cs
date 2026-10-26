using System;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// FU-STORYSHOVE (2026-10-26, ledger <c>USER-STORY-HERO-HITSTUN-SHOVE</c>): the
/// Story hero's grounded non-launching hitstun is the same short shove as the
/// Fighter's (FU-KNOCK R3) - the hit scales the speed by
/// <c>BasicComboRules.HitstunShoveScale</c>, <c>ProcessStunned</c> decays it by
/// <c>HitstunShoveDecay</c> each tick and snaps it to zero, and the hero is
/// planted for the rest of the stun. Launches, the guard-break push and airborne
/// non-launchers keep their motion. Fighter half: <c>FighterHitstunShoveTests</c>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryHitstunShoveTests {

    private static HitPayload MobHit(float force, bool launches = false, float hitstun = 0.6f) => new HitPayload {
        AttackerIndex = -1,
        TargetIndex = 0,
        AttackID = "test.mob_hit",
        HitboxID = "primary",
        AttackClass = AttackClass.Basic,
        Damage = 5f,
        Knockback = new Vector2(force, -1.5f),
        Launches = launches,
        SourceIsStandardMob = true,
        HitstunDuration = hitstun,
        HitOrigin = new Vector2(-20f, 0f),
        AttackerFacingRight = true,
        ExemptFromHitstop = true
    };

    /// <summary>Hits the hero and returns (distance px, first tick at rest or -1, stun ticks).</summary>
    private static (float Distance, int StopTick, int StunTicks) Measure(string hero, float force) {
        (float, int, int) result = default;
        Run(hero, player => {
            float x0 = player.GlobalPosition.X;
            player.GetNode<Hurtbox>("Hurtbox").TakeHit(MobHit(force));
            AssertThat(player.CurrentState).IsEqual(CharacterState.Stunned);
            int stop = -1;
            int ticks = 0;
            while (player.CurrentState == CharacterState.Stunned && ticks < 120) {
                Step(player);
                ticks++;
                if (stop < 0 && player.Velocity.X == 0f) stop = ticks;
            }
            result = (player.GlobalPosition.X - x0, stop, ticks);
        });
        return result;
    }

    [TestCase]
    public void AGroundedNonLaunchingHitMovesTheHeroAShortDistanceAndPlantsIt() {
        (float distance, int stop, int ticks) = Measure("einstein", 2f);
        AssertThat(stop >= 0 && stop < ticks)
            .OverrideFailureMessage($"The hero must be at rest before the stun ends (stop {stop}, stun {ticks}).")
            .IsTrue();
        AssertThat(distance > 5f && distance < 36f)
            .OverrideFailureMessage($"A light mob hit should shove well under a unit, moved {distance:0.0} px.")
            .IsTrue();
        AssertThat(stop <= 20)
            .OverrideFailureMessage($"The shove should stop within about 15 ticks, stopped at {stop}.")
            .IsTrue();
    }

    [TestCase]
    public void AHeavierHitShovesFartherThanALightOne() {
        float light = Measure("einstein", 2f).Distance;
        float heavy = Measure("einstein", 5f).Distance;
        AssertThat(heavy > light * 1.5f)
            .OverrideFailureMessage($"The shove stays proportional to the hit: light {light:0.0}, heavy {heavy:0.0}.")
            .IsTrue();
    }

    [TestCase]
    public void ALaunchingHitStillFliesAndTumbles() {
        Run("einstein", player => {
            player.GetNode<Hurtbox>("Hurtbox").TakeHit(MobHit(2f, launches: true));
            AssertThat(player.IsInTumble).IsTrue();
            float speed = Math.Abs(player.Velocity.X);
            Step(player);
            AssertThat(Math.Abs(player.Velocity.X) >= speed * 0.95f)
                .OverrideFailureMessage($"A launch keeps its horizontal speed ({speed:0} -> {player.Velocity.X:0}).")
                .IsTrue();
        });
    }

    [TestCase]
    public void AnAirborneNonLaunchingHitKeepsItsVector() {
        Run("einstein", player => {
            player.GlobalPosition += new Vector2(0f, -300f);
            player.Velocity = Vector2.Zero;
            Step(player);
            AssertThat(player.IsOnFloor()).IsFalse();
            player.GetNode<Hurtbox>("Hurtbox").TakeHit(MobHit(2f));
            float vx = player.Velocity.X;
            float expected = 2f / 2f * 60f; // einstein weight 1.0, not scaled by the shove
            AssertThat(Math.Abs(vx - expected) < 0.5f)
                .OverrideFailureMessage($"An airborne hit is not scaled by the shove: {vx:0.0} vs {expected:0.0}.")
                .IsTrue();
            Step(player);
            AssertThat(Math.Abs(player.Velocity.X - vx) < 0.5f)
                .OverrideFailureMessage("An airborne non-launched hero is not decelerated by the shove.")
                .IsTrue();
        });
    }

    [TestCase]
    public void TheGuardBreakPushIsUnchangedAndKeepsItsConstantSpeed() {
        Run("einstein", player => {
            var block = player.GetNode<BlockSystem>("BlockSystem");
            AssertThat(block.DepleteCharges(block.MaxCharges)).IsEqual(BlockResult.GuardBroken);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Dazed);
            Vector2 push = BlockSystem.GuardBreakPushVelocity(
                player.GlobalPosition.X, player.GlobalPosition.X, player.IsFacingRight);
            AssertThat(Math.Abs(push.X)).IsEqual(120f);
            AssertThat(player.Velocity.X).IsEqual(push.X);
            for (int i = 0; i < 5; i++) {
                float before = Math.Abs(player.Velocity.X);
                Step(player);
                if (player.CurrentState != CharacterState.Dazed) break;
                AssertThat(Math.Abs(player.Velocity.X) >= before - 0.01f)
                    .OverrideFailureMessage("The daze push is frictionless and must not decay.")
                    .IsTrue();
            }
        });
    }

    private static void Run(string hero, Action<PlayerController> body) {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var floor = new StaticBody2D {
            Name = "StoryShoveFloor", CollisionLayer = CollisionLayers.Environment, CollisionMask = 0
        };
        floor.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(8000f, 40f) },
            Position = new Vector2(0f, 20f)
        });
        tree.Root.AddChild(floor);
        PlayerController player = CharacterFactory.CreateCharacter(hero, applyStoryProgression: false);
        tree.Root.AddChild(player);
        try {
            player.GlobalPosition = new Vector2(0f, -8f);
            for (int frame = 0; frame < 120 && !player.IsOnFloor(); frame++) {
                player.Velocity = new Vector2(player.Velocity.X, 400f);
                Step(player);
            }
            for (int frame = 0; frame < 60 && player.CurrentState != CharacterState.Idle; frame++) Step(player);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Idle);
            body(player);
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
            floor.Free();
        }
    }

    private static void Step(PlayerController player) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.None));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        player._PhysicsProcess(1.0 / 60.0);
    }
}
