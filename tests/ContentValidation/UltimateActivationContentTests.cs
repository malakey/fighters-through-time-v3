using System.Collections.Generic;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using FTT.Tests.Determinism;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 13 W6 — the ABILITY_DATA Ultimate table (A02 activation strikes,
/// D15 totals) against the eight shipped <c>ultimate.tres</c> files. Both modes
/// read the same resource: Story's scripts walk
/// <see cref="AbilityData.CinematicHitDamage"/> and the Fighter sim reads the
/// loadout projection (<see cref="FighterLoadoutFactory.UltimateData"/>), so the
/// suite pins the table, the projection, and a real-loadout cinematic landing
/// the design total. Pocahontas is deliberately absent (W5 replaces her).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class UltimateActivationContentTests {

    private readonly record struct Row(
        string Character, float Total, float StatusDamage,
        UltimateActivationShape Shape, float RangePixels);

    // ABILITY_DATA.md "Ultimates" (E06, J05, L04, T03, S04, M05, C04, LN05).
    // Tesla's row is its base (no coils); Cleopatra's 78 counts the 12 Venom.
    private static readonly Row[] Table = {
        new("einstein", 78f, 0f, UltimateActivationShape.Projectile, 360f),
        new("joan", 76f, 0f, UltimateActivationShape.Melee, 300f),
        new("leonardo", 74f, 0f, UltimateActivationShape.Projectile, 420f),
        new("tesla", 70f, 0f, UltimateActivationShape.Projectile, 360f),
        new("shakespeare", 76f, 0f, UltimateActivationShape.Projectile, 360f),
        new("mozart", 80f, 0f, UltimateActivationShape.Projectile, 360f),
        new("cleopatra", 78f, 12f, UltimateActivationShape.Projectile, 360f),
        new("lincoln", 70f, 0f, UltimateActivationShape.GroundWave, 300f),
    };

    [TestCase]
    public void EveryUltimateAuthorsItsTableTotalAndActivationStrike() {
        var issues = new List<string>();
        foreach (Row row in Table) {
            AbilityData data = Load(row.Character);
            if (data == null) { issues.Add($"{row.Character}: ultimate.tres did not load"); continue; }
            float statusDamage = data.AppliedStatus == StatusType.Venom
                ? 2f * data.StatusIntensity * Mathf.Floor(data.StatusDuration)
                : 0f;
            if (!Mathf.IsEqualApprox(data.UltimateImpactTotal + statusDamage, row.Total)) {
                issues.Add($"{row.Character}: total {data.UltimateImpactTotal} + {statusDamage} != {row.Total}");
            }
            if (!Mathf.IsEqualApprox(statusDamage, row.StatusDamage)) {
                issues.Add($"{row.Character}: status damage {statusDamage} != {row.StatusDamage}");
            }
            if (data.ActivationShape != row.Shape) issues.Add($"{row.Character}: shape {data.ActivationShape}");
            if (!Mathf.IsEqualApprox(data.ActivationRange, row.RangePixels)) {
                issues.Add($"{row.Character}: range {data.ActivationRange}");
            }
            // A02 provisional timing, identical for every Ultimate until F09.
            if (data.ActivationWindupFrames != UltimateActivationRules.DefaultWindupFrames
                || data.ActivationActiveFrames != UltimateActivationRules.DefaultActiveFrames
                || data.ActivationWhiffRecoveryFrames != UltimateActivationRules.DefaultWhiffRecoveryFrames) {
                issues.Add($"{row.Character}: activation timing {data.ActivationWindupFrames}/"
                    + $"{data.ActivationActiveFrames}/{data.ActivationWhiffRecoveryFrames}");
            }
            // The Story window fits every hit plus the finale.
            if (data.CinematicHitCount * data.DamageTickIntervalFrames > data.ActiveFrames) {
                issues.Add($"{row.Character}: {data.CinematicHitCount} hits do not fit {data.ActiveFrames} active frames");
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void BothModesReadTheSameSequenceFromTheResource() {
        var issues = new List<string>();
        foreach (Row row in Table) {
            AbilityData data = Load(row.Character);
            if (data == null) { issues.Add($"{row.Character}: did not load"); continue; }
            // Story walks the cinematic hit by hit.
            float storySum = 0f;
            for (int hit = 1; hit <= data.CinematicHitCount; hit++) storySum += data.CinematicHitDamage(hit);
            if (!Mathf.IsEqualApprox(storySum, data.UltimateImpactTotal)) {
                issues.Add($"{row.Character}: Story hits sum to {storySum}");
            }
            // The Fighter loadout projects the same numbers, never a restated copy.
            FighterUltimateData sim = FighterLoadoutFactory.UltimateData(data);
            if (sim.HitCount != data.HitCount) issues.Add($"{row.Character}: sim hit count {sim.HitCount}");
            if (sim.TickIntervalFrames != data.DamageTickIntervalFrames) issues.Add($"{row.Character}: sim interval {sim.TickIntervalFrames}");
            if (sim.FinaleDamage != Mathf.RoundToInt(data.FinaleDamage)) issues.Add($"{row.Character}: sim finale {sim.FinaleDamage}");
            if (sim.FinaleLaunches != data.FinaleLaunches) issues.Add($"{row.Character}: sim finale launch");
            if (sim.ActivationShape != (int)data.ActivationShape) issues.Add($"{row.Character}: sim shape");
            if (sim.WindupFrames != data.ActivationWindupFrames
                || sim.ActiveFrames != data.ActivationActiveFrames
                || sim.WhiffRecoveryFrames != data.ActivationWhiffRecoveryFrames) {
                issues.Add($"{row.Character}: sim activation timing");
            }
            if (!Mathf.IsEqualApprox(sim.ActivationRange.ToFloat(), data.ActivationRange / KitMotionRules.StoryPixelsPerUnit)) {
                issues.Add($"{row.Character}: sim range {sim.ActivationRange.ToFloat()}");
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void EachShippedLoadoutLandsItsTableTotalInFighterMode() {
        var issues = new List<string>();
        FighterLoadout dummy = FighterLoadoutFactory.FromCharacterData(BuildDummy());
        foreach (Row row in Table) {
            CharacterData character = AuthoredResources.Load<CharacterData>(
                $"res://resources/Characters/{row.Character}_data.tres");
            if (character == null) { issues.Add($"{row.Character}: character did not load"); continue; }
            var simulation = new FighterSimulation(
                FighterLoadoutFactory.FromCharacterData(character), dummy,
                seed: 1306, spawnDistance: 1, rules: FighterMatchRules.Disabled);
            int tick = UltimateActivationTestKit.Idle(simulation, 0, 20);
            simulation.SeedFighterInfluenceForTest(0, 100);
            simulation.TryGetFighter(1, out FighterStateComponent before);
            tick = UltimateActivationTestKit.CastAndConnect(simulation, tick, out bool connected);
            if (!connected) { issues.Add($"{row.Character}: the activation strike never connected"); continue; }
            // Long enough for every cinematic, the finale and a 3 s Venom.
            UltimateActivationTestKit.Idle(simulation, tick, 420);
            simulation.TryGetFighter(1, out FighterStateComponent after);
            int dealt = before.CurrentHP - after.CurrentHP;
            if (dealt != Mathf.RoundToInt(row.Total)) issues.Add($"{row.Character}: dealt {dealt}, table {row.Total}");
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    private static AbilityData Load(string character) =>
        AuthoredResources.Load<AbilityData>($"res://resources/Abilities/{character}/ultimate.tres");

    /// <summary>A kit-less, 500-HP sparring dummy that never moves on its own.</summary>
    private static CharacterData BuildDummy() => new() {
        CharacterID = "joan",
        MaxHP = 500,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 2,
        MaxMoveSpeed = 7f,
        MaxJumpForce = 12f,
        BasicAttackDamage = 5f,
        BasicAttackKnockback = 0f,
        SpecialAttackOne = new AbilityData(),
        SpecialAttackTwo = new AbilityData(),
        MovementAbility = new MovementAbilityData(),
        UltimateAttack = new AbilityData { BaseDamage = 10f }
    };
}
