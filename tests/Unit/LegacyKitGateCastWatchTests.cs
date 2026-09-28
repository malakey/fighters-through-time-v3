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
/// A hero ability node whose execution the test controls directly. The gate reads
/// only <see cref="BaseSpecial.Data"/> and <see cref="BaseSpecial.IsExecuting"/>, so
/// this is the whole surface CastWatch and the owned-construct option consult.
/// </summary>
public partial class GateCastProbeSpecial : BaseSpecial {
    public void ForceExecuting(bool executing) =>
        CurrentPhase = executing ? AbilityPhase.Active : AbilityPhase.Inactive;
    protected override void OnStartup() { }
    protected override void OnActive() { }
    protected override void OnRecovery() { }
}

/// <summary>
/// Package 12 W8 — the fifth <see cref="LegacyGateMode"/> (<c>CastWatch</c>) and the
/// Zone gate's owned-construct option, which retire Package 11's
/// <c>LegacyCastGateWatcher</c> and <c>VineSnareGateResolver</c> stand-ins. V01c is
/// the rule under test: a 4A gate answers its one authored ability, cast inside its
/// own area, and nothing else — and (W1) never while the world is suspended.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class LegacyKitGateCastWatchTests {

    private const string RequiredID = "w8_probe_required_ability";
    private const string OtherID = "w8_probe_other_ability";

    [TestCase]
    public void TheFifthModeIsAppendedAndCarriesItsOwnLocalizedPrompt() {
        // Append-only enum: the four shipped ordinals never move.
        AssertThat((int)LegacyGateMode.Strike).IsEqual(0);
        AssertThat((int)LegacyGateMode.Zone).IsEqual(1);
        AssertThat((int)LegacyGateMode.Traversal).IsEqual(2);
        AssertThat((int)LegacyGateMode.Nexus).IsEqual(3);
        AssertThat((int)LegacyGateMode.CastWatch).IsEqual(4);

        string key = LegacyKitGate.PromptKeyFor(LegacyGateMode.CastWatch);
        AssertString(key).IsEqual("legacy_gate_cast_prompt");
        AssertString(TranslationServer.Translate(key).ToString())
            .OverrideFailureMessage("The CastWatch prompt does not resolve through the compiled translation.")
            .IsNotEqual(key);
    }

    [TestCase]
    public void ACastWatchGateLatchesOnlyOnItsOwnAbilityCastInsideItsArea() {
        using var fixture = new GateFixture(LegacyGateMode.CastWatch);
        LegacyKitGate gate = fixture.Gate;
        int resolvedEvents = 0;
        gate.Resolved += _ => resolvedEvents++;

        AssertThat(gate.TryRecognizeCast(OtherID, gate.GlobalPosition, worldSuspended: false))
            .OverrideFailureMessage("Another ability opened a CastWatch gate.").IsFalse();
        AssertThat(gate.TryRecognizeCast("combo_1", gate.GlobalPosition, worldSuspended: false)).IsFalse();
        AssertThat(gate.TryRecognizeCast(RequiredID,
            gate.GlobalPosition + new Vector2(gate.CastWatchRadius + 1f, 0f), worldSuspended: false))
            .OverrideFailureMessage("A cast outside the gate's area opened it.").IsFalse();
        AssertThat(gate.IsResolved).IsFalse();

        AssertThat(gate.TryRecognizeCast(RequiredID,
            gate.GlobalPosition + new Vector2(gate.CastWatchRadius - 1f, 0f), worldSuspended: false)).IsTrue();
        AssertThat(gate.IsResolved).IsTrue();

        // A latch, not a trigger: a second qualifying cast changes nothing.
        AssertThat(gate.TryRecognizeCast(RequiredID, gate.GlobalPosition, worldSuspended: false)).IsFalse();
        AssertThat(resolvedEvents).IsEqual(1);
    }

    [TestCase]
    public void OnlyACastWatchGateAnswersACast() {
        using var fixture = new GateFixture(LegacyGateMode.Strike);
        AssertThat(fixture.Gate.TryRecognizeCast(RequiredID, fixture.Gate.GlobalPosition, worldSuspended: false))
            .OverrideFailureMessage("A Strike gate latched on a cast it should never watch.").IsFalse();
        AssertThat(fixture.Gate.IsResolved).IsFalse();
    }

    [TestCase]
    public void ACastWatchGateRefusesWhileTheWorldIsSuspended() {
        using var fixture = new GateFixture(LegacyGateMode.CastWatch, withPlayer: true);
        LegacyKitGate gate = fixture.Gate;

        AssertThat(gate.TryRecognizeCast(RequiredID, gate.GlobalPosition, worldSuspended: true))
            .OverrideFailureMessage("A CastWatch gate latched during a world suspension.").IsFalse();
        AssertThat(gate.IsResolved).IsFalse();

        // The suspension query reads the same facts InteractionArea refuses on.
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        AssertThat(LegacyKitGate.IsWorldSuspended(fixture.Player, tree)).IsFalse();
        fixture.Player.TimeFrozen = true;
        AssertThat(LegacyKitGate.IsWorldSuspended(fixture.Player, tree)).IsTrue();
        fixture.Player.TimeFrozen = false;
    }

    [TestCase]
    public void ThePollReadsTheHerosOwnExecutingSpecialByAbilityID() {
        using var fixture = new GateFixture(LegacyGateMode.CastWatch, withPlayer: true);
        LegacyKitGate gate = fixture.Gate;
        GateCastProbeSpecial required = fixture.AddSpecial(RequiredID);
        GateCastProbeSpecial other = fixture.AddSpecial(OtherID);
        fixture.Player.GlobalPosition = gate.GlobalPosition;

        // Nothing executing: nothing latches.
        gate._PhysicsProcess(1.0 / 60.0);
        AssertThat(gate.IsResolved).IsFalse();

        // The wrong ability executing in-area: refused.
        other.ForceExecuting(true);
        gate._PhysicsProcess(1.0 / 60.0);
        AssertThat(gate.IsResolved).OverrideFailureMessage("Another special opened the gate.").IsFalse();
        other.ForceExecuting(false);

        // The right ability, but cast from outside the area: refused.
        required.ForceExecuting(true);
        fixture.Player.GlobalPosition = gate.GlobalPosition + new Vector2(gate.CastWatchRadius + 200f, 0f);
        gate._PhysicsProcess(1.0 / 60.0);
        AssertThat(gate.IsResolved).OverrideFailureMessage("An out-of-area cast opened the gate.").IsFalse();

        // In-area, but the world is frozen: refused.
        fixture.Player.GlobalPosition = gate.GlobalPosition;
        fixture.Player.TimeFrozen = true;
        gate._PhysicsProcess(1.0 / 60.0);
        AssertThat(gate.IsResolved).OverrideFailureMessage("The gate latched during Time Freeze.").IsFalse();

        // In-area and live: latched.
        fixture.Player.TimeFrozen = false;
        gate._PhysicsProcess(1.0 / 60.0);
        AssertThat(gate.IsResolved).IsTrue();
    }

    [TestCase]
    public void TheZoneOwnedConstructOptionAcceptsOnlyALiveConstructOfTheAuthoredAbility() {
        using var fixture = new GateFixture(LegacyGateMode.Zone);
        LegacyKitGate gate = fixture.Gate;
        var requiredScene = new PackedScene();
        var otherScene = new PackedScene();
        PooledNode construct = fixture.AddConstruct(requiredScene, gate.GlobalPosition);
        PooledNode foreign = fixture.AddConstruct(otherScene, gate.GlobalPosition);

        // Off by default: an ordinary Zone gate never reads constructs.
        AssertThat(gate.ZoneAcceptsOwnedConstruct).IsFalse();
        AssertThat(gate.TryRecognizeConstruct(construct, requiredScene, worldSuspended: false)).IsFalse();

        gate.ZoneAcceptsOwnedConstruct = true;
        AssertThat(gate.TryRecognizeConstruct(foreign, requiredScene, worldSuspended: false))
            .OverrideFailureMessage("A construct from another ability opened the gate.").IsFalse();
        AssertThat(gate.TryRecognizeConstruct(construct, requiredScene, worldSuspended: true))
            .OverrideFailureMessage("A construct opened the gate during a world suspension.").IsFalse();
        construct.GlobalPosition = gate.GlobalPosition + new Vector2(gate.ResolveRadius + 1f, 0f);
        AssertThat(gate.TryRecognizeConstruct(construct, requiredScene, worldSuspended: false))
            .OverrideFailureMessage("A construct outside the gate's radius opened it.").IsFalse();
        AssertThat(gate.IsResolved).IsFalse();

        construct.GlobalPosition = gate.GlobalPosition;
        AssertThat(gate.TryRecognizeConstruct(construct, requiredScene, worldSuspended: false)).IsTrue();
        AssertThat(gate.IsResolved).IsTrue();
    }

    [TestCase]
    public void TheZonePollFindsTheHerosOwnConstructThroughTheAuthoredAbilityScene() {
        using var fixture = new GateFixture(LegacyGateMode.Zone, withPlayer: true);
        LegacyKitGate gate = fixture.Gate;
        gate.ZoneAcceptsOwnedConstruct = true;
        var requiredScene = new PackedScene();
        fixture.AddSpecial(RequiredID, requiredScene);
        PooledNode construct = fixture.AddConstruct(requiredScene, gate.GlobalPosition);

        // Deployed but not yet the hero's (not in ActivePersistentObjects): nothing.
        gate._PhysicsProcess(1.0 / 60.0);
        AssertThat(gate.IsResolved).IsFalse();

        fixture.Player.ActivePersistentObjects.Add(construct);
        fixture.Player.TimeFrozen = true;
        gate._PhysicsProcess(1.0 / 60.0);
        AssertThat(gate.IsResolved).OverrideFailureMessage("The construct opened the gate during Time Freeze.").IsFalse();

        fixture.Player.TimeFrozen = false;
        gate._PhysicsProcess(1.0 / 60.0);
        AssertThat(gate.IsResolved).IsTrue();
    }

    private sealed class GateFixture : IDisposable {
        public readonly LegacyKitGate Gate;
        public readonly PlayerController Player;
        private readonly Node2D _constructs;

        public GateFixture(LegacyGateMode mode, bool withPlayer = false) {
            Node root = ((SceneTree)Engine.GetMainLoop()).Root;
            Gate = new LegacyKitGate {
                Name = "W8ProbeGate",
                GateID = "w8_probe_gate",
                RequiredAbilityID = RequiredID,
                Mode = mode,
                Position = new Vector2(12000f, -4000f)
            };
            root.AddChild(Gate);
            _constructs = new Node2D { Name = "W8ProbeConstructs" };
            root.AddChild(_constructs);
            if (withPlayer) {
                Player = CharacterFactory.CreateCharacter("einstein");
                Player.ProcessMode = Node.ProcessModeEnum.Disabled;
                root.AddChild(Player);
            }
        }

        public GateCastProbeSpecial AddSpecial(string abilityID, PackedScene persistentScene = null) {
            var special = new GateCastProbeSpecial {
                Name = $"Probe_{abilityID}",
                Data = new AbilityData { AbilityID = abilityID, PersistentObjectScene = persistentScene },
                ProcessMode = Node.ProcessModeEnum.Disabled
            };
            Player.AddChild(special);
            return special;
        }

        public PooledNode AddConstruct(PackedScene origin, Vector2 position) {
            var construct = new PooledNode { Name = $"Construct{_constructs.GetChildCount()}", SceneOrigin = origin };
            _constructs.AddChild(construct);
            construct.GlobalPosition = position;
            return construct;
        }

        public void Dispose() {
            if (Player != null && GodotObject.IsInstanceValid(Player)) {
                Player.ActivePersistentObjects.Clear();
                Player.TimeFrozen = false;
                Player.Free();
            }
            if (GodotObject.IsInstanceValid(_constructs)) _constructs.Free();
            if (GodotObject.IsInstanceValid(Gate)) Gate.Free();
        }
    }
}
