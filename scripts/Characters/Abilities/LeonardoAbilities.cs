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
        private float _growthPerSecond;

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
            BeginSpiral();
            Owner.SpecialOneCooldownTimer = Data?.CooldownDuration ?? 10f;
            FTT.Core.EventBus.Instance?.RaiseCooldownStarted(new FTT.Core.CooldownPayload {
                PlayerIndex = Owner.PlayerIndex,
                Slot = FTT.Core.AbilitySlot.Special1,
                Duration = Data?.CooldownDuration ?? 10f
            });
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
            // First tick lands immediately (next physics step); the spiral reaches
            // full radius by the final tick.
            _tickTimer = 0f;
            _radius = StartRadiusPixels;
            _growthPerSecond = _ticksRemaining > 1
                ? (MaxRadiusPixels - StartRadiusPixels) / ((_ticksRemaining - 1) * _tickInterval)
                : 0f;
            _spiralActive = true;

            // Placeholder spiral visual only; damage runs through the shape queries
            // below so per-target perk behavior can be evaluated.
            SpawnPlaceholderZone(
                _spiralCenter, 0f,
                Data?.Lifetime > 0f ? Data.Lifetime : 1.5f, 1f,
                new Color(0.85f, 0.7f, 0.25f), MaxRadiusPixels);
        }

        public override void _PhysicsProcess(double delta) {
            base._PhysicsProcess(delta);
            if (!_spiralActive) return;
            if (Owner == null || !IsInstanceValid(Owner)) { _spiralActive = false; return; }

            float dt = (float)delta;
            _radius = Mathf.Min(_radius + _growthPerSecond * dt, MaxRadiusPixels);
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
                CollisionMask = targetHurtboxLayer
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

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
            DeployTurret();
            Owner.SpecialTwoCooldownTimer = Data?.CooldownDuration ?? 10f;
            FTT.Core.EventBus.Instance?.RaiseCooldownStarted(new FTT.Core.CooldownPayload {
                PlayerIndex = Owner.PlayerIndex,
                Slot = FTT.Core.AbilitySlot.Special2,
                Duration = Data?.CooldownDuration ?? 10f
            });
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
            _wingSpeed = MovementData?.MovementSpeed > 0f ? MovementData.MovementSpeed : 380f;
            Owner.Velocity = new Vector2(Owner.Velocity.X, -_wingSpeed);
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
        }

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
            _isGliding = true;
            _glideTimer = _glideDuration;
            float cooldown = Data?.CooldownDuration ?? 5f;
            Owner.MovementAbilityCooldownTimer = cooldown;
            FTT.Core.EventBus.Instance?.RaiseMovementAbilityUsed(new FTT.Core.MovementAbilityPayload {
                PlayerIndex = Owner.PlayerIndex,
                AbilityName = Data?.AbilityName ?? "Ornithopter Flight",
                StartPosition = Owner.GlobalPosition,
                EndPosition = Owner.GlobalPosition
            });
            FTT.Core.EventBus.Instance?.RaiseCooldownStarted(new FTT.Core.CooldownPayload {
                PlayerIndex = Owner.PlayerIndex,
                Slot = FTT.Core.AbilitySlot.MovementAbility,
                Duration = cooldown
            });
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
    /// Ultimate — The Vitruvian Matrix. Structured sketch pending the dedicated
    /// ultimates pass (audit gap X7): trap + blueprint-dimension bombardment
    /// multi-hit ending in a final explosion.
    /// </summary>
    public partial class LeonardoVitruvianMatrix : BaseSpecial {
        private const float CinematicDuration = 3.0f;
        private const int HitCount = 8;
        private const float DamagePerHit = 10f;

        private float _hitTimer;
        private int _hitsDone;
        private UltimateMeter _meter;

        public override void _Ready() {
            base._Ready();
            _meter = Owner?.GetNodeOrNull<UltimateMeter>("UltimateMeter");
        }

        protected override bool Validate() {
            return base.Validate() && (_meter?.IsFull ?? false);
        }

        protected override void OnStartup() {
            PhaseTimer = 0.6f;
            _hitsDone = 0;
            _hitTimer = 0;
            Owner.Velocity = Vector2.Zero;
            _meter?.Consume();
        }

        protected override void OnActive() {
            PhaseTimer = CinematicDuration;
        }

        protected override void OnRecovery() {
            PhaseTimer = 0.5f;
        }

        public override void _PhysicsProcess(double delta) {
            if (CurrentPhase == AbilityPhase.Active) {
                float dt = (float)delta;
                _hitTimer += dt;
                float hitInterval = CinematicDuration / HitCount;
                while (_hitTimer >= hitInterval && _hitsDone < HitCount) {
                    _hitTimer -= hitInterval;
                    _hitsDone++;
                    DealMatrixHit();
                }
            }
            base._PhysicsProcess(delta);
        }

        private void DealMatrixHit() {
            var hitbox = GetOrCreateChildHitbox("MatrixHitbox");
            hitbox.Damage = DamagePerHit;
            hitbox.KnockbackForce = new Vector2(0, -3f);
            hitbox.OwnerPlayerIndex = Owner.PlayerIndex;
            hitbox.GlobalPosition = Owner.GlobalPosition;
            hitbox.Activate();
            GetTree().CreateTimer(0.1f).Timeout += () => hitbox.Deactivate();
        }
    }
}
