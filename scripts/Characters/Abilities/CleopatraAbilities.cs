using Godot;
using FTT.Combat;
using FTT.Characters;

namespace FTT.Characters.Abilities {

    public partial class CleopatraSerpentNest : BaseSpecial {
        private const float StartupDuration = 0.3f;
        private const float ActiveDuration = 0.1f;
        private const float RecoveryDuration = 0.25f;
        private const int MaxActiveNests = 1;

        protected override void OnStartup() {
            PhaseTimer = StartupDuration;
        }

        protected override void OnActive() {
            PhaseTimer = ActiveDuration;
            DeployNest();
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

        private void DeployNest() {
            if (Owner == null) return;
            if (Data?.ProjectileScene == null) {
                SpawnPlaceholderZone(
                    Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? 80f : -80f, 0f),
                    Data.BaseDamage,
                    8f,
                    1f,
                    new Color(0.4f, 0.7f, 0.2f),
                    50f);
                return;
            }

            while (Owner.ActivePersistentObjects.Count >= MaxActiveNests) {
                var oldest = Owner.ActivePersistentObjects[0];
                Owner.ActivePersistentObjects.RemoveAt(0);
                if (oldest is FTT.Core.PooledNode pooled) pooled.ReturnToPool();
                else oldest.QueueFree();
            }

            var nest = FTT.Core.PoolManager.Instance?.Spawn(
                Data.ProjectileScene,
                Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? 80f : -80f, 0f)
            );

