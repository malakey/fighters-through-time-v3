using Godot;
using FTT.Combat;
using FTT.Characters;

namespace FTT.Characters.Abilities {

    public partial class MozartRequiemChord : BaseSpecial {
        private const float StartupDuration = 0.25f;
        private const float ActiveDuration = 0.1f;
        private const float RecoveryDuration = 0.3f;

        protected override void OnStartup() {
            PhaseTimer = StartupDuration;
        }

        protected override void OnActive() {
            PhaseTimer = ActiveDuration;
            LaunchChord();
            Owner.SpecialOneCooldownTimer = Data?.CooldownDuration ?? 10f;
            FTT.Core.EventBus.Instance?.RaiseCooldownStarted(new FTT.Core.CooldownPayload {
                PlayerIndex = Owner.PlayerIndex,
                Slot = FTT.Core.AbilitySlot.Special1,
                Duration = Data?.CooldownDuration ?? 10f
            });
        }

        protected override void OnRecovery() {
            PhaseTimer = RecoveryDuration;
        }

        private void LaunchChord() {
            if (Data?.ProjectileScene == null && Owner != null) {
                SpawnPlaceholderProjectile(
                    Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? 45f : -45f, -5f),
                    Data?.ProjectileSpeed ?? 280f, Owner.IsFacingRight, new Color(0.6f, 0.4f, 0.8f),
                    new Vector2(22, 16));
                return;
            }
            if (Owner == null) return;

            var chord = FTT.Core.PoolManager.Instance?.Spawn(
                Data.ProjectileScene,
                Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? 45f : -45f, -5f)
            );

