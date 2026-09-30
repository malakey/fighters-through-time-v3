using Godot;
using FTT.Combat;
using FTT.Characters;
using FTT.Core;

namespace FTT.Characters.Abilities {

    /// <summary>
    /// Special 1 — The Emancipator: Lincoln slams his rail into the ground and a
    /// shockwave travels forward along the floor, knocking enemies upward. Since
    /// Package 13 W7b (LN03) it is the shared ground wave
    /// (<see cref="StoryGroundWave"/>): 5 units at 10 units/s, grounded targets
    /// only — a jump clears it — ending at a wall or where the floor ends. It is
    /// an authored Shield-Breaker (A01, Package 13 W1): a blocked wave spends
    /// every remaining charge. Timing, damage, speed, and travel come from the
    /// authored AbilityData (travel distance = ProjectileSpeed x
    /// ProjectileLifetime). Story-only Resonance perk Executive Order: +50%
    /// travel distance (7.5 units) and +20% damage.
    /// </summary>
    public partial class LincolnEmancipator : BaseSpecial {

        public const string ExecutiveOrderPerkKey = "executive_order";

        private const float ExecutiveOrderTravelMultiplier = 1.5f;
        private const float ExecutiveOrderDamageMultiplier = 1.2f;
        private const int WaveVisualIntervalFrames = 6;
        private const float WaveLeadPixels = 50f;

        private readonly StoryGroundWave _wave = new();
        private float _waveDamage;
        private int _waveVisualCountdown;

