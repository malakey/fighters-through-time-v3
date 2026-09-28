using Godot;
using FTT.Combat;
using FTT.Characters;

namespace FTT.Characters.Abilities {

    /// <summary>
    /// Story-mode Sonata Drift construct: a floating musical staff platform that
    /// Mozart can land on and run along. The body is a one-way platform
    /// (StaticBody2D on the OneWayPlatform layer with one-way collision), so he
    /// can jump up through it and drop through it with the universal down-down
    /// input. The platform deals no damage, ignores attacks, expires after the
    /// authored 3-second lifetime, freezes during Chronal Rewind, and fully
    /// resets its pooled state (collision included) between uses.
    /// </summary>
    public partial class SonataPlatformNode : FTT.Core.PooledNode, FTT.Core.IPoolable, FTT.Environment.IStoryRewindSimulation, FTT.Environment.IStoryTimeFreezable {

        /// <summary>Body top relative to the root origin (body is 12 px thick, centered).</summary>
        private const float SurfaceOffsetPixels = 6f;
        private const float StandHalfWidthPixels = 100f;
        private const float StandVerticalTolerancePixels = 14f;

        public int OwnerIndex { get; private set; }

        private PlayerController _ownerPlayer;
        private float _lifetime;
        private bool _rewindFrozen;
        private CollisionShape2D _bodyShape;

        public override void _Ready() {
            _bodyShape = GetNodeOrNull<CollisionShape2D>("Body/CollisionShape2D");
        }

        public void Initialize(AbilityData data, PlayerController owner) {
            _ownerPlayer = owner;
            OwnerIndex = owner?.PlayerIndex ?? 0;
            // Story-only PersistentDuration minors extend the staff's lifespan.
            _lifetime = (data?.Lifetime > 0f ? data.Lifetime : 3f)
                * (owner?.StoryPersistentDurationMultiplier ?? 1f);
            _rewindFrozen = false;
            _landingRefundSpent = false;
            SetBodyCollisionEnabled(true);
        }

        private bool _landingRefundSpent;

        /// <summary>
        /// Package 12 W4: Sonata Drift's staff-landing refund is granted once
        /// per platform, so hopping on the same staff cannot keep halving the
        /// recharge. Returns true exactly once per spawned platform.
        /// </summary>
        public bool TryConsumeLandingRefund() {
            if (_landingRefundSpent) return false;
            _landingRefundSpent = true;
            return true;
        }

        /// <summary>
        /// True while the given player stands on this staff's surface. Used by the
        /// Virtuoso Dash perk; the player origin sits at the feet, so a grounded
        /// player on the staff rests within a small tolerance of the surface line.
        /// </summary>
        public bool IsStandingOn(PlayerController player) {
            if (player == null || !IsInstanceValid(player) || !player.IsOnFloor()) return false;
            float surfaceY = GlobalPosition.Y - SurfaceOffsetPixels;
            return Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X) <= StandHalfWidthPixels
                && Mathf.Abs(player.GlobalPosition.Y - surfaceY) <= StandVerticalTolerancePixels;
        }

        public void OnSpawn() { }

        public void OnDespawn() {
            if (_ownerPlayer != null && IsInstanceValid(_ownerPlayer)) {
                _ownerPlayer.ActivePersistentObjects.Remove(this);
            }
            // A pooled inactive platform is reparented, not freed, so its static
            // body would keep colliding wherever it lands; disable the shape.
            SetBodyCollisionEnabled(false);
            _ownerPlayer = null;
            _lifetime = 0f;
            _rewindFrozen = false;
            _landingRefundSpent = false;
        }

        public void SetStoryRewindFrozen(bool frozen) => _rewindFrozen = frozen;

        public override void _PhysicsProcess(double delta) {
            if (_rewindFrozen) return;
            _lifetime -= (float)delta;
            if (_lifetime <= 0f) ReturnToPool();
        }

        private void SetBodyCollisionEnabled(bool enabled) {
            _bodyShape ??= GetNodeOrNull<CollisionShape2D>("Body/CollisionShape2D");
            _bodyShape?.SetDeferred(CollisionShape2D.PropertyName.Disabled, !enabled);
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
