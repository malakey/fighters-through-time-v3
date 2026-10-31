using Godot;
using FTT.Characters;
using FTT.Core;

namespace FTT.Combat {

    public partial class UltimateMeter : Node {
        public float CurrentValue { get; private set; }
        // The numbers live once, in InfluenceMeterRules (shared with the Fighter
        // sim); these names forward to it for the Story call sites. Story has no
        // stocks, so the dead stock-loss retention path was deleted on 2026-10-31.
        public const float MaxValue = InfluenceMeterRules.MaxValue;
        public const float PointsPerDamageDealt = InfluenceMeterRules.PointsPerDamageDealt;
        public const float PointsPerDamageTaken = InfluenceMeterRules.PointsPerDamageTaken;

        private int _playerIndex;
        /// <summary>The controller whose <c>CurrentUltimateMeter</c> mirrors this node (G2).</summary>
        private PlayerController _owner;

        public override void _Ready() {
            _owner = GetParent() as PlayerController;
            _playerIndex = _owner?.PlayerIndex ?? 0;
            Publish();
        }

        public bool IsFull => CurrentValue >= MaxValue;

        /// <summary>
        /// The Story-only multiplier on meter GAIN, read from the owning
        /// controller: D5's Story scale on the campaign hero times Mozart's
        /// build-rate lane (<see cref="PlayerController.StoryMeterGainFactor"/>). It scales
        /// the two gain paths below — damage dealt, and damage taken including
        /// Rally echo as it drains — and nothing else: flat grants
        /// (<see cref="AddFlat"/>), spends and direct sets never read it. A node
        /// with no controller (a bare test node) gains at the rulebook rate.
        /// </summary>
        private float GainFactor =>
            (_owner ?? GetParent() as PlayerController)?.StoryMeterGainFactor ?? 1f;

        public void AddFromDamageDealt(float damageAmount) =>
            SetValue(CurrentValue + InfluenceMeterRules.GainFromDamageDealt(damageAmount, GainFactor));

        public void AddFromDamageTaken(float damageAmount) =>
            SetValue(CurrentValue + InfluenceMeterRules.GainFromDamageTaken(damageAmount, GainFactor));

        public void AddFlat(float points) => SetValue(CurrentValue + Mathf.Max(0f, points));

        public void Consume() => SetValue(0f);

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
