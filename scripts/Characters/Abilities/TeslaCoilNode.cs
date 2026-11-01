using Godot;
using FTT.Combat;
using FTT.Characters;
using FTT.Core;

namespace FTT.Characters.Abilities {

    /// <summary>
    /// Story-mode Tesla Coil construct (design Section 4 specification): 25 HP,
    /// 30 s lifespan, max 2 active per owner. Fires a 5 HP electrical arc at the
    /// nearest enemy in range every 2 s. Two coils within 8 world units (480 px)
    /// link into an alternating-current fence dealing 8 HP per 0.5 s tick and
    /// applying a brief StaticCharge to enemies caught between them. The coil is
    /// damageable/destroyable, persists across owner death, freezes during
    /// Chronal Rewind, and fully resets its pooled state.
    /// </summary>
    public partial class TeslaCoilNode : FTT.Core.PooledNode, FTT.Core.IPoolable, FTT.Environment.IStoryRewindSimulation, FTT.Environment.IStoryTimeFreezable {

        public const float LinkRangePixels = 480f;   // 8 world units at 60 px/unit.
        /// <summary>T02 (Package 13 W7a): coils arc at targets within 4 units; mirrors the Fighter sim AttackRange.</summary>
        public const float ArcRangePixels = KitMotionRules.TeslaCoilArcRangeUnits * KitMotionRules.StoryPixelsPerUnit;
        private const int MaxCoilHP = 25;
        // 2026-08-11 construct rebalance: fence cadence halved and damage halved
        // alongside the .tres arc retune; construct hits carry no knockback.
        private const float FenceTickInterval = 1.0f;
        private const float FenceDamage = 4f;
        private const float FenceHalfHeightPixels = 60f;
        private const float FenceStaticChargeDuration = 1.0f;

        public int OwnerIndex { get; private set; }
        public float ArcDamage { get; private set; }
        /// <summary>Seconds the coil still has to stand. Test seam.</summary>
        public float LifetimeRemaining => _lifetime;
        public bool IsCoilDestroyed { get; private set; }

        /// <summary>
        /// Lingering-kit pass: synced-strike charges left (plan 1.4). Only a synced strike and the Lorentz
        /// discharge spend them; the coil's own arc and the fence never do. At 0 the coil fizzles through the
        /// ordinary destroy path. Reset on every <see cref="Initialize"/> and <see cref="OnDespawn"/> (pooled);
        /// untouched by Time Freeze and the death rewind (constructs are not rewound).
        /// </summary>
        public int Charges { get; private set; }
        public int MaxCharges => LingeringKitRules.CoilCharges;

        private PlayerController _ownerPlayer;
        private AbilityData _data;
        private int _currentHP;
        private float _lifetime;
        private float _arcInterval;
        private float _arcTimer;
        private float _fenceTimer;
        private TeslaCoilNode _partner;
        private bool _drivesFence;
        private bool _rewindFrozen;
        private Hurtbox _hurtbox;
        private bool _hurtboxBound;
        private ProgressBar _hpBar;
        // Story-only Resonance minors captured at deploy time (1f in Fighter Mode).
        private float _rangeMultiplier = 1f;
        private bool _resonantOverdrive;
        private float _statusDurationMultiplier = 1f;
        private PlayerController _subscribedOwner;
        private ConstructChargePips _pips;

        // Pooled constructs re-enter the tree on every spawn cycle but _Ready runs
        // once, so the hurtbox subscription lives on the enter/exit pair (audit
        // H-3: a _Ready-only bind left the warmed coil indestructible after its
        // first reparent).
        public override void _EnterTree() {
            _hurtbox ??= GetNodeOrNull<Hurtbox>("Hurtbox");
            if (_hurtbox == null || _hurtboxBound) return;
            _hurtbox.OnHit += OnCoilHit;
            _hurtboxBound = true;
        }

        public override void _ExitTree() {
            UnsubscribeFromOwner();
            if (!_hurtboxBound) return;
            _hurtboxBound = false;
            if (_hurtbox != null) _hurtbox.OnHit -= OnCoilHit;
        }

