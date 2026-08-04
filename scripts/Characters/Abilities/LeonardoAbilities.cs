using Godot;
using FTT.Combat;
using FTT.Characters;

namespace FTT.Characters.Abilities {

    public partial class LeonardoGoldenRatio : BaseSpecial {
        private const float StartupDuration = 0.3f;
        private const float ActiveDuration = 0.1f;
        private const float RecoveryDuration = 0.25f;

        protected override void OnStartup() {
            PhaseTimer = StartupDuration;
        }

        protected override void OnActive() {
            PhaseTimer = ActiveDuration;
            SpawnSpiral();
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

        private void SpawnSpiral() {
            if (Data?.ProjectileScene == null && Owner != null) {
                SpawnPlaceholderZone(
                    Owner.GlobalPosition,
                    Data.BaseDamage,
                    0.75f,
                    0.25f,
                    new Color(0.8f, 0.7f, 0.2f),
                    60f);
                return;
            }
            if (Owner == null) return;

            var spiral = FTT.Core.PoolManager.Instance?.Spawn(
                Data.ProjectileScene,
                Owner.GlobalPosition
            );

            if (spiral is LeonardoFibonacciSpiral zone) {
                zone.Initialize(
                    Data.BaseDamage,
                    Data.KnockbackForce,
                    Data.IsMultiHit ? Data.HitCount : 3,
                    Owner.PlayerIndex
                );
            }
        }
    }

    public partial class LeonardoFibonacciSpiral : FTT.Core.PooledNode, FTT.Core.IPoolable {
        private float _damage;
        private Vector2 _knockback;
        private int _maxHits;
        private int _ownerIndex;
        private float _expandTimer;
        private float _tickInterval = 0.25f;
        private float _tickTimer;
        private int _hitsApplied;
        private float _radius = 20f;
        private const float MaxRadius = 120f;
        private Hitbox _hitbox;

        public void Initialize(float damage, Vector2 knockback, int hitCount, int ownerIndex) {
            _damage = damage;
            _knockback = knockback;
            _maxHits = hitCount;
            _ownerIndex = ownerIndex;
            _expandTimer = 0.75f;
            _tickTimer = 0f;
            _hitsApplied = 0;
            _radius = 20f;

            _hitbox = GetNodeOrNull<Hitbox>("Hitbox");
            if (_hitbox != null) {
                _hitbox.Damage = _damage;
                _hitbox.KnockbackForce = _knockback;
                _hitbox.OwnerPlayerIndex = _ownerIndex;
            }
        }

        public void OnSpawn() { }
        public void OnDespawn() => _hitbox?.Deactivate();

        public override void _PhysicsProcess(double delta) {
            float dt = (float)delta;
            _expandTimer -= dt;
            if (_expandTimer <= 0) {
                ReturnToPool();
                return;
            }

            _radius = Mathf.Min(_radius + 80f * dt, MaxRadius);
            Scale = Vector2.One * (_radius / 40f);

            _tickTimer += dt;
            if (_tickTimer >= _tickInterval && _hitsApplied < _maxHits) {
                _tickTimer -= _tickInterval;
                _hitsApplied++;
                _hitbox?.Activate();
                GetTree().CreateTimer(0.05f).Timeout += () => _hitbox?.Deactivate();
            }
        }
    }

    public partial class LeonardoClockworkTurret : BaseSpecial {
        private const float StartupDuration = 0.35f;
        private const float ActiveDuration = 0.1f;
        private const float RecoveryDuration = 0.3f;
        private const int MaxActiveTurrets = 1;

        protected override void OnStartup() {
            PhaseTimer = StartupDuration;
        }

