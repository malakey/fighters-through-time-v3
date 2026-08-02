using Godot;
using FTT.Combat;
using FTT.Characters;

namespace FTT.Characters.Abilities {

    public partial class JoanRighteousSmite : BaseSpecial {
        private const float StartupDuration = 0.25f;
        private const float ActiveDuration = 0.15f;
        private const float RecoveryDuration = 0.3f;

        protected override void OnStartup() {
            PhaseTimer = StartupDuration;
        }

        protected override void OnActive() {
            PhaseTimer = ActiveDuration;
            SpawnShockwave();
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

        private void SpawnShockwave() {
            if (Data?.ProjectileScene == null && Owner != null) {
                SpawnPlaceholderProjectile(
                    Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? 40f : -40f, 10f),
                    Data?.ProjectileSpeed ?? 250f, Owner.IsFacingRight, new Color(0.9f, 0.8f, 0.2f),
                    new Vector2(30, 16));
                return;
            }
            if (Owner == null) return;

            var shockwave = FTT.Core.PoolManager.Instance?.Spawn(
                Data.ProjectileScene,
                Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? 40f : -40f, 10f)
            );

            if (shockwave is JoanGroundShockwave zone) {
                zone.Initialize(
                    Data.BaseDamage,
                    Data.KnockbackForce,
                    Data.AppliedStatus,
                    Data.StatusDuration,
                    Owner.IsFacingRight,
                    Owner.PlayerIndex
                );
            }
        }
    }

    public partial class JoanGroundShockwave : FTT.Core.PooledNode, FTT.Core.IPoolable {
        private float _damage;
        private Vector2 _knockback;
        private FTT.Core.StatusType _status;
        private float _statusDuration;
        private bool _movingRight;
        private int _ownerIndex;
        private float _speed = 180f;
        private float _lifetime = 0.6f;
        private Hitbox _hitbox;

        public void Initialize(float damage, Vector2 knockback, FTT.Core.StatusType status,
            float statusDuration, bool facingRight, int ownerIndex) {
            _damage = damage;
            _knockback = knockback;
            _status = status;
            _statusDuration = statusDuration;
            _movingRight = facingRight;
            _ownerIndex = ownerIndex;
            _lifetime = 0.6f;

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

    public partial class JoanDivinePiercing : BaseSpecial {
        private const float StartupDuration = 0.2f;
        private const float ActiveDuration = 0.25f;
        private const float RecoveryDuration = 0.35f;
        private const int BlockChargeDepletion = 2;

        protected override void OnStartup() {
            PhaseTimer = StartupDuration;
        }

        protected override void OnActive() {
            PhaseTimer = ActiveDuration;
            ActivateThrustHitbox();
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

        private void ActivateThrustHitbox() {
            var hitbox = GetNodeOrNull<Hitbox>("ThrustHitbox");
            if (hitbox == null || Owner == null) return;

            hitbox.Damage = Data?.BaseDamage ?? 12f;
            hitbox.KnockbackForce = Data?.KnockbackForce ?? new Vector2(4f, -1f);
            hitbox.OwnerPlayerIndex = Owner.PlayerIndex;
            hitbox.Activate();

            var offset = Data?.HitboxOffset ?? new Vector2(30f, 0f);
            if (!Owner.IsFacingRight) offset.X = -offset.X;
            hitbox.GlobalPosition = Owner.GlobalPosition + offset;

            GetTree().CreateTimer(ActiveDuration).Timeout += () => {
                hitbox.Deactivate();
                DepleteBlockChargesInRange(hitbox);
            };
        }

        private void DepleteBlockChargesInRange(Hitbox hitbox) {
            var area = hitbox.GetOverlappingAreas();
            foreach (var a in area) {
                if (a is Hurtbox hurtbox && hurtbox.OwnerPlayerIndex != Owner.PlayerIndex) {
                    var target = hurtbox.GetParent<PlayerController>();
                    if (target?.CurrentState == CharacterState.Blocking) {
                        target.CurrentBlockCharges = Mathf.Max(0, target.CurrentBlockCharges - BlockChargeDepletion);
                        if (target.CurrentBlockCharges <= 0) {
                            FTT.Core.EventBus.Instance?.RaiseBlockBroken(hurtbox.OwnerPlayerIndex);
                        }
                    }
                }
            }
        }
    }

    public partial class JoanAscendantWings : BaseSpecial {
        private const float StartupDuration = 0.15f;
        private const float ActiveDuration = 0.2f;
        private const float RecoveryDuration = 0.1f;
        private const float CooldownTime = 5.0f;
        private const float LeapForce = 420f;
        private const float GlideGravityScale = 0.35f;
        private const float MaxGlideDuration = 3.0f;

        private bool _isGliding;
        private float _glideTimer;

        protected override void OnStartup() {
            PhaseTimer = StartupDuration;
            _isGliding = false;
            _glideTimer = 0f;
            Owner.Velocity = new Vector2(Owner.Velocity.X, -LeapForce);
        }

        protected override void OnActive() {
            PhaseTimer = ActiveDuration;
        }

        protected override void OnRecovery() {
            PhaseTimer = RecoveryDuration;
            Owner.MovementAbilityCooldownTimer = CooldownTime;
            FTT.Core.EventBus.Instance?.RaiseMovementAbilityUsed(new FTT.Core.MovementAbilityPayload {
                PlayerIndex = Owner.PlayerIndex,
                AbilityName = "Ascendant Wings",
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

            if (CurrentPhase == AbilityPhase.Recovery || CurrentPhase == AbilityPhase.Cleanup) {
                if (!_isGliding && !Owner.IsOnFloor() &&
                    Input.IsActionPressed(FTT.Core.InputManager.Actions.Jump)) {
                    _isGliding = true;
                    _glideTimer = MaxGlideDuration;
                }

                if (_isGliding) {
                    _glideTimer -= dt;
                    var vel = Owner.Velocity;
                    vel.Y = Mathf.Min(vel.Y, 30f * 60f * GlideGravityScale * dt);
                    Owner.Velocity = vel;

                    if (_glideTimer <= 0 || Owner.IsOnFloor() ||
                        !Input.IsActionPressed(FTT.Core.InputManager.Actions.Jump)) {
                        _isGliding = false;
                        AdvanceToCleanup();
                    }
                    return;
                }
            }

            base._PhysicsProcess(delta);
        }
    }

    public partial class JoanGrandCrusade : BaseSpecial {
        private const float CinematicDuration = 2.5f;
        private const int HitCount = 6;
        private const float DamagePerHit = 12f;

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
                    DealCavalryHit();
                }
            }
            base._PhysicsProcess(delta);
        }

        private void DealCavalryHit() {
            var chargeDir = Owner.IsFacingRight ? Vector2.Right : Vector2.Left;
            var hitbox = GetNodeOrNull<Hitbox>("CavalryHitbox");
            if (hitbox == null) return;

            hitbox.Damage = DamagePerHit;
            hitbox.KnockbackForce = chargeDir * 8f + new Vector2(0, -2f);
            hitbox.OwnerPlayerIndex = Owner.PlayerIndex;
            hitbox.GlobalPosition = Owner.GlobalPosition + chargeDir * (40f + _hitsDone * 60f);
            hitbox.Activate();
            GetTree().CreateTimer(0.08f).Timeout += () => hitbox.Deactivate();
        }
    }
}
