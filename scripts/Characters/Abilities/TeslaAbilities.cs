using Godot;
using FTT.Combat;
using FTT.Characters;

namespace FTT.Characters.Abilities {

    public partial class TeslaTeslaCoil : BaseSpecial {
        private const float StartupDuration = 0.35f;
        private const float ActiveDuration = 0.1f;
        private const float RecoveryDuration = 0.3f;
        private const int MaxActiveCoils = 2;

        protected override void OnStartup() {
            PhaseTimer = StartupDuration;
        }

        protected override void OnActive() {
            PhaseTimer = ActiveDuration;
            DeployCoil();
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

        private void DeployCoil() {
            if (Owner == null) return;
            if (Data?.ProjectileScene == null) return;

            while (Owner.ActivePersistentObjects.Count >= MaxActiveCoils) {
                var oldest = Owner.ActivePersistentObjects[0];
                Owner.ActivePersistentObjects.RemoveAt(0);
                if (oldest is FTT.Core.PooledNode pooled) pooled.ReturnToPool();
                else oldest.QueueFree();
            }

            var coil = FTT.Core.PoolManager.Instance?.Spawn(
                Data.ProjectileScene,
                Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? 70f : -70f, 0f)
            );

            if (coil is TeslaCoilNode node) {
                node.Initialize(Data.BaseDamage, Owner.PlayerIndex);
                Owner.ActivePersistentObjects.Add(node);
                TryLinkCoils();
            }
        }

        private void TryLinkCoils() {
            if (Owner.ActivePersistentObjects.Count < 2) return;
            if (Owner.ActivePersistentObjects[^1] is TeslaCoilNode coilA &&
                Owner.ActivePersistentObjects[^2] is TeslaCoilNode coilB) {
                coilA.LinkPartner(coilB);
                coilB.LinkPartner(coilA);
            }
        }
    }

    public partial class TeslaCoilNode : FTT.Core.PooledNode, FTT.Core.IPoolable {
        private const float Lifespan = 30f;
        private const float ArcInterval = 0.5f;
        private const float ArcRange = 200f;
        private const float ArcDamage = 4f;

        private float _damage;
        private int _ownerIndex;
        private float _lifetime;
        private float _arcTimer;
        private TeslaCoilNode _partner;
        private Hitbox _barrierHitbox;

        public void Initialize(float damage, int ownerIndex) {
            _damage = damage;
            _ownerIndex = ownerIndex;
            _lifetime = Lifespan;
            _arcTimer = ArcInterval;
            _partner = null;
        }

        public void LinkPartner(TeslaCoilNode partner) {
            _partner = partner;
            _barrierHitbox = GetNodeOrNull<Hitbox>("BarrierHitbox");
            _barrierHitbox?.Activate();
        }

        public void OnSpawn() { }
        public void OnDespawn() {
            _barrierHitbox?.Deactivate();
            if (_partner != null) {
                _partner._partner = null;
                _partner._barrierHitbox?.Deactivate();
            }
        }

        public void Explode() {
            var hitbox = GetNodeOrNull<Hitbox>("ExplosionHitbox");
            if (hitbox != null) {
                hitbox.Damage = _damage * 2f;
                hitbox.KnockbackForce = new Vector2(0, -5f);
                hitbox.OwnerPlayerIndex = _ownerIndex;
                hitbox.Activate();
                GetTree().CreateTimer(0.15f).Timeout += () => hitbox.Deactivate();
            }
            ReturnToPool();
        }

        public override void _PhysicsProcess(double delta) {
            float dt = (float)delta;
            _lifetime -= dt;
            if (_lifetime <= 0) {
                ReturnToPool();
                return;
            }

            _arcTimer -= dt;
            if (_arcTimer <= 0) {
                _arcTimer = ArcInterval;
                FireArc();
            }
        }

        private void FireArc() {
            var target = FindNearestEnemy();
            if (target == null) return;

            var arcScene = GetNodeOrNull<PackedScene>("ArcScene");
            if (arcScene == null) return;

            var arc = FTT.Core.PoolManager.Instance?.Spawn(arcScene, GlobalPosition);
            if (arc is TeslaCoilArc projectile) {
                projectile.Initialize(ArcDamage, target.GlobalPosition, _ownerIndex);
            }
        }

