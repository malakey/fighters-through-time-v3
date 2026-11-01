using Godot;
using FTT.Combat;
using FTT.Characters;
using FTT.Core;

namespace FTT.Characters.Abilities {

    /// <summary>
    /// Story-mode Clockwork Turret construct (design Section 4 specification):
    /// 20 HP, 15 s lifespan, max 1 active per owner, placed at Leonardo's feet.
    /// Fires a straight ballista bolt (12 units/s, L03) at the nearest enemy in
    /// line of sight within 30 world units (1800 px) every 180 frames (4 damage)
    /// and only while one is in range. <b>Lingering-kit pass (2026-10-31):</b> that
    /// own shot spends nothing and the turret no longer self-destructs after four
    /// bolts; it lives out its 15 s unless its charges or HP run out first. It holds
    /// <see cref="LingeringKitRules.TurretCharges"/> charges (+1 with Clockwork
    /// Overdrive); each synced strike (a fast 8-damage bolt with 20 frames of
    /// hitstun, fired when its owner's hit lands on a target within 10 units) and the
    /// glide's bonus bolt spends one, and at 0 it fizzles.
    /// The turret is damageable/destroyable, persists across owner death, freezes
    /// during Chronal Rewind, and fully resets its pooled state. Story-only
    /// Resonance perk Clockwork Overdrive adds a charge and a faster own shot at
    /// Chronal Extractors.
    /// </summary>
    public partial class LeonardoTurretNode : FTT.Core.PooledNode, FTT.Core.IPoolable, FTT.Environment.IStoryRewindSimulation, FTT.Environment.IStoryTimeFreezable {

        private const float TargetRangePixels = 1800f;   // 30 world units at 60 px/unit.
        private const int MaxTurretHP = 20;
        private const float OverdriveIntervalMultiplier = LingeringKitRules.TurretOverdriveIntervalPercent / 100f;
        /// <summary>L03 fallback bolt speed (12 units/s) when the resource authors none.</summary>
        private const float DefaultBoltSpeedPixels = 720f;
        /// <summary>Where the bolt leaves the placeholder turret body (px above its feet).</summary>
        private static readonly Vector2 MuzzleOffset = new(0f, -20f);

        public int OwnerIndex { get; private set; }
        /// <summary>
        /// The damage a bolt's Hitbox carries: the build-time half of the
        /// owner's Story ability-damage composition over the bolt's base damage
        /// (the hitbox adds the item-drop buff and the AbilityDamage lane when it
        /// lands).
        /// </summary>
        public float BoltDamage { get; private set; }

        /// <summary>The bolt's damage (the rulebook's own shot) x the Story ProjectileDamage lane.</summary>
        private float _boltBaseDamage;

        /// <summary>
        /// Charges left (plan 1.4): spent only by a synced strike and the glide bonus bolt. The own shot
        /// spends nothing. Reset on Initialize and OnDespawn; untouched by Time Freeze and the death rewind.
        /// </summary>
        public int Charges { get; private set; }
        /// <summary>This deployment's charge cap: 4, or 5 with Clockwork Overdrive.</summary>
        public int MaxCharges { get; private set; } = LingeringKitRules.TurretCharges;
        public bool IsTurretDestroyed { get; private set; }

        /// <summary>
        /// Package 11 A4 (Re-placement traversal node, V7.6). True while this
        /// turret can still be picked up and re-placed once. Reset on every
        /// spawn from the pool, so a recycled node never carries a spent
        /// allowance forward.
        /// </summary>
        public bool ReplacementAvailable { get; private set; } = true;

        /// <summary>Consumes the single re-placement allowance atomically.</summary>
        public bool TryConsumeReplacement() {
            if (!ReplacementAvailable || IsTurretDestroyed) return false;
            ReplacementAvailable = false;
            return true;
        }

        private PlayerController _ownerPlayer;
        private AbilityData _data;
        private int _currentHP;
        private float _lifetime;
        private float _fireInterval;
        private float _fireTimer;
        private bool _rewindFrozen;
        private Hurtbox _hurtbox;
        private bool _hurtboxBound;
        private ProgressBar _hpBar;
        private int _maxHP = MaxTurretHP;
        private bool _clockworkOverdrive;
        private PlayerController _subscribedOwner;
        private ConstructChargePips _pips;

        // Pooled constructs re-enter the tree on every spawn cycle but _Ready runs
        // once, so the hurtbox subscription lives on the enter/exit pair (audit
        // H-3: a _Ready-only bind left the warmed turret indestructible after its
        // first reparent).
        public override void _EnterTree() {
            _hurtbox ??= GetNodeOrNull<Hurtbox>("Hurtbox");
            if (_hurtbox == null || _hurtboxBound) return;
            _hurtbox.OnHit += OnTurretHit;
            _hurtboxBound = true;
        }