        public void Initialize(AbilityData data, PlayerController owner, bool resonantOverdrive) {
            _data = data;
            _ownerPlayer = owner;
            OwnerIndex = owner?.PlayerIndex ?? 0;
            ArcDamage = data?.BaseDamage ?? 5f;
            _currentHP = MaxCoilHP;
            IsCoilDestroyed = false;
            // Story-only Resonance minors: PersistentDuration extends the coil's
            // lifespan, PersistentRange widens arc/link reach, StatusDuration
            // lengthens the fence's StaticCharge. All 1f outside Story Mode.
            // Package 11 A4: V7.6 re-scopes the coil-lifespan minor from the
            // character-wide PersistentDuration lane onto
            // AbilityDuration(tesla_coil) - 30 s to 40 s when bought.
            _lifetime = (data?.Lifetime > 0f ? data.Lifetime : 30f)
                * (owner?.StoryPersistentDurationMultiplier ?? 1f)
                * (owner?.StoryScoped("AbilityDuration", "tesla_coil") ?? 1f);
            _rangeMultiplier = owner?.StoryPersistentRangeMultiplier ?? 1f;
            _statusDurationMultiplier = owner?.StoryStatusDurationMultiplier ?? 1f;
            _arcInterval = (data?.DamageTickIntervalFrames ?? 120) / 60f;
            if (_arcInterval <= 0f) _arcInterval = 2f;
            // Resonant Overdrive (Story-only Resonance major perk). V7.6: the
            // "+5 s coil duration" clause MOVED to Minor Coil Duration, so the
            // Major now grants the 25% faster arcs and Extractor targeting
            // only - never both halves of the old effect.
            _resonantOverdrive = resonantOverdrive;
            if (resonantOverdrive) {
                _arcInterval /= 1.25f;
            }
            _arcTimer = _arcInterval;
            _fenceTimer = FenceTickInterval;
            _partner = null;
            _drivesFence = false;
            Charges = MaxCharges;
            SubscribeToOwner(owner);
            UpdateHPBar();
            UpdatePips();
        }

        private void UpdatePips() {
            _pips ??= GetNodeOrNull<ConstructChargePips>("ChargePips");
            _pips?.SetCharges(Charges, MaxCharges);
            if (_pips != null) _pips.Visible = !IsCoilDestroyed;
        }

        private void SubscribeToOwner(PlayerController owner) {
            UnsubscribeFromOwner();
            if (owner == null) return;
            _subscribedOwner = owner;
            owner.OwnerHitLanded += OnOwnerHitLanded;
        }

        private void UnsubscribeFromOwner() {
            if (_subscribedOwner != null && IsInstanceValid(_subscribedOwner)) {
                _subscribedOwner.OwnerHitLanded -= OnOwnerHitLanded;
            }
            _subscribedOwner = null;
        }

        /// <summary>
        /// Plan 1.3: the owner's landed basic hit or direct Special hit on a target inside this coil's
        /// 4-unit reach (a 2D circle) fires one synced arc and spends one charge. Two coils in reach both
        /// fire. A strike, a tick, a construct hit or the Pulse never reaches this (the
        /// <see cref="OwnerHitLandedInfo.TriggersSyncedStrike"/> filter), so there is no loop.
        /// </summary>
        private void OnOwnerHitLanded(OwnerHitLandedInfo info) {
            if (_rewindFrozen || IsCoilDestroyed || Charges <= 0 || !info.TriggersSyncedStrike) return;
            if (info.Target == null || !IsInstanceValid(info.Target)) return;
            if (!ConstructStrikes.InReach(GlobalPosition, info.Target.GlobalPosition,
                    LingeringKitRules.CoilSyncedReachUnits, _rangeMultiplier)) return;
            Charges--;
            UpdatePips();
            HitPayload arc = BuildSyncedPayload(
                LingeringKitRules.CoilSyncedArcDamage, LingeringKitRules.CoilSyncedArcHitstunFrames,
                ConstructStrikes.CoilSyncedArcHitboxID);
            ConstructStrikes.Deliver(_ownerPlayer, info.Target, in arc, out _);
            if (Charges <= 0) DestroyCoil();
        }

        /// <summary>A synced strike or discharge payload: Construct delivery, <c>CountsAsCombo</c>, real hitstun, no knockback or hitstop.</summary>
        private HitPayload BuildSyncedPayload(float damage, int hitstunFrames, string hitboxID) {
            HitPayload payload = BuildHitPayload(damage, AttackClass.Basic, GlobalPosition,
                FTT.Core.StatusType.None, 0f, Vector2.Zero, hitboxID: hitboxID);
            payload.HitstunDuration = hitstunFrames / 60f;
            payload.CountsAsCombo = true;
            return payload;
        }

