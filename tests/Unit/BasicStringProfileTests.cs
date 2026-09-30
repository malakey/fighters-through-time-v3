using FTT.Combat;
using FTT.FighterSim;
using System.Collections.Generic;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Pins the V7.1 per-character basic-string profiles ("Normals Are the
/// Character", applied 2026-08-22): the damage-shape anchor, the authored
/// speed and reach bands, the universal chassis invariants, and the Story/sim
/// profile mapping that keeps both modes on the same rulebook.
/// </summary>
[TestSuite]
public class BasicStringProfileTests {

    /// <summary>
    /// <b>Deliberately a literal list, and the one exception in the suite.</b>
    /// Package 11 A6b repointed every other roster array at
    /// <c>FTT.Core.CharacterRoster</c>, but this suite is pure C# — no
    /// <c>[RequireGodotRuntime]</c> — because it tests <c>BasicComboRules</c>,
    /// which has no Godot dependency and should keep running in the fast host.
    /// <c>CharacterRoster</c> reads the content manifest through Godot's
    /// <c>FileAccess</c>, so touching it from here is an access violation that
    /// takes the whole test host down (0xC0000005, observed). Annotating the
    /// suite with the Godot runtime purely to read a roster would couple a pure
    /// rules test to the engine for no gain. A roster addition needs one line
    /// here; the manifest-backed suites cover the mandate.
    /// </summary>
    private static readonly string[] RosterIDs = {
        "einstein", "joan", "leonardo", "lincoln", "cleopatra",
        "tesla", "shakespeare", "mozart", "tubman"
    };

    [TestCase]
    public void EveryProfileSumsToTheThreePointThreeAnchor() {
        // A full string must stay 3.3x BasicAttackDamage for every character —
        // the entire special/ultimate damage-anchor economy is priced on it.
        foreach (string id in RosterIDs) {
            BasicStringProfile profile = BasicComboRules.StringProfileFor(id);
            int sum = profile.DamageTenths[0] + profile.DamageTenths[1] + profile.DamageTenths[2];
            AssertThat(sum).IsEqual(33);
        }
    }

    [TestCase]
    public void EveryProfileStaysInsideTheAuthoredBands() {
        foreach (string id in RosterIDs) {
            BasicStringProfile profile = BasicComboRules.StringProfileFor(id);
            // Speed: ground opener 5-8, ground finisher 14-17 (17 is the cap —
            // hit 2's 40-frame hitstun must still cover the finisher link).
            AssertThat(profile.GroundStartupFrames[0] >= 5 && profile.GroundStartupFrames[0] <= 8).IsTrue();
            AssertThat(profile.GroundStartupFrames[2] >= 14 && profile.GroundStartupFrames[2] <= 17).IsTrue();
            AssertThat(profile.AerialStartupFrames[0] >= 4 && profile.AerialStartupFrames[0] <= 7).IsTrue();
            AssertThat(profile.AerialStartupFrames[2] >= 11 && profile.AerialStartupFrames[2] <= 14).IsTrue();
            // Reach: +/-20% width, height never below template.
            AssertThat(profile.ReachWidthPercent >= 85 && profile.ReachWidthPercent <= 120).IsTrue();
            AssertThat(profile.ReachHeightPercent >= 100 && profile.ReachHeightPercent <= 115).IsTrue();
        }
    }

    [TestCase]
    public void TheChassisStaysUniversal() {
        // Hit 2's startup is part of the shared chassis: the guaranteed
        // hit 1 -> hit 2 link is a roster-wide readability contract.
        foreach (string id in RosterIDs) {
            BasicStringProfile profile = BasicComboRules.StringProfileFor(id);
            AssertThat(profile.GroundStartupFrames[1]).IsEqual(BasicComboRules.GroundStartupFrames[1]);
            AssertThat(profile.AerialStartupFrames[1]).IsEqual(BasicComboRules.AerialStartupFrames[1]);
        }
    }

    [TestCase]
    public void UnknownAndNullCharacterIDsResolveToTheTemplate() {
        AssertObject(BasicComboRules.StringProfileFor(null))
            .IsSame(BasicComboRules.TemplateStringProfile);
        AssertObject(BasicComboRules.StringProfileFor("time_bandit"))
            .IsSame(BasicComboRules.TemplateStringProfile);
        BasicStringProfile template = BasicComboRules.TemplateStringProfile;
        AssertThat(template.GroundStartupFrames[0]).IsEqual(BasicComboRules.GroundStartupFrames[0]);
        AssertThat(template.GroundStartupFrames[2]).IsEqual(BasicComboRules.GroundStartupFrames[2]);
        AssertThat(template.DamageTenths[0] + template.DamageTenths[1] + template.DamageTenths[2]).IsEqual(33);
    }

    [TestCase]
    public void SimEnumMappingMatchesTheStoryRulebook() {
        // The Fighter sim indexes profiles by FighterCharacterID; a reorder of
        // the enum or the cache array must fail here, not in a desync.
        AssertObject(FighterBasicAttackRules.StringProfileFor((int)FighterCharacterID.Einstein))
            .IsSame(BasicComboRules.StringProfileFor("einstein"));
        AssertObject(FighterBasicAttackRules.StringProfileFor((int)FighterCharacterID.Joan))
            .IsSame(BasicComboRules.StringProfileFor("joan"));
        AssertObject(FighterBasicAttackRules.StringProfileFor((int)FighterCharacterID.Leonardo))
            .IsSame(BasicComboRules.StringProfileFor("leonardo"));
        AssertObject(FighterBasicAttackRules.StringProfileFor((int)FighterCharacterID.Lincoln))
            .IsSame(BasicComboRules.StringProfileFor("lincoln"));
        AssertObject(FighterBasicAttackRules.StringProfileFor((int)FighterCharacterID.Cleopatra))
            .IsSame(BasicComboRules.StringProfileFor("cleopatra"));
        AssertObject(FighterBasicAttackRules.StringProfileFor((int)FighterCharacterID.Tesla))
            .IsSame(BasicComboRules.StringProfileFor("tesla"));
        AssertObject(FighterBasicAttackRules.StringProfileFor((int)FighterCharacterID.Shakespeare))
            .IsSame(BasicComboRules.StringProfileFor("shakespeare"));
        AssertObject(FighterBasicAttackRules.StringProfileFor((int)FighterCharacterID.Mozart))
            .IsSame(BasicComboRules.StringProfileFor("mozart"));
        AssertObject(FighterBasicAttackRules.StringProfileFor((int)FighterCharacterID.Tubman))
            .IsSame(BasicComboRules.StringProfileFor("tubman"));
        // Out-of-range sim IDs (enemy shots use -1) get the template.
        AssertObject(FighterBasicAttackRules.StringProfileFor(-1))
            .IsSame(BasicComboRules.TemplateStringProfile);
        AssertObject(FighterBasicAttackRules.StringProfileFor(99))
            .IsSame(BasicComboRules.TemplateStringProfile);
    }
}
