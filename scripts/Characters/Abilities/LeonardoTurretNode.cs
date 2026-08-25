using Godot;
using FTT.Combat;
using FTT.Characters;

namespace FTT.Characters.Abilities {

    /// <summary>
    /// Story-mode Clockwork Turret construct (design Section 4 specification):
    /// 20 HP, 15 s lifespan, max 1 active per owner. Fires a ballista bolt at
    /// the nearest enemy within 30 world units (1800 px) on the authored
    /// interval (V7: every 2 s), and self-destructs after its final bolt — the
    /// authored HitCount (V7: 4 bolts) — or when its lifespan/HP runs out.
    /// The turret is damageable/destroyable, persists across owner death, freezes
    /// during Chronal Rewind, and fully resets its pooled state. Story-only
    /// Resonance perk Clockwork Overdrive upgrades it to a rapid burst of 5 bolts.
    /// </summary>
    public partial class LeonardoTurretNode : FTT.Core.PooledNode, FTT.Core.IPoolable, FTT.Environment.IStoryRewindSimulation {

        private const float TargetRangePixels = 1800f;   // 30 world units at 60 px/unit.
        private const int MaxTurretHP = 20;
        private const int BaseBoltLimit = 3;
        private const int OverdriveBoltLimit = 5;
        private const float OverdriveIntervalMultiplier = 0.5f;

        public int OwnerIndex { get; private set; }
        public float BoltDamage { get; private set; }
        public int BoltsRemaining { get; private set; }
        public bool IsTurretDestroyed { get; private set; }

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
            _currentHP = Mathf.RoundToInt(MaxTurretHP * (owner?.StoryPersistentHealthMultiplier ?? 1f));
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
            // fires 5 bolts in a rapid burst instead of 3 before self-destructing.
            if (clockworkOverdrive) {
                BoltsRemaining = OverdriveBoltLimit;
                _fireInterval *= OverdriveIntervalMultiplier;
            }
            _fireTimer = _fireInterval;
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
        /// Fires an instant-strike ballista bolt at the nearest enemy hurtbox in
        /// range. Returns false when no target is available so the bolt is not
        /// consumed while the turret waits (mirrors the Fighter-sim turret, which
        /// only decrements RemainingAttacks on an actual attack).
        /// </summary>
        private bool TryFireBolt() {
            Hurtbox nearest = null;
            float nearestDistance = TargetRangePixels;
            foreach (Hurtbox hurtbox in QueryEnemyHurtboxes(GlobalPosition, TargetRangePixels)) {
                float distance = GlobalPosition.DistanceTo(hurtbox.GlobalPosition);
                if (distance <= nearestDistance) {
                    nearestDistance = distance;
                    nearest = hurtbox;
                }
            }
            if (nearest == null) return false;

            bool targetIsRight = nearest.GlobalPosition.X >= GlobalPosition.X;
            float dealt = nearest.TakeHit(new HitPayload {
                AttackerIndex = OwnerIndex,
                AttackID = _data?.AbilityID ?? "leonardo_clockwork_turret",
                HitboxID = "turret_bolt",
                AttackClass = AttackClass.Basic,
                Damage = BoltDamage,
                Knockback = _data?.KnockbackForce ?? Vector2.Zero,
                HitstunDuration = 0.15f,
                HitOrigin = GlobalPosition,
                AttackerFacingRight = targetIsRight,
                AppliedStatus = FTT.Core.StatusType.None,
                StatusDuration = 0f,
                StatusIntensity = 1f,
                ScreenShakeIntensity = 0.1f,
                ScreenShakeDuration = 0.05f
            });
            if (dealt > 0f && _ownerPlayer != null && IsInstanceValid(_ownerPlayer)) {
                // Construct damage never reclaims Rally echo (V7.1: direct hits only).
                _ownerPlayer.AddInfluenceFromDamageDealt(dealt, collectsEcho: false);
            }
            return true;
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
    }
}
