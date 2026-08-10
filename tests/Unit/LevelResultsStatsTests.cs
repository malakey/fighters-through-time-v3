using FTT.Core;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 8 B1. The level results overlay now reports completion time and
/// rewinds spent alongside the dust, and the two new statistics are tracked by
/// <see cref="StoryManager"/>.
///
/// Neither existed before this change, and the rewind count in particular cannot
/// be derived from what did: <c>ChronalRewindsRemaining</c> is a pool that Easy
/// and Normal refill at every new checkpoint, so differencing it under-reports
/// exactly the runs where the player used the most rewinds. It is counted from
/// the rewind event instead, which is what these cases pin.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class LevelResultsStatsTests {

    private static LevelResultsPanel AddPanel(Node host) {
        LevelResultsPanel panel = LevelResultsPanel.CreateDefault();
        host.AddChild(panel);
        return panel;
    }

    private static Node CreateHost(string name) {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var host = new Node { Name = name };
        tree.Root.AddChild(host);
        return host;
    }

    [TestCase]
    public void TheAuthoredSceneCarriesTheTwoNewStatLinesAndAdoptsTheTheme() {
        // Package 8 B1 deleted the code-built duplicate of this panel: the two
        // layouts had already drifted (the authored button carried no text at all),
        // which is exactly how a stat added to one silently misses the other.
        AssertThat(ResourceLoader.Exists(LevelResultsPanel.SceneResourcePath)).IsTrue();

        Node host = CreateHost("LevelResultsAuthoredHost");
        try {
            LevelResultsPanel panel = AddPanel(host);
            AssertThat(panel.Name.ToString()).IsEqual("LevelResults");

            AssertObject(panel.GetNodeOrNull<Label>("Shade/Panel/Layout/Title")).IsNotNull();
            AssertObject(panel.GetNodeOrNull<Label>("Shade/Panel/Layout/DustEarned")).IsNotNull();
            AssertObject(panel.GetNodeOrNull<Label>("Shade/Panel/Layout/CompletionTime")).IsNotNull();
            AssertObject(panel.GetNodeOrNull<Label>("Shade/Panel/Layout/RewindsUsed")).IsNotNull();

            var button = panel.GetNodeOrNull<Button>("Shade/Panel/Layout/ReturnButton");
            AssertObject(button).IsNotNull();
            // Raw key + Godot control auto-translation, per the A1 pattern.
            AssertThat(button.Text).IsEqual("results_return_hub");
            // The one focusable control takes focus so a controller player can act.
            AssertThat(button.FocusMode).IsEqual(Control.FocusModeEnum.All);

            var shade = panel.GetNodeOrNull<Control>("Shade");
            AssertObject(shade.Theme).IsNotNull();
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void EveryStatLineIsRenderedFromTheSuppliedRunStatistics() {
        Node host = CreateHost("LevelResultsStatsHost");
        try {
            LevelResultsPanel panel = AddPanel(host);
            panel.ShowResults("orleans_level_title", 240, 187f, 3);

            var dust = panel.GetNodeOrNull<Label>("Shade/Panel/Layout/DustEarned");
            var time = panel.GetNodeOrNull<Label>("Shade/Panel/Layout/CompletionTime");
            var rewinds = panel.GetNodeOrNull<Label>("Shade/Panel/Layout/RewindsUsed");

            AssertThat(dust.Text.Contains("240")).IsTrue();
            AssertThat(time.Text.Contains("3:07")).IsTrue();
            AssertThat(rewinds.Text.Contains("3")).IsTrue();
            // No line may still be showing its raw format placeholder.
            AssertThat(time.Text.Contains("{0}")).IsFalse();
            AssertThat(rewinds.Text.Contains("{0}")).IsFalse();
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void ItemizedDustLinesRenderAndSumIntoTheTotal() {
        // Audit M-1 / design "Level Results Overlay": the dust total is itemized
        // into mob kills, Chronal Extractors, and the boss reward, and the printed
        // total is exactly the sum of the three lines.
        Node host = CreateHost("LevelResultsItemizedHost");
        try {
            LevelResultsPanel panel = AddPanel(host);
            panel.ShowResults("orleans_level_title", 18, 30, 50, 120f, 1);

            var total = panel.GetNodeOrNull<Label>("Shade/Panel/Layout/DustEarned");
            var mobs = panel.GetNodeOrNull<Label>("Shade/Panel/Layout/DustMobs");
            var extractors = panel.GetNodeOrNull<Label>("Shade/Panel/Layout/DustExtractors");
            var boss = panel.GetNodeOrNull<Label>("Shade/Panel/Layout/DustBoss");
            AssertObject(mobs).IsNotNull();
            AssertObject(extractors).IsNotNull();
            AssertObject(boss).IsNotNull();

            AssertThat(total.Text.Contains("98")).IsTrue();
            AssertThat(mobs.Visible).IsTrue();
            AssertThat(mobs.Text.Contains("18")).IsTrue();
            AssertThat(extractors.Visible).IsTrue();
            AssertThat(extractors.Text.Contains("30")).IsTrue();
            AssertThat(boss.Visible).IsTrue();
            AssertThat(boss.Text.Contains("50")).IsTrue();

            // The legacy single-total form hides the itemization rather than
            // leaving stale lines from an earlier render on screen.
            panel.ShowResults("orleans_level_title", 240, 187f, 3);
            AssertThat(mobs.Visible).IsFalse();
            AssertThat(extractors.Visible).IsFalse();
            AssertThat(boss.Visible).IsFalse();
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void ANegativeRewindCountIsFlooredRatherThanShown() {
        Node host = CreateHost("LevelResultsNegativeHost");
        try {
            LevelResultsPanel panel = AddPanel(host);
            panel.ShowResults("orleans_level_title", 0, 0f, -4);

            var rewinds = panel.GetNodeOrNull<Label>("Shade/Panel/Layout/RewindsUsed");
            AssertThat(rewinds.Text.Contains("-4")).IsFalse();
            AssertThat(rewinds.Text.Contains("0")).IsTrue();
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void DurationsFormatAsMinutesAndSecondsAndGrowAnHourFieldOnlyWhenNeeded() {
        AssertString(StoryManager.FormatDuration(0f)).IsEqual("0:00");
        AssertString(StoryManager.FormatDuration(9f)).IsEqual("0:09");
        AssertString(StoryManager.FormatDuration(59.9f)).IsEqual("0:59");
        AssertString(StoryManager.FormatDuration(60f)).IsEqual("1:00");
        AssertString(StoryManager.FormatDuration(187f)).IsEqual("3:07");
        AssertString(StoryManager.FormatDuration(3600f)).IsEqual("1:00:00");
        AssertString(StoryManager.FormatDuration(3725f)).IsEqual("1:02:05");
        // A corrupt or unstarted clock reads as zero, never as a negative time.
        AssertString(StoryManager.FormatDuration(-30f)).IsEqual("0:00");
    }

    [TestCase]
    public void TheLevelClockRunsWhileTheLevelIsLiveAndFreezesAtCompletion() {
        StoryManager story = StoryManager.Instance;
        AssertObject(story).IsNotNull();
        try {
            story.BeginLevelRun();
            AssertThat(story.IsLevelTimerRunning).IsTrue();
            AssertFloat(story.LevelElapsedSeconds).IsEqual(0f);

            for (int frame = 0; frame < 60; frame++) story._Process(1.0 / 60.0);
            AssertFloat(story.LevelElapsedSeconds).IsEqualApprox(1f, 0.01f);

            story.CompleteLevelRun();
            AssertThat(story.IsLevelTimerRunning).IsFalse();
            AssertFloat(story.LastLevelCompletionSeconds).IsEqualApprox(1f, 0.01f);

            // A frozen result must not creep while the results overlay is open.
            for (int frame = 0; frame < 60; frame++) story._Process(1.0 / 60.0);
            AssertFloat(story.LastLevelCompletionSeconds).IsEqualApprox(1f, 0.01f);
        } finally {
            story.StopLevelRun();
        }
    }

    [TestCase]
    public void RewindsAreCountedFromTheEventBecauseTheRemainingPoolRefills() {
        StoryManager story = StoryManager.Instance;
        EventBus bus = EventBus.Instance;
        AssertObject(bus).IsNotNull();
        try {
            story.BeginLevelRun();
            AssertThat(story.LevelRewindsUsed).IsEqual(0);

            bus.RaiseRewindTriggered(Vector2.Zero);
            bus.RaiseRewindTriggered(Vector2.Zero);
            // A checkpoint refill puts the pool back up; the count must not follow.
            story.SetRewinds(3);
            bus.RaiseRewindTriggered(Vector2.Zero);

            AssertThat(story.LevelRewindsUsed).IsEqual(3);

            story.CompleteLevelRun();
            AssertThat(story.LastLevelRewindsUsed).IsEqual(3);

            // Rewinds outside a live level (hub practice, teardown) are not counted.
            bus.RaiseRewindTriggered(Vector2.Zero);
            AssertThat(story.LastLevelRewindsUsed).IsEqual(3);
        } finally {
            story.StopLevelRun();
        }
    }

    [TestCase]
    public void StartingANewLevelRunZeroesBothStatisticsForTheFreshAttempt() {
        StoryManager story = StoryManager.Instance;
        try {
            story.BeginLevelRun();
            EventBus.Instance?.RaiseRewindTriggered(Vector2.Zero);
            for (int frame = 0; frame < 30; frame++) story._Process(1.0 / 60.0);

            // A Timeline Collapse restart is a fresh attempt: carrying the
            // abandoned attempt's clock in would report a time never experienced.
            story.BeginLevelRun();

            AssertFloat(story.LevelElapsedSeconds).IsEqual(0f);
            AssertThat(story.LevelRewindsUsed).IsEqual(0);
        } finally {
            story.StopLevelRun();
        }
    }
}
