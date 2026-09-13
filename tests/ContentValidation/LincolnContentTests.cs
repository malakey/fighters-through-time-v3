using System.Collections.Generic;
using FTT.Combat;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Canonical Lincoln content contracts: authored resource numbers match the
/// design Section 5 Rail-Splitter specification, and the Resonance grid
/// authors the three Story-only major perk keys the kit code consumes.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class LincolnContentTests {

    [TestCase]
    public void EmancipatorResourceIsAForwardGroundWaveWithKnockUp() {
        var data = FTT.Core.AuthoredResources.Load<AbilityData>("res://resources/Abilities/lincoln/special_1.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.ExecutionType).IsEqual(AbilityExecutionType.Area);
        AssertThat(data.BaseDamage).IsEqual(40f);
        // V7 band: the roster's biggest single hit sits at the top of the
        // 6-14 s cooldown band with haymaker frames (design §5).
        AssertThat(data.CooldownDuration).IsEqual(13f);
        AssertThat(data.StartupFrames).IsEqual(16);
        // Single-pulse Fighter zone lifetime plus a dominant upward launch.
        AssertThat(data.Lifetime).IsEqual(0.25f);
        AssertThat(data.KnockbackForce.Y < 0f).IsTrue();
        AssertThat(Mathf.Abs(data.KnockbackForce.Y) > Mathf.Abs(data.KnockbackForce.X)).IsTrue();
        // Story wave travel: 200 px/s over 1.5 s of forward floor travel.
        AssertThat(data.ProjectileSpeed).IsEqual(200f);
        AssertThat(data.ProjectileLifetime).IsEqual(1.5f);
    }

    [TestCase]
    public void SplittingStrikeResourceDealsThirtySixMeleeDamage() {
        var data = FTT.Core.AuthoredResources.Load<AbilityData>("res://resources/Abilities/lincoln/special_2.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.ExecutionType).IsEqual(AbilityExecutionType.Melee);
        AssertThat(data.BaseDamage).IsEqual(36f);
        AssertThat(data.CooldownDuration).IsEqual(12f);
    }

    [TestCase]
    public void RailChargeResourceIsAThreeSecondArmoredDash() {
        var data = ResourceLoader.Load<MovementAbilityData>("res://resources/Abilities/lincoln/movement.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.MovementType).IsEqual(MovementType.Dash);
        AssertThat(data.MovementDuration).IsEqual(3f);
        AssertThat(data.GrantsHyperArmor).IsTrue();
        AssertThat(data.CooldownDuration).IsEqual(5f);
        AssertThat(data.MovementSpeed > 0f).IsTrue();
    }

    [TestCase]
    public void LincolnGridAuthorsTheThreeMajorPerkKeysTheKitConsumes() {
        var grid = FTT.Core.AuthoredResources.Load<ResonanceGridData>("res://resources/Resonance/lincoln_grid.tres");
        AssertObject(grid).IsNotNull();
        var majorKeys = new HashSet<string>();
        foreach (ResonanceNodeData node in grid.Nodes) {
            if (!string.IsNullOrWhiteSpace(node.AbilityModifierKey)) majorKeys.Add(node.AbilityModifierKey);
        }
        AssertThat(majorKeys.Contains(
            FTT.Characters.Abilities.LincolnEmancipator.ExecutiveOrderPerkKey)).IsTrue();
        AssertThat(majorKeys.Contains(
            FTT.Characters.Abilities.LincolnRailCharge.HomesteadBulwarkPerkKey)).IsTrue();
        AssertThat(majorKeys.Contains(
            FTT.Characters.Abilities.LincolnSplittingStrike.KineticSplittingPerkKey)).IsTrue();
        // Package 11 A4: the V7.6 traversal node rides the same
        // AbilityModifierKey plumbing as the three Majors.
        AssertThat(majorKeys.Contains(
            FTT.Characters.Abilities.LincolnRailCharge.RailBreakerPerkKey)).IsTrue();
    }
}
