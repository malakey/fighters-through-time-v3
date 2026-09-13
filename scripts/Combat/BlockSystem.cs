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
        private const float LockoutSeconds = BasicComboRules.BlockShatterLockoutFrames / 60f;
        private const float ShieldStunSeconds = BasicComboRules.ShieldstunFrames / 60f;
        private bool _isBlocking;
        private float _lockoutTimer;
        private float _shieldStunTimer;
        private PlayerController _owner;

        public override void _Ready() {
            _owner = GetParent<PlayerController>();
            CurrentCharges = Mathf.Max(0, MaxCharges);
            RaiseChargesChanged();
        }

        /// <summary>
        /// M-27: publishes the charge state so the Story HUD's shield pips track
        /// blocks, guard breaks, resets, and regen. Raised from every site that
        /// mutates <see cref="CurrentCharges"/>.
        /// </summary>
        private void RaiseChargesChanged() {
            FTT.Core.EventBus.Instance?.RaiseBlockChargesChanged(new FTT.Core.BlockChargesPayload {
                PlayerIndex = _owner?.PlayerIndex ?? 0,
                CurrentCharges = CurrentCharges,
                MaxCharges = MaxCharges
            });
        }

        public bool IsBlocking => _isBlocking;

        /// <summary>
        /// V7.3: whether the stance may rise at all — at least one charge and
        /// no running shatter lockout. CheckBlockInput and the hitstun
        /// block-cancel both gate on this, mirroring the sim's IsBlockStance.
        /// </summary>
        public bool CanRaiseStance => CurrentCharges > 0 && _lockoutTimer <= 0f;

        /// <summary>V7.3 shieldstun: the blocker is locked into the stance —
        /// no grab, roll, drop-through, or release until it expires.</summary>
        public bool IsInShieldStun => _shieldStunTimer > 0f;

        public void StartBlock() {
            _isBlocking = CanRaiseStance;
            if (_isBlocking) _regenTimer = 0f;
        }

        public void EndBlock() {
            _isBlocking = false;
        }

        public BlockResult ResolveHit(in HitPayload hit) {
            if (!_isBlocking || CurrentCharges <= 0 || _owner == null) return BlockResult.NotBlocked;
            // V7.2: boss-only red-telegraph attacks pierce the stance outright.
            if (hit.Unblockable || BlockRules.BypassesBlock(hit.AttackClass)) return BlockResult.NotBlocked;
            if (!BlockRules.IsHitInFront(_owner.GlobalPosition, _owner.IsFacingRight, hit.HitOrigin)) {
                return BlockResult.NotBlocked;
            }

            // V7.2 classification: an authored per-hit charge cost (Guard-Crush
            // = 2) overrides the class default; no single enemy hit ever
            // full-shatters — shatter comes only from chip or 2 + 2 pressure.
            int cost = hit.BlockChargeCost > 0
                ? Mathf.Min(hit.BlockChargeCost, CurrentCharges)
                : BlockRules.ChargeCost(hit.AttackClass, CurrentCharges);
            if (cost <= 0) return BlockResult.NotBlocked;

            CurrentCharges = Mathf.Max(0, CurrentCharges - cost);
            _regenTimer = 0f;
            RaiseChargesChanged();
            if (CurrentCharges > 0) {
                // V7.3 shieldstun: every non-shatter blocked hit locks the
                // stance up for the shared window (the sim mirrors this).
                _shieldStunTimer = ShieldStunSeconds;
                GrantHenrysBastion();
                FTT.Core.EventBus.Instance?.RaiseBlockAbsorbed(_owner.PlayerIndex, CurrentCharges);
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
            _lockoutTimer = 0f;
            _shieldStunTimer = 0f;
            RaiseChargesChanged();
        }

        /// <summary>
        /// Restores every charge without touching the blocking stance — the
        /// Chronal Orb shield-restore pickup's entry point. Writing here keeps
        /// this system authoritative; the PlayerController.CurrentBlockCharges
        /// field is only a display mirror. V7.3 ruling: a shield restore also
        /// ends a running shatter lockout — a full shield with no stance would
        /// read as a bug.
        /// </summary>
        public void RestoreAllCharges() {
            CurrentCharges = Mathf.Max(0, MaxCharges);
            _regenTimer = 0f;
            _lockoutTimer = 0f;
            RaiseChargesChanged();
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
            RaiseChargesChanged();
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
            // V7.3: the shatter arms the five-second lockout — no stance and no
            // regen until it expires (charge #1 lands at shatter + 480f).
            _lockoutTimer = LockoutSeconds;
            _shieldStunTimer = 0f;
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
            // Shieldstun and the lockout share the owner's hitstop suspension:
            // a frozen fighter's timers do not tick (mirrors the sim, where
            // TickCounters is skipped during hitstop).
            if (_owner != null && _owner.IsInHitstop) return;
            // V7.6 Time Freeze suspends passive combat recovery: the design is
            // explicit that "these rules override ordinary live-play
            // regeneration", so a five-second freeze may not hand the player a
            // free shield charge.
            if (_owner != null && _owner.TimeFrozen) return;
            if (_shieldStunTimer > 0f) _shieldStunTimer -= (float)delta;
            // V7.3 shatter lockout: regen is held (interval re-armed) while it
            // runs, so the first charge lands one interval after it expires.
            if (_lockoutTimer > 0f) {
                _lockoutTimer -= (float)delta;
                _regenTimer = 0f;
                return;
            }
            if (_isBlocking || CurrentCharges >= MaxCharges) return;
            // Story-only BlockRecovery minors regenerate charges faster (1f
            // outside Story Mode).
            _regenTimer += (float)delta * (_owner?.StoryBlockRecoveryMultiplier ?? 1f);
            if (_regenTimer >= RegenInterval) {
                _regenTimer -= RegenInterval;
                CurrentCharges = Mathf.Min(CurrentCharges + 1, MaxCharges);
                RaiseChargesChanged();
            }
        }
    }
}
