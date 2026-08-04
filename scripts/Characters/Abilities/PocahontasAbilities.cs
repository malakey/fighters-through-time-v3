using Godot;
using FTT.Combat;
using FTT.Characters;

namespace FTT.Characters.Abilities {

    public partial class PocahontasSpiritStrike : BaseSpecial {
        private const float StartupDuration = 0.2f;
        private const float ActiveDuration = 0.35f;
        private const float RecoveryDuration = 0.3f;
        private const float SwoopDamage = 14f;
        private const float SwoopSpeed = 420f;

        private Vector2 _swoopDirection;

        protected override void OnStartup() {
            PhaseTimer = StartupDuration;
            float hDir = Owner.IsFacingRight ? 1f : -1f;
            _swoopDirection = new Vector2(hDir, -0.6f).Normalized();
        }

        protected override void OnActive() {
            PhaseTimer = ActiveDuration;
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

        public override void _PhysicsProcess(double delta) {
            if (CurrentPhase == AbilityPhase.Active) {
                Owner.Velocity = _swoopDirection * SwoopSpeed;

                var hitbox = GetNodeOrNull<Hitbox>("EagleHitbox");
                if (hitbox != null) {
                    hitbox.Damage = SwoopDamage;
                    hitbox.KnockbackForce = _swoopDirection * 4f;
                    hitbox.OwnerPlayerIndex = Owner.PlayerIndex;
                    hitbox.GlobalPosition = Owner.GlobalPosition;
                    hitbox.Activate();
                }
            } else {
                GetNodeOrNull<Hitbox>("EagleHitbox")?.Deactivate();
            }
            base._PhysicsProcess(delta);
        }
    }

    public partial class PocahontasVineSnare : BaseSpecial {
        private const float StartupDuration = 0.25f;
        private const float ActiveDuration = 0.1f;
        private const float RecoveryDuration = 0.3f;
        private const int MaxActiveSnares = 2;
        private const float RootDuration = 1.5f;

        protected override void OnStartup() {
            PhaseTimer = StartupDuration;
        }

        protected override void OnActive() {
            PhaseTimer = ActiveDuration;
            DeploySeedPod();
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

        private void DeploySeedPod() {
            if (Owner == null) return;
            if (Data?.ProjectileScene == null) {
                SpawnPlaceholderZone(
                    Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? 60f : -60f, 0f),
                    Data.BaseDamage,
                    10f,
                    2f,
                    new Color(0.3f, 0.6f, 0.2f),
                    40f);
                return;
            }

            while (Owner.ActivePersistentObjects.Count >= MaxActiveSnares) {
                var oldest = Owner.ActivePersistentObjects[0];
                Owner.ActivePersistentObjects.RemoveAt(0);
                if (oldest is FTT.Core.PooledNode pooled) pooled.ReturnToPool();
                else oldest.QueueFree();
            }

            var pod = FTT.Core.PoolManager.Instance?.Spawn(
                Data.ProjectileScene,
                Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? 60f : -60f, 0f)
            );

            if (pod is PocahontasVineSnareZone zone) {
                zone.Initialize(
                    Data.BaseDamage,
                    RootDuration,
                    Data.StatusDuration > 0 ? Data.StatusDuration : 10f,
                    Owner.PlayerIndex
                );
                Owner.ActivePersistentObjects.Add(zone);
            }
        }
    }

    public partial class PocahontasVineSnareZone : FTT.Core.PooledNode, FTT.Core.IPoolable {
        private float _damage;
        private float _rootDuration;
        private int _ownerIndex;
        private float _lifetime;
        private Area2D _triggerArea;

        public void Initialize(float damage, float rootDuration, float lifespan, int ownerIndex) {
            _damage = damage;
            _rootDuration = rootDuration;
            _ownerIndex = ownerIndex;
            _lifetime = lifespan;
        }

        public void OnSpawn() {
            _triggerArea = GetNodeOrNull<Area2D>("Area2D");
            if (_triggerArea != null) _triggerArea.BodyEntered += OnBodyEntered;
        }

        public void OnDespawn() {
            if (_triggerArea != null) _triggerArea.BodyEntered -= OnBodyEntered;
        }

        private void OnBodyEntered(Node2D body) {
            if (body is PlayerController pc && pc.PlayerIndex != _ownerIndex) {
                pc.ApplyDamage((int)_damage);
                pc.GetNodeOrNull<StatusController>("StatusController")
                    ?.ApplyStatus(FTT.Core.StatusType.Root, _rootDuration);
            }
        }

        public override void _PhysicsProcess(double delta) {
            _lifetime -= (float)delta;
            if (_lifetime <= 0) ReturnToPool();
        }
    }

    public partial class PocahontasBreezeGlide : BaseSpecial {
        private const float StartupDuration = 0.1f;
        private const float ActiveDuration = 0.15f;
        private const float RecoveryDuration = 0.1f;
        private const float CooldownTime = 5.0f;
        private const float DashForce = 350f;
        private const float MaxGlideDuration = 3.0f;
        private const float GlideSpeed = 140f;

        private bool _isGliding;
        private float _glideTimer;

        protected override void OnStartup() {
            PhaseTimer = StartupDuration;
            _isGliding = false;
            float hDir = Owner.IsFacingRight ? 1f : -1f;
            Owner.Velocity = new Vector2(hDir * DashForce, Owner.Velocity.Y);
            Owner.RemainingJumps = Owner.Data?.MaxJumpCount ?? 2;
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
                AbilityName = "Breeze Glide",
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
                vel.Y = Mathf.Min(vel.Y, 30f);
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

    public partial class PocahontasTidewaterTempest : BaseSpecial {
        private const float CinematicDuration = 2.8f;
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
                    DealSpiritHit();
                }
            }
            base._PhysicsProcess(delta);
        }

        private void DealSpiritHit() {
            var hitbox = GetNodeOrNull<Hitbox>("StormHitbox");
            if (hitbox == null) return;

            hitbox.Damage = DamagePerHit;
            hitbox.KnockbackForce = new Vector2(0, -5f);
            hitbox.OwnerPlayerIndex = Owner.PlayerIndex;
            hitbox.GlobalPosition = Owner.GlobalPosition + new Vector2(
                (float)GD.RandRange(-80f, 80f),
                (float)GD.RandRange(-40f, 40f)
            );
            hitbox.Activate();
            GetTree().CreateTimer(0.1f).Timeout += () => hitbox.Deactivate();
        }
    }
}
