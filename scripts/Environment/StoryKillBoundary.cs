using Godot;
using FTT.Characters;
using FTT.Core;

namespace FTT.Environment {

    /// <summary>
    /// Package 11 A3 (V7.6 F16): an authored lethal boundary under a Story
    /// pit, chasm or void.
    ///
    /// Crossing it resolves exactly one non-hit fall death through
    /// <see cref="PlayerController.KillPlayerNonHit"/> — regardless of HP,
    /// block, hyper-armor, a one-hit shield or any temporary invulnerability,
    /// and <b>without</b> touching Defy History, the Rally echo or the meter.
    /// Camera framing never kills: only this authored Area2D does, and it is
    /// always placed well below the deepest reachable geometry.
    ///
    /// There is no 25%-HP penalty, no recoverable pit and no free checkpoint
    /// teleport. With a rewind charge the ordinary death flow spends exactly
    /// one and lands the hero on the deepest valid grounded sample; with none,
    /// Acts I-II Collapse (A3b routes Act III to Anchor Snap / Smothered).
    ///
    /// F16 edge case, implemented here: if Timeline Integrity reaches zero on
    /// the same update, the timer-caused Collapse resolves <i>instead</i> — the
    /// player is never charged both a rewind charge and a collapse fee for one
    /// moment.
    /// </summary>
    public partial class StoryKillBoundary : Area2D {

        /// <summary>Scene-tree group, so a level can sweep its boundaries.</summary>
        public const string Group = "story_kill_boundary";

        /// <summary>Stable authoring identity, for dossier/test cross-checks.</summary>
        [Export] public string BoundaryID = "";

        /// <summary>Deaths resolved by this boundary. Test seam.</summary>
        public int KillCount { get; private set; }

        public override void _Ready() {
            AddToGroup(Group);
            CollisionLayer = CollisionLayers.Trigger;
            CollisionMask = CollisionLayers.Player;
            Monitoring = true;
            Monitorable = false;
            BodyEntered += OnBodyEntered;
            if (GetChildCount() == 0) {
                AddChild(new CollisionShape2D {
                    Shape = new RectangleShape2D { Size = new Vector2(600f, 200f) }
                });
            }
        }

        public override void _ExitTree() => BodyEntered -= OnBodyEntered;

        private void OnBodyEntered(Node2D body) {
            if (body is PlayerController player) Resolve(player);
        }

        /// <summary>
        /// The kill decision. Internal and directly callable so the pin tests
        /// exercise the exact path the <c>BodyEntered</c> signal takes.
        /// Returns true when a fall death was actually resolved.
        /// </summary>
        internal bool Resolve(PlayerController player) {
            if (player == null || !GodotObject.IsInstanceValid(player)) return false;
            // One fall, one death. A body already dead or respawning is mid-way
            // through the rewind flow this boundary started; re-resolving would
            // double-count the kill and re-raise OnPlayerDied, which the rewind
            // manager would answer with a second spent charge.
            if (player.CurrentState is CharacterState.Dead or CharacterState.Respawning) return false;
            // The kill cascades into the Dead transition, OnPlayerDied and the
            // rewind manager's world freeze (which releases pooled enemy
            // projectiles). All of that is physics-state mutation, and a
            // BodyEntered handler is mid-flush, so the guard makes PoolManager
            // and the safe setters defer themselves. Direct test calls stay
            // synchronous because the guard is a marker, not a deferral.
            using var scope = PhysicsCallbackGuard.Enter();
            StoryManager story = StoryManager.Instance;
            if (story != null && story.IsIntegrityClockRunning && story.TimelineIntegrityPercent <= 0f) {
                // F16: the clock's own timer-caused Collapse owns this update.
                // Do not also spend a rewind charge.
                return false;
            }
            player.KillPlayerNonHit();
            KillCount++;
            return true;
        }
    }
}