        /// <summary>
        /// Plan 1.2 / decision 25: the Lorentz discharge. Spends ALL remaining charges as one burst at
        /// <paramref name="target"/> (6 damage per charge, 20 frames of hitstun), then the coil fizzles.
        /// The caller (<c>TeslaLorentzPulse</c>) picks the target, delivers and credits it, and pays the one
        /// Detonation Momentum per Pulse. Returns false with nothing spent when the coil is gone or empty.
        /// </summary>
        public bool TryBuildDischarge(out HitPayload burst) {
            burst = default;
            if (IsCoilDestroyed || _rewindFrozen || Charges <= 0) return false;
            burst = BuildSyncedPayload(
                LingeringKitRules.DischargeDamagePerCharge * Charges, LingeringKitRules.DischargeHitstunFrames,
                ConstructStrikes.CoilDischargeHitboxID);
            return true;
        }

        /// <summary>Called after a built discharge has been delivered: the coil has spent everything and fizzles.</summary>
        public void FinishDischarge() {
            Charges = 0;
            UpdatePips();
            DestroyCoil();
        }

        /// <summary>The enemy hurtboxes within <paramref name="radiusPixels"/> of this coil (the Pulse's target pick).</summary>
        public System.Collections.Generic.List<Hurtbox> EnemiesWithin(float radiusPixels) =>
            QueryEnemyHurtboxes(GlobalPosition, radiusPixels);

        private void UpdateHPBar() {
            _hpBar ??= GetNodeOrNull<ProgressBar>("HPBar");
            if (_hpBar == null) return;
            _hpBar.MaxValue = MaxCoilHP;
            _hpBar.Value = Mathf.Max(0, _currentHP);
            _hpBar.Visible = !IsCoilDestroyed;
        }

        public void LinkPartner(TeslaCoilNode partner, bool drivesFence) {
            _partner = partner;
            _drivesFence = drivesFence;
        }

        public void OnSpawn() { }

        public void OnDespawn() {
            UnsubscribeFromOwner();
            Charges = 0;
            if (_partner != null && IsInstanceValid(_partner)) {
                _partner._partner = null;
                _partner._drivesFence = false;
            }
            if (_ownerPlayer != null && IsInstanceValid(_ownerPlayer)) {
                _ownerPlayer.ActivePersistentObjects.Remove(this);
            }
            _partner = null;
            _drivesFence = false;
            _ownerPlayer = null;
            _data = null;
            _currentHP = 0;
            IsCoilDestroyed = true;
            _rewindFrozen = false;
            _rangeMultiplier = 1f;
            _resonantOverdrive = false;
            _statusDurationMultiplier = 1f;
        }

        public void SetStoryRewindFrozen(bool frozen) => _rewindFrozen = frozen;

        public override void _PhysicsProcess(double delta) {
            if (_rewindFrozen || IsCoilDestroyed) return;
            float dt = (float)delta;

            _lifetime -= dt;
            if (_lifetime <= 0f) {
                DestroyCoil();
                return;
            }

            _arcTimer -= dt;
            if (_arcTimer <= 0f) {
                _arcTimer = _arcInterval;
                FireArc();
            }

            if (_drivesFence && _partner != null && IsInstanceValid(_partner) && !_partner.IsCoilDestroyed) {
                _fenceTimer -= dt;
                if (_fenceTimer <= 0f) {
                    _fenceTimer = FenceTickInterval;
                    if (GlobalPosition.DistanceTo(_partner.GlobalPosition) <= LinkRangePixels * _rangeMultiplier) {
                        TickFence();
                    }
                }
            }
        }

        private float OnCoilHit(HitPayload payload) {
            if (IsCoilDestroyed || payload.AttackerIndex == OwnerIndex) return 0f;
            int applied = Mathf.Clamp(Mathf.RoundToInt(payload.Damage), 0, _currentHP);
            _currentHP -= applied;
            UpdateHPBar();
            if (_currentHP <= 0) DestroyCoil();
            return applied;
        }

        private void DestroyCoil() {
            if (IsCoilDestroyed) return;
            IsCoilDestroyed = true;
            ReturnToPool();
        }

