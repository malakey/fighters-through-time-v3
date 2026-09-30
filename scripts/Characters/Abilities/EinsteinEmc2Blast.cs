using Godot;
using FTT.Combat;
using FTT.Characters;
using FTT.Core;

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
        /// <summary>Fallback burst radius (1.2 units) when the resource authors none.</summary>
        private const float DefaultBurstRadius = 72f;
        private const float EventHorizonDamageMultiplier = 1.2f;
        private const float CriticalMassBurnDuration = 3f;

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
            EmitCharacterVfx(
                "res://scenes/vfx/einstein/EinsteinMassEnergyVfx.tscn",
                Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? 38f : -38f, -28f));
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
            // Package 13 W7a (E05): the designed two-stage hit is authored —
            // 7 contact + 20 burst — rather than a hidden 1/3 split.
            float contactDamage = Data?.ProjectileContactDamage > 0f
                ? Data.ProjectileContactDamage
                : Mathf.Round((Data?.BaseDamage ?? 15f) * ContactDamageShare);
            var projectile = SpawnPlaceholderProjectile(
                Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? 50f : -50f, 0f),
                Data?.ProjectileSpeed ?? 720f, Owner.IsFacingRight, new Color(0.3f, 0.6f, 1f),
                new Vector2(20, 14), Data?.ProjectileLifetime ?? 5f, contactDamage);
            if (projectile != null) {
                projectile.DetonateOnImpact = true;
                projectile.MakeMinorContactStage();
                // E05: a straight shot with no range limit — it bursts on a
                // fighter, terrain or a wall (and at its lifetime cap).
                projectile.BurstsOnTerrain = Data?.ProjectileBurstsOnTerrain ?? true;
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

            // E01 (Package 13 W7a): a burst inside Einstein's own active rift
            // collapses it — the same E=mc² execution, never a second cast. Who
            // is caught is decided at the detonation instant, before the burst
            // resolves, so a blocker the burst then shatters is not pulled.
            GetOwnRift()?.TryCollapse(impactPosition);

            // Burst flash visual only; damage is applied through the shape query so
            // per-target perk multipliers can be evaluated.
            float burstRadius = BurstRadius;
            SpawnPlaceholderZone(impactPosition, 0f, 0.25f, 1f, new Color(1f, 0.9f, 0.4f), burstRadius);

            var space = Owner.GetWorld2D()?.DirectSpaceState;
            if (space == null) return;
            uint targetHurtboxLayer = Owner.PlayerIndex == 0
                ? FTT.Core.CollisionLayers.EnemyHurtbox
                : FTT.Core.CollisionLayers.PlayerHurtbox;
            var query = new PhysicsShapeQueryParameters2D {
                Shape = new CircleShape2D { Radius = burstRadius },
                Transform = new Transform2D(0f, impactPosition),
                CollideWithAreas = true,
                CollideWithBodies = false,
                CollisionMask = StoryShapeQuery.DeliveryMask(targetHurtboxLayer)
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

                HitPayload hit = Stamp(new HitPayload {
                    AttackerIndex = Owner.PlayerIndex,
                    AttackID = Data?.AbilityID ?? "einstein_mass_energy_conversion",
                    HitboxID = "burst",
                    AttackClass = AttackClass.Special,
                    Damage = burstDamage * Owner.StorySpecialDamageMultiplier,
                    Knockback = Data?.KnockbackForce ?? new Vector2(4, -2),
                    HitstunDuration = Data?.HitstunDuration ?? 0.2f,
                    HitOrigin = impactPosition,
                    AttackerFacingRight = Owner.IsFacingRight,
                    // Package 11 A4 (Critical Mass, V7.6): the Radiant Burn is
                    // deliberately NOT carried on this payload. The design
                    // requires it to land AFTER the triggering hit resolves, so
                    // the 1.25x vulnerability cannot amplify the burst that
                    // applied it, and to be skipped entirely when the hit was
                    // blocked or the target was invulnerable.
                    AppliedStatus = FTT.Core.StatusType.None,
                    StatusDuration = 0f,
                    StatusIntensity = 1f,
                    ScreenShakeIntensity = Data?.ScreenShakeIntensity ?? 0.3f,
                    ScreenShakeDuration = Data?.ScreenShakeDuration ?? 0.15f
                });
                float dealt = hurtbox.TakeHit(hit);
                Credit(in hit, dealt);
                // dealt > 0 is exactly "the hit resolved into real damage":
                // a block, an invulnerable target and a DoT-only contact all
                // return zero, which is the design's gating list.
                if (criticalMass && dealt > 0f) ApplyCriticalMassBurn(hurtbox);
            }
        }

        /// <summary>The burst radius in pixels: the authored value, 1.2 units by default.</summary>
        public float BurstRadius => Data?.ProjectileBurstRadius > 0f ? Data.ProjectileBurstRadius : DefaultBurstRadius;

        /// <summary>Test seam: resolves the burst at <paramref name="impactPosition"/> synchronously.</summary>
        public void DetonateForTest(Vector2 impactPosition) => Detonate(impactPosition);

        private EinsteinRelativityRift GetOwnRift() {
            if (Owner == null || !IsInstanceValid(Owner)) return null;
            Godot.Collections.Array<Node> children = Owner.GetChildren();
            using var lifetime = children.AsDisposable();
            foreach (Node child in children) {
                if (child is EinsteinRelativityRift rift) return rift;
            }
            return null;
        }

        /// <summary>
        /// Critical Mass (Story-only Resonance major, V7.6). Applies Radiant
        /// Burn - 1.25x damage taken for 3 seconds with no periodic damage,
        /// which is exactly what <c>RadiantBurnStrategy</c> already models - to
        /// a target the burst actually damaged, AFTER that hit resolved. It
        /// occupies the DAMAGE slot and replaces under the shared
        /// stronger-wins rule, so it coexists with the rift's Time Dilation in
        /// the control slot. Never fires on projectile contact (only the
        /// detonation burst calls this), a blocked hit, or an invulnerable
        /// target.
        /// </summary>
        private static void ApplyCriticalMassBurn(Hurtbox hurtbox) {
            Node current = hurtbox.GetParent();
            while (current != null) {
                if (current is PlayerController player) {
                    player.GetNodeOrNull<StatusController>("StatusController")
                        ?.ApplyStatus(FTT.Core.StatusType.RadiantBurn, CriticalMassBurnDuration, 1f);
                    return;
                }
                if (current is FTT.Enemies.EnemyController enemy) {
                    enemy.ApplyStatusEffect(FTT.Core.StatusType.RadiantBurn, CriticalMassBurnDuration, 1f);
                    return;
                }
                current = current.GetParent();
            }
        }

        private static bool TargetHasTimeDilation(Hurtbox hurtbox) {
            Node current = hurtbox.GetParent();
            while (current != null) {
                if (current is PlayerController player) {
                    var status = player.GetNodeOrNull<StatusController>("StatusController");
                    return status?.HasStatus(FTT.Core.StatusType.TimeDilation) == true;
                }
                if (current is FTT.Enemies.EnemyController enemy) {
                    return enemy.HasStatusEffect(FTT.Core.StatusType.TimeDilation);
                }
                current = current.GetParent();
            }
            return false;
        }
    }
}
