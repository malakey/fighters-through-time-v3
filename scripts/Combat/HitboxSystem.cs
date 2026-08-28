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
    }
}
