using Godot;
using FTT.Combat;
using FTT.Characters;
using FTT.Core;

namespace FTT.Characters.Abilities {

    /// <summary>
    /// Special 2 — Relativity Rift: a localized distortion field that inflicts
    /// TimeDilation (-50% enemy movement/jump/animation speed) and continuous chip
    /// damage (authored: 1.5 per 0.5 s tick over 3 s). If Einstein stands inside
    /// his own rift he gains +25% movement speed, mirroring the Fighter-sim zone.
    /// All tuning comes from the authored AbilityData resource.
    /// </summary>
    public partial class EinsteinRelativityRift : BaseSpecial {

        private const float RiftRadius = 100f;
        private const float OwnerSpeedMultiplier = 1.25f;

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
            EmitCharacterVfx(
                "res://scenes/vfx/einstein/EinsteinRelativityRiftVfx.tscn",
                Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? 72f : -72f, -26f));
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
            SpawnRift();
        }

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
        }

        /// <summary>
        /// Event Horizon (Story-only Resonance major, V7.6): hostile
        /// projectiles crossing the rift are slowed to half speed. The rift's
        /// live radius and centre, so the sweep below can find them.
        /// </summary>
        public const string EventHorizonPerkKey = EinsteinEmc2Blast.EventHorizonPerkKey;

        /// <summary>V7.6 Event Horizon: speed a projectile keeps while inside the rift.</summary>
        public const float EventHorizonProjectileSpeedScale = 0.5f;

        private Vector2 _riftCenter;
        private float _riftRadius;
        private float _riftLifetime;

        /// <summary>The deployed rift's effective radius after Story minors. Test seam.</summary>
        public float ActiveRiftRadiusPixels => _riftRadius;

        /// <summary>True while a deployed rift is still open. Test seam.</summary>
        public bool RiftActive => _riftLifetime > 0f;

        public override void _PhysicsProcess(double delta) {
            base._PhysicsProcess(delta);
            if (_riftLifetime <= 0f) return;
            _riftLifetime -= (float)delta;
            SlowCrossingProjectiles();
        }

        /// <summary>
        /// Event Horizon's second clause. Halves the velocity of every hostile
        /// projectile inside the rift, each frame it remains inside, and
        /// restores nothing on exit - the projectile simply keeps whatever
        /// speed it left with, which is the design's "slowed to half speed"
        /// without a per-shot bookkeeping record. Never touches beams, zones or
        /// hazards: only pooled enemy projectiles join the group.
        /// </summary>
        private void SlowCrossingProjectiles() {
            if (Owner == null || !Owner.HasStoryPerk(EventHorizonPerkKey) || !IsInsideTree()) return;
            Godot.Collections.Array<Node> shots =
                GetTree().GetNodesInGroup(FTT.Enemies.EnemyProjectile.GroupName);
            using var lifetime = shots.AsDisposable();
            foreach (Node node in shots) {
                if (node is not FTT.Enemies.EnemyProjectile shot || !shot.IsInsideTree()) continue;
                if (shot.GlobalPosition.DistanceTo(_riftCenter) > _riftRadius) continue;
                shot.ScaleVelocity(EventHorizonProjectileSpeedScale);
            }
        }

        private void SpawnRift() {
            if (Owner == null) return;
            float tickInterval = (Data?.DamageTickIntervalFrames ?? 30) / 60f;
            if (tickInterval <= 0f) tickInterval = 0.5f;
            _riftCenter = Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? 100f : -100f, 0f);
            _riftRadius = RiftRadius * Owner.StoryZoneRadiusMultiplier
                * Owner.StoryScoped("AbilityRange", "relativity_rift");
            _riftLifetime = Data?.Lifetime > 0f ? Data.Lifetime : 3f;
            SpawnPlaceholderZone(
                _riftCenter,
                Data?.BaseDamage ?? 1.5f,
                _riftLifetime,
                tickInterval,
                new Color(0.4f, 0.3f, 0.9f),
                // Package 11 A4: V7.6 re-scopes "Minor Rift Range" from the
                // character-wide ZoneRadius lane onto
                // AbilityRange(relativity_rift). Neutral 1.0 outside Story.
                _riftRadius,
                Data?.AppliedStatus ?? FTT.Core.StatusType.TimeDilation,
                Data?.StatusDuration > 0f ? Data.StatusDuration : 3f,
                Data?.StatusIntensity ?? 1f,
                OwnerSpeedMultiplier);
        }
    }
}