            if (nest is CleopatraSerpentNestZone zone) {
                zone.Initialize(
                    Data.BaseDamage,
                    Data.StatusDuration,
                    Data.AppliedStatus,
                    Owner.PlayerIndex
                );
                Owner.ActivePersistentObjects.Add(zone);
            }
        }
    }

    public partial class CleopatraSerpentNestZone : FTT.Core.PooledNode, FTT.Core.IPoolable {
        private const float Lifespan = 12f;
        private const float RootDuration = 1f;
        private const float VenomDuration = 4f;

        private float _biteDamage;
        private float _statusDuration;
        private FTT.Core.StatusType _venomStatus;
        private int _ownerIndex;
        private float _lifetime;
        private Area2D _triggerArea;

        public void Initialize(float biteDamage, float statusDuration,
            FTT.Core.StatusType venomStatus, int ownerIndex) {
            _biteDamage = biteDamage;
            _statusDuration = statusDuration;
            _venomStatus = venomStatus;
            _ownerIndex = ownerIndex;
            _lifetime = Lifespan;
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
                pc.ApplyDamage((int)_biteDamage);
                pc.GetNodeOrNull<StatusController>("StatusController")
                    ?.ApplyStatus(FTT.Core.StatusType.Stunned, RootDuration);
                pc.GetNodeOrNull<StatusController>("StatusController")
                    ?.ApplyStatus(_venomStatus, _statusDuration > 0 ? _statusDuration : VenomDuration);
                FTT.Core.EventBus.Instance?.RaiseStatusEffectApplied(new FTT.Core.StatusEffectPayload {
                    TargetIndex = pc.PlayerIndex,
                    Type = _venomStatus,
                    Duration = VenomDuration
                });
            }
        }

        public override void _PhysicsProcess(double delta) {
            _lifetime -= (float)delta;
            if (_lifetime <= 0) ReturnToPool();
        }
    }

    public partial class CleopatraSandstormVortex : BaseSpecial {
        private const float StartupDuration = 0.25f;
        private const float ActiveDuration = 0.1f;
        private const float RecoveryDuration = 0.3f;

        protected override void OnStartup() {
            PhaseTimer = StartupDuration;
        }

        protected override void OnActive() {
            PhaseTimer = ActiveDuration;
            SpawnVortex();
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

        private void SpawnVortex() {
            if (Data?.ProjectileScene == null && Owner != null) {
                SpawnPlaceholderZone(
                    Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? 100f : -100f, 0f),
                    Data.BaseDamage,
                    2f,
                    0.4f,
                    new Color(0.8f, 0.7f, 0.3f),
                    70f);
                return;
            }
            if (Owner == null) return;

            var vortex = FTT.Core.PoolManager.Instance?.Spawn(
                Data.ProjectileScene,
                Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? 100f : -100f, 0f)
            );

            if (vortex is CleopatraSandstormVortexZone zone) {
                zone.Initialize(
                    Data.BaseDamage,
                    Data.StatusDuration,
                    Data.StatusIntensity,
                    Owner.PlayerIndex
                );
            }
        }
    }

    public partial class CleopatraSandstormVortexZone : FTT.Core.PooledNode, FTT.Core.IPoolable {
        private const float Duration = 2f;
        private const float TickInterval = 0.4f;
        private const float PullStrength = 80f;

        private float _damagePerTick;
        private float _statusDuration;
        private float _statusIntensity;
        private int _ownerIndex;
        private float _lifetime;
        private float _tickTimer;

        public void Initialize(float damagePerTick, float statusDuration,
            float statusIntensity, int ownerIndex) {
            _damagePerTick = damagePerTick;
            _statusDuration = statusDuration;
            _statusIntensity = statusIntensity;
            _ownerIndex = ownerIndex;
            _lifetime = Duration;
            _tickTimer = 0f;
        }

        public void OnSpawn() { }
        public void OnDespawn() { }

        public override void _PhysicsProcess(double delta) {
            float dt = (float)delta;
            _lifetime -= dt;
            if (_lifetime <= 0) {
                ReturnToPool();
                return;
            }

            PullAndDamageOverlapping();

            _tickTimer += dt;
            if (_tickTimer >= TickInterval) {
                _tickTimer -= TickInterval;
                ApplyTickDamage();
            }
        }

        private void PullAndDamageOverlapping() {
            var area = GetNodeOrNull<Area2D>("Area2D");
            if (area == null) return;

            foreach (var body in area.GetOverlappingBodies()) {
                if (body is PlayerController pc && pc.PlayerIndex != _ownerIndex) {
                    var pull = (GlobalPosition - pc.GlobalPosition).Normalized() * PullStrength * (float)GetPhysicsProcessDeltaTime();
                    pc.Velocity += pull;
                }
            }
        }

        private void ApplyTickDamage() {
            var area = GetNodeOrNull<Area2D>("Area2D");
            if (area == null) return;

            foreach (var body in area.GetOverlappingBodies()) {
                if (body is PlayerController pc && pc.PlayerIndex != _ownerIndex) {
                    pc.ApplyDamage((int)_damagePerTick);
                    pc.GetNodeOrNull<StatusController>("StatusController")
                        ?.ApplyStatus(FTT.Core.StatusType.TimeDilation, _statusDuration);
                    FTT.Core.EventBus.Instance?.RaiseStatusEffectApplied(new FTT.Core.StatusEffectPayload {
                        TargetIndex = pc.PlayerIndex,
                        Type = FTT.Core.StatusType.TimeDilation,
                        Duration = _statusDuration
                    });
                }
            }
        }
    }

    public partial class CleopatraDesertMirage : BaseSpecial {
        private const float StartupDuration = 0.1f;
        private const float ActiveDuration = 0.3f;
        private const float RecoveryDuration = 0.1f;
        private const float CooldownTime = 5.0f;
        private const float MirageDistance = 180f;
        private const float MaxMirageDuration = 3.0f;

        private Vector2 _mirageDirection;
        private Vector2 _startPosition;
        private float _mirageTimer;

        protected override void OnStartup() {
            PhaseTimer = StartupDuration;
            _startPosition = Owner.GlobalPosition;
            _mirageDirection = ResolveDirection();
            _mirageTimer = MaxMirageDuration;
        }

        protected override void OnActive() {
            PhaseTimer = ActiveDuration;
            Owner.GlobalPosition = _startPosition + _mirageDirection * MirageDistance;
        }

        protected override void OnRecovery() {
            PhaseTimer = RecoveryDuration;
            Owner.MovementAbilityCooldownTimer = CooldownTime;
            FTT.Core.EventBus.Instance?.RaiseMovementAbilityUsed(new FTT.Core.MovementAbilityPayload {
                PlayerIndex = Owner.PlayerIndex,
                AbilityName = "Desert Mirage",
                StartPosition = _startPosition,
                EndPosition = Owner.GlobalPosition
            });
            FTT.Core.EventBus.Instance?.RaiseCooldownStarted(new FTT.Core.CooldownPayload {
                PlayerIndex = Owner.PlayerIndex,
                Slot = FTT.Core.AbilitySlot.MovementAbility,
                Duration = CooldownTime
            });
        }

        private Vector2 ResolveDirection() {
            float hInput = 0f;
            if (Input.IsActionPressed(FTT.Core.InputManager.Actions.MoveRight)) hInput += 1f;
            if (Input.IsActionPressed(FTT.Core.InputManager.Actions.MoveLeft)) hInput -= 1f;
            float vInput = 0f;
            if (Input.IsActionPressed(FTT.Core.InputManager.Actions.Jump)) vInput -= 1f;
            if (Input.IsActionPressed(FTT.Core.InputManager.Actions.Down)) vInput += 1f;

            var dir = new Vector2(hInput, vInput);
            if (dir == Vector2.Zero) {
                dir = Owner.IsFacingRight ? Vector2.Right : Vector2.Left;
            }
            return dir.Normalized();
        }
    }

    public partial class CleopatraWrathOfTheNile : BaseSpecial {
        private const float CinematicDuration = 3.5f;
        private const int HitCount = 10;
        private const float DamagePerHit = 8f;
        private const float PoisonDuration = 5f;

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
                    DealSandstormHit();
                }
            }
            base._PhysicsProcess(delta);
        }

        private void DealSandstormHit() {
            var hitbox = GetNodeOrNull<Hitbox>("SandstormHitbox");
            if (hitbox == null) return;

            hitbox.Damage = DamagePerHit;
            hitbox.KnockbackForce = new Vector2(
                (float)GD.RandRange(-3f, 3f),
                (float)GD.RandRange(-2f, 2f)
            );
            hitbox.OwnerPlayerIndex = Owner.PlayerIndex;
            hitbox.GlobalPosition = Owner.GlobalPosition + new Vector2(
                (float)GD.RandRange(-150f, 150f),
                (float)GD.RandRange(-80f, 80f)
            );
            hitbox.Activate();
            GetTree().CreateTimer(0.06f).Timeout += () => {
                hitbox.Deactivate();
                ApplyPoisonToHitTargets();
            };
        }

        private void ApplyPoisonToHitTargets() {
            var hitbox = GetNodeOrNull<Hitbox>("SandstormHitbox");
            if (hitbox == null) return;

            foreach (var area in hitbox.GetOverlappingAreas()) {
                if (area is Hurtbox hurtbox && hurtbox.OwnerPlayerIndex != Owner.PlayerIndex) {
                    var target = hurtbox.GetParent<PlayerController>();
                    target?.GetNodeOrNull<StatusController>("StatusController")
                        ?.ApplyStatus(FTT.Core.StatusType.Burning, PoisonDuration);
                    FTT.Core.EventBus.Instance?.RaiseStatusEffectApplied(new FTT.Core.StatusEffectPayload {
                        TargetIndex = hurtbox.OwnerPlayerIndex,
                        Type = FTT.Core.StatusType.Burning,
                        Duration = PoisonDuration
                    });
                }
            }
        }
    }
}
