using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Environment;
using FTT.FighterSim;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 13 W5 — Harriet Tubman's authored content against the design
/// (character review P01, design §5 "The Conductor", ABILITY_DATA). Replaces the
/// retired Pocahontas content suites: stats, the four abilities, both modes'
/// Conductor's Call reach, the Ultimate's 76, the Crossing grid, and the
/// manifest / presentation hook-up.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TubmanContentTests {

    private static CharacterData Tubman() =>
        AuthoredResources.Load<CharacterData>("res://resources/Characters/tubman_data.tres");

    [TestCase]
    public void TubmanCarriesTheDesignStatLine() {
        CharacterData data = Tubman();
        AssertObject(data).IsNotNull();
        AssertThat(data.CharacterID).IsEqual("tubman");
        AssertThat(data.DisplayNameKey).IsEqual("character_tubman_name");
        AssertThat(data.MaxHP).IsEqual(100);
        AssertFloat(data.Weight).IsEqual(1.0f);
        AssertFloat(data.MaxMoveSpeed).IsEqual(6.75f);
        AssertFloat(data.AirControlMultiplier).IsEqual(0.55f);
        AssertFloat(data.BasicAttackDamage).IsEqual(5.5f);
        AssertThat(data.MaxJumpCount).IsEqual(2);
        // Jump ordering (swap knock-on): Lincoln 11.5 is the floor and
        // Cleopatra 12.75 the ceiling; Tubman sits at 12.5.
        AssertFloat(data.MaxJumpForce).IsEqual(12.5f);
        AssertObject(data.SpriteFramesResource).IsNotNull();
        AssertObject(data.CharacterPortrait).IsNotNull();
        // Template string profile: no BasicComboRules row of her own.
        AssertObject(BasicComboRules.StringProfileFor("tubman")).IsSame(BasicComboRules.TemplateStringProfile);
        AssertThat(CharacterRoster.Contains("tubman")).IsTrue();
        AssertThat(CharacterRoster.Contains("pocahontas")).IsFalse();
    }

    [TestCase]
    public void TheFourAbilitiesMatchAbilityData() {
        CharacterData data = Tubman();
        AbilityData call = data.SpecialAttackOne;
        AssertThat(call.AbilityID).IsEqual("tubman_conductors_call");
        AssertFloat(call.BaseDamage).IsEqual(18f);
        AssertThat(call.StartupFrames).IsEqual(12);
        AssertThat(call.RecoveryFrames).IsEqual(18);
        AssertFloat(call.CooldownDuration).IsEqual(9f);
        AssertThat(call.BlockClass).IsEqual(BlockClass.Special);
        AssertThat(call.Launches).IsFalse();
        // Five units along the ground in both modes: Story sweeps speed ×
        // active frames; the sim spawns one unit ahead and flies speed × life.
        float storyReachUnits = call.ProjectileSpeed * call.ActiveFrames / 60f / KitMotionRules.StoryPixelsPerUnit;
        AssertFloat(storyReachUnits).IsEqualApprox(5f, 0.001f);
        float simReachUnits = 1f + call.ProjectileSpeed / KitMotionRules.StoryPixelsPerUnit * call.ProjectileLifetime;
        AssertFloat(simReachUnits).IsEqualApprox(5f, 0.001f);

        AbilityData foresight = data.SpecialAttackTwo;
        AssertThat(foresight.AbilityID).IsEqual("tubman_foresight");
        AssertFloat(foresight.BaseDamage).IsEqual(20f);
        AssertThat(foresight.StartupFrames).IsEqual(4);
        AssertThat(foresight.ActiveFrames).IsEqual(20);
        AssertThat(foresight.RecoveryFrames).IsEqual(24);
        AssertFloat(foresight.CooldownDuration).IsEqual(10f);
        AssertThat(foresight.Launches).IsTrue();
        AssertFloat(TubmanKitRules.ForesightAnswerRangeUnits).IsEqual(2.5f);

        var leap = (MovementAbilityData)data.MovementAbility;
        AssertThat(leap.AbilityID).IsEqual("tubman_north_star_leap");
        AssertFloat(leap.DistanceMoved / KitMotionRules.StoryPixelsPerUnit).IsEqualApprox(4f, 0.001f);
        AssertThat(Mathf.RoundToInt(leap.MovementDuration * 60f)).IsEqual(18);
        AssertFloat(leap.CooldownDuration).IsEqual(5f);
        AssertFloat(TubmanKitRules.NorthStarLeapLedgeSnapBonusUnits).IsEqual(0.5f);

        AbilityData ultimate = data.UltimateAttack;
        AssertThat(ultimate.AbilityID).IsEqual("tubman_freedom_line");
        AssertFloat(ultimate.UltimateImpactTotal).IsEqual(76f);
        AssertThat(ultimate.HitCount).IsEqual(7);
        AssertFloat(ultimate.BaseDamage).IsEqual(8f);
        AssertFloat(ultimate.FinaleDamage).IsEqual(20f);
        AssertThat(ultimate.ActivationShape).IsEqual(UltimateActivationShape.Projectile);
        AssertFloat(ultimate.ActivationRange / KitMotionRules.StoryPixelsPerUnit).IsEqualApprox(6f, 0.001f);
    }

    [TestCase]
    public void TheCrossingGridReusesTheSwapReadyShapeAndPrices() {
        var grid = AuthoredResources.Load<ResonanceGridData>("res://resources/Resonance/tubman_grid.tres");
        AssertObject(grid).IsNotNull();
        AssertThat(grid.CharacterID).IsEqual("tubman");
        AssertThat(grid.Nodes.Length).IsEqual(9);
        int total = 0;
        ResonanceNodeData starGuide = null, ward = null;
        foreach (ResonanceNodeData node in grid.Nodes) {
            total += node.UnlockCost;
            AssertThat(node.NodeID.StartsWith("tubman_")).IsTrue();
            if (node.NodeID == "tubman_star_guide") starGuide = node;
            if (node.NodeID == "tubman_north_star_ward") ward = node;
        }
        AssertThat(total).IsEqual(975);
        AssertObject(starGuide).IsNotNull();
        AssertThat(starGuide.Type).IsEqual(ResonanceNodeType.Traversal);
        AssertThat(starGuide.PrerequisiteMode).IsEqual(PrerequisiteMode.Any);
        AssertThat(starGuide.AbilityModifierKey).IsEqual(FTT.Characters.Abilities.TubmanNorthStarLeap.StarGuidePerkKey);
        AssertObject(ward).IsNotNull();
        AssertThat(ward.AbilityModifierKey).IsEqual(FTT.Characters.Abilities.TubmanNorthStarLeap.NorthStarWardPerkKey);
        // North Star Ward is the third ResonanceBarrier source (A11).
        AssertThat(StoryDefenseRules.IsResonanceBarrierSource(StoryShieldEffect.NorthStarWard)).IsTrue();
    }

    [TestCase]
    public void TheManifestLevel0VariantAndLocalizationCoverTubman() {
        TranslationServer.SetLocale("en");
        foreach (string key in new[] {
                     "character_tubman_name",
                     "ability_tubman_conductors_call_name", "ability_tubman_foresight_name",
                     "ability_tubman_north_star_leap_name", "ability_tubman_freedom_line_name",
                     "resonance_tubman_star_guide_name", "resonance_tubman_north_star_ward_name",
                     "hero_name_tubman", "hero_address_tubman", "hero_possessive_tubman", "hero_home_era_tubman",
                     "hero_accept_line_tubman", "hero_farewell_line_tubman", "dlg_l00_intro_2_tubman" }) {
            AssertThat(TranslationServer.Translate(key).ToString())
                .OverrideFailureMessage($"{key} does not resolve through the compiled translation.")
                .IsNotEqual(key);
        }
        var level0 = AuthoredResources.Load<FTT.UI.DialogueSetData>("res://resources/Dialogue/level_00_dialogue.tres");
        FTT.UI.DialogueSequenceData intro = level0.FindSequence("level_00.intro", "tubman");
        AssertObject(intro).IsNotNull();
        AssertThat(intro.DialogueID).IsEqual("level_00.intro@tubman");
        AssertThat(System.Array.IndexOf(intro.LineKeys, "dlg_l00_intro_2_tubman") >= 0).IsTrue();
        AssertObject(level0.FindSequence("level_00.intro", "pocahontas").DialogueID)
            .IsEqual("level_00.intro");
    }
}
