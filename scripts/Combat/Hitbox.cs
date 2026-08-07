using Godot;
using System;
using FTT.Characters;
using FTT.Core;

namespace FTT.Combat {

    /// <summary>
    /// Query-only attack volume. Lives in its own file so authored scenes can
    /// attach it directly (Godot C# resolves a script class by file name).
    /// </summary>
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
        private bool _areaEnteredConnected;

        /// <summary>
        /// Raised after a hurtbox accepts a payload from this hitbox. The float is
        /// the actual HP damage dealt (0 when blocked/invulnerable). Projectiles
        /// use this to detonate on first confirmed contact.
        /// </summary>
        public event Action<HitPayload, float> HitConfirmed;

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
            _areaEnteredConnected = true;
        }

        public override void _ExitTree() {
            if (_areaEnteredConnected) {
                AreaEntered -= OnAreaEntered;
                _areaEnteredConnected = false;
            }
        }

        public HitPayload CreatePayload(int targetIndex) {
            bool facingRight = SourcePlayer?.IsFacingRight ?? KnockbackForce.X >= 0f;
            // Story-only Resonance minors (KnockbackForce, StatusDuration,
            // StatusDamage); every multiplier is neutral 1f outside Story Mode.
            bool damagingStatus = AppliedStatus is StatusType.Venom or StatusType.RadiantBurn;
            return new HitPayload {
                AttackerIndex = OwnerPlayerIndex,
                TargetIndex = targetIndex,
                AttackID = AttackID ?? "",
                HitboxID = HitboxID ?? "primary",
                AttackClass = AttackClass,
                Damage = Mathf.Max(0f, Damage) * (SourcePlayer?.StoryTemporaryDamageMultiplier ?? 1f),
                Knockback = KnockbackForce * (SourcePlayer?.StoryKnockbackMultiplier ?? 1f),
                HitstunDuration = Mathf.Max(0f, HitstunDuration),
                HitOrigin = GlobalPosition,
                AttackerFacingRight = facingRight,
                AppliedStatus = AppliedStatus,
                StatusDuration = Mathf.Max(0f, StatusDuration)
                    * (SourcePlayer?.StoryStatusDurationMultiplier ?? 1f),
                StatusIntensity = (StatusIntensity <= 0f ? 1f : StatusIntensity)
                    * (damagingStatus ? SourcePlayer?.StoryStatusIntensityMultiplier ?? 1f : 1f),
                ScreenShakeIntensity = Mathf.Max(0f, ScreenShakeIntensity),
                ScreenShakeDuration = Mathf.Max(0f, ScreenShakeDuration)
            };
        }

        private void OnAreaEntered(Area2D area) {
            if (!IsActive || area is not Hurtbox hurtbox) return;
            if (hurtbox.OwnerPlayerIndex == OwnerPlayerIndex) return;

            HitPayload payload = CreatePayload(hurtbox.OwnerPlayerIndex);
            float damageApplied = hurtbox.TakeHit(payload);
            if (damageApplied > 0f) SourcePlayer?.AddInfluenceFromDamageDealt(damageApplied);
            HitConfirmed?.Invoke(payload, damageApplied);
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
}
