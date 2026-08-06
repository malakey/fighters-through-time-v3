using Godot;
using FTT.Characters;

namespace FTT.Combat {

    public enum BlockResult {
        NotBlocked,
        Blocked,
        GuardBroken
    }

    public static class BlockRules {
        public static bool IsHitInFront(Vector2 defenderPosition, bool defenderFacingRight, Vector2 hitOrigin) {
            return defenderFacingRight
                ? hitOrigin.X >= defenderPosition.X
                : hitOrigin.X <= defenderPosition.X;
        }

        public static int ChargeCost(AttackClass attackClass, int currentCharges) => attackClass switch {
            AttackClass.Basic => 1,
            AttackClass.Hazard => 1,
            AttackClass.Special => currentCharges,
            _ => 0
        };

        public static bool BypassesBlock(AttackClass attackClass) =>
            attackClass == AttackClass.Ultimate;
    }

    public partial class BlockSystem : Node {
        [Export] public int MaxCharges = 3;
        public int CurrentCharges { get; private set; }

        private float _regenTimer;
        private const float RegenInterval = 3.0f;
        private bool _isBlocking;
        private PlayerController _owner;

        public override void _Ready() {
            _owner = GetParent<PlayerController>();
            CurrentCharges = Mathf.Max(0, MaxCharges);
        }

        public bool IsBlocking => _isBlocking;

        public void StartBlock() {
            _isBlocking = CurrentCharges > 0;
            if (_isBlocking) _regenTimer = 0f;
        }

        public void EndBlock() {
            _isBlocking = false;
        }

        public BlockResult ResolveHit(in HitPayload hit) {
            if (!_isBlocking || CurrentCharges <= 0 || _owner == null) return BlockResult.NotBlocked;
            if (BlockRules.BypassesBlock(hit.AttackClass)) return BlockResult.NotBlocked;
            if (!BlockRules.IsHitInFront(_owner.GlobalPosition, _owner.IsFacingRight, hit.HitOrigin)) {
                return BlockResult.NotBlocked;
            }

            int cost = BlockRules.ChargeCost(hit.AttackClass, CurrentCharges);
            if (cost <= 0) return BlockResult.NotBlocked;

            CurrentCharges = Mathf.Max(0, CurrentCharges - cost);
            _regenTimer = 0f;
            if (CurrentCharges > 0) return BlockResult.Blocked;

            BreakGuard();
            return BlockResult.GuardBroken;
        }

        public void ApplyStockReset() {
            CurrentCharges = Mathf.Max(0, MaxCharges);
            _regenTimer = 0f;
            _isBlocking = false;
        }

        private void BreakGuard() {
            _isBlocking = false;
            _owner.TransitionTo(CharacterState.Dazed);

            Vector2 velocity = _owner.Velocity;
            velocity += _owner.IsFacingRight ? new Vector2(-120f, -60f) : new Vector2(120f, -60f);
            _owner.Velocity = velocity;
            FTT.Core.EventBus.Instance?.RaiseBlockBroken(_owner.PlayerIndex);
        }

        public override void _PhysicsProcess(double delta) {
            if (_isBlocking || CurrentCharges >= MaxCharges) return;
            _regenTimer += (float)delta;
            if (_regenTimer >= RegenInterval) {
                _regenTimer -= RegenInterval;
                CurrentCharges = Mathf.Min(CurrentCharges + 1, MaxCharges);
            }
        }
    }
}
