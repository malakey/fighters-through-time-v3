using Godot;
using FTT.Combat;
using FTT.Characters;

namespace FTT.Characters.Abilities {

    /// <summary>
    /// Special 1 — Golden Ratio: draws a glowing Fibonacci spiral at the cast point
    /// that expands outward, dealing the authored damage per tick for up to the
    /// authored hit count (design: 10 damage x 3 ticks for 30 total) with a radial
    /// knockback away from the spiral center on the final tick. All tuning comes
    /// from the authored AbilityData resource. Story-only Resonance perk Master
    /// Stroke adds +15% spiral damage and pulls enemies slightly toward the spiral
    /// center on each tick.
    /// </summary>
    public partial class LeonardoGoldenRatio : BaseSpecial {

        public const string MasterStrokePerkKey = "master_stroke";

        private const float StartRadiusPixels = 40f;
        private const float MaxRadiusPixels = 120f;   // 2 world units; mirrors the Fighter zone footprint.
        private const float MasterStrokeDamageMultiplier = 1.15f;
        private const float MasterStrokePullPixels = 30f;
        private const float MasterStrokeStopDistancePixels = 20f;

        private bool _spiralActive;
        private Vector2 _spiralCenter;
        private float _tickInterval = 0.5f;
        private float _tickTimer;
        private int _ticksRemaining;
        private float _radius;
        private float _maxRadius = MaxRadiusPixels;
        private float _growthPerSecond;

