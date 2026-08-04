using Godot;
using System;
using FTT.Characters;
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

    public partial class Hitbox : Area2D {
        [ExportGroup("Identity")]
        [Export] public string AttackID = "";
        [Export] public string HitboxID = "primary";
        [Export] public AttackClass AttackClass = AttackClass.Basic;
        [Export] public int OwnerPlayerIndex = -1;

        [ExportGroup("Hit")]
        [Export] public float Damage = 10f;
        [Export] public Vector2 KnockbackForce = new(3f, -2f);
        [Export] public float HitstunDuration = 0.2f;
        [Export] public StatusType AppliedStatus = StatusType.None;
        [Export] public float StatusDuration;
        [Export] public float StatusIntensity = 1f;
        [Export] public float ScreenShakeIntensity = 0.2f;
        [Export] public float ScreenShakeDuration = 0.1f;

        [ExportGroup("Runtime")]
        [Export] public bool IsActive;

        public PlayerController SourcePlayer { get; set; }

        public void Activate() {
            IsActive = true;
            Monitoring = true;
        }

        public void Deactivate() {
            IsActive = false;
            Monitoring = false;
        }

        public override void _Ready() {
            SourcePlayer ??= FindOwningPlayer();
            Deactivate();
            AreaEntered += OnAreaEntered;
        }

        public override void _ExitTree() {
            AreaEntered -= OnAreaEntered;
        }

        public HitPayload CreatePayload(int targetIndex) {
            bool facingRight = SourcePlayer?.IsFacingRight ?? KnockbackForce.X >= 0f;
            return new HitPayload {
                AttackerIndex = OwnerPlayerIndex,
                TargetIndex = targetIndex,
                AttackID = AttackID ?? "",
                HitboxID = HitboxID ?? "primary",
                AttackClass = AttackClass,
                Damage = Mathf.Max(0f, Damage),
                Knockback = KnockbackForce,
                HitstunDuration = Mathf.Max(0f, HitstunDuration),
                HitOrigin = GlobalPosition,
                AttackerFacingRight = facingRight,
                AppliedStatus = AppliedStatus,
                StatusDuration = Mathf.Max(0f, StatusDuration),
                StatusIntensity = StatusIntensity <= 0f ? 1f : StatusIntensity,
                ScreenShakeIntensity = Mathf.Max(0f, ScreenShakeIntensity),
                ScreenShakeDuration = Mathf.Max(0f, ScreenShakeDuration)
            };
        }

        private void OnAreaEntered(Area2D area) {
            if (!IsActive || area is not Hurtbox hurtbox) return;
            if (hurtbox.OwnerPlayerIndex == OwnerPlayerIndex) return;

            float damageApplied = hurtbox.TakeHit(CreatePayload(hurtbox.OwnerPlayerIndex));
            if (damageApplied > 0f) SourcePlayer?.AddInfluenceFromDamageDealt(damageApplied);
        }

        private PlayerController FindOwningPlayer() {
            Node current = GetParent();
            while (current != null) {
                if (current is PlayerController player) return player;
                current = current.GetParent();
            }
            return null;
        }
    }

    public partial class Hurtbox : Area2D {
        [Export] public int OwnerPlayerIndex = -1;

        /// <summary>
        /// The single adjacent combat receiver returns actual HP damage dealt. That
        /// result drives Influence gain, so blocked or invulnerable hits grant none.
        /// </summary>
        public event Func<HitPayload, float> OnHit;

        public float TakeHit(HitPayload payload) {
            payload.TargetIndex = OwnerPlayerIndex;
            return OnHit?.Invoke(payload) ?? 0f;
        }
    }
}
