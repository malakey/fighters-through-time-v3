using Godot;
using FTT.Combat;
using FTT.Characters;

namespace FTT.Characters.Abilities {

    /// <summary>
    /// Special 1 — Righteous Smite (J02, Package 13 W7b): a downward broadsword
    /// swing that sends a SHORT ground shockwave rolling about 2.5 units along
    /// the floor. It is a ground wave, not a projectile: grounded targets only,
    /// low knockback with the authored 30 frames of hitstun and no launch, so
    /// the Radiant Burn string can follow. Damage, speed, travel (speed ×
    /// lifetime), hitstun, status and phase frames all come from the authored
    /// AbilityData. Story-only Resonance perk Unstoppable Crusade grants
    /// hyper-armor through the active swing plus 1.5 s after casting.
    /// </summary>
    public partial class JoanRighteousSmite : BaseSpecial {

        public const string UnstoppableCrusadePerkKey = "unstoppable_crusade";

        private const float PostCastHyperArmorSeconds = 1.5f;
        private const float WaveLeadPixels = 30f;

        private readonly StoryGroundWave _wave = new();

        /// <summary>The live shockwave (test seam).</summary>
        public StoryGroundWave Wave => _wave;

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
            float speed = Data?.ProjectileSpeed > 0f ? Data.ProjectileSpeed : 600f;
            float lifetime = Data?.ProjectileLifetime > 0f ? Data.ProjectileLifetime : 0.25f;
            _wave.Start(
                Owner.GetWorld2D()?.DirectSpaceState,
                Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? WaveLeadPixels : -WaveLeadPixels, 0f),
                Owner.IsFacingRight, speed, speed * lifetime,
                Data?.HitboxSize ?? new Vector2(40f, 30f), groundedOnly: true);
            SpawnPlaceholderZone(_wave.Front, 0f, lifetime, 1f, new Color(0.95f, 0.85f, 0.3f),
                (Data?.HitboxSize.Y ?? 30f) * 0.5f);
        }

        public override void _PhysicsProcess(double delta) {
            base._PhysicsProcess(delta);
            if (!_wave.Active || Owner == null || !IsInstanceValid(Owner)) return;
            _wave.Advance((float)delta, Owner.GetWorld2D()?.DirectSpaceState,
                StoryShapeQuery.DeliveryMask(TargetHurtboxLayer()), Owner.PlayerIndex, StrikeWithWave);
        }

        private uint TargetHurtboxLayer() => Owner.PlayerIndex == 0
            ? FTT.Core.CollisionLayers.EnemyHurtbox
            : FTT.Core.CollisionLayers.PlayerHurtbox;

        /// <summary>One shockwave contact: the authored Special hit, delivered once per target.</summary>
        internal void StrikeWithWave(Hurtbox hurtbox, Vector2 front) {
            string abilityID = Data?.AbilityID ?? JoanAscendantWings.RighteousSmiteAttackID;
            HitPayload hit = Stamp(new HitPayload {
                AttackerIndex = Owner.PlayerIndex,
                AttackID = abilityID,
                HitboxID = "ground_wave",
                AttackClass = AttackClass.Special,
                // Minor Smite Damage is ability-scoped (AbilityDamage keyed by
                // the ability ID); the wave delivers through a direct TakeHit, so
                // the lane is read here as the Emancipator's wave always did.
                Damage = (Data?.BaseDamage ?? 28f) * Owner.StorySpecialDamageMultiplier
                    * Owner.StoryScoped("AbilityDamage", abilityID),
                Knockback = (Data?.KnockbackForce ?? new Vector2(2f, -1f)) * Owner.StoryKnockbackMultiplier,
                HitstunDuration = Data?.HitstunDuration ?? 0.5f,
                HitOrigin = front,
                AttackerFacingRight = _wave.MovingRight,
                AppliedStatus = Data?.AppliedStatus ?? FTT.Core.StatusType.RadiantBurn,
                StatusDuration = (Data?.StatusDuration ?? 3f) * Owner.StoryStatusDurationMultiplier,
                StatusIntensity = Data?.StatusIntensity ?? 1f,
                ScreenShakeIntensity = Data?.ScreenShakeIntensity ?? 0.2f,
                ScreenShakeDuration = Data?.ScreenShakeDuration ?? 0.15f
            });
            float dealt = hurtbox.TakeHit(hit);
            Credit(in hit, dealt);
            if (dealt > 0f) {
                EmitImpactVfx(front);
                // Wings Refresh reads "a direct Righteous Smite hit" off the
                // shared hit-landed hook the Hitbox path raises.
                Owner.NotifyStoryHitLanded(hit);
            }
        }
    }

    /// <summary>
    /// Special 2 — Divine Piercing (J01/J03, Package 13 W7b): Joan LUNGES about
    /// 3 units forward across the active frames while her broadsword thrusts —
    /// her ground gap-closer. The authored HitCount thrusts (3 × 8 = 24) are
    /// spread across the active frames; only the last carries the authored
    /// knockback, so the flurry lands in full. It is one of the three authored
    /// Shield-Breakers (A01): against a blocking opponent the first absorbed
    /// thrust spends every remaining charge through the generic block path
    /// (<c>BlockClass.ShieldBreaker</c> on the resource).
    /// </summary>
    public partial class JoanDivinePiercing : BaseSpecial {

        private const int MaxQueryResults = 16;

        private int _thrustsDone;
        private int _activeFramesElapsed;
        private bool _lungeRight;

        /// <summary>The lunge speed in px/s: the J01 distance over the authored active frames.</summary>
        public float LungeSpeedPixelsPerSecond =>
            (float)KitReachRules.DivinePiercingLungeUnits * KitMotionRules.StoryPixelsPerUnit
                * 60f / Mathf.Max(1, Data?.ActiveFrames ?? 12);

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
            _thrustsDone = 0;
            _activeFramesElapsed = 0;
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
            _lungeRight = Owner.IsFacingRight;
        }

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
            if (Owner != null) Owner.Velocity = new Vector2(0f, Owner.Velocity.Y);
        }

        protected override void OnInterrupted() {
            if (Owner != null && IsInstanceValid(Owner)) Owner.Velocity = new Vector2(0f, Owner.Velocity.Y);
        }

        public override void _PhysicsProcess(double delta) {
            if (CurrentPhase == AbilityPhase.Active) {
                Owner.Velocity = new Vector2(
                    _lungeRight ? LungeSpeedPixelsPerSecond : -LungeSpeedPixelsPerSecond,
                    Owner.Velocity.Y);
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
                    Damage = (Data?.BaseDamage ?? 8f) * Owner.StorySpecialDamageMultiplier,
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
                // Only the final thrust launches; the others hold.
                if (!finalThrust) hit.Launches = false;
                float dealt = hurtbox.TakeHit(hit);
                Credit(in hit, dealt);
            }
        }

    }

    /// <summary>
    /// Movement — Ascendant Wings (A08, Package 13 W7b): a rising slash-leap,
    /// usable in the air. The rise is unchanged (D13,
    /// <c>VERIFY-WING-DIVE-LEAP</c>): the hero climbs at the authored
    /// <c>MovementSpeed</c> for the cast's authored startup + active + recovery
    /// frames. There is no glide. Holding the movement button when the rise
    /// ends starts the <b>Wing-Dive</b> — a steep forward descent for up to the
    /// authored <c>MovementDuration</c> (1 s); releasing the button ends it into
    /// a normal fall, and pressing Attack during it ends it into her aerial
    /// string (<c>PlayerController.ProcessUsingMovementAbility</c> reads
    /// <see cref="IsWingDiving"/>). The Fighter sim runs the same rise and dive
    /// (<c>FighterKitMotion.WingRise</c> / <c>WingDive</c>).
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

        private const float DefaultLeapSpeed = 420f;
        private const float DefaultDiveSeconds = 1f;

        private bool _isDiving;
        private int _diveFramesRemaining;
        private bool _diveRight;

        /// <summary>True while the held Wing-Dive is descending.</summary>
        public bool IsWingDiving => _isDiving;

        /// <summary>The dive velocity in px/s (Y down): forward and steeply downward.</summary>
        public static Vector2 WingDiveVelocity(bool facingRight) => new(
            (facingRight ? 1f : -1f) * (float)KitReachRules.WingDiveForwardUnitsPerSecond * KitMotionRules.StoryPixelsPerUnit,
            (float)KitReachRules.WingDiveDescentUnitsPerSecond * KitMotionRules.StoryPixelsPerUnit);

        private MovementAbilityData MovementData => Data as MovementAbilityData;

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
            _isDiving = false;
            _diveFramesRemaining = 0;
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

        /// <summary>H-4: a stun/death mid-cast ends the dive's velocity hold.</summary>
        protected override void OnInterrupted() {
            _isDiving = false;
        }

        /// <summary>
        /// A08: Attack during the dive ends it; the owner then starts the aerial
        /// string on the same frame. Returns true when a dive was cancelled.
        /// </summary>
        public bool TryCancelDiveForAttack() {
            if (!_isDiving) return false;
            _isDiving = false;
            Interrupt();
            return true;
        }

        /// <summary>
        /// The rise is over. A held button in the air turns it into the
        /// Wing-Dive (the cast stays live until the dive ends); otherwise the
        /// cast ends into a normal fall.
        /// </summary>
        protected override void OnCleanup() {
            if (!_isDiving && Owner != null && !Owner.IsOnFloor()
                && Owner.CurrentInputFrame.IsHeld(FTT.Core.GameplayButtons.MovementAbility)) {
                _isDiving = true;
                _diveRight = Owner.IsFacingRight;
                _diveFramesRemaining = Mathf.RoundToInt(
                    (MovementData?.MovementDuration > 0f ? MovementData.MovementDuration : DefaultDiveSeconds) * 60f);
                return;
            }
            _isDiving = false;
            base.OnCleanup();
        }

        public override void _PhysicsProcess(double delta) {
            if (_isDiving) {
                _diveFramesRemaining--;
                Owner.Velocity = WingDiveVelocity(_diveRight);
                if (_diveFramesRemaining <= 0 || Owner.IsOnFloor()
                    || !Owner.CurrentInputFrame.IsHeld(FTT.Core.GameplayButtons.MovementAbility)) {
                    _isDiving = false;
                    base.OnCleanup();
                }
                return;
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
