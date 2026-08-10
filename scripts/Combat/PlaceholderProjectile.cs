using Godot;
using FTT.Core;

namespace FTT.Combat {

    public partial class PlaceholderProjectile : PooledNode, IPoolable {
        private float _speed;
        private bool _movingRight;
        private float _lifetime;
        private Hitbox _hitbox;
        private ColorRect _visual;
        private ColorRect _trail;
        private CollisionShape2D _shape;

        /// <summary>
        /// When true, the projectile despawns on its first confirmed contact and
        /// raises <see cref="Impacted"/> with the impact position (heavy detonating
        /// projectiles like Einstein's E=mc²). Cleared on despawn.
        /// </summary>
        public bool DetonateOnImpact { get; set; }

        /// <summary>Raised once at the impact position when a detonating projectile connects.</summary>
        public event System.Action<Vector2> Impacted;

        public override void _Ready() {
            AddToGroup("story_projectile");
        }

        public void Setup(float damage, Vector2 knockback, float speed, bool movingRight,
                          int ownerIndex, Color color, Vector2 size = default, float lifetime = 3f,
                          FTT.Characters.PlayerController sourcePlayer = null, AbilityData data = null) {
            if (size == default) size = new Vector2(24, 12);
            _speed = speed;
            _movingRight = movingRight;
            _lifetime = lifetime;

            EnsureNodes();
            _visual.Size = size;
            _visual.Position = -size / 2;
            _visual.Color = color;
            _trail.Size = new Vector2(size.X * 0.6f, size.Y * 0.5f);
            _trail.Position = new Vector2(movingRight ? -size.X * 0.6f : size.X * 0.5f, -size.Y * 0.25f);
            _trail.Color = new Color(color.R, color.G, color.B, 0.4f);
            ((RectangleShape2D)_shape.Shape).Size = size;

            _hitbox.AttackID = data?.AbilityID ?? "placeholder_projectile";
            _hitbox.HitboxID = "projectile";
            _hitbox.AttackClass = data?.Slot == FTT.Core.AbilitySlot.Ultimate
                ? AttackClass.Ultimate
                : AttackClass.Special;
            _hitbox.Damage = damage;
            _hitbox.KnockbackForce = knockback;
            _hitbox.HitstunDuration = data?.HitstunDuration ?? 0.2f;
            _hitbox.AppliedStatus = data?.AppliedStatus ?? FTT.Core.StatusType.None;
            _hitbox.StatusDuration = data?.StatusDuration ?? 0f;
            _hitbox.StatusIntensity = data?.StatusIntensity ?? 1f;
            _hitbox.ScreenShakeIntensity = data?.ScreenShakeIntensity ?? 0.2f;
            _hitbox.ScreenShakeDuration = data?.ScreenShakeDuration ?? 0.1f;
            _hitbox.OwnerPlayerIndex = ownerIndex;
            _hitbox.SourcePlayer = sourcePlayer;
            _hitbox.CollisionLayer = CollisionLayers.Projectile;
            _hitbox.CollisionMask = CollisionLayers.ProjectileMask;
            _hitbox.Monitorable = true;
            // Hostile-to-player shots clear with enemy projectiles on a Chronal
            // Rewind. Owner index alone is not enough: the Level 13 Mirror clone
            // fires with a non-negative player index, so the firing controller's
            // hostility flag is consulted at grouping time (audit H-8).
            bool hostileToPlayer = ownerIndex < 0 || (sourcePlayer?.IsStoryHostile ?? false);
            if (hostileToPlayer) AddToGroup("enemy_projectile");
            else RemoveFromGroup("enemy_projectile");
            _hitbox.Activate();
        }

        private void EnsureNodes() {
            if (_hitbox != null) return;
            _visual = new ColorRect { Name = "Visual", MouseFilter = Control.MouseFilterEnum.Ignore };
            AddChild(_visual);
            _trail = new ColorRect { Name = "Trail", MouseFilter = Control.MouseFilterEnum.Ignore };
            AddChild(_trail);
            _hitbox = new Hitbox { Name = "Hitbox" };
            _shape = new CollisionShape2D {
                Name = "CollisionShape2D",
                Shape = new RectangleShape2D()
            };
            _hitbox.AddChild(_shape);
            AddChild(_hitbox);
            _hitbox.HitConfirmed += OnHitConfirmed;
        }

        private void OnHitConfirmed(HitPayload payload, float damageApplied) {
            if (!DetonateOnImpact) return;
            Vector2 impactPosition = GlobalPosition;
            Impacted?.Invoke(impactPosition);
            ReturnToPool();
        }

        public override void _PhysicsProcess(double delta) {
            float dt = (float)delta;
            _lifetime -= dt;
            if (_lifetime <= 0) { ReturnToPool(); return; }

            var pos = GlobalPosition;
            pos.X += (_movingRight ? _speed : -_speed) * dt;
            GlobalPosition = pos;

            Modulate = new Color(1, 1, 1, Mathf.Min(1f, _lifetime * 2f));
        }

        public void OnSpawn() {
            _lifetime = 0f;
            Modulate = Colors.White;
        }

        public void OnDespawn() {
            _hitbox?.Deactivate();
            RemoveFromGroup("enemy_projectile");
            _speed = 0f;
            _lifetime = 0f;
            _movingRight = true;
            DetonateOnImpact = false;
            Impacted = null;
            Modulate = Colors.White;
        }
    }

}