        protected override void OnActive() {
            PhaseTimer = ActiveDuration;
            DeployTurret();
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

        private void DeployTurret() {
            if (Owner == null) return;
            if (Data?.ProjectileScene == null) return;

            while (Owner.ActivePersistentObjects.Count >= MaxActiveTurrets) {
                var oldest = Owner.ActivePersistentObjects[0];
                Owner.ActivePersistentObjects.RemoveAt(0);
                if (oldest is FTT.Core.PooledNode pooled) pooled.ReturnToPool();
                else oldest.QueueFree();
            }

            var turret = FTT.Core.PoolManager.Instance?.Spawn(
                Data.ProjectileScene,
                Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? 60f : -60f, 0f)
            );

            if (turret is LeonardoClockworkTurretNode node) {
                node.Initialize(Owner.PlayerIndex, Owner.IsFacingRight);
                Owner.ActivePersistentObjects.Add(node);
            }
        }
    }

    public partial class LeonardoClockworkTurretNode : FTT.Core.PooledNode, FTT.Core.IPoolable {
        private const float Lifespan = 15f;
        private const int MaxBolts = 3;
        private const float BoltDamage = 5f;
        private const float FireInterval = 2f;
        private const float TargetRange = 300f;

        private int _ownerIndex;
        private bool _facingRight;
        private float _lifetime;
        private float _fireTimer;
        private int _boltsFired;

        public void Initialize(int ownerIndex, bool facingRight) {
            _ownerIndex = ownerIndex;
            _facingRight = facingRight;
            _lifetime = Lifespan;
            _fireTimer = FireInterval;
            _boltsFired = 0;
        }

        public void OnSpawn() { }
        public void OnDespawn() { }

        public override void _PhysicsProcess(double delta) {
            float dt = (float)delta;
            _lifetime -= dt;
            if (_lifetime <= 0 || _boltsFired >= MaxBolts) {
                ReturnToPool();
                return;
            }

            _fireTimer -= dt;
            if (_fireTimer <= 0) {
                _fireTimer = FireInterval;
                FireBolt();
            }
        }

        private void FireBolt() {
            _boltsFired++;

            var target = FindNearestEnemy();
            var direction = target != null
                ? (target.GlobalPosition - GlobalPosition).Normalized()
                : (_facingRight ? Vector2.Right : Vector2.Left);

            var bolt = FTT.Core.PoolManager.Instance?.Spawn(
                GetNode<PackedScene>("BoltScene"),
                GlobalPosition
            );
            if (bolt is LeonardoTurretBolt projectile) {
                projectile.Initialize(BoltDamage, direction, _ownerIndex);
            }
        }

        private Node2D FindNearestEnemy() {
            Node2D nearest = null;
            float nearestDist = TargetRange;
            foreach (var node in GetTree().GetNodesInGroup("players")) {
                if (node is PlayerController pc && pc.PlayerIndex != _ownerIndex) {
                    float dist = GlobalPosition.DistanceTo(pc.GlobalPosition);
                    if (dist < nearestDist) {
                        nearestDist = dist;
                        nearest = pc;
                    }
                }
            }
            return nearest;
        }
    }

    public partial class LeonardoTurretBolt : FTT.Core.PooledNode, FTT.Core.IPoolable {
        private float _speed = 240f;
        private float _damage;
        private Vector2 _direction;
        private int _ownerIndex;
        private float _lifetime = 3f;
        private Hitbox _hitbox;

        public void Initialize(float damage, Vector2 direction, int ownerIndex) {
            _damage = damage;
            _direction = direction.Normalized();
            _ownerIndex = ownerIndex;
            _lifetime = 3f;

            _hitbox = GetNodeOrNull<Hitbox>("Hitbox");
            if (_hitbox != null) {
                _hitbox.Damage = _damage;
                _hitbox.KnockbackForce = _direction * 3f;
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
            GlobalPosition += _direction * _speed * dt;
        }
    }

    public partial class LeonardoOrnithopterFlight : BaseSpecial {
        private const float StartupDuration = 0.1f;
        private const float ActiveDuration = 0.15f;
        private const float RecoveryDuration = 0.1f;
        private const float CooldownTime = 5.0f;
        private const float BoostForce = 380f;
        private const float MaxGlideDuration = 3.0f;
        private const float GlideSpeed = 120f;

