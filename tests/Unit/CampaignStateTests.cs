using FTT.Characters;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Audit M-4 plus the Timeline Collapse low pair. A fresh campaign's Chronal
/// Rewind pool follows the difficulty (Easy 5 / Normal 3 / Hard 1) — the same
/// 5/3/1 <c>SaveManager.CreateStorySlot</c> writes to the slot — instead of the
/// old hardcoded 3 that ran Easy two rewinds short until the first resume. And
/// the collapse HP refill reads the live player (whose <c>MaximumHP</c> already
/// folds in the Resonance MaxHP bonus), falling back to the authored resource
/// through the pinning cache rather than the crash-class bare <c>GD.Load</c>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CampaignStateTests {

    [TestCase]
    public void AFreshCampaignStartsWithTheDifficultysFullRewindPool() {
        StoryManager story = StoryManager.Instance;
        AssertObject(story).IsNotNull();
        try {
            story.ResetCampaignState(Difficulty.Easy);
            AssertThat(story.ChronalRewindsRemaining)
                .OverrideFailureMessage("Easy must start with its full pool of 5 rewinds (audit M-4).")
                .IsEqual(5);

            story.ResetCampaignState(Difficulty.Hard);
            AssertThat(story.ChronalRewindsRemaining).IsEqual(1);

            story.ResetCampaignState(Difficulty.Normal);
            AssertThat(story.ChronalRewindsRemaining).IsEqual(3);

            // The runtime pool and the freshly created save slot must agree, or the
            // first resume silently rewrites the pool the campaign started with.
            foreach (Difficulty difficulty in new[] { Difficulty.Easy, Difficulty.Normal, Difficulty.Hard }) {
                story.ResetCampaignState(difficulty);
                AssertThat(story.ChronalRewindsRemaining)
                    .IsEqual(FTT.Environment.ChronalRewindManager.GetMaximumRewinds(difficulty));
            }
        } finally {
            // Leave the autoload in its boot state (Tutorial, 0 dust, Normal pool).
            story.ResetCampaignState(Difficulty.Normal);
        }
    }

    [TestCase]
    public void CollapseHPRefillPrefersTheLivePlayerIncludingItsResonanceBonus() {
        StoryManager story = StoryManager.Instance;
        var tree = (SceneTree)Engine.GetMainLoop();
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        string originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
        PlayerController player = null;
        try {
            GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
            GameManager.Instance.CurrentSession.SelectedCharacterID = "einstein";
            AssertObject(tree.GetFirstNodeInGroup("StoryPlayer"))
                .OverrideFailureMessage("Test environment is dirty: a StoryPlayer is already in the tree.")
                .IsNull();

            player = CharacterFactory.CreateCharacter("einstein");
            tree.Root.AddChild(player);
            AssertThat(story.GetSelectedCharacterMaximumHP()).IsEqual(player.MaximumHP);

            // A Resonance MaxHP perk must follow into the refill (the old GD.Load
            // path re-read the base resource and dropped the bonus entirely).
            player.StoryMaxHPBonus = 25;
            AssertThat(story.GetSelectedCharacterMaximumHP()).IsEqual(player.MaximumHP);
            AssertThat(story.GetSelectedCharacterMaximumHP()).IsEqual((player.Data?.MaxHP ?? 100) + 25);
        } finally {
            if (player != null && GodotObject.IsInstanceValid(player)) {
                player.GetParent()?.RemoveChild(player);
                player.Free();
            }
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = originalCharacter;
        }
    }

    [TestCase]
    public void CollapseHPRefillFallsBackToTheAuthoredResourceAndRejectsUnknownIDs() {
        StoryManager story = StoryManager.Instance;
        var tree = (SceneTree)Engine.GetMainLoop();
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        string originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
        try {
            GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
            AssertObject(tree.GetFirstNodeInGroup("StoryPlayer"))
                .OverrideFailureMessage("Test environment is dirty: a StoryPlayer is already in the tree.")
                .IsNull();

            // No live player: the authored resource (via the pinning cache) answers.
            GameManager.Instance.CurrentSession.SelectedCharacterID = "einstein";
            CharacterData einstein = AuthoredResources.Load<CharacterData>(
                "res://resources/Characters/einstein_data.tres");
            AssertObject(einstein).IsNotNull();
            AssertThat(story.GetSelectedCharacterMaximumHP()).IsEqual(einstein.MaxHP);

            // An ID outside the nine-character roster must never be interpolated
            // into a resource path; the safe default answers instead.
            GameManager.Instance.CurrentSession.SelectedCharacterID = "not_a_character";
            AssertThat(story.GetSelectedCharacterMaximumHP()).IsEqual(100);
            GameManager.Instance.CurrentSession.SelectedCharacterID = "";
            AssertThat(story.GetSelectedCharacterMaximumHP()).IsEqual(100);
        } finally {
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = originalCharacter;
        }
    }
}
