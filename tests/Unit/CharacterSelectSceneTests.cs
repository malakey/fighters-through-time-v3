using System.Collections.Generic;
using FTT.Core;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 8 B4. The authored Fighter Mode character/stage/rules screen.
///
/// <para>The defect this suite mainly exists for: the nine character tiles used to
/// be <see cref="PanelContainer"/>s driven by a <c>GuiInput</c> mouse handler, so
/// a controller or keyboard player could not pick a fighter at all. They are now
/// real focusable <see cref="Button"/>s in the authored grid. The suite also pins
/// that the conversion preserved every session write, the stage routing through
/// the catalog, and all four frequency bands.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CharacterSelectSceneTests {

    private const string ScenePath = "res://scenes/menus/CharacterSelect.tscn";
    private const string Root = "Center/Root/";

    [TestCase]
    public void TheAuthoredSceneCarriesEveryControlTheScriptBindsByPath() {
        CharacterSelectScreen screen = Open(out Node host);
        try {
            AssertThat(screen.GetNodeOrNull<ColorRect>("Background") != null).IsTrue();
            foreach (string path in new[] {
                         "Title", "Grid", "SelectedName", "Stats",
                         "OpponentRow/PreviousButton", "OpponentRow/OpponentLabel",
                         "OpponentRow/NextButton",
                         "ModeRow/LocalHumanToggle", "ModeRow/CpuDifficulty",
                         "StageRow/StageSelect", "StageRow/StagePreview",
                         "RulesRow/MatchMode", "RulesRow/StockCount", "RulesRow/TimeLimit",
                         "RulesRow/ItemFrequency", "RulesRow/HazardFrequency",
                         "ButtonRow/BackButton", "ButtonRow/FightButton" }) {
                AssertThat(screen.GetNodeOrNull<Control>(Root + path) != null)
                    .OverrideFailureMessage($"Missing authored control {path}")
                    .IsTrue();
            }
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void TheNineRosterTilesAreFocusableButtonsRatherThanMouseOnlyPanels() {
        CharacterSelectScreen screen = Open(out Node host);
        try {
            var grid = screen.GetNode<GridContainer>(Root + "Grid");
            AssertThat(grid.GetChildCount()).IsEqual(9);
            for (int index = 0; index < 9; index++) {
                var tile = grid.GetNodeOrNull<Button>($"CharacterButton{index}");
                AssertThat(tile != null)
                    .OverrideFailureMessage($"Tile {index} is not a Button")
                    .IsTrue();
                AssertThat(tile.FocusMode)
                    .OverrideFailureMessage($"Tile {index} cannot take focus")
                    .IsEqual(Control.FocusModeEnum.All);
                AssertThat(tile.Text.Length > 0)
                    .OverrideFailureMessage($"Tile {index} has no name")
                    .IsTrue();
            }
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void EveryInteractiveControlJoinsOneFocusChainAndTheRosterTakesInitialFocus() {
        CharacterSelectScreen screen = Open(out Node host);
        try {
            IReadOnlyList<Control> chain = screen.FocusChain;
            // 9 tiles + 2 opponent arrows + toggle + cpu difficulty + stage select
            // + match mode + 2 spin-box editors + 2 frequency selects + back + fight.
            AssertThat(chain.Count).IsEqual(21);

            // A SpinBox keeps its editable LineEdit as an INTERNAL child, which
            // GetChild does not return, so the stock count and time limit were
            // invisible to the generic collector and silently unreachable by keyboard
            // and controller. B4 worked around it with the explicit chain above; C1
            // folded the splice into FocusChainBuilder.Collect, so the two now agree.
            // Asserting the membership rather than only the count is what keeps this
            // proving something if either side is ever rebuilt.
            List<Control> collected = FocusChainBuilder.Collect(screen);
            AssertThat(collected.Count).IsEqual(chain.Count);
            AssertThat(collected.Contains(screen.GetNode<SpinBox>(Root + "RulesRow/StockCount").GetLineEdit()))
                .OverrideFailureMessage("The stock-count editor is not reachable by focus.").IsTrue();
            AssertThat(collected.Contains(screen.GetNode<SpinBox>(Root + "RulesRow/TimeLimit").GetLineEdit()))
                .OverrideFailureMessage("The time-limit editor is not reachable by focus.").IsTrue();

            foreach (Control control in chain) {
                AssertThat(control.FocusMode)
                    .OverrideFailureMessage($"{control.Name} is not focusable")
                    .IsEqual(Control.FocusModeEnum.All);
                bool linked = !control.FocusNeighborTop.IsEmpty || !control.FocusNeighborBottom.IsEmpty;
                AssertThat(linked)
                    .OverrideFailureMessage($"{control.Name} has no focus neighbour")
                    .IsTrue();
            }

            // Focus starts on the roster: the first thing a controller player
            // touches is the choice the screen exists to make.
            AssertThat(screen.GetNode<Button>(Root + "Grid/CharacterButton0").HasFocus()).IsTrue();
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void PressingATileSelectsThatFighterAndRepaintsTheSummary() {
        TranslationServer.SetLocale("en");
        CharacterSelectScreen screen = Open(out Node host);
        try {
            AssertThat(screen.SelectedIndex).IsEqual(0);
            string first = screen.GetNode<Label>(Root + "SelectedName").Text;

            screen.GetNode<Button>(Root + "Grid/CharacterButton4")
                .EmitSignal(BaseButton.SignalName.Pressed);

            AssertThat(screen.SelectedIndex).IsEqual(4);
            string second = screen.GetNode<Label>(Root + "SelectedName").Text;
            AssertThat(second).IsNotEqual(first);
            AssertThat(second).IsNotEqual("fighter_player_selection");
            AssertThat(screen.GetNode<Label>(Root + "Stats").Text)
                .IsNotEqual(TranslationServer.Translate("fighter_stats_unavailable").ToString());
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void TheOpponentCyclerWrapsInBothDirections() {
        CharacterSelectScreen screen = Open(out Node host);
        try {
            AssertThat(screen.OpponentIndex).IsEqual(1);
            var previous = screen.GetNode<Button>(Root + "OpponentRow/PreviousButton");
            var next = screen.GetNode<Button>(Root + "OpponentRow/NextButton");

            previous.EmitSignal(BaseButton.SignalName.Pressed);
            AssertThat(screen.OpponentIndex).IsEqual(0);
            previous.EmitSignal(BaseButton.SignalName.Pressed);
            AssertThat(screen.OpponentIndex).IsEqual(8);
            next.EmitSignal(BaseButton.SignalName.Pressed);
            AssertThat(screen.OpponentIndex).IsEqual(0);
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void AllFourFrequencyBandsSurviveTheConversionAndDefaultToHigh() {
        CharacterSelectScreen screen = Open(out Node host);
        try {
            foreach (string path in new[] { "RulesRow/ItemFrequency", "RulesRow/HazardFrequency" }) {
                var select = screen.GetNode<OptionButton>(Root + path);
                AssertThat(select.ItemCount)
                    .OverrideFailureMessage($"{path} does not offer four bands")
                    .IsEqual(4);
                AssertThat(select.GetSelectedId())
                    .OverrideFailureMessage($"{path} default band")
                    .IsEqual(3);
            }
            AssertThat(screen.GetNode<OptionButton>(Root + "RulesRow/MatchMode").ItemCount).IsEqual(3);
            AssertThat(screen.GetNode<OptionButton>(Root + "ModeRow/CpuDifficulty").ItemCount).IsEqual(3);
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void StageSelectIsPopulatedFromTheCatalogAndRoutesToARealScene() {
        CharacterSelectScreen screen = Open(out Node host);
        try {
            var stages = screen.GetNode<OptionButton>(Root + "StageRow/StageSelect");
            AssertThat(stages.ItemCount > 0).IsTrue();
            AssertThat(stages.Disabled).IsFalse();

            FTT.Environment.FighterStageCatalog catalog = FTT.Environment.FighterStageCatalog.LoadDefault();
            AssertThat(catalog != null).IsTrue();
            for (int index = 0; index < stages.ItemCount; index++) {
                // A raw key here would mean the catalog's DisplayNameKey is missing
                // from the compiled translation table.
                AssertThat(stages.GetItemText(index).StartsWith("stage_"))
                    .OverrideFailureMessage($"Stage row {index} shows a raw key")
                    .IsFalse();
            }
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void TheSelectionWritesTheWholeSessionAndMatchSettingsRoundTrip() {
        if (GameManager.Instance == null) return;
        SessionData original = GameManager.Instance.CurrentSession;
        CharacterSelectScreen screen = Open(out Node host);
        try {
            screen.GetNode<Button>(Root + "Grid/CharacterButton3").EmitSignal(BaseButton.SignalName.Pressed);
            screen.GetNode<Button>(Root + "OpponentRow/NextButton").EmitSignal(BaseButton.SignalName.Pressed);
            screen.GetNode<CheckButton>(Root + "ModeRow/LocalHumanToggle").ButtonPressed = true;
            screen.GetNode<OptionButton>(Root + "ModeRow/CpuDifficulty").Select(2);
            screen.GetNode<OptionButton>(Root + "RulesRow/MatchMode").Select(2);
            screen.GetNode<SpinBox>(Root + "RulesRow/StockCount").Value = 5;
            screen.GetNode<SpinBox>(Root + "RulesRow/TimeLimit").Value = 120;
            screen.GetNode<OptionButton>(Root + "RulesRow/ItemFrequency").Select(0);
            screen.GetNode<OptionButton>(Root + "RulesRow/HazardFrequency").Select(1);

            // ApplySelectionToSession is the seam OnFight uses; pressing Fight for
            // real would change the scene out from under the test runner.
            FTT.Environment.FighterStageData stage = screen.ApplySelectionToSession();
            AssertThat(stage != null).IsTrue();
            AssertThat(ResourceLoader.Exists(stage.ScenePath)).IsTrue();

            SessionData session = GameManager.Instance.CurrentSession;
            AssertThat(session.SelectedCharacterID).IsEqual("lincoln");
            // Opponent starts on index 1 (joan); one Next lands on leonardo.
            AssertThat(session.OpponentCharacterID).IsEqual("leonardo");
            AssertThat(session.FighterOpponentType).IsEqual(FighterOpponentType.LocalHuman);
            AssertThat(session.CpuDifficulty).IsEqual(CpuDifficulty.Hard);
            AssertThat(session.SelectedStageID.Length > 0).IsTrue();

            MatchSettings settings = session.MatchSettings;
            AssertThat(settings.Mode).IsEqual(MatchMode.Hybrid);
            AssertThat(settings.StockCount).IsEqual(5);
            AssertThat(settings.TimeLimit).IsEqual(120f);
            // Off must clear the boolean as well as the band; Low must set both.
            AssertThat(settings.ItemSpawnRate).IsEqual(ChronalOrbFrequency.Off);
            AssertThat(settings.ItemsEnabled).IsFalse();
            AssertThat(settings.HazardRate).IsEqual(HazardTriggerFrequency.Low);
            AssertThat(settings.StageHazardsEnabled).IsTrue();
        } finally {
            Teardown(host);
            GameManager.Instance.CurrentSession = original;
        }
    }

    [TestCase]
    public void StaticCopyIsStoredAsRawTranslationKeysAndResolvesThroughTheCompiledTable() {
        TranslationServer.SetLocale("en");
        CharacterSelectScreen screen = Open(out Node host);
        try {
            AssertThat(screen.GetNode<Label>(Root + "Title").Text).IsEqual("fighter_select_title");
            AssertThat(screen.GetNode<Button>(Root + "ButtonRow/FightButton").Text).IsEqual("fighter_start_match");
            AssertThat(screen.GetNode<Label>(Root + "StageRow/StageLabel").Text).IsEqual("fighter_stage");

            foreach (string key in new[] {
                         "fighter_select_title", "fighter_start_match", "common_back",
                         "fighter_stage", "fighter_items", "fighter_hazards",
                         "fighter_local_human", "fighter_cpu_difficulty",
                         "hud_stocks", "hud_timer" }) {
                AssertThat(TranslationServer.Translate(key).ToString())
                    .OverrideFailureMessage($"{key} does not resolve through the compiled translation")
                    .IsNotEqual(key);
            }
        } finally {
            Teardown(host);
        }
    }

    // ---- helpers ------------------------------------------------------------

    private static CharacterSelectScreen Open(out Node host) {
        host = new Node { Name = "CharacterSelectHost" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        var packed = ResourceLoader.Load<PackedScene>(ScenePath);
        var screen = packed.Instantiate<CharacterSelectScreen>();
        host.AddChild(screen);
        return screen;
    }

    private static void Teardown(Node host) {
        if (!GodotObject.IsInstanceValid(host)) return;
        host.GetParent()?.RemoveChild(host);
        host.Free();
    }
}
