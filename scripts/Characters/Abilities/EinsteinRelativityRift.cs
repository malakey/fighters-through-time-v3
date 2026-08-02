using Godot;
using FTT.Combat;
using FTT.Characters;

namespace FTT.Characters.Abilities {

    public partial class EinsteinRelativityRift : BaseSpecial {

        private const float StartupDuration = 0.2f;
        private const float ActiveDuration = 0.1f;
        private const float RecoveryDuration = 0.3f;

        protected override void OnStartup() {
            PhaseTimer = StartupDuration;
        }

        protected override void OnActive() {
            PhaseTimer = ActiveDuration;
            SpawnRift();
        }

        protected override void OnRecovery() {
            PhaseTimer = RecoveryDuration;
        }

        private void SpawnRift() {
            if (Data?.ProjectileScene == null && Owner != null) {
                SpawnPlaceholderZone(
                    Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? 100f : -100f, 0f),
                    Data.BaseDamage,
                    Data.StatusDuration > 0 ? Data.StatusDuration : 3f,
                    0.5f,
                    new Color(0.4f, 0.3f, 0.9f));
                Owner.SpecialTwoCooldownTimer = Data.CooldownDuration;
                FTT.Core.EventBus.Instance?.RaiseCooldownStarted(new FTT.Core.CooldownPayload {
                    PlayerIndex = Owner.PlayerIndex,
                    Slot = FTT.Core.AbilitySlot.Special2,
                    Duration = Data.CooldownDuration
                });
                return;
            }
            if (Owner == null) return;

            var rift = FTT.Core.PoolManager.Instance?.Spawn(
                Data.ProjectileScene,
                Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? 100f : -100f, 0f)
            );

            if (rift is RelativityRiftZone zone) {
                zone.Initialize(Data.BaseDamage, Data.StatusDuration, Owner.PlayerIndex);
            }

            Owner.SpecialTwoCooldownTimer = Data.CooldownDuration;
            FTT.Core.EventBus.Instance?.RaiseCooldownStarted(new FTT.Core.CooldownPayload {
                PlayerIndex = Owner.PlayerIndex,
                Slot = FTT.Core.AbilitySlot.Special2,
                Duration = Data.CooldownDuration
            });
        }
    }

    public partial class RelativityRiftZone : FTT.Core.PooledNode, FTT.Core.IPoolable {
        private float _damagePerTick = 1.5f;
        private float _duration = 3.0f;
        private float _tickInterval = 0.5f;
        private float _tickTimer;
        private float _lifetime;
        private int _ownerIndex;

        public void Initialize(float damagePerTick, float duration, int ownerIndex) {
            _damagePerTick = damagePerTick;
            _duration = duration;
            _lifetime = duration;
            _ownerIndex = ownerIndex;
            _tickTimer = 0;
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

            _tickTimer += dt;
            if (_tickTimer >= _tickInterval) {
                _tickTimer -= _tickInterval;
                ApplyEffectsToOverlapping();
            }
        }

        private void ApplyEffectsToOverlapping() {
            var area = GetNodeOrNull<Area2D>("Area2D");
            if (area == null) return;

            foreach (var body in area.GetOverlappingBodies()) {
                if (body is PlayerController pc) {
                    if (pc.PlayerIndex == _ownerIndex) {
                        // Self-buff: +25% speed (handled by status system)
                    } else {
                        pc.ApplyDamage((int)_damagePerTick);
                    }
                }
            }
        }
    }
}
