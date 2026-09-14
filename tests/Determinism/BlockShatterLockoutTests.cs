using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Pins the V7.3 block-model closure in the deterministic simulation: a
/// shatter arms the five-second lockout during which the stance cannot rise
/// and charge regeneration is held (charge #1 lands exactly one regen interval
/// after the lockout expires), the shatter replaces the flat blocked hitstop
/// with the longer shared freeze, an empty shield never raises the stance,
/// and a stock loss clears every piece of the new state. Numbers come from
/// <see cref="BasicComboRules"/>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class BlockShatterLockoutTests {

    [TestCase]
    public void ShatterArmsTheLockoutAndHoldsRegenUntilItExpires() {
        var simulation = NewZeroKnockbackSimulation(seed: 130);
        int shatterTick = LandThreeBlockedHits(simulation);

        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent shattered)).IsTrue();
        AssertThat(shattered.BlockCharges).IsEqual(0);
        AssertThat(shattered.DazeFrames)
            .OverrideFailureMessage("The shatter must open the one-second daze.")
            .IsEqual(60);
        AssertThat(simulation.TryGetFighterVerb(1, out FighterVerbComponent verb)).IsTrue();
        AssertThat(verb.BlockLockoutFrames)
            .OverrideFailureMessage("The shatter must arm the five-second lockout.")
            .IsEqual(BasicComboRules.BlockShatterLockoutFrames);
        // The shatter freeze replaces the flat blocked hitstop.
        AssertThat(verb.HitstopFrames)
            .OverrideFailureMessage("A shatter freezes for the shared shatter window, not the blocked 2f.")
            .IsEqual(BasicComboRules.ShatterFreezeFrames);

        // Regen is held through the freeze + lockout, then the interval counts:
        // charge #1 lands at shatter + freeze + 300 + 180 and not a frame sooner.
        int chargeTick = shatterTick + BasicComboRules.ShatterFreezeFrames
            + BasicComboRules.BlockShatterLockoutFrames + BasicComboRules.BlockChargeRegenFrames;
        int tick = shatterTick + 1;
        for (; tick < chargeTick - 5; tick++) {
            Advance(simulation, tick);
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent stillEmpty)).IsTrue();
        AssertThat(stillEmpty.BlockCharges)
            .OverrideFailureMessage("Regen must be held through the whole lockout (first charge at shatter+480f).")
            .IsEqual(0);
        for (; tick <= chargeTick + 5; tick++) {
            Advance(simulation, tick);
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent recovered)).IsTrue();
        AssertThat(recovered.BlockCharges)
            .OverrideFailureMessage("The first charge must land one regen interval after the lockout expires.")
            .IsEqual(1);
        AssertThat(simulation.TryGetFighterVerb(1, out FighterVerbComponent expired)).IsTrue();
        AssertThat(expired.BlockLockoutFrames).IsEqual(0);
    }

    [TestCase]
    public void TheStanceNeverRisesOnAnEmptyShieldOrDuringTheLockout() {
        FighterStateComponent fighter = GroundedBlocker();
        FighterRuntimeComponent runtime = BlockHeldRuntime();
        FighterVerbComponent verb = default;
        AssertThat(FighterBasicAttackRules.IsBlockStance(in fighter, in runtime, in verb))
            .OverrideFailureMessage("The baseline grounded blocker must be in the stance.")
            .IsTrue();

        // 0-charge ignore: Block held with an empty shield is no stance at all.
        FighterStateComponent empty = fighter;
        empty.BlockCharges = 0;
        AssertThat(FighterBasicAttackRules.IsBlockStance(in empty, in runtime, in verb))
            .OverrideFailureMessage("An empty shield must never raise the stance.")
            .IsFalse();

        // Shatter lockout: charges are irrelevant while the lockout runs.
        FighterVerbComponent locked = verb;
        locked.BlockLockoutFrames = 1;
        AssertThat(FighterBasicAttackRules.IsBlockStance(in fighter, in runtime, in locked))
            .OverrideFailureMessage("The stance must not rise during the shatter lockout.")
            .IsFalse();

        // Shieldstun locks the stance UP regardless of held buttons.
        FighterVerbComponent stunned = verb;
        stunned.ShieldStunFrames = 1;
        FighterRuntimeComponent released = runtime;
        released.HeldButtons = 0;
        AssertThat(FighterBasicAttackRules.IsBlockStance(in fighter, in released, in stunned))
            .OverrideFailureMessage("Shieldstun must hold the stance up even with Block released.")
            .IsTrue();
    }

    [TestCase]
    public void AStockLossClearsTheLockoutShieldstunAndCancelGate() {
        FighterStateComponent fighter = GroundedBlocker();
        fighter.MaxHP = 100;
        fighter.CurrentHP = 0;
        FighterRuntimeComponent runtime = default;
        FighterVerbComponent verb = default;
        verb.BlockLockoutFrames = BasicComboRules.BlockShatterLockoutFrames;
        verb.ShieldStunFrames = BasicComboRules.ShieldstunFrames;
        verb.HitstunBlockCancelBlocked = 1;
        verb.LedgeGrabsThisAirtime = 2;
        FighterTuningComponent tuning = default;
        FighterDefenseComponent defense = default;

        FighterSimulationRules.ApplyStockLoss(ref fighter, ref runtime, ref verb, ref defense, in tuning);

        AssertThat(verb.BlockLockoutFrames)
            .OverrideFailureMessage("A stock loss must clear the shatter lockout.")
            .IsEqual(0);
        AssertThat(verb.ShieldStunFrames).IsEqual(0);
        AssertThat(verb.HitstunBlockCancelBlocked).IsEqual(0);
        AssertThat(verb.LedgeGrabsThisAirtime).IsEqual(0);
    }

    // ---- Harness -------------------------------------------------------------

    /// <summary>Mashes the attacker's string into a turtling victim until all
    /// three shield charges are spent; returns the shatter tick.</summary>
    private static int LandThreeBlockedHits(FighterSimulation simulation) {
        int landed = 0;
        for (int tick = 0; tick < 400; tick++) {
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
            AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
            bool press = landed == 0
                ? runtime.AttackPhase == FighterBasicAttackRules.PhaseNone
                : runtime.AttackPhase == FighterBasicAttackRules.PhaseChainHold;
            Advance(
                simulation,
                tick,
                p1Buttons: press ? GameplayButtons.BasicAttack : GameplayButtons.None,
                p2Held: GameplayButtons.Block);
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
            if (after.BlockCharges < before.BlockCharges) {
                landed++;
                if (landed == 3) return tick;
            }
        }
        AssertThat(false).OverrideFailureMessage("The string never shattered the shield.").IsTrue();
        return -1;
    }

    private static FighterStateComponent GroundedBlocker() => new() {
        PlayerID = 1,
        Stocks = 3,
        MaxHP = 100,
        CurrentHP = 100,
        BlockCharges = 3,
        IsGrounded = 1
    };

    private static FighterRuntimeComponent BlockHeldRuntime() => new() {
        HeldButtons = (int)GameplayButtons.Block
    };

    private static FighterSimulation NewZeroKnockbackSimulation(int seed) => new(
        FighterLoadoutFactory.FromCharacterData(BuildZeroKnockbackAttacker()),
        FighterLoadout.Default(FighterCharacterID.Joan),
        seed: seed,
        spawnDistance: 1,
        rules: FighterMatchRules.Disabled);

    private static CharacterData BuildZeroKnockbackAttacker() => new() {
        CharacterID = "tesla",
        MaxHP = 100,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 1,
        MaxMoveSpeed = 8f,
        MaxJumpForce = 13f,
        BasicAttackDamage = 10f,
        BasicAttackKnockback = 0f,
        SpecialAttackOne = new AbilityData(),
        SpecialAttackTwo = new AbilityData(),
        MovementAbility = new MovementAbilityData(),
        UltimateAttack = new AbilityData { BaseDamage = 20f }
    };

    private static void Advance(
        FighterSimulation simulation,
        int tick,
        GameplayButtons p1Buttons = GameplayButtons.None,
        GameplayButtons p2Held = GameplayButtons.None) {
        simulation.Advance(
            new PlayerInputFrame {
                Tick = (uint)tick,
                Held = p1Buttons,
                Pressed = p1Buttons
            },
            new PlayerInputFrame {
                Tick = (uint)tick,
                Held = p2Held,
                Pressed = GameplayButtons.None
            });
    }
}
