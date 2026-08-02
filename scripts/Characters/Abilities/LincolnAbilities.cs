using Godot;
using FTT.Combat;
using FTT.Characters;

namespace FTT.Characters.Abilities {

    public partial class LincolnEmancipator : BaseSpecial {
        private const float StartupDuration = 0.4f;
        private const float ActiveDuration = 0.2f;
        private const float RecoveryDuration = 0.45f;
        private const int BlockChargeDepletion = 2;

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
                    Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? 50f : -50f, 10f),
                    Data?.ProjectileSpeed ?? 200f, Owner.IsFacingRight, new Color(0.2f, 0.2f, 0.5f),
                    new Vector2(35, 18));
                return;
            }
            if (Owner == null) return;

            var shockwave = FTT.Core.PoolManager.Instance?.Spawn(
                Data.ProjectileScene,
                Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? 50f : -50f, 10f)
            );

            if (shockwave is LincolnGroundShockwave zone) {
                zone.Initialize(
                    Data.BaseDamage,
                    Data.KnockbackForce,
                    BlockChargeDepletion,
                    Owner.IsFacingRight,
                    Owner.PlayerIndex
                );
            }
        }
    }

    public partial class LincolnGroundShockwave : FTT.Core.PooledNode, FTT.Core.IPoolable {
        private float _damage;
        private Vector2 _knockback;
        private int _blockDepletion;
        private bool _movingRight;
        private int _ownerIndex;
        private float _speed = 150f;
        private float _lifetime = 0.8f;
        private Hitbox _hitbox;

        public void Initialize(float damage, Vector2 knockback, int blockDepletion,
            bool facingRight, int ownerIndex) {
            _damage = damage;
            _knockback = knockback;
            _blockDepletion = blockDepletion;
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

    public partial class LincolnSplittingStrike : BaseSpecial {
        private const float StartupDuration = 0.35f;
        private const float ActiveDuration = 0.2f;
        private const float RecoveryDuration = 0.5f;

        protected override void OnStartup() {
            PhaseTimer = StartupDuration;
        }

        protected override void OnActive() {
            PhaseTimer = ActiveDuration;
            ActivateOverheadHitbox();
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

        private void ActivateOverheadHitbox() {
            var hitbox = GetNodeOrNull<Hitbox>("OverheadHitbox");
            if (hitbox == null || Owner == null) return;

            hitbox.Damage = Data?.BaseDamage ?? 18f;
            hitbox.KnockbackForce = Data?.KnockbackForce ?? new Vector2(2f, 8f);
            hitbox.OwnerPlayerIndex = Owner.PlayerIndex;

            var offset = Data?.HitboxOffset ?? new Vector2(20f, -30f);
            if (!Owner.IsFacingRight) offset.X = -offset.X;
            hitbox.GlobalPosition = Owner.GlobalPosition + offset;
            hitbox.Activate();

            GetTree().CreateTimer(ActiveDuration).Timeout += () => {
                ShatterShieldsInRange(hitbox);
                SpikeAirborneTargets(hitbox);
                hitbox.Deactivate();
            };
        }

        private void ShatterShieldsInRange(Hitbox hitbox) {
            foreach (var area in hitbox.GetOverlappingAreas()) {
                if (area is Hurtbox hurtbox && hurtbox.OwnerPlayerIndex != Owner.PlayerIndex) {
                    var target = hurtbox.GetParent<PlayerController>();
                    if (target?.CurrentState == CharacterState.Blocking) {
                        target.CurrentBlockCharges = 0;
                        FTT.Core.EventBus.Instance?.RaiseBlockBroken(hurtbox.OwnerPlayerIndex);
                    }
                }
            }
        }

        private void SpikeAirborneTargets(Hitbox hitbox) {
            foreach (var area in hitbox.GetOverlappingAreas()) {
                if (area is Hurtbox hurtbox && hurtbox.OwnerPlayerIndex != Owner.PlayerIndex) {
                    var target = hurtbox.GetParent<PlayerController>();
                    if (target != null && !target.IsOnFloor()) {
                        target.Velocity = new Vector2(target.Velocity.X, 400f);
                    }
                }
            }
        }
    }

    public partial class LincolnRailCharge : BaseSpecial {
        private const float StartupDuration = 0.15f;
        private const float ActiveDuration = 3.0f;
        private const float RecoveryDuration = 0.2f;
        private const float CooldownTime = 5.0f;
        private const float ChargeSpeed = 200f;

        private Vector2 _chargeDirection;
        private float _chargeTimer;

        protected override void OnStartup() {
            PhaseTimer = StartupDuration;
            if (Data?.GrantsHyperArmor == true) {
                // Hyper-armor visual during charge
            }
            _chargeDirection = Owner.IsFacingRight ? Vector2.Right : Vector2.Left;
            _chargeTimer = ActiveDuration;
        }

        protected override void OnActive() {
            PhaseTimer = ActiveDuration;
        }

        protected override void OnRecovery() {
            PhaseTimer = RecoveryDuration;
            Owner.MovementAbilityCooldownTimer = CooldownTime;
            FTT.Core.EventBus.Instance?.RaiseMovementAbilityUsed(new FTT.Core.MovementAbilityPayload {
                PlayerIndex = Owner.PlayerIndex,
                AbilityName = "Rail Charge",
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
            if (CurrentPhase == AbilityPhase.Active) {
                float dt = (float)delta;
                _chargeTimer -= dt;
                Owner.Velocity = _chargeDirection * ChargeSpeed;
                if (_chargeTimer <= 0) AdvanceToRecovery();
                return;
            }
            base._PhysicsProcess(delta);
        }
    }

    public partial class LincolnUnionIndestructible : BaseSpecial {
        private const float CinematicDuration = 3.0f;
        private const int HitCount = 5;
        private const float BarrierDuration = 1.5f;
        private const float SmashDamage = 25f;

        private float _hitTimer;
        private int _hitsDone;
        private bool _barriersRaised;
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
            _barriersRaised = false;
            Owner.Velocity = Vector2.Zero;
            _meter?.Consume();
        }

        protected override void OnActive() {
            PhaseTimer = CinematicDuration;
            if (!_barriersRaised) {
                RaiseBarriers();
                _barriersRaised = true;
            }
        }

        protected override void OnRecovery() {
            PhaseTimer = 0.6f;
            DeliverGroundSmash();
        }

        public override void _PhysicsProcess(double delta) {
            if (CurrentPhase == AbilityPhase.Active) {
                float dt = (float)delta;
                _hitTimer += dt;
                float hitInterval = CinematicDuration / HitCount;
                while (_hitTimer >= hitInterval && _hitsDone < HitCount) {
                    _hitTimer -= hitInterval;
                    _hitsDone++;
                    DealBarrierTrapHit();
                }
            }
            base._PhysicsProcess(delta);
        }

        private void RaiseBarriers() {
            if (Data?.ProjectileScene == null) return;
            var barrierPos = Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? 80f : -80f, 0f);
            FTT.Core.PoolManager.Instance?.Spawn(Data.ProjectileScene, barrierPos);
        }

        private void DealBarrierTrapHit() {
            var hitbox = GetNodeOrNull<Hitbox>("TrapHitbox");
            if (hitbox == null) return;

            hitbox.Damage = 8f;
            hitbox.KnockbackForce = new Vector2(Owner.IsFacingRight ? 4f : -4f, -1f);
            hitbox.OwnerPlayerIndex = Owner.PlayerIndex;
            hitbox.Activate();
            GetTree().CreateTimer(0.08f).Timeout += () => hitbox.Deactivate();
        }

        private void DeliverGroundSmash() {
            var hitbox = GetNodeOrNull<Hitbox>("SmashHitbox");
            if (hitbox == null) return;

            hitbox.Damage = SmashDamage;
            hitbox.KnockbackForce = new Vector2(Owner.IsFacingRight ? 10f : -10f, -6f);
            hitbox.OwnerPlayerIndex = Owner.PlayerIndex;
            hitbox.Activate();
            GetTree().CreateTimer(0.15f).Timeout += () => hitbox.Deactivate();
        }
    }
}
