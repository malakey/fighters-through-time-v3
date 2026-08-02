using Godot;
using System.Collections.Generic;

namespace FTT.Combat {

    public interface IStatusStrategy {
        void OnApply(FTT.Characters.PlayerController target);
        void OnTick(FTT.Characters.PlayerController target, float delta);
        void OnRemove(FTT.Characters.PlayerController target);
    }

    public class TimeDilationStrategy : IStatusStrategy {
        public float SpeedReduction = 0.5f;
        public void OnApply(FTT.Characters.PlayerController target) { }
        public void OnTick(FTT.Characters.PlayerController target, float delta) { }
        public void OnRemove(FTT.Characters.PlayerController target) { }
    }

    public class EnergizedStrategy : IStatusStrategy {
        public float SpeedBoost = 0.25f;
        public void OnApply(FTT.Characters.PlayerController target) { }
        public void OnTick(FTT.Characters.PlayerController target, float delta) { }
        public void OnRemove(FTT.Characters.PlayerController target) { }
    }

    public class WeakenedStrategy : IStatusStrategy {
        public float DamageReduction = 0.25f;
        public void OnApply(FTT.Characters.PlayerController target) { }
        public void OnTick(FTT.Characters.PlayerController target, float delta) { }
        public void OnRemove(FTT.Characters.PlayerController target) { }
    }

    public class BurningStrategy : IStatusStrategy {
        public float DamagePerTick = 2f;
        public float TickInterval = 0.5f;
        private float _tickTimer;
        public void OnApply(FTT.Characters.PlayerController target) { _tickTimer = 0; }
        public void OnTick(FTT.Characters.PlayerController target, float delta) {
            _tickTimer += delta;
            if (_tickTimer >= TickInterval) {
                _tickTimer -= TickInterval;
                target.ApplyDamage((int)DamagePerTick);
            }
        }
        public void OnRemove(FTT.Characters.PlayerController target) { }
    }

    public class StunnedStrategy : IStatusStrategy {
        public void OnApply(FTT.Characters.PlayerController target) {
            target.TransitionTo(FTT.Characters.CharacterState.Stunned);
        }
        public void OnTick(FTT.Characters.PlayerController target, float delta) { }
        public void OnRemove(FTT.Characters.PlayerController target) { }
    }

    public partial class StatusController : Node {
        private FTT.Core.StatusType _activeType = FTT.Core.StatusType.None;
        private IStatusStrategy _activeStrategy;
        private float _remainingDuration;
        private FTT.Characters.PlayerController _owner;

        private static readonly Dictionary<FTT.Core.StatusType, System.Func<IStatusStrategy>> StrategyFactory = new() {
            { FTT.Core.StatusType.TimeDilation, () => new TimeDilationStrategy() },
            { FTT.Core.StatusType.Energized, () => new EnergizedStrategy() },
            { FTT.Core.StatusType.Weakened, () => new WeakenedStrategy() },
            { FTT.Core.StatusType.Burning, () => new BurningStrategy() },
            { FTT.Core.StatusType.Stunned, () => new StunnedStrategy() }
        };

        public override void _Ready() {
            _owner = GetParent<FTT.Characters.PlayerController>();
        }

        public void ApplyStatus(FTT.Core.StatusType type, float duration) {
            if (type == FTT.Core.StatusType.None) return;
            if (_activeStrategy != null) {
                _activeStrategy.OnRemove(_owner);
            }

            _activeType = type;
            _remainingDuration = duration;
            _activeStrategy = StrategyFactory.TryGetValue(type, out var factory) ? factory() : null;
            _activeStrategy?.OnApply(_owner);

            FTT.Core.EventBus.Instance?.RaiseStatusEffectApplied(new FTT.Core.StatusEffectPayload {
                TargetIndex = _owner?.PlayerIndex ?? 0,
                Type = type,
                Duration = duration
            });
        }

        public void ClearStatus() {
            if (_activeStrategy != null) {
                _activeStrategy.OnRemove(_owner);
                _activeStrategy = null;
            }
            _activeType = FTT.Core.StatusType.None;
            _remainingDuration = 0;
        }

        public override void _PhysicsProcess(double delta) {
            if (_activeType == FTT.Core.StatusType.None || _activeStrategy == null) return;

            float dt = (float)delta;
            _remainingDuration -= dt;
            _activeStrategy.OnTick(_owner, dt);

            if (_remainingDuration <= 0) {
                ClearStatus();
            }
        }

        public FTT.Core.StatusType ActiveType => _activeType;
        public float RemainingDuration => _remainingDuration;
    }
}
