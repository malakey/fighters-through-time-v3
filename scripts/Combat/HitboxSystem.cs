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
    }
}
