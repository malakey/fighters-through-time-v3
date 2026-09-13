using Godot;
using FTT.Characters;
using FTT.Core;

namespace FTT.Environment {

    /// <summary>
    /// Rope-swing anchor (Level 7 Nassau rigging, Level 10 Globe gallery). The body
    /// swings about its own origin through <see cref="AmplitudeDegrees"/> and carries
    /// a child <see cref="LedgeGrabPoint"/>, so grabbing it reuses the tested
    /// <c>CharacterState.LedgeHanging</c> path — no new player state.
    ///
    /// PlayerController pins the player to <c>LedgeGrabPoint.HangPosition</c> once, at
    /// grab time, and zeroes velocity for the duration of the hang; it never
    /// re-reads the ledge afterwards. Two consequences are handled here rather than
    /// in shared runtime code:
    ///   * <see cref="CarryOccupant"/> re-pins the occupant to the moving hang anchor
    ///     every physics frame, so the player actually rides the swing.
    ///   * the ledge jump-off sets only <c>Velocity.Y</c>, so it would inherit no
    ///     horizontal momentum. On release this node adds its own measured tangential
    ///     velocity, scaled by <see cref="ReleaseLaunchAssist"/> and capped by
    ///     <see cref="MaxLaunchSpeed"/>. Set the assist to 0 for a dead-stop anchor.
    /// </summary>
    public partial class PendulumAnchor : AnimatableBody2D, IStoryRewindable, IStoryTimeFreezable {
        [Signal] public delegate void OccupantLaunchedEventHandler(int playerIndex, Vector2 launchVelocity);

        [Export] public string AnchorID = "";
        [Export(PropertyHint.Range, "0,170,0.5")] public float AmplitudeDegrees = 45f;
        [Export(PropertyHint.Range, "0.1,60,0.05")] public float PeriodSeconds = 2.5f;
        /// <summary>Normalized 0-1 offset so neighbouring anchors swing out of phase.</summary>
        [Export(PropertyHint.Range, "0,1,0.01")] public float PhaseOffset;
        [Export] public NodePath LedgeGrabPointPath = "LedgeGrabPoint";
        [Export] public bool CarryOccupant = true;
        [Export(PropertyHint.Range, "0,4,0.05")] public float ReleaseLaunchAssist = 1f;
        [Export(PropertyHint.Range, "0,4000,10")] public float MaxLaunchSpeed = 900f;
        [Export] public bool Enabled = true;
        [Export] public StoryRewindPolicy RewindPolicy { get; set; } = StoryRewindPolicy.RestoreCheckpointState;

        private Vector2 _previousGrabPosition;
        private PlayerController _lastOccupant;
        private float _checkpointPhase;
        private bool _initialized;

        /// <summary>Normalized 0-1 swing position.</summary>
        public float SwingPhase { get; private set; }
        /// <summary>World-space velocity of the hang anchor, in pixels per second.</summary>
        public Vector2 AnchorVelocity { get; private set; }
        public LedgeGrabPoint Ledge => GetNodeOrNull<LedgeGrabPoint>(LedgeGrabPointPath);
        public PlayerController Occupant => Ledge?.Occupant;
        public Vector2 GrabPosition => Ledge?.HangPosition ?? GlobalPosition;

        public override void _Ready() {
            AddToGroup("puzzle_object");
            SyncToPhysics = false;
            ApplySwingTransform();
            _previousGrabPosition = GrabPosition;
            _initialized = true;
            if (EventBus.Instance != null) {
                EventBus.Instance.OnCheckpointReached += CaptureCheckpointState;
                EventBus.Instance.OnRewindTriggered += OnRewind;
            }
        }

        public override void _ExitTree() {
            if (EventBus.Instance != null) {
                EventBus.Instance.OnCheckpointReached -= CaptureCheckpointState;
                EventBus.Instance.OnRewindTriggered -= OnRewind;
            }
            _lastOccupant = null;
        }

        public override void _PhysicsProcess(double delta) {
            if (_timeFrozen) return;
            AdvanceSwing((float)delta);
        }

        public void AdvanceSwing(float dt) {
            if (!_initialized) { _previousGrabPosition = GrabPosition; _initialized = true; }
            if (Enabled && dt > 0f) {
                SwingPhase = Mathf.PosMod(SwingPhase + dt / Mathf.Max(0.01f, PeriodSeconds), 1f);
            }
            ApplySwingTransform();

            Vector2 grab = GrabPosition;
            AnchorVelocity = dt > 0f ? (grab - _previousGrabPosition) / dt : Vector2.Zero;
            _previousGrabPosition = grab;
            UpdateOccupant();
        }

        /// <summary>
        /// Adds the anchor's current tangential velocity to a player who just let go.
        /// Public so an ability or a level script can hand off a swing explicitly.
        /// </summary>
        public Vector2 ApplyReleaseLaunch(PlayerController player) {
            if (player == null || !GodotObject.IsInstanceValid(player)) return Vector2.Zero;
            Vector2 launch = AnchorVelocity * ReleaseLaunchAssist;
            if (launch.Length() > MaxLaunchSpeed) launch = launch.Normalized() * MaxLaunchSpeed;
            player.Velocity += launch;
            EmitSignal(SignalName.OccupantLaunched, player.PlayerIndex, launch);
            return launch;
        }

        public void CaptureCheckpointState(string checkpointID) => _checkpointPhase = SwingPhase;

        public void ApplyStoryRewind() {
            if (RewindPolicy == StoryRewindPolicy.PreserveCurrentState) return;
            SwingPhase = RewindPolicy == StoryRewindPolicy.ResetToInitialState ? 0f : _checkpointPhase;
            ApplySwingTransform();
            _previousGrabPosition = GrabPosition;
            AnchorVelocity = Vector2.Zero;
            _lastOccupant = null;
        }

        private void ApplySwingTransform() =>
            RotationDegrees = AmplitudeDegrees * Mathf.Sin((SwingPhase + PhaseOffset) * Mathf.Tau);

        private void UpdateOccupant() {
            LedgeGrabPoint ledge = Ledge;
            if (ledge == null) return;
            PlayerController occupant = ledge.Occupant;
            if (occupant != null && GodotObject.IsInstanceValid(occupant)) {
                _lastOccupant = occupant;
                if (CarryOccupant) occupant.GlobalPosition = ledge.HangPosition;
                return;
            }
            if (_lastOccupant == null) return;
            PlayerController released = _lastOccupant;
            _lastOccupant = null;
            ApplyReleaseLaunch(released);
        }

        private void OnRewind(Vector2 targetPosition) => ApplyStoryRewind();

        // === IStoryTimeFreezable (V7.6 Time Freeze) ===========================

        private bool _timeFrozen;

        /// <summary>True while Time Freeze holds the world. Test seam.</summary>
        public bool IsTimeFrozen => _timeFrozen;

        /// <summary>
        /// Stops simulating in place. Nothing else is mutated, so the phase, the
        /// timer and the position all survive and resume with no catch-up tick —
        /// the collision shape stays live throughout.
        /// </summary>
        public void SetTimeFrozen(bool frozen) => _timeFrozen = frozen;

    }
}
