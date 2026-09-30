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
        // LN03 (Package 13 W7b): a ground wave (the Projectile execution the
        // sim routes to FighterGroundWave), no longer a static Fighter zone.
        AssertThat(data.ExecutionType).IsEqual(AbilityExecutionType.Projectile);
        AssertThat(data.BaseDamage).IsEqual(40f);
        AssertThat(data.BlockClass).IsEqual(BlockClass.ShieldBreaker);
        // V7 band: the roster's biggest single hit sits at the top of the
        // 6-14 s cooldown band with haymaker frames (design §5).
        AssertThat(data.CooldownDuration).IsEqual(13f);
        AssertThat(data.StartupFrames).IsEqual(16);
        // A dominant upward launch.
        AssertThat(data.Launches).IsTrue();
        AssertThat(data.KnockbackForce.Y < 0f).IsTrue();
        AssertThat(Mathf.Abs(data.KnockbackForce.Y) > Mathf.Abs(data.KnockbackForce.X)).IsTrue();
        // LN03: 5 units along the ground at 10 units/s (600 px/s for 0.5 s).
        AssertThat(data.ProjectileSpeed).IsEqual(600f);
        AssertThat(data.ProjectileLifetime).IsEqual(0.5f);
    }

    [TestCase]
    public void SplittingStrikeResourceDealsThirtySixMeleeDamage() {
        var data = FTT.Core.AuthoredResources.Load<AbilityData>("res://resources/Abilities/lincoln/special_2.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.ExecutionType).IsEqual(AbilityExecutionType.Melee);
        AssertThat(data.BaseDamage).IsEqual(36f);
        AssertThat(data.CooldownDuration).IsEqual(12f);
        // LN03 (Package 13 W7b): a 2.2-unit (132 px) overhead arc from his
        // front, whose authored vector spikes (points down).
        AssertThat(data.HitboxSize.X).IsEqual(132f);
        AssertThat(data.HitboxOffset.X).IsEqual(66f);
        AssertFloat((float)KitReachRules.SplittingStrikeReachUnits * KitMotionRules.StoryPixelsPerUnit)
            .IsEqualApprox(data.HitboxSize.X, 0.01f);
        AssertThat(data.KnockbackForce.Y > 0f).IsTrue();
    }

    [TestCase]
    public void RailChargeResourceIsAShortArmoredBurstThatStopsOnContact() {
        var data = ResourceLoader.Load<MovementAbilityData>("res://resources/Abilities/lincoln/movement.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.MovementType).IsEqual(MovementType.Dash);
        // LN02 (Package 13 W7b): 5 units (300 px) over 30 frames; the 3 s
        // duration is retired. Contact: 6 damage, moderate horizontal
        // knockback, not a launch.
        AssertThat(data.MovementDuration).IsEqual(0.5f);
        AssertThat(data.DistanceMoved).IsEqual(300f);
        AssertThat(data.ActiveFrames).IsEqual(30);
        AssertThat(data.GrantsHyperArmor).IsTrue();
        AssertThat(data.CooldownDuration).IsEqual(5f);
        AssertThat(data.BaseDamage).IsEqual(6f);
        AssertThat(data.Launches).IsFalse();
        AssertThat(data.KnockbackForce.Y).IsEqual(0f);
        AssertThat(data.KnockbackForce.X > 0f).IsTrue();
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