            if (chord is MozartRequiemChordProjectile projectile) {
                projectile.Initialize(
                    Data.BaseDamage,
                    Data.KnockbackForce,
                    Data.IsMultiHit ? Data.HitCount : 3,
                    Data.ProjectileSpeed,
                    Owner.IsFacingRight,
                    Owner.PlayerIndex
                );
            }
        }
    }

    public partial class MozartRequiemChordProjectile : FTT.Core.PooledNode, FTT.Core.IPoolable {
        private float _damage;
        private Vector2 _knockback;
        private int _hitCount;
        private bool _movingRight;
        private int _ownerIndex;
        private float _speed;
        private float _lifetime = 3f;
        private Hitbox _hitbox;
        private bool _hasImpacted;

        public void Initialize(float damage, Vector2 knockback, int hitCount,
            float speed, bool facingRight, int ownerIndex) {
            _damage = damage;
            _knockback = knockback;
            _hitCount = hitCount;
            _movingRight = facingRight;
            _ownerIndex = ownerIndex;
            _speed = speed * 60f;
            _lifetime = 3f;
            _hasImpacted = false;

            _hitbox = GetNodeOrNull<Hitbox>("Hitbox");
            if (_hitbox != null) {
                _hitbox.Damage = _damage;
                _hitbox.KnockbackForce = _knockback;
                _hitbox.OwnerPlayerIndex = _ownerIndex;
                _hitbox.Activate();
            }
        }

        public void OnSpawn() { }
        public void OnDespawn() => _hitbox?.Deactivate();

        public override void _PhysicsProcess(double delta) {
            float dt = (float)delta;
            _lifetime -= dt;
            if (_lifetime <= 0) {
                ReturnToPool();
                return;
            }

            if (!_hasImpacted) {
                var pos = GlobalPosition;
                pos.X += (_movingRight ? _speed : -_speed) * dt;
                GlobalPosition = pos;
            }
        }

        public void TriggerShockwave() {
            if (_hasImpacted) return;
            _hasImpacted = true;

            for (int i = 0; i < _hitCount; i++) {
                float delay = i * 0.12f;
                GetTree().CreateTimer(delay).Timeout += () => {
                    var shockbox = GetNodeOrNull<Hitbox>("ShockwaveHitbox");
                    if (shockbox == null) return;
                    shockbox.Damage = _damage * 0.6f;
                    shockbox.KnockbackForce = _knockback;
                    shockbox.OwnerPlayerIndex = _ownerIndex;
                    shockbox.Activate();
                    GetTree().CreateTimer(0.08f).Timeout += () => shockbox.Deactivate();
                };
            }

            GetTree().CreateTimer(_hitCount * 0.12f + 0.1f).Timeout += ReturnToPool;
        }
    }

    public partial class MozartFortissimoWave : BaseSpecial {
        private const float StartupDuration = 0.2f;
        private const float ActiveDuration = 0.25f;
        private const float RecoveryDuration = 0.35f;
        private const float WaveDamage = 12f;

        protected override void OnStartup() {
            PhaseTimer = StartupDuration;
        }

        protected override void OnActive() {
            PhaseTimer = ActiveDuration;
            EmitWave();
            Owner.SpecialTwoCooldownTimer = Data?.CooldownDuration ?? 10f;
            FTT.Core.EventBus.Instance?.RaiseCooldownStarted(new FTT.Core.CooldownPayload {
                PlayerIndex = Owner.PlayerIndex,
                Slot = FTT.Core.AbilitySlot.Special2,
                Duration = Data?.CooldownDuration ?? 10f
            });
        }

        protected override void OnRecovery() {
            PhaseTimer = RecoveryDuration;
        }

        private void EmitWave() {
            if (Data?.ProjectileScene == null && Owner != null) {
                SpawnPlaceholderProjectile(
                    Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? 50f : -50f, 0f),
                    Data?.ProjectileSpeed ?? 250f, Owner.IsFacingRight, new Color(0.8f, 0.6f, 0.9f),
                    new Vector2(28, 20));
                return;
            }
            if (Owner == null) return;

            var wave = FTT.Core.PoolManager.Instance?.Spawn(
                Data.ProjectileScene,
                Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? 50f : -50f, 0f)
            );

            if (wave is MozartFortissimoWaveNode node) {
                node.Initialize(
                    WaveDamage,
                    Data.KnockbackForce,
                    Owner.IsFacingRight,
                    Owner.PlayerIndex
                );
            }
        }
    }

    public partial class MozartFortissimoWaveNode : FTT.Core.PooledNode, FTT.Core.IPoolable {
        private float _damage;
        private Vector2 _knockback;
        private bool _movingRight;
        private int _ownerIndex;
        private float _speed = 200f;
        private float _lifetime = 0.8f;
        private Hitbox _hitbox;

        public void Initialize(float damage, Vector2 knockback, bool facingRight, int ownerIndex) {
            _damage = damage;
            _knockback = knockback;
            _movingRight = facingRight;
            _ownerIndex = ownerIndex;
            _lifetime = 0.8f;

            _hitbox = GetNodeOrNull<Hitbox>("Hitbox");
            if (_hitbox != null) {
                _hitbox.Damage = _damage;
                _hitbox.KnockbackForce = _knockback;
                _hitbox.OwnerPlayerIndex = _ownerIndex;
                _hitbox.Activate();
            }
        }

        public void OnSpawn() { }
        public void OnDespawn() => _hitbox?.Deactivate();

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

    public partial class MozartSonataDrift : BaseSpecial {
        private const float StartupDuration = 0.15f;
        private const float ActiveDuration = 0.1f;
        private const float RecoveryDuration = 0.15f;
        private const float CooldownTime = 5.0f;
        private const float PlatformDuration = 3.0f;

        protected override void OnStartup() {
            PhaseTimer = StartupDuration;
        }

        protected override void OnActive() {
            PhaseTimer = ActiveDuration;
            DeployPlatform();
        }

        protected override void OnRecovery() {
            PhaseTimer = RecoveryDuration;
            Owner.MovementAbilityCooldownTimer = CooldownTime;
            FTT.Core.EventBus.Instance?.RaiseMovementAbilityUsed(new FTT.Core.MovementAbilityPayload {
                PlayerIndex = Owner.PlayerIndex,
                AbilityName = "Sonata Drift",
                StartPosition = Owner.GlobalPosition,
                EndPosition = Owner.GlobalPosition
            });
            FTT.Core.EventBus.Instance?.RaiseCooldownStarted(new FTT.Core.CooldownPayload {
                PlayerIndex = Owner.PlayerIndex,
                Slot = FTT.Core.AbilitySlot.MovementAbility,
                Duration = CooldownTime
            });
        }

        private void DeployPlatform() {
            if (Data?.ProjectileScene == null || Owner == null) return;

            var platform = FTT.Core.PoolManager.Instance?.Spawn(
                Data.ProjectileScene,
                Owner.GlobalPosition + new Vector2(0, 40f)
            );

            if (platform is MozartSonataPlatform node) {
                node.Initialize(PlatformDuration);
            }
        }
    }

    public partial class MozartSonataPlatform : FTT.Core.PooledNode, FTT.Core.IPoolable {
        private float _lifetime;

        public void Initialize(float duration) {
            _lifetime = duration;
        }

        public void OnSpawn() { }
        public void OnDespawn() { }

        public override void _PhysicsProcess(double delta) {
            _lifetime -= (float)delta;
            if (_lifetime <= 0) ReturnToPool();
        }
    }

    public partial class MozartSymphonyOfSorrow : BaseSpecial {
        private const float CinematicDuration = 3.0f;
        private const int MeteorCount = 10;
        private const float MeteorDamage = 8f;

        private float _meteorTimer;
        private int _meteorsSpawned;
        private UltimateMeter _meter;

        public override void _Ready() {
            base._Ready();
            _meter = Owner?.GetNodeOrNull<UltimateMeter>("UltimateMeter");
        }

        protected override bool Validate() {
            return base.Validate() && (_meter?.IsFull ?? false);
        }

        protected override void OnStartup() {
            PhaseTimer = 0.5f;
            _meteorsSpawned = 0;
            _meteorTimer = 0;
            Owner.Velocity = new Vector2(Owner.Velocity.X, -120f);
            _meter?.Consume();
        }

        protected override void OnActive() {
            PhaseTimer = CinematicDuration;
        }

        protected override void OnRecovery() {
            PhaseTimer = 0.4f;
        }

        public override void _PhysicsProcess(double delta) {
            if (CurrentPhase == AbilityPhase.Active) {
                float dt = (float)delta;
                Owner.Velocity = new Vector2(Owner.Velocity.X, Mathf.Min(Owner.Velocity.Y, -60f));

                _meteorTimer += dt;
                float spawnInterval = CinematicDuration / MeteorCount;
                while (_meteorTimer >= spawnInterval && _meteorsSpawned < MeteorCount) {
                    _meteorTimer -= spawnInterval;
                    _meteorsSpawned++;
                    SpawnMeteor();
                }
            }
            base._PhysicsProcess(delta);
        }

        private void SpawnMeteor() {
            var meteorScene = GetNodeOrNull<PackedScene>("MeteorScene");
            if (meteorScene == null) return;

            float offsetX = (float)GD.RandRange(-200f, 200f);
            var spawnPos = Owner.GlobalPosition + new Vector2(offsetX, -250f);
            var meteor = FTT.Core.PoolManager.Instance?.Spawn(meteorScene, spawnPos);

            if (meteor is MozartPianoKeyMeteor node) {
                node.Initialize(MeteorDamage, Owner.PlayerIndex);
            }
        }
    }

    public partial class MozartPianoKeyMeteor : FTT.Core.PooledNode, FTT.Core.IPoolable {
        private float _fallSpeed = 320f;
        private float _damage;
        private int _ownerIndex;
        private Hitbox _hitbox;

        public void Initialize(float damage, int ownerIndex) {
            _damage = damage;
            _ownerIndex = ownerIndex;

            _hitbox = GetNodeOrNull<Hitbox>("Hitbox");
            if (_hitbox != null) {
                _hitbox.Damage = _damage;
                _hitbox.KnockbackForce = new Vector2(0, 4f);
                _hitbox.OwnerPlayerIndex = _ownerIndex;
                _hitbox.Activate();
            }
        }

        public void OnSpawn() { }
        public void OnDespawn() => _hitbox?.Deactivate();

        public override void _PhysicsProcess(double delta) {
            GlobalPosition += Vector2.Down * _fallSpeed * (float)delta;
            if (GlobalPosition.Y > 600f) ReturnToPool();
        }
    }
}
