using Godot;
using FTT.Combat;
using FTT.Characters;

namespace FTT.Characters.Abilities {

    public partial class ShakespeareYoricksLament : BaseSpecial {
        private const float StartupDuration = 0.25f;
        private const float ActiveDuration = 0.1f;
        private const float RecoveryDuration = 0.3f;

        protected override void OnStartup() {
            PhaseTimer = StartupDuration;
        }

        protected override void OnActive() {
            PhaseTimer = ActiveDuration;
            LaunchSkull();
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

        private void LaunchSkull() {
            if (Data?.ProjectileScene == null && Owner != null) {
                SpawnPlaceholderProjectile(
                    Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? 40f : -40f, -10f),
                    Data?.ProjectileSpeed ?? 200f, Owner.IsFacingRight, new Color(0.8f, 0.7f, 0.5f),
                    new Vector2(18, 18));
                return;
            }
            if (Owner == null) return;

            var skull = FTT.Core.PoolManager.Instance?.Spawn(
                Data.ProjectileScene,
                Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? 40f : -40f, -10f)
            );

            if (skull is ShakespeareYorickSkull projectile) {
                projectile.Initialize(
                    Data.BaseDamage,
                    Data.KnockbackForce,
                    Data.StatusDuration > 0 ? Data.StatusDuration : 2.5f,
                    Owner.IsFacingRight,
                    Owner.PlayerIndex
                );
            }
        }
    }

    public partial class ShakespeareYorickSkull : FTT.Core.PooledNode, FTT.Core.IPoolable {
        private float _damage;
        private Vector2 _knockback;
        private float _statusDuration;
        private bool _movingRight;
        private int _ownerIndex;
        private float _speed = 150f;
        private float _lifetime = 4f;
        private Hitbox _hitbox;
        private bool _hasImpacted;

        public void Initialize(float damage, Vector2 knockback, float statusDuration,
            bool facingRight, int ownerIndex) {
            _damage = damage;
            _knockback = knockback;
            _statusDuration = statusDuration;
            _movingRight = facingRight;
            _ownerIndex = ownerIndex;
            _lifetime = 4f;
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

            var pos = GlobalPosition;
            pos.X += (_movingRight ? _speed : -_speed) * dt;
            GlobalPosition = pos;

            if (!_hasImpacted && IsOnFloor()) {
                _hasImpacted = true;
                EmitSonicWave();
            }
        }

        private void EmitSonicWave() {
            var area = GetNodeOrNull<Area2D>("ImpactArea");
            if (area == null) return;

            foreach (var body in area.GetOverlappingBodies()) {
                if (body is PlayerController pc && pc.PlayerIndex != _ownerIndex) {
                    pc.ApplyDamage((int)_damage);
                    pc.GetNodeOrNull<StatusController>("StatusController")
                        ?.ApplyStatus(FTT.Core.StatusType.TimeDilation, _statusDuration);
                }
            }
            ReturnToPool();
        }

        private bool IsOnFloor() {
            var space = GetWorld2D()?.DirectSpaceState;
            if (space == null) return false;
            var query = PhysicsRayQueryParameters2D.Create(
                GlobalPosition, GlobalPosition + Vector2.Down * 20f);
            return space.IntersectRay(query).Count > 0;
        }
    }

    public partial class ShakespeareTheTempest : BaseSpecial {
        private const float StartupDuration = 0.2f;
        private const float ActiveDuration = 1.2f;
        private const float RecoveryDuration = 0.35f;
        private const float StormRadius = 120f;
        private const float LiftForce = 280f;
        private const float PushForce = 180f;

        protected override void OnStartup() {
            PhaseTimer = StartupDuration;
        }

        protected override void OnActive() {
            PhaseTimer = ActiveDuration;
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

        public override void _PhysicsProcess(double delta) {
            if (CurrentPhase == AbilityPhase.Active) {
                float dt = (float)delta;
                Owner.Velocity = new Vector2(Owner.Velocity.X, -LiftForce * dt);

                foreach (var node in GetTree().GetNodesInGroup("players")) {
                    if (node is PlayerController pc && pc.PlayerIndex != Owner.PlayerIndex) {
                        if (Owner.GlobalPosition.DistanceTo(pc.GlobalPosition) <= StormRadius) {
                            var pushDir = (pc.GlobalPosition - Owner.GlobalPosition).Normalized();
                            pc.Velocity += pushDir * PushForce * dt;
                        }
                    }
                }
            }
            base._PhysicsProcess(delta);
        }
    }

    public partial class ShakespeareProsperosFlight : BaseSpecial {
        private const float StartupDuration = 0.1f;
        private const float ActiveDuration = 0.15f;
        private const float RecoveryDuration = 0.1f;
        private const float CooldownTime = 5.0f;
        private const float GustForce = 400f;
        private const float MaxGlideDuration = 3.0f;
        private const float GlideSpeed = 130f;

        private bool _isGliding;
        private float _glideTimer;

        protected override void OnStartup() {
            PhaseTimer = StartupDuration;
            _isGliding = false;
            float hDir = Owner.IsFacingRight ? 1f : -1f;
            Owner.Velocity = new Vector2(hDir * GustForce * 0.5f, -GustForce);
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
                AbilityName = "Prospero's Flight",
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
                float hInput = 0f;
                if (Input.IsActionPressed(FTT.Core.InputManager.Actions.MoveRight)) hInput += 1f;
                if (Input.IsActionPressed(FTT.Core.InputManager.Actions.MoveLeft)) hInput -= 1f;

                var vel = Owner.Velocity;
                vel.X = hInput * GlideSpeed;
                vel.Y = Mathf.Min(vel.Y, 35f);
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

    public partial class ShakespeareAllTheWorldsAStage : BaseSpecial {
        private const float CinematicDuration = 3.0f;
        private const int HitCount = 7;
        private const float DamagePerHit = 11f;

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
            PhaseTimer = 0.5f;
            _hitsDone = 0;
            _hitTimer = 0;
            Owner.Velocity = Vector2.Zero;
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
                _hitTimer += dt;
                float hitInterval = CinematicDuration / HitCount;
                while (_hitTimer >= hitInterval && _hitsDone < HitCount) {
                    _hitTimer -= hitInterval;
                    _hitsDone++;
                    DealPhantomStrike();
                }
            }
            base._PhysicsProcess(delta);
        }

        private void DealPhantomStrike() {
            var hitbox = GetNodeOrNull<Hitbox>("PhantomHitbox");
            if (hitbox == null) return;

            var strikeDir = Owner.IsFacingRight ? Vector2.Right : Vector2.Left;
            hitbox.Damage = DamagePerHit;
            hitbox.KnockbackForce = strikeDir * 5f + new Vector2(0, -2f);
            hitbox.OwnerPlayerIndex = Owner.PlayerIndex;
            hitbox.GlobalPosition = Owner.GlobalPosition + strikeDir * (30f + _hitsDone * 50f);
            hitbox.Activate();
            GetTree().CreateTimer(0.08f).Timeout += () => hitbox.Deactivate();
        }
    }
}
