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
        /// <summary>The controller whose <c>CurrentUltimateMeter</c> mirrors this node (G2).</summary>
        private PlayerController _owner;

        public override void _Ready() {
            _owner = GetParent() as PlayerController;
            _playerIndex = _owner?.PlayerIndex ?? 0;
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
            // 2026-10-04 fix pass (G2): the owning controller's public
            // CurrentUltimateMeter field is a mirror of this node, read by Defy
            // History, the Defy seal and the save writers. Writers that reach the
            // node directly (every Ultimate's own Consume, an orb, a test) used to
            // leave that mirror stale until the next controller-side meter event,
            // so it is synced on every write rather than at each call site.
            (_owner ?? GetParent() as PlayerController)?.SyncUltimateMeterFromNode(CurrentValue);
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
