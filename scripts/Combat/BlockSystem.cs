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
        private const float RegenInterval = BasicComboRules.BlockChargeRegenFrames / 60f;
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
            if (CurrentCharges > 0) {
                GrantHenrysBastion();
                return BlockResult.Blocked;
            }

            BreakGuard();
            return BlockResult.GuardBroken;
        }

        /// <summary>Story-only Resonance perk key: a successful block summons a phantom shield guard.</summary>
        public const string HenrysBastionPerkKey = "henrys_bastion";
        private const float HenrysBastionCapacityShare = 0.10f;

        /// <summary>
        /// Henry's Bastion (Story-only): successfully blocking an attack summons a
        /// phantom royal shield guard absorbing up to 10% of Shakespeare's maximum
        /// HP. The guard is granted fully charged on each successful block; the
        /// design's "temporary" guard lapses by absorbing damage rather than on a
        /// timer (approximation noted in docs/PACKAGE3_KIT_AUDIT.md).
        /// </summary>
        private void GrantHenrysBastion() {
            if (_owner == null || !_owner.HasStoryPerk(HenrysBastionPerkKey)) return;
            float capacity = HenrysBastionCapacityShare * _owner.MaximumHP;
            _owner.ConfigureStoryShield(capacity);
            _owner.RechargeStoryShield(capacity);
        }

        public void ApplyStockReset() {
            CurrentCharges = Mathf.Max(0, MaxCharges);
            _regenTimer = 0f;
            _isBlocking = false;
        }

        /// <summary>
        /// Depletes a fixed number of charges outside the per-class cost table.
        /// Design-specified "shield-stutter" specials (Lincoln's Emancipator,
        /// Joan's Divine Piercing) drain exactly 2 charges instead of the generic
        /// special full shatter. Breaks the guard when the last charge is spent.
        /// </summary>
        public BlockResult DepleteCharges(int count) {
            if (CurrentCharges <= 0 || count <= 0) return BlockResult.NotBlocked;
            CurrentCharges = Mathf.Max(0, CurrentCharges - count);
            _regenTimer = 0f;
            if (CurrentCharges > 0) {
                // Guard Impact haptic: a shield-stutter special absorbed without a
                // break is still a successful block (design haptic table).
                FTT.Core.HapticFeedbackManager.Instance?.OnGuardImpact(_owner?.PlayerIndex ?? -1);
                return BlockResult.Blocked;
            }

            BreakGuard();
            return BlockResult.GuardBroken;
        }

        /// <summary>Story-only Resonance perk key: teleport backward on guard break.</summary>
        public const string QuantumEntanglementPerkKey = "quantum_entanglement";
        private const float QuantumEntanglementDistance = 150f;

        private void BreakGuard() {
            _isBlocking = false;
            _owner.TransitionTo(CharacterState.Dazed);

            if (_owner.HasStoryPerk(QuantumEntanglementPerkKey)) {
                // Quantum Entanglement: instead of being shoved, Einstein blinks
                // backward out of immediate follow-up range.
                float direction = _owner.IsFacingRight ? -1f : 1f;
                _owner.GlobalPosition += new Vector2(direction * QuantumEntanglementDistance, 0f);
                _owner.Velocity = new Vector2(0f, _owner.Velocity.Y);
            } else {
                Vector2 velocity = _owner.Velocity;
                velocity += _owner.IsFacingRight ? new Vector2(-120f, -60f) : new Vector2(120f, -60f);
                _owner.Velocity = velocity;
            }
            FTT.Core.EventBus.Instance?.RaiseBlockBroken(_owner.PlayerIndex);
        }

        public override void _PhysicsProcess(double delta) {
            if (_isBlocking || CurrentCharges >= MaxCharges) return;
            // Story-only BlockRecovery minors regenerate charges faster (1f
            // outside Story Mode).
            _regenTimer += (float)delta * (_owner?.StoryBlockRecoveryMultiplier ?? 1f);
            if (_regenTimer >= RegenInterval) {
                _regenTimer -= RegenInterval;
                CurrentCharges = Mathf.Min(CurrentCharges + 1, MaxCharges);
            }
        }
    }
}
