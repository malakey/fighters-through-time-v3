using Godot;
using FTT.Combat;
using FTT.Characters;

namespace FTT.Characters.Abilities {

    /// <summary>
    /// Story-mode Vine Snare construct (design Section 4 specification): a thrown
    /// seed pod grows into thorny vines with 15 HP, a 10 s lifespan, and a max of
    /// 2 active per owner. Enemies stepping into the vines are bitten for light
    /// damage plus Root for the authored 1.5 s (the newest status completely
    /// replaces the previous one); the snare re-bites a target once its Root has
    /// worn off. With the Story-only Thorn Snare Resonance perk, targets in the
    /// vines take periodic thorn damage while their Root is active. The snare is
    /// damageable/destroyable, persists across owner death, freezes during
    /// Chronal Rewind, and fully resets its pooled state.
    /// </summary>
    public partial class VineSnareNode : FTT.Core.PooledNode, FTT.Core.IPoolable, FTT.Environment.IStoryRewindSimulation {

        private const float TriggerRangePixels = 120f; // 2 world units at 60 px/unit; mirrors the Fighter sim AttackRange.
        private const int MaxSnareHP = 15;
        // Thorn Snare perk tick damage is a placeholder-tuning choice; the design
        // specifies "continuous damage" without a number.
        private const float ThornTickDamage = 2f;

        public int OwnerIndex { get; private set; }
        public bool IsSnareDestroyed { get; private set; }

        private PlayerController _ownerPlayer;
        private AbilityData _data;
        private int _currentHP;
        private float _lifetime;
        private float _biteInterval;
        private float _biteTimer;
        private bool _thornSnare;
        private bool _rewindFrozen;
        private Hurtbox _hurtbox;
        private bool _hurtboxBound;
        private ProgressBar _hpBar;

        // Pooled constructs re-enter the tree on every spawn cycle but _Ready runs
        // once, so the hurtbox subscription lives on the enter/exit pair (audit
        // H-3: a _Ready-only bind left the warmed snare indestructible after its
        // first reparent).
        public override void _EnterTree() {
            _hurtbox ??= GetNodeOrNull<Hurtbox>("Hurtbox");
            if (_hurtbox == null || _hurtboxBound) return;
            _hurtbox.OnHit += OnSnareHit;
            _hurtboxBound = true;
        }

        public override void _ExitTree() {
            if (!_hurtboxBound) return;
            _hurtboxBound = false;
            if (_hurtbox != null) _hurtbox.OnHit -= OnSnareHit;
        }

        public void Initialize(AbilityData data, PlayerController owner, bool thornSnare) {
            _data = data;
            _ownerPlayer = owner;
            OwnerIndex = owner?.PlayerIndex ?? 0;
            _currentHP = MaxSnareHP;
            IsSnareDestroyed = false;
            // Story-only PersistentDuration minors extend the snare's lifespan.
            _lifetime = (data?.Lifetime > 0f ? data.Lifetime : 10f)
                * (owner?.StoryPersistentDurationMultiplier ?? 1f);
            _biteInterval = (data?.DamageTickIntervalFrames ?? 30) / 60f;
            if (_biteInterval <= 0f) _biteInterval = 0.5f;
            _biteTimer = 0f;
            _thornSnare = thornSnare;
            UpdateHPBar();
        }

        private void UpdateHPBar() {
            _hpBar ??= GetNodeOrNull<ProgressBar>("HPBar");
            if (_hpBar == null) return;
            _hpBar.MaxValue = MaxSnareHP;
            _hpBar.Value = Mathf.Max(0, _currentHP);
            _hpBar.Visible = !IsSnareDestroyed;
        }

        public void OnSpawn() { }

        public void OnDespawn() {
            if (_ownerPlayer != null && IsInstanceValid(_ownerPlayer)) {
                _ownerPlayer.ActivePersistentObjects.Remove(this);
            }
            _ownerPlayer = null;
            _data = null;
            _currentHP = 0;
            IsSnareDestroyed = true;
            _thornSnare = false;
            _rewindFrozen = false;
        }

        public void SetStoryRewindFrozen(bool frozen) => _rewindFrozen = frozen;

