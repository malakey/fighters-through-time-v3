using Godot;

namespace FTT.Environment {

    /// <summary>
    /// A designated puzzle weight (V01c): a dedicated prop, not a combat body.
    ///
    /// <para><b>Package 12 W8 (GAP-08).</b> A weight now carries a stable
    /// <see cref="PropID"/> and the <see cref="PuzzleOwnerID"/> of the one puzzle it
    /// belongs to, and remembers its authored home transform the first time it
    /// enters the tree. A <see cref="PressurePlate"/> with an allowlist accepts only
    /// its own puzzle's props, and a <see cref="PuzzleResetStation"/> returns the
    /// props of an unsolved puzzle to their homes through
    /// <see cref="RestoreToAuthoredHome"/>.</para>
    /// </summary>
    public partial class WeightedObject : RigidBody2D {

        /// <summary>Group every puzzle weight joins (authored in the template too).</summary>
        public const string MovableWeightGroup = "movable_weight";

        [Export(PropertyHint.Range, "0.1,100,0.1")] public float WeightUnits = 1f;

        /// <summary>Stable prop identity, unique within its puzzle. Survives a reset.</summary>
        [Export] public string PropID = "";

        /// <summary>
        /// The <see cref="PuzzleManager.PuzzleID"/> this prop belongs to. Empty means
        /// "unowned": accepted only by plates with an empty allowlist, and never
        /// restored by any reset station.
        /// </summary>
        [Export] public string PuzzleOwnerID = "";

        private bool _homeCaptured;

        /// <summary>The authored starting transform, captured once at first tree entry.</summary>
        public Transform2D AuthoredHome { get; private set; } = Transform2D.Identity;

        /// <summary>True once <see cref="AuthoredHome"/> holds the authored transform.</summary>
        public bool HasAuthoredHome => _homeCaptured;

        /// <summary>
        /// How many V01b resets have restored this same live instance. The prop's
        /// identity (instance, <see cref="PropID"/>, owner) survives every reset.
        /// </summary>
        public int ResetGeneration { get; private set; }

        public override void _Ready() {
            AddToGroup(MovableWeightGroup);
            Mass = Mathf.Max(0.1f, WeightUnits);
            CaptureAuthoredHome();
        }

        /// <summary>
        /// Remembers the current transform as the authored home. Only the first call
        /// counts, so a prop that leaves and re-enters the tree (or is restored)
        /// never rebases its home onto wherever it happened to be.
        /// </summary>
        public void CaptureAuthoredHome() {
            if (_homeCaptured || !IsInsideTree()) return;
            AuthoredHome = GlobalTransform;
            _homeCaptured = true;
        }

        /// <summary>
        /// Explicit home override for props built in code after they entered the
        /// tree, and for replacement instances that must inherit a lost prop's home.
        /// </summary>
        public void SetAuthoredHome(Transform2D home) {
            AuthoredHome = home;
            _homeCaptured = true;
        }

        /// <summary>
        /// True when this prop is designated to <paramref name="puzzleID"/>. An
        /// unowned prop belongs to no puzzle.
        /// </summary>
        public bool BelongsTo(string puzzleID) =>
            !string.IsNullOrWhiteSpace(puzzleID) && PuzzleOwnerID == puzzleID;

        /// <summary>
        /// Returns the prop to its authored home with all residual motion cleared.
        /// Writes the body state straight to the physics server as well as the node,
        /// so the teleport holds even if the body is asleep or mid-integration. Does
        /// not validate geometry — <see cref="PuzzleResetStation"/> does that for the
        /// whole puzzle before calling this.
        /// </summary>
        public void RestoreToAuthoredHome() {
            if (!_homeCaptured) return;
            LinearVelocity = Vector2.Zero;
            AngularVelocity = 0f;
            GlobalTransform = AuthoredHome;
            if (IsInsideTree()) {
                Rid rid = GetRid();
                PhysicsServer2D.BodySetState(rid, PhysicsServer2D.BodyState.Transform, AuthoredHome);
                PhysicsServer2D.BodySetState(rid, PhysicsServer2D.BodyState.LinearVelocity, Vector2.Zero);
                PhysicsServer2D.BodySetState(rid, PhysicsServer2D.BodyState.AngularVelocity, 0f);
            }
            Sleeping = false;
            ResetGeneration++;
        }
    }
}
