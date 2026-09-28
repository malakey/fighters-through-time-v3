using FTT.Characters;
using FTT.Core;
using Godot;

namespace FTT.Environment {

    /// <summary>
    /// V7 secrets: the marker for a level's hidden room or cache. Entering it
    /// once counts the secret on the results screen.
    ///
    /// <b>V7.6 F01 (Package 11 A3):</b> a secret no longer restores Timeline
    /// Integrity — nothing does. Finding one subtracts 0.1 from the level's
    /// drain FACTOR for the rest of the attempt, which is future time rather
    /// than a refill, so the gauge never jumps. Found state joins the
    /// per-attempt registry so a mid-level resume keeps it found.
    ///
    /// <para><b>Package 12 W8 (GAP-03).</b> The cache is now the physical source
    /// of the F05 discovery reward. <see cref="SecretID"/> is the level's
    /// reserved <c>{level}.secret</c> source ID; discovery issues that source's
    /// authored award once and spawns it as a physical pickup
    /// (<see cref="DustAwardSource.Secret"/>), paid and attributed only at
    /// collection like every other F05 source. The claim persists; the issue
    /// does not — so a reload after discovery but before collection respawns
    /// the one pickup (<see cref="RestorePendingAward"/>), and a collected
    /// claim never pays again. A level with no ledger (the unit harness) pays
    /// nothing: the secret has no advisory fallback value.</para>
    /// </summary>
    public partial class SecretCache : Area2D {
        /// <summary>Scene-tree group the resume pass sweeps.</summary>
        public const string Group = "secret_cache";

        /// <summary>Trigger size of a code-built cache: a small room, not a corridor.</summary>
        public static readonly Vector2 DefaultTriggerSize = new(200f, 200f);

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

        /// <summary>The pickup the last discovery (or restore) spawned, or null. Test seam.</summary>
        public ChronalDustPickup SpawnedAward { get; private set; }

        /// <summary>
        /// Builds a cache for a level controller: the trigger shape plus a faint
        /// placeholder glint so the hiding place reads once the player is in it.
        /// </summary>
        public static SecretCache Create(string secretID, Vector2 position) {
            var cache = new SecretCache {
                Name = $"SecretCache_{(secretID ?? "secret").Replace('.', '_')}",
                SecretID = secretID ?? "secret",
                Position = position
            };
            cache.AddChild(new CollisionShape2D {
                Name = "CollisionShape2D",
                Shape = new RectangleShape2D { Size = DefaultTriggerSize }
            });
            cache.AddChild(new ColorRect {
                Name = "Glint",
                Size = new Vector2(18f, 18f),
                Position = new Vector2(-9f, DefaultTriggerSize.Y * 0.5f - 34f),
                Color = new Color(1f, 0.86f, 0.45f, 0.35f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            });
            return cache;
        }

        public override void _Ready() {
            AddToGroup(Group);
            CollisionLayer = CollisionLayers.Trigger;
            CollisionMask = CollisionLayers.Player;
            Monitoring = true;
            BodyEntered += OnBodyEntered;
            if (GetChildCount() == 0) {
                AddChild(new CollisionShape2D {
                    Shape = new RectangleShape2D { Size = DefaultTriggerSize }
                });
            }
        }

        public override void _ExitTree() => BodyEntered -= OnBodyEntered;

        private void OnBodyEntered(Node2D body) {
            if (body is not PlayerController) return;
            // Discovery spawns a pickup; SpawnDustAward defers itself off the
            // physics flush while this guard is live.
            using var scope = PhysicsCallbackGuard.Enter();
            Discover();
        }

        /// <summary>Counts the secret once and issues its F05 award. Test seam.</summary>
        public bool Discover() {
            if (_found) return false;
            _found = true;
            StoryManager.Instance?.RegisterSecretFound(SecretID, IsSpecialSecret);
            IssueAward();
            return true;
        }

        /// <summary>
        /// V7.3 mid-level resume: the secret was found earlier this attempt —
        /// mark it found locally WITHOUT re-registering (no double count).
        /// </summary>
        public void MarkAlreadyFound() => _found = true;

        /// <summary>
        /// Package 12 W8: a resume that finds this secret already discovered but
        /// its reward never collected respawns the one pickup. A claimed source,
        /// or one already issued this load, spawns nothing.
        /// </summary>
        public bool RestorePendingAward() {
            if (!_found || LevelRewardDirectory.IsClaimed(SecretID)) return false;
            return IssueAward();
        }

        private bool IssueAward() {
            if (LevelRewardDirectory.EnsureCompiled() == null) return false;
            if (!LevelRewardDirectory.TryIssueSourceAward(SecretID, out string sourceID, out int amount)
                || amount <= 0) {
                return false;
            }
            Node parent = GetParent() ?? this;
            SpawnedAward = StoryDropSystem.SpawnDustAward(
                amount, GlobalPosition, parent, DustAwardSource.Secret, sourceID);
            return true;
        }
    }
}
