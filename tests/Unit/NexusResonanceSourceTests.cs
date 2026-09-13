using System;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// F04 — the Nexus Resonance Source (V7.6, Package 11 A12). Level 4A's required
/// Ultimate set-piece authorizes the hero's real Ultimate from the nexus rather than
/// from the meter, and every clause of that authorization is a rule something else
/// could quietly break:
///
/// <list type="bullet">
///   <item>it arms only in a safe cleared area,</item>
///   <item>it casts at zero meter, and <b>neither fills nor consumes</b> the meter —
///     which is also why it can never light the F13 Defy seal,</item>
///   <item>leaving the cast area, death, reload or an interrupted cast clears the
///     arming, with unlimited free retries afterwards,</item>
///   <item>a solved gate disables the source,</item>
///   <item>it is <b>never saved armed</b>,</item>
///   <item>Time Freeze can neither arm it nor cast through it.</item>
/// </list>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class NexusResonanceSourceTests {

    private const string EinsteinUltimateID = "einstein_cosmological_constant";

    [TestCase]
    public void TheSourceArmsOnlyInASafeClearedAreaAndOnlyFromInsideIt() {
        using var fixture = new NexusFixture();

        // Contested (the approach encounter is still live): no arming.
        fixture.Source.CastAreaContested = true;
        AssertThat(fixture.Source.CanInteract(fixture.Player))
            .OverrideFailureMessage("A contested cast area must refuse to arm.").IsFalse();
        fixture.Source.Interact(fixture.Player);
        AssertThat(fixture.Source.IsArmed).IsFalse();

        // Cleared, but the player is outside the bounded cast area.
        fixture.Source.CastAreaContested = false;
        fixture.Player.GlobalPosition =
            fixture.Source.GlobalPosition + new Vector2(fixture.Source.CastAreaRadius + 80f, 0f);
        AssertThat(fixture.Source.CanInteract(fixture.Player)).IsFalse();

        // Cleared and inside: armed.
        fixture.Player.GlobalPosition = fixture.Source.GlobalPosition;
        AssertThat(fixture.Source.CanInteract(fixture.Player)).IsTrue();
        fixture.Source.Interact(fixture.Player);
        AssertThat(fixture.Source.IsArmed).IsTrue();
        AssertThat(fixture.Player.NexusUltimateSource).IsEqual(fixture.Source);
        AssertThat(fixture.Player.IsNexusUltimateAuthorized).IsTrue();
    }

    [TestCase]
    public void TheAuthorizedUltimateCastsAtZeroMeterAndNeitherFillsNorConsumesIt() {
        using var fixture = new NexusFixture();
        fixture.SetMeter(0f);
        fixture.Source.Interact(fixture.Player);
        AssertThat(fixture.Source.IsArmed).IsTrue();

        // The ordinary gate would refuse outright: every roster ultimate validates
        // on a full meter and consumes it in its own startup.
        AssertThat(fixture.Meter.IsFull).IsFalse();

        bool cast = fixture.PressUltimate();
        AssertThat(cast).OverrideFailureMessage(
            "F04: the normal Ultimate input must cast at ANY meter value, including zero.").IsTrue();
        AssertThat(fixture.Player.CurrentState).IsEqual(CharacterState.UsingUltimate);

        AssertThat(fixture.Meter.CurrentValue)
            .OverrideFailureMessage("A Nexus cast neither fills nor consumes the meter.")
            .IsEqual(0f);
        AssertThat(fixture.Player.CurrentUltimateMeter).IsEqual(0f);
        // A zero meter after the cast is exactly why the F13 Defy seal cannot light:
        // Defy needs a full meter at the moment of a lethal hit.
        AssertThat(fixture.Player.CurrentUltimateMeter < UltimateMeter.MaxValue).IsTrue();
    }

    [TestCase]
    public void APartialMeterIsRestoredExactlyAfterTheCast() {
        using var fixture = new NexusFixture();
        fixture.SetMeter(37f);
        fixture.Source.Interact(fixture.Player);

        AssertThat(fixture.PressUltimate()).IsTrue();
        AssertThat(fixture.Meter.CurrentValue)
            .OverrideFailureMessage("The meter must read exactly what it read before the cast.")
            .IsEqual(37f);
        AssertThat(fixture.Player.CurrentUltimateMeter).IsEqual(37f);
    }

    [TestCase]
    public void LeavingTheCastAreaOrGoingDownClearsTheArmingAndRetriesAreFreeAndUnlimited() {
        using var fixture = new NexusFixture();
        fixture.SetMeter(0f);

        // Walk out of the bounded area.
        fixture.Source.Interact(fixture.Player);
        AssertThat(fixture.Source.IsArmed).IsTrue();
        fixture.Player.GlobalPosition =
            fixture.Source.GlobalPosition + new Vector2(fixture.Source.CastAreaRadius + 120f, 0f);
        fixture.Source._PhysicsProcess(1.0 / 60.0);
        AssertThat(fixture.Source.IsArmed).IsFalse();
        AssertThat(fixture.Source.LastClearReason).IsEqual(NexusArmingClearReason.LeftCastArea);
        AssertThat(fixture.Player.NexusUltimateSource).IsNull();

        // Return: available again, no cost, no charge consumed.
        fixture.Player.GlobalPosition = fixture.Source.GlobalPosition;
        AssertThat(fixture.Source.IsAvailable).IsTrue();
        fixture.Source.Interact(fixture.Player);
        AssertThat(fixture.Source.IsArmed).IsTrue();

        // An interrupted cast clears it too, and again costs nothing.
        fixture.Source.NotifyCastInterrupted();
        AssertThat(fixture.Source.IsArmed).IsFalse();
        AssertThat(fixture.Source.LastClearReason).IsEqual(NexusArmingClearReason.CastInterrupted);

        // Retries are unlimited: a fourth, fifth and sixth arming all succeed.
        for (int attempt = 0; attempt < 3; attempt++) {
            fixture.Source.Interact(fixture.Player);
            AssertThat(fixture.Source.IsArmed).IsTrue();
            fixture.Source.NotifyCastInterrupted();
        }
        AssertThat(fixture.Source.ArmCount >= 5).IsTrue();
        AssertThat(fixture.Source.IsSolved).IsFalse();
        AssertThat(fixture.Meter.CurrentValue).IsEqual(0f);
    }

    [TestCase]
    public void ResolvingTheTargetLatchesTheGateAndDisablesTheSource() {
        using var fixture = new NexusFixture();
        fixture.SetMeter(0f);
        AssertThat(fixture.Gate.IsResolved).IsFalse();

        fixture.Source.Interact(fixture.Player);
        AssertThat(fixture.PressUltimate()).IsTrue();

        AssertThat(fixture.Source.IsSolved)
            .OverrideFailureMessage("A landed puzzle cast latches the gate.").IsTrue();
        AssertThat(fixture.Gate.IsResolved).IsTrue();
        AssertThat(fixture.Source.IsArmed).IsFalse();
        AssertThat(fixture.Source.IsAvailable)
            .OverrideFailureMessage("A solved gate keeps the source disabled.").IsFalse();
        AssertThat(fixture.Source.CanInteract(fixture.Player)).IsFalse();

        // A second arming attempt does nothing at all.
        fixture.Source.Interact(fixture.Player);
        AssertThat(fixture.Source.IsArmed).IsFalse();
    }

    [TestCase]
    public void TheSourceIsNeverRestoredArmedAndASolvedGateRestoresDisabled() {
        using var fixture = new NexusFixture();
        fixture.Source.Interact(fixture.Player);
        AssertThat(fixture.Source.IsArmed).IsTrue();

        // F10: an unsolved gate restores an AVAILABLE source, never an armed one.
        fixture.Source.RestoreFromCheckpoint(gateSolved: false);
        AssertThat(fixture.Source.IsArmed)
            .OverrideFailureMessage("The Nexus source is never saved or restored armed.").IsFalse();
        AssertThat(fixture.Source.IsSolved).IsFalse();
        AssertThat(fixture.Source.IsAvailable).IsTrue();
        AssertThat(fixture.Player.NexusUltimateSource).IsNull();
        AssertThat(fixture.Gate.IsResolved).IsFalse();

        // A solved gate restores disabled, with the gate already latched.
        fixture.Source.RestoreFromCheckpoint(gateSolved: true);
        AssertThat(fixture.Source.IsArmed).IsFalse();
        AssertThat(fixture.Source.IsSolved).IsTrue();
        AssertThat(fixture.Source.IsAvailable).IsFalse();
        AssertThat(fixture.Gate.IsResolved).IsTrue();
    }

    [TestCase]
    public void TimeFreezeCanNeitherArmTheSourceNorCastThroughAnArmedOne() {
        using var fixture = new NexusFixture();
        fixture.SetMeter(0f);

        // The world is suspended: the source refuses to arm at all.
        NexusResonanceSource.WorldTimeSuspendedProbe = static () => true;
        try {
            AssertThat(fixture.Source.IsAvailable).IsFalse();
            AssertThat(fixture.Source.CanInteract(fixture.Player)).IsFalse();
            fixture.Source.Interact(fixture.Player);
            AssertThat(fixture.Source.IsArmed).IsFalse();
        } finally {
            NexusResonanceSource.WorldTimeSuspendedProbe = static () => false;
        }

        // Armed first, then the world is suspended: the authorization drops, so the
        // Ultimate input falls back to the ordinary (here: empty) meter gate.
        fixture.Source.Interact(fixture.Player);
        AssertThat(fixture.Source.IsArmed).IsTrue();
        NexusResonanceSource.WorldTimeSuspendedProbe = static () => true;
        try {
            AssertThat(fixture.Player.IsNexusUltimateAuthorized)
                .OverrideFailureMessage("Time Freeze cannot cast through an armed source.").IsFalse();
            fixture.Source._PhysicsProcess(1.0 / 60.0);
            AssertThat(fixture.Source.IsArmed).IsFalse();
            AssertThat(fixture.Source.LastClearReason).IsEqual(NexusArmingClearReason.WorldSuspended);
        } finally {
            NexusResonanceSource.WorldTimeSuspendedProbe = static () => false;
        }
        AssertThat(fixture.Source.IsSolved).IsFalse();
    }

    [TestCase]
    public void AnArmedSourceOnlyAuthorizesItsOwnAuthoredUltimate() {
        using var fixture = new NexusFixture();
        fixture.Source.Interact(fixture.Player);
        AssertThat(fixture.Source.AuthorizesUltimate(EinsteinUltimateID)).IsTrue();
        AssertThat(fixture.Source.AuthorizesUltimate("joan_grand_crusade"))
            .OverrideFailureMessage("A 4A gate accepts its own authored ability and nothing else (V01c).")
            .IsFalse();
    }

    // === Fixture ===

    /// <summary>
    /// A player, a Nexus gate and its source, with every shared singleton the
    /// interception touches restored on dispose.
    /// </summary>
    private sealed class NexusFixture : IDisposable {
        public readonly PlayerController Player;
        public readonly NexusResonanceSource Source;
        public readonly LegacyKitGate Gate;
        public readonly UltimateMeter Meter;
        private readonly StaticBody2D _floor;

        public NexusFixture() {
            NexusResonanceSource.WorldTimeSuspendedProbe = static () => false;

            // The cast area is a safe standing area in the level; give the harness a
            // floor so the player is grounded and idle rather than falling out of the
            // cast radius while frames are pumped.
            _floor = new StaticBody2D {
                Name = "NexusFixtureFloor",
                CollisionLayer = CollisionLayers.Environment,
                CollisionMask = 0
            };
            _floor.AddChild(new CollisionShape2D {
                Shape = new RectangleShape2D { Size = new Vector2(4000f, 40f) },
                Position = new Vector2(0f, 20f)
            });
            ((SceneTree)Engine.GetMainLoop()).Root.AddChild(_floor);

            Player = CharacterFactory.CreateCharacter("einstein");
            ((SceneTree)Engine.GetMainLoop()).Root.AddChild(Player);
            Meter = Player.GetNode<UltimateMeter>("UltimateMeter");

            Gate = new LegacyKitGate {
                Name = "NexusGate",
                GateID = "test_nexus_gate",
                RequiredAbilityID = EinsteinUltimateID,
                Mode = LegacyGateMode.Nexus
            };
            ((SceneTree)Engine.GetMainLoop()).Root.AddChild(Gate);

            Source = new NexusResonanceSource {
                Name = "TestNexusSource",
                SourceID = "test_nexus_source",
                RequiredUltimateAbilityID = EinsteinUltimateID,
                Gate = Gate
            };
            ((SceneTree)Engine.GetMainLoop()).Root.AddChild(Source);

            Player.GlobalPosition = Source.GlobalPosition;
        }

        public void SetMeter(float value) {
            Meter.SetValue(value);
            Player.CurrentUltimateMeter = value;
        }

        /// <summary>
        /// Drives an Ultimate press through the real input path. Pumps a few frames
        /// rather than exactly one: a freshly constructed player spends its first
        /// frames settling onto the floor, and the state handler that reads the
        /// Ultimate input is only reached once it has. `BufferedInputSource` replays
        /// the same frame, so the press stays live across the pump.
        /// </summary>
        public bool PressUltimate() {
            var source = new BufferedInputSource();
            source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.Ultimate));
            InputManager.Instance.SetInputSource(Player.PlayerIndex, source);
            for (int frame = 0; frame < 12; frame++) {
                Player._PhysicsProcess(1.0 / 60.0);
                if (Player.CurrentState == CharacterState.UsingUltimate) return true;
            }
            return false;
        }

        public void Dispose() {
            NexusResonanceSource.WorldTimeSuspendedProbe = static () => false;
            InputManager.Instance?.ClearInputSource(Player.PlayerIndex);
            Player.NexusUltimateSource = null;
            if (GodotObject.IsInstanceValid(Source)) Source.Free();
            if (GodotObject.IsInstanceValid(Gate)) Gate.Free();
            if (GodotObject.IsInstanceValid(Player)) Player.Free();
            if (GodotObject.IsInstanceValid(_floor)) _floor.Free();
        }
    }
}