        /// <summary>The live shockwave (test seam).</summary>
        public StoryGroundWave Wave => _wave;

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
            StartWave();
        }

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
        }

        private void StartWave() {
            if (Owner == null) return;
            bool executiveOrder = Owner.HasStoryPerk(ExecutiveOrderPerkKey);
            float speed = Data?.ProjectileSpeed > 0f ? Data.ProjectileSpeed : 600f;
            float travel = speed * (Data?.ProjectileLifetime > 0f ? Data.ProjectileLifetime : 0.5f)
                * (executiveOrder ? ExecutiveOrderTravelMultiplier : 1f);
            // Package 11 A4: V7.6 re-scopes "Minor Shockwave Damage" from the
            // character-wide SpecialDamage lane onto
            // AbilityDamage(lincoln_emancipator). The wave resolves its hits
            // through a direct TakeHit, so the lane is read here rather than in
            // Hitbox.CreatePayload.
            _waveDamage = (Data?.BaseDamage ?? 20f)
                * (executiveOrder ? ExecutiveOrderDamageMultiplier : 1f)
                * Owner.StorySpecialDamageMultiplier
                * Owner.StoryScoped("AbilityDamage", Data?.AbilityID ?? "lincoln_emancipator");
            _wave.Start(
                Owner.GetWorld2D()?.DirectSpaceState,
                Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? WaveLeadPixels : -WaveLeadPixels, 0f),
                Owner.IsFacingRight, speed, travel,
                Data?.HitboxSize ?? new Vector2(60f, 50f), groundedOnly: true);
            _waveVisualCountdown = 0;
        }

        public override void _PhysicsProcess(double delta) {
            base._PhysicsProcess(delta);
            if (!_wave.Active) return;
            if (Owner == null || !IsInstanceValid(Owner)) {
                _wave.Stop();
                return;
            }

            uint targetHurtboxLayer = Owner.PlayerIndex == 0
                ? FTT.Core.CollisionLayers.EnemyHurtbox
                : FTT.Core.CollisionLayers.PlayerHurtbox;
            _wave.Advance((float)delta, Owner.GetWorld2D()?.DirectSpaceState,
                StoryShapeQuery.DeliveryMask(targetHurtboxLayer), Owner.PlayerIndex, StrikeWithWave);

            if (_wave.Active && --_waveVisualCountdown <= 0) {
                _waveVisualCountdown = WaveVisualIntervalFrames;
                SpawnPlaceholderZone(
                    _wave.Front, 0f, 0.2f, 1f, new Color(0.2f, 0.2f, 0.5f),
                    (Data?.HitboxSize.Y ?? 50f) / 2f);
            }
        }

        /// <summary>One shockwave contact, delivered once per grounded target.</summary>
        internal void StrikeWithWave(Hurtbox hurtbox, Vector2 front) {
            // V7.6 F15 / A01 (Package 13 W1): The Emancipator is a
            // Shield-Breaker FULL shatter. Stamp() carries the resource's
            // BlockClass.ShieldBreaker onto the payload, so the normal
            // BlockSystem.ResolveHit response takes 1, 2 or 3 charges to 0;
            // an ordinary Special would spend only two.
            HitPayload hit = Stamp(new HitPayload {
                AttackerIndex = Owner.PlayerIndex,
                AttackID = Data?.AbilityID ?? "lincoln_emancipator",
                HitboxID = "ground_wave",
                AttackClass = AttackClass.Special,
                Damage = _waveDamage,
                Knockback = Data?.KnockbackForce ?? new Vector2(2f, -6f),
                HitstunDuration = Data?.HitstunDuration ?? 0.2f,
                HitOrigin = front,
                AttackerFacingRight = _wave.MovingRight,
                AppliedStatus = FTT.Core.StatusType.None,
                StatusDuration = 0f,
                StatusIntensity = 1f,
                ScreenShakeIntensity = Data?.ScreenShakeIntensity ?? 0.2f,
                ScreenShakeDuration = Data?.ScreenShakeDuration ?? 0.15f
            });
            float dealt = hurtbox.TakeHit(hit);
            Credit(in hit, dealt);
        }

    }

    /// <summary>
    /// Special 2 — Splitting Strike (LN03, Package 13 W7b: a 2.2-unit arc, the
    /// authored HitboxSize/HitboxOffset): a massive overhead arc dealing the authored
    /// damage through the shared hitbox contract. A blocking target loses every
    /// charge at once (an authored Shield-Breaker — A01, Package 13 W1); airborne
    /// targets caught in the arc are spiked straight down. The Story-only
    /// Resonance perk Kinetic Splitting (V7.6) bounces a spiked target off the
    /// ground into a follow-up window and adds +50% against Chronal Extractors
    /// and enemy constructs.
    /// </summary>
    public partial class LincolnSplittingStrike : BaseSpecial {

        /// <summary>
        /// Story-only Resonance perk key. V7.6 FULL REWORK - nothing of the V6
        /// perk survives. The V6 effect merely re-classed combo hit 3 as
        /// Special, which restated the baseline and granted nothing. Now:
        /// the spike GROUND-BOUNCES an airborne target, opening a 20-frame
        /// follow-up window that true-combos into the string's launching Hit 2
        /// (PvE only, and subject to the V7.4 Stagger Discipline's diminishing
        /// special stun); and the strike deals +50% to Chronal Extractors and
        /// enemy constructs (resolved in <see cref="FTT.Environment.StoryExtractorDamage"/>).
        /// </summary>
        public const string KineticSplittingPerkKey = "kinetic_splitting";

        /// <summary>V7.6 Kinetic Splitting: the follow-up window a ground bounce opens.</summary>
        public const int GroundBounceWindowFrames = 20;
        /// <summary>V7.6 Kinetic Splitting: upward speed the ground bounce imparts.</summary>
        public const float GroundBounceUpwardSpeed = 260f;

        private const float SpikeDownwardSpeed = 400f;

        /// <summary>
        /// Enemies spiked this execution that are still falling toward the
        /// bounce. Cleared when the ability leaves its active phase.
        /// </summary>
        private readonly System.Collections.Generic.List<CharacterBody2D> _bounceWatch = new();

        private Hitbox _overheadHitbox;

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
            ActivateOverheadHitbox();
        }

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
            _overheadHitbox?.Deactivate();
        }

        /// <summary>H-4: interruption mid-arc must not leave the overhead hitbox live.</summary>
        protected override void OnInterrupted() {
            _overheadHitbox?.Deactivate();
        }

        private void ActivateOverheadHitbox() {
            _overheadHitbox = GetOrCreateChildHitbox("OverheadHitbox");
            if (_overheadHitbox == null || Owner == null) return;

            _overheadHitbox.Damage = (Data?.BaseDamage ?? 18f) * Owner.StorySpecialDamageMultiplier;
            // LN03 (Package 13 W7b): the authored vector is the airborne spike;
            // a grounded target takes its horizontal half as grounded knockback
            // (no launch), and SpikeAirborneTargets drives the airborne ones
            // down — the same split the Fighter sim applies.
            Vector2 authored = Data?.KnockbackForce ?? new Vector2(4f, 5f);
            _overheadHitbox.KnockbackForce = new Vector2(authored.X, 0f);
            _overheadHitbox.Launches = false;
            _overheadHitbox.OwnerPlayerIndex = Owner.PlayerIndex;
            _overheadHitbox.SourcePlayer = Owner;

            var offset = Data?.HitboxOffset ?? new Vector2(30f, 0f);
            if (!Owner.IsFacingRight) offset.X = -offset.X;
            _overheadHitbox.GlobalPosition = Owner.GlobalPosition + offset;
            _overheadHitbox.Activate();
        }

        public override void _PhysicsProcess(double delta) {
            base._PhysicsProcess(delta);
            if (CurrentPhase == AbilityPhase.Active) SpikeAirborneTargets();
            ResolveGroundBounces();
        }

        /// <summary>
        /// Kinetic Splitting (Story-only, V7.6). A spiked enemy that reaches
        /// the floor BOUNCES back up and is held in a
        /// <see cref="GroundBounceWindowFrames"/>-frame follow-up window, so
        /// Lincoln can true-combo into the string's launching Hit 2. PvE only:
        /// the watch list only ever holds <c>EnemyController</c> bodies, and
        /// nothing here reaches <c>scripts/FighterSim/</c>. The stun the window
        /// applies runs through the enemy's ordinary Special-class intake, so
        /// the V7.4 Stagger Discipline's diminishing special stun and its
        /// getup armor both still govern it.
        /// </summary>
        private void ResolveGroundBounces() {
            if (_bounceWatch.Count == 0) return;
            for (int i = _bounceWatch.Count - 1; i >= 0; i--) {
                CharacterBody2D body = _bounceWatch[i];
                if (body == null || !GodotObject.IsInstanceValid(body) || !body.IsInsideTree()) {
                    _bounceWatch.RemoveAt(i);
                    continue;
                }
                if (!body.IsOnFloor()) continue;
                _bounceWatch.RemoveAt(i);
                body.Velocity = new Vector2(body.Velocity.X, -GroundBounceUpwardSpeed);
                if (body is FTT.Enemies.EnemyController enemy) {
                    enemy.ApplyStun(GroundBounceWindowFrames / 60f);
                }
            }
        }

        /// <summary>Test seam: enemies currently awaiting their ground bounce.</summary>
        public int PendingGroundBounces => _bounceWatch.Count;

        /// <summary>
        /// Design: the overhead arc spikes airborne enemies directly downward.
        /// Runs each active frame so a target entering the arc mid-swing is still
        /// spiked after the shared hitbox contact resolves its damage.
        /// </summary>
        private void SpikeAirborneTargets() {
            if (_overheadHitbox == null || !_overheadHitbox.IsActive || Owner == null) return;
            Godot.Collections.Array<Area2D> areas = _overheadHitbox.GetOverlappingAreas();
            using var areasLifetime = areas.AsDisposable();
            foreach (var area in areas) {
                if (area is not Hurtbox hurtbox || hurtbox.OwnerPlayerIndex == Owner.PlayerIndex) continue;
                Node current = hurtbox.GetParent();
                while (current != null) {
                    if (current is CharacterBody2D body) {
                        if (!body.IsOnFloor()) {
                            body.Velocity = new Vector2(body.Velocity.X, SpikeDownwardSpeed);
                            // Kinetic Splitting: remember the spiked body so the
                            // ground contact bounces it into the follow-up window.
                            if (Owner.HasStoryPerk(KineticSplittingPerkKey)
                                && body is FTT.Enemies.EnemyController
                                && !_bounceWatch.Contains(body)) {
                                _bounceWatch.Add(body);
                            }
                        }
                        break;
                    }
                    current = current.GetParent();
                }
            }
        }
    }

    /// <summary>
    /// Movement — Rail Charge (LN02, Package 13 W7b): a SHORT ARMORED BURST
    /// behind the wooden rail — the authored distance (5 units) over the
    /// authored duration (30 frames), usable in the air for horizontal recovery.
    /// Lincoln has hyper-armor for the charge (damage yes, hitstun no via
    /// StoryCombatRules); grabs still beat it. On contact the ram deals the
    /// authored 6 damage with moderate horizontal knockback (not a launch) and
    /// the charge STOPS. The retired 3-second duration is gone. Story-only
    /// Resonance perk Homestead Bulwark: landing a charge hit grants 3 s of
    /// hyper-armor.
    /// </summary>
    public partial class LincolnRailCharge : BaseSpecial {

        public const string HomesteadBulwarkPerkKey = "homestead_bulwark";

        /// <summary>
        /// Story-only Resonance TRAVERSAL flag (V7.6, Tier 2, F06 Option A):
        /// each Rail Charge ACTIVATION destroys ONE hostile projectile authored
        /// as breakable during its travel, resolved BEFORE that projectile can
        /// apply damage or on-hit effects. The allowance is consumed once per
        /// activation, never per frame or per overlap; later projectiles and
        /// unbreakable attacks resolve normally against his existing armor.
        /// Beams, persistent zones, environmental hazards and unbreakable
        /// projectiles are never destroyed. No reflection, no extra damage, no
        /// blanket invulnerability, and NO effect on the universal roll.
        /// Baseline Rail Charge breaks nothing.
        /// </summary>
        public const string RailBreakerPerkKey = "rail_breaker";

        /// <summary>V7.6 Rail Breaker: projectiles destroyed per activation.</summary>
        public const int RailBreakerProjectilesPerActivation = 1;

        /// <summary>True once this activation has spent its break. Test seam.</summary>
        public bool RailBreakerSpent { get; private set; }

        private const float HomesteadBulwarkArmorSeconds = 3f;

        private Vector2 _chargeDirection;
        private Vector2 _startPosition;
        private float _chargeDuration = 0.5f;
        private float _chargeSpeed = 600f;
        private bool _contactStopPending;

        /// <summary>The charge speed in px/s: the authored distance over the authored duration.</summary>
        public float ChargeSpeedPixelsPerSecond => _chargeSpeed;
        private readonly System.Collections.Generic.HashSet<ulong> _struckHurtboxes = new();

        private MovementAbilityData MovementData => Data as MovementAbilityData;

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
            _chargeDuration = MovementData?.MovementDuration > 0f ? MovementData.MovementDuration : 0.5f;
            _chargeSpeed = MovementData?.DistanceMoved > 0f
                ? MovementData.DistanceMoved / _chargeDuration
                : MovementData?.MovementSpeed > 0f ? MovementData.MovementSpeed : 600f;
            _contactStopPending = false;
            _chargeDirection = Owner.IsFacingRight ? Vector2.Right : Vector2.Left;
            _startPosition = Owner.GlobalPosition;
            _struckHurtboxes.Clear();
            // One break per ACTIVATION, armed here and spent at most once.
            RailBreakerSpent = false;
        }

        protected override void OnActive() {
            PhaseTimer = _chargeDuration;
        }

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
            FTT.Core.EventBus.Instance?.RaiseMovementAbilityUsed(new FTT.Core.MovementAbilityPayload {
                PlayerIndex = Owner.PlayerIndex,
                AbilityName = Data?.AbilityName ?? "Rail Charge",
                StartPosition = _startPosition,
                EndPosition = Owner.GlobalPosition
            });
        }

        public override void _PhysicsProcess(double delta) {
            if (CurrentPhase == AbilityPhase.Active) {
                Owner.Velocity = _chargeDirection * _chargeSpeed;
                // Rail Breaker runs BEFORE contact resolution so the broken
                // projectile never applies its damage or on-hit effects.
                TryBreakProjectile();
                StrikeContacts();
                if (_contactStopPending) {
                    // LN02: the charge stops dead on contact — travel and armor
                    // (the Active phase) both end.
                    _contactStopPending = false;
                    Owner.Velocity = new Vector2(0f, Owner.Velocity.Y);
                    AdvanceToRecovery();
                }
            }
            base._PhysicsProcess(delta);
        }

        protected override void OnInterrupted() {
            _contactStopPending = false;
        }

        /// <summary>
        /// Rail Breaker (Story-only traversal node, V7.6). Destroys at most one
        /// breakable hostile projectile per activation, checked each active
        /// frame until the single allowance is spent. Deliberately reads the
        /// pooled projectile groups rather than a collision layer: beams,
        /// persistent zones and environmental hazards never join those groups
        /// and so can never be destroyed.
        /// </summary>
        private void TryBreakProjectile() {
            if (Owner == null || RailBreakerSpent) return;
            if (!Owner.HasStoryPerk(RailBreakerPerkKey)) return;
            Vector2 offset = Data?.HitboxOffset ?? new Vector2(30f, 0f);
            if (!Owner.IsFacingRight) offset.X = -offset.X;
            Vector2 center = Owner.GlobalPosition + offset;
            Vector2 extents = (Data?.HitboxSize ?? new Vector2(60, 50)) * 0.5f;

            Godot.Collections.Array<Node> shots =
                Owner.GetTree()?.GetNodesInGroup(FTT.Enemies.EnemyProjectile.GroupName);
            if (shots == null) return;
            using var lifetime = shots.AsDisposable();
            foreach (Node node in shots) {
                if (node is not FTT.Enemies.EnemyProjectile shot) continue;
                if (!shot.IsBreakable || !shot.IsInsideTree()) continue;
                Vector2 delta = shot.GlobalPosition - center;
                if (Mathf.Abs(delta.X) > extents.X || Mathf.Abs(delta.Y) > extents.Y) continue;
                shot.ReturnToPool();
                RailBreakerSpent = true;
                return;
            }
        }

        /// <summary>
        /// LN02: the ram. The first contact deals the authored damage with the
        /// authored horizontal knockback and hitstun (no launch) and stops the
        /// charge; each target is struck at most once.
        /// </summary>
        private void StrikeContacts() {
            if (Owner == null) return;
            var space = Owner.GetWorld2D()?.DirectSpaceState;
            if (space == null) return;

            var offset = Data?.HitboxOffset ?? new Vector2(30f, 0f);
            if (!Owner.IsFacingRight) offset.X = -offset.X;
            Vector2 contactCenter = Owner.GlobalPosition + offset;
            uint targetHurtboxLayer = Owner.PlayerIndex == 0
                ? FTT.Core.CollisionLayers.EnemyHurtbox
                : FTT.Core.CollisionLayers.PlayerHurtbox;
            var query = new PhysicsShapeQueryParameters2D {
                Shape = new RectangleShape2D { Size = Data?.HitboxSize ?? new Vector2(60, 50) },
                Transform = new Transform2D(0f, contactCenter),
                CollideWithAreas = true,
                CollideWithBodies = false,
                CollisionMask = StoryShapeQuery.DeliveryMask(targetHurtboxLayer)
            };

            foreach (Godot.Collections.Dictionary result in space.IntersectShape(query, 16)) {
                if (result["collider"].AsGodotObject() is not Hurtbox hurtbox) continue;
                if (hurtbox.OwnerPlayerIndex == Owner.PlayerIndex) continue;
                if (!_struckHurtboxes.Add(hurtbox.GetInstanceId())) continue;

                HitPayload hit = Stamp(new HitPayload {
                    AttackerIndex = Owner.PlayerIndex,
                    AttackID = Data?.AbilityID ?? "lincoln_rail_charge",
                    HitboxID = "ram",
                    AttackClass = AttackClass.Special,
                    Damage = (Data?.BaseDamage ?? 6f) * Owner.StorySpecialDamageMultiplier,
                    Knockback = Data?.KnockbackForce ?? new Vector2(5f, 0f),
                    HitstunDuration = Data?.HitstunDuration ?? 0.2f,
                    HitOrigin = contactCenter,
                    AttackerFacingRight = Owner.IsFacingRight,
                    AppliedStatus = FTT.Core.StatusType.None,
                    StatusDuration = 0f,
                    StatusIntensity = 1f,
                    ScreenShakeIntensity = Data?.ScreenShakeIntensity ?? 0.2f,
                    ScreenShakeDuration = Data?.ScreenShakeDuration ?? 0.15f
                });
                float dealt = hurtbox.TakeHit(hit);
                // Contact stops the charge whether the ram landed or was blocked.
                _contactStopPending = true;
                if (dealt > 0f) {
                    Credit(in hit, dealt);
                    if (Owner.HasStoryPerk(HomesteadBulwarkPerkKey)) {
                        Owner.ApplyStoryHyperArmor(HomesteadBulwarkArmorSeconds);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Ultimate — Union Indestructible: Lincoln slams his rail into the ground,
    /// raising a line of split-rail fence barriers that pens enemies in front of
    /// him (Root held through the trap window), then delivers the authored
    /// HitCount rail smashes spread across the active window; the final smash
    /// shatters the fence with the authored heavy finisher knockback. Requires
    /// and consumes a full Ultimate Meter. Every number is data-driven from the
    /// authored AbilityData: per-hit BaseDamage, HitCount, DamageTickInterval,
    /// phase frames, pen size/offset, Root duration, HitstunDuration, and the
    /// finisher KnockbackForce. Smashes hit with the ultimate attack class, so
    /// they bypass shields and hyper-armor per the canonical block rules. The
    /// cinematic leap/camera presentation is Package 8.
    /// </summary>
    public partial class LincolnUnionIndestructible : BaseSpecial {

        private const int MaxQueryResults = 16;

        private UltimateMeter _meter;
        private int _smashesDone;
        private int _activeFramesElapsed;
        private Vector2 _penCenter;
        private bool _penFacingRight;

        public override void _Ready() {
            base._Ready();
            _meter = Owner?.GetNodeOrNull<UltimateMeter>("UltimateMeter");
        }

        protected override bool Validate() {
            return base.Validate() && (_meter?.IsFull ?? false);
        }

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
            _smashesDone = 0;
            _activeFramesElapsed = 0;
            Owner.Velocity = Vector2.Zero;
            _meter?.Consume();
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
            _penFacingRight = Owner.IsFacingRight;
            var offset = Data?.HitboxOffset ?? new Vector2(110f, 0f);
            if (!_penFacingRight) offset.X = -offset.X;
            _penCenter = Owner.GlobalPosition + offset;
            RootPennedTargets();
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
                // Smash n lands on active frame (n - 1) * interval + 1, mirroring
                // the Fighter zone whose first pulse fires on the cast frame.
                while (_smashesDone < hitCount
                    && _activeFramesElapsed >= interval * _smashesDone + 1) {
                    _smashesDone++;
                    ExecuteSmash(_smashesDone >= hitCount);
                }
            }
            base._PhysicsProcess(delta);
        }

        /// <summary>
        /// The fence raise: every target already inside the pen is rooted for the
        /// whole trap window. Applied directly to the status handlers (Quicksand
        /// Grip pattern) because zero-damage hits do not carry status through the
        /// player damage gate; each subsequent smash payload refreshes a shorter
        /// authored Root, so the pen holds even for targets entering late.
        /// </summary>
        private void RootPennedTargets() {
            float trapWindow = Data?.ActiveDuration ?? 2.5f;
            foreach (Hurtbox hurtbox in QueryPennedHurtboxes()) {
                Node current = hurtbox.GetParent();
                while (current != null) {
                    if (current is PlayerController player) {
                        player.GetNodeOrNull<StatusController>("StatusController")
                            ?.ApplyStatus(FTT.Core.StatusType.Root, trapWindow);
                        break;
                    }
                    if (current is FTT.Enemies.EnemyController enemy) {
                        enemy.ApplyStatusEffect(FTT.Core.StatusType.Root, trapWindow);
                        break;
                    }
                    current = current.GetParent();
                }
            }
        }

        /// <summary>
        /// One rail smash across the pen. Non-final smashes deal damage without
        /// knockback so the pen keeps holding; the final smash shatters the fence
        /// and carries the authored massive KnockbackForce. Each smash re-applies
        /// the authored Root, keeping trapped targets penned between hits.
        /// </summary>
        private void ExecuteSmash(bool finalSmash) {
            if (Owner == null) return;
            foreach (Hurtbox hurtbox in QueryPennedHurtboxes()) {
                HitPayload hit = Stamp(new HitPayload {
                    AttackerIndex = Owner.PlayerIndex,
                    AttackID = Data?.AbilityID ?? "lincoln_union_indestructible",
                    HitboxID = finalSmash ? "fence_shatter_smash" : $"rail_smash_{_smashesDone}",
                    AttackClass = AttackClass.Ultimate,
                    Damage = (Data?.BaseDamage ?? 8f) * Owner.StorySpecialDamageMultiplier,
                    Knockback = finalSmash ? Data?.KnockbackForce ?? new Vector2(12f, -8f) : Vector2.Zero,
                    HitstunDuration = Data?.HitstunDuration ?? 0.5f,
                    HitOrigin = _penCenter,
                    AttackerFacingRight = _penFacingRight,
                    AppliedStatus = Data?.AppliedStatus ?? FTT.Core.StatusType.Root,
                    StatusDuration = Data?.StatusDuration ?? 0.6f,
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

        private System.Collections.Generic.List<Hurtbox> QueryPennedHurtboxes() {
            var results = new System.Collections.Generic.List<Hurtbox>();
            var space = Owner?.GetWorld2D()?.DirectSpaceState;
            if (space == null) return results;

            uint targetHurtboxLayer = Owner.PlayerIndex == 0
                ? FTT.Core.CollisionLayers.EnemyHurtbox
                : FTT.Core.CollisionLayers.PlayerHurtbox;
            var query = new PhysicsShapeQueryParameters2D {
                Shape = new RectangleShape2D { Size = Data?.HitboxSize ?? new Vector2(220f, 100f) },
                Transform = new Transform2D(0f, _penCenter),
                CollideWithAreas = true,
                CollideWithBodies = false,
                CollisionMask = StoryShapeQuery.DeliveryMask(targetHurtboxLayer)
            };
            foreach (Godot.Collections.Dictionary result in space.IntersectShape(query, MaxQueryResults)) {
                if (result["collider"].AsGodotObject() is not Hurtbox hurtbox) continue;
                if (hurtbox.OwnerPlayerIndex == Owner.PlayerIndex) continue;
                results.Add(hurtbox);
            }
            return results;
        }
    }
}
