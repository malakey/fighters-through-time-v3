using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// The level rank's subscriber (gameplay review §1.9): attached with the level stack, so it
/// covers Florence and never the hub; it hears the campaign hero's discrete hits and hazard
/// contacts, and never a periodic tick or the Mirror clone.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class LevelRankTrackerTests {
    private const int HitDamage = 10;

    private static HitPayload Hit(HitDelivery delivery) => new() {
        Damage = HitDamage,
        AttackerIndex = -1,
        AttackID = "rank_tally_probe",
        HitboxID = "rank_tally_probe",
        Delivery = delivery
    };

    [TestCase]
    public void TheBootstrapperAttachesItToLevelsAndFlorenceButNotToTheHub() {
        var tree = (SceneTree)Engine.GetMainLoop();
        var level = new Node { Name = "RankTrackerLevelRoot" };
        var hub = new Node { Name = "RankTrackerHubRoot" };
        tree.Root.AddChild(level);
        tree.Root.AddChild(hub);
        Node florence = null;
        try {
            StorySceneBootstrapper.Attach(level, "");
            StorySceneBootstrapper.Attach(hub, "", includeRewind: false);
            AssertObject(level.GetNodeOrNull<LevelRankTracker>(LevelRankTracker.NodeName)).IsNotNull();
            AssertObject(hub.GetNodeOrNull<LevelRankTracker>(LevelRankTracker.NodeName)).IsNull();

            florence = ResourceLoader.Load<PackedScene>(StoryManager.GetLevelScenePath(CampaignLevel.Florence)).Instantiate();
            tree.Root.AddChild(florence);
            AssertObject(florence.GetNodeOrNull<LevelRankTracker>(LevelRankTracker.NodeName))
                .OverrideFailureMessage("Florence does not extend the level base class and must still be ranked.")
                .IsNotNull();
        } finally {
            if (florence != null) {
                PoolManager.Instance?.ReleaseActiveUnder(florence);
                florence.GetParent()?.RemoveChild(florence);
                florence.Free();
            }
            level.Free();
            hub.Free();
        }
    }

    [TestCase]
    public void TheHerosDiscreteHitsAndHazardsFeedTheTallyAndItsPeriodicTicksDoNot() {
        using var run = new RunScope();
        var tree = (SceneTree)Engine.GetMainLoop();
        var tracker = new LevelRankTracker { Name = LevelRankTracker.NodeName };
        PlayerController hero = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(hero);
        tree.Root.AddChild(tracker);
        try {
            tracker.BindHero();
            AssertObject(tracker.BoundHero).IsEqual(hero);
            StoryManager story = StoryManager.Instance;

            hero.TakeDamage(Hit(HitDelivery.Tick));
            hero.ApplyEnvironmentalDamage(HitDamage, periodicTick: true);
            AssertThat(story.RankHits)
                .OverrideFailureMessage("A zone pulse and a periodic environmental tick are not hits.")
                .IsEqual(0);

            int hp = hero.CurrentHP;
            hero.TakeDamage(Hit(HitDelivery.DirectHit));
            int hitLoss = hp - hero.CurrentHP;
            AssertThat(hitLoss > 0).OverrideFailureMessage("Harness fault: the hit dealt nothing.").IsTrue();
            AssertThat(story.RankHits).IsEqual(1);
            AssertThat(story.RankBaseDamage).IsEqual((float)hitLoss);

            int applied = hero.ApplyEnvironmentalDamage(HitDamage);
            AssertThat(story.RankHits).IsEqual(2);
            AssertThat(story.RankBaseDamage).IsEqual((float)(hitLoss + applied));

            // Unsubscribed with the tracker: the hero's next loss reaches nothing.
            tree.Root.RemoveChild(tracker);
            hero.ApplyEnvironmentalDamage(HitDamage);
            AssertThat(story.RankHits).IsEqual(2);
        } finally {
            InputManager.Instance?.ClearInputSource(hero.PlayerIndex);
            hero.Free();
            tracker.Free();
        }
    }

    [TestCase]
    public void TheMirrorCloneNeverFeedsTheTally() {
        using var run = new RunScope();
        var tree = (SceneTree)Engine.GetMainLoop();
        var tracker = new LevelRankTracker { Name = LevelRankTracker.NodeName };
        // The clone as MirrorParadoxController builds it: hostile, out of the hero's group.
        PlayerController clone = CharacterFactory.CreateCharacter("einstein", applyStoryProgression: false);
        clone.IsStoryHostile = true;
        tree.Root.AddChild(clone);
        tree.Root.AddChild(tracker);
        PlayerController hero = null;
        try {
            // Even with the clone still in the group, the tracker refuses a hostile body...
            AssertThat(clone.IsInGroup("StoryPlayer")).IsTrue();
            tracker.BindHero();
            AssertObject(tracker.BoundHero).IsNull();
            clone.ApplyEnvironmentalDamage(HitDamage);
            AssertThat(StoryManager.Instance.RankHits).IsEqual(0);

            // ...and once it has left the group the hero binds, and the clone's losses stay out.
            clone.RemoveFromGroup("StoryPlayer");
            hero = CharacterFactory.CreateCharacter("einstein");
            tree.Root.AddChild(hero);
            tracker.BindHero();
            AssertObject(tracker.BoundHero).IsEqual(hero);
            clone.ApplyEnvironmentalDamage(HitDamage);
            AssertThat(StoryManager.Instance.RankHits).IsEqual(0);
            hero.ApplyEnvironmentalDamage(HitDamage);
            AssertThat(StoryManager.Instance.RankHits).IsEqual(1);
        } finally {
            InputManager.Instance?.ClearInputSource(clone.PlayerIndex);
            tracker.Free();
            hero?.Free();
            clone.Free();
        }
    }

    /// <summary>A slotless live run on Normal, restored afterwards.</summary>
    private sealed class RunScope : System.IDisposable {
        private readonly SessionData _session = GameManager.Instance.CurrentSession;
        private readonly CampaignLevel _level = StoryManager.Instance.CurrentLevel;

        public RunScope() {
            StoryManager story = StoryManager.Instance;
            story.SuppressSceneLoadsForTesting = true;
            story.PrepareDirectLevel(CampaignLevel.Orleans, "einstein", Difficulty.Normal);
            story.BeginLevelRun();
        }

        public void Dispose() {
            StoryManager story = StoryManager.Instance;
            story.SuppressSceneLoadsForTesting = false;
            story.ClearLevelAttemptState();
            story.StopLevelRun();
            story.PrepareDirectLevel(_level,
                string.IsNullOrEmpty(_session.SelectedCharacterID) ? "einstein" : _session.SelectedCharacterID,
                _session.Difficulty);
            GameManager.Instance.CurrentSession = _session;
        }
    }
}
