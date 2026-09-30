using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using Godot;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 12 W4 — Tesla's Lightning Blink in the deterministic sim (design §5,
/// timing 2026-09-26): 6 startup / 12 translation / 10 recovery frames over the
/// authored 3.0 units, with projectile pass-through during the translation only.
/// The phases ride the existing universal-movement slot (no new component), so
/// these cases also pin that the move survives snapshot and rollback.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TeslaBlinkSimTests {

    [TestCase]
    public void TheBlinkRunsSixStartupTwelveTranslationAndTenRecoveryFrames() {
        var simulation = new FighterSimulation(
            TeslaLoadout(), FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 1201, spawnDistance: 4, rules: FighterMatchRules.Disabled);
        // The press tick's own movement step runs before the ability system arms
        // the blink, so the baseline is the position after that tick.
        simulation.Advance(Frame(0, -127, GameplayButtons.MovementAbility), Frame(0, 0, GameplayButtons.None));
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent before)).IsTrue();
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent armed)).IsTrue();
        AssertThat(armed.UniversalMovementState).IsEqual(FighterKitMotion.BlinkStartup);
        AssertThat(armed.MovementCooldownFrames > 0).IsTrue();

        int startup = 0, travel = 0, recovery = 0;
        FP64 xAfterStartup = before.Position.x;
        FP64 xAfterTravel = before.Position.x;
        for (int tick = 1; tick <= 32; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime);
            simulation.TryGetFighter(0, out FighterStateComponent state);
            switch (runtime.UniversalMovementState) {
                case FighterKitMotion.BlinkStartup: startup++; xAfterStartup = state.Position.x; break;
                case FighterKitMotion.BlinkTravel: travel++; xAfterTravel = state.Position.x; break;
                case FighterKitMotion.BlinkRecovery: recovery++; break;
            }
        }

        AssertThat(startup).IsEqual(KitMotionRules.LightningBlinkStartupFrames);
        AssertThat(travel).IsEqual(KitMotionRules.LightningBlinkTravelFrames);
        AssertThat(recovery).IsEqual(KitMotionRules.LightningBlinkRecoveryFrames);
        AssertThat(startup + travel + recovery <= KitMotionRules.LightningBlinkActionCapFrames).IsTrue();
        // The spark gather holds him in place; the translation covers 3.0 units.
        AssertThat(xAfterStartup).IsEqual(before.Position.x);
        FP64 travelled = before.Position.x - xAfterTravel;
        AssertThat(FP64.Abs(travelled - FP64.FromInt(3)) < FP64.FromDouble(0.02))
            .OverrideFailureMessage($"The blink covered {travelled} units, not 3.0.")
            .IsTrue();
        simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent done);
        AssertThat(done.UniversalMovementState).IsEqual(0);
    }

    [TestCase]
    public void AProjectilePassesThroughTheTranslationButLandsOnAStandingTesla() {
        // Control: Tesla stands still and the shot lands.
        FighterSimulation control = ProjectileDuel(blink: false);
        AssertThat(control.TryGetFighter(0, out FighterStateComponent hit)).IsTrue();
        AssertThat(hit.CurrentHP < hit.MaxHP)
            .OverrideFailureMessage("The control shot must land, or the pass-through proves nothing.")
            .IsTrue();

        // Blink: a down-blink on the ground keeps him in the lane, so only the
        // translation's pass-through can explain the miss.
        FighterSimulation blinked = ProjectileDuel(blink: true);
        AssertThat(blinked.TryGetFighter(0, out FighterStateComponent passed)).IsTrue();
        AssertThat(passed.CurrentHP)
            .OverrideFailureMessage("A projectile must pass through Tesla during the translation.")
            .IsEqual(passed.MaxHP);
    }

    [TestCase]
    public void AMidTranslationSnapshotResumesTheSameHashSequence() {
        FighterLoadout tesla = TeslaLoadout();
        var uninterrupted = new FighterSimulation(
            tesla, FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 1203, spawnDistance: 4, rules: FighterMatchRules.Disabled);
        var restored = new FighterSimulation(
            tesla, FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 1203, spawnDistance: 4, rules: FighterMatchRules.Disabled);

        uninterrupted.Advance(Frame(0, -127, GameplayButtons.MovementAbility, moveY: -127), Frame(0, 0, GameplayButtons.None));
        for (int tick = 1; tick < 10; tick++) {
            uninterrupted.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        uninterrupted.TryGetFighterRuntime(0, out FighterRuntimeComponent mid);
        AssertThat(mid.UniversalMovementState).IsEqual(FighterKitMotion.BlinkTravel);

        restored.RestoreFullState(uninterrupted.CaptureFullState());
        AssertThat(restored.CurrentHash).IsEqual(uninterrupted.CurrentHash);
        for (int tick = 10; tick < 60; tick++) {
            long expected = uninterrupted.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            long actual = restored.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            AssertThat(actual).IsEqual(expected);
        }
    }

    /// <summary>
    /// Tesla at −2, the shooter at +2 facing him. The shot spawns one unit
    /// ahead of the shooter at 30 units/s, fired on tick 5, so it crosses
    /// Tesla's box on ticks ~10–13 — inside the 7–18 translation window.
    /// </summary>
    private static FighterSimulation ProjectileDuel(bool blink) {
        var simulation = new FighterSimulation(
            TeslaLoadout(), FighterLoadoutFactory.FromCharacterData(ShooterData()),
            seed: 1202, spawnDistance: 2, rules: FighterMatchRules.Disabled);
        for (int tick = 0; tick <= 30; tick++) {
            PlayerInputFrame tesla = blink && tick == 0
                ? Frame(tick, 0, GameplayButtons.MovementAbility, moveY: 127)
                : Frame(tick, 0, GameplayButtons.None);
            PlayerInputFrame shooter = tick == 5
                ? Frame(tick, 0, GameplayButtons.Special1)
                : Frame(tick, 0, GameplayButtons.None);
            simulation.Advance(tesla, shooter);
        }
        return simulation;
    }

    private static FighterLoadout TeslaLoadout() =>
        FighterLoadoutFactory.FromCharacterData(
            AuthoredResources.Load<CharacterData>("res://resources/Characters/tesla_data.tres"));

    // Package 13 W7b: not "joan" — her Special 1 is a ground wave now, which is
    // deliberately not a projectile for the blink's pass-through.
    private static CharacterData ShooterData() => new() {
        CharacterID = "leonardo",
        MaxHP = 100,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 1,
        MaxMoveSpeed = 8f,
        MaxJumpForce = 13f,
        BasicAttackDamage = 10f,
        BasicAttackKnockback = 3f,
        SpecialAttackOne = new AbilityData {
            ExecutionType = AbilityExecutionType.Projectile,
            BaseDamage = 10f,
            ProjectileSpeed = 1800f,
            ProjectileLifetime = 1f,
            CooldownDuration = 5f
        },
        SpecialAttackTwo = new AbilityData(),
        MovementAbility = new MovementAbilityData(),
        UltimateAttack = new AbilityData { BaseDamage = 20f }
    };

    private static PlayerInputFrame Frame(int tick, sbyte moveX, GameplayButtons pressed, sbyte moveY = 0) => new() {
        Tick = (uint)tick,
        MoveX = moveX,
        MoveY = moveY,
        Held = pressed,
        Pressed = pressed
    };
}
