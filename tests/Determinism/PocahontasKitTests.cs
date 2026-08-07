using FTT.Core;
using FTT.FighterSim;
using FTT.Characters;
using FTT.Combat;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Deterministic Fighter-side coverage for the canonical Pocahontas kit: the
/// Vine Snare persistent construct (deploy limit 2, 10 s lifetime, proximity
/// bite applying Root), the Breeze Glide reduced-gravity float window with
/// double-jump reset, and rollback safety across the snare lifecycle.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class PocahontasKitTests {

    /// <summary>
    /// The kit tests churn large deterministic-simulation allocations alongside
    /// throwaway Godot resource instances. Draining pending finalizers here
    /// keeps them from racing a later suite's ResourceLoader work, which can
    /// trip the engine's script-instance dispose FATAL mid-load.
    /// </summary>
    [After]
    public void DrainPendingFinalizers() {
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();
    }

    [TestCase]
    public void VineSnareDeploysAreCappedAtTwoAndRootTheOpponent() {
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(BuildSnareCharacter()),
            FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 61,
            spawnDistance: 1,
            rules: FighterMatchRules.Disabled);

        // Three rapid deploys must never exceed the design's two active pods;
        // the oldest snare is recycled.
        simulation.Advance(Frame(0, 0, GameplayButtons.Special2), Frame(0, 0, GameplayButtons.None));
        simulation.Advance(Frame(1, 0, GameplayButtons.None), Frame(1, 0, GameplayButtons.None));
        simulation.Advance(Frame(2, 0, GameplayButtons.Special2), Frame(2, 0, GameplayButtons.None));
        simulation.Advance(Frame(3, 0, GameplayButtons.None), Frame(3, 0, GameplayButtons.None));
        simulation.Advance(Frame(4, 0, GameplayButtons.Special2), Frame(4, 0, GameplayButtons.None));
        AssertThat(simulation.PersistentObjectCount).IsEqual(2);

        // 10 s lifetime authors 600 simulation frames.
        AssertThat(simulation.TryGetFirstPersistentObject(out FighterPersistentObjectComponent snare)).IsTrue();
        AssertThat(snare.MaxDeployLimit).IsEqual(2);
        AssertThat(snare.LifetimeFrames > 590).IsTrue();
        AssertThat(snare.LifetimeFrames <= 600).IsTrue();

        // The adjacent opponent is bitten once the snare's proximity trigger
        // re-arms: light damage plus Root for the authored 1.5 s (90 frames).
        for (int tick = 5; tick <= 45; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(target.CurrentHP < target.MaxHP).IsTrue();
        AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent runtime)).IsTrue();
        AssertThat(runtime.StatusType).IsEqual((int)StatusType.Root);
        AssertThat(runtime.StatusFrames <= 90).IsTrue();
    }

    [TestCase]
    public void BreezeGlideGrantsAuthoredFloatWindowAndResetsDoubleJump() {
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(BuildGlideCharacter()),
            FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 62,
            spawnDistance: 4,
            rules: FighterMatchRules.Disabled);

        // Exhaust both jumps: grounded jump leaves one, air jump leaves zero.
        simulation.Advance(Frame(0, 0, GameplayButtons.Jump), Frame(0, 0, GameplayButtons.None));
        for (int tick = 1; tick < 10; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        simulation.Advance(Frame(10, 0, GameplayButtons.Jump), Frame(10, 0, GameplayButtons.None));
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent spent)).IsTrue();
        AssertThat(spent.RemainingJumps).IsEqual(0);
        AssertThat(spent.IsGrounded).IsEqual(0);

        // Glide start: forward boost, a 3 s (180-frame) reduced-gravity float
        // window, and the double jump comes back.
        simulation.Advance(Frame(11, 0, GameplayButtons.MovementAbility), Frame(11, 0, GameplayButtons.None));
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent gliding)).IsTrue();
        AssertThat(gliding.RemainingJumps).IsEqual(2);
        AssertThat(gliding.IsGrounded).IsEqual(0);
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
        AssertThat(runtime.FloatFrames).IsEqual(180);

        // The float window decays one frame per tick while the glide holds the
        // fighter airborne.
        for (int tick = 12; tick < 72; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent decayed)).IsTrue();
        AssertThat(decayed.FloatFrames).IsEqual(120);
    }

    [TestCase]
    public void VineSnareLifecycleIsSnapshotAndRollbackSafe() {
        FighterLoadout loadout = FighterLoadoutFactory.FromCharacterData(BuildSnareCharacter());
        var uninterrupted = new FighterSimulation(
            loadout, FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 63, spawnDistance: 1, rules: FighterMatchRules.Disabled);
        var restored = new FighterSimulation(
            loadout, FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 63, spawnDistance: 1, rules: FighterMatchRules.Disabled);

        uninterrupted.Advance(Frame(0, 0, GameplayButtons.Special2), Frame(0, 0, GameplayButtons.None));
        uninterrupted.Advance(Frame(1, 0, GameplayButtons.None), Frame(1, 0, GameplayButtons.None));
        uninterrupted.Advance(Frame(2, 0, GameplayButtons.Special2), Frame(2, 0, GameplayButtons.None));
        for (int tick = 3; tick < 60; tick++) {
            uninterrupted.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, -127, GameplayButtons.None));
        }

        byte[] snapshot = uninterrupted.CaptureFullState();
        restored.RestoreFullState(snapshot);
        AssertThat(restored.CurrentHash).IsEqual(uninterrupted.CurrentHash);

        // Resimulate through repeated bites, Root cycles, and knockback pushes;
        // both simulations must stay hash-identical every tick.
        for (int tick = 60; tick < 420; tick++) {
            long expected = uninterrupted.Advance(
                Frame(tick, 0, GameplayButtons.None), Frame(tick, -127, GameplayButtons.None));
            long actual = restored.Advance(
                Frame(tick, 0, GameplayButtons.None), Frame(tick, -127, GameplayButtons.None));
            AssertThat(actual).IsEqual(expected);
        }
    }

    private static CharacterData BuildSnareCharacter() => new() {
        CharacterID = "pocahontas",
        MaxHP = 90,
        Weight = 0.8f,
        MaxBlockCharges = 3,
        MaxJumpCount = 2,
        MaxMoveSpeed = 9f,
        MaxJumpForce = 15f,
        BasicAttackDamage = 9f,
        BasicAttackKnockback = 2.5f,
        SpecialAttackOne = new AbilityData(),
        SpecialAttackTwo = new AbilityData {
            ExecutionType = AbilityExecutionType.PersistentObject,
            BaseDamage = 8f,
            PersistentObjectID = "vine_snare",
            MaxActiveObjects = 2,
            Lifetime = 10f,
            CooldownDuration = 0f,
            AppliedStatus = StatusType.Root,
            StatusDuration = 1.5f
        },
        MovementAbility = new MovementAbilityData(),
        UltimateAttack = new AbilityData { BaseDamage = 20f }
    };

    private static CharacterData BuildGlideCharacter() => new() {
        CharacterID = "pocahontas",
        MaxHP = 90,
        Weight = 0.8f,
        MaxBlockCharges = 3,
        MaxJumpCount = 2,
        MaxMoveSpeed = 9f,
        MaxJumpForce = 15f,
        BasicAttackDamage = 9f,
        BasicAttackKnockback = 2.5f,
        SpecialAttackOne = new AbilityData(),
        SpecialAttackTwo = new AbilityData(),
        MovementAbility = new MovementAbilityData {
            MovementType = MovementType.Glide,
            MovementDuration = 3f,
            MovementSpeed = 350f,
            CooldownDuration = 5f,
            ResetsDoubleJump = true
        },
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
