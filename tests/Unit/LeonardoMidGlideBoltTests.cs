using FTT.Characters;
using FTT.Characters.Abilities;
using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 12 W4 — Leonardo's movement-rule kit copy ("Ornithopter glide can
/// fire one turret bolt mid-flight if a turret is deployed") and M03 (the
/// turret's bolt budget is 4, 5 with Clockwork Overdrive). A commanded bolt is
/// the turret's own next bolt, out of its own budget; a command that cannot fire
/// spends nothing.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class LeonardoMidGlideBoltTests {
    private const double Tick = 1.0 / 60.0;

    [TestCase]
    public void TheTurretCarriesFourBoltsAndFiveWithClockworkOverdrive() {
        AbilityData data = AuthoredResources.Load<AbilityData>("res://resources/Abilities/leonardo/special_2.tres");
        AssertThat(data.HitCount).IsEqual(4);
        var turret = new LeonardoTurretNode();
        try {
            turret.Initialize(data, owner: null, clockworkOverdrive: false);
            AssertThat(turret.BoltsRemaining).IsEqual(4);
            turret.Initialize(data, owner: null, clockworkOverdrive: true);
            AssertThat(turret.BoltsRemaining).IsEqual(5);
            // The data-less fallback is 4 as well (M03 cleanup).
            turret.Initialize(null, owner: null, clockworkOverdrive: false);
            AssertThat(turret.BoltsRemaining).IsEqual(4);
        } finally {
            turret.Free();
        }
    }

    [TestCase]
    public void AMidGlideCommandNeedsAGlideAndATurretAndSpendsNothingWhenRefused() {
        PlayerController player = CharacterFactory.CreateCharacter("leonardo");
        var host = new Node { Name = "LeonardoGlideHost" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        host.AddChild(player);
        var turret = new LeonardoTurretNode();
        try {
            var flight = player.GetNode<LeonardoOrnithopterFlight>("MovementAbility");
            // Not gliding: refused.
            AssertThat(flight.TryFireMidGlideBolt()).IsFalse();

            player.TransitionTo(CharacterState.Airborne);
            var source = new BufferedInputSource();
            source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.MovementAbility));
            InputManager.Instance.SetInputSource(player.PlayerIndex, source);
            player._PhysicsProcess(Tick);
            for (int frame = 0; frame < flight.Data.StartupFrames + flight.Data.ActiveFrames + 1; frame++) {
                flight._PhysicsProcess(Tick);
            }

            // Gliding, but no turret deployed: refused, nothing spent.
            AssertThat(flight.TryFireMidGlideBolt()).IsFalse();
            AssertThat(flight.MidGlideBoltSpent).IsFalse();

            // A deployed turret with nothing in range cannot fire: still nothing
            // spent, neither the flight's one command nor a turret bolt.
            AbilityData data = AuthoredResources.Load<AbilityData>("res://resources/Abilities/leonardo/special_2.tres");
            host.AddChild(turret);
            turret.Initialize(data, player, clockworkOverdrive: false);
            player.ActivePersistentObjects.Add(turret);
            AssertThat(flight.TryFireMidGlideBolt()).IsFalse();
            AssertThat(flight.MidGlideBoltSpent).IsFalse();
            AssertThat(turret.BoltsRemaining).IsEqual(4);
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.ActivePersistentObjects.Remove(turret);
            if (GodotObject.IsInstanceValid(turret)) {
                turret.GetParent()?.RemoveChild(turret);
                turret.Free();
            }
            host.GetParent()?.RemoveChild(host);
            host.Free();
        }
    }
}
