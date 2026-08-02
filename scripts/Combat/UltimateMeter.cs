using Godot;

namespace FTT.Combat {

    public partial class UltimateMeter : Node {
        public float CurrentValue { get; private set; }
        public const float MaxValue = 100f;
        public const float PointsPerDamageDealt = 1.0f;
        public const float PointsPerDamageTaken = 0.25f;

        public bool IsFull => CurrentValue >= MaxValue;

        public void AddFromDamageDealt(float damageAmount) {
            CurrentValue = Mathf.Min(CurrentValue + damageAmount * PointsPerDamageDealt, MaxValue);
            FTT.Core.EventBus.Instance?.RaiseUltimateMeterChanged(CurrentValue / MaxValue);
        }

        public void AddFromDamageTaken(float damageAmount) {
            CurrentValue = Mathf.Min(CurrentValue + damageAmount * PointsPerDamageTaken, MaxValue);
            FTT.Core.EventBus.Instance?.RaiseUltimateMeterChanged(CurrentValue / MaxValue);
        }

        public void Consume() {
            CurrentValue = 0f;
            FTT.Core.EventBus.Instance?.RaiseUltimateMeterChanged(0f);
        }

        public void Reset() {
            CurrentValue = 0f;
        }
    }
}
