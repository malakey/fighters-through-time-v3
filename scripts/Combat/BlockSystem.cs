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
                GrantHenrysBastion(++_blockGrantEventId);
                FTT.Core.EventBus.Instance?.RaiseBlockAbsorbed(_owner.PlayerIndex, CurrentCharges);
                return BlockResult.Blocked;
            }

            BreakGuard(hit.HitOrigin.X);
            return BlockResult.GuardBroken;
        }

        /// <summary>Story-only Resonance perk key: a successful block summons a phantom shield guard.</summary>
        public const string HenrysBastionPerkKey = "henrys_bastion";
        private const float HenrysBastionCapacityShare = 0.10f;

        /// <summary>
        /// Henry's Bastion (Story-only): a qualifying REAL block summons a
        /// phantom royal shield guard absorbing up to 10% of Shakespeare's
        /// maximum HP.
        ///
        /// <para><b>V7.6 D01/D02a/D02c (Package 11 A1b).</b> It is granted only
        /// AFTER that block resolves and cannot absorb its own triggering hit —
        /// and because D01 resolves an existing barrier BEFORE block, a hit the
        /// guard absorbs in full never reaches <see cref="ResolveHit"/> and can
        /// therefore never summon or refresh the guard through a false block
        /// event. The guard now carries a real 8-second (480 active tick)
        /// lifetime instead of the old "lapses by absorbing damage" approximation.
        /// A repeat grant refills capacity and restarts the timer; it never adds
        /// either. <paramref name="grantEventId"/> is the D02a grant identity, so
        /// a duplicate callback for the same block contact grants nothing.</para>
        /// </summary>
        private void GrantHenrysBastion(int grantEventId) {
            if (_owner == null || !_owner.HasStoryPerk(HenrysBastionPerkKey)) return;
            _owner.GrantStoryShield(
                StoryShieldEffect.HenrysBastion,
                HenrysBastionCapacityShare * _owner.MaximumHP,
                StoryDefenseRules.GrantedShieldLifetimeFrames,
                grantEventId);
        }

        /// <summary>
        /// D02a grant identity for a block contact: monotonically increasing per
        /// resolved block, so a duplicate animation or collision callback for the
        /// same contact reuses the same value and is refused.
        /// </summary>
        private int _blockGrantEventId;

        /// <summary>
        /// Package 11 A4 (Shield of Orleans, V7.6 F06 Option A). Hands back
        /// shield charges without touching the stance, the shieldstun window,
        /// a running daze or the shatter lockout - Joan's Guard-Crush refund
        /// runs AFTER normal consumption and after any shatter, so a refunded
        /// charge still sits behind <see cref="CanRaiseStance"/>'s lockout
        /// check and is deliberately unusable until that lockout ends. Clamped
        /// to <paramref name="cap"/> (never above <see cref="MaxCharges"/>).
        /// </summary>
        public void RefundCharges(int count, int cap) {
            if (count <= 0) return;
            int ceiling = Mathf.Min(Mathf.Max(0, cap), Mathf.Max(0, MaxCharges));
            int refunded = Mathf.Min(ceiling, CurrentCharges + count);
            if (refunded == CurrentCharges) return;
            CurrentCharges = refunded;
            RaiseChargesChanged();
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
        /// field is only a display mirror.
        ///
        /// <para><b>V7.6 F17 (Package 11 A1b).</b> A charge restore does NOT end
        /// a running shatter lockout, and does not change the regeneration
        /// countdown's relationship to it. The V7.3 sentence "a Chronal
        /// Shield-Restore orb ends the lockout along with restoring charges (the
        /// orb is the authored fast exit)" is <b>deleted</b>: the only specified
        /// block-recovery rules are normal regeneration and the existing perk
        /// exceptions — Shield of Orléans can refund one charge after a
        /// Guard-Crush shatter, but that charge stays unusable until the five
        /// second lockout ends. Temporal Aegis is a separate one-hit shield and
        /// restores nothing here at all.</para>
        /// </summary>
        public void RestoreAllCharges() {
            CurrentCharges = Mathf.Max(0, MaxCharges);
            _regenTimer = 0f;
            RaiseChargesChanged();
        }

        /// <summary>
        /// Spends a fixed number of charges outside the per-class cost table,
        /// breaking the guard when the last charge goes.
        ///
        /// <para><b>V7.6 F15 (Package 11 A1b).</b> This is no longer a combat
        /// rule. It used to implement the retired two-charge "shield-stutter"
        /// exception for Lincoln's Emancipator and Joan's Divine Piercing, which
        /// are now ordinary Special-class FULL shatters resolved through
        /// <see cref="ResolveHit"/> like every other Special — both call sites
        /// are deleted. What remains is a plain charge-spend utility for
        /// scripted and test setup, and it deliberately no longer fires the
        /// Guard Impact haptic, because nothing routed through it is a
        /// "successful block" any more.</para>
        /// </summary>
        public BlockResult DepleteCharges(int count) {
            if (CurrentCharges <= 0 || count <= 0) return BlockResult.NotBlocked;
            CurrentCharges = Mathf.Max(0, CurrentCharges - count);
            _regenTimer = 0f;
            RaiseChargesChanged();
            if (CurrentCharges > 0) return BlockResult.Blocked;

            BreakGuard();
            return BlockResult.GuardBroken;
        }

        /// <summary>Story-only Resonance perk key: teleport backward on guard break.</summary>
        public const string QuantumEntanglementPerkKey = "quantum_entanglement";
        private const float QuantumEntanglementDistance = 150f;

        /// <summary>
        /// The shatter. <paramref name="attackerX"/> is the attacking side's X
        /// (the contact origin — a successful block needs it in front of the
        /// defender); null when there is no attacker (scripted
        /// <see cref="DepleteCharges"/>), which falls back to "backward from
        /// facing".
        /// </summary>
        private void BreakGuard(float? attackerX = null) {
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
                // Low-item decision 2026-09-26 (Package 12 W3): the fixed
                // (2.0, -1.0) Y-down push, X AWAY FROM THE ATTACKER, unscaled
                // (no weight, no low-HP scaling) and ASSIGNED — the shatter
                // replaces the defender's velocity instead of adding to it. It
                // is not a launch: no DI, tumble or tech. The sim mirrors it in
                // FighterDamageRules.ApplyFighterHit's shatter branch.
                _owner.Velocity = GuardBreakPushVelocity(
                    _owner.GlobalPosition.X, attackerX ?? _owner.GlobalPosition.X, _owner.IsFacingRight);
            }
            FTT.Core.EventBus.Instance?.RaiseBlockBroken(_owner.PlayerIndex);
        }

        /// <summary>
        /// The Story pixel-space guard-break push: the shared
        /// <see cref="BasicComboRules.GuardBreakPushX"/> /
        /// <see cref="BasicComboRules.GuardBreakPushYDown"/> units × 60, signed
        /// away from the attacker by <see cref="BasicComboRules.GuardBreakPushSign"/>.
        /// </summary>
        public static Vector2 GuardBreakPushVelocity(float defenderX, float attackerX, bool defenderFacingRight) =>
            new(BasicComboRules.GuardBreakPushSign(defenderX, attackerX, defenderFacingRight)
                    * BasicComboRules.GuardBreakPushX * 60f,
                BasicComboRules.GuardBreakPushYDown * 60f);

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
