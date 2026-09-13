using FTT.Characters;
using FTT.Core;
using Godot;

namespace FTT.Environment {

    /// <summary>
    /// V7 secrets: the marker for a level's hidden room or cache — wrap it
    /// around the off-path TreasureChest or Extractor. Entering it once counts
    /// the secret on the results screen.
    ///
    /// <b>V7.6 F01 (Package 11 A3):</b> a secret no longer restores Timeline
    /// Integrity — nothing does. Finding one subtracts 0.1 from the level's
    /// drain FACTOR for the rest of the attempt, which is future time rather
    /// than a refill, so the gauge never jumps. Found state joins the
    /// per-attempt registry so a mid-level resume keeps it found.
    /// </summary>
    public partial class SecretCache : Area2D {
        /// <summary>Scene-tree group the resume pass sweeps.</summary>
        public const string Group = "secret_cache";

        [Export] public string SecretID = "secret";

        /// <summary>
        /// Marks the level's designated special secret. V7.6 F01 retired the
        /// +5%/+2% split this used to select — every secret now applies the
        /// same −0.1 drain-factor reduction — so the flag is presentation and
        /// authoring metadata only. Retained (rather than deleted) so authored
        /// scenes keep loading.
        /// </summary>
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

        /// <summary>Counts the secret once. Test seam.</summary>
        public bool Discover() {
            if (_found) return false;
            _found = true;
            StoryManager.Instance?.RegisterSecretFound(SecretID, IsSpecialSecret);
            return true;
        }

        /// <summary>
        /// V7.3 mid-level resume: the secret was found earlier this attempt —
        /// mark it found locally WITHOUT re-registering (no double count).
        /// </summary>
        public void MarkAlreadyFound() => _found = true;
    }
}