        /// <summary>
        /// Ultimate support (T03, Package 13 W6): the coil detonates, dealing
        /// <see cref="UltimateActivationRules.CoilDetonationDamage"/> to every
        /// enemy inside its own arc radius, and is destroyed.
        /// </summary>
        public void Explode() {
            if (IsCoilDestroyed) return;
            foreach (Hurtbox hurtbox in QueryEnemyHurtboxes(GlobalPosition, ArcRangePixels)) {
                // Package 12 W4 (D03h): the detonation is the Cataclysm's own
                // damage, so it is Ultimate-origin and earns Tesla no meter.
                // A01 (Package 13 W1): a persistent object's hit is Basic-class
                // (one charge), the burst included.
                HitPayload burst = BuildHitPayload(
                    UltimateActivationRules.CoilDetonationDamage, AttackClass.Special, GlobalPosition,
                    FTT.Core.StatusType.None, 0f, Vector2.Zero, originOverride: HitOrigin.Ultimate,
                    hitboxID: "coil_burst");
                float dealt = hurtbox.TakeHit(burst);
                CreditOwnerInfluence(in burst, dealt);
            }
            DestroyCoil();
        }

        private void FireArc() {
            Hurtbox nearest = null;
            float arcRange = ArcRangePixels * _rangeMultiplier;
            float nearestDistance = arcRange;
            foreach (Hurtbox hurtbox in QueryEnemyHurtboxes(GlobalPosition, arcRange)) {
                float distance = GlobalPosition.DistanceTo(hurtbox.GlobalPosition);
                if (distance <= nearestDistance) {
                    nearestDistance = distance;
                    nearest = hurtbox;
                }
            }
            if (nearest == null) {
                ArcAtExtractor(arcRange);
                return;
            }
            HitPayload arc = BuildHitPayload(
                ArcDamage, AttackClass.Basic, GlobalPosition,
                FTT.Core.StatusType.None, 0f, _data?.KnockbackForce ?? Vector2.Zero);
            float dealt = nearest.TakeHit(arc);
            CreditOwnerInfluence(in arc, dealt);
        }

        /// <summary>
        /// Resonant Overdrive (Story-only Resonance major, V7.6): the coil's
        /// arcs also strike Chronal Extractors inside the arc radius. A new
        /// target acquisition against damageable environment, taken only when
        /// no living enemy is in range so the Major never steals a hit from
        /// ordinary combat. This is the sanctioned INDIRECT channel between a
        /// grid and the V7.6 Timeline Integrity timer.
        /// </summary>
        private void ArcAtExtractor(float arcRange) {
            if (!_resonantOverdrive || !IsInsideTree()) return;
            Godot.Collections.Array<Node> extractors =
                GetTree().GetNodesInGroup(FTT.Environment.ChronalExtractor.GroupName);
            using var lifetime = extractors.AsDisposable();
            FTT.Environment.ChronalExtractor nearest = null;
            float nearestDistance = arcRange;
            foreach (Node node in extractors) {
                if (node is not FTT.Environment.ChronalExtractor extractor || extractor.IsDestroyed) continue;
                float distance = GlobalPosition.DistanceTo(extractor.GlobalPosition);
                if (distance <= nearestDistance) {
                    nearestDistance = distance;
                    nearest = extractor;
                }
            }
            nearest?.TakeEnvironmentDamage(ArcDamage);
        }

        private void TickFence() {
            Vector2 partnerPosition = _partner.GlobalPosition;
            Vector2 center = (GlobalPosition + partnerPosition) * 0.5f;
            var shape = new RectangleShape2D {
                Size = new Vector2(
                    Mathf.Max(Mathf.Abs(partnerPosition.X - GlobalPosition.X), 20f),
                    Mathf.Max(Mathf.Abs(partnerPosition.Y - GlobalPosition.Y), FenceHalfHeightPixels * 2f))
            };
            foreach (Hurtbox hurtbox in QueryEnemyHurtboxes(center, shape)) {
                // V7.6 F07: the fence's Static Charge stays a pure interrupt and
                // it also lays a Conductive mark at the BASELINE 90 frames — the
                // tesla_conductive_hold Resonance node extends only the finisher's
                // mark, never a linked fence's.
                HitPayload fence = BuildHitPayload(
                    FenceDamage, AttackClass.Basic, center,
                    FTT.Core.StatusType.StaticCharge, FenceStaticChargeDuration,
                    _data?.KnockbackForce ?? Vector2.Zero,
                    FTT.Combat.ComboMarkType.Conductive,
                    FTT.Combat.BasicComboRules.ConductiveMarkFenceFrames);
                float dealt = hurtbox.TakeHit(fence);
                CreditOwnerInfluence(in fence, dealt);
            }
        }

