using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.UI;
using System.Collections.Generic;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// V7.3 Fighter Onboarding: the per-character Move List screen. What matters
/// here is the single-source rule — every number the screen displays is read
/// live from <see cref="BasicComboRules"/> / <see cref="UniversalMovementRules"/>
/// or the character's authored .tres, never authored a second time — and that
/// the screen carries no font-size overrides (the UI-scale contract).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class MoveListScreenTests {

    /// <summary>
    /// Package 11 A6b: the roster is the content manifest, never a literal
    /// cast list (design §2's standing "the roster will grow" mandate). A
    /// duplicated array here is exactly what blocks a roster addition — the
    /// content would be complete and this suite would still fail.
    /// </summary>
    private static readonly IReadOnlyList<string> RosterIDs = FTT.Core.CharacterRoster.IDs;

    [TestCase]
    public void TheScreenBuildsForAllNineRosterIDsWithoutError() {
        MoveListScreen screen = Mount();
        try {
            foreach (string id in RosterIDs) {
                screen.Populate(id);
                AssertThat(screen.CharacterID).IsEqual(id);
                var sections = screen.Root.GetNode<VBoxContainer>("Center/Panel/Layout/Scroll/Sections");
                AssertThat(sections.GetChildCount())
                    .OverrideFailureMessage($"{id}: the move list built no rows.")
                    .IsGreater(10);
                var name = screen.Root.GetNode<Label>("Center/Panel/Layout/Header/HeaderText/CharacterName");
                AssertThat(name.Text.Trim().Length > 0)
                    .OverrideFailureMessage($"{id}: the header has no character name.")
                    .IsTrue();
                // The four ability rows come straight off the authored .tres.
                foreach (string slot in new[] {
                    "movelist_slot_special1", "movelist_slot_special2",
                    "movelist_slot_movement", "movelist_slot_ultimate"
                }) {
                    AssertObject(sections.GetNodeOrNull<HBoxContainer>($"Ability_{slot}"))
                        .OverrideFailureMessage($"{id}: missing ability row for {slot}.")
                        .IsNotNull();
                }
                // CM-MOVE (decision 5.2, 2026-10-16): the Fighter ledge-options row,
                // filled from BasicComboRules' ledge section; never in Story mode.
                var ledge = sections.GetNodeOrNull<Label>("LedgeOptions");
                AssertObject(ledge).OverrideFailureMessage($"{id}: missing the ledge-options row.").IsNotNull();
                AssertThat(ledge.Text).IsEqual(string.Format(
                    TranslationServer.Translate("movelist_ledge_options").ToString(),
                    BasicComboRules.LedgeNeutralClimbFrames, BasicComboRules.LedgeNeutralClimbIntangibleFrames,
                    BasicComboRules.LedgeActionClimbFrames, BasicComboRules.LedgeAttackStartupFrames,
                    BasicComboRules.LedgeAttackActiveFrames, BasicComboRules.LedgeAttackRecoveryFrames,
                    BasicComboRules.LedgeFirstGrabIntangibleFrames));
                screen.Populate(id, MoveListScreen.MoveListMode.Story);
                AssertObject(screen.Root.GetNode<VBoxContainer>("Center/Panel/Layout/Scroll/Sections")
                    .GetNodeOrNull<Label>("LedgeOptions"))
                    .OverrideFailureMessage($"{id}: the Story move list must not show the Fighter ledge row.")
                    .IsNull();
            }
        } finally {
            screen.Free();
        }
    }

    /// <summary>
    /// The exemplar single-source pin: the displayed hit-1 row is exactly the
    /// localized format filled from BasicComboRules' authored string profile —
    /// Joan's 5-frame opener, the shared active/recovery frames, and the damage
    /// shape applied to her authored BasicAttackDamage.
    /// </summary>
    [TestCase]
    public void TheDisplayedHitOneStartupEqualsTheRulebookProfile() {
        TranslationServer.SetLocale("en");
        MoveListScreen screen = Mount();
        try {
            screen.Populate("joan");
            var row = screen.Root.GetNode<VBoxContainer>("Center/Panel/Layout/Scroll/Sections")
                .GetNodeOrNull<Label>("StringHit1");
            AssertObject(row).IsNotNull();

            BasicStringProfile profile = BasicComboRules.StringProfileFor("joan");
            var joan = AuthoredResources.Load<CharacterData>("res://resources/Characters/joan_data.tres");
            string expected = string.Format(
                TranslationServer.Translate("movelist_string_hit").ToString(),
                1,
                profile.GroundStartupFrames[0],
                BasicComboRules.GroundActiveFrames[0],
                BasicComboRules.GroundRecoveryFrames[0],
                joan.BasicAttackDamage * profile.DamageTenths[0] / 10f);
            AssertThat(row.Text).IsEqual(expected);
            // Joan's authored 5-frame opener is what proves the profile (not the
            // 6-frame template) reached the screen.
            AssertThat(profile.GroundStartupFrames[0]).IsEqual(5);
        } finally {
            screen.Free();
        }
    }

    [TestCase]
    public void NoLabelOnTheScreenCarriesAFontSizeOverride() {
        MoveListScreen screen = Mount();
        try {
            screen.Populate("einstein");
            var offenders = new System.Collections.Generic.List<string>();
            SweepLabels(screen.Root, offenders);
            if (offenders.Count > 0) AssertThat(string.Join(" | ", offenders)).IsEqual("");
        } finally {
            screen.Free();
        }
    }

    private static void SweepLabels(Node node, System.Collections.Generic.List<string> offenders) {
        if (node is Label label && label.HasThemeFontSizeOverride("font_size")) {
            offenders.Add(label.GetPath().ToString());
        }
        if (node is RichTextLabel rich && rich.HasThemeFontSizeOverride("normal_font_size")) {
            offenders.Add(rich.GetPath().ToString());
        }
        for (int index = 0; index < node.GetChildCount(); index++) {
            SweepLabels(node.GetChild(index), offenders);
        }
    }

        /// <summary>
    /// MM-TEXT (O25, 5.3): a Special row shows the Momentum cost line read from the rulebook and no cooldown; the movement
    /// ability keeps damage and cooldown; a Defy row is in both modes, with the rulebook's cost.
    /// </summary>
    [TestCase]
    public void SpecialsShowTheMomentumLineAndBothModesShowADefyRow() {
        TranslationServer.SetLocale("en");
        MoveListScreen screen = Mount();
        try {
            foreach (MoveListScreen.MoveListMode mode in new[] { MoveListScreen.MoveListMode.Fighter, MoveListScreen.MoveListMode.Story }) {
                screen.Populate("joan", mode);
                var sections = screen.Root.GetNode<VBoxContainer>("Center/Panel/Layout/Scroll/Sections");
                var defy = sections.GetNodeOrNull<Label>("Defy");
                AssertObject(defy).OverrideFailureMessage($"{mode}: no Defy row.").IsNotNull();
                AssertThat(defy.Text.Contains(BasicComboRules.DefyInfluenceCost.ToString())).IsTrue();
            }
            screen.Populate("joan", MoveListScreen.MoveListMode.Fighter);
            var joan = AuthoredResources.Load<CharacterData>("res://resources/Characters/joan_data.tres");
            string line = MoveListScreen.SpecialStatsLine(AbilitySlot.Special1, joan.SpecialAttackOne);
            string expected = string.Format(
                TranslationServer.Translate("movelist_ability_momentum").ToString(),
                joan.SpecialAttackOne.BaseDamage,
                (float)BasicComboRules.MomentumSpecialCostUnits / BasicComboRules.MomentumUnitsPerPip,
                BasicComboRules.SpecialReuseDelayFrames / 60f);
            AssertThat(line).IsEqual(expected);
            AssertThat(line.Contains("cooldown")).IsFalse();
            AssertThat(MoveListScreen.SpecialStatsLine(AbilitySlot.MovementAbility, joan.MovementAbility).Contains("cooldown")).IsTrue();
        } finally {
            screen.Free();
        }
    }

    private static MoveListScreen Mount() {
        var screen = new MoveListScreen { Name = "MoveListScreenUnderTest" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(screen);
        return screen;
    }
}
