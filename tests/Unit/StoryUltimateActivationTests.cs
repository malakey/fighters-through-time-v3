using System;
using System.Threading.Tasks;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 13 W6 — A02 on the Story side. Story keeps its screen-clearing
/// Ultimates: a player's press casts at once. Only a controller flagged with
/// <see cref="PlayerController.UsesFighterUltimateActivation"/> — the Mirror
/// Paradox clone, "a boss or CPU using a player kit against the player" — plays
/// the Fighter version: the meter is spent on acceptance, a hyper-armored
/// wind-up and the activation strike follow, contact starts the real Ultimate,
/// and a whiff ends in whiff recovery with the meter still spent.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryUltimateActivationTests {

    [TestCase]
    public void APlayersUltimateStillCastsAtOnce() {
        Run(caster => {
            caster.AddInfluenceFromDamageDealt(UltimateMeter.MaxValue, collectsEcho: false);
            Step(caster, GameplayButtons.Ultimate);
            AssertThat(caster.CurrentState).IsEqual(CharacterState.UsingUltimate);
            AssertThat(caster.UltimateActivationPhase).IsEqual(0);
            AssertThat(caster.GetNode<BaseSpecial>("Ultimate").IsExecuting).IsTrue();
        }, playerIndex: 0, fighterVersion: false);
    }

    [TestCase]
    public void TheFighterVersionSpendsTheMeterAndWhiffsIntoRecovery() {
        Run(caster => {
            caster.AddInfluenceFromDamageDealt(UltimateMeter.MaxValue, collectsEcho: false);
            Step(caster, GameplayButtons.Ultimate);
            BaseSpecial ultimate = caster.GetNode<BaseSpecial>("Ultimate");
            AbilityData data = ultimate.Data;

            AssertThat(caster.CurrentState).IsEqual(CharacterState.UsingUltimate);
            AssertThat(caster.UltimateActivationPhase).IsEqual(1);
            AssertThat(caster.CurrentUltimateMeter).IsEqual(0f);
            AssertThat(caster.StoryHyperArmorActive).IsTrue();
            AssertThat(ultimate.IsExecuting).IsFalse();

            // Nobody in reach: wind-up, active frames, then the whiff recovery.
            for (int frame = 0; frame < data.ActivationWindupFrames + data.ActivationActiveFrames; frame++) {
                Step(caster, GameplayButtons.None);
            }
            AssertThat(caster.UltimateActivationPhase).IsEqual(3);
            AssertThat(caster.CurrentState).IsEqual(CharacterState.UsingUltimate);
            // Recovery inputs are ignored — the whiff cannot be cancelled.
            for (int frame = 0; frame < data.ActivationWhiffRecoveryFrames - 2; frame++) {
                Step(caster, GameplayButtons.Roll | GameplayButtons.Jump);
                AssertThat(caster.CurrentState).IsEqual(CharacterState.UsingUltimate);
            }
            for (int frame = 0; frame < 4; frame++) Step(caster, GameplayButtons.None);
            AssertThat(caster.UltimateActivationPhase).IsEqual(0);
            AssertThat(caster.CurrentState).IsNotEqual(CharacterState.UsingUltimate);
            AssertThat(ultimate.IsExecuting).IsFalse();
            AssertThat(caster.CurrentUltimateMeter).IsEqual(0f);
        }, playerIndex: 1, fighterVersion: true);
    }

    [TestCase]
    public async Task TheFighterVersionsContactStartsTheRealUltimate() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        StaticBody2D floor = BuildFloor(tree);
        PlayerController caster = CharacterFactory.CreateCharacter(
            "einstein", 1, applyStoryProgression: false, applyLegacyUnlockLocks: false);
        PlayerController victim = CharacterFactory.CreateCharacter(
            "joan", 0, applyStoryProgression: false, applyLegacyUnlockLocks: false);
        caster.UsesFighterUltimateActivation = true;
        tree.Root.AddChild(caster);
        tree.Root.AddChild(victim);
        try {
            caster.GlobalPosition = new Vector2(0f, -8f);
            victim.GlobalPosition = new Vector2(120f, -8f);
            Settle(caster);
            Settle(victim);
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            caster.GlobalPosition = new Vector2(0f, caster.GlobalPosition.Y);
            victim.GlobalPosition = new Vector2(120f, victim.GlobalPosition.Y);
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);

            caster.AddInfluenceFromDamageDealt(UltimateMeter.MaxValue, collectsEcho: false);
            Step(caster, GameplayButtons.Ultimate);
            AssertThat(caster.UltimateActivationPhase).IsEqual(1);
            BaseSpecial ultimate = caster.GetNode<BaseSpecial>("Ultimate");
            bool executed = false;
            for (int frame = 0; frame < 40 && !executed; frame++) {
                Step(caster, GameplayButtons.None);
                executed = ultimate.IsExecuting;
            }
            AssertThat(executed).IsTrue();
            AssertThat(caster.UltimateActivationPhase).IsEqual(0);
            AssertThat(caster.CurrentState).IsEqual(CharacterState.UsingUltimate);
            // The lent meter was handed back: still spent.
            AssertThat(caster.CurrentUltimateMeter).IsEqual(0f);
        } finally {
            InputManager.Instance?.ClearInputSource(caster.PlayerIndex);
            InputManager.Instance?.ClearInputSource(victim.PlayerIndex);
            caster.Free();
            victim.Free();
            floor.Free();
        }
    }

    private static void Run(Action<PlayerController> body, int playerIndex, bool fighterVersion) {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        StaticBody2D floor = BuildFloor(tree);
        PlayerController player = CharacterFactory.CreateCharacter(
            "einstein", playerIndex, applyStoryProgression: false, applyLegacyUnlockLocks: false);
        player.UsesFighterUltimateActivation = fighterVersion;
        tree.Root.AddChild(player);
        try {
            player.GlobalPosition = new Vector2(0f, -8f);
            Settle(player);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Idle);
            body(player);
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
            floor.Free();
        }
    }

    private static StaticBody2D BuildFloor(SceneTree tree) {
        var floor = new StaticBody2D {
            Name = "W6Floor", CollisionLayer = CollisionLayers.Environment, CollisionMask = 0
        };
        floor.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(8000f, 40f) },
            Position = new Vector2(0f, 20f)
        });
        tree.Root.AddChild(floor);
        return floor;
    }

    private static void Settle(PlayerController player) {
        for (int frame = 0; frame < 120 && !player.IsOnFloor(); frame++) {
            player.Velocity = new Vector2(player.Velocity.X, 400f);
            Step(player, GameplayButtons.None);
        }
        for (int frame = 0; frame < 60 && player.CurrentState != CharacterState.Idle; frame++) {
            Step(player, GameplayButtons.None);
        }
    }

    private static void Step(PlayerController player, GameplayButtons buttons) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, buttons));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        player._PhysicsProcess(1.0 / 60.0);
    }
}
