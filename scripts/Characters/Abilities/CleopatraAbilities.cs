using Godot;
using FTT.Combat;
using FTT.Characters;

namespace FTT.Characters.Abilities {

    /// <summary>
    /// Special 1 — Serpent Nest: deploys a persistent nest construct (15 HP, 12 s,
    /// max 1 active). Spectral asps bite enemies passing over the nest for light
    /// damage, one second of immobilizing hitstun (the design's brief Root under
    /// the single-status rule), and Venom for 4 s. Story-only Resonance perk
    /// Asp's Bite doubles Venom potency against airborne targets.
    /// </summary>
    public partial class CleopatraSerpentNest : BaseSpecial {

        public const string AspsBitePerkKey = "asps_bite";

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
            DeployNest();
        }

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
        }

        private void DeployNest() {
            if (Owner == null) return;
            if (Data?.PersistentObjectScene == null) {
                GD.PushWarning("Serpent Nest has no PersistentObjectScene authored; deploy skipped.");
                return;
            }

            int maxActive = Data.MaxActiveObjects > 0 ? Data.MaxActiveObjects : 1;
            while (CountActiveNests() >= maxActive) {
                SerpentNestNode oldest = FindOldestNest();
                if (oldest == null) break;
                Owner.ActivePersistentObjects.Remove(oldest);
                oldest.ReturnToPool();
            }

            Node spawned = FTT.Core.PoolManager.Instance?.Spawn(
                Data.PersistentObjectScene,
                Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? 80f : -80f, 0f),
                Owner.GetParent());
            if (spawned is not SerpentNestNode nest) return;

            nest.Initialize(Data, Owner, Owner.HasStoryPerk(AspsBitePerkKey));
            Owner.ActivePersistentObjects.Add(nest);
        }

        private int CountActiveNests() {
            int count = 0;
            foreach (Node2D node in Owner.ActivePersistentObjects) {
                if (node is SerpentNestNode nest && IsInstanceValid(nest) && !nest.IsNestDestroyed) count++;
            }
            return count;
        }

        private SerpentNestNode FindOldestNest() {
            foreach (Node2D node in Owner.ActivePersistentObjects) {
                if (node is SerpentNestNode nest && IsInstanceValid(nest) && !nest.IsNestDestroyed) return nest;
            }
            return null;
        }
    }

    /// <summary>
    /// Special 2 — Sandstorm Vortex: a swirling sand zone ahead of Cleopatra that
    /// pulls caught targets toward its center (a steady horizontal positional
    /// drag that never zeroes velocity), deals the authored tick damage (5 ticks
    /// at 0.4 s across the 2 s lifetime; the final tick carries the authored
    /// launch so escaping the sand costs something), and applies TimeDilation
    /// (-40% speed at intensity 0.8) for 2 s. Targets are hit through the shared
    /// Hurtbox contract, so enemies and fighters both respond. Story-only
    /// Resonance perk Quicksand Grip roots vortex-caught targets when Cleopatra
    /// casts Desert Mirage.
    /// </summary>
    public partial class CleopatraSandstormVortex : BaseSpecial {

        public const float VortexRadiusPixels = 120f;
        // Mirrors the Fighter sim's 0.05 units-per-frame pull (3 px/frame).
        private const float PullPixelsPerSecond = 180f;

        private bool _vortexActive;
        private Vector2 _vortexCenter;
        private float _vortexRadius = VortexRadiusPixels;
        private float _vortexLifetime;
        private float _vortexTickInterval;
        private float _vortexTickTimer;

        /// <summary>Whether the deployed vortex is still churning (test observable).</summary>
        public bool VortexActive => _vortexActive;
        /// <summary>The deployed vortex's effective radius after Story minors (test observable).</summary>
        public float ActiveVortexRadiusPixels => _vortexRadius;
        /// <summary>The deployed vortex centre in world space (test observable).</summary>
        public Vector2 ActiveVortexCenter => _vortexCenter;

        /// <summary>
        /// Story-only Resonance TRAVERSAL flag (V7.6, Tier 2, F06 Option A).
        /// Casting Desert Mirage from INSIDE Cleopatra's own Sandstorm Vortex
        /// halves that cast's cooldown, after every other modifier.
        /// </summary>
        public const string VortexStepPerkKey = "vortex_step";

        /// <summary>V7.6 Vortex Step: the multiplier applied to that cast's cooldown.</summary>
        public const float VortexStepCooldownScale = 0.5f;

        /// <summary>
        /// The per-INSTANCE allowance. A vortex grants exactly one Vortex Step;
        /// a new vortex gets a fresh allowance, and restoring this same vortex
        /// (a rewind) keeps whatever it had left. Overlapping vortices cannot
        /// stack because the consumer picks exactly one deterministically.
        /// </summary>
        public bool VortexStepAvailable { get; private set; }

        /// <summary>
        /// Consumes this vortex's Vortex Step allowance ATOMICALLY. Returns
        /// true only when the vortex is live, the allowance is unspent, and
        /// <paramref name="castPosition"/> is inside the churn — so a refused
        /// input spends nothing, and an interruption AFTER an accepted cast
        /// still leaves the allowance spent.
        /// </summary>
        public bool TryConsumeVortexStep(Vector2 castPosition) {
            if (!_vortexActive || !VortexStepAvailable) return false;
            if (castPosition.DistanceTo(_vortexCenter) > _vortexRadius) return false;
            VortexStepAvailable = false;
            return true;
        }

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
            SpawnVortex();
        }

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
        }

        private void SpawnVortex() {
            if (Owner == null) return;
            _vortexCenter = Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? 100f : -100f, 0f);
            // Story-only minors: "Sand Radius +15%" (cleopatra_dm1, ZoneRadius)
            // widens the vortex; "Sand Duration +20%" (cleopatra_dm2,
            // ZoneDuration) lengthens it. Neutral 1f outside Story Mode.
            // Package 11 A4: V7.6 retires cleopatra_dm1 (ZoneRadius) outright
            // and re-scopes the duration minor onto the SERPENT NEST, so the
            // vortex keeps its authored radius and lifetime. The generic lanes
            // stay in the expression as neutral 1.0 carriers for any future
            // character-wide authoring.
            _vortexRadius = VortexRadiusPixels * Owner.StoryZoneRadiusMultiplier;
            _vortexLifetime = (Data?.Lifetime > 0f ? Data.Lifetime : 2f)
                * Owner.StoryZoneDurationMultiplier;
            _vortexTickInterval = (Data?.DamageTickIntervalFrames ?? 24) / 60f;
            if (_vortexTickInterval <= 0f) _vortexTickInterval = 0.4f;
            _vortexTickTimer = _vortexTickInterval;
            _vortexActive = true;
            // Fresh instance, fresh Vortex Step allowance.
            VortexStepAvailable = true;

            // Presentation only: damage, status, and the pull all run through the
            // hurtbox queries below so enemies participate alongside fighters.
            SpawnPlaceholderZone(
                _vortexCenter,
                0f,
                _vortexLifetime,
                1f,
                new Color(0.8f, 0.7f, 0.3f),
                _vortexRadius);
        }

        public override void _PhysicsProcess(double delta) {
            base._PhysicsProcess(delta);
            UpdateVortex((float)delta);
        }

        private void UpdateVortex(float dt) {
            if (!_vortexActive || Owner == null) return;

            PullTargetsTowardCenter(dt);

            _vortexTickTimer -= dt;
            if (_vortexTickTimer <= 0f) {
                _vortexTickTimer += _vortexTickInterval;
                // The last tick before the vortex dissipates carries the authored
                // launch so escaping the sand costs something; earlier ticks stay
                // impulse-free so the pull keeps its grip (V7 tuning batch).
                TickVortexDamage(finalTick: _vortexLifetime <= _vortexTickInterval);
            }

            _vortexLifetime -= dt;
            if (_vortexLifetime <= 0f) _vortexActive = false;
        }

        private void PullTargetsTowardCenter(float dt) {
            float step = PullPixelsPerSecond * dt;
            foreach (Hurtbox hurtbox in QueryTargetHurtboxes()) {
                CharacterBody2D body = FindTargetBody(hurtbox);
                if (body == null) continue;
                float dx = _vortexCenter.X - body.GlobalPosition.X;
                if (Mathf.Abs(dx) <= step) {
                    body.GlobalPosition = new Vector2(_vortexCenter.X, body.GlobalPosition.Y);
                } else {
                    body.GlobalPosition += new Vector2(Mathf.Sign(dx) * step, 0f);
                }
            }
        }

        private void TickVortexDamage(bool finalTick) {
            foreach (Hurtbox hurtbox in QueryTargetHurtboxes()) ApplyVortexTickTo(hurtbox, finalTick);
        }

        /// <summary>
        /// One Vortex tick against one caught hurtbox; returns the HP it dealt.
        /// Internal so the D03g pin can deliver a tick without a physics query.
        /// </summary>
        internal float ApplyVortexTickTo(Hurtbox hurtbox, bool finalTick) {
                // VERIFY-VORTEX-RECLAIM (Package 12 W4, D03g): every Vortex tick,
                // the final launching tick included, is Tick delivery by
                // construction and reclaims NO Rally. The credit reads the
                // stamped payload, never a literal flag.
                HitPayload tick = Stamp(new HitPayload {
                    AttackerIndex = Owner.PlayerIndex,
                    AttackID = Data?.AbilityID ?? "cleopatra_sandstorm_vortex",
                    HitboxID = "vortex_tick",
                    AttackClass = AttackClass.Special,
                    Damage = (Data?.BaseDamage ?? 2f) * Owner.StorySpecialDamageMultiplier,
                    Knockback = finalTick ? Data?.KnockbackForce ?? Vector2.Zero : Vector2.Zero,
                    HitstunDuration = finalTick ? Data?.HitstunDuration ?? 0.2f : 0f,
                    HitOrigin = _vortexCenter,
                    AttackerFacingRight = Owner.IsFacingRight,
                    AppliedStatus = Data?.AppliedStatus ?? FTT.Core.StatusType.TimeDilation,
                    StatusDuration = Data?.StatusDuration > 0f ? Data.StatusDuration : 2f,
                    StatusIntensity = Data?.StatusIntensity ?? 0.8f,
                    ScreenShakeIntensity = 0.05f,
                    ScreenShakeDuration = 0.05f
                }, HitDelivery.Tick);
                // Only the final tick carries the authored launch; the churn ticks never do.
                tick.Launches = finalTick && (Data?.Launches ?? false);
                float dealt = hurtbox.TakeHit(tick);
                Credit(in tick, dealt);
                return dealt;
        }

        /// <summary>
        /// Quicksand Grip (Story-only Resonance major perk): roots every target
        /// currently caught in the vortex for the given duration when Cleopatra
        /// casts Desert Mirage. The Root is applied directly to the target's
        /// status handler because zero-damage hits do not carry status through
        /// the player damage gate.
        /// </summary>
        public void RootVortexTargets(float rootDuration) {
            if (!_vortexActive || Owner == null) return;
            foreach (Hurtbox hurtbox in QueryTargetHurtboxes()) {
                Node current = hurtbox.GetParent();
                while (current != null) {
                    if (current is PlayerController player) {
                        player.GetNodeOrNull<StatusController>("StatusController")
                            ?.ApplyStatus(FTT.Core.StatusType.Root, rootDuration);
                        break;
                    }
                    if (current is FTT.Enemies.EnemyController enemy) {
                        enemy.ApplyStatusEffect(FTT.Core.StatusType.Root, rootDuration);
                        break;
                    }
                    current = current.GetParent();
                }
            }
        }

        private static CharacterBody2D FindTargetBody(Hurtbox hurtbox) {
            Node current = hurtbox.GetParent();
            while (current != null) {
                if (current is CharacterBody2D body) return body;
                current = current.GetParent();
            }
            return null;
        }

        private System.Collections.Generic.List<Hurtbox> QueryTargetHurtboxes() {
            var results = new System.Collections.Generic.List<Hurtbox>();
            var space = Owner?.GetWorld2D()?.DirectSpaceState;
            if (space == null) return results;
            uint targetHurtboxLayer = Owner.PlayerIndex == 0
                ? FTT.Core.CollisionLayers.EnemyHurtbox
                : FTT.Core.CollisionLayers.PlayerHurtbox;
            var query = new PhysicsShapeQueryParameters2D {
                Shape = new CircleShape2D { Radius = _vortexRadius },
                Transform = new Transform2D(0f, _vortexCenter),
                CollideWithAreas = true,
                CollideWithBodies = false,
                CollisionMask = targetHurtboxLayer
            };
            foreach (Godot.Collections.Dictionary result in space.IntersectShape(query, 16)) {
                if (result["collider"].AsGodotObject() is Hurtbox hurtbox
                    && hurtbox.OwnerPlayerIndex != Owner.PlayerIndex) {
                    results.Add(hurtbox);
                }
            }
            return results;
        }
    }

    /// <summary>
    /// Movement — Desert Mirage: Cleopatra dissolves into sand and rushes a short
    /// distance in the held input direction, usable in the air for recovery.
    /// Distance, duration (capped at the design's 3 s limit), and cooldown come
    /// from the authored MovementAbilityData resource. Every accepted cast
    /// leaves a one-second sand decoy at its origin that Story enemies target
    /// (<see cref="SandDecoyNode"/>, Package 12 W4). Story-only Resonance
    /// perks: Quicksand Grip (vortex-caught targets are rooted 1 s on cast) and
    /// Royal Aegis (a shield worth 10% of max HP granted on cast, to Cleopatra
    /// and — as a separate recipient — to the decoy).
    /// </summary>
    public partial class CleopatraDesertMirage : BaseSpecial {

        public const string QuicksandGripPerkKey = "quicksand_grip";
        public const string RoyalAegisPerkKey = "royal_aegis";

        private const float MaxMirageDuration = 3.0f;
        private const float QuicksandRootDuration = 1.0f;

        private Vector2 _mirageDirection;
        private Vector2 _startPosition;
        private float _mirageDuration = 0.3f;
        private float _mirageDistance = 180f;

        private MovementAbilityData MovementData => Data as MovementAbilityData;

        private bool _vortexStepApplied;
        /// <summary>D02a grant identity: one per ACCEPTED Desert Mirage cast.</summary>
        private int _mirageCastId;

        /// <summary>True when this cast consumed a Vortex Step. Test seam.</summary>
        public bool VortexStepApplied => _vortexStepApplied;

        /// <summary>
        /// Vortex Step's cooldown scale for THIS cast: half after every other
        /// modifier, or neutral when no allowance was consumed. Read by
        /// BaseSpecial when it arms the cooldown, so the halving never rewrites
        /// a cooldown that has already started and never shortens the cast
        /// animation.
        /// </summary>
        public override float CooldownScaleForThisCast =>
            _vortexStepApplied ? CleopatraSandstormVortex.VortexStepCooldownScale : 1f;

        /// <summary>
        /// The owner's own live Sandstorm Vortex, if any. Deterministic pick:
        /// the first Special-slot vortex in child order, so overlapping
        /// vortices can never consume more than one allowance.
        /// </summary>
        private CleopatraSandstormVortex FindOwnVortex() {
            if (Owner == null) return null;
            foreach (Node child in Owner.GetChildren()) {
                if (child is CleopatraSandstormVortex vortex && vortex.VortexActive) return vortex;
            }
            return null;
        }

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
            _mirageDuration = Mathf.Min(
                MovementData?.MovementDuration > 0f ? MovementData.MovementDuration : 0.3f,
                MaxMirageDuration);
            _mirageDistance = MovementData?.DistanceMoved > 0f ? MovementData.DistanceMoved : 180f;

            float hInput = Owner.CurrentInputFrame.Horizontal;
            // Negative vertical aims upward (Godot 2D Y is down); since §2.7 it
            // comes from the Up input rather than a held Jump.
            float vInput = Owner.CurrentInputFrame.Vertical;
            _mirageDirection = new Vector2(hInput, vInput);
            if (_mirageDirection == Vector2.Zero) {
                _mirageDirection = Owner.IsFacingRight ? Vector2.Right : Vector2.Left;
            }
            _mirageDirection = _mirageDirection.Normalized();
            _startPosition = Owner.GlobalPosition;
            // Vortex Step (traversal): consumed atomically at the accepted
            // cast, BEFORE the cooldown is armed in BaseSpecial, and never
            // twice for one vortex instance.
            _vortexStepApplied = Owner.HasStoryPerk(CleopatraSandstormVortex.VortexStepPerkKey)
                && FindOwnVortex()?.TryConsumeVortexStep(Owner.GlobalPosition) == true;
            ApplyCastPerks();
        }

        protected override void OnActive() {
            PhaseTimer = _mirageDuration;
        }

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
            FTT.Core.EventBus.Instance?.RaiseMovementAbilityUsed(new FTT.Core.MovementAbilityPayload {
                PlayerIndex = Owner.PlayerIndex,
                AbilityName = Data?.AbilityName ?? "Desert Mirage",
                StartPosition = _startPosition,
                EndPosition = Owner.GlobalPosition
            });
        }

        public override void _PhysicsProcess(double delta) {
            if (CurrentPhase == AbilityPhase.Active) {
                float speed = _mirageDistance / _mirageDuration;
                Owner.Velocity = _mirageDirection * speed;
            }
            base._PhysicsProcess(delta);
        }

        private void ApplyCastPerks() {
            if (Owner.HasStoryPerk(QuicksandGripPerkKey)) {
                var vortex = Owner.GetNodeOrNull<CleopatraSandstormVortex>("Special2");
                vortex?.RootVortexTargets(QuicksandRootDuration);
            }
            // Package 12 W4 (GAP-10c): every accepted Mirage leaves the sand
            // decoy at its origin for one second (baseline kit rule).
            SandDecoyNode decoy = EnsureDecoy();
            decoy.Arm(Owner, _startPosition);
            int castId = ++_mirageCastId;
            if (Owner.HasStoryPerk(RoyalAegisPerkKey)) {
                // V7.6 D02a/D02c (Package 11 A1b): a valid Desert Mirage grant
                // REFRESHES the 10%-max-HP shield to its cap and restarts the
                // eight-second lifetime; it never adds capacity or duration.
                // The grant identity is the accepted cast, so a duplicate
                // animation or collision callback for the same cast is refused.
                float capacity = FTT.Combat.StoryDefenseRules.GrantedShieldCapacityShare * Owner.MaximumHP;
                Owner.GrantStoryShield(
                    FTT.Combat.StoryShieldEffect.RoyalAegis,
                    capacity,
                    FTT.Combat.StoryDefenseRules.GrantedShieldLifetimeFrames,
                    castId);
                // D02a: "the decoy inherits it as a SEPARATE recipient" — its own
                // capacity, depletion, lifetime and grant identity, never pooled
                // with Cleopatra's (Package 12 W4 closes the P11 A1b deviation).
                decoy.GrantShield(
                    capacity, FTT.Combat.StoryDefenseRules.GrantedShieldLifetimeFrames, castId);
            }
        }

        private SandDecoyNode _decoy;

        /// <summary>The one reusable decoy under this ability. Test seam.</summary>
        public SandDecoyNode Decoy => _decoy;

        private SandDecoyNode EnsureDecoy() {
            if (_decoy != null && IsInstanceValid(_decoy)) return _decoy;
            _decoy = new SandDecoyNode { Name = "SandDecoy" };
            AddChild(_decoy);
            return _decoy;
        }
    }

    /// <summary>
    /// Ultimate — Wrath of the Nile: a massive sandstorm/flood that engulfs the
    /// arena around Cleopatra. The storm deals the authored HitCount ticks
    /// (10 x 8 base at a 21-frame interval across the 210-frame active window)
    /// through hurtbox queries with the shield-bypassing Ultimate attack class,
    /// and the final tick leaves every caught target with the authored heavy
    /// Venom (intensity 1.5 for 5 s). Venom rides only the last tick because
    /// under the single-status rule earlier ticks would keep resetting the DoT
    /// timer while the storm rages; applying it last lets the full-duration
    /// poison survive the storm. Requires a full Ultimate meter, consumed on
    /// cast.
    /// </summary>
    public partial class CleopatraWrathOfTheNile : BaseSpecial {

        // 600 px = the Fighter zone's 10-unit arena half-width at 60 px/unit.
        public const float StormRadiusPixels = 600f;

        private UltimateMeter _meter;
        private int _hitsDone;
        private int _tickFramesRemaining;
        private Vector2 _stormCenter;

        private int HitCap => Data?.HitCount > 0 ? Data.HitCount : 10;
        private int TickIntervalFrames =>
            Data?.DamageTickIntervalFrames > 0 ? Data.DamageTickIntervalFrames : 21;

        public override void _Ready() {
            base._Ready();
            _meter = Owner?.GetNodeOrNull<UltimateMeter>("UltimateMeter");
        }

        protected override bool Validate() {
            return base.Validate() && (_meter?.IsFull ?? false);
        }

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
            _hitsDone = 0;
            Owner.Velocity = Vector2.Zero;
            _meter?.Consume();
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
            _stormCenter = Owner.GlobalPosition;
            _tickFramesRemaining = TickIntervalFrames;
            // Presentation only: damage and the heavy Venom run through the
            // hurtbox queries below so enemies participate alongside fighters.
            SpawnPlaceholderZone(
                _stormCenter,
                0f,
                Data?.Lifetime > 0f ? Data.Lifetime : 3.5f,
                1f,
                new Color(0.85f, 0.65f, 0.25f),
                StormRadiusPixels);
        }

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
        }

        public override void _PhysicsProcess(double delta) {
            if (CurrentPhase == AbilityPhase.Active && _hitsDone < HitCap) {
                _tickFramesRemaining--;
                if (_tickFramesRemaining <= 0) {
                    _tickFramesRemaining = TickIntervalFrames;
                    _hitsDone++;
                    DealStormTick(_hitsDone >= HitCap);
                }
            }
            base._PhysicsProcess(delta);
        }

        private void DealStormTick(bool finalTick) {
            foreach (Hurtbox hurtbox in QueryTargetHurtboxes()) {
                HitPayload hit = Stamp(new HitPayload {
                    AttackerIndex = Owner.PlayerIndex,
                    AttackID = Data?.AbilityID ?? "cleopatra_wrath_of_the_nile",
                    HitboxID = "sandstorm_tick",
                    AttackClass = AttackClass.Ultimate,
                    Damage = (Data?.BaseDamage ?? 8f) * Owner.StorySpecialDamageMultiplier,
                    Knockback = Vector2.Zero,
                    HitstunDuration = 0f,
                    HitOrigin = _stormCenter,
                    AttackerFacingRight = Owner.IsFacingRight,
                    AppliedStatus = finalTick
                        ? Data?.AppliedStatus ?? FTT.Core.StatusType.Venom
                        : FTT.Core.StatusType.None,
                    StatusDuration = Data?.StatusDuration > 0f ? Data.StatusDuration : 5f,
                    // Story-only StatusDamage minors raise the heavy Venom's potency.
                    StatusIntensity = (Data?.StatusIntensity ?? 1.5f)
                        * (finalTick ? Owner.StoryStatusIntensityMultiplier : 1f),
                    ScreenShakeIntensity = Data?.ScreenShakeIntensity ?? 0.6f,
                    ScreenShakeDuration = Data?.ScreenShakeDuration ?? 0.3f
                });
                float dealt = hurtbox.TakeHit(hit);
                // V7.6 D03h (Package 11 A1b): Ultimate-origin damage awards its caster
                // ZERO damage-dealt meter, regardless of HP removed, target count or
                // when it lands. Direct-hit Rally reclaim is retained (D03g).
                Credit(in hit, dealt);
            }
        }

        private System.Collections.Generic.List<Hurtbox> QueryTargetHurtboxes() {
            var results = new System.Collections.Generic.List<Hurtbox>();
            var space = Owner?.GetWorld2D()?.DirectSpaceState;
            if (space == null) return results;
            uint targetHurtboxLayer = Owner.PlayerIndex == 0
                ? FTT.Core.CollisionLayers.EnemyHurtbox
                : FTT.Core.CollisionLayers.PlayerHurtbox;
            var query = new PhysicsShapeQueryParameters2D {
                Shape = new CircleShape2D { Radius = StormRadiusPixels },
                Transform = new Transform2D(0f, _stormCenter),
                CollideWithAreas = true,
                CollideWithBodies = false,
                CollisionMask = targetHurtboxLayer
            };
            foreach (Godot.Collections.Dictionary result in space.IntersectShape(query, 32)) {
                if (result["collider"].AsGodotObject() is Hurtbox hurtbox
                    && hurtbox.OwnerPlayerIndex != Owner.PlayerIndex) {
                    results.Add(hurtbox);
                }
            }
            return results;
        }
    }
}
