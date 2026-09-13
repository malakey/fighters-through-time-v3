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
        /// <summary>
        /// V7.6 F07: a caster-owned combo mark this hitbox applies, independent of
        /// <see cref="AppliedStatus"/>. Tesla's finisher sets Conductive.
        /// </summary>
        [Export] public ComboMarkType ComboMark = ComboMarkType.None;
        /// <summary>Mark duration in frames; 0 applies nothing. Never scaled by a status minor.</summary>
        [Export] public int ComboMarkFrames;

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
                ScreenShakeDuration = Mathf.Max(0f, ScreenShakeDuration),
                BlockChargeCost = Mathf.Max(0, BlockChargeCost),
                Unblockable = Unblockable,
                ComboMark = ComboMark,
                ComboMarkFrames = Mathf.Max(0, ComboMarkFrames)
            };
        }

        private void OnAreaEntered(Area2D area) {
            if (!IsActive || area is not Hurtbox hurtbox) return;
            if (hurtbox.OwnerPlayerIndex == OwnerPlayerIndex) return;
            if (IsDiscardedByTimeFreeze(hurtbox)) return;

            // Everything a landed hit cascades into (kills, drop spawns,
            // lethal-hit rewinds, pool releases) runs inside the engine's
            // in/out signal flush here; the guard lets those systems defer the
            // writes the engine would otherwise reject mid-flush.
            using var scope = PhysicsCallbackGuard.Enter();
            HitPayload payload = CreatePayload(hurtbox.OwnerPlayerIndex);
            float damageApplied = hurtbox.TakeHit(payload);
            if (damageApplied > 0f) SourcePlayer?.AddInfluenceFromDamageDealt(damageApplied);
            HitConfirmed?.Invoke(payload, damageApplied);
        }


        /// <summary>
        /// The pure half of the Time Freeze escape-only rule: a PLAYER-sourced
        /// hit (attacker index &gt;= 0) on a NON-player target (index &lt; 0) is
        /// discarded while the world is frozen. Enemy-sourced hits and
        /// player-versus-player hits are untouched.
        /// </summary>
        public static bool PlayerHitIsDiscardedWhileFrozen(
            int attackerIndex, int targetIndex, bool worldFrozen) =>
            worldFrozen && attackerIndex >= 0 && targetIndex < 0;


        /// <summary>
        /// V7.6 Time Freeze escape-only guarantee: while the Story world is
        /// frozen, enemies and bosses are invulnerable — including against
        /// already-live player projectiles, constructs, zones and DoT. The hit is
        /// <b>discarded</b> here at the single shared chokepoint, before
        /// <c>Hurtbox.TakeHit</c>, so nothing downstream can pay out damage,
        /// stagger, Rally echo or meter; "attacks are discarded, never queued".
        ///
        /// <para>Enemy-sourced hits on the player are untouched: the freeze stops
        /// enemies from producing them in the first place, and a frozen hazard
        /// that somehow reaches here should still be inert, which it is — its
        /// tick, and therefore its activation, is frozen.</para>
        ///
        /// <para>This runs inside the engine's in/out signal flush, so the read is
        /// strictly side-effect-free: a group lookup and two property reads.</para>
        /// </summary>
        private bool IsDiscardedByTimeFreeze(Hurtbox hurtbox) {
            if (!PlayerHitIsDiscardedWhileFrozen(OwnerPlayerIndex, hurtbox.OwnerPlayerIndex, worldFrozen: true)) {
                return false;
            }
            if (!IsInsideTree()) return false;
            return GetTree()?.GetFirstNodeInGroup(
                    FTT.Environment.TimeFreezeController.ControllerGroup)
                is FTT.Environment.TimeFreezeController controller
                && IsInstanceValid(controller)
                && controller.IsFrozen;
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
