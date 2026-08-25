using FTT.Characters;
using FTT.Core;
using Godot;

namespace FTT.Environment {

    /// <summary>
    /// V7.2 Chronal Feast — the beat-em-up meal, era-flavored. An authored,
    /// placed pickup: instant heal on touch (Easy 50% / Normal 35% / Hard 20%
    /// of max HP), never respawns within an attempt. The authored anchor spots
    /// exist once per level; difficulty selects how many are populated
    /// (Easy 3 / Normal 2 / Hard 1) via <see cref="AnchorIndex"/> — an anchor
    /// whose index is at or past the difficulty's count removes itself on load.
    /// </summary>
    public partial class ChronalFeast : Area2D {
        /// <summary>Authored anchor order: 0 populates on every difficulty,
        /// 1 on Normal and Easy, 2 on Easy only.</summary>
        [Export] public int AnchorIndex;

        private bool _consumed;

        public override void _Ready() {
            Difficulty difficulty = StoryDifficultyTuning.CurrentStoryDifficulty;
            if (AnchorIndex >= StoryDifficultyTuning.GetChronalFeastCount(difficulty)) {
                QueueFree();
                return;
            }
            CollisionLayer = CollisionLayers.Trigger;
            CollisionMask = CollisionLayers.Player;
            Monitoring = true;
            BodyEntered += OnBodyEntered;
            if (GetChildCount() == 0) BuildPlaceholderVisual();
        }

        private void OnBodyEntered(Node2D body) {
            if (body is PlayerController player) TryConsume(player);
        }

        /// <summary>The touch heal: once, never respawning within the attempt.</summary>
        public bool TryConsume(PlayerController player) {
            if (_consumed || player == null || player.CurrentState == CharacterState.Dead) return false;
            _consumed = true;
            int heal = StoryDifficultyTuning.ScaleHeal(
                player.MaximumHP,
                StoryDifficultyTuning.GetChronalFeastFraction(
                    StoryDifficultyTuning.CurrentStoryDifficulty));
            if (heal > 0) player.HealStory(heal);
            EnvironmentAudioCues.PlayPickup(1.0f);
            QueueFree();
            return true;
        }

        private void BuildPlaceholderVisual() {
            AddChild(new CollisionShape2D {
                Shape = new RectangleShape2D { Size = new Vector2(36f, 24f) }
            });
            AddChild(new ColorRect {
                Name = "Visual",
                Size = new Vector2(36f, 24f),
                Position = new Vector2(-18f, -12f),
                Color = new Color(0.95f, 0.75f, 0.3f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            });
        }
    }
}
