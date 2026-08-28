using FTT.Characters;
using FTT.Core;
using Godot;

namespace FTT.Environment {

    /// <summary>
    /// V7 secrets: the marker for a level's hidden room or cache — wrap it
    /// around the off-path TreasureChest or Extractor. Entering it once counts
    /// the secret on the results screen and restores Timeline Integrity —
    /// V7.3: +5% for the level's designated special secret
    /// (<see cref="IsSpecialSecret"/>, the default, which preserves today's
    /// single-secret levels), +2% for an ordinary one. Found state joins the
    /// per-attempt registry so a mid-level resume keeps it found.
    /// </summary>
    public partial class SecretCache : Area2D {
        /// <summary>Scene-tree group the resume pass sweeps.</summary>
        public const string Group = "secret_cache";

        [Export] public string SecretID = "secret";

        /// <summary>V7.3: the level's designated special secret restores +5%;
        /// ordinary secrets restore +2%. Defaults true because every shipped
        /// level authors at most one secret — its designated special one.</summary>
        [Export] public bool IsSpecialSecret = true;

        private bool _found;

        /// <summary>True once discovered this attempt. Test seam.</summary>
        public bool Found => _found;

        public override void _Ready() {
            AddToGroup(Group);
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

        /// <summary>Counts the secret once and pays its restoration. Test seam.</summary>
        public bool Discover() {
            if (_found) return false;
            _found = true;
            StoryManager.Instance?.RegisterSecretFound(SecretID, IsSpecialSecret);
            return true;
        }

        /// <summary>
        /// V7.3 mid-level resume: the secret was found earlier this attempt —
        /// mark it found locally WITHOUT re-registering (no double count, no
        /// double Integrity restore).
        /// </summary>
        public void MarkAlreadyFound() => _found = true;
    }
}
