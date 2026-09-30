using Godot;
using FTT.Combat;
using FTT.Characters;

namespace FTT.Characters.Abilities {

    /// <summary>
    /// Story-mode Clockwork Turret construct (design Section 4 specification):
    /// 20 HP, 15 s lifespan, max 1 active per owner, placed at Leonardo's feet.
    /// Fires a straight ballista bolt (12 units/s, L03) at the nearest enemy in
    /// line of sight within 30 world units (1800 px) on the authored interval
    /// (V7: every 2 s) and only while one is in range, so the lifespan is the
    /// idle cap; it self-destructs after its final bolt — the authored HitCount
    /// (V7: 4 bolts, the glide's bonus bolt spending one) — or when its
    /// lifespan/HP runs out.
    /// The turret is damageable/destroyable, persists across owner death, freezes
    /// during Chronal Rewind, and fully resets its pooled state. Story-only
    /// Resonance perk Clockwork Overdrive upgrades it to a rapid burst of 5 bolts.
    /// </summary>
    public partial class LeonardoTurretNode : FTT.Core.PooledNode, FTT.Core.IPoolable, FTT.Environment.IStoryRewindSimulation, FTT.Environment.IStoryTimeFreezable {

        private const float TargetRangePixels = 1800f;   // 30 world units at 60 px/unit.
        private const int MaxTurretHP = 20;
        private const int BaseBoltLimit = 4;
        private const int OverdriveBoltLimit = 5;
        private const float OverdriveIntervalMultiplier = 0.5f;
        /// <summary>L03 fallback bolt speed (12 units/s) when the resource authors none.</summary>
        private const float DefaultBoltSpeedPixels = 720f;
        /// <summary>Where the bolt leaves the placeholder turret body (px above its feet).</summary>
        private static readonly Vector2 MuzzleOffset = new(0f, -20f);

        public int OwnerIndex { get; private set; }
        public float BoltDamage { get; private set; }
        public int BoltsRemaining { get; private set; }
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
            BoltDamage = (data?.BaseDamage ?? 5f) * (owner?.StoryProjectileDamageMultiplier ?? 1f);
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
            _lifetime = (data?.Lifetime > 0f ? data.Lifetime : 15f)
                * (owner?.StoryPersistentDurationMultiplier ?? 1f);
            _fireInterval = (data?.DamageTickIntervalFrames ?? 120) / 60f;
            if (_fireInterval <= 0f) _fireInterval = 2f;
            BoltsRemaining = data?.HitCount > 0 ? data.HitCount : BaseBoltLimit;
            // Clockwork Overdrive (Story-only Resonance major perk): the turret
            // fires 5 bolts in a rapid burst instead of 4 before self-destructing (M03).
            if (clockworkOverdrive) {
                BoltsRemaining = OverdriveBoltLimit;
                _fireInterval *= OverdriveIntervalMultiplier;
            }
            _fireTimer = _fireInterval;
            BoltsFired = 0;
            UpdateHPBar();
        }

        private void UpdateHPBar() {
            _hpBar ??= GetNodeOrNull<ProgressBar>("HPBar");
            if (_hpBar == null) return;
            _hpBar.MaxValue = Mathf.Max(1, _maxHP);
            _hpBar.Value = Mathf.Max(0, _currentHP);
            _hpBar.Visible = !IsTurretDestroyed;
        }

        public void OnSpawn() { }

        public void OnDespawn() {
            if (_ownerPlayer != null && IsInstanceValid(_ownerPlayer)) {
                _ownerPlayer.ActivePersistentObjects.Remove(this);
            }
            _ownerPlayer = null;
            _data = null;
            _currentHP = 0;
            BoltsRemaining = 0;
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
            if (_fireTimer <= 0f && TryFireBolt()) {
                _fireTimer = _fireInterval;
                BoltsRemaining--;
                // Design Section 4: the turret self-destructs after its final bolt.
                if (BoltsRemaining <= 0) DestroyTurret();
            }
        }

        /// <summary>
        /// Package 12 W4 (V7 kit rule, Leonardo): "Ornithopter glide can fire one
        /// turret bolt mid-flight if a turret is deployed." Fires this turret's
        /// next bolt NOW, out of its own budget and on its own targeting — the
        /// bolt count, self-destruct on the last bolt and cadence reset are the
        /// ordinary ones. Returns false (spending nothing) when the turret is
        /// gone, frozen or has no target in range.
        /// </summary>
        public bool TryCommandBolt() {
            if (IsTurretDestroyed || _rewindFrozen || BoltsRemaining <= 0) return false;
            if (!TryFireBolt()) return false;
            _fireTimer = _fireInterval;
            BoltsRemaining--;
            if (BoltsRemaining <= 0) DestroyTurret();
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
        /// line of sight. Returns false when no target is available so the bolt
        /// is not consumed while the turret waits (mirrors the Fighter-sim
        /// turret, which only decrements RemainingAttacks on an actual shot).
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
            PlaceholderProjectile bolt = BaseSpecial.SpawnStoryProjectile(GetParent(), muzzle);
            if (bolt == null) {
                StrikeInstantly(nearest);
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

        /// <summary>Bolts this deployment has fired as projectiles. Test seam.</summary>
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
                Damage = BoltDamage,
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
