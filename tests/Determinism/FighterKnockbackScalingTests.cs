using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Gameplay-feel plan §2.5, Fighter side. Two locked rules:
///
/// 1. <b>Low-health knockback scaling.</b> Every impulse is multiplied by
///    <c>1 + missingHPFraction</c> of the victim measured after the hit's own
///    damage — a linear 1x at full HP to 2x at 0 HP — applied once, in
///    <c>FighterDamageRules.ApplyFighterHit</c>, so basics, specials,
///    ultimates, projectiles, zones, constructs and stage hazards all inherit
///    it without a second copy of the rule.
/// 2. <b>The finisher creates separation.</b> With
///    <c>BasicComboRules.KnockbackMultipliers[2]</c> at 4.5x, the third hit of
///    the string leaves every one of the nine authored kits more than the
///    2-unit melee range apart by the time the victim's hitstun ends — even at
///    <i>full</i> victim HP, where the scaling above contributes nothing.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterKnockbackScalingTests {

    /// <summary>Melee reach in <c>FighterCombatSystem</c>, plus the design margin.</summary>
    private static readonly FP64 RequiredFinisherSeparation = FP64.FromDouble(2.5);

    private static readonly string[] RosterIDs = {
        "einstein", "joan", "leonardo", "lincoln", "cleopatra",
        "tesla", "shakespeare", "mozart", "pocahontas"
    };

    [TestCase]
    public void TheFinisherSeparatesEveryKitBeyondMeleeRangeAtFullVictimHP() {
        var issues = new System.Collections.Generic.List<string>();
        foreach (string id in RosterIDs) {
            (FP64 separation, int victimHP, int victimMaxHP) = MeasureFinisherSeparation(id);
            if (victimHP != victimMaxHP) {
                issues.Add($"{id}: the finisher must land at full victim HP (was {victimHP}/{victimMaxHP}).");
            }
            if (separation < RequiredFinisherSeparation) {
                issues.Add($"{id}: finisher separation {Describe(separation)} < required 2.5 units.");
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void TheSameHitScalesItsImpulseLinearlyWithTheVictimsMissingHP() {
        // One identical opener (8 damage, knockback 4, victim weight 1, so an
        // unscaled impulse of exactly 2.0) against three victims whose HP pools
        // put them at a different missing-HP fraction after that same hit. The
        // pools are chosen so every expected value is exact in fixed point:
        //
        //   MaxHP 64 -> 56 left -> missing 1/8  -> 1.125x -> impulse 2.25
        //   MaxHP 32 -> 24 left -> missing 1/4  -> 1.25x  -> impulse 2.5
        //   MaxHP 16 ->  8 left -> missing 1/2  -> 1.5x   -> impulse 3.0
        FighterLoadout attacker = FighterLoadoutFactory.FromCharacterData(BuildAttacker());
        (int MaxHP, int PostHitHP, double Impulse)[] cases = {
            (64, 56, 2.25),
            (32, 24, 2.5),
            (16, 8, 3.0)
        };

        var issues = new System.Collections.Generic.List<string>();
        foreach ((int maxHP, int postHitHP, double impulse) in cases) {
            FighterLoadout victim = FighterLoadoutFactory.FromCharacterData(BuildVictim(maxHP));
            (FP64 measured, int hpAfter) = MeasureOpenerImpulse(attacker, victim);
            if (hpAfter != postHitHP) {
                issues.Add($"MaxHP {maxHP}: post-hit HP {hpAfter} != {postHitHP}.");
            }
            FP64 expected = FP64.FromDouble(impulse);
            if (measured.RawValue != expected.RawValue) {
                issues.Add($"MaxHP {maxHP}: impulse {Describe(measured)} != {impulse}.");
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void TheSharedScaleRampsFromOneAtFullHPToTwoAtZeroAndIgnoresDegenerateInputs() {
        // The rulebook helper both modes read. Story multiplies its knockback
        // vector by it; the sim reproduces the identical ratio in FP64.
        AssertThat(BasicComboRules.LowHealthKnockbackScale(100f, 100f)).IsEqual(1f);
        AssertThat(BasicComboRules.LowHealthKnockbackScale(75f, 100f)).IsEqual(1.25f);
        AssertThat(BasicComboRules.LowHealthKnockbackScale(50f, 100f)).IsEqual(1.5f);
        AssertThat(BasicComboRules.LowHealthKnockbackScale(0f, 100f)).IsEqual(2f);
        // Degenerate inputs never scale past the ramp: no divide-by-zero, no
        // over-2x from negative HP, no under-1x from an over-full bar.
        AssertThat(BasicComboRules.LowHealthKnockbackScale(10f, 0f)).IsEqual(1f);
        AssertThat(BasicComboRules.LowHealthKnockbackScale(-5f, 100f)).IsEqual(2f);
        AssertThat(BasicComboRules.LowHealthKnockbackScale(150f, 100f)).IsEqual(1f);
    }

    /// <summary>
    /// Walks the shared string to its finisher against a mirror of the same
    /// authored kit while keeping the victim at full HP: hits one and two are
    /// deliberately whiffed from outside the 2-unit melee range, both fighters
    /// close in during hit two's recovery, and only the finisher connects. The
    /// attacker's input is released at the press so it has decelerated to a
    /// stop before the launch, leaving the victim's travel as the whole of the
    /// measured gap.
    /// </summary>
    private static (FP64 Separation, int VictimHP, int VictimMaxHP) MeasureFinisherSeparation(string characterID) {
        var data = AuthoredResources.Load<CharacterData>($"res://resources/Characters/{characterID}_data.tres");
        AssertObject(data).IsNotNull();
        FighterLoadout kit = FighterLoadoutFactory.FromCharacterData(data);
        var simulation = new FighterSimulation(
            kit,
            kit,
            seed: 4500,
            spawnDistance: 2,
            rules: FighterMatchRules.Disabled);

        int swings = 0;
        bool finisherPressed = false;
        int connectTick = -1;
        int victimHPAtConnect = 0;
        for (int tick = 0; tick < 600; tick++) {
            AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent attacker)).IsTrue();
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent victim)).IsTrue();
            FP64 gap = victim.Position.x - attacker.Position.x;

            bool press = false;
            sbyte closing = 0;
            if (swings < 2) {
                press = swings == 0
                    ? runtime.AttackPhase == FighterBasicAttackRules.PhaseNone
                    : runtime.AttackPhase == FighterBasicAttackRules.PhaseChainHold;
            } else if (!finisherPressed) {
                // Close only once hit two's active window is spent, so neither
                // whiffed hit can clip the victim on the way in.
                bool safeToClose = runtime.AttackPhase == FighterBasicAttackRules.PhaseRecovery
                    || runtime.AttackPhase == FighterBasicAttackRules.PhaseChainHold;
                if (gap > FP64.FromDouble(1.6)) {
                    if (safeToClose) closing = 1;
                } else if (runtime.AttackPhase == FighterBasicAttackRules.PhaseChainHold) {
                    press = true;
                    finisherPressed = true;
                }
            }

            int hpBefore = victim.CurrentHP;
            Advance(
                simulation,
                tick,
                p1MoveX: (sbyte)(closing * 127),
                p1Buttons: press ? GameplayButtons.BasicAttack : GameplayButtons.None,
                p2MoveX: (sbyte)(closing * -127));
            if (press) swings++;

            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent afterTick)).IsTrue();
            if (afterTick.CurrentHP < hpBefore) {
                AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent hitRuntime)).IsTrue();
                AssertThat(hitRuntime.ComboIndex)
                    .OverrideFailureMessage($"{characterID}: only the finisher may connect in this protocol.")
                    .IsEqual(2);
                connectTick = tick;
                victimHPAtConnect = hpBefore;
                break;
            }
        }
        AssertThat(connectTick >= 0)
            .OverrideFailureMessage($"{characterID}: the finisher never connected.")
            .IsTrue();

        // Ride the victim's hitstun out on neutral input and read the gap.
        for (int tick = connectTick + 1; tick < connectTick + 240; tick++) {
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent victim)).IsTrue();
            if (victim.HitstunFrames <= 0) break;
            Advance(simulation, tick);
        }
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent finalAttacker)).IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent finalVictim)).IsTrue();
        return (
            FP64.Abs(finalVictim.Position.x - finalAttacker.Position.x),
            victimHPAtConnect,
            finalVictim.MaxHP);
    }

    /// <summary>
    /// Lands a single opener on a stationary victim and returns the horizontal
    /// launch speed plus the victim's post-hit HP.
    /// </summary>
    private static (FP64 Impulse, int PostHitHP) MeasureOpenerImpulse(
        FighterLoadout attacker,
        FighterLoadout victim) {
        var simulation = new FighterSimulation(
            attacker,
            victim,
            seed: 4501,
            spawnDistance: 1,
            rules: FighterMatchRules.Disabled);

        Advance(simulation, 0, p1Buttons: GameplayButtons.BasicAttack);
        for (int tick = 1; tick <= BasicComboRules.GroundStartupFrames[0]; tick++) {
            Advance(simulation, tick);
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent struck)).IsTrue();
        AssertThat(struck.HitstunFrames)
            .OverrideFailureMessage("The opener must have connected.")
            .IsEqual(BasicComboRules.HitstunFrames[0]);
        return (FP64.Abs(struck.Velocity.x), struck.CurrentHP);
    }

    /// <summary>Basic damage 10, knockback 4, so the opener deals 8 and pushes 4 / (1 + weight).</summary>
    private static CharacterData BuildAttacker() => new() {
        CharacterID = "einstein",
        MaxHP = 100,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 1,
        MaxMoveSpeed = 8f,
        MaxJumpForce = 13f,
        BasicAttackDamage = 10f,
        BasicAttackKnockback = 4f,
        SpecialAttackOne = new AbilityData(),
        SpecialAttackTwo = new AbilityData(),
        MovementAbility = new MovementAbilityData(),
        UltimateAttack = new AbilityData { BaseDamage = 20f }
    };

    private static CharacterData BuildVictim(int maxHP) => new() {
        CharacterID = "joan",
        MaxHP = maxHP,
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

    private static string Describe(FP64 value) =>
        ((double)value.RawValue / FP64.One.RawValue).ToString("0.###");

    private static void Advance(
        FighterSimulation simulation,
        int tick,
        sbyte p1MoveX = 0,
        GameplayButtons p1Buttons = GameplayButtons.None,
        sbyte p2MoveX = 0,
        GameplayButtons p2Buttons = GameplayButtons.None) {
        simulation.Advance(
            new PlayerInputFrame {
                Tick = (uint)tick,
                MoveX = p1MoveX,
                Held = p1Buttons,
                Pressed = p1Buttons
            },
            new PlayerInputFrame {
                Tick = (uint)tick,
                MoveX = p2MoveX,
                Held = p2Buttons,
                Pressed = p2Buttons
            });
    }
}
