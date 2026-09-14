using Godot;
using FTT.Core;

namespace FTT.Combat {

    public enum AttackClass {
        Basic,
        Special,
        Ultimate,
        Hazard
    }

    /// <summary>
    /// Complete, mode-independent description of a confirmed hit. Fighter rollback
    /// uses the same fields in its deterministic state boundary; Godot vectors here
    /// are the Story/presentation adapter and are converted at that boundary.
    /// </summary>
    public struct HitPayload {
        public int AttackerIndex;
        public int TargetIndex;
        public string AttackID;
        public string HitboxID;
        public AttackClass AttackClass;
        public float Damage;
        public Vector2 Knockback;
        public float HitstunDuration;
        public Vector2 HitOrigin;
        public bool AttackerFacingRight;
        public StatusType AppliedStatus;
        public float StatusDuration;
        public float StatusIntensity;
        public float ScreenShakeIntensity;
        public float ScreenShakeDuration;
        /// <summary>
        /// V7.2 enemy-attack classification: charges a block spends for this
        /// hit. 0 uses the class default (Basic/Hazard 1, Special full
        /// shatter); Guard-Crush attacks author 2.
        /// </summary>
        public int BlockChargeCost;
        /// <summary>V7.2: boss-only red-telegraph attacks that no block answers.</summary>
        public bool Unblockable;
        /// <summary>V7.3: construct/DoT ticks carry no hitstop — only direct
        /// player-authored hits freeze. The four construct nodes (turret, coil,
        /// nest, snare) set this; the victim's hit handler skips ApplyHitstop.</summary>
        public bool ExemptFromHitstop;
        /// <summary>
        /// V7.6 F07 (Package 11 A1): a caster-owned combo MARK this hit applies,
        /// alongside — and independent of — <see cref="AppliedStatus"/>. A mark
        /// occupies no status slot, causes no action lock, and contributes zero
        /// stagger budget. <see cref="AttackerIndex"/> identifies the owner.
        /// </summary>
        public ComboMarkType ComboMark;
        /// <summary>Mark duration in frames; 0 applies nothing.</summary>
        public int ComboMarkFrames;
        /// <summary>
        /// V7.6 D03d (Package 11 A1b): the PRIMARY hit of a validated paired
        /// grab/throw event. A legal primary throw deals normal damage and
        /// applies its normal launch WITHOUT consuming Temporal Aegis or finite
        /// HP-barrier capacity — no decrement, no absorption or break, no block
        /// perk, and the skipped protection is not treated as partial damage
        /// reduction. Ordinary block is already unreachable for a held victim.
        ///
        /// <para>It is never inferred from a projectile's visual, from a broad
        /// Unblockable flag, or from all hits sharing the attacker's execution:
        /// Story's secondary thrown-mob collision is a separate projectile hit
        /// with normal defenses and must NOT set this.</para>
        /// </summary>
        public bool BypassesFiniteShields;
    }
}
