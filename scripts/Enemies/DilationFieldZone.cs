using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Environment;
using Godot;

namespace FTT.Enemies {

    /// <summary>
    /// V7.3 Chrono-Warden rework: the persistent zone a
    /// <see cref="EnemyAbilityArchetype.PersistentFieldAtTarget"/> ability
    /// leaves at the target position captured at cast. It lives
    /// <see cref="LifeSeconds"/>, and while a player stands inside its radius
    /// it applies — and keeps refreshing — the authored Slow
    /// (<see cref="StatusType.TimeDilation"/>) status, so walking out is the
    /// counterplay rather than waiting the debuff out.
    ///
    /// <para>Membership in <c>"persistent_construct"</c> puts it in
    /// <c>ChronalRewindManager.FrozenSimulationGroups</c>: the world freeze
    /// during a rewind/scrub pauses both its life timer and its status
    /// application (<see cref="IStoryRewindSimulation"/>).</para>
    ///
    /// <para>The Area2D body carries the authored circle on the Player mask so
    /// the zone is physically present; the per-step status sweep itself reads
    /// the <c>"Players"</c> group by distance (the extractor's siphon-engage
    /// pattern), which keeps the behavior deterministic under headless direct
    /// calls where no physics frames flush overlaps.</para>
    /// </summary>
    public partial class DilationFieldZone : Area2D, IStoryRewindSimulation, IStoryTimeFreezable {

        /// <summary>Zone radius in pixels (the ability's PulseRadius).</summary>
        public float RadiusPixels { get; private set; } = 140f;

        /// <summary>Seconds of life remaining. Test seam.</summary>
        public float LifeSeconds { get; private set; } = 4f;

        /// <summary>Status the field applies while a player is inside.</summary>
        public StatusType AppliedStatus { get; private set; } = StatusType.TimeDilation;

        public float StatusDuration { get; private set; } = 2.5f;
        public float StatusIntensity { get; private set; } = 1f;

        private bool _rewindFrozen;

        public bool IsStoryRewindFrozen => _rewindFrozen;

        public void SetStoryRewindFrozen(bool frozen) => _rewindFrozen = frozen;

        /// <summary>Reads radius, life, and status off the authored ability.</summary>
        public void Configure(EnemyAbilityData ability) {
            if (ability == null) return;
            RadiusPixels = Mathf.Max(8f, ability.PulseRadius);
            LifeSeconds = Mathf.Max(0.1f, ability.FieldDurationSeconds);
            AppliedStatus = ability.AppliedStatus == StatusType.None
                ? StatusType.TimeDilation
                : ability.AppliedStatus;
            StatusDuration = ability.StatusDuration > 0f ? ability.StatusDuration : 2.5f;
            StatusIntensity = ability.StatusIntensity <= 0f ? 1f : ability.StatusIntensity;
        }

        public override void _Ready() {
            AddToGroup("persistent_construct");
            Monitoring = true;
            Monitorable = true;
            CollisionLayer = 0;
            CollisionMask = CollisionLayers.Player;
            if (GetNodeOrNull<CollisionShape2D>("CollisionShape2D") == null) {
                AddChild(new CollisionShape2D {
                    Name = "CollisionShape2D",
                    Shape = new CircleShape2D { Radius = RadiusPixels }
                });
            }
        }

        public override void _PhysicsProcess(double delta) {
            if (_rewindFrozen) return;
            float dt = (float)delta;
            LifeSeconds -= dt;
            if (LifeSeconds <= 0f) {
                QueueFree();
                return;
            }
            ApplyStatusToPlayersInside();
        }

        /// <summary>
        /// Applies/refreshes the Slow on every player inside the radius. The
        /// newest-replaces status rule makes the per-frame reapply a refresh:
        /// the timer restarts each frame the player stays in, and starts its
        /// final countdown the frame they leave.
        /// </summary>
        private void ApplyStatusToPlayersInside() {
            SceneTree tree = GetTree();
            if (tree == null) return;
            Godot.Collections.Array<Node> players = tree.GetNodesInGroup("Players");
            using var playersLifetime = players.AsDisposable();
            foreach (Node node in players) {
                if (node is not PlayerController player) continue;
                if (player.GlobalPosition.DistanceTo(GlobalPosition) > RadiusPixels) continue;
                player.GetNodeOrNull<StatusController>("StatusController")
                    ?.ApplyStatus(AppliedStatus, StatusDuration, StatusIntensity);
            }
        }

        /// <summary>
        /// V7.6 Time Freeze. Shares the freeze flag with the death rewind because
        /// this class's rewind freeze is already a pure latch — it mutates nothing
        /// on the way in, so positions, phases and timers all survive the freeze
        /// and resume with no catch-up tick.
        /// </summary>
        public void SetTimeFrozen(bool frozen) => _rewindFrozen = frozen;

    }
}
