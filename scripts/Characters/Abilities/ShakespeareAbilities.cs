using Godot;
using FTT.Combat;
using FTT.Characters;

namespace FTT.Characters.Abilities {

    /// <summary>
    /// Special 1 — Yorick's Lament: throws a rolling skull that deals minor
    /// contact damage, then releases a wailing sonic wave at the impact point
    /// applying TimeDilation for the authored 2.5 s. The authored StatusIntensity
    /// is 0.6 because the shared TimeDilation formula in both modes is
    /// speed x (1 - 0.5 x intensity): 1 - 0.5 x 0.6 = 0.7, the design's 30%
    /// movement/animation slow. Timing, damage, and cooldown come from the
    /// authored AbilityData. Story-only Resonance perk: Macbeth's Curse (the
    /// wave also poisons — see ApplyMacbethVenom for the single-status note).
    /// </summary>
    public partial class ShakespeareYoricksLament : BaseSpecial {

        public const string MacbethsCursePerkKey = "macbeths_curse";

        private const float ContactDamageShare = 1f / 3f;
        private const float WaveRadius = 110f;
        private const float MacbethVenomDuration = 3f;
        // VenomStrategy/EnemyController tick 2 HP x intensity per second, so 0.5
        // yields the design's 1 chip damage every 1.0 s.
        private const float MacbethVenomIntensity = 0.5f;

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
            LaunchSkull();
        }

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
        }

        private void LaunchSkull() {
            if (Owner == null) return;
            float contactDamage = Mathf.Round((Data?.BaseDamage ?? 14f) * ContactDamageShare);
            var projectile = SpawnPlaceholderProjectile(
                Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? 40f : -40f, -10f),
                Data?.ProjectileSpeed ?? 200f, Owner.IsFacingRight, new Color(0.8f, 0.7f, 0.5f),
                new Vector2(18, 18), Data?.ProjectileLifetime ?? 5f, contactDamage);
            if (projectile != null) {
                projectile.DetonateOnImpact = true;
                // The wave owns the TimeDilation; without this the skull's contact
                // hit applied the same slow a second time (V7 tuning batch).
                projectile.ClearContactStatus();
                projectile.Impacted += OnProjectileImpacted;
            }
        }

        private void OnProjectileImpacted(Vector2 impactPosition) {
            // Area signals fire while the physics space is locked; defer the wave
            // query one step so the shape cast is legal.
            CallDeferred(nameof(EmitSonicWave), impactPosition);
        }

        private void EmitSonicWave(Vector2 impactPosition) {
            if (Owner == null || !IsInstanceValid(Owner)) return;

            // Wave flash visual only; damage and status are applied through the
            // shape query so the Macbeth's Curse perk can be evaluated per target.
            SpawnPlaceholderZone(impactPosition, 0f, 0.25f, 1f, new Color(0.7f, 0.65f, 0.9f), WaveRadius);

            var space = Owner.GetWorld2D()?.DirectSpaceState;
            if (space == null) return;
            uint targetHurtboxLayer = Owner.PlayerIndex == 0
                ? FTT.Core.CollisionLayers.EnemyHurtbox
                : FTT.Core.CollisionLayers.PlayerHurtbox;
            var query = new PhysicsShapeQueryParameters2D {
                Shape = new CircleShape2D { Radius = WaveRadius },
                Transform = new Transform2D(0f, impactPosition),
                CollideWithAreas = true,
                CollideWithBodies = false,
                CollisionMask = StoryShapeQuery.DeliveryMask(targetHurtboxLayer)
            };

            bool macbethsCurse = Owner.HasStoryPerk(MacbethsCursePerkKey);
            foreach (Godot.Collections.Dictionary result in space.IntersectShape(query, 16)) {
                if (result["collider"].AsGodotObject() is not Hurtbox hurtbox) continue;
                if (hurtbox.OwnerPlayerIndex == Owner.PlayerIndex) continue;

                HitPayload hit = Stamp(new HitPayload {
                    AttackerIndex = Owner.PlayerIndex,
                    AttackID = Data?.AbilityID ?? "shakespeare_yoricks_lament",
                    HitboxID = "sonic_wave",
                    AttackClass = AttackClass.Special,
                    Damage = (Data?.BaseDamage ?? 14f) * Owner.StorySpecialDamageMultiplier,
                    // Story-only KnockbackForce minors strengthen the wave's shove.
                    Knockback = (Data?.KnockbackForce ?? new Vector2(3, -2)) * Owner.StoryKnockbackMultiplier,
                    HitstunDuration = Data?.HitstunDuration ?? 0.2f,
                    HitOrigin = impactPosition,
                    AttackerFacingRight = Owner.IsFacingRight,
                    AppliedStatus = Data?.AppliedStatus ?? FTT.Core.StatusType.TimeDilation,
                    // Package 11 A4: "Minor Lament Slow" is the scoped
                    // AbilityDuration(shakespeare_yoricks_lament) lane -
                    // 2.5 s to 3.0 s when bought.
                    StatusDuration = (Data?.StatusDuration ?? 2.5f)
                        * Owner.StoryScoped("AbilityDuration", "shakespeare_yoricks_lament"),
                    StatusIntensity = Data?.StatusIntensity ?? 0.6f,
                    ScreenShakeIntensity = Data?.ScreenShakeIntensity ?? 0.2f,
                    ScreenShakeDuration = Data?.ScreenShakeDuration ?? 0.15f
                });
                float dealt = hurtbox.TakeHit(hit);
                Credit(in hit, dealt);

                if (macbethsCurse) ApplyMacbethVenom(hurtbox);
            }
        }

        /// <summary>
        /// Macbeth's Curse (Story-only): the wave also applies the Tragic Poison
        /// Venom tick. Under the V7 two-slot status rule the poison rides the
        /// damage slot while the wave's TimeDilation holds the control slot, so
        /// the perk now stacks the DoT on top of the slow instead of trading one
        /// for the other.
        /// </summary>
        private static void ApplyMacbethVenom(Hurtbox hurtbox) {
            Node current = hurtbox.GetParent();
            while (current != null) {
                if (current is PlayerController player) {
                    player.GetNodeOrNull<StatusController>("StatusController")
                        ?.ApplyStatus(FTT.Core.StatusType.Venom, MacbethVenomDuration, MacbethVenomIntensity);
                    return;
                }
                if (current is FTT.Enemies.EnemyController enemy) {
                    enemy.ApplyStatusEffect(FTT.Core.StatusType.Venom, MacbethVenomDuration, MacbethVenomIntensity);
                    return;
                }
                current = current.GetParent();
            }
        }
    }

    /// <summary>
    /// Special 2 — The Tempest: a localized wind storm around Shakespeare that
    /// continuously shoves adjacent enemies away from him while the gust lifts
    /// him into the air. Zero-damage crowd control; storm duration comes from the
    /// authored active frames and the cooldown from the resource. Targets are
    /// found through the shared hurtbox contract, so Story enemies and sparring
    /// players are both affected.
    /// </summary>
    public partial class ShakespeareTheTempest : BaseSpecial {

        /// <summary>
        /// Story-only Resonance TRAVERSAL flag (V7.6, Tier 2). The Tempest's
        /// lift can be jumped from at its APEX — a jump refresh, granted once
        /// per lift as the active phase ends.
        /// <para>V7.6 correction: the V7 text said "the barrier can be jumped
        /// from"; no barrier exists anywhere in this kit.</para>
        /// </summary>
        public const string TempestApexJumpPerkKey = "tempest_apex_jump";

        private const float StormRadius = 120f;
        private const float LiftSpeed = 200f;
        private const float PushAcceleration = 180f;

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
        }

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
            // Tempest Apex Jump (traversal): the lift has just finished, which
            // IS the apex — refresh the jump budget so Shakespeare can leave
            // from the top. A refresh only; it grants no glide, cancels
            // nothing, and cannot bypass a Suppression or a Time Freeze.
            if (Owner != null && Owner.HasStoryPerk(TempestApexJumpPerkKey)) {
                Owner.RemainingJumps = Owner.Data?.MaxJumpCount ?? 2;
            }
        }

        public override void _PhysicsProcess(double delta) {
            if (CurrentPhase == AbilityPhase.Active && Owner != null) {
                float dt = (float)delta;
                Owner.Velocity = new Vector2(Owner.Velocity.X, -LiftSpeed);
                PushAdjacentTargets(dt);
            }
            base._PhysicsProcess(delta);
        }

        private void PushAdjacentTargets(float dt) {
            var space = Owner.GetWorld2D()?.DirectSpaceState;
            if (space == null) return;
            uint targetHurtboxLayer = Owner.PlayerIndex == 0
                ? FTT.Core.CollisionLayers.EnemyHurtbox
                : FTT.Core.CollisionLayers.PlayerHurtbox;
            var query = new PhysicsShapeQueryParameters2D {
                Shape = new CircleShape2D { Radius = StormRadius },
                Transform = new Transform2D(0f, Owner.GlobalPosition),
                CollideWithAreas = true,
                CollideWithBodies = false,
                CollisionMask = targetHurtboxLayer
            };

            foreach (Godot.Collections.Dictionary result in space.IntersectShape(query, 16)) {
                if (result["collider"].AsGodotObject() is not Hurtbox hurtbox) continue;
                if (hurtbox.OwnerPlayerIndex == Owner.PlayerIndex) continue;
                PushTargetAway(hurtbox, dt);
            }
        }

        private void PushTargetAway(Hurtbox hurtbox, float dt) {
            Node current = hurtbox.GetParent();
            while (current != null) {
                if (current is CharacterBody2D body) {
                    Vector2 away = body.GlobalPosition - Owner.GlobalPosition;
                    Vector2 pushDir = away == Vector2.Zero
                        ? (Owner.IsFacingRight ? Vector2.Right : Vector2.Left)
                        : away.Normalized();
                    // Story-only KnockbackForce minors strengthen the storm's push.
                    body.Velocity += pushDir * PushAcceleration * Owner.StoryKnockbackMultiplier * dt;
                    return;
                }
                current = current.GetParent();
            }
        }
    }

    /// <summary>
    /// Movement — Prospero's Flight: a magical gust propels Shakespeare forward
    /// and upward, then lets him glide horizontally for up to the authored glide
    /// duration (3 s). Usable in the air for recovery. Glide speed, glide
    /// duration, and cooldown come from the authored MovementAbilityData.
    /// Story-only Resonance perk: Midsummer Glide (+20% glide speed and 8 damage
    /// to enemies glided through, each struck once per glide).
    /// </summary>
    public partial class ShakespeareProsperosFlight : BaseSpecial {

        public const string MidsummerGlidePerkKey = "midsummer_glide";

        private const float GustForce = 400f;
        private const float GlideFallSpeedCap = 35f;
        private const float GlideStrikeRadius = 50f;
        private const float MidsummerGlideSpeedMultiplier = 1.2f;
        private const float MidsummerGlideDamage = 8f;

        private bool _isGliding;
        private float _glideTimer;
        private float _glideSpeed;
        private Vector2 _startPosition;
        private readonly System.Collections.Generic.HashSet<Hurtbox> _glideVictims = new();

        private MovementAbilityData MovementData => Data as MovementAbilityData;

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
            _isGliding = false;
            _startPosition = Owner.GlobalPosition;
            float hDir = Owner.IsFacingRight ? 1f : -1f;
            Owner.Velocity = new Vector2(hDir * GustForce * 0.5f, -GustForce);
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
        }

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
            _isGliding = true;
            _glideTimer = MovementData?.MovementDuration > 0f ? MovementData.MovementDuration : 3f;
            _glideSpeed = MovementData?.MovementSpeed > 0f ? MovementData.MovementSpeed : 130f;
            if (Owner.HasStoryPerk(MidsummerGlidePerkKey)) {
                _glideSpeed *= MidsummerGlideSpeedMultiplier;
            }
            _glideVictims.Clear();

            FTT.Core.EventBus.Instance?.RaiseMovementAbilityUsed(new FTT.Core.MovementAbilityPayload {
                PlayerIndex = Owner.PlayerIndex,
                AbilityName = Data?.AbilityName ?? "Prospero's Flight",
                StartPosition = _startPosition,
                EndPosition = Owner.GlobalPosition
            });
        }

        /// <summary>H-4: a stun/death mid-cast releases the glide's velocity steering.</summary>
        protected override void OnInterrupted() {
            _isGliding = false;
            _glideVictims.Clear();
        }

        public override void _PhysicsProcess(double delta) {
            float dt = (float)delta;

            if (_isGliding) {
                _glideTimer -= dt;
                float hInput = Owner.CurrentInputFrame.Horizontal;

                var vel = Owner.Velocity;
                vel.X = hInput * _glideSpeed;
                vel.Y = Mathf.Min(vel.Y, GlideFallSpeedCap);
                Owner.Velocity = vel;

                if (Owner.HasStoryPerk(MidsummerGlidePerkKey)) {
                    StrikeGlidedThroughTargets();
                }

                if (_glideTimer <= 0 || Owner.IsOnFloor()) {
                    _isGliding = false;
                    _glideVictims.Clear();
                    if (CurrentPhase != AbilityPhase.Inactive) AdvanceToCleanup();
                }
                return;
            }

            base._PhysicsProcess(delta);
        }

        /// <summary>
        /// Midsummer Glide (Story-only): enemies Shakespeare passes through while
        /// gliding take 8 damage, once per target per glide.
        /// </summary>
        private void StrikeGlidedThroughTargets() {
            var space = Owner.GetWorld2D()?.DirectSpaceState;
            if (space == null) return;
            uint targetHurtboxLayer = Owner.PlayerIndex == 0
                ? FTT.Core.CollisionLayers.EnemyHurtbox
                : FTT.Core.CollisionLayers.PlayerHurtbox;
            var query = new PhysicsShapeQueryParameters2D {
                Shape = new CircleShape2D { Radius = GlideStrikeRadius },
                Transform = new Transform2D(0f, Owner.GlobalPosition),
                CollideWithAreas = true,
                CollideWithBodies = false,
                CollisionMask = StoryShapeQuery.DeliveryMask(targetHurtboxLayer)
            };

            foreach (Godot.Collections.Dictionary result in space.IntersectShape(query, 16)) {
                if (result["collider"].AsGodotObject() is not Hurtbox hurtbox) continue;
                if (hurtbox.OwnerPlayerIndex == Owner.PlayerIndex) continue;
                if (!_glideVictims.Add(hurtbox)) continue;

                HitPayload hit = Stamp(new HitPayload {
                    AttackerIndex = Owner.PlayerIndex,
                    AttackID = Data?.AbilityID ?? "shakespeare_prosperos_flight",
                    HitboxID = "glide_strike",
                    AttackClass = AttackClass.Special,
                    Damage = MidsummerGlideDamage * Owner.StorySpecialDamageMultiplier,
                    Knockback = new Vector2(2, -1),
                    HitstunDuration = 0.1f,
                    HitOrigin = Owner.GlobalPosition,
                    AttackerFacingRight = Owner.IsFacingRight,
                    AppliedStatus = FTT.Core.StatusType.None,
                    StatusDuration = 0f,
                    StatusIntensity = 1f,
                    ScreenShakeIntensity = 0.1f,
                    ScreenShakeDuration = 0.05f
                });
                float dealt = hurtbox.TakeHit(hit);
                Credit(in hit, dealt);
            }
        }
    }

    /// <summary>
    /// Ultimate — All the World's a Stage (design Section 5): a Globe Theatre
    /// set rises around Shakespeare and tragic phantoms — the three Witches,
    /// Romeo &amp; Juliet, and Hamlet — deliver the authored HitCount sequential
    /// strikes across the active window at the authored
    /// DamageTickIntervalFrames cadence. Every strike deals the authored
    /// per-hit BaseDamage across the authored HitboxSize stage footprint via
    /// hurtbox queries; only the closing strike (Hamlet) carries the authored
    /// KnockbackForce launch — earlier phantoms are impulse-free, mirroring the
    /// Fighter zone's pulse rules. Requires and consumes a full Ultimate Meter;
    /// ultimates bypass block by attack class.
    /// </summary>
    public partial class ShakespeareAllTheWorldsAStage : BaseSpecial {

        private const int MaxQueryResults = 16;

        private int _strikesDone;
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
            _strikesDone = 0;
            _activeFramesElapsed = 0;
            Owner.Velocity = Vector2.Zero;
            _meter?.Consume();
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
                // D15 (Package 13 W6): three phantom strikes, then Hamlet's finale.
                int hitCount = Data != null ? Data.CinematicHitCount : 1;
                int interval = Data?.DamageTickIntervalFrames > 0
                    ? Data.DamageTickIntervalFrames
                    : Mathf.Max(1, (Data?.ActiveFrames ?? hitCount) / hitCount);
                while (_strikesDone < hitCount && _activeFramesElapsed >= interval * (_strikesDone + 1)) {
                    _strikesDone++;
                    DealPhantomStrike();
                }
            }
            base._PhysicsProcess(delta);
        }

        private void DealPhantomStrike() {
            if (Owner == null || !IsInstanceValid(Owner)) return;
            var space = Owner.GetWorld2D()?.DirectSpaceState;
            if (space == null) return;

            uint targetHurtboxLayer = Owner.PlayerIndex == 0
                ? FTT.Core.CollisionLayers.EnemyHurtbox
                : FTT.Core.CollisionLayers.PlayerHurtbox;
            Vector2 stageSize = Data?.HitboxSize ?? new Vector2(720f, 240f);
            Vector2 authoredOffset = Data?.HitboxOffset ?? Vector2.Zero;
            Vector2 stageCenter = Owner.GlobalPosition + new Vector2(
                Owner.IsFacingRight ? authoredOffset.X : -authoredOffset.X, authoredOffset.Y);
            var query = new PhysicsShapeQueryParameters2D {
                Shape = new RectangleShape2D { Size = stageSize },
                Transform = new Transform2D(0f, stageCenter),
                CollideWithAreas = true,
                CollideWithBodies = false,
                CollisionMask = StoryShapeQuery.DeliveryMask(targetHurtboxLayer)
            };

            bool finale = _strikesDone >= (Data?.CinematicHitCount ?? 1);
            bool carriesStatus = Data?.CinematicHitCarriesStatus(_strikesDone) ?? true;
            foreach (Godot.Collections.Dictionary result in space.IntersectShape(query, MaxQueryResults)) {
                if (result["collider"].AsGodotObject() is not Hurtbox hurtbox) continue;
                if (hurtbox.OwnerPlayerIndex == Owner.PlayerIndex) continue;

                HitPayload hit = Stamp(new HitPayload {
                    AttackerIndex = Owner.PlayerIndex,
                    AttackID = Data?.AbilityID ?? "shakespeare_all_the_worlds_a_stage",
                    HitboxID = $"phantom_strike_{_strikesDone}",
                    AttackClass = AttackClass.Ultimate,
                    Damage = (Data?.CinematicHitDamage(_strikesDone) ?? 14f) * Owner.StorySpecialDamageMultiplier,
                    Knockback = finale
                        ? (Data?.KnockbackForce ?? new Vector2(5f, -3f)) * Owner.StoryKnockbackMultiplier
                        : Vector2.Zero,
                    HitstunDuration = finale ? UltimateActivationRules.FinaleHitstunSeconds : Data?.HitstunDuration ?? 0.2f,
                    HitOrigin = stageCenter,
                    AttackerFacingRight = Owner.IsFacingRight,
                    AppliedStatus = carriesStatus ? Data?.AppliedStatus ?? FTT.Core.StatusType.None : FTT.Core.StatusType.None,
                    StatusDuration = carriesStatus ? Data?.StatusDuration ?? 0f : 0f,
                    StatusIntensity = Data?.StatusIntensity ?? 1f,
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
    }
}