        /// <summary>The expanding spiral's current radius after Story minors (test observable).</summary>
        public float ActiveSpiralRadiusPixels => _radius;

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
            BeginSpiral();
        }

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
        }

        private void BeginSpiral() {
            if (Owner == null) return;
            _spiralCenter = Owner.GlobalPosition;
            _tickInterval = (Data?.DamageTickIntervalFrames ?? 30) / 60f;
            if (_tickInterval <= 0f) _tickInterval = 0.5f;
            _ticksRemaining = Data?.IsMultiHit == true ? Mathf.Max(1, Data.HitCount) : 3;
            // Package 11 A4: V7.6 re-scopes "Minor Spiral Range" from the
            // character-wide ZoneRadius lane onto
            // AbilityRange(leonardo_golden_ratio).
            float radiusScale = Owner.StoryZoneRadiusMultiplier
                * Owner.StoryScoped("AbilityRange", "leonardo_golden_ratio");
            float maxRadius = MaxRadiusPixels * radiusScale;
            // First tick lands immediately (next physics step); the spiral reaches
            // full radius by the final tick.
            _tickTimer = 0f;
            _radius = StartRadiusPixels * radiusScale;
            _maxRadius = maxRadius;
            _growthPerSecond = _ticksRemaining > 1
                ? (maxRadius - _radius) / ((_ticksRemaining - 1) * _tickInterval)
                : 0f;
            _spiralActive = true;

            // Placeholder spiral visual only; damage runs through the shape queries
            // below so per-target perk behavior can be evaluated.
            SpawnPlaceholderZone(
                _spiralCenter, 0f,
                Data?.Lifetime > 0f ? Data.Lifetime : 1.5f, 1f,
                new Color(0.85f, 0.7f, 0.25f), maxRadius);
        }

        public override void _PhysicsProcess(double delta) {
            base._PhysicsProcess(delta);
            if (!_spiralActive) return;
            if (Owner == null || !IsInstanceValid(Owner)) { _spiralActive = false; return; }

            float dt = (float)delta;
            _radius = Mathf.Min(_radius + _growthPerSecond * dt, _maxRadius);
            _tickTimer -= dt;
            if (_tickTimer > 0f) return;
            _tickTimer = _tickInterval;
            TickSpiral();
            _ticksRemaining--;
            if (_ticksRemaining <= 0) _spiralActive = false;
        }

        private void TickSpiral() {
            var space = Owner.GetWorld2D()?.DirectSpaceState;
            if (space == null) return;

            bool masterStroke = Owner.HasStoryPerk(MasterStrokePerkKey);
            bool finalTick = _ticksRemaining == 1;
            float damage = (Data?.BaseDamage ?? 10f)
                * (masterStroke ? MasterStrokeDamageMultiplier : 1f)
                * Owner.StorySpecialDamageMultiplier;

            uint targetHurtboxLayer = Owner.PlayerIndex == 0
                ? FTT.Core.CollisionLayers.EnemyHurtbox
                : FTT.Core.CollisionLayers.PlayerHurtbox;
            var query = new PhysicsShapeQueryParameters2D {
                Shape = new CircleShape2D { Radius = _radius },
                Transform = new Transform2D(0f, _spiralCenter),
                CollideWithAreas = true,
                CollideWithBodies = false,
                CollisionMask = StoryShapeQuery.DeliveryMask(targetHurtboxLayer)
            };

            foreach (Godot.Collections.Dictionary result in space.IntersectShape(query, 16)) {
                if (result["collider"].AsGodotObject() is not Hurtbox hurtbox) continue;
                if (hurtbox.OwnerPlayerIndex == Owner.PlayerIndex) continue;

                // Master Stroke (Story-only Resonance major perk): each tick drags
                // the target slightly toward the spiral center.
                if (masterStroke) PullTargetTowardCenter(hurtbox);

                // Radial knockback: the final tick shoves the target away from the
                // spiral center; earlier ticks are impulse-free damage pulses.
                bool pushRight = hurtbox.GlobalPosition.X >= _spiralCenter.X;
                float dealt = hurtbox.TakeHit(new HitPayload {
                    AttackerIndex = Owner.PlayerIndex,
                    AttackID = Data?.AbilityID ?? "leonardo_golden_ratio",
                    HitboxID = finalTick ? "spiral_burst" : "spiral_tick",
                    AttackClass = AttackClass.Special,
                    Damage = damage,
                    Knockback = finalTick ? (Data?.KnockbackForce ?? new Vector2(3, -2)) : Vector2.Zero,
                    HitstunDuration = finalTick ? (Data?.HitstunDuration ?? 0.2f) : 0.1f,
                    HitOrigin = _spiralCenter,
                    AttackerFacingRight = pushRight,
                    AppliedStatus = FTT.Core.StatusType.None,
                    StatusDuration = 0f,
                    StatusIntensity = 1f,
                    ScreenShakeIntensity = Data?.ScreenShakeIntensity ?? 0.2f,
                    ScreenShakeDuration = Data?.ScreenShakeDuration ?? 0.15f
                });
                if (dealt > 0f) Owner.AddInfluenceFromDamageDealt(dealt);
            }
        }

        private void PullTargetTowardCenter(Hurtbox hurtbox) {
            Node current = hurtbox.GetParent();
            while (current != null) {
                if (current is CharacterBody2D body) {
                    Vector2 toCenter = _spiralCenter - body.GlobalPosition;
                    float distance = toCenter.Length();
                    if (distance > MasterStrokeStopDistancePixels) {
                        float step = Mathf.Min(MasterStrokePullPixels, distance - MasterStrokeStopDistancePixels);
                        body.GlobalPosition += toCenter.Normalized() * step;
                    }
                    return;
                }
                current = current.GetParent();
            }
        }
    }

    /// <summary>
    /// Special 2 — Clockwork Turret: deploys a persistent automated turret
    /// construct (20 HP, 15 s, max 1 active) that fires ballista bolts at the
    /// nearest enemy and self-destructs after its third bolt. Numbers come from
    /// the authored AbilityData resource and the design Section 4 turret
    /// specification. Story-only Resonance perk Clockwork Overdrive upgrades the
    /// turret to a rapid burst of 5 bolts before self-destructing.
    /// </summary>
    public partial class LeonardoClockworkTurret : BaseSpecial {

        public const string ClockworkOverdrivePerkKey = "clockwork_overdrive";

        /// <summary>
        /// Story-only Resonance TRAVERSAL flag (V7.6, Tier 2): a deployed
        /// Clockwork Turret can be picked up and RE-PLACED once. The re-place
        /// moves the existing turret rather than deploying a second one, so it
        /// never widens the deploy limit, and it keeps the turret's remaining
        /// bolts and health - only its position changes. Once per turret.
        /// </summary>
        public const string ReplacementPerkKey = "turret_replacement";

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
            DeployTurret();
        }

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
        }

        private void DeployTurret() {
            if (Owner == null) return;
            if (Data?.PersistentObjectScene == null) {
                GD.PushWarning("Clockwork Turret has no PersistentObjectScene authored; deploy skipped.");
                return;
            }

            // Re-placement (traversal node, V7.6): a live turret that still
            // has its one allowance is PICKED UP and re-placed instead of
            // being recycled for a fresh deploy. The deploy limit is untouched
            // and the turret keeps its bolts and health.
            if (Owner.HasStoryPerk(ReplacementPerkKey) && TryReplaceTurret()) return;

            int maxActive = Data.MaxActiveObjects > 0 ? Data.MaxActiveObjects : 1;
            while (CountActiveTurrets() >= maxActive) {
                LeonardoTurretNode oldest = FindOldestTurret();
                if (oldest == null) break;
                Owner.ActivePersistentObjects.Remove(oldest);
                oldest.ReturnToPool();
            }

            Node spawned = FTT.Core.PoolManager.Instance?.Spawn(
                Data.PersistentObjectScene,
                Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? 70f : -70f, 0f),
                Owner.GetParent());
            if (spawned is not LeonardoTurretNode turret) return;

            turret.Initialize(Data, Owner, Owner.HasStoryPerk(ClockworkOverdrivePerkKey));
            Owner.ActivePersistentObjects.Add(turret);
        }

        /// <summary>
        /// Consumes a live turret's single re-placement allowance and moves it
        /// to the new deploy position. Returns false when no live turret has an
        /// allowance left, in which case the ordinary deploy path runs.
        /// </summary>
        private bool TryReplaceTurret() {
            foreach (Node2D node in Owner.ActivePersistentObjects) {
                if (node is not LeonardoTurretNode turret) continue;
                if (!IsInstanceValid(turret) || turret.IsTurretDestroyed) continue;
                if (!turret.TryConsumeReplacement()) continue;
                turret.GlobalPosition =
                    Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? 70f : -70f, 0f);
                return true;
            }
            return false;
        }

        private int CountActiveTurrets() {
            int count = 0;
            foreach (Node2D node in Owner.ActivePersistentObjects) {
                if (node is LeonardoTurretNode turret && IsInstanceValid(turret) && !turret.IsTurretDestroyed) count++;
            }
            return count;
        }

        private LeonardoTurretNode FindOldestTurret() {
            foreach (Node2D node in Owner.ActivePersistentObjects) {
                if (node is LeonardoTurretNode turret && IsInstanceValid(turret) && !turret.IsTurretDestroyed) return turret;
            }
            return null;
        }
    }

    /// <summary>
    /// Movement — Ornithopter Flight: mechanical bat wings give a vertical boost,
    /// then up to the authored glide duration of horizontal glide (design: 3 s),
    /// usable in the air for recovery. Boost/glide speed, glide duration, and
    /// cooldown come from the authored MovementAbilityData resource. Story-only
    /// Resonance perk Daedalus Wings lets the glide cancel directly into a
    /// downward melee dive.
    /// </summary>
    public partial class LeonardoOrnithopterFlight : BaseSpecial {

        public const string DaedalusWingsPerkKey = "daedalus_wings";

        private const float GlideSinkSpeedPixels = 40f;
        private const float DiveSpeedPixels = 620f;

        private MovementAbilityData MovementData => Data as MovementAbilityData;

        private bool _isGliding;
        private bool _isDiving;
        private float _glideTimer;
        private float _glideDuration = 3f;
        private float _wingSpeed = 380f;

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
            _isGliding = false;
            _isDiving = false;
            _glideDuration = MovementData?.MovementDuration > 0f ? MovementData.MovementDuration : 3f;
            // Story-only GlideSpeed minors quicken the ornithopter's wing speed
            // (boost and glide alike); neutral 1f outside Story Mode.
            _wingSpeed = (MovementData?.MovementSpeed > 0f ? MovementData.MovementSpeed : 380f)
                * Owner.StoryGlideSpeedMultiplier;
            Owner.Velocity = new Vector2(Owner.Velocity.X, -_wingSpeed);
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
        }

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
            _isGliding = true;
            _glideTimer = _glideDuration;
            FTT.Core.EventBus.Instance?.RaiseMovementAbilityUsed(new FTT.Core.MovementAbilityPayload {
                PlayerIndex = Owner.PlayerIndex,
                AbilityName = Data?.AbilityName ?? "Ornithopter Flight",
                StartPosition = Owner.GlobalPosition,
                EndPosition = Owner.GlobalPosition
            });
        }

        /// <summary>
        /// H-4: a stun/death mid-cast releases the wing glide/dive steering and
        /// deactivates the Daedalus dive hitbox the dive path would otherwise
        /// leave live.
        /// </summary>
        protected override void OnInterrupted() {
            _isGliding = false;
            if (_isDiving) {
                _isDiving = false;
                GetNodeOrNull<Hitbox>("DaedalusDiveHitbox")?.Deactivate();
            }
        }

        public override void _PhysicsProcess(double delta) {
            float dt = (float)delta;

            if (_isDiving) {
                UpdateDive();
                return;
            }

            if (_isGliding) {
                _glideTimer -= dt;

                // Daedalus Wings (Story-only Resonance major perk): pressing Down
                // cancels the glide directly into a downward melee dive.
                if (Owner.HasStoryPerk(DaedalusWingsPerkKey)
                    && Owner.CurrentInputFrame.IsPressed(FTT.Core.GameplayButtons.Down)
                    && !Owner.IsOnFloor()) {
                    BeginDaedalusDive();
                    return;
                }

                float hInput = Owner.CurrentInputFrame.Horizontal;
                var vel = Owner.Velocity;
                vel.X = hInput * _wingSpeed;
                vel.Y = Mathf.Min(vel.Y, GlideSinkSpeedPixels);
                Owner.Velocity = vel;

                if (_glideTimer <= 0 || Owner.IsOnFloor()) {
                    _isGliding = false;
                    if (CurrentPhase != AbilityPhase.Inactive) AdvanceToCleanup();
                }
                return;
            }

            base._PhysicsProcess(delta);
        }

        private void BeginDaedalusDive() {
            _isGliding = false;
            _isDiving = true;
            Owner.Velocity = new Vector2(Owner.Velocity.X * 0.25f, DiveSpeedPixels);

            // VFX hook: Daedalus Wings' damaging steam trail is presentation-only
            // and attaches here once the production glide/dive VFX exist.

            var hitbox = GetOrCreateChildHitbox("DaedalusDiveHitbox");
            hitbox.Damage = (Owner.Data?.BasicAttackDamage ?? 9f) * Owner.StoryBasicDamageMultiplier;
            hitbox.KnockbackForce = new Vector2(2f, 2f);
            hitbox.OwnerPlayerIndex = Owner.PlayerIndex;
            hitbox.GlobalPosition = Owner.GlobalPosition + new Vector2(0f, 30f);
            hitbox.Activate();
        }

        private void UpdateDive() {
            var hitbox = GetOrCreateChildHitbox("DaedalusDiveHitbox");
            hitbox.GlobalPosition = Owner.GlobalPosition + new Vector2(0f, 30f);
            Owner.Velocity = new Vector2(Owner.Velocity.X, DiveSpeedPixels);

            if (Owner.IsOnFloor()) {
                hitbox.Deactivate();
                _isDiving = false;
                if (CurrentPhase != AbilityPhase.Inactive) AdvanceToCleanup();
            }
        }
    }

    /// <summary>
    /// Ultimate — The Vitruvian Matrix: Leonardo throws a geometric trap that
    /// locks every enemy inside the Vitruvian circle in place (Root, refreshed
    /// by each bombardment hit), then clockwork gears and cannons land the
    /// authored HitCount hits of BaseDamage across the active window; the final
    /// hit carries the authored knockback as the closing explosion. All numbers
    /// come from the authored AbilityData resource (10 x 8 = 80 total at 0.3 s
    /// intervals across the 2.4 s trap window). Ultimate-class hits bypass
    /// shields; the blueprint-dimension cinematic is presentation (Package 8).
    /// </summary>
    public partial class LeonardoVitruvianMatrix : BaseSpecial {

        // 2.5 world units, mirroring the Fighter zone half-width (type 23);
        // thrown 2 units ahead of Leonardo like the Fighter zone center.
        private const float MatrixRadiusPixels = 150f;
        private const float MatrixForwardOffsetPixels = 120f;

        private bool _matrixActive;
        private Vector2 _matrixCenter;
        private float _tickInterval = 0.3f;
        private float _tickTimer;
        private int _ticksRemaining;
        private UltimateMeter _meter;

        public override void _Ready() {
            base._Ready();
            _meter = Owner?.GetNodeOrNull<UltimateMeter>("UltimateMeter");
        }

        protected override bool Validate() {
            return base.Validate() && (_meter?.IsFull ?? false);
        }

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
            Owner.Velocity = Vector2.Zero;
            _meter?.Consume();
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
            BeginMatrix();
        }

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
            _matrixActive = false;
        }

        /// <summary>
        /// H-4: interrupting the channel stops the remaining bombardment ticks —
        /// the matrix is Leonardo conducting the trap, not a fire-and-forget
        /// construct (its normal recovery already shuts it down early).
        /// </summary>
        protected override void OnInterrupted() {
            _matrixActive = false;
        }

        private void BeginMatrix() {
            if (Owner == null) return;
            _matrixCenter = Owner.GlobalPosition + new Vector2(
                Owner.IsFacingRight ? MatrixForwardOffsetPixels : -MatrixForwardOffsetPixels, 0f);
            _tickInterval = (Data?.DamageTickIntervalFrames ?? 18) / 60f;
            if (_tickInterval <= 0f) _tickInterval = 0.3f;
            _ticksRemaining = Data?.IsMultiHit == true ? Mathf.Max(1, Data.HitCount) : 8;
            _tickTimer = 0f; // First bombardment hit lands on the next physics step.
            _matrixActive = true;

            // Trap: every enemy already inside the circle is locked in place.
            // The Root is applied directly to the status handlers because the
            // trap itself deals no damage (zero-damage hits do not carry status
            // through the player damage gate); each bombardment hit then
            // refreshes the hold through its own payload.
            RootTargetsInCircle();

            // Placeholder circle visual only; damage runs through the shape
            // queries below so the final-hit knockback stays per-target.
            SpawnPlaceholderZone(
                _matrixCenter, 0f,
                Data?.Lifetime > 0f ? Data.Lifetime : 2.4f, 1f,
                new Color(0.35f, 0.55f, 0.9f), MatrixRadiusPixels);
        }

        public override void _PhysicsProcess(double delta) {
            base._PhysicsProcess(delta);
            if (!_matrixActive) return;
            if (Owner == null || !IsInstanceValid(Owner)) { _matrixActive = false; return; }

            _tickTimer -= (float)delta;
            if (_tickTimer > 0f) return;
            _tickTimer += _tickInterval;
            TickBombardment();
            _ticksRemaining--;
            if (_ticksRemaining <= 0) _matrixActive = false;
        }

        private void TickBombardment() {
            bool finalHit = _ticksRemaining == 1;
            float damage = (Data?.BaseDamage ?? 10f) * Owner.StorySpecialDamageMultiplier;

            foreach (Hurtbox hurtbox in QueryHurtboxesInCircle()) {
                bool pushRight = hurtbox.GlobalPosition.X >= _matrixCenter.X;
                float dealt = hurtbox.TakeHit(new HitPayload {
                    AttackerIndex = Owner.PlayerIndex,
                    AttackID = Data?.AbilityID ?? "leonardo_vitruvian_matrix",
                    HitboxID = finalHit ? "matrix_explosion" : "matrix_bombardment",
                    AttackClass = AttackClass.Ultimate,
                    Damage = damage,
                    // The final hit is the massive closing explosion: it carries
                    // the authored knockback away from the circle center; the
                    // earlier bombardment hits are impulse-free so the Root hold
                    // is what keeps targets caged.
                    Knockback = finalHit ? (Data?.KnockbackForce ?? new Vector2(5, -3)) : Vector2.Zero,
                    HitstunDuration = finalHit ? (Data?.HitstunDuration ?? 0.3f) : 0.1f,
                    HitOrigin = _matrixCenter,
                    AttackerFacingRight = pushRight,
                    // Each bombardment hit refreshes the trap's Root hold; the
                    // final explosion applies none so the launch is not held.
                    AppliedStatus = finalHit
                        ? FTT.Core.StatusType.None
                        : Data?.AppliedStatus ?? FTT.Core.StatusType.Root,
                    StatusDuration = Data?.StatusDuration > 0f ? Data.StatusDuration : 0.4f,
                    StatusIntensity = Data?.StatusIntensity ?? 1f,
                    ScreenShakeIntensity = Data?.ScreenShakeIntensity ?? 0.6f,
                    ScreenShakeDuration = Data?.ScreenShakeDuration ?? 0.3f
                });
                // V7.6 D03h (Package 11 A1b): Ultimate-origin damage awards its caster
                // ZERO damage-dealt meter, regardless of HP removed, target count or
                // when it lands. Direct-hit Rally reclaim is retained (D03g).
                if (dealt > 0f) Owner.AddInfluenceFromDamageDealt(dealt, ultimateOrigin: true);
            }
        }

        private void RootTargetsInCircle() {
            float rootDuration = Data?.StatusDuration > 0f ? Data.StatusDuration : 0.4f;
            foreach (Hurtbox hurtbox in QueryHurtboxesInCircle()) {
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

        private System.Collections.Generic.List<Hurtbox> QueryHurtboxesInCircle() {
            var hits = new System.Collections.Generic.List<Hurtbox>();
            var space = Owner?.GetWorld2D()?.DirectSpaceState;
            if (space == null) return hits;

            uint targetHurtboxLayer = Owner.PlayerIndex == 0
                ? FTT.Core.CollisionLayers.EnemyHurtbox
                : FTT.Core.CollisionLayers.PlayerHurtbox;
            var query = new PhysicsShapeQueryParameters2D {
                Shape = new CircleShape2D { Radius = MatrixRadiusPixels },
                Transform = new Transform2D(0f, _matrixCenter),
                CollideWithAreas = true,
                CollideWithBodies = false,
                CollisionMask = StoryShapeQuery.DeliveryMask(targetHurtboxLayer)
            };
            foreach (Godot.Collections.Dictionary result in space.IntersectShape(query, 16)) {
                if (result["collider"].AsGodotObject() is not Hurtbox hurtbox) continue;
                if (hurtbox.OwnerPlayerIndex == Owner.PlayerIndex) continue;
                hits.Add(hurtbox);
            }
            return hits;
        }
    }
}
