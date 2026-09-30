using Godot;
using FTT.Combat;
using FTT.Characters;

namespace FTT.Characters.Abilities {

    /// <summary>
    /// Special 1 — Righteous Smite: a downward broadsword swing that launches a
    /// holy shockwave along the ground, dealing the authored 14 damage and
    /// applying RadiantBurn for 3 s. All numbers (damage, speed, lifetime,
    /// status, phase frames) come from the authored AbilityData; the placeholder
    /// projectile travels ground-hugging until production VFX lands.
    /// Story-only Resonance perk Unstoppable Crusade grants hyper-armor through
    /// the active swing plus 1.5 s after casting.
    /// </summary>
    public partial class JoanRighteousSmite : BaseSpecial {

        public const string UnstoppableCrusadePerkKey = "unstoppable_crusade";

        private const float PostCastHyperArmorSeconds = 1.5f;
        private const float GroundHugOffsetY = 24f;

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
            SpawnShockwave();
            if (Owner.HasStoryPerk(UnstoppableCrusadePerkKey)) {
                float activeSeconds = Mathf.Max(1, Data?.ActiveFrames ?? 6) / 60f;
                Owner.ApplyStoryHyperArmor(activeSeconds);
            }
        }

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
            if (Owner.HasStoryPerk(UnstoppableCrusadePerkKey)) {
                Owner.ApplyStoryHyperArmor(PostCastHyperArmorSeconds);
            }
        }

        private void SpawnShockwave() {
            if (Owner == null) return;
            float damage = (Data?.BaseDamage ?? 14f) * Owner.StorySpecialDamageMultiplier;
            SpawnPlaceholderProjectile(
                Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? 40f : -40f, GroundHugOffsetY),
                Data?.ProjectileSpeed ?? 250f, Owner.IsFacingRight, new Color(0.95f, 0.85f, 0.3f),
                new Vector2(36, 18), Data?.ProjectileLifetime ?? 5f, damage);
        }
    }

    /// <summary>
    /// Special 2 — Divine Piercing: a stationary flurry of broadsword thrusts.
    /// The authored HitCount thrusts are spread across the active frames and
    /// total BaseDamage * HitCount. It is one of the three authored
    /// Shield-Breakers (A01, Package 13 W1): against a blocking opponent the
    /// first absorbed thrust spends every remaining charge through the generic
    /// block path (<c>BlockClass.ShieldBreaker</c> on the resource).
    /// </summary>
    public partial class JoanDivinePiercing : BaseSpecial {

        private const int MaxQueryResults = 16;

        private int _thrustsDone;
        private int _activeFramesElapsed;

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
            _thrustsDone = 0;
            _activeFramesElapsed = 0;
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
        }

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
        }

        public override void _PhysicsProcess(double delta) {
            if (CurrentPhase == AbilityPhase.Active) {
                _activeFramesElapsed++;
                int hitCount = Mathf.Max(1, Data?.HitCount ?? 1);
                int interval = Mathf.Max(1, (Data?.ActiveFrames ?? hitCount) / hitCount);
                while (_thrustsDone < hitCount && _activeFramesElapsed >= interval * (_thrustsDone + 1)) {
                    _thrustsDone++;
                    ExecuteThrust();
                }
            }
            base._PhysicsProcess(delta);
        }

        private void ExecuteThrust() {
            if (Owner == null) return;
            var space = Owner.GetWorld2D()?.DirectSpaceState;
            if (space == null) return;

            Vector2 size = Data?.HitboxSize ?? new Vector2(60f, 50f);
            Vector2 offset = Data?.HitboxOffset ?? new Vector2(30f, 0f);
            if (!Owner.IsFacingRight) offset.X = -offset.X;
            uint targetHurtboxLayer = Owner.PlayerIndex == 0
                ? FTT.Core.CollisionLayers.EnemyHurtbox
                : FTT.Core.CollisionLayers.PlayerHurtbox;
            var query = new PhysicsShapeQueryParameters2D {
                Shape = new RectangleShape2D { Size = size },
                Transform = new Transform2D(0f, Owner.GlobalPosition + offset),
                CollideWithAreas = true,
                CollideWithBodies = false,
                CollisionMask = StoryShapeQuery.DeliveryMask(targetHurtboxLayer)
            };

            bool finalThrust = _thrustsDone >= (Data?.HitCount ?? 1);
            foreach (Godot.Collections.Dictionary result in space.IntersectShape(query, MaxQueryResults)) {
                if (result["collider"].AsGodotObject() is not Hurtbox hurtbox) continue;
                if (hurtbox.OwnerPlayerIndex == Owner.PlayerIndex) continue;

                // V7.6 F15 / A01 (Package 13 W1): Divine Piercing is a
                // Shield-Breaker — a FULL shatter. joan/special_2.tres authors
                // BlockClass.ShieldBreaker and Stamp() carries it onto the
                // payload, so BlockSystem.ResolveHit takes 1, 2 or 3 charges to
                // 0 with the normal shatter response (shatter-freeze, daze,
                // 5 s lockout). An ordinary Special would spend only two.
                //
                // The multi-hit shatters exactly ONCE per execution without a
                // separate latch: the shatter transitions the victim to Dazed,
                // so no later thrust in this execution can find a live stance,
                // and each thrust's IntersectShape yields a hurtbox once. Later
                // distinct contacts then follow normal hit eligibility with no
                // extra absorption and no invulnerability.
                HitPayload hit = Stamp(new HitPayload {
                    AttackerIndex = Owner.PlayerIndex,
                    AttackID = Data?.AbilityID ?? "joan_divine_piercing",
                    HitboxID = $"thrust_{_thrustsDone}",
                    AttackClass = AttackClass.Special,
                    Damage = (Data?.BaseDamage ?? 3f) * Owner.StorySpecialDamageMultiplier,
                    Knockback = finalThrust ? Data?.KnockbackForce ?? new Vector2(4f, -1f) : Vector2.Zero,
                    HitstunDuration = Data?.HitstunDuration ?? 0.2f,
                    HitOrigin = Owner.GlobalPosition,
                    AttackerFacingRight = Owner.IsFacingRight,
                    AppliedStatus = Data?.AppliedStatus ?? FTT.Core.StatusType.None,
                    StatusDuration = Data?.StatusDuration ?? 0f,
                    StatusIntensity = Data?.StatusIntensity ?? 1f,
                    ScreenShakeIntensity = Data?.ScreenShakeIntensity ?? 0.2f,
                    ScreenShakeDuration = Data?.ScreenShakeDuration ?? 0.1f
                });
                float dealt = hurtbox.TakeHit(hit);
                Credit(in hit, dealt);
            }
        }

    }

    /// <summary>
    /// Movement — Ascendant Wings: a rising vertical leap; holding Jump glides
    /// downward on ethereal wings for up to the authored duration (3 s). Usable
    /// in the air. Leap speed, glide duration, cooldown, and phase frames come
    /// from the authored MovementAbilityData.
    /// </summary>
    public partial class JoanAscendantWings : BaseSpecial {

        /// <summary>
        /// Story-only Resonance TRAVERSAL flag (V7.6, Tier 2). A direct
        /// connecting combo finisher or Righteous Smite hit resets this
        /// ability's cooldown to zero, once per attack execution. The rule
        /// lives in <c>PlayerController.TryWingsRefresh</c> because both
        /// trigger sources are ordinary Hitbox hits; the key is declared here
        /// beside the ability it refreshes.
        /// </summary>
        public const string WingsRefreshPerkKey = "wings_refresh";

        /// <summary>The AttackID Righteous Smite's shockwave carries.</summary>
        public const string RighteousSmiteAttackID = "joan_righteous_smite";

        private const float GlideGravityScale = 0.35f;
        private const float DefaultLeapSpeed = 420f;
        private const float DefaultGlideSeconds = 3f;

        private bool _isGliding;
        private float _glideTimer;

        private MovementAbilityData MovementData => Data as MovementAbilityData;

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
            _isGliding = false;
            _glideTimer = 0f;
            float leapSpeed = MovementData?.MovementSpeed > 0f ? MovementData.MovementSpeed : DefaultLeapSpeed;
            Owner.Velocity = new Vector2(Owner.Velocity.X, -leapSpeed);
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
        }

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
            FTT.Core.EventBus.Instance?.RaiseMovementAbilityUsed(new FTT.Core.MovementAbilityPayload {
                PlayerIndex = Owner.PlayerIndex,
                AbilityName = Data?.AbilityName ?? "Ascendant Wings",
                StartPosition = Owner.GlobalPosition,
                EndPosition = Owner.GlobalPosition
            });
        }

        /// <summary>H-4: a stun/death mid-cast releases the wing glide's velocity hold.</summary>
        protected override void OnInterrupted() {
            _isGliding = false;
        }

        public override void _PhysicsProcess(double delta) {
            float dt = (float)delta;

            if (CurrentPhase == AbilityPhase.Recovery || CurrentPhase == AbilityPhase.Cleanup) {
                if (!_isGliding && !Owner.IsOnFloor() &&
                    Owner.CurrentInputFrame.IsHeld(FTT.Core.GameplayButtons.Jump)) {
                    _isGliding = true;
                    _glideTimer = MovementData?.MovementDuration > 0f
                        ? MovementData.MovementDuration
                        : DefaultGlideSeconds;
                }

                if (_isGliding) {
                    _glideTimer -= dt;
                    var vel = Owner.Velocity;
                    vel.Y = Mathf.Min(vel.Y, 30f * 60f * GlideGravityScale * dt);
                    Owner.Velocity = vel;

                    if (_glideTimer <= 0 || Owner.IsOnFloor() ||
                        !Owner.CurrentInputFrame.IsHeld(FTT.Core.GameplayButtons.Jump)) {
                        _isGliding = false;
                        AdvanceToCleanup();
                    }
                    return;
                }
            }

            base._PhysicsProcess(delta);
        }
    }

    /// <summary>
    /// Ultimate — The Grand Crusade: a directional cavalry charge. Joan travels
    /// forward at the authored charge speed for the whole active window,
    /// delivering the authored HitCount trample hits via hurtbox shape queries
    /// along the path (once per authored tick interval). The final hit carries
    /// the authored KnockbackForce, carrying the opponent toward the blast zone;
    /// intermediate hits are knockback-free so the full multi-hit total lands.
    /// Ultimate-class hits bypass block. All numbers (per-hit BaseDamage,
    /// HitCount, tick interval, charge speed, hitbox shape, knockback, phase
    /// frames) come from the authored AbilityData; cinematic presentation
    /// (banner plant, spectral knights) is Package 8.
    /// </summary>
    public partial class JoanGrandCrusade : BaseSpecial {

        private const float DefaultChargeSpeed = 900f;
        private const int MaxQueryResults = 16;

        private int _hitsDone;
        private int _activeFramesElapsed;
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
            _hitsDone = 0;
            _activeFramesElapsed = 0;
            Owner.Velocity = Vector2.Zero;
            _meter?.Consume();
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
        }

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
            Owner.Velocity = new Vector2(0f, Owner.Velocity.Y);
        }

        public override void _PhysicsProcess(double delta) {
            if (CurrentPhase == AbilityPhase.Active) {
                float chargeSpeed = Data?.ProjectileSpeed > 0f ? Data.ProjectileSpeed : DefaultChargeSpeed;
                Owner.Velocity = new Vector2(
                    Owner.IsFacingRight ? chargeSpeed : -chargeSpeed,
                    Owner.Velocity.Y);

                _activeFramesElapsed++;
                int hitCount = Mathf.Max(1, Data?.HitCount ?? 1);
                int interval = Data?.DamageTickIntervalFrames > 0
                    ? Data.DamageTickIntervalFrames
                    : Mathf.Max(1, (Data?.ActiveFrames ?? hitCount) / hitCount);
                while (_hitsDone < hitCount && _activeFramesElapsed >= interval * (_hitsDone + 1)) {
                    _hitsDone++;
                    ExecuteTrampleHit();
                }
            }
            base._PhysicsProcess(delta);
        }

        private void ExecuteTrampleHit() {
            if (Owner == null) return;
            var space = Owner.GetWorld2D()?.DirectSpaceState;
            if (space == null) return;

            Vector2 size = Data?.HitboxSize ?? new Vector2(90f, 70f);
            Vector2 offset = Data?.HitboxOffset ?? new Vector2(40f, 0f);
            if (!Owner.IsFacingRight) offset.X = -offset.X;
            uint targetHurtboxLayer = Owner.PlayerIndex == 0
                ? FTT.Core.CollisionLayers.EnemyHurtbox
                : FTT.Core.CollisionLayers.PlayerHurtbox;
            var query = new PhysicsShapeQueryParameters2D {
                Shape = new RectangleShape2D { Size = size },
                Transform = new Transform2D(0f, Owner.GlobalPosition + offset),
                CollideWithAreas = true,
                CollideWithBodies = false,
                CollisionMask = StoryShapeQuery.DeliveryMask(targetHurtboxLayer)
            };

            bool finalHit = _hitsDone >= (Data?.HitCount ?? 1);
            foreach (Godot.Collections.Dictionary result in space.IntersectShape(query, MaxQueryResults)) {
                if (result["collider"].AsGodotObject() is not Hurtbox hurtbox) continue;
                if (hurtbox.OwnerPlayerIndex == Owner.PlayerIndex) continue;

                HitPayload hit = Stamp(new HitPayload {
                    AttackerIndex = Owner.PlayerIndex,
                    AttackID = Data?.AbilityID ?? "joan_grand_crusade",
                    HitboxID = $"trample_{_hitsDone}",
                    AttackClass = AttackClass.Ultimate,
                    Damage = (Data?.BaseDamage ?? 12f) * Owner.StorySpecialDamageMultiplier,
                    Knockback = finalHit ? Data?.KnockbackForce ?? new Vector2(8f, -2f) : Vector2.Zero,
                    HitstunDuration = Data?.HitstunDuration ?? 0.2f,
                    HitOrigin = Owner.GlobalPosition,
                    AttackerFacingRight = Owner.IsFacingRight,
                    AppliedStatus = Data?.AppliedStatus ?? FTT.Core.StatusType.None,
                    StatusDuration = Data?.StatusDuration ?? 0f,
                    StatusIntensity = Data?.StatusIntensity ?? 1f,
                    ScreenShakeIntensity = Data?.ScreenShakeIntensity ?? 0.6f,
                    ScreenShakeDuration = Data?.ScreenShakeDuration ?? 0.3f
                });
                float dealt = hurtbox.TakeHit(hit);
                // V7.6 D03h (Package 11 A1b): Ultimate-origin damage awards
                // its caster ZERO damage-dealt meter. Direct-hit Rally reclaim
                // from an Ultimate impact is retained (D03g).
                Credit(in hit, dealt);
            }
        }
    }
}
