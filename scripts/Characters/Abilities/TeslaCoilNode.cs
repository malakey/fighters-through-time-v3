using Godot;
using FTT.Combat;
using FTT.Characters;

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
    public partial class TeslaCoilNode : FTT.Core.PooledNode, FTT.Core.IPoolable, FTT.Environment.IStoryRewindSimulation {

        public const float LinkRangePixels = 480f;   // 8 world units at 60 px/unit.
        private const float ArcRangePixels = 300f;   // 5 world units; mirrors the Fighter sim AttackRange.
        private const int MaxCoilHP = 25;
        private const float FenceTickInterval = 0.5f;
        private const float FenceDamage = 8f;
        private const float FenceHalfHeightPixels = 60f;
        private const float FenceStaticChargeDuration = 1.0f;
        private const float ExplosionRadiusPixels = 150f;

        public int OwnerIndex { get; private set; }
        public float ArcDamage { get; private set; }
        public bool IsCoilDestroyed { get; private set; }

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
        // Story-only Resonance minors captured at deploy time (1f in Fighter Mode).
        private float _rangeMultiplier = 1f;
        private float _statusDurationMultiplier = 1f;

        public override void _Ready() {
            _hurtbox = GetNodeOrNull<Hurtbox>("Hurtbox");
            if (_hurtbox != null) _hurtbox.OnHit += OnCoilHit;
        }

        public override void _ExitTree() {
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
            _lifetime = (data?.Lifetime > 0f ? data.Lifetime : 30f)
                * (owner?.StoryPersistentDurationMultiplier ?? 1f);
            _rangeMultiplier = owner?.StoryPersistentRangeMultiplier ?? 1f;
            _statusDurationMultiplier = owner?.StoryStatusDurationMultiplier ?? 1f;
            _arcInterval = (data?.DamageTickIntervalFrames ?? 120) / 60f;
            if (_arcInterval <= 0f) _arcInterval = 2f;
            // Resonant Overdrive (Story-only Resonance major perk): coils last
            // 5 seconds longer and fire arcs 25% faster.
            if (resonantOverdrive) {
                _lifetime += 5f;
                _arcInterval /= 1.25f;
            }
            _arcTimer = _arcInterval;
            _fenceTimer = FenceTickInterval;
            _partner = null;
            _drivesFence = false;
        }

        public void LinkPartner(TeslaCoilNode partner, bool drivesFence) {
            _partner = partner;
            _drivesFence = drivesFence;
        }

        public void OnSpawn() { }

        public void OnDespawn() {
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
            if (_currentHP <= 0) DestroyCoil();
            return applied;
        }

        private void DestroyCoil() {
            if (IsCoilDestroyed) return;
            IsCoilDestroyed = true;
            ReturnToPool();
        }

        /// <summary>Ultimate support: the coil detonates for double arc damage.</summary>
        public void Explode() {
            if (IsCoilDestroyed) return;
            foreach (Hurtbox hurtbox in QueryEnemyHurtboxes(GlobalPosition, ExplosionRadiusPixels)) {
                float dealt = hurtbox.TakeHit(BuildHitPayload(
                    ArcDamage * 2f, AttackClass.Special, GlobalPosition,
                    FTT.Core.StatusType.None, 0f, new Vector2(3f, -3f)));
                CreditOwnerInfluence(dealt);
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
            if (nearest == null) return;
            float dealt = nearest.TakeHit(BuildHitPayload(
                ArcDamage, AttackClass.Basic, GlobalPosition,
                FTT.Core.StatusType.None, 0f, new Vector2(1f, -0.5f)));
            CreditOwnerInfluence(dealt);
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
                float dealt = hurtbox.TakeHit(BuildHitPayload(
                    FenceDamage, AttackClass.Basic, center,
                    FTT.Core.StatusType.StaticCharge, FenceStaticChargeDuration, new Vector2(1f, -0.5f)));
                CreditOwnerInfluence(dealt);
            }
        }

        private HitPayload BuildHitPayload(
            float damage, AttackClass attackClass, Vector2 origin,
            FTT.Core.StatusType status, float statusDuration, Vector2 knockback) => new() {
            AttackerIndex = OwnerIndex,
            AttackID = _data?.AbilityID ?? "tesla_tesla_coil",
            HitboxID = attackClass == AttackClass.Basic ? "coil_arc" : "coil_burst",
            AttackClass = attackClass,
            Damage = damage,
            Knockback = knockback,
            HitstunDuration = 0.15f,
            HitOrigin = origin,
            AttackerFacingRight = true,
            AppliedStatus = status,
            StatusDuration = statusDuration * _statusDurationMultiplier,
            StatusIntensity = 1f,
            ScreenShakeIntensity = 0.1f,
            ScreenShakeDuration = 0.05f
        };

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

        private void CreditOwnerInfluence(float dealt) {
            if (dealt > 0f && _ownerPlayer != null && IsInstanceValid(_ownerPlayer)) {
                _ownerPlayer.AddInfluenceFromDamageDealt(dealt);
            }
        }
    }
}
