using System.Threading.Tasks;
using FTT.Core;
using FTT.UI;
using FTT.UI.Pulp;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// The level results' rank lines (gameplay review §1.9): the letter, the bonus (which joins
/// the total, because it banks) and what the letter was earned on, after the tier bonus.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class LevelRankResultsPanelTests {
    private const string Layout = "Shade/Panel/Layout/";

    private static Node CreateHost(string name) {
        var tree = (SceneTree)Engine.GetMainLoop();
        var host = new Node { Name = name };
        tree.Root.AddChild(host);
        return host;
    }

    private static LevelResultsPanel AddPanel(Node host) {
        LevelResultsPanel panel = LevelResultsPanel.CreateDefault();
        host.AddChild(panel);
        return panel;
    }

    [TestCase]
    public async Task TheRankLinesShowTheLetterTheBonusAndTheDetailAndTheTotalAddsTheBonus() {
        Node host = CreateHost("LevelRankPanelHost");
        float originalScale = UIPalette.AppliedUiScale;
        try {
            LevelResultsPanel panel = AddPanel(host);
            var rank = new LevelRankResult(LevelRankLetter.A, 80.0, 35, 38, 62);
            panel.ShowResults("orleans_level_title", 15, 30, 25, 10, 0, 12, 8, 35, rank, 120f, 1);

            var letter = panel.GetNode<Label>(Layout + "RankLine");
            var dust = panel.GetNode<Label>(Layout + "RankDustLine");
            var detail = panel.GetNode<Label>(Layout + "RankDetailLine");
            AssertThat(letter.Visible).IsTrue();
            AssertThat(letter.Text).IsEqual(string.Format(TranslationServer.Translate("results_rank"), "A"));
            AssertThat(dust.Visible).IsTrue();
            AssertThat(dust.Text).IsEqual(string.Format(TranslationServer.Translate("results_rank_dust"), 35));
            AssertThat(detail.Visible).IsTrue();
            AssertThat(detail.Text).IsEqual(string.Format(TranslationServer.Translate("results_rank_detail"), 38, "62%"));

            // 15 + 30 + 25 + 10 + 8 + 35 − 12 = 111.
            AssertThat(panel.GetNode<Label>(Layout + "DustEarned").Text.Contains("111")).IsTrue();

            // The fullest panel a run can show: every line up, with the rank's three, at both UI scales.
            var tree = (SceneTree)Engine.GetMainLoop();
            foreach (float scale in new[] { 1f, 1.4f }) {
                UIPalette.ApplyUiScale(scale);
                panel.ShowResults("orleans_level_title", 140, 60, 25, 30, 100, 40, 15, 35, rank, 612f, 2);
                for (int frame = 0; frame < 6; frame++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                Rect2 screen = panel.GetNode<Control>("Shade").GetGlobalRect();
                Rect2 slab = panel.GetNode<Control>("Shade/Panel").GetGlobalRect();
                AssertThat(screen.Grow(1f).Encloses(slab))
                    .OverrideFailureMessage($"The slab {slab} leaves the screen {screen} at UI scale {scale}.")
                    .IsTrue();
                ResultRow row = detail.GetNode<ResultRow>(ResultRow.NodeName);
                AssertThat(row.Value.GetMinimumSize().X <= row.Size.X)
                    .OverrideFailureMessage($"The rank detail value overruns its row at UI scale {scale}.")
                    .IsTrue();
            }
        } finally {
            UIPalette.ApplyUiScale(originalScale);
            host.Free();
        }
    }

    [TestCase]
    public void AnUntimedRankShowsNoSpeedAndThePaneOrdersTheLinesAfterTheTierBonus() {
        Node host = CreateHost("LevelRankPanelUntimedHost");
        try {
            LevelResultsPanel panel = AddPanel(host);
            var rank = new LevelRankResult(LevelRankLetter.S, 100.0, 50, 0, LevelRankRules.SpeedPercentUntimed);
            panel.ShowResults("florence_level_title", 15, 30, 25, 10, 0, 0, 0, 50, rank, 120f, 1);

            var detail = panel.GetNode<Label>(Layout + "RankDetailLine");
            AssertThat(detail.Text.Contains("%, ")).IsFalse();
            AssertThat(detail.Text.Contains("—")).IsTrue();

            int tier = panel.GetNode<Label>(Layout + "DustTierBonus").GetIndex();
            AssertThat(panel.GetNode<Label>(Layout + "RankLine").GetIndex()).IsEqual(tier + 1);
            AssertThat(panel.GetNode<Label>(Layout + "RankDustLine").GetIndex()).IsEqual(tier + 2);
            AssertThat(panel.GetNode<Label>(Layout + "RankDetailLine").GetIndex()).IsEqual(tier + 3);
            AssertThat(panel.GetNode<Label>(Layout + "DustLosses").GetIndex() > tier + 3).IsTrue();
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void WithNoRankTheLinesStayHiddenAndAnEarlierRankDoesNotLinger() {
        Node host = CreateHost("LevelRankPanelHiddenHost");
        try {
            LevelResultsPanel panel = AddPanel(host);
            var letter = panel.GetNode<Label>(Layout + "RankLine");
            var dust = panel.GetNode<Label>(Layout + "RankDustLine");
            var detail = panel.GetNode<Label>(Layout + "RankDetailLine");

            var rank = new LevelRankResult(LevelRankLetter.S, 100.0, 50, 0, 100);
            panel.ShowResults("orleans_level_title", 15, 30, 25, 10, 0, 0, 0, 50, rank, 120f, 1);
            AssertThat(letter.Visible).IsTrue();

            // Level 0's single-total form, and the itemised form with no rank.
            panel.ShowResults("level_00_title", 240, 187f, 3);
            AssertThat(letter.Visible).IsFalse();
            AssertThat(dust.Visible).IsFalse();
            AssertThat(detail.Visible).IsFalse();

            panel.ShowResults("orleans_level_title", 15, 30, 25, 10, 12, 8, 120f, 1);
            AssertThat(letter.Visible).IsFalse();
            AssertThat(dust.Visible).IsFalse();
            AssertThat(detail.Visible).IsFalse();
        } finally {
            host.Free();
        }
    }
}
