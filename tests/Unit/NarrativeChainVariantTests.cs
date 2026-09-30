using System;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 13 W4: the level framework's dialogue chain under hero variants, and
/// the one-shot absence beat.
///
/// <para>A hero variant completes under its own ID — <c>level_02.exit@joan</c> —
/// because the seen/skip rules key on it. Before this package the framework
/// compared the raw completed ID against the base exit/post-boss IDs, so a
/// variant exit never presented the results and a variant post-boss beat stalled
/// the chain. The Package 13 scripts add variants to exactly those slots (Level 1's
/// exit and boss intro, Level 8's post-boss), so the framework now compares base
/// IDs.</para>
///
/// <para>Reuses <c>PostBossChainTestLevelController</c> from
/// <c>StoryLevelControllerBaseTests</c>; every case restores the pause flag and the
/// session slot.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class NarrativeChainVariantTests {

    [TestCase]
    public void AHeroVariantExitStillPresentsTheResultsAndCompletesTheLevel() {
        using var fixture = new ChainFixture();
        PostBossChainTestLevelController level = fixture.Level;
        level.DefeatBossForTest(25);
        AssertThat(level.StartedDialogues).Contains("level_02.exit");
        AssertThat(level.LevelComplete).IsFalse();

        EventBus.Instance.RaiseDialogueComplete("level_02.exit@joan");
        AssertThat(level.LevelComplete).IsTrue();
        // The exit ends the level; it is never routed to the subclass hook.
        AssertThat(level.RoutedToSubclass.Contains("level_02.exit")).IsFalse();
    }

    [TestCase]
    public void AHeroVariantPostBossBeatAdvancesTheChainToTheExit() {
        using var fixture = new ChainFixture();
        PostBossChainTestLevelController level = fixture.Level;
        level.Beats.Add("level_02.postboss");
        level.DefeatBossForTest(25);
        AssertThat(level.StartedDialogues).ContainsExactly("level_02.postboss");

        EventBus.Instance.RaiseDialogueComplete("level_02.postboss@cleopatra");
        // The subclass hook sees the base ID, and the chain moves on.
        AssertThat(level.RoutedToSubclass).ContainsExactly("level_02.postboss");
        AssertThat(level.StartedDialogues).Contains("level_02.exit");
    }

    [TestCase]
    public void TheAbsenceBeatPlaysOnceAndRetriesIfTheBoxWasBusy() {
        using var fixture = new ChainFixture();
        PostBossChainTestLevelController level = fixture.Level;
        AssertString(level.AbsenceDialogueID).IsEqual("level_02.absence");

        // Another sequence held the box: nothing latched, the next crossing retries.
        level.DialogueStartsSucceed = false;
        AssertThat(level.PlayAbsenceBeat()).IsFalse();
        AssertThat(level.AbsenceBeatShown).IsFalse();

        level.DialogueStartsSucceed = true;
        AssertThat(level.PlayAbsenceBeat()).IsTrue();
        AssertThat(level.AbsenceBeatShown).IsTrue();
        AssertThat(level.PlayAbsenceBeat()).IsFalse();
        AssertThat(level.StartedDialogues.FindAll(id => id == "level_02.absence").Count).IsEqual(2);
    }

    private sealed class ChainFixture : IDisposable {
        public readonly PostBossChainTestLevelController Level;
        private readonly int _originalSlot;
        private readonly bool _originalPaused;

        public ChainFixture() {
            var tree = (SceneTree)Engine.GetMainLoop();
            _originalPaused = tree.Paused;
            _originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
            Level = new PostBossChainTestLevelController { Name = "NarrativeChainTestLevel" };
            tree.Root.AddChild(Level);
        }

        public void Dispose() {
            if (GodotObject.IsInstanceValid(Level)) {
                Level.GetParent()?.RemoveChild(Level);
                Level.Free();
            }
            GameManager.Instance.CurrentSession.ActiveSaveSlot = _originalSlot;
            ((SceneTree)Engine.GetMainLoop()).Paused = _originalPaused;
        }
    }
}
