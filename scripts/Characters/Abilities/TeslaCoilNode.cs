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
        private const float ArcRangePixels = 300f;   // 5 world units; mirrors the Fighter sim AttackRange.
        private const int MaxCoilHP = 25;
        // 2026-08-11 construct rebalance: fence cadence halved and damage halved
        // alongside the .tres arc retune; construct hits carry no knockback.
        private const float FenceTickInterval = 1.0f;
        private const float FenceDamage = 4f;
        private const float FenceHalfHeightPixels = 60f;
        private const float FenceStaticChargeDuration = 1.0f;
        private const float ExplosionRadiusPixels = 150f;

        public int OwnerIndex { get; private set; }
        public float ArcDamage { get; private set; }
        /// <summary>Seconds the coil still has to stand. Test seam.</summary>
        public float LifetimeRemaining => _lifetime;
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
        private bool _hurtboxBound;
        private ProgressBar _hpBar;
        // Story-only Resonance minors captured at deploy time (1f in Fighter Mode).
        private float _rangeMultiplier = 1f;
        private bool _resonantOverdrive;
        private float _statusDurationMultiplier = 1f;

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
            UpdateHPBar();
        }

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

        /// <summary>Ultimate support: the coil detonates for double arc damage.</summary>
        public void Explode() {
            if (IsCoilDestroyed) return;
            foreach (Hurtbox hurtbox in QueryEnemyHurtboxes(GlobalPosition, ExplosionRadiusPixels)) {
                float dealt = hurtbox.TakeHit(BuildHitPayload(
                    ArcDamage * 2f, AttackClass.Special, GlobalPosition,
                    FTT.Core.StatusType.None, 0f, Vector2.Zero));
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
            if (nearest == null) {
                ArcAtExtractor(arcRange);
                return;
            }
            float dealt = nearest.TakeHit(BuildHitPayload(
                ArcDamage, AttackClass.Basic, GlobalPosition,
                FTT.Core.StatusType.None, 0f, _data?.KnockbackForce ?? Vector2.Zero));
            CreditOwnerInfluence(dealt);
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
                float dealt = hurtbox.TakeHit(BuildHitPayload(
                    FenceDamage, AttackClass.Basic, center,
                    FTT.Core.StatusType.StaticCharge, FenceStaticChargeDuration,
                    _data?.KnockbackForce ?? Vector2.Zero,
                    FTT.Combat.ComboMarkType.Conductive,
                    FTT.Combat.BasicComboRules.ConductiveMarkFenceFrames));
                CreditOwnerInfluence(dealt);
            }
        }

        private HitPayload BuildHitPayload(
            float damage, AttackClass attackClass, Vector2 origin,
            FTT.Core.StatusType status, float statusDuration, Vector2 knockback,
            FTT.Combat.ComboMarkType comboMark = FTT.Combat.ComboMarkType.None,
            int comboMarkFrames = 0) => new() {
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
            ScreenShakeDuration = 0.05f,
            // V7.3: construct ticks carry no hitstop.
            ExemptFromHitstop = true,
            ComboMark = comboMark,
            ComboMarkFrames = comboMarkFrames
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
                // Construct damage never reclaims Rally echo (V7.1: direct hits only).
                _ownerPlayer.AddInfluenceFromDamageDealt(dealt, collectsEcho: false);
            }
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
