using Godot;
using FTT.Combat;
using FTT.Characters;

namespace FTT.Characters.Abilities {

    public partial class EinsteinEmc2Blast : BaseSpecial {

        private const float StartupDuration = 0.3f;
        private const float ActiveDuration = 0.1f;
        private const float RecoveryDuration = 0.2f;

        protected override void OnStartup() {
            PhaseTimer = StartupDuration;
            if (Data?.GrantsHyperArmor == true) {
                // Apply hyper armor visual
            }
        }

        protected override void OnActive() {
            PhaseTimer = ActiveDuration;
            SpawnProjectile();
        }

        protected override void OnRecovery() {
            PhaseTimer = RecoveryDuration;
        }

        private void SpawnProjectile() {
            if (Data?.ProjectileScene == null && Owner != null) {
                SpawnPlaceholderProjectile(
                    Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? 50f : -50f, 0f),
                    Data?.ProjectileSpeed ?? 350f, Owner.IsFacingRight, new Color(0.3f, 0.6f, 1f),
                    new Vector2(20, 14));
                Owner.SpecialOneCooldownTimer = Data?.CooldownDuration ?? 6f;
                return;
            }
            if (Owner == null) return;

            var projectile = FTT.Core.PoolManager.Instance?.Spawn(
                Data.ProjectileScene,
                Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? 50f : -50f, 0f)
            );

            if (projectile is EinsteinProjectile ep) {
                ep.Initialize(Data.BaseDamage, Data.KnockbackForce, Data.ProjectileSpeed, Owner.IsFacingRight, Owner.PlayerIndex);
            }

            Owner.SpecialOneCooldownTimer = Data.CooldownDuration;
            FTT.Core.EventBus.Instance?.RaiseCooldownStarted(new FTT.Core.CooldownPayload {
                PlayerIndex = Owner.PlayerIndex,
                Slot = FTT.Core.AbilitySlot.Special1,
                Duration = Data.CooldownDuration
            });
        }
    }

    public partial class EinsteinProjectile : FTT.Core.PooledNode, FTT.Core.IPoolable {
        private float _speed;
        private float _damage;
        private Vector2 _knockback;
        private bool _movingRight;
        private int _ownerIndex;
        private float _lifetime;
        private const float MaxLifetime = 5.0f;

        private Hitbox _hitbox;

        public void Initialize(float damage, Vector2 knockback, float speed, bool facingRight, int ownerIndex) {
            _damage = damage;
            _knockback = knockback;
            _speed = speed * 60f;
            _movingRight = facingRight;
            _ownerIndex = ownerIndex;
            _lifetime = MaxLifetime;

            _hitbox = GetNodeOrNull<Hitbox>("Hitbox");
            if (_hitbox != null) {
                _hitbox.Damage = _damage;
                _hitbox.KnockbackForce = _knockback;
                _hitbox.OwnerPlayerIndex = _ownerIndex;
                _hitbox.Activate();
            }
        }

        public void OnSpawn() {
            _lifetime = MaxLifetime;
        }

        public void OnDespawn() {
            _hitbox?.Deactivate();
        }

        public override void _PhysicsProcess(double delta) {
            float dt = (float)delta;
            _lifetime -= dt;
            if (_lifetime <= 0) {
                ReturnToPool();
                return;
            }

            var pos = GlobalPosition;
            pos.X += (_movingRight ? _speed : -_speed) * dt;
            GlobalPosition = pos;
        }
    }
}
