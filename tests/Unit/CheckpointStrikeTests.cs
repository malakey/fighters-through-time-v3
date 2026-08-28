using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// V7.3 strike-to-activate checkpoints (design "Chronal Rift Checkpoints"):
/// the Area2D is only a proximity prompt — activation requires striking the
/// fracture's PersistentObject-layer surface with a player attack. The entry
/// checkpoint self-activates (the player just arrived through it), the
/// Mending heal pays once per checkpoint per attempt (authoritative registry
/// on StoryManager), and enemy hits can never stabilize a fracture.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CheckpointStrikeTests {

    [TestCase]
    public void WalkingThroughAFractureOnlyPromptsAndStrikingActivatesIt() {
        StoryManager story = StoryManager.Instance;
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        story.ClearLevelAttemptState();
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(player);
        var trigger = new CheckpointTrigger {
            Name = "StrikeTestCheckpoint",
            CheckpointID = "strike_test_checkpoint"
        };
        tree.Root.AddChild(trigger);
        int reachedCount = 0;
        void OnReached(string id) { if (id == "strike_test_checkpoint") reachedCount++; }
        EventBus.Instance.OnCheckpointReached += OnReached;
        try {
            player.ApplyDamage(60);
            int wounded = player.CurrentHP;

            // Walk-through: prompt only — no activation, no save, no heal.
            trigger.HandleBodyEntered(player);
            AssertThat(trigger.IsActivated).IsFalse();
            AssertThat(story.IsCheckpointActivated("strike_test_checkpoint")).IsFalse();
            AssertThat(reachedCount).IsEqual(0);
            AssertThat(player.CurrentHP).IsEqual(wounded);

            // An enemy hit (unattributed attacker) must not stabilize it.
            trigger.HandleStrike(Strike(attackerIndex: -1));
            AssertThat(trigger.IsActivated)
                .OverrideFailureMessage("Enemy hits must never stabilize a fracture.")
                .IsFalse();

            // The player's strike activates: registry, heal (Mending, once).
            trigger.HandleStrike(Strike(attackerIndex: 0));
            AssertThat(trigger.IsActivated).IsTrue();
            AssertThat(story.IsCheckpointActivated("strike_test_checkpoint")).IsTrue();
            AssertThat(reachedCount).IsEqual(1);
            int expectedHeal = StoryDifficultyTuning.ScaleHeal(
                player.MaximumHP,
                StoryDifficultyTuning.GetCheckpointMendingFraction(
                    StoryDifficultyTuning.CurrentStoryDifficulty));
            AssertThat(player.CurrentHP)
                .OverrideFailureMessage("The first activation must pay the Mending heal.")
                .IsEqual(Mathf.Min(player.MaximumHP, wounded + expectedHeal));

            // Re-striking re-anchors (event re-raised) but heals nothing.
            player.ApplyDamage(30);
            int rewounded = player.CurrentHP;
            trigger.HandleStrike(Strike(attackerIndex: 0));
            AssertThat(reachedCount).IsEqual(2);
            AssertThat(player.CurrentHP)
                .OverrideFailureMessage("Re-touching an activated checkpoint heals nothing.")
                .IsEqual(rewounded);
        } finally {
            EventBus.Instance.OnCheckpointReached -= OnReached;
            SessionExitGuard.ClearMarker();
            story.ClearLevelAttemptState();
            player.Free();
            trigger.Free();
        }
    }

    [TestCase]
    public void TheEntryCheckpointSelfActivatesOnWalkThrough() {
        StoryManager story = StoryManager.Instance;
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        story.ClearLevelAttemptState();
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(player);
        var trigger = new CheckpointTrigger {
            Name = "EntryTestCheckpoint",
            CheckpointID = "entry_test_checkpoint_0",
            SelfActivating = true
        };
        tree.Root.AddChild(trigger);
        try {
            trigger.HandleBodyEntered(player);
            AssertThat(trigger.IsActivated)
                .OverrideFailureMessage("The entry checkpoint self-activates — the player just arrived through it.")
                .IsTrue();
            AssertThat(story.IsCheckpointActivated("entry_test_checkpoint_0")).IsTrue();
        } finally {
            SessionExitGuard.ClearMarker();
            story.ClearLevelAttemptState();
            player.Free();
            trigger.Free();
        }
    }

    [TestCase]
    public void EveryCheckpointGrowsAPersistentObjectStrikeSurface() {
        // Uniform across code-built checkpoints (base builder, Level 00/01):
        // the trigger itself grows the strikeable surface in _Ready, on the
        // PersistentObject layer the player's hitbox mask already covers.
        var entry = new CheckpointTrigger { CheckpointID = "x_checkpoint_0" };
        var mid = new CheckpointTrigger { CheckpointID = "x_checkpoint_1" };
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        tree.Root.AddChild(entry);
        tree.Root.AddChild(mid);
        try {
            foreach (CheckpointTrigger trigger in new[] { entry, mid }) {
                var surface = trigger.GetNodeOrNull<EnvironmentHurtboxAdapter>("StrikeSurface");
                AssertObject(surface)
                    .OverrideFailureMessage($"'{trigger.CheckpointID}' has no strikeable surface.")
                    .IsNotNull();
                AssertThat(surface.CollisionLayer).IsEqual(CollisionLayers.PersistentObject);
                AssertThat(surface.CollisionMask).IsEqual(CollisionLayers.PlayerHitbox);
                AssertThat(surface.OwnerPlayerIndex)
                    .OverrideFailureMessage("The surface must share the -1 owner index so enemy hitboxes skip it.")
                    .IsEqual(-1);
            }
        } finally {
            StoryManager.Instance.ClearLevelAttemptState();
            entry.Free();
            mid.Free();
        }
    }

    private static HitPayload Strike(int attackerIndex) => new() {
        AttackerIndex = attackerIndex,
        TargetIndex = -1,
        AttackID = "test.strike",
        HitboxID = "primary",
        AttackClass = AttackClass.Basic,
        Damage = 8f,
        Knockback = Vector2.Zero,
        HitstunDuration = 0f,
        HitOrigin = Vector2.Zero,
        AttackerFacingRight = true
    };
}
