using System;
using FTT.Characters;
using FTT.Characters.Abilities;
using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Combat and movement update 2026-10-16 (plan §2.1.6, CM-SIM commit 6): the
/// Story probe the Fighter Special phase machine is built against. It records
/// two Story behaviours the sim must match or deliberately record:
/// <list type="bullet">
/// <item>the tick a projectile Special's effect appears, counted from the
/// press tick T (Einstein's E=mc², authored 14 / 6 / 20);</item>
/// <item>the cast's horizontal motion, for a running grounded caster and an
/// airborne one.</item>
/// </list>
/// Manual ticks in the tree order: the controller, then its four ability
/// nodes, the order the engine processes them in. Each case prints what it
/// measured (run with a detailed console logger to read it).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StorySpecialTimingProbeTests {
    private const double Step = 1.0 / 60.0;
    // Away from every other suite's geometry.
    private static readonly Vector2 Origin = new(-61500f, 1500f);
    private static readonly string[] AbilityNodes = { "Special1", "Special2", "MovementAbility", "Ultimate" };

    /// <summary>
    /// The press is read by the controller on T and the cast's own phase clock
    /// runs in the child ability node after it on the same tick, so Story's
    /// projectile appears on T+S−1, one tick before the sim's T+S convention.
    /// The offset is recorded under <c>VERIFY-SAME-TICK-VERB-PARITY</c>.
    /// </summary>
    [TestCase]
    public void StorysProjectileSpecialFiresOnTheTickBeforeTheSimsConvention() {
        Run("einstein", (player, host) => {
            var blast = player.GetNode<EinsteinEmc2Blast>("Special1");
            int startup = blast.Data.StartupFrames;
            AssertThat(startup).OverrideFailureMessage("Harness: E=mc² authors a 14-frame startup.").IsEqual(14);
            int effectTick = -1;
            int activeTick = -1;
            for (int tick = 0; tick <= startup + 4 && effectTick < 0; tick++) {
                Tick(player, tick == 0 ? GameplayButtons.Special1 : GameplayButtons.None);
                if (tick == 0) {
                    AssertThat(player.CurrentState)
                        .OverrideFailureMessage("Harness: Special 1 was refused.")
                        .IsEqual(CharacterState.UsingSpecial);
                }
                if (activeTick < 0 && blast.CurrentPhase == AbilityPhase.Active) activeTick = tick;
                if (LiveProjectiles(player) > 0) effectTick = tick;
            }
            Console.WriteLine($"[CM-SIM probe] Story E=mc² (S={startup}): Active on T+{activeTick}, projectile on T+{effectTick}.");
            AssertThat(effectTick)
                .OverrideFailureMessage($"Story's projectile appeared on T+{effectTick}; the plan's reading is T+S-1 = T+{startup - 1}.")
                .IsEqual(startup - 1);
            AssertThat(activeTick).IsEqual(effectTick);
        });
    }

    /// <summary>
    /// Story's cast motion before the Q1 ruling: <c>ProcessUsingSpecial</c>
    /// writes only Y, so a running grounded caster slides at its entry speed
    /// with no friction for the whole cast and an airborne caster keeps its
    /// drift. (Rewritten in place to the braking rule when it lands.)
    /// </summary>
    [TestCase]
    public void StorysCastMotionKeepsTheEntrySpeedGroundedAndAirborne() {
        Run("einstein", (player, host) => {
            var blast = player.GetNode<EinsteinEmc2Blast>("Special1");
            for (int tick = 0; tick < 20; tick++) Tick(player, GameplayButtons.None, horizontal: 1f);
            float entry = player.Velocity.X;
            AssertThat(entry > 100f).OverrideFailureMessage($"Harness: the hero never ran ({entry} px/s).").IsTrue();
            Tick(player, GameplayButtons.Special1, horizontal: 1f);
            AssertThat(player.CurrentState).IsEqual(CharacterState.UsingSpecial);
            float min = player.Velocity.X;
            float max = player.Velocity.X;
            string trace = $"{player.Velocity.X:0.#}";
            for (int tick = 1; tick < 12 && blast.IsExecuting; tick++) {
                Tick(player, GameplayButtons.None, horizontal: 1f);
                min = Mathf.Min(min, player.Velocity.X);
                max = Mathf.Max(max, player.Velocity.X);
                trace += $" {player.Velocity.X:0.#}";
            }
            Console.WriteLine($"[CM-SIM probe] Story grounded cast X velocity from entry {entry:0.#}: {trace}");
            AssertFloat(min).OverrideFailureMessage($"Grounded cast velocity dropped to {min}.").IsEqualApprox(entry, 1f);
            AssertFloat(max).IsEqualApprox(entry, 1f);
        });

        Run("einstein", (player, host) => {
            var blast = player.GetNode<EinsteinEmc2Blast>("Special1");
            for (int tick = 0; tick < 20; tick++) Tick(player, GameplayButtons.None, horizontal: 1f);
            Tick(player, GameplayButtons.Jump, horizontal: 1f);
            for (int tick = 0; tick < 3; tick++) {
                Tick(player, GameplayButtons.Jump, previous: GameplayButtons.Jump, horizontal: 1f);
            }
            AssertThat(player.IsOnFloor()).OverrideFailureMessage("Harness: the hero must be airborne.").IsFalse();
            float entry = player.Velocity.X;
            Tick(player, GameplayButtons.Special1, horizontal: 1f);
            AssertThat(player.CurrentState).IsEqual(CharacterState.UsingSpecial);
            string trace = $"{player.Velocity.X:0.#}";
            float drift = player.Velocity.X;
            for (int tick = 1; tick < 10 && blast.IsExecuting && !player.IsOnFloor(); tick++) {
                Tick(player, GameplayButtons.None, horizontal: -1f);
                trace += $" {player.Velocity.X:0.#}";
                AssertFloat(player.Velocity.X)
                    .OverrideFailureMessage($"The airborne cast's drift changed on tick {tick} ({trace}).")
                    .IsEqualApprox(drift, 1f);
            }
            Console.WriteLine($"[CM-SIM probe] Story airborne cast X velocity from entry {entry:0.#}: {trace}");
        });
    }

    // === Harness ===============================================================

    private static int LiveProjectiles(Node anchor) {
        int live = 0;
        Godot.Collections.Array<Node> nodes = anchor.GetTree().GetNodesInGroup("story_projectile");
        using var lifetime = nodes.AsDisposable();
        foreach (Node node in nodes) {
            if (node is PlaceholderProjectile projectile && projectile.IsLive) live++;
        }
        return live;
    }

    private static void Run(string heroID, Action<PlayerController, Node2D> body) {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        bool paused = tree.Paused;
        var host = new Node2D { Name = "SpecialTimingProbeHost" };
        tree.Root.AddChild(host);
        var pools = new PoolManager { Name = "SpecialTimingProbePools" };
        tree.Root.AddChild(pools);
        host.AddChild(Floor());
        PlayerController player = CharacterFactory.CreateCharacter(heroID, 0,
            applyStoryProgression: false, applyLegacyUnlockLocks: false);
        player.Position = Origin + new Vector2(0f, -8f);
        host.AddChild(player);
        try {
            Settle(player);
            body(player, host);
        } finally {
            InputManager.Instance?.ClearInputSource(0);
            pools.ClearAllPools();
            tree.Root.RemoveChild(pools);
            pools.Free();
            host.Free();
            tree.Paused = paused;
        }
    }

    private static StaticBody2D Floor() {
        var floor = new StaticBody2D {
            Name = "SpecialTimingProbeFloor",
            CollisionLayer = CollisionLayers.Environment,
            CollisionMask = 0,
            Position = Origin
        };
        floor.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(6000f, 40f) },
            Position = new Vector2(0f, 20f)
        });
        return floor;
    }

    private static void Settle(PlayerController player) {
        for (int frame = 0; frame < 120 && !player.IsOnFloor(); frame++) {
            player.Velocity = new Vector2(player.Velocity.X, 400f);
            Tick(player, GameplayButtons.None);
        }
        for (int frame = 0; frame < 60 && player.CurrentState != CharacterState.Idle; frame++) {
            Tick(player, GameplayButtons.None);
        }
        AssertThat(player.CurrentState)
            .OverrideFailureMessage($"setup: {player.CurrentState} onFloor={player.IsOnFloor()} pos={player.GlobalPosition}")
            .IsEqual(CharacterState.Idle);
    }

    /// <summary>One tick in the tree order: the controller, then its four ability nodes.</summary>
    private static void Tick(PlayerController player, GameplayButtons held,
        GameplayButtons previous = GameplayButtons.None, float horizontal = 0f) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, horizontal, 0f, held, previous));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        player._PhysicsProcess(Step);
        foreach (string name in AbilityNodes) {
            if (player.GetNodeOrNull<BaseSpecial>(name) is { } ability && GodotObject.IsInstanceValid(ability)) {
                ability._PhysicsProcess(Step);
            }
        }
    }
}
