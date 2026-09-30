using Godot;
using FTT.Combat;
using FTT.Characters;

namespace FTT.Characters.Abilities {

    /// <summary>
    /// Special 1 — Yorick's Lament: conjures Yorick's skull from <i>Hamlet</i>
    /// and throws it STRAIGHT (S03, Package 13 W7a — no longer rolling) at 10
    /// units/s up to 8 units; it deals minor
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
        /// <summary>S03 fallback wave radius (1.5 units) when the resource authors none.</summary>
        private const float DefaultWaveRadius = 90f;
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
            // S03 (Package 13 W7a): thrown straight at 10 units/s up to 8 units
            // (the authored speed x lifetime), 5 contact + 16 wave.
            float contactDamage = Data?.ProjectileContactDamage > 0f
                ? Data.ProjectileContactDamage
                : Mathf.Round((Data?.BaseDamage ?? 14f) * ContactDamageShare);
            var projectile = SpawnPlaceholderProjectile(
                Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? 40f : -40f, -10f),
                Data?.ProjectileSpeed ?? 600f, Owner.IsFacingRight, new Color(0.8f, 0.7f, 0.5f),
                new Vector2(18, 18), Data?.ProjectileLifetime ?? 0.8f, contactDamage);
            if (projectile != null) {
                projectile.DetonateOnImpact = true;
                // The wave owns the TimeDilation and the knockback; the skull's
                // contact is the minor stage (the slow is applied once).
                projectile.MakeMinorContactStage();
                // Bursts on a fighter, terrain or at max range.
                projectile.BurstsOnTerrain = Data?.ProjectileBurstsOnTerrain ?? true;
                projectile.Impacted += OnProjectileImpacted;
            }
        }

        /// <summary>The sonic wave's radius in pixels: the authored value, 1.5 units by default.</summary>
        public float WaveRadius => Data?.ProjectileBurstRadius > 0f ? Data.ProjectileBurstRadius : DefaultWaveRadius;

        /// <summary>Test seam: resolves the wave at <paramref name="impactPosition"/> synchronously.</summary>
        public void EmitSonicWaveForTest(Vector2 impactPosition) => EmitSonicWave(impactPosition);

        private void OnProjectileImpacted(Vector2 impactPosition) {
            // Area signals fire while the physics space is locked; defer the wave
            // query one step so the shape cast is legal.
            CallDeferred(nameof(EmitSonicWave), impactPosition);
        }

        private void EmitSonicWave(Vector2 impactPosition) {
            if (Owner == null || !IsInstanceValid(Owner)) return;

            // Wave flash visual only; damage and status are applied through the
            // shape query so the Macbeth's Curse perk can be evaluated per target.
            float waveRadius = WaveRadius;
            SpawnPlaceholderZone(impactPosition, 0f, 0.25f, 1f, new Color(0.7f, 0.65f, 0.9f), waveRadius);

            var space = Owner.GetWorld2D()?.DirectSpaceState;
            if (space == null) return;
            uint targetHurtboxLayer = Owner.PlayerIndex == 0
                ? FTT.Core.CollisionLayers.EnemyHurtbox
                : FTT.Core.CollisionLayers.PlayerHurtbox;
            var query = new PhysicsShapeQueryParameters2D {
                Shape = new CircleShape2D { Radius = waveRadius },
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
    /// Special 2 — The Tempest, raised by a summoned Ariel (A10 presentation):
    /// <b>a windbox, not a hit</b> (S01, Package 13 W7a). For the storm's
    /// authored active frames (12) every opponent inside the 2-unit radius,
    /// blocking or not, is pushed about 3 units outward — a positional shove
    /// through the body's own collision, so walls stop it. It consumes no block
    /// charge and causes no hitstun, hitstop or shieldstun, grants no DI or
    /// tumble, and builds no meter or Rally reclaim; nothing here builds a
    /// <see cref="HitPayload"/> at all. Targets in a grab state or an Ultimate
    /// cinematic and knockback-immune bosses are not moved. Shakespeare is
    /// lifted about 2.5 units over the same frames, then falls normally, with
    /// no invulnerability (S02).
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

        private const float PixelsPerUnit = KitMotionRules.StoryPixelsPerUnit;
        /// <summary>S02: 2.0-unit radius.</summary>
        public const float StormRadius = KitMotionRules.TempestRadiusUnits * PixelsPerUnit;
        /// <summary>S02: ~3 units outward over <see cref="KitMotionRules.TempestPushFrames"/> frames.</summary>
        public const float PushPerFramePixels =
            KitMotionRules.TempestPushUnits * PixelsPerUnit / KitMotionRules.TempestPushFrames;
        /// <summary>S02: the lift covers ~2.5 units over the push frames.</summary>
        private const float LiftSpeed =
            KitMotionRules.TempestLiftUnits * PixelsPerUnit * 60f / KitMotionRules.TempestPushFrames;

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
            _caught.Clear();
        }

        /// <summary>H-4: an interrupted storm stops pushing.</summary>
        protected override void OnInterrupted() => _caught.Clear();

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
            _caught.Clear();
            // The lift is spent: the recovery plays in the air, then a normal fall.
            if (Owner != null && Owner.Velocity.Y < 0f) Owner.Velocity = new Vector2(Owner.Velocity.X, 0f);
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
                Owner.Velocity = new Vector2(Owner.Velocity.X, -LiftSpeed);
                PushAdjacentTargets();
            }
            base._PhysicsProcess(delta);
        }

        /// <summary>One frame of the windbox. Public so tests can drive it without input.</summary>
        public void PushAdjacentTargets() {
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

            // A body the storm touches is caught for the rest of the storm, so
            // the shove carries its full ~3 units rather than stopping at the
            // radius edge (the sim's caught mask is the same rule).
            foreach (Godot.Collections.Dictionary result in space.IntersectShape(query, 16)) {
                if (result["collider"].AsGodotObject() is not Hurtbox hurtbox) continue;
                if (hurtbox.OwnerPlayerIndex == Owner.PlayerIndex) continue;
                CharacterBody2D body = FindBody(hurtbox);
                if (body != null) _caught.Add(body);
            }
            foreach (CharacterBody2D body in _caught) {
                if (body == null || !IsInstanceValid(body) || !CanBePushed(body)) continue;
                PushTargetAway(body);
            }
        }

        private readonly System.Collections.Generic.HashSet<CharacterBody2D> _caught = new();

        /// <summary>S01: grab states, an Ultimate cinematic and immovable bosses are never moved.</summary>
        public static bool CanBePushed(CharacterBody2D body) => body switch {
            PlayerController player => player.CurrentState is not (CharacterState.Grabbing
                or CharacterState.Thrown or CharacterState.UsingUltimate or CharacterState.Dead),
            FTT.Enemies.EnemyController enemy => !enemy.IsHeldByPlayer,
            FTT.Enemies.BossController boss => boss.Data?.IsKnockbackImmune != true,
            _ => true
        };

        private void PushTargetAway(CharacterBody2D body) {
            float away = body.GlobalPosition.X - Owner.GlobalPosition.X;
            float direction = away > 0f ? 1f : away < 0f ? -1f : (Owner.IsFacingRight ? 1f : -1f);
            // Story-only KnockbackForce minors strengthen the storm's push.
            float step = PushPerFramePixels * Owner.StoryKnockbackMultiplier;
            // A positional shove through the body's own collision: walls stop it,
            // and it touches neither velocity nor hit state.
            body.MoveAndCollide(new Vector2(direction * step, 0f));
        }

        private static CharacterBody2D FindBody(Hurtbox hurtbox) {
            Node current = hurtbox.GetParent();
            while (current != null) {
                if (current is CharacterBody2D body) return body;
                current = current.GetParent();
            }
            return null;
        }
    }

    /// <summary>
    /// Movement — Prospero's Flight: a single strong <b>gust burst</b> (A08,
    /// Package 13 W7a) — about 4 units forward and 2.5 up over 20 frames along
    /// facing, then a normal fall. No sustained glide, and (F15) no teleport or
    /// invulnerability. Distance comes from the authored MovementAbilityData;
    /// the rise and frame count from <c>KitMotionRules</c>, shared with the sim.
    /// Story-only Resonance perk <b>Midsummer Gust</b> (node ID and perk key
    /// kept as <c>midsummer_glide</c>): 20% more gust distance and 8 damage to
    /// enemies the gust carries him through, each struck once per gust.
    /// </summary>
    public partial class ShakespeareProsperosFlight : BaseSpecial {

        /// <summary>Retained perk key (the node is displayed as Midsummer Gust).</summary>
        public const string MidsummerGlidePerkKey = "midsummer_glide";

        private const float PixelsPerUnit = KitMotionRules.StoryPixelsPerUnit;
        private const float GustStrikeRadius = 50f;
        /// <summary>Midsummer Gust: 8.0 contact damage.</summary>
        public const float MidsummerGustDamage = 8f;

        private bool _isGusting;
        private Vector2 _gustVelocity;
        private Vector2 _startPosition;
        private readonly System.Collections.Generic.HashSet<Hurtbox> _gustVictims = new();

        private MovementAbilityData MovementData => Data as MovementAbilityData;

        /// <summary>True while the gust is carrying him. Test seam.</summary>
        public bool IsGusting => _isGusting;

        /// <summary>The gust's velocity in px/s (x along facing, y up is negative). Test seam.</summary>
        public Vector2 GustVelocity => _gustVelocity;

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
            _startPosition = Owner.GlobalPosition;
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
            int frames = Mathf.Max(1, Data?.ActiveFrames ?? KitMotionRules.ProsperoGustFrames);
            float seconds = frames / 60f;
            float forward = MovementData?.DistanceMoved > 0f
                ? MovementData.DistanceMoved
                : KitMotionRules.ProsperoGustForwardUnits * PixelsPerUnit;
            if (Owner.HasStoryPerk(MidsummerGlidePerkKey)) forward *= KitMotionRules.MidsummerGustDistanceMultiplier;
            float rise = KitMotionRules.ProsperoGustRiseUnits * PixelsPerUnit;
            float facing = Owner.IsFacingRight ? 1f : -1f;
            _gustVelocity = new Vector2(facing * forward / seconds, -rise / seconds);
            _isGusting = true;
            _gustVictims.Clear();
            Owner.Velocity = _gustVelocity;
        }

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
            EndGust();
            FTT.Core.EventBus.Instance?.RaiseMovementAbilityUsed(new FTT.Core.MovementAbilityPayload {
                PlayerIndex = Owner.PlayerIndex,
                AbilityName = Data?.AbilityName ?? "Prospero's Flight",
                StartPosition = _startPosition,
                EndPosition = Owner.GlobalPosition
            });
        }

        /// <summary>H-4: a stun/death mid-cast releases the gust's velocity steering.</summary>
        protected override void OnInterrupted() {
            _isGusting = false;
            _gustVictims.Clear();
        }

        public override void _PhysicsProcess(double delta) {
            if (_isGusting && CurrentPhase == AbilityPhase.Active && Owner != null) {
                Owner.Velocity = _gustVelocity;
                if (Owner.HasStoryPerk(MidsummerGlidePerkKey)) StrikeGustedThroughTargets();
            }
            base._PhysicsProcess(delta);
        }

        /// <summary>A burst, not a glide: it ends with its momentum spent, then the fall is the ordinary one.</summary>
        private void EndGust() {
            if (!_isGusting) return;
            _isGusting = false;
            _gustVictims.Clear();
            if (Owner != null) Owner.Velocity = Vector2.Zero;
        }

        /// <summary>
        /// Midsummer Gust (Story-only): enemies Shakespeare passes through during
        /// the gust take 8 damage, once per target per gust.
        /// </summary>
        private void StrikeGustedThroughTargets() {
            var space = Owner.GetWorld2D()?.DirectSpaceState;
            if (space == null) return;
            uint targetHurtboxLayer = Owner.PlayerIndex == 0
                ? FTT.Core.CollisionLayers.EnemyHurtbox
                : FTT.Core.CollisionLayers.PlayerHurtbox;
            var query = new PhysicsShapeQueryParameters2D {
                Shape = new CircleShape2D { Radius = GustStrikeRadius },
                Transform = new Transform2D(0f, Owner.GlobalPosition),
                CollideWithAreas = true,
                CollideWithBodies = false,
                CollisionMask = StoryShapeQuery.DeliveryMask(targetHurtboxLayer)
            };

            foreach (Godot.Collections.Dictionary result in space.IntersectShape(query, 16)) {
                if (result["collider"].AsGodotObject() is not Hurtbox hurtbox) continue;
                if (hurtbox.OwnerPlayerIndex == Owner.PlayerIndex) continue;
                if (!_gustVictims.Add(hurtbox)) continue;

                HitPayload hit = Stamp(new HitPayload {
                    AttackerIndex = Owner.PlayerIndex,
                    AttackID = Data?.AbilityID ?? "shakespeare_prosperos_flight",
                    HitboxID = "gust_strike",
                    AttackClass = AttackClass.Special,
                    Damage = MidsummerGustDamage * Owner.StorySpecialDamageMultiplier,
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
        private const float FinaleHitstunDuration = 0.5f;

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
                int hitCount = Mathf.Max(1, Data?.HitCount ?? 1);
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

            bool finale = _strikesDone >= (Data?.HitCount ?? 1);
            foreach (Godot.Collections.Dictionary result in space.IntersectShape(query, MaxQueryResults)) {
                if (result["collider"].AsGodotObject() is not Hurtbox hurtbox) continue;
                if (hurtbox.OwnerPlayerIndex == Owner.PlayerIndex) continue;

                HitPayload hit = Stamp(new HitPayload {
                    AttackerIndex = Owner.PlayerIndex,
                    AttackID = Data?.AbilityID ?? "shakespeare_all_the_worlds_a_stage",
                    HitboxID = $"phantom_strike_{_strikesDone}",
                    AttackClass = AttackClass.Ultimate,
                    Damage = (Data?.BaseDamage ?? 14f) * Owner.StorySpecialDamageMultiplier,
                    Knockback = finale
                        ? (Data?.KnockbackForce ?? new Vector2(5f, -3f)) * Owner.StoryKnockbackMultiplier
                        : Vector2.Zero,
                    HitstunDuration = finale ? FinaleHitstunDuration : Data?.HitstunDuration ?? 0.2f,
                    HitOrigin = stageCenter,
                    AttackerFacingRight = Owner.IsFacingRight,
                    AppliedStatus = Data?.AppliedStatus ?? FTT.Core.StatusType.None,
                    StatusDuration = Data?.StatusDuration ?? 0f,
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
