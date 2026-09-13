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
        /// <summary>V7.2 classification: charges a block spends (0 = class default; Guard-Crush = 2).</summary>
        [Export] public int BlockChargeCost;
        /// <summary>V7.2: boss-only red-telegraph attacks no block answers.</summary>
        [Export] public bool Unblockable;

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
            this.SetMonitoringSafe(true);
        }

        public void Deactivate() {
            IsActive = false;
            this.SetMonitoringSafe(false);
        }

        // Pooled owners re-enter the tree on every spawn/release cycle but _Ready
        // runs once, so the hit-delivery connection lives on the enter/exit pair
        // (audit C-1: a _Ready-only connect left warmed and recycled hitboxes
        // permanently disconnected after their first reparent).
        public override void _EnterTree() {
            if (_areaEnteredConnected) return;
            AreaEntered += OnAreaEntered;
            _areaEnteredConnected = true;
        }

        public override void _Ready() {
            SourcePlayer ??= FindOwningPlayer();
            Deactivate();
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
                // Package 11 A4: the V7.6 ability-scoped AbilityDamage lane
                // applies to every hit an ability authors, keyed by its
                // AttackID (which is the AbilityID everywhere in the kit code).
                // Neutral 1.0 for basics, enemies and Fighter Mode.
                Damage = Mathf.Max(0f, Damage)
                    * (SourcePlayer?.StoryTemporaryDamageMultiplier ?? 1f)
                    * (SourcePlayer?.StoryScoped("AbilityDamage", AttackID ?? "") ?? 1f),
                Knockback = KnockbackForce * (SourcePlayer?.StoryKnockbackMultiplier ?? 1f),
                HitstunDuration = Mathf.Max(0f, HitstunDuration),
                HitOrigin = GlobalPosition,
                AttackerFacingRight = facingRight,
                AppliedStatus = AppliedStatus,
                StatusDuration = Mathf.Max(0f, StatusDuration)
                    * (SourcePlayer?.StoryStatusDurationMultiplier ?? 1f),
                StatusIntensity = (StatusIntensity <= 0f ? 1f : StatusIntensity)
                    * (damagingStatus ? SourcePlayer?.StoryStatusIntensityMultiplier ?? 1f : 1f)
                    * ResolveScopedStatusIntensity(),
                ScreenShakeIntensity = Mathf.Max(0f, ScreenShakeIntensity),
                ScreenShakeDuration = Mathf.Max(0f, ScreenShakeDuration),
                BlockChargeCost = Mathf.Max(0, BlockChargeCost),
                Unblockable = Unblockable
            };
        }

        /// <summary>
        /// Package 11 A4 (Resonance V7.6). Cleopatra's two Venom minors are
        /// ability-SCOPED, so they cannot ride the character-wide
        /// StatusIntensity lane. Minor Asp Mark scales only the basic
        /// finisher's Venom mark (0.5 -> 0.75 at +50%); Minor Venom Damage
        /// scales every Venom tick. BasicComboRules is cross-mode and stays
        /// read-only - the Story multiplier is applied HERE, at the hit
        /// application site, exactly as the plan requires.
        /// </summary>
        private float ResolveScopedStatusIntensity() {
            if (SourcePlayer == null || AppliedStatus != FTT.Core.StatusType.Venom) return 1f;
            float scale = SourcePlayer.StoryScoped("AbilityDamage", "venom");
            if ((HitboxID ?? "") == "combo_3") {
                scale *= SourcePlayer.StoryScoped("AbilityDamage", "finisher_venom");
            }
            return scale;
        }

        private void OnAreaEntered(Area2D area) {
            if (!IsActive || area is not Hurtbox hurtbox) return;
            if (hurtbox.OwnerPlayerIndex == OwnerPlayerIndex) return;

            // Everything a landed hit cascades into (kills, drop spawns,
            // lethal-hit rewinds, pool releases) runs inside the engine's
            // in/out signal flush here; the guard lets those systems defer the
            // writes the engine would otherwise reject mid-flush.
            using var scope = PhysicsCallbackGuard.Enter();
            HitPayload payload = CreatePayload(hurtbox.OwnerPlayerIndex);
            float damageApplied = hurtbox.TakeHit(payload);
            if (damageApplied > 0f) SourcePlayer?.AddInfluenceFromDamageDealt(damageApplied);
            // Package 11 A4: the shared Story "a hit of mine landed" hook the
            // V7.6 traversal flags read (Joan's Wings Refresh). Runs for every
            // Hitbox-delivered hit - melee, special and pooled projectile -
            // which is exactly the design's "a direct Hit 3 or Righteous Smite
            // hit" surface without a second chokepoint.
            if (damageApplied > 0f) SourcePlayer?.NotifyStoryHitLanded(payload);
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