        /// <summary>
        /// A coil hit's damage under its owner's whole Story ability-damage
        /// composition (SpecialDamage, the coil's AbilityDamage lane and the
        /// item-drop damage buff), read when the hit lands; neutral for an
        /// ownerless coil.
        /// </summary>
        private float ScaledByOwner(float damage) =>
            _ownerPlayer != null && IsInstanceValid(_ownerPlayer)
                ? _ownerPlayer.StoryAbilityDamage(damage, _data?.AbilityID)
                : damage;

        private HitPayload BuildHitPayload(
            float damage, AttackClass attackClass, Vector2 origin,
            FTT.Core.StatusType status, float statusDuration, Vector2 knockback,
            FTT.Combat.ComboMarkType comboMark = FTT.Combat.ComboMarkType.None,
            int comboMarkFrames = 0,
            HitOrigin? originOverride = null,
            string hitboxID = "coil_arc") => StampConstruct(new HitPayload {
            AttackerIndex = OwnerIndex,
            AttackID = _data?.AbilityID ?? "tesla_tesla_coil",
            HitboxID = hitboxID,
            AttackClass = attackClass,
            Damage = ScaledByOwner(damage),
            Knockback = knockback,
            HitstunDuration = 0.15f,
            HitOrigin = origin,
            AttackerFacingRight = true,
            AppliedStatus = status,
            StatusDuration = statusDuration * _statusDurationMultiplier,
            StatusIntensity = 1f,
            ScreenShakeIntensity = 0.1f,
            ScreenShakeDuration = 0.05f,
            // V7.3: construct ticks carry no hitstop.
            ExemptFromHitstop = true,
            ComboMark = comboMark,
            ComboMarkFrames = comboMarkFrames
        }, originOverride);

        /// <summary>
        /// Package 12 W4: the coil's authored hit contract, stamped once —
        /// Construct delivery (D03g: no Rally), the ability's origin unless the
        /// Cataclysm detonation overrides it to Ultimate (D03h: no meter).
        /// </summary>
        private HitPayload StampConstruct(HitPayload payload, HitOrigin? originOverride) {
            payload = BaseSpecial.WithAbilityContract(payload, _data, _ownerPlayer, HitDelivery.Construct);
            if (originOverride.HasValue) payload.Origin = originOverride.Value;
            // An arc or fence tick never launches; it keeps its authored hitstun only.
            payload.Launches = false;
            return payload;
        }

        private System.Collections.Generic.List<Hurtbox> QueryEnemyHurtboxes(Vector2 center, float radius) =>
            QueryEnemyHurtboxes(center, new CircleShape2D { Radius = radius });

        private System.Collections.Generic.List<Hurtbox> QueryEnemyHurtboxes(Vector2 center, Shape2D shape) {
            var results = new System.Collections.Generic.List<Hurtbox>();
            var space = GetWorld2D()?.DirectSpaceState;
            if (space == null) return results;
            uint targetHurtboxLayer = OwnerIndex == 0
                ? FTT.Core.CollisionLayers.EnemyHurtbox
                : FTT.Core.CollisionLayers.PlayerHurtbox;
            var query = new PhysicsShapeQueryParameters2D {
                Shape = shape,
                Transform = new Transform2D(0f, center),
                CollideWithAreas = true,
                CollideWithBodies = false,
                CollisionMask = targetHurtboxLayer
            };
            foreach (Godot.Collections.Dictionary result in space.IntersectShape(query, 16)) {
                if (result["collider"].AsGodotObject() is Hurtbox hurtbox
                    && hurtbox.OwnerPlayerIndex != OwnerIndex) {
                    results.Add(hurtbox);
                }
            }
            return results;
        }

        private void CreditOwnerInfluence(in HitPayload payload, float dealt) {
            if (_ownerPlayer == null || !IsInstanceValid(_ownerPlayer)) return;
            // Construct delivery never reclaims Rally (D03g), read off the payload.
            BaseSpecial.CreditDealt(_ownerPlayer, in payload, dealt);
        }

        /// <summary>
        /// V7.6 Time Freeze. Shares the freeze flag with the death rewind because
        /// this class's rewind freeze is already a pure latch — it mutates nothing
        /// on the way in, so positions, phases and timers all survive the freeze
        /// and resume with no catch-up tick.
        /// </summary>
        public void SetTimeFrozen(bool frozen) => _rewindFrozen = frozen;

    }
}
