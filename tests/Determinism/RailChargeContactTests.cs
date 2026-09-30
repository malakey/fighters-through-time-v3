using FTT.Characters;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 13 W7b (LN02) — Lincoln's Rail Charge in the sim, on his authored
/// data: a short armored burst of 5 units over 30 frames that stops dead on
/// contact, where the ram deals 6 damage with moderate horizontal knockback and
/// no launch. Travel and armor end together; a blocked ram stops too.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class RailChargeContactTests {

    [TestCase]
    public void TheChargeStopsDeadOnContactWithSixDamageAndAHorizontalShove() {
        var simulation = NewMatch();
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent before)).IsTrue();
        simulation.Advance(Frame(0, GameplayButtons.MovementAbility), Frame(0, GameplayButtons.None));
        int contactTick = -1;
        for (int tick = 1; tick <= 30; tick++) {
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent probe)).IsTrue();
            if (probe.CurrentHP < 100) {
                contactTick = tick;
                break;
            }
        }
        AssertThat(contactTick > 0).IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(target.CurrentHP).IsEqual(94);
        // Moderate horizontal knockback, not a launch: still on the ground.
        AssertThat(target.IsGrounded).IsEqual(1);
        AssertThat(target.Velocity.x > FP64.Zero).IsTrue();
        AssertThat(target.Velocity.y).IsEqual(FP64.Zero);

        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent lincoln)).IsTrue();
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
        AssertThat(runtime.UniversalMovementState).IsEqual(0);
        AssertThat(lincoln.Velocity.x).IsEqual(FP64.Zero);
        AssertThat(lincoln.HyperArmorFrames).IsEqual(0);
        AssertThat(lincoln.Position.x - before.Position.x < FP64.FromInt(5)).IsTrue();
    }

    [TestCase]
    public void ABlockedRamStillStopsTheCharge() {
        var simulation = NewMatch();
        simulation.Advance(Frame(0, GameplayButtons.None), Frame(0, GameplayButtons.Block));
        simulation.Advance(Frame(1, GameplayButtons.MovementAbility), Frame(1, GameplayButtons.Block));
        bool stoppedOnContact = false;
        for (int tick = 2; tick <= 32; tick++) {
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.Block));
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent probe)).IsTrue();
            AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
            if (probe.BlockCharges < 3) {
                stoppedOnContact = runtime.UniversalMovementState == 0;
                break;
            }
        }
        AssertThat(stoppedOnContact).IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent defender)).IsTrue();
        AssertThat(defender.CurrentHP).IsEqual(100);
    }

    private static FighterSimulation NewMatch() => new(
        FighterLoadoutFactory.FromCharacterData(
            AuthoredResources.Load<CharacterData>("res://resources/Characters/lincoln_data.tres")),
        FighterLoadout.Default(FighterCharacterID.Tesla),
        seed: 98,
        spawnDistance: 2,
        rules: FighterMatchRules.Disabled);

    private static PlayerInputFrame Frame(int tick, GameplayButtons pressed) => new() {
        Tick = (uint)tick,
        Held = pressed,
        Pressed = pressed
    };
}
