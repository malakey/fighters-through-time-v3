using Godot;

namespace FTT.Core {

    public enum PlaceholderPoolCategory {
        Projectile,
        CombatVfx,
        EnvironmentalVfx,
        Enemy,
        Loot,
        PersistentConstruct
    }

    public partial class PooledPlaceholder : PooledNode, IPoolable, FTT.Environment.IStoryRewindable, FTT.Environment.IStoryRewindSimulation {
        [Export] public string ContentID = "placeholder";
        [Export] public PlaceholderPoolCategory Category;
        [Export(PropertyHint.Range, "0,36000,1")] public int LifetimeFrames;
        /// <summary>
        /// Particles this instance's emitters produce while alive. Reserved against
        /// the shared 500-particle budget on spawn and released on despawn; when the
        /// budget is exhausted the oldest emitters are silenced rather than the new
        /// one being dropped, so the newest gameplay feedback always shows.
        /// </summary>
        [Export(PropertyHint.Range, "0,500,1")] public int ParticleBudgetCost;
        [Export] public FTT.Environment.StoryRewindPolicy RewindPolicy { get; set; } = FTT.Environment.StoryRewindPolicy.RestoreCheckpointState;

        public Vector2 RuntimeVelocity { get; set; }
        public float RuntimeDamage { get; set; }
        public int RuntimeOwnerID { get; set; } = -1;

        private int _framesRemaining;
        private Vector2 _initialPosition;
        private Vector2 _checkpointPosition;
        private Vector2 _checkpointVelocity;
        private bool _rewindFrozen;

        public override void _PhysicsProcess(double delta) {
            if (_rewindFrozen) return;
            Position += RuntimeVelocity * (float)delta;
            if (_framesRemaining <= 0) return;
            _framesRemaining--;
            if (_framesRemaining == 0) ReturnToPool();
        }

        public void OnSpawn() {
            _framesRemaining = LifetimeFrames;
            Visible = true;
            SetPhysicsProcess(true);
            ResetParticles(ReserveParticleBudget());
            _initialPosition = GlobalPosition;
            _checkpointPosition = GlobalPosition;
            _checkpointVelocity = RuntimeVelocity;
            _rewindFrozen = false;
            if (Category == PlaceholderPoolCategory.PersistentConstruct && EventBus.Instance != null) {
                EventBus.Instance.OnCheckpointReached += CaptureCheckpointState;
                EventBus.Instance.OnRewindTriggered += OnRewindTriggered;
            }
        }

        public void OnDespawn() {
            if (EventBus.Instance != null) {
                EventBus.Instance.OnCheckpointReached -= CaptureCheckpointState;
                EventBus.Instance.OnRewindTriggered -= OnRewindTriggered;
            }
            RuntimeVelocity = Vector2.Zero;
            RuntimeDamage = 0f;
            RuntimeOwnerID = -1;
            _framesRemaining = 0;
            Position = Vector2.Zero;
            Rotation = 0f;
            Scale = Vector2.One;
            Modulate = Colors.White;
            ResetParticles(false);
            if (ParticleBudgetCost > 0) FTT.Combat.ParticleBudget.Shared.Release(GetInstanceId());
            SetPhysicsProcess(false);
            _rewindFrozen = false;
        }

        /// <summary>
        /// Claims this instance's slice of the shared particle budget. Returns false
        /// when the request cannot fit at all, in which case the instance spawns
        /// without particles rather than breaching the budget.
        /// </summary>
        private bool ReserveParticleBudget() {
            if (ParticleBudgetCost <= 0) return true;
            bool granted = FTT.Combat.ParticleBudget.Shared.TryReserve(
                GetInstanceId(), ParticleBudgetCost,
                out System.Collections.Generic.IReadOnlyList<ulong> evicted);
            foreach (ulong id in evicted) {
                if (!IsInstanceIdValid(id)) continue;
                if (InstanceFromId(id) is PooledPlaceholder displaced) displaced.ResetParticles(false);
            }
            return granted;
        }

        public void CaptureCheckpointState(string checkpointID) {
            if (Category != PlaceholderPoolCategory.PersistentConstruct) return;
            _checkpointPosition = GlobalPosition;
            _checkpointVelocity = RuntimeVelocity;
        }

        public void ApplyStoryRewind() {
            if (Category != PlaceholderPoolCategory.PersistentConstruct ||
                RewindPolicy == FTT.Environment.StoryRewindPolicy.PreserveCurrentState) return;
            GlobalPosition = RewindPolicy == FTT.Environment.StoryRewindPolicy.ResetToInitialState
                ? _initialPosition
                : _checkpointPosition;
            RuntimeVelocity = RewindPolicy == FTT.Environment.StoryRewindPolicy.ResetToInitialState
                ? Vector2.Zero
                : _checkpointVelocity;
        }

        public void SetStoryRewindFrozen(bool frozen) => _rewindFrozen = frozen;

        private void OnRewindTriggered(Vector2 targetPosition) => ApplyStoryRewind();

        private void ResetParticles(bool emitting) {
            // An instance spawned off-screen must not start emitting: the visibility
            // suspender owns that decision and will resume it when it scrolls in.
            if (emitting && GetNodeOrNull<FTT.Combat.PresentationVisibilitySuspender>(
                    FTT.Combat.PresentationVisibilitySuspender.NodeName)?.IsSuspended == true) {
                emitting = false;
            }
            Godot.Collections.Array<Node> children = GetChildren();
            using var childrenLifetime = children.AsDisposable();
            foreach (Node child in children) {
                if (child is GpuParticles2D gpuParticles) {
                    gpuParticles.Emitting = emitting;
                    if (emitting) gpuParticles.Restart();
                } else if (child is CpuParticles2D cpuParticles) {
                    cpuParticles.Emitting = emitting;
                    if (emitting) cpuParticles.Restart();
                }
            }
        }
    }
}