        public override void _PhysicsProcess(double delta) {
            if (_rewindFrozen || IsSnareDestroyed) return;
            float dt = (float)delta;

            _lifetime -= dt;
            if (_lifetime <= 0f) {
                DestroySnare();
                return;
            }

            _biteTimer -= dt;
            if (_biteTimer <= 0f) {
                _biteTimer = _biteInterval;
                BiteTargetsInRange();
            }
        }

        private float OnSnareHit(HitPayload payload) {
            if (IsSnareDestroyed || payload.AttackerIndex == OwnerIndex) return 0f;
            int applied = Mathf.Clamp(Mathf.RoundToInt(payload.Damage), 0, _currentHP);
            _currentHP -= applied;
            UpdateHPBar();
            if (_currentHP <= 0) DestroySnare();
            return applied;
        }

        private void DestroySnare() {
            if (IsSnareDestroyed) return;
            IsSnareDestroyed = true;
            ReturnToPool();
        }

        /// <summary>
        /// Proximity bite: every tick, enemies inside the vines that are not
        /// already Rooted take light damage plus Root. Already-rooted targets are
        /// left alone unless the Thorn Snare perk is active, in which case they
        /// take continuous thorn damage while the Root holds them in place.
        /// </summary>
        private void BiteTargetsInRange() {
            float damageMultiplier = _ownerPlayer != null && IsInstanceValid(_ownerPlayer)
                ? _ownerPlayer.StorySpecialDamageMultiplier
                : 1f;
            foreach (Hurtbox hurtbox in QueryEnemyHurtboxes()) {
                bool rooted = TargetIsRooted(hurtbox);
                if (!rooted) {
                    float dealt = hurtbox.TakeHit(BuildHitPayload(
                        "bite",
                        (_data?.BaseDamage ?? 8f) * damageMultiplier,
                        _data?.AppliedStatus ?? FTT.Core.StatusType.Root,
                        _data?.StatusDuration > 0f ? _data.StatusDuration : 1.5f));
                    CreditOwnerInfluence(dealt);
                } else if (_thornSnare) {
                    float dealt = hurtbox.TakeHit(BuildHitPayload(
                        "thorn_tick",
                        ThornTickDamage * damageMultiplier,
                        FTT.Core.StatusType.None,
                        0f));
                    CreditOwnerInfluence(dealt);
                }
            }
        }

        private HitPayload BuildHitPayload(
            string hitboxID, float damage, FTT.Core.StatusType status, float statusDuration) => new() {
            AttackerIndex = OwnerIndex,
            AttackID = _data?.AbilityID ?? "pocahontas_vine_snare",
            HitboxID = hitboxID,
            AttackClass = AttackClass.Special,
            Damage = damage,
            Knockback = Vector2.Zero,
            HitstunDuration = 0f,
            HitOrigin = GlobalPosition,
            AttackerFacingRight = true,
            AppliedStatus = status,
            StatusDuration = statusDuration,
            StatusIntensity = _data?.StatusIntensity ?? 1f,
            ScreenShakeIntensity = 0.1f,
            ScreenShakeDuration = 0.05f
        };

        private static bool TargetIsRooted(Hurtbox hurtbox) {
            Node current = hurtbox.GetParent();
            while (current != null) {
                if (current is PlayerController player) {
                    var status = player.GetNodeOrNull<StatusController>("StatusController");
                    return status?.HasStatus(FTT.Core.StatusType.Root) == true;
                }
                if (current is FTT.Enemies.EnemyController enemy) {
                    return enemy.HasStatusEffect(FTT.Core.StatusType.Root);
                }
                current = current.GetParent();
            }
            return false;
        }

        private System.Collections.Generic.List<Hurtbox> QueryEnemyHurtboxes() {
            var results = new System.Collections.Generic.List<Hurtbox>();
            var space = GetWorld2D()?.DirectSpaceState;
            if (space == null) return results;
            uint targetHurtboxLayer = OwnerIndex == 0
                ? FTT.Core.CollisionLayers.EnemyHurtbox
                : FTT.Core.CollisionLayers.PlayerHurtbox;
            var query = new PhysicsShapeQueryParameters2D {
                Shape = new CircleShape2D { Radius = TriggerRangePixels },
                Transform = new Transform2D(0f, GlobalPosition),
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
    }
}