        public override void _ExitTree() {
            UnsubscribeFromOwner();
            if (!_hurtboxBound) return;
            _hurtboxBound = false;
            if (_hurtbox != null) _hurtbox.OnHit -= OnTurretHit;
        }

        public void Initialize(AbilityData data, PlayerController owner, bool clockworkOverdrive) {
            _data = data;
            _ownerPlayer = owner;
            OwnerIndex = owner?.PlayerIndex ?? 0;
            // Story-only Resonance minors: ProjectileDamage raises bolt damage and
            // PersistentHealth reinforces the chassis. Both 1f outside Story Mode.
            // Lingering-kit pass: the own shot is 4 damage (the rulebook, one value).
            _boltBaseDamage = LingeringKitRules.TurretOwnShotDamage * (owner?.StoryProjectileDamageMultiplier ?? 1f);
            BoltDamage = owner?.StoryAbilityHitboxDamage(_boltBaseDamage) ?? _boltBaseDamage;
            // Package 11 A4: V7.6 re-scopes "Minor Turret Plating" from the
            // character-wide PersistentHealth lane onto
            // ConstructHP(leonardo_clockwork_turret), and raises it to +25%.
            ReplacementAvailable = true;
            _currentHP = Mathf.RoundToInt(MaxTurretHP
                * (owner?.StoryPersistentHealthMultiplier ?? 1f)
                * (owner?.StoryScoped("ConstructHP", "leonardo_clockwork_turret") ?? 1f));
            _maxHP = _currentHP;
            IsTurretDestroyed = false;
            // Story-only PersistentDuration minors lengthen the deployment, the
            // same rule the nest/coil/snare constructs already follow.
            _lifetime = (data?.Lifetime > 0f ? data.Lifetime : LingeringKitRules.TurretLifetimeSeconds)
                * (owner?.StoryPersistentDurationMultiplier ?? 1f);
            _fireInterval = LingeringKitRules.TurretOwnShotIntervalFrames / 60f;
            // Clockwork Overdrive (Story-only Resonance major perk, decision 26): +1 charge, and an own shot
            // at half the interval against Chronal Extractors only.
            _clockworkOverdrive = clockworkOverdrive;
            MaxCharges = LingeringKitRules.TurretCharges
                + (clockworkOverdrive ? LingeringKitRules.TurretOverdriveBonusCharges : 0);
            Charges = MaxCharges;
            _fireTimer = _fireInterval;
            BoltsFired = 0;
            SyncedBoltsFired = 0;
            SubscribeToOwner(owner);
            UpdateHPBar();
            UpdatePips();
        }

        private void UpdateHPBar() {
            _hpBar ??= GetNodeOrNull<ProgressBar>("HPBar");
            if (_hpBar == null) return;
            _hpBar.MaxValue = Mathf.Max(1, _maxHP);
            _hpBar.Value = Mathf.Max(0, _currentHP);
            _hpBar.Visible = !IsTurretDestroyed;
        }

