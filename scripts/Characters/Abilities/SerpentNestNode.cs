using Godot;
using FTT.Combat;
using FTT.Characters;

namespace FTT.Characters.Abilities {

    /// <summary>
    /// Story-mode Serpent Nest construct (design Section 4 specification): 15 HP,
    /// 12 s lifespan, max 1 active per owner. Spectral asps bite every enemy
    /// passing over the nest at the authored tick interval, dealing light physical
    /// damage and applying Venom for 4 s. The design's "Root 1 s then Venom 4 s"
    /// conflicts with the single-status rule (the newest status completely
    /// replaces the previous), so the brief root is delivered as one second of
    /// bite hitstun while Venom is the applied status. The nest is
    /// damageable/destroyable, persists across owner death, freezes during
    /// Chronal Rewind, and fully resets its pooled state.
    /// </summary>
    public partial class SerpentNestNode : FTT.Core.PooledNode, FTT.Core.IPoolable, FTT.Environment.IStoryRewindSimulation {

        private const float BiteRangePixels = 90f;   // 1.5 world units at 60 px/unit; "passing over" contact.
        private const int MaxNestHP = 15;
        private const float RootHitstunSeconds = 1.0f;
        private const float VenomDurationFallback = 4f;

        public int OwnerIndex { get; private set; }
        public bool IsNestDestroyed { get; private set; }

        private PlayerController _ownerPlayer;
        private AbilityData _data;
        private int _currentHP;
        private float _lifetime;
        private float _biteInterval;
        private float _biteTimer;
        private bool _aspsBite;
        private bool _rewindFrozen;
        private Hurtbox _hurtbox;
        private bool _hurtboxBound;
        private ProgressBar _hpBar;

        // Pooled constructs re-enter the tree on every spawn cycle but _Ready runs
        // once, so the hurtbox subscription lives on the enter/exit pair (audit
        // H-3: a _Ready-only bind left the warmed nest indestructible after its
        // first reparent).
        public override void _EnterTree() {
            _hurtbox ??= GetNodeOrNull<Hurtbox>("Hurtbox");
            if (_hurtbox == null || _hurtboxBound) return;
            _hurtbox.OnHit += OnNestHit;
            _hurtboxBound = true;
        }

        public override void _ExitTree() {
            if (!_hurtboxBound) return;
            _hurtboxBound = false;
            if (_hurtbox != null) _hurtbox.OnHit -= OnNestHit;
        }

        public void Initialize(AbilityData data, PlayerController owner, bool aspsBite) {
            _data = data;
            _ownerPlayer = owner;
            OwnerIndex = owner?.PlayerIndex ?? 0;
            _currentHP = MaxNestHP;
            IsNestDestroyed = false;
            // Story-only PersistentDuration minors extend the nest's lifespan.
            _lifetime = (data?.Lifetime > 0f ? data.Lifetime : 12f)
                * (owner?.StoryPersistentDurationMultiplier ?? 1f);
            _biteInterval = (data?.DamageTickIntervalFrames ?? 120) / 60f;
            if (_biteInterval <= 0f) _biteInterval = 2f;
            // Asp's Bite (Story-only Resonance major perk): Venom deals double
            // damage to airborne targets; the potency doubles at bite time.
            _aspsBite = aspsBite;
            _biteTimer = _biteInterval;
            UpdateHPBar();
        }

        private void UpdateHPBar() {
            _hpBar ??= GetNodeOrNull<ProgressBar>("HPBar");
            if (_hpBar == null) return;
            _hpBar.MaxValue = MaxNestHP;
            _hpBar.Value = Mathf.Max(0, _currentHP);
            _hpBar.Visible = !IsNestDestroyed;
        }

        public void OnSpawn() { }

        public void OnDespawn() {
            if (_ownerPlayer != null && IsInstanceValid(_ownerPlayer)) {
                _ownerPlayer.ActivePersistentObjects.Remove(this);
            }
            _ownerPlayer = null;
            _data = null;
            _currentHP = 0;
            IsNestDestroyed = true;
            _aspsBite = false;
            _rewindFrozen = false;
        }

        public void SetStoryRewindFrozen(bool frozen) => _rewindFrozen = frozen;

        public override void _PhysicsProcess(double delta) {
            if (_rewindFrozen || IsNestDestroyed) return;
            float dt = (float)delta;

            _lifetime -= dt;
            if (_lifetime <= 0f) {
                DestroyNest();
                return;
            }

            _biteTimer -= dt;
            if (_biteTimer <= 0f) {
                _biteTimer = _biteInterval;
                BiteOverlappingTargets();
            }
        }

        private float OnNestHit(HitPayload payload) {
            if (IsNestDestroyed || payload.AttackerIndex == OwnerIndex) return 0f;
            int applied = Mathf.Clamp(Mathf.RoundToInt(payload.Damage), 0, _currentHP);
            _currentHP -= applied;
            UpdateHPBar();
            if (_currentHP <= 0) DestroyNest();
            return applied;
        }

        private void DestroyNest() {
            if (IsNestDestroyed) return;
            IsNestDestroyed = true;
            ReturnToPool();
        }

        private void BiteOverlappingTargets() {
            float damageMultiplier = _ownerPlayer != null && IsInstanceValid(_ownerPlayer)
                ? _ownerPlayer.StorySpecialDamageMultiplier
                : 1f;
            // Story-only StatusDamage minors raise the bite Venom's potency.
            float intensityMultiplier = _ownerPlayer != null && IsInstanceValid(_ownerPlayer)
                ? _ownerPlayer.StoryStatusIntensityMultiplier
                : 1f;
            foreach (Hurtbox hurtbox in QueryEnemyHurtboxes(GlobalPosition, BiteRangePixels)) {
                float venomIntensity = (_data?.StatusIntensity ?? 1f) * intensityMultiplier;
                if (_aspsBite && TargetIsAirborne(hurtbox)) venomIntensity *= 2f;
                float dealt = hurtbox.TakeHit(new HitPayload {
                    AttackerIndex = OwnerIndex,
                    AttackID = _data?.AbilityID ?? "cleopatra_serpent_nest",
                    HitboxID = "nest_bite",
                    AttackClass = AttackClass.Special,
                    Damage = (_data?.BaseDamage ?? 10f) * damageMultiplier,
                    Knockback = Vector2.Zero,
                    // The design's brief Root (1 s) lands as bite hitstun so the
                    // Venom status below is not immediately replaced.
                    HitstunDuration = RootHitstunSeconds,
                    HitOrigin = GlobalPosition,
                    AttackerFacingRight = true,
                    AppliedStatus = FTT.Core.StatusType.Venom,
                    StatusDuration = _data?.StatusDuration > 0f ? _data.StatusDuration : VenomDurationFallback,
                    StatusIntensity = venomIntensity,
                    ScreenShakeIntensity = 0.1f,
                    ScreenShakeDuration = 0.05f
                });
                CreditOwnerInfluence(dealt);
            }
        }

        private static bool TargetIsAirborne(Hurtbox hurtbox) {
            Node current = hurtbox.GetParent();
            while (current != null) {
                if (current is CharacterBody2D body) return !body.IsOnFloor();
                current = current.GetParent();
            }
            return false;
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

        private void CreditOwnerInfluence(float dealt) {
            if (dealt > 0f && _ownerPlayer != null && IsInstanceValid(_ownerPlayer)) {
                _ownerPlayer.AddInfluenceFromDamageDealt(dealt);
            }
        }
    }
}