        private bool _isGliding;
        private float _glideTimer;

        protected override void OnStartup() {
            PhaseTimer = StartupDuration;
            _isGliding = false;
            Owner.Velocity = new Vector2(Owner.Velocity.X, -BoostForce);
        }

        protected override void OnActive() {
            PhaseTimer = ActiveDuration;
        }

        protected override void OnRecovery() {
            PhaseTimer = RecoveryDuration;
            _isGliding = true;
            _glideTimer = MaxGlideDuration;
            Owner.MovementAbilityCooldownTimer = CooldownTime;
            FTT.Core.EventBus.Instance?.RaiseMovementAbilityUsed(new FTT.Core.MovementAbilityPayload {
                PlayerIndex = Owner.PlayerIndex,
                AbilityName = "Ornithopter Flight",
                StartPosition = Owner.GlobalPosition,
                EndPosition = Owner.GlobalPosition
            });
            FTT.Core.EventBus.Instance?.RaiseCooldownStarted(new FTT.Core.CooldownPayload {
                PlayerIndex = Owner.PlayerIndex,
                Slot = FTT.Core.AbilitySlot.MovementAbility,
                Duration = CooldownTime
            });
        }

        public override void _PhysicsProcess(double delta) {
            float dt = (float)delta;

            if (_isGliding) {
                _glideTimer -= dt;
                float hInput = Owner.CurrentInputFrame.Horizontal;

                var vel = Owner.Velocity;
                vel.X = hInput * GlideSpeed;
                vel.Y = Mathf.Min(vel.Y, 40f);
                Owner.Velocity = vel;

                if (_glideTimer <= 0 || Owner.IsOnFloor()) {
                    _isGliding = false;
                    if (CurrentPhase != AbilityPhase.Inactive) AdvanceToCleanup();
                }
                return;
            }

            base._PhysicsProcess(delta);
        }
    }

    public partial class LeonardoVitruvianMatrix : BaseSpecial {
        private const float CinematicDuration = 3.0f;
        private const int HitCount = 8;
        private const float DamagePerHit = 10f;

        private float _hitTimer;
        private int _hitsDone;
        private UltimateMeter _meter;

        public override void _Ready() {
            base._Ready();
            _meter = Owner?.GetNodeOrNull<UltimateMeter>("UltimateMeter");
        }

        protected override bool Validate() {
            return base.Validate() && (_meter?.IsFull ?? false);
        }

        protected override void OnStartup() {
            PhaseTimer = 0.6f;
            _hitsDone = 0;
            _hitTimer = 0;
            Owner.Velocity = Vector2.Zero;
            _meter?.Consume();
        }

        protected override void OnActive() {
            PhaseTimer = CinematicDuration;
        }

        protected override void OnRecovery() {
            PhaseTimer = 0.5f;
        }

        public override void _PhysicsProcess(double delta) {
            if (CurrentPhase == AbilityPhase.Active) {
                float dt = (float)delta;
                _hitTimer += dt;
                float hitInterval = CinematicDuration / HitCount;
                while (_hitTimer >= hitInterval && _hitsDone < HitCount) {
                    _hitTimer -= hitInterval;
                    _hitsDone++;
                    DealMatrixHit();
                }
            }
            base._PhysicsProcess(delta);
        }

        private void DealMatrixHit() {
            var hitbox = GetNodeOrNull<Hitbox>("MatrixHitbox");
            if (hitbox == null) return;

            hitbox.Damage = DamagePerHit;
            hitbox.KnockbackForce = new Vector2(0, -3f);
            hitbox.OwnerPlayerIndex = Owner.PlayerIndex;
            hitbox.Activate();
            GetTree().CreateTimer(0.1f).Timeout += () => hitbox.Deactivate();
        }
    }
}
