using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Pins the V7.2 universal grab &amp; throws in the deterministic simulation:
/// the Block+BasicAttack chord, grab-beats-block (a blocking victim is seized
/// and the throw pays no shieldstun), the punishing whiff, the up throw's
/// vertical launch, and the anti-loop throw immunity. Numbers come from
/// <see cref="BasicComboRules"/> and <c>FighterGrabRules</c>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterGrabTests {

    [TestCase]
    public void GrabBeatsBlockAndTheThrowIsUnblockable() {
        var simulation = NewSimulation(seed: 971);
        int tick = WalkIntoGrabRange(simulation);

        // The victim turtles the whole way; the chord seizes them regardless.
        simulation.Advance(
            FrameChord(tick++, GameplayButtons.Block | GameplayButtons.BasicAttack, GameplayButtons.BasicAttack),
            FrameHeld(tick - 1, GameplayButtons.Block));
        for (int step = 0; step < BasicComboRules.GrabStartupFrames + 2; step++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), FrameHeld(tick, GameplayButtons.Block));
            tick++;
        }
        AssertThat(simulation.TryGetFighterVerb(1, out FighterVerbComponent held)).IsTrue();
        AssertThat(held.BeingHeld)
            .OverrideFailureMessage("The grab must seize a blocking victim — grab beats block.")
            .IsEqual(1);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent victimHeld)).IsTrue();
        int chargesBefore = victimHeld.BlockCharges;

        // Ride the decision window and throw animation out: the throw's
        // 1.0x BasicDamage lands as real HP (no charge is spent — unblockable)
        // and the victim leaves with throw immunity.
        for (int step = 0; step < BasicComboRules.ThrowDecisionFrames + BasicComboRules.ThrowAnimationFrames + 4; step++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), FrameHeld(tick, GameplayButtons.Block));
            tick++;
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent thrown)).IsTrue();
        AssertThat(thrown.CurrentHP < thrown.MaxHP)
            .OverrideFailureMessage("The throw's damage must land through the block.")
            .IsTrue();
        AssertThat(thrown.BlockCharges)
            .OverrideFailureMessage("An unblockable throw spends no shield charge.")
            .IsEqual(chargesBefore);
        AssertThat(simulation.TryGetFighterVerb(1, out FighterVerbComponent released)).IsTrue();
        AssertThat(released.BeingHeld).IsEqual(0);
        AssertThat(released.ThrowImmunityFrames > 0)
            .OverrideFailureMessage("The thrown victim leaves with the anti-regrab window.")
            .IsTrue();
        AssertThat(simulation.TryGetFighterVerb(0, out FighterVerbComponent grabber)).IsTrue();
        AssertThat(grabber.GrabPhase).IsEqual(0);
    }

    [TestCase]
    public void AGrabWhiffsAgainstAnAirborneVictimIntoTheLongRecovery() {
        var simulation = NewSimulation(seed: 972);
        int tick = WalkIntoGrabRange(simulation);

        // The victim jumps as the grab comes out: airborne targets whiff it.
        simulation.Advance(
            FrameChord(tick++, GameplayButtons.Block | GameplayButtons.BasicAttack, GameplayButtons.BasicAttack),
            Frame(tick - 1, 0, GameplayButtons.Jump));
        for (int step = 0; step < BasicComboRules.GrabStartupFrames + BasicComboRules.GrabActiveFrames + 1; step++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            tick++;
        }
        AssertThat(simulation.TryGetFighterVerb(1, out FighterVerbComponent untouched)).IsTrue();
        AssertThat(untouched.BeingHeld).IsEqual(0);
        AssertThat(simulation.TryGetFighterVerb(0, out FighterVerbComponent whiffed)).IsTrue();
        AssertThat(whiffed.GrabPhase)
            .OverrideFailureMessage("The empty active window must fall into whiff recovery.")
            .IsEqual(3);
        AssertThat(whiffed.GrabPhaseFrames > BasicComboRules.GrabClashBounceFrames)
            .OverrideFailureMessage("The whiff eats the long 24-frame recovery, not the clash bounce.")
            .IsTrue();
    }

    [TestCase]
    public void TheUpThrowLaunchesVerticallyAndTheForwardThrowHorizontally() {
        // Up throw: hold Up through the decision window.
        var upSim = NewSimulation(seed: 973);
        FighterStateComponent upVictim = RunThrow(upSim, moveX: 0, moveY: -127);
        AssertThat(upVictim.Velocity.y > FP64.Zero).IsTrue();
        AssertThat(FP64.Abs(upVictim.Velocity.y) > FP64.Abs(upVictim.Velocity.x))
            .OverrideFailureMessage(
                $"The up throw is the launcher — vertical must dominate "
                + $"(v=({upVictim.Velocity.x.ToFloat()}, {upVictim.Velocity.y.ToFloat()})).")
            .IsTrue();

        // Forward throw: neutral input resolves forward.
        var forwardSim = NewSimulation(seed: 973);
        FighterStateComponent forwardVictim = RunThrow(forwardSim, moveX: 0, moveY: 0);
        AssertThat(FP64.Abs(forwardVictim.Velocity.x) > FP64.Abs(forwardVictim.Velocity.y))
            .OverrideFailureMessage("The forward throw is the spacing tool — horizontal must dominate.")
            .IsTrue();
    }

    // ---- Harness -------------------------------------------------------------

    /// <summary>Grabs, holds with the given decision input, and returns the
    /// victim's state on the first tick after the launch.</summary>
    private static FighterStateComponent RunThrow(FighterSimulation simulation, sbyte moveX, sbyte moveY) {
        int tick = WalkIntoGrabRange(simulation);
        simulation.Advance(
            FrameChord(tick++, GameplayButtons.Block | GameplayButtons.BasicAttack, GameplayButtons.BasicAttack),
            Frame(tick - 1, 0, GameplayButtons.None));
        int total = BasicComboRules.GrabStartupFrames + BasicComboRules.GrabActiveFrames
            + BasicComboRules.ThrowDecisionFrames + BasicComboRules.ThrowAnimationFrames + 2;
        for (int step = 0; step < total; step++) {
            simulation.Advance(
                new PlayerInputFrame {
                    Tick = (uint)tick, MoveX = moveX, MoveY = moveY,
                    Held = GameplayButtons.None, Pressed = GameplayButtons.None
                },
                Frame(tick, 0, GameplayButtons.None));
            tick++;
            AssertThat(simulation.TryGetFighterVerb(1, out FighterVerbComponent verb)).IsTrue();
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent victim)).IsTrue();
            if (verb.ThrowImmunityFrames > 0) return victim;
        }
        AssertThat(false).OverrideFailureMessage("The throw never resolved.").IsTrue();
        return default;
    }

    /// <summary>Walks player one toward the victim until the pushboxes hold
    /// them at contact range (inside the 0.8-unit grab reach).</summary>
    private static int WalkIntoGrabRange(FighterSimulation simulation) {
        int tick = 0;
        for (; tick < 90; tick++) {
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent one)).IsTrue();
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent two)).IsTrue();
            if (FP64.Abs(two.Position.x - one.Position.x) <= FP64.FromDouble(BasicComboRules.GrabReachUnits)) {
                return tick;
            }
            simulation.Advance(Frame(tick, 127, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(false).OverrideFailureMessage("Never reached grab range.").IsTrue();
        return tick;
    }

    private static FighterSimulation NewSimulation(int seed) => new(
        FighterLoadoutFactory.FromCharacterData(BuildGrappler()),
        FighterLoadoutFactory.FromCharacterData(BuildGrappler()),
        seed: seed,
        spawnDistance: 1,
        rules: FighterMatchRules.Disabled);

    private static CharacterData BuildGrappler() => new() {
        CharacterID = "tesla",
        MaxHP = 100,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 1,
        MaxMoveSpeed = 8f,
        MaxJumpForce = 13f,
        BasicAttackDamage = 10f,
        BasicAttackKnockback = 3f,
        SpecialAttackOne = new AbilityData(),
        SpecialAttackTwo = new AbilityData(),
        MovementAbility = new MovementAbilityData(),
        UltimateAttack = new AbilityData { BaseDamage = 20f }
    };

    private static PlayerInputFrame Frame(int tick, sbyte moveX, GameplayButtons pressed) => new() {
        Tick = (uint)tick,
        MoveX = moveX,
        MoveY = 0,
        Held = pressed,
        Pressed = pressed
    };

    private static PlayerInputFrame FrameHeld(int tick, GameplayButtons held) => new() {
        Tick = (uint)tick,
        MoveX = 0,
        MoveY = 0,
        Held = held,
        Pressed = GameplayButtons.None
    };

    private static PlayerInputFrame FrameChord(int tick, GameplayButtons held, GameplayButtons pressed) => new() {
        Tick = (uint)tick,
        MoveX = 0,
        MoveY = 0,
        Held = held,
        Pressed = pressed
    };
}
