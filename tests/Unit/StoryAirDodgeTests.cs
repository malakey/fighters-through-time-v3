using System;
using FTT.Characters;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 13 W1 — A03/A05 (Story half). Roll pressed while airborne is the air
/// dodge — an <c>AirDodge</c> sub-phase of Rolling reusing the roll animation:
/// 4 startup / 8 invulnerable / 10 recovery, once per airtime, refreshed by
/// landing, refused under Root. The Fighter half is
/// <c>tests/Determinism/FighterAirDodgeTests.cs</c>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryAirDodgeTests {

    [TestCase]
    public void AnAirborneRollIsAnEightFrameInvulnerableAirDodgeOncePerAirtime() {
        Run(player => {
            Step(player, GameplayButtons.Jump);
            for (int frame = 0; frame < 4; frame++) Step(player, GameplayButtons.None);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Airborne);

            Step(player, GameplayButtons.Roll);
            AssertThat(player.IsAirDodging).IsTrue();
            AssertThat(player.AirDodgeUsedThisAirtime).IsTrue();

            int invulnerable = player.IsRollInvulnerable ? 1 : 0;
            // The press frame only enters Rolling; the 22 dodge frames follow.
            for (int frame = 1; frame <= UniversalMovementRules.AirDodgeTotalFrames; frame++) {
                Step(player, GameplayButtons.None);
                if (player.IsRollInvulnerable) invulnerable++;
            }
            AssertThat(invulnerable)
                .OverrideFailureMessage("The air dodge carries exactly its 8 invulnerable frames.")
                .IsEqual(UniversalMovementRules.AirDodgeInvulnerableFrames);
            AssertThat(player.IsAirDodging).IsFalse();

            if (!player.IsOnFloor()) {
                Step(player, GameplayButtons.Roll);
                AssertThat(player.CurrentState)
                    .OverrideFailureMessage("A second air dodge in the same airtime is refused.")
                    .IsNotEqual(CharacterState.Rolling);
            }
            for (int frame = 0; frame < 180 && !player.IsOnFloor(); frame++) Step(player, GameplayButtons.None);
            Step(player, GameplayButtons.None);
            AssertThat(player.AirDodgeUsedThisAirtime)
                .OverrideFailureMessage("Landing refreshes the air dodge.")
                .IsFalse();
        });
    }

    [TestCase]
    public void RootRefusesTheAirDodge() {
        Run(player => {
            Step(player, GameplayButtons.Jump);
            for (int frame = 0; frame < 4; frame++) Step(player, GameplayButtons.None);
            player.ApplyStatusEffect(StatusType.Root, 1.0f, 1f);
            // The status controller mirrors Root onto this flag on its own tick.
            player.IsMovementRooted = true;
            Step(player, GameplayButtons.Roll);
            AssertThat(player.IsAirDodging)
                .OverrideFailureMessage("A05: Root is a true immobilize — no air dodge.")
                .IsFalse();
            AssertThat(player.AirDodgeUsedThisAirtime).IsFalse();
        });
    }

    private static void Run(Action<PlayerController> body) {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var floor = new StaticBody2D {
            Name = "AirDodgeFloor", CollisionLayer = CollisionLayers.Environment, CollisionMask = 0
        };
        floor.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(8000f, 40f) },
            Position = new Vector2(0f, 20f)
        });
        tree.Root.AddChild(floor);
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(player);
        try {
            player.GlobalPosition = new Vector2(0f, -8f);
            for (int frame = 0; frame < 120 && !player.IsOnFloor(); frame++) {
                player.Velocity = new Vector2(player.Velocity.X, 400f);
                Step(player, GameplayButtons.None);
            }
            for (int frame = 0; frame < 60 && player.CurrentState != CharacterState.Idle; frame++) {
                Step(player, GameplayButtons.None);
            }
            AssertThat(player.CurrentState).IsEqual(CharacterState.Idle);
            body(player);
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
            floor.Free();
        }
    }

    private static void Step(PlayerController player, GameplayButtons buttons) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, buttons));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        player._PhysicsProcess(1.0 / 60.0);
    }
}
