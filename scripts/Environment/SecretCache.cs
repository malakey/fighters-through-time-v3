using FTT.Characters;
using FTT.Core;
using Godot;

namespace FTT.Environment {

    /// <summary>
    /// V7 secrets: the marker for a level's hidden room or cache — wrap it
    /// around the off-path TreasureChest or Extractor. Entering it once counts
    /// the secret on the results screen and restores +5% Timeline Integrity
    /// (the only exploration pressure the linear structure carries).
    /// </summary>
    public partial class SecretCache : Area2D {
        [Export] public string SecretID = "secret";

        private bool _found;

        /// <summary>True once discovered this attempt. Test seam.</summary>
        public bool Found => _found;

        public override void _Ready() {
            CollisionLayer = CollisionLayers.Trigger;
            CollisionMask = CollisionLayers.Player;
            Monitoring = true;
            BodyEntered += OnBodyEntered;
            if (GetChildCount() == 0) {
                AddChild(new CollisionShape2D {
                    Shape = new RectangleShape2D { Size = new Vector2(200f, 200f) }
                });
            }
        }

        private void OnBodyEntered(Node2D body) {
            if (body is PlayerController) Discover();
        }

        /// <summary>Counts the secret once. Test seam.</summary>
        public bool Discover() {
            if (_found) return false;
            _found = true;
            StoryManager.Instance?.RegisterSecretFound();
            return true;
        }
    }
}