        private Node2D FindNearestEnemy() {
            Node2D nearest = null;
            float nearestDist = ArcRange;
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

    public partial class TeslaCoilArc : FTT.Core.PooledNode, FTT.Core.IPoolable {
        private float _speed = 360f;
        private float _damage;
        private Vector2 _direction;
        private int _ownerIndex;
        private float _lifetime = 0.4f;
        private Hitbox _hitbox;

        public void Initialize(float damage, Vector2 targetPos, int ownerIndex) {
            _damage = damage;
            _direction = (targetPos - GlobalPosition).Normalized();
            _ownerIndex = ownerIndex;
            _lifetime = 0.4f;

            _hitbox = GetNodeOrNull<Hitbox>("Hitbox");
            if (_hitbox != null) {
                _hitbox.Damage = _damage;
                _hitbox.KnockbackForce = _direction * 2f;
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

    public partial class TeslaLorentzPulse : BaseSpecial {
        private const float StartupDuration = 0.25f;
        private const float ActiveDuration = 0.15f;
        private const float RecoveryDuration = 0.35f;
        private const float RootDuration = 2.0f;
        private const float PulseRadius = 140f;

        protected override void OnStartup() {
            PhaseTimer = StartupDuration;
        }

        protected override void OnActive() {
            PhaseTimer = ActiveDuration;
            EmitPulse();
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

        private void EmitPulse() {
            var hitbox = GetNodeOrNull<Hitbox>("PulseHitbox");
            if (hitbox != null) {
                hitbox.Damage = Data?.BaseDamage ?? 8f;
                hitbox.KnockbackForce = Data?.KnockbackForce ?? new Vector2(0, -2f);
                hitbox.OwnerPlayerIndex = Owner.PlayerIndex;
                hitbox.GlobalPosition = Owner.GlobalPosition;
                hitbox.Activate();
                GetTree().CreateTimer(ActiveDuration).Timeout += () => hitbox.Deactivate();
            }

            foreach (var node in GetTree().GetNodesInGroup("players")) {
                if (node is PlayerController pc && pc.PlayerIndex != Owner.PlayerIndex) {
                    if (Owner.GlobalPosition.DistanceTo(pc.GlobalPosition) <= PulseRadius) {
                        pc.GetNodeOrNull<StatusController>("StatusController")
                            ?.ApplyStatus(FTT.Core.StatusType.Root, RootDuration);
                        ChainLightningToCoils(pc);
                    }
                }
            }
        }

        private void ChainLightningToCoils(PlayerController target) {
            foreach (var obj in Owner.ActivePersistentObjects) {
                if (obj is TeslaCoilNode coil) {
                    var arcScene = coil.GetNodeOrNull<PackedScene>("ArcScene");
                    if (arcScene == null) continue;
                    var arc = FTT.Core.PoolManager.Instance?.Spawn(arcScene, target.GlobalPosition);
                    if (arc is TeslaCoilArc projectile) {
                        projectile.Initialize(Data?.BaseDamage ?? 6f, coil.GlobalPosition, Owner.PlayerIndex);
                    }
                }
            }
        }
    }

    public partial class TeslaLightningBlink : BaseSpecial {
        private const float StartupDuration = 0.05f;
        private const float ActiveDuration = 0.2f;
        private const float RecoveryDuration = 0.1f;
        private const float CooldownTime = 5.0f;
        private const float BlinkDistance = 160f;

        private Vector2 _blinkDirection;
        private Vector2 _startPosition;

        protected override void OnStartup() {
            PhaseTimer = StartupDuration;

            float hInput = Owner.CurrentInputFrame.Horizontal;
            float vInput = Owner.CurrentInputFrame.Vertical;

            _blinkDirection = new Vector2(hInput, vInput);
            if (_blinkDirection == Vector2.Zero) {
                _blinkDirection = Owner.IsFacingRight ? Vector2.Right : Vector2.Left;
            }
            _blinkDirection = _blinkDirection.Normalized();
            _startPosition = Owner.GlobalPosition;
        }

        protected override void OnActive() {
            PhaseTimer = ActiveDuration;
        }

        protected override void OnRecovery() {
            PhaseTimer = RecoveryDuration;
            Owner.MovementAbilityCooldownTimer = CooldownTime;
            FTT.Core.EventBus.Instance?.RaiseMovementAbilityUsed(new FTT.Core.MovementAbilityPayload {
                PlayerIndex = Owner.PlayerIndex,
                AbilityName = "Lightning Blink",
                StartPosition = _startPosition,
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
                float speed = BlinkDistance / ActiveDuration * 60f;
                Owner.Velocity = _blinkDirection * speed;
            }
            base._PhysicsProcess(delta);
        }
    }

    public partial class TeslaWardenclyffeCataclysm : BaseSpecial {
        private const float CinematicDuration = 2.5f;
        private const float ShockwaveDamage = 18f;
        private const int HitCount = 4;

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
            ExplodeAllCoils();
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
                    DealShockwaveHit();
                }
            }
            base._PhysicsProcess(delta);
        }

        private void ExplodeAllCoils() {
            var coils = new System.Collections.Generic.List<Node2D>(Owner.ActivePersistentObjects);
            foreach (var obj in coils) {
                if (obj is TeslaCoilNode coil) {
                    coil.Explode();
                    Owner.ActivePersistentObjects.Remove(obj);
                }
            }
        }

        private void DealShockwaveHit() {
            var hitbox = GetNodeOrNull<Hitbox>("ShockwaveHitbox");
            if (hitbox == null) return;

            hitbox.Damage = ShockwaveDamage;
            hitbox.KnockbackForce = new Vector2(0, -6f);
            hitbox.OwnerPlayerIndex = Owner.PlayerIndex;
            hitbox.GlobalPosition = Owner.GlobalPosition;
            hitbox.Activate();
            GetTree().CreateTimer(0.12f).Timeout += () => hitbox.Deactivate();
        }
    }
}