        private void UpdatePips() {
            _pips ??= GetNodeOrNull<ConstructChargePips>("ChargePips");
            _pips?.SetCharges(Charges, MaxCharges);
            if (_pips != null) _pips.Visible = !IsTurretDestroyed;
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
        /// Plan 1.3: the owner's landed basic hit or direct Special hit on a target within 10 units (a 2D
        /// circle) fires one fast straight synced bolt at it and spends one charge. A strike bolt, the own
        /// shot, a tick, the Pulse or a throw never reaches this (the
        /// <see cref="OwnerHitLandedInfo.TriggersSyncedStrike"/> filter).
        /// </summary>
        private void OnOwnerHitLanded(OwnerHitLandedInfo info) {
            if (_rewindFrozen || IsTurretDestroyed || Charges <= 0 || !info.TriggersSyncedStrike) return;
            if (info.Target == null || !IsInstanceValid(info.Target)) return;
            if (!ConstructStrikes.InReach(GlobalPosition, info.Target.GlobalPosition,
                    LingeringKitRules.TurretSyncedReachUnits)) return;
            Charges--;
            UpdatePips();
            Hurtbox target = info.Target;
            // The bolt is a pooled collision body: spawned off the physics flush that raised the seam.
            if (FTT.Core.PhysicsCallbackGuard.IsInPhysicsCallback) {
                Callable.From(() => FireSyncedBolt(target)).CallDeferred();
            } else {
                FireSyncedBolt(target);
            }
            if (Charges <= 0) DestroyTurret();
        }

        /// <summary>
        /// Test seam: deliver every bolt as the instant strike (the no-pool fallback) even when a projectile
        /// pool exists, so a unit case can read the damage without physics frames. Never set in production.
        /// </summary>
        public static bool InstantStrikesForTesting { get; set; }

        /// <summary>Synced strikes this deployment has fired. Test seam.</summary>
        public int SyncedBoltsFired { get; private set; }

        private void FireSyncedBolt(Hurtbox target) {
            if (target == null || !IsInstanceValid(target) || !IsInsideTree()) return;
            Vector2 muzzle = GlobalPosition + MuzzleOffset;
            Vector2 aim = target.GlobalPosition - muzzle;
            PlaceholderProjectile bolt = InstantStrikesForTesting ? null : BaseSpecial.SpawnStoryProjectile(GetParent(), muzzle);
            SyncedBoltsFired++;
            if (bolt == null) {
                // No projectile pool (a bare unit harness): the same strike, instantly.
                HitPayload strike = BaseSpecial.WithAbilityContract(new HitPayload {
                    AttackerIndex = OwnerIndex,
                    AttackID = _data?.AbilityID ?? "leonardo_clockwork_turret",
                    HitboxID = ConstructStrikes.TurretSyncedBoltHitboxID,
                    AttackClass = AttackClass.Basic,
                    Damage = _ownerPlayer != null && IsInstanceValid(_ownerPlayer)
                        ? _ownerPlayer.StoryAbilityDamage(LingeringKitRules.TurretSyncedBoltDamage, _data?.AbilityID)
                        : LingeringKitRules.TurretSyncedBoltDamage,
                    Knockback = Vector2.Zero,
                    HitstunDuration = LingeringKitRules.TurretSyncedBoltHitstunFrames / 60f,
                    HitOrigin = GlobalPosition,
                    AttackerFacingRight = aim.X >= 0f,
                    AppliedStatus = FTT.Core.StatusType.None,
                    StatusDuration = 0f,
                    StatusIntensity = 1f,
                    ScreenShakeIntensity = 0.1f,
                    ScreenShakeDuration = 0.05f,
                    ExemptFromHitstop = true,
                    CountsAsCombo = true
                }, _data, _ownerPlayer, HitDelivery.Construct);
                strike.Launches = false;
                ConstructStrikes.Deliver(_ownerPlayer, target, in strike, out _);
                return;
            }
            // The pooled bolt's Hitbox adds the contact-time half of the Story damage composition.
            float damage = _ownerPlayer != null && IsInstanceValid(_ownerPlayer)
                ? _ownerPlayer.StoryAbilityHitboxDamage(LingeringKitRules.TurretSyncedBoltDamage)
                : LingeringKitRules.TurretSyncedBoltDamage;
            float speed = (float)(LingeringKitRules.TurretSyncedBoltSpeedUnitsPerSecond * KitMotionRules.StoryPixelsPerUnit)
                * (_ownerPlayer?.StoryProjectileSpeedMultiplier ?? 1f);
            float lifetime = (float)(LingeringKitRules.TurretSyncedReachUnits * 2.0 * KitMotionRules.StoryPixelsPerUnit) / speed;
            bolt.Setup(damage, Vector2.Zero, speed, aim.X >= 0f, OwnerIndex,
                new Color(1f, 0.85f, 0.4f), new Vector2(18f, 6f), lifetime, _ownerPlayer, _data);
            bolt.ConfigureSyncedStrike(LingeringKitRules.TurretSyncedBoltHitstunFrames / 60f);
            bolt.ConfigureDirection(aim);
            bolt.StopsOnTerrain = true;
        }

        public void OnSpawn() { }

        public void OnDespawn() {
            UnsubscribeFromOwner();
            if (_ownerPlayer != null && IsInstanceValid(_ownerPlayer)) {
                _ownerPlayer.ActivePersistentObjects.Remove(this);
            }
            _ownerPlayer = null;
            _data = null;
            _currentHP = 0;
            Charges = 0;
            _clockworkOverdrive = false;
            IsTurretDestroyed = true;
            _rewindFrozen = false;
        }

        public void SetStoryRewindFrozen(bool frozen) => _rewindFrozen = frozen;

        public override void _PhysicsProcess(double delta) {
            if (_rewindFrozen || IsTurretDestroyed) return;
            float dt = (float)delta;

            _lifetime -= dt;
            if (_lifetime <= 0f) {
                DestroyTurret();
                return;
            }

            _fireTimer -= dt;
            if (_fireTimer <= 0f) {
                // The own shot spends no charge and no longer ends the turret. Against an enemy it comes
                // every 180 frames; Clockwork Overdrive adds a Chronal Extractor shot at half that
                // interval, taken only when no enemy is in range.
                if (TryFireBolt()) {
                    _fireTimer = _fireInterval;
                } else if (_clockworkOverdrive && TryFireAtExtractor()) {
                    _fireTimer = _fireInterval * OverdriveIntervalMultiplier;
                }
            }
        }

        /// <summary>
        /// Clockwork Overdrive (decision 26): the faster own shot is for Chronal Extractors only. A direct
        /// environment hit (the coil's Overdrive pattern) at the own-shot damage times the Overdrive
        /// Extractor multiplier, on the nearest standing Extractor in range and line of sight.
        /// </summary>
        private bool TryFireAtExtractor() {
            if (!IsInsideTree()) return false;
            Godot.Collections.Array<Node> extractors =
                GetTree().GetNodesInGroup(FTT.Environment.ChronalExtractor.GroupName);
            using var lifetime = extractors.AsDisposable();
            FTT.Environment.ChronalExtractor nearest = null;
            float nearestDistance = TargetRangePixels;
            Vector2 muzzle = GlobalPosition + MuzzleOffset;
            foreach (Node node in extractors) {
                if (node is not FTT.Environment.ChronalExtractor extractor || extractor.IsDestroyed) continue;
                float distance = GlobalPosition.DistanceTo(extractor.GlobalPosition);
                if (distance <= nearestDistance && HasLineOfSight(muzzle, extractor.GlobalPosition)) {
                    nearestDistance = distance;
                    nearest = extractor;
                }
            }
            if (nearest == null) return false;
            float multiplier = FTT.Environment.StoryExtractorDamage.ExtractorMultiplier(
                _ownerPlayer, FTT.Environment.StoryExtractorDamage.ClockworkTurretAttackID);
            nearest.TakeEnvironmentDamage(BoltDamage * multiplier);
            BoltsFired++;
            return true;
        }

        /// <summary>
        /// Package 12 W4 (V7 kit rule, Leonardo): "Ornithopter glide can fire one
        /// turret bolt mid-flight if a turret is deployed." Fires this turret's
        /// next bolt NOW on its own targeting; since the lingering-kit pass it
        /// spends one of the turret's charges, and the turret fizzles at 0.
        /// Returns false (spending nothing) when the turret is gone, frozen,
        /// empty or has no target in range.
        /// </summary>
        public bool TryCommandBolt() {
            if (IsTurretDestroyed || _rewindFrozen || Charges <= 0) return false;
            if (!TryFireBolt()) return false;
            _fireTimer = _fireInterval;
            Charges--;
            UpdatePips();
            if (Charges <= 0) DestroyTurret();
            return true;
        }

        private float OnTurretHit(HitPayload payload) {
            if (IsTurretDestroyed || payload.AttackerIndex == OwnerIndex) return 0f;
            int applied = Mathf.Clamp(Mathf.RoundToInt(payload.Damage), 0, _currentHP);
            _currentHP -= applied;
            UpdateHPBar();
            if (_currentHP <= 0) DestroyTurret();
            return applied;
        }

        private void DestroyTurret() {
            if (IsTurretDestroyed) return;
            IsTurretDestroyed = true;
            ReturnToPool();
        }

        /// <summary>
        /// Fires one ballista bolt at the nearest enemy hurtbox in range AND in
        /// line of sight. Returns false when no target is available so the shot
        /// is not consumed while the turret waits.
        ///
        /// <para><b>Package 13 W7a (L03).</b> The bolt is a real straight
        /// projectile at the authored <c>ProjectileSpeed</c> (12 units/s) aimed at
        /// the target, stopping on solid terrain — no longer an instant strike.
        /// It stays a construct hit: Basic-class, impulse-free, non-launching, no
        /// Rally reclaim and no hitstop. With no projectile pool (a bare unit
        /// harness) the historical instant strike is the fallback.</para>
        /// </summary>
        private bool TryFireBolt() {
            Hurtbox nearest = null;
            float nearestDistance = TargetRangePixels;
            Vector2 muzzle = GlobalPosition + MuzzleOffset;
            foreach (Hurtbox hurtbox in QueryEnemyHurtboxes(GlobalPosition, TargetRangePixels)) {
                float distance = GlobalPosition.DistanceTo(hurtbox.GlobalPosition);
                if (distance <= nearestDistance && HasLineOfSight(muzzle, hurtbox.GlobalPosition)) {
                    nearestDistance = distance;
                    nearest = hurtbox;
                }
            }
            if (nearest == null) return false;

            Vector2 aim = nearest.GlobalPosition - muzzle;
            PlaceholderProjectile bolt = InstantStrikesForTesting ? null : BaseSpecial.SpawnStoryProjectile(GetParent(), muzzle);
            if (bolt == null) {
                StrikeInstantly(nearest);
                BoltsFired++;
                return true;
            }
            float speed = (_data?.ProjectileSpeed > 0f ? _data.ProjectileSpeed : DefaultBoltSpeedPixels)
                * (_ownerPlayer?.StoryProjectileSpeedMultiplier ?? 1f);
            float lifetime = _data?.ProjectileLifetime > 0f ? _data.ProjectileLifetime : TargetRangePixels / speed;
            bolt.Setup(BoltDamage, Vector2.Zero, speed, aim.X >= 0f, OwnerIndex,
                new Color(0.85f, 0.65f, 0.3f), new Vector2(16f, 6f), lifetime, _ownerPlayer, _data);
            bolt.ConfigureConstructHit();
            bolt.ConfigureDirection(aim);
            bolt.StopsOnTerrain = true;
            BoltsFired++;
            return true;
        }

        /// <summary>Own shots this deployment has fired (projectile or Extractor). Test seam.</summary>
        public int BoltsFired { get; private set; }

        /// <summary>L03: the straight bolt needs a clear line through solid terrain.</summary>
        private bool HasLineOfSight(Vector2 from, Vector2 to) {
            var space = GetWorld2D()?.DirectSpaceState;
            if (space == null) return true;
            var query = PhysicsRayQueryParameters2D.Create(from, to, FTT.Core.CollisionLayers.Environment);
            using Godot.Collections.Dictionary hit = space.IntersectRay(query);
            return hit == null || hit.Count == 0;
        }

        /// <summary>The pre-W7a instant strike, kept only as the no-pool fallback.</summary>
        private void StrikeInstantly(Hurtbox nearest) {
            bool targetIsRight = nearest.GlobalPosition.X >= GlobalPosition.X;
            HitPayload bolt = BaseSpecial.WithAbilityContract(new HitPayload {
                AttackerIndex = OwnerIndex,
                AttackID = _data?.AbilityID ?? "leonardo_clockwork_turret",
                HitboxID = "turret_bolt",
                AttackClass = AttackClass.Basic,
                // No Hitbox delivers this fallback strike, so it takes the whole
                // Story ability-damage composition here.
                Damage = _ownerPlayer != null && IsInstanceValid(_ownerPlayer)
                    ? _ownerPlayer.StoryAbilityDamage(_boltBaseDamage, _data?.AbilityID)
                    : _boltBaseDamage,
                Knockback = Vector2.Zero,
                HitstunDuration = 0.15f,
                HitOrigin = GlobalPosition,
                AttackerFacingRight = targetIsRight,
                AppliedStatus = FTT.Core.StatusType.None,
                StatusDuration = 0f,
                StatusIntensity = 1f,
                ScreenShakeIntensity = 0.1f,
                ScreenShakeDuration = 0.05f,
                // V7.3: construct ticks carry no hitstop.
                ExemptFromHitstop = true
            }, _data, _ownerPlayer, HitDelivery.Construct);
            float dealt = nearest.TakeHit(bolt);
            if (_ownerPlayer != null && IsInstanceValid(_ownerPlayer)) {
                // Construct delivery never reclaims Rally (D03g), read off the payload.
                BaseSpecial.CreditDealt(_ownerPlayer, in bolt, dealt);
            }
        }

        private System.Collections.Generic.List<Hurtbox> QueryEnemyHurtboxes(Vector2 center, float radius) {
            var results = new System.Collections.Generic.List<Hurtbox>();
            var space = GetWorld2D()?.DirectSpaceState;
            if (space == null) return results;
            uint targetHurtboxLayer = OwnerIndex == 0
                ? FTT.Core.CollisionLayers.EnemyHurtbox
                : FTT.Core.CollisionLayers.PlayerHurtbox;
            var query = new PhysicsShapeQueryParameters2D {
                Shape = new CircleShape2D { Radius = radius },
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

        /// <summary>
        /// V7.6 Time Freeze. Shares the freeze flag with the death rewind because
        /// this class's rewind freeze is already a pure latch — it mutates nothing
        /// on the way in, so positions, phases and timers all survive the freeze
        /// and resume with no catch-up tick.
        /// </summary>
        public void SetTimeFrozen(bool frozen) => _rewindFrozen = frozen;

    }
}
