using Godot;
using FTT.Combat;
using FTT.Characters;

namespace FTT.Characters.Abilities {

    /// <summary>
    /// Special 1 — Mass-Energy Conversion (E=mc²): a heavy projectile with a brief
    /// wind-up that deals minor contact damage, then detonates into a radiant burst
    /// at the impact point. Timing/damage come from the authored AbilityData.
    /// Story-only Resonance perks: Event Horizon (+20% burst damage to targets
    /// under TimeDilation) and Critical Mass (burst applies RadiantBurn for 3 s).
    /// </summary>
    public partial class EinsteinEmc2Blast : BaseSpecial {

        public const string EventHorizonPerkKey = "event_horizon";
        public const string CriticalMassPerkKey = "critical_mass";

        private const float ContactDamageShare = 1f / 3f;
        private const float BurstRadius = 100f;
        private const float EventHorizonDamageMultiplier = 1.2f;
        private const float CriticalMassBurnDuration = 3f;

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
            SpawnProjectile();
        }

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
        }

        private void SpawnProjectile() {
            if (Owner == null) return;
            float contactDamage = Mathf.Round((Data?.BaseDamage ?? 15f) * ContactDamageShare);
            var projectile = SpawnPlaceholderProjectile(
                Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? 50f : -50f, 0f),
                Data?.ProjectileSpeed ?? 350f, Owner.IsFacingRight, new Color(0.3f, 0.6f, 1f),
                new Vector2(20, 14), Data?.ProjectileLifetime ?? 5f, contactDamage);
            if (projectile != null) {
                projectile.DetonateOnImpact = true;
                projectile.Impacted += OnProjectileImpacted;
            }
        }

        private void OnProjectileImpacted(Vector2 impactPosition) {
            // Area signals fire while the physics space is locked; defer the burst
            // query one step so the shape cast is legal.
            CallDeferred(nameof(Detonate), impactPosition);
        }

        private void Detonate(Vector2 impactPosition) {
            if (Owner == null || !IsInstanceValid(Owner)) return;

            // Burst flash visual only; damage is applied through the shape query so
            // per-target perk multipliers can be evaluated.
            SpawnPlaceholderZone(impactPosition, 0f, 0.25f, 1f, new Color(1f, 0.9f, 0.4f), BurstRadius);

            var space = Owner.GetWorld2D()?.DirectSpaceState;
            if (space == null) return;
            uint targetHurtboxLayer = Owner.PlayerIndex == 0
                ? FTT.Core.CollisionLayers.EnemyHurtbox
                : FTT.Core.CollisionLayers.PlayerHurtbox;
            var query = new PhysicsShapeQueryParameters2D {
                Shape = new CircleShape2D { Radius = BurstRadius },
                Transform = new Transform2D(0f, impactPosition),
                CollideWithAreas = true,
                CollideWithBodies = false,
                CollisionMask = targetHurtboxLayer
            };

            bool criticalMass = Owner.HasStoryPerk(CriticalMassPerkKey);
            bool eventHorizon = Owner.HasStoryPerk(EventHorizonPerkKey);
            foreach (Godot.Collections.Dictionary result in space.IntersectShape(query, 16)) {
                if (result["collider"].AsGodotObject() is not Hurtbox hurtbox) continue;
                if (hurtbox.OwnerPlayerIndex == Owner.PlayerIndex) continue;

                float burstDamage = Data?.BaseDamage ?? 15f;
                if (eventHorizon && TargetHasTimeDilation(hurtbox)) {
                    burstDamage *= EventHorizonDamageMultiplier;
                }

                float dealt = hurtbox.TakeHit(new HitPayload {
                    AttackerIndex = Owner.PlayerIndex,
                    AttackID = Data?.AbilityID ?? "einstein_mass_energy_conversion",
                    HitboxID = "burst",
                    AttackClass = AttackClass.Special,
                    Damage = burstDamage * Owner.StorySpecialDamageMultiplier,
                    Knockback = Data?.KnockbackForce ?? new Vector2(4, -2),
                    HitstunDuration = Data?.HitstunDuration ?? 0.2f,
                    HitOrigin = impactPosition,
                    AttackerFacingRight = Owner.IsFacingRight,
                    AppliedStatus = criticalMass ? FTT.Core.StatusType.RadiantBurn : FTT.Core.StatusType.None,
                    StatusDuration = criticalMass ? CriticalMassBurnDuration : 0f,
                    StatusIntensity = 1f,
                    ScreenShakeIntensity = Data?.ScreenShakeIntensity ?? 0.3f,
                    ScreenShakeDuration = Data?.ScreenShakeDuration ?? 0.15f
                });
                if (dealt > 0f) Owner.AddInfluenceFromDamageDealt(dealt);
            }
        }

        private static bool TargetHasTimeDilation(Hurtbox hurtbox) {
            Node current = hurtbox.GetParent();
            while (current != null) {
                if (current is PlayerController player) {
                    var status = player.GetNodeOrNull<StatusController>("StatusController");
                    return status?.ActiveType == FTT.Core.StatusType.TimeDilation;
                }
                if (current is FTT.Enemies.EnemyController enemy) {
                    return enemy.ActiveStatusType == FTT.Core.StatusType.TimeDilation;
                }
                current = current.GetParent();
            }
            return false;
        }
    }
}
