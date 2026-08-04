using Godot;
using FTT.Characters;
using FTT.Core;

namespace FTT.Combat {

    public partial class UltimateMeter : Node {
        public float CurrentValue { get; private set; }
        public const float MaxValue = 100f;
        public const float PointsPerDamageDealt = 1.0f;
        public const float PointsPerDamageTaken = 0.25f;
        public const float StockLossRetention = 0.75f;

        private int _playerIndex;

        public override void _Ready() {
            _playerIndex = GetParent<PlayerController>()?.PlayerIndex ?? 0;
            Publish();
        }

        public bool IsFull => CurrentValue >= MaxValue;

        public void AddFromDamageDealt(float damageAmount) =>
            SetValue(CurrentValue + Mathf.Max(0f, damageAmount) * PointsPerDamageDealt);

        public void AddFromDamageTaken(float damageAmount) =>
            SetValue(CurrentValue + Mathf.Max(0f, damageAmount) * PointsPerDamageTaken);

        public void AddFlat(float points) => SetValue(CurrentValue + Mathf.Max(0f, points));

        public void Consume() => SetValue(0f);

        public void ApplyStockLossRetention() => SetValue(CurrentValue * StockLossRetention);

        public void Reset() => SetValue(0f);

        public void SetValue(float value) {
            CurrentValue = Mathf.Clamp(value, 0f, MaxValue);
            Publish();
        }

        private void Publish() {
            EventBus.Instance?.RaiseUltimateMeterChanged(new UltimateMeterPayload {
                PlayerIndex = _playerIndex,
                CurrentValue = CurrentValue,
                NormalizedValue = CurrentValue / MaxValue,
                IsFull = IsFull
            });
        }
    }
}
