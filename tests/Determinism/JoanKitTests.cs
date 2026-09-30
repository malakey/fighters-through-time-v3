using FTT.Core;
using FTT.FighterSim;
using FTT.Characters;
using FTT.Combat;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Deterministic Fighter-side coverage for the canonical Joan kit: the
/// Righteous Smite short ground wave (J02, Package 13 W7b) with RadiantBurn and
/// its damage-amplification behavior, Divine Piercing's lunging three-thrust
/// flurry (J01/J03) and its Shield-Breaker shatter, Ascendant Wings' rising
/// leap with no glide float (A08), and rollback safety across the full kit.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class JoanKitTests {

    [TestCase]
    public void RighteousSmiteProjectileAppliesRadiantBurnThatAmplifiesDamage() {
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(BuildSmiteCharacter()),
            FighterLoadout.Default(FighterCharacterID.Tesla),
            seed: 61,
            spawnDistance: 1,
            rules: FighterMatchRules.Disabled);

        // The smite shockwave travels along the ground and lands within a few
        // ticks: 14 damage plus RadiantBurn for the authored 3 s.
        simulation.Advance(Frame(0, 0, GameplayButtons.Special1), Frame(0, 0, GameplayButtons.None));
        for (int tick = 1; tick <= 5; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent burned)).IsTrue();
        AssertThat(burned.CurrentHP).IsEqual(86);
        AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent runtime)).IsTrue();
        // RadiantBurn occupies the V7 damage status slot.
        AssertThat(runtime.DamageStatusType).IsEqual((int)StatusType.RadiantBurn);
        AssertThat(runtime.DamageStatusFrames > 0).IsTrue();

        // RadiantBurn amplifies damage taken by 25%: Joan's V7.1 opener
        // (10 x 1.0 = 10) resolves as 13 while the burn is active. The smite's
        // shove plus Joan's shorter authored reach (1.8 units) mean she walks
        // back into range first.
        for (int tick = 6; tick <= 25; tick++) {
            simulation.Advance(Frame(tick, 127, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        simulation.Advance(Frame(26, 0, GameplayButtons.BasicAttack), Frame(26, 0, GameplayButtons.None));
        // Basics are phased swings: the opener's hit lands during its active
        // window (Joan's 5 startup frames), still well inside the 3 s burn.
        for (int tick = 27; tick <= 40; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent amplified)).IsTrue();
        AssertThat(amplified.CurrentHP).IsEqual(73);
    }

    [TestCase]
    public void DivinePiercingDealsFullMultiHitTotalAndFullShattersABlockedStance() {
        // J01/J03 (Package 13 W7b), rewritten in place: the thrusts are no longer
        // one instant hit on the press tick. Joan lunges across the 12 active
        // frames and three 8-damage thrusts land along the way — 24 total.
        var open = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(BuildPiercingCharacter()),
            FighterLoadout.Default(FighterCharacterID.Tesla),
            seed: 62,
            spawnDistance: 1,
            rules: FighterMatchRules.Disabled);
        open.Advance(Frame(0, 0, GameplayButtons.Special2), Frame(0, 0, GameplayButtons.None));
        AssertThat(open.TryGetFighter(1, out FighterStateComponent untouched)).IsTrue();
        AssertThat(untouched.CurrentHP).IsEqual(100);
        for (int tick = 1; tick <= 40; tick++) {
            open.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(open.TryGetFighter(1, out FighterStateComponent struck)).IsTrue();
        AssertThat(struck.CurrentHP).IsEqual(76);

        // V7.6 F15 / A01 (Package 13 W1), rewritten in place: Divine Piercing is
        // a Shield-Breaker FULL shatter in both modes (the loadout projects its
        // BlockClass; an ordinary Special would spend only two). The retired
        // two-charge "shield-stutter" exception took 3 charges to 1 and left the
        // defender undazed; the Special class now takes 1, 2 or 3 charges to 0
        // with the normal shatter response (shatter-freeze, daze, 5 s lockout).
        // Lincoln's Emancipator already passed 0 here, so the two modes
        // disagreed with each other until this pass.
        var blocked = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(BuildPiercingCharacter()),
            FighterLoadout.Default(FighterCharacterID.Tesla),
            seed: 63,
            spawnDistance: 1,
            rules: FighterMatchRules.Disabled);
        blocked.Advance(Frame(0, 0, GameplayButtons.None), Frame(0, 0, GameplayButtons.Block));
        blocked.Advance(Frame(1, 0, GameplayButtons.Special2), Frame(1, 0, GameplayButtons.Block));
        // Step to the first absorbed thrust.
        for (int tick = 2; tick <= 20; tick++) {
            AssertThat(blocked.TryGetFighter(1, out FighterStateComponent probe)).IsTrue();
            if (probe.BlockCharges == 0) break;
            blocked.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.Block));
        }
        AssertThat(blocked.TryGetFighter(1, out FighterStateComponent defender)).IsTrue();
        AssertThat(defender.BlockCharges)
            .OverrideFailureMessage("A full shatter takes every remaining charge to zero.")
            .IsEqual(0);
        AssertThat(defender.CurrentHP)
            .OverrideFailureMessage("The absorbed contact still deals no HP damage.")
            .IsEqual(100);
        AssertThat(defender.DazeFrames)
            .OverrideFailureMessage("The Special shatter dazes for the shared 1 s window.")
            .IsEqual(60);
        AssertThat(blocked.TryGetFighterVerb(1, out FighterVerbComponent shattered)).IsTrue();
        AssertThat(shattered.BlockLockoutFrames)
            .OverrideFailureMessage("...and arms the five-second lockout.")
            .IsEqual(FTT.Combat.BasicComboRules.BlockShatterLockoutFrames);
    }

    [TestCase]
    public void AscendantWingsIsARisingLeapWithNoGlideFloat() {
        // A08 (Package 13 W7b), rewritten in place: the retired 3 s glide armed a
        // 180-frame float window. Ascendant Wings is now a rising slash-leap at
        // the authored speed for the cast's frames — no float at all.
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(BuildWingsCharacter()),
            FighterLoadout.Default(FighterCharacterID.Tesla),
            seed: 64,
            spawnDistance: 4,
            rules: FighterMatchRules.Disabled);

        simulation.Advance(Frame(0, 0, GameplayButtons.MovementAbility), Frame(0, 0, GameplayButtons.None));
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent joan)).IsTrue();
        AssertThat(joan.IsGrounded).IsEqual(0);
        AssertThat(joan.Velocity.y).IsEqual(xpTURN.Klotho.Deterministic.Math.FP64.FromInt(7));
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
        AssertThat(runtime.FloatFrames).IsEqual(0);
        AssertThat(runtime.UniversalMovementState).IsEqual(FighterKitMotion.WingRise);

        // Released at the top of the rise, she simply falls: no glide window.
        for (int tick = 1; tick <= 60; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent later)).IsTrue();
        AssertThat(later.FloatFrames).IsEqual(0);
        AssertThat(later.UniversalMovementState).IsEqual(0);
    }

    [TestCase]
    public void JoanKitLifecycleIsSnapshotAndRollbackSafe() {
        FighterLoadout loadout = FighterLoadoutFactory.FromCharacterData(BuildFullCharacter());
        var uninterrupted = new FighterSimulation(
            loadout, FighterLoadout.Default(FighterCharacterID.Tesla),
            seed: 65, spawnDistance: 2, rules: FighterMatchRules.Disabled);
        var restored = new FighterSimulation(
            loadout, FighterLoadout.Default(FighterCharacterID.Tesla),
            seed: 65, spawnDistance: 2, rules: FighterMatchRules.Disabled);

        uninterrupted.Advance(Frame(0, 0, GameplayButtons.Special1), Frame(0, 0, GameplayButtons.None));
        for (int tick = 1; tick < 5; tick++) {
            uninterrupted.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        uninterrupted.Advance(Frame(5, 0, GameplayButtons.MovementAbility), Frame(5, 0, GameplayButtons.None));
        for (int tick = 6; tick < 20; tick++) {
            uninterrupted.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.Block));
        }
        uninterrupted.Advance(Frame(20, 0, GameplayButtons.Special2), Frame(20, 0, GameplayButtons.Block));
        for (int tick = 21; tick < 60; tick++) {
            uninterrupted.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, -127, GameplayButtons.None));
        }

        byte[] snapshot = uninterrupted.CaptureFullState();
        restored.RestoreFullState(snapshot);
        AssertThat(restored.CurrentHash).IsEqual(uninterrupted.CurrentHash);

        for (int tick = 60; tick < 420; tick++) {
            long expected = uninterrupted.Advance(
                Frame(tick, 0, GameplayButtons.None), Frame(tick, -127, GameplayButtons.None));
            long actual = restored.Advance(
                Frame(tick, 0, GameplayButtons.None), Frame(tick, -127, GameplayButtons.None));
            AssertThat(actual).IsEqual(expected);
        }
    }

    private static AbilityData BuildSmiteAbility() => new() {
        ExecutionType = AbilityExecutionType.Projectile,
        BaseDamage = 14f,
        // J02 (Package 13 W7b): a short ground wave, 2.5 units at 10 u/s.
        ProjectileSpeed = 600f,
        ProjectileLifetime = 0.25f,
        HitstunFrames = 30,
        Launches = false,
        KnockbackForce = Vector2.Zero,
        CooldownDuration = 10f,
        AppliedStatus = StatusType.RadiantBurn,
        StatusDuration = 3f,
        StatusIntensity = 1f
    };

    private static AbilityData BuildPiercingAbility() => new() {
        ExecutionType = AbilityExecutionType.Melee,
        BaseDamage = 8f,
        IsMultiHit = true,
        HitCount = 3,
        ActiveFrames = 12,
        KnockbackForce = new Vector2(4f, -1f),
        CooldownDuration = 10f,
        // A01 (Package 13 W1): Divine Piercing is an authored Shield-Breaker.
        BlockClass = FTT.Combat.BlockClass.ShieldBreaker
    };

    private static MovementAbilityData BuildWingsAbility() => new() {
        MovementType = MovementType.WingDive,
        MovementDuration = 1f,
        MovementSpeed = 420f,
        CooldownDuration = 5f
    };

    private static CharacterData BuildSmiteCharacter() => BuildCharacter(
        BuildSmiteAbility(), new AbilityData(), new MovementAbilityData());

    private static CharacterData BuildPiercingCharacter() => BuildCharacter(
        new AbilityData(), BuildPiercingAbility(), new MovementAbilityData());

    private static CharacterData BuildWingsCharacter() => BuildCharacter(
        new AbilityData(), new AbilityData(), BuildWingsAbility());

    private static CharacterData BuildFullCharacter() => BuildCharacter(
        BuildSmiteAbility(), BuildPiercingAbility(), BuildWingsAbility());

    private static CharacterData BuildCharacter(
        AbilityData specialOne,
        AbilityData specialTwo,
        MovementAbilityData movement) => new() {
        CharacterID = "joan",
        MaxHP = 100,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 1,
        MaxMoveSpeed = 8f,
        MaxJumpForce = 13f,
        BasicAttackDamage = 10f,
        BasicAttackKnockback = 3f,
        SpecialAttackOne = specialOne,
        SpecialAttackTwo = specialTwo,
        MovementAbility = movement,
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
