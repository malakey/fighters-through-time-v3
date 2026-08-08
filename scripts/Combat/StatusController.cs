using Godot;
using System;
using System.Collections.Generic;

namespace FTT.Combat {

    public interface IStatusStrategy {
        void OnApply(FTT.Characters.PlayerController target, float duration, float intensity);
        void OnTick(FTT.Characters.PlayerController target, float delta);
        void OnRemove(FTT.Characters.PlayerController target);
        float CaptureRuntimeState() => 0f;
        void RestoreRuntimeState(float value) { }
    }

    public struct StatusSnapshot {
        public FTT.Core.StatusType Type;
        public float RemainingDuration;
        public float Intensity;
        public float StrategyRuntimeState;
    }

    public sealed class TimeDilationStrategy : IStatusStrategy {
        public void OnApply(FTT.Characters.PlayerController target, float duration, float intensity) {
            float potency = intensity <= 0 ? 1.0f : intensity;
            target.StatusMovementMultiplier = MathF.Max(0.1f, 1.0f - (0.5f * potency));
            target.StatusJumpMultiplier = target.StatusMovementMultiplier;
            target.StatusAnimationMultiplier = 0.5f;
        }
        public void OnTick(FTT.Characters.PlayerController target, float delta) { }
        public void OnRemove(FTT.Characters.PlayerController target) => target.ResetStatusModifiers();
    }

    public sealed class VenomStrategy : IStatusStrategy {
        private const float TickInterval = 1.0f;
        private float _damagePerTick;
        private float _tickTimer;

        public void OnApply(FTT.Characters.PlayerController target, float duration, float intensity) {
            _damagePerTick = 2.0f * (intensity <= 0 ? 1.0f : intensity);
            _tickTimer = TickInterval;
        }
        public void OnTick(FTT.Characters.PlayerController target, float delta) {
            _tickTimer -= delta;
            if (_tickTimer <= 0.0f) {
                _tickTimer += TickInterval;
                target.ApplyPersistentDamage(Math.Max(1, (int)MathF.Round(_damagePerTick)));
            }
        }
        public void OnRemove(FTT.Characters.PlayerController target) => target.ResetStatusModifiers();
        public float CaptureRuntimeState() => _tickTimer;
        public void RestoreRuntimeState(float value) => _tickTimer = value;
    }

    public sealed class StaticChargeStrategy : IStatusStrategy {
        public void OnApply(FTT.Characters.PlayerController target, float duration, float intensity) {
            target.ApplyStun(duration);
        }
        public void OnTick(FTT.Characters.PlayerController target, float delta) { }
        public void OnRemove(FTT.Characters.PlayerController target) => target.ResetStatusModifiers();
    }

    public sealed class RadiantBurnStrategy : IStatusStrategy {
        public void OnApply(FTT.Characters.PlayerController target, float duration, float intensity) {
            float potency = intensity <= 0 ? 1.0f : intensity;
            target.StatusDamageTakenMultiplier = 1.0f + (0.25f * potency);
        }
        public void OnTick(FTT.Characters.PlayerController target, float delta) { }
        public void OnRemove(FTT.Characters.PlayerController target) => target.ResetStatusModifiers();
    }

    public sealed class RootStrategy : IStatusStrategy {
        public void OnApply(FTT.Characters.PlayerController target, float duration, float intensity) {
            target.IsMovementRooted = true;
            var velocity = target.Velocity;
            velocity.X = 0.0f;
            target.Velocity = velocity;
        }
        public void OnTick(FTT.Characters.PlayerController target, float delta) { }
        public void OnRemove(FTT.Characters.PlayerController target) => target.ResetStatusModifiers();
    }

    public partial class StatusController : Node {
        private FTT.Core.StatusType _activeType = FTT.Core.StatusType.None;
        private IStatusStrategy _activeStrategy;
        private float _remainingDuration;
        private float _intensity;
        private FTT.Characters.PlayerController _owner;

        private static readonly Dictionary<FTT.Core.StatusType, Func<IStatusStrategy>> StrategyFactory = new() {
            { FTT.Core.StatusType.TimeDilation, () => new TimeDilationStrategy() },
            { FTT.Core.StatusType.Venom, () => new VenomStrategy() },
            { FTT.Core.StatusType.StaticCharge, () => new StaticChargeStrategy() },
            { FTT.Core.StatusType.RadiantBurn, () => new RadiantBurnStrategy() },
            { FTT.Core.StatusType.Root, () => new RootStrategy() }
        };

        public override void _Ready() {
            _owner = GetParent<FTT.Characters.PlayerController>();
        }

        public void ApplyStatus(FTT.Core.StatusType type, float duration, float intensity = 1.0f) {
            if (type == FTT.Core.StatusType.None || duration <= 0.0f || _owner == null) return;
            ClearStatus();

            _activeType = type;
            _remainingDuration = duration;
            _intensity = intensity <= 0.0f ? 1.0f : intensity;
            _activeStrategy = StrategyFactory.TryGetValue(type, out Func<IStatusStrategy> factory) ? factory() : null;
            _activeStrategy?.OnApply(_owner, duration, _intensity);

            FTT.Core.EventBus.Instance?.RaiseStatusEffectApplied(new FTT.Core.StatusEffectPayload {
                TargetIndex = _owner.PlayerIndex,
                Type = type,
                Duration = duration,
                Intensity = _intensity
            });
        }

        public StatusSnapshot CaptureState() => new() {
            Type = _activeType,
            RemainingDuration = _remainingDuration,
            Intensity = _intensity,
            StrategyRuntimeState = _activeStrategy?.CaptureRuntimeState() ?? 0f
        };

        public void RestoreState(StatusSnapshot snapshot) {
            ClearStatus();
            if (snapshot.Type == FTT.Core.StatusType.None || snapshot.RemainingDuration <= 0f) return;

            ApplyStatus(snapshot.Type, snapshot.RemainingDuration, snapshot.Intensity);
            _remainingDuration = snapshot.RemainingDuration;
            _activeStrategy?.RestoreRuntimeState(snapshot.StrategyRuntimeState);
        }

        public void ClearStatus() {
            bool hadStatus = _activeType != FTT.Core.StatusType.None;
            _activeStrategy?.OnRemove(_owner);
            _activeStrategy = null;
            _activeType = FTT.Core.StatusType.None;
            _remainingDuration = 0.0f;
            _intensity = 0.0f;
            if (!hadStatus || _owner == null) return;
            // Presentation layers (glow arbiter, HUD status pips) need the falling
            // edge as well as the rising one; nothing gameplay-side reads this.
            FTT.Core.EventBus.Instance?.RaiseStatusEffectCleared(new FTT.Core.StatusEffectPayload {
                TargetIndex = _owner.PlayerIndex,
                Type = FTT.Core.StatusType.None,
                Duration = 0f,
                Intensity = 0f
            });
        }

        public override void _PhysicsProcess(double delta) {
            if (_activeType == FTT.Core.StatusType.None || _activeStrategy == null || _owner == null) return;

            float dt = (float)delta;
            _remainingDuration -= dt;
            _activeStrategy.OnTick(_owner, dt);
            if (_remainingDuration <= 0.0f) ClearStatus();
        }

        public FTT.Core.StatusType ActiveType => _activeType;
        public float RemainingDuration => _remainingDuration;
        public float Intensity => _intensity;
    }
}
