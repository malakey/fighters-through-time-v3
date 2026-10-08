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
        /// <summary>A01 (Package 13 W1): a Shield-Breaker Special — a valid block spends every charge.</summary>
        [Export] public bool ShieldBreaker;
        /// <summary>
        /// V7.6 F07: a caster-owned combo mark this hitbox applies, independent of
        /// <see cref="AppliedStatus"/>. Tesla's finisher sets Conductive.
        /// </summary>
        [Export] public ComboMarkType ComboMark = ComboMarkType.None;
        /// <summary>Mark duration in frames; 0 applies nothing. Never scaled by a status minor.</summary>
        [Export] public int ComboMarkFrames;

        [ExportGroup("Hit contract (M08)")]
        /// <summary>M05 launch flag carried into the payload (data only until W3b).</summary>
        [Export] public bool Launches;
        /// <summary>Delivery channel carried into the payload.</summary>
        [Export] public HitDelivery Delivery = HitDelivery.DirectHit;
        /// <summary>
        /// Origin carried into the payload. Its <see cref="HitOrigin.Ultimate"/>
        /// value is what denies the caster damage-dealt meter (D03h). Every site
        /// that configures a hitbox from an <see cref="AbilityData"/> copies it
        /// through <see cref="ApplyAbilityHitContract"/>.
        /// </summary>
        [Export] public HitOrigin Origin = HitOrigin.Basic;

        [ExportGroup("Runtime")]
        [Export] public bool IsActive;

        public PlayerController SourcePlayer { get; set; }

        /// <summary>
        /// F7 (2026-10-04 feel pass, provisional): stamped by the enemy runtime
        /// on hitboxes a Standard-tier mob owns (its attack hitbox and its
        /// pooled projectiles), copied into <see cref="HitPayload.SourceIsStandardMob"/>.
        /// Story-only — the Fighter simulation never reads it.
        /// </summary>
        public bool SourceIsStandardMob { get; set; }

        /// <summary>
        /// 2026-10-04 fix pass (M1), Story-only: when true, a contact with an
        /// enemy or boss hurtbox lands only if the segment between the hurtbox's
        /// centre and <see cref="SourcePlayer"/>'s body centre is clear of
        /// Environment (<see cref="EnvironmentProbe"/>) — one point query and one
        /// ray per new contact, cast from the target back to the swinger (see
        /// <see cref="IsBlockedByEnvironment"/> for why). Set on the player's
        /// <c>MeleeHitbox</c> (basics and the directional strikes) by
        /// <c>CharacterFactory</c>: an Area2D ignores walls, so a swing used to
        /// land on a boss standing on the far side of a 20 px wall. Projectiles
        /// stop on terrain themselves; zones, constructs and kit shape queries
        /// are not covered. The Fighter simulation never reads it (Fighter hits
        /// are resolved in fixed point, not by Area2D).
        /// </summary>
        public bool RequiresEnvironmentLineOfSight { get; set; }

        /// <summary>
        /// 2026-10-04 fix pass (R12): the <see cref="BaseSpecial.ExecutionSerial"/>
        /// of the cast that fired this hitbox, copied into
        /// <see cref="HitPayload.SourceExecutionSerial"/>, so a pooled shot that
        /// lands after its caster started the next cast claims its OWN cast's
        /// once-per-execution caster freeze. 0 = the execution current at landing
        /// (every hitbox that lives inside its cast). Reset by pooled owners.
        /// </summary>
        public int SourceExecutionSerial { get; set; }
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
                Unblockable = Unblockable,
                ShieldBreaker = ShieldBreaker && AttackClass == AttackClass.Special,
                ComboMark = ComboMark,
                ComboMarkFrames = Mathf.Max(0, ComboMarkFrames),
                SourceActorId = SourcePlayer != null && IsInstanceValid(SourcePlayer)
                    ? SourcePlayer.GetInstanceId()
                    : 0UL,
                Launches = Launches,
                Delivery = Delivery,
                Origin = Origin,
                SourceIsStandardMob = SourceIsStandardMob,
                SourceExecutionSerial = SourceExecutionSerial,
                // Package 13 W7a (L03): a construct's pooled bolt carries no
                // hitstop, the V7.3 construct rule every direct construct
                // payload already stamps.
                ExemptFromHitstop = Delivery == HitDelivery.Construct
            };
        }

        /// <summary>The placeholder and enemy projectile paths both tag their hitbox "projectile".</summary>
        public bool IsProjectileHitbox => HitboxID == "projectile";

        /// <summary>
        /// M08 (Package 12 W3): copies an ability's authored hit contract onto
        /// this hitbox — the attack class, block cost, hitstun, launch flag,
        /// delivery and origin. The one place a hitbox reads them from
        /// <see cref="AbilityData"/>, so BaseSpecial, the pooled placeholder
        /// projectile and the factory's generic special cannot drift apart.
        /// </summary>
        public void ApplyAbilityHitContract(AbilityData data) {
            if (data == null) return;
            AttackClass = data.ResolvedAttackClass;
            BlockChargeCost = HitClassification.BlockChargeCostFor(data.BlockClass);
            Unblockable = data.BlockClass == BlockClass.Unblockable
                && data.Origin != HitOrigin.Ultimate;
            ShieldBreaker = HitClassification.IsShieldBreaker(data.BlockClass);
            HitstunDuration = data.HitstunDuration;
            Launches = data.Launches;
            Delivery = data.Delivery;
            Origin = data.Origin;
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
            if (IsDiscardedByTimeFreeze(hurtbox)) return;
            // Package 12 W1 (R03 / GAP-06): the Post-Landing Hold and a boss's
            // T01b suspension discard every contact — no HitConfirmed, no meter,
            // no Rally. Hurtbox.TakeHit carries the same gate for the kit's
            // shape-query deliveries that never pass through a Hitbox.
            if (IsDiscardedByWorldHold()) return;
            // Package 12 W4 (Tesla kit rule): a projectile PASSES THROUGH a
            // fighter whose movement ability is in its pass-through window (the
            // Lightning Blink translation). No contact at all — no HitConfirmed,
            // so the shot is not consumed and flies on.
            if (IsProjectileHitbox && hurtbox.GetParent() is PlayerController target
                && target.PassesThroughProjectiles) return;
            // M1 (2026-10-04 fix pass): a player's swing does not reach through a
            // wall. Read before the callback guard is entered — the probe
            // answers "clear" inside the guard — and side-effect-free: one point
            // query and one ray.
            if (IsBlockedByEnvironment(hurtbox)) return;

            // Everything a landed hit cascades into (kills, drop spawns,
            // lethal-hit rewinds, pool releases) runs inside the engine's
            // in/out signal flush here; the guard lets those systems defer the
            // writes the engine would otherwise reject mid-flush.
            using var scope = PhysicsCallbackGuard.Enter();
            HitPayload payload = CreatePayload(hurtbox.OwnerPlayerIndex);
            // M08: one identity per confirmed contact.
            payload.ContactId = HitClassification.NextContactId();
            float damageApplied = hurtbox.TakeDamage(in payload);
            // V7.6 D03h (Package 11 A1b), M08 (Package 12 W3): the shared
            // strike path is the one place every Ultimate hitbox lands, and the
            // meter rule now reads the payload's AUTHORED origin rather than
            // inferring it from the attack class. An Ultimate earns its caster
            // zero damage-dealt meter; the Rally reclaim follows the authored
            // delivery (a direct hit reclaims, D03g).
            if (damageApplied > 0f) {
                SourcePlayer?.AddInfluenceFromDamageDealt(
                    damageApplied,
                    collectsEcho: HitClassification.CollectsEcho(payload.Delivery),
                    ultimateOrigin: HitClassification.IsUltimateOrigin(payload.Origin));
            }
            // Package 11 A4: the shared Story "a hit of mine landed" hook the
            // V7.6 traversal flags read (Joan's Wings Refresh). Runs for every
            // Hitbox-delivered hit - melee, special and pooled projectile -
            // which is exactly the design's "a direct Hit 3 or Righteous Smite
            // hit" surface without a second chokepoint.
            if (damageApplied > 0f) {
                SourcePlayer?.NotifyStoryHitLanded(payload);
                // 2026-10-04 feel pass (F5): an ability-authored hit gets its
                // impact VFX, HitConfirm and caster freeze at the struck hurtbox.
                SourcePlayer?.ConfirmAbilityHitboxHit(payload, damageApplied, hurtbox.GlobalPosition);
            }
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

        /// <summary>
        /// The shared <see cref="FTT.Environment.ChronalRewindManager.IsWorldHeld"/>
        /// query. Side-effect-free, so it is safe inside the signal flush.
        /// </summary>
        private bool IsDiscardedByWorldHold() =>
            IsInsideTree() && FTT.Environment.ChronalRewindManager.IsWorldHeld(GetTree());

        /// <summary>
        /// M1: true when <see cref="RequiresEnvironmentLineOfSight"/> is set, the
        /// target is an enemy or a boss, and Environment geometry lies between the
        /// hurtbox's centre and the source player's body centre. Checkpoint
        /// fractures, extractors and other strike surfaces are never gated (a
        /// breakable wall's own body would otherwise block its own strike).
        ///
        /// <para>The ray is cast FROM the target back to the swinger, and that
        /// direction is load-bearing: a ray ignores a shape it starts inside, so
        /// a wall-phasing enemy caught mid-wall (the Rift Phantom, whose body
        /// mask drops Environment while it chases or attacks) is not sheltered
        /// by the wall it is passing through. That is the same answer its own
        /// swing gets — the P2 wall clip
        /// (<c>EnemyAbilityExecutor.NearestBlockingWall</c>) is cast from the
        /// enemy's centre outward and likewise never sees the wall it starts
        /// in. Any other wall between the two still blocks, so a phantom whose
        /// centre is past a wall's far face is behind it, both ways. The ray
        /// skips the whole BODY the target's centre is inside
        /// (<c>ignoreBodiesAtOrigin</c>), not only the one convex shape a ray
        /// ignores by itself, so a multi-shape or concave wall does not shelter
        /// the phantom either.</para>
        /// </summary>
        private bool IsBlockedByEnvironment(Hurtbox hurtbox) {
            if (!RequiresEnvironmentLineOfSight) return false;
            PlayerController source = SourcePlayer;
            if (source == null || !IsInstanceValid(source)) return false;
            if (hurtbox.GetParent() is not (FTT.Enemies.EnemyController or FTT.Enemies.BossController)) return false;
            Vector2 target = hurtbox.GetNodeOrNull<CollisionShape2D>("CollisionShape2D")?.GlobalPosition
                ?? hurtbox.GlobalPosition;
            return !EnvironmentProbe.IsClear(source, target, source.BodyCentreGlobalPosition,
                ignoreBodiesAtOrigin: true);
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
