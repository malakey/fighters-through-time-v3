using Godot;
using FTT.Combat;
using FTT.Characters;

namespace FTT.Characters.Abilities {

    /// <summary>
    /// Special 1 — Spirit Strike (design §5, Package 12 W4). A spectral eagle
    /// carries Pocahontas <b>up-forward at 45°</b> — a forced dash of
    /// <see cref="KitMotionRules.SpiritStrikeCarryUnits"/> units over the
    /// authored active window (15 frames) — while the eagle's hitbox
    /// <b>swoops down-forward</b> ahead of her. The two vectors are separate:
    /// she rises while the eagle dives. Damage, knockback, hitstun and phase
    /// frames come from the authored AbilityData via the factory-built
    /// EagleHitbox; the carry and dive geometry are the shared
    /// <see cref="KitMotionRules"/> the Fighter sim reads too.
    ///
    /// <para>The retired interim shape moved her along (h, −0.6) at 420 px/s
    /// for 21 frames and dragged the hitbox on her own body.</para>
    /// </summary>
    public partial class PocahontasSpiritStrike : BaseSpecial {

        private float _facing = 1f;
        private int _carryFrame;

        /// <summary>Carry frames elapsed in the current cast. Test seam.</summary>
        public int CarryFrame => _carryFrame;

        /// <summary>Carry speed along the diagonal, px/s.</summary>
        public static float CarrySpeedPixelsPerSecond =>
            KitMotionRules.SpiritStrikeCarryUnits * KitMotionRules.StoryPixelsPerUnit
            * 60f / KitMotionRules.SpiritStrikeCarryFrames;

        /// <summary>Her velocity during the carry (Godot Y is down, so up is negative).</summary>
        public static Vector2 CarryVelocity(float facing) {
            float axis = CarrySpeedPixelsPerSecond / Mathf.Sqrt(2f);
            return new Vector2(facing * axis, -axis);
        }

        /// <summary>The eagle's offset from Pocahontas on a carry frame, in Story pixels.</summary>
        public static Vector2 EagleOffsetPixels(int carryFrame, float facing) {
            (double forward, double up) = KitMotionRules.SpiritEagleOffsetUnits(carryFrame);
            return new Vector2(
                facing * (float)forward * KitMotionRules.StoryPixelsPerUnit,
                -(float)up * KitMotionRules.StoryPixelsPerUnit);
        }

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
            _facing = Owner.IsFacingRight ? 1f : -1f;
            _carryFrame = 0;
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
            _carryFrame = 0;
        }

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
            // The carry is a forced dash: it ends with its momentum spent.
            Owner.Velocity = new Vector2(0f, Mathf.Max(0f, Owner.Velocity.Y));
        }

        public override void _PhysicsProcess(double delta) {
            var hitbox = GetNodeOrNull<Hitbox>("EagleHitbox");
            if (CurrentPhase == AbilityPhase.Active) {
                Owner.Velocity = CarryVelocity(_facing);
                // The factory-built hitbox carries the authored damage (scaled
                // by the Story special multiplier), knockback and hitstun; the
                // eagle dives along its own path ahead of her.
                if (hitbox != null) {
                    hitbox.GlobalPosition = Owner.GlobalPosition + EagleOffsetPixels(_carryFrame, _facing);
                    hitbox.Activate();
                }
                _carryFrame++;
            } else {
                hitbox?.Deactivate();
            }
            base._PhysicsProcess(delta);
        }

        /// <summary>H-4: interruption mid-swoop must not leave the eagle hitbox live.</summary>
        protected override void OnInterrupted() {
            GetNodeOrNull<Hitbox>("EagleHitbox")?.Deactivate();
        }
    }

    /// <summary>
    /// Special 2 — Vine Snare: throws a seed pod that grows into a persistent
    /// thorny-vine construct (15 HP, 10 s, max 2 active). Enemies stepping into
    /// the vines take light damage and are Rooted for 1.5 s. Story-only Resonance
    /// perk Thorn Snare makes rooted targets take continuous thorn damage while
    /// held. All tuning comes from the authored AbilityData resource.
    /// </summary>
    public partial class PocahontasVineSnare : BaseSpecial {

        public const string ThornSnarePerkKey = "thorn_snare";

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
            DeploySnare();
        }

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
        }

        private void DeploySnare() {
            if (Owner == null) return;
            if (Data?.PersistentObjectScene == null) {
                GD.PushWarning("Vine Snare has no PersistentObjectScene authored; deploy skipped.");
                return;
            }

            int maxActive = Data.MaxActiveObjects > 0 ? Data.MaxActiveObjects : 2;
            while (CountActiveSnares() >= maxActive) {
                VineSnareNode oldest = FindOldestSnare();
                if (oldest == null) break;
                Owner.ActivePersistentObjects.Remove(oldest);
                oldest.ReturnToPool();
            }

            Node spawned = FTT.Core.PoolManager.Instance?.Spawn(
                Data.PersistentObjectScene,
                Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? 60f : -60f, 0f),
                Owner.GetParent());
            if (spawned is not VineSnareNode snare) return;

            snare.Initialize(Data, Owner, Owner.HasStoryPerk(ThornSnarePerkKey));
            Owner.ActivePersistentObjects.Add(snare);
        }

        private int CountActiveSnares() {
            int count = 0;
            foreach (Node2D node in Owner.ActivePersistentObjects) {
                if (node is VineSnareNode snare && IsInstanceValid(snare) && !snare.IsSnareDestroyed) count++;
            }
            return count;
        }

        private VineSnareNode FindOldestSnare() {
            foreach (Node2D node in Owner.ActivePersistentObjects) {
                if (node is VineSnareNode snare && IsInstanceValid(snare) && !snare.IsSnareDestroyed) return snare;
            }
            return null;
        }
    }

    /// <summary>
    /// Movement — Breeze Glide: Pocahontas dashes forward on a wind current,
    /// resets her double jump, and can glide horizontally for up to the authored
    /// 3 seconds. Dash speed, glide duration, jump reset, and cooldown come from
    /// the authored MovementAbilityData resource. Story-only Resonance perks:
    /// Tornado Lift (glide start launches nearby enemies upward) and Leaf Barrier
    /// (glide start grants a shield worth 10% of max HP).
    /// </summary>
    public partial class PocahontasBreezeGlide : BaseSpecial {

        /// <summary>
        /// Story-only Resonance TRAVERSAL flag (V7.6, Tier 2): the Breeze Glide
        /// can be RE-ENTERED once per airtime. The latch resets on grounding
        /// and on a stock loss / respawn, so a single jump never buys more than
        /// one extra entry.
        /// </summary>
        public const string SecondGlidePerkKey = "second_glide";

        /// <summary>True once this airtime's extra re-entry has been spent. Test seam.</summary>
        public bool SecondGlideConsumed { get; private set; }

        /// <summary>
        /// Second Glide (traversal). Grants ONE extra glide entry per airtime.
        /// Returns true when the re-entry is authorized and consumes the
        /// airtime allowance atomically; false when the perk is absent, the
        /// allowance is already spent, or the owner is grounded (a grounded
        /// cast is the ordinary entry and spends nothing).
        /// </summary>
        /// <summary>
        /// Package 12 W4: the Second Glide re-entry is the movement cooldown
        /// bypass. <c>PlayerController</c> starts the re-entry with
        /// <c>TryExecute(armCooldown: false)</c>, so it neither checks nor
        /// restarts the running cooldown.
        /// </summary>
        public override bool TryConsumeCooldownBypass() => TryConsumeSecondGlide();

        public bool TryConsumeSecondGlide() {
            if (Owner == null || Owner.IsOnFloor()) return false;
            if (!Owner.HasStoryPerk(SecondGlidePerkKey) || SecondGlideConsumed) return false;
            SecondGlideConsumed = true;
            return true;
        }

        /// <summary>Airtime latch reset - grounding, respawn and stock loss all call this.</summary>
        public void ResetAirtimeAllowance() => SecondGlideConsumed = false;

        public const string TornadoLiftPerkKey = "tornado_lift";
        public const string LeafBarrierPerkKey = "leaf_barrier";

        private const float GlideHorizontalSpeed = 140f;
        private const float GlideMaxFallSpeed = 30f;
        private const float TornadoLiftRadiusPixels = 150f;
        private static readonly Vector2 TornadoLiftKnockback = new(0f, -6f);

        private bool _isGliding;
        private float _glideTimer;
        private float _dashSpeed = 350f;
        private float _maxGlideDuration = 3f;

        private MovementAbilityData MovementData => Data as MovementAbilityData;

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
            _isGliding = false;
            _dashSpeed = MovementData?.MovementSpeed > 0f ? MovementData.MovementSpeed : 350f;
            // Package 11 A4: V7.6 re-scopes the glide-window minor from the
            // character-wide GlideDuration lane onto
            // AbilityDuration(pocahontas_breeze_glide). Neutral 1.0 outside
            // Story Mode and on a grid that has not bought it.
            _maxGlideDuration = (MovementData?.MovementDuration > 0f ? MovementData.MovementDuration : 3f)
                * Owner.StoryScoped("AbilityDuration", "pocahontas_breeze_glide");

            float hDir = Owner.IsFacingRight ? 1f : -1f;
            Owner.Velocity = new Vector2(hDir * _dashSpeed, Owner.Velocity.Y);
            if (MovementData?.ResetsDoubleJump == true) {
                Owner.RemainingJumps = Owner.Data?.MaxJumpCount ?? 2;
            }
            ApplyTornadoLift();
            ApplyLeafBarrier();
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
        }

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
            _isGliding = true;
            _glideTimer = _maxGlideDuration;
            FTT.Core.EventBus.Instance?.RaiseMovementAbilityUsed(new FTT.Core.MovementAbilityPayload {
                PlayerIndex = Owner.PlayerIndex,
                AbilityName = Data?.AbilityName ?? "Breeze Glide",
                StartPosition = Owner.GlobalPosition,
                EndPosition = Owner.GlobalPosition
            });
        }

        /// <summary>H-4: a stun/death mid-cast releases the glide's velocity steering.</summary>
        protected override void OnInterrupted() {
            _isGliding = false;
        }

        /// <summary>True during the held glide (after the dash). Test seam and the attack gate.</summary>
        public bool IsGliding => _isGliding;

        /// <summary>
        /// Package 12 W4 (V7 kit rule): "the basic string is usable mid-glide
        /// without ending the glide". While gliding, <c>PlayerController</c>
        /// lets a basic attack start from <c>UsingMovementAbility</c>; the glide
        /// keeps steering through the swing and takes the owner back when the
        /// swing ends in the air. Any other state (stun, special, ledge, death)
        /// ends the glide.
        /// </summary>
        public bool AllowsBasicAttackMidGlide => _isGliding;

        private void KeepOwnerInGlide() {
            switch (Owner.CurrentState) {
                case CharacterState.UsingMovementAbility:
                case CharacterState.Attacking:
                    return;
                case CharacterState.Airborne:
                    // The mid-glide swing finished in the air: resume the glide.
                    Owner.TransitionTo(CharacterState.UsingMovementAbility);
                    return;
                default:
                    _isGliding = false;
                    if (CurrentPhase != AbilityPhase.Inactive) AdvanceToCleanup();
                    return;
            }
        }

        public override void _PhysicsProcess(double delta) {
            float dt = (float)delta;

            if (_isGliding) {
                KeepOwnerInGlide();
                if (!_isGliding) return;
                _glideTimer -= dt;
                float hInput = Owner.CurrentInputFrame.Horizontal;

                var vel = Owner.Velocity;
                vel.X = hInput * GlideHorizontalSpeed;
                vel.Y = Mathf.Min(vel.Y, GlideMaxFallSpeed);
                Owner.Velocity = vel;

                if (_glideTimer <= 0 || Owner.IsOnFloor()) {
                    _isGliding = false;
                    if (CurrentPhase != AbilityPhase.Inactive) AdvanceToCleanup();
                }
                return;
            }

            base._PhysicsProcess(delta);
        }

        /// <summary>
        /// Tornado Lift (Story-only): starting a Breeze Glide creates a vertical
        /// updraft that launches nearby enemies upward. The design specifies no
        /// damage, so the updraft is a pure knockback hit.
        /// </summary>
        private void ApplyTornadoLift() {
            if (Owner == null || !Owner.HasStoryPerk(TornadoLiftPerkKey)) return;
            var space = Owner.GetWorld2D()?.DirectSpaceState;
            if (space == null) return;

            uint targetHurtboxLayer = Owner.PlayerIndex == 0
                ? FTT.Core.CollisionLayers.EnemyHurtbox
                : FTT.Core.CollisionLayers.PlayerHurtbox;
            var query = new PhysicsShapeQueryParameters2D {
                Shape = new CircleShape2D { Radius = TornadoLiftRadiusPixels },
                Transform = new Transform2D(0f, Owner.GlobalPosition),
                CollideWithAreas = true,
                CollideWithBodies = false,
                CollisionMask = targetHurtboxLayer
            };

            foreach (Godot.Collections.Dictionary result in space.IntersectShape(query, 16)) {
                if (result["collider"].AsGodotObject() is not Hurtbox hurtbox) continue;
                if (hurtbox.OwnerPlayerIndex == Owner.PlayerIndex) continue;
                hurtbox.TakeHit(Stamp(new HitPayload {
                    AttackerIndex = Owner.PlayerIndex,
                    AttackID = Data?.AbilityID ?? "pocahontas_breeze_glide",
                    HitboxID = "tornado_lift",
                    AttackClass = AttackClass.Special,
                    Damage = 0f,
                    Knockback = TornadoLiftKnockback,
                    HitstunDuration = 0.1f,
                    HitOrigin = Owner.GlobalPosition,
                    AttackerFacingRight = Owner.IsFacingRight,
                    AppliedStatus = FTT.Core.StatusType.None,
                    StatusDuration = 0f,
                    StatusIntensity = 1f,
                    ScreenShakeIntensity = 0.1f,
                    ScreenShakeDuration = 0.05f
                }));
            }
        }

        /// <summary>
        /// Leaf Barrier (Story-only): entering a Breeze Glide grants a shield
        /// absorbing 10% of maximum health before HP is touched.
        ///
        /// <para><b>V7.6 D02a/D02c (Package 11 A1b).</b> A valid glide ENTRY
        /// grants or refreshes the shield to its full cap and restarts the
        /// eight-second lifetime; it never adds capacity or duration. An
        /// accepted <b>Second Glide re-entry</b> qualifies as a new entry -
        /// merely continuing the same glide or holding its input does not, which
        /// is why the grant identity is the entry counter rather than the call.
        /// Before this, any <c>ApplyLeafBarrier()</c> call re-granted.</para>
        /// </summary>
        private void ApplyLeafBarrier() {
            if (Owner == null || !Owner.HasStoryPerk(LeafBarrierPerkKey)) return;
            Owner.GrantStoryShield(
                FTT.Combat.StoryShieldEffect.LeafBarrier,
                FTT.Combat.StoryDefenseRules.GrantedShieldCapacityShare * Owner.MaximumHP,
                FTT.Combat.StoryDefenseRules.GrantedShieldLifetimeFrames,
                ++_glideEntryId);
        }

        /// <summary>
        /// D02a grant identity: one per ACCEPTED glide entry. A refused input
        /// never reaches OnStartup, and holding the glide never increments it.
        /// </summary>
        private int _glideEntryId;
    }

    /// <summary>
    /// Ultimate — Tidewater Tempest (canonical, X7 pass): Pocahontas plants her
    /// staff and channels a surging storm of water and wind spirits centered on
    /// her. The storm sweeps the authored HitCount ticks (8 x 10 damage) across
    /// the authored active window at the authored cadence via hurtbox shape
    /// queries; the final surge throws every caught enemy outward with the
    /// authored KnockbackForce. Phase frames, per-hit damage, hit count, tick
    /// interval, storm footprint, and surge knockback all come from
    /// `ultimate.tres`. The spectral wolf/eagle/deer dashes are presentation
    /// (Package 8).
    /// </summary>
    public partial class PocahontasTidewaterTempest : BaseSpecial {
        private const int MaxQueryResults = 16;

        private int _activeFramesElapsed;
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
            UseAuthoredPhaseFrames();
            _activeFramesElapsed = 0;
            _hitsDone = 0;
            // The staff is planted for the whole channel.
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
                Owner.Velocity = new Vector2(0f, Owner.Velocity.Y);
                _activeFramesElapsed++;
                int hitCount = Mathf.Max(1, Data?.HitCount ?? 1);
                int interval = Data?.DamageTickIntervalFrames > 0
                    ? Data.DamageTickIntervalFrames
                    : Mathf.Max(1, (Data?.ActiveFrames ?? hitCount) / hitCount);
                while (_hitsDone < hitCount && _activeFramesElapsed >= interval * (_hitsDone + 1)) {
                    _hitsDone++;
                    DealStormTick(_hitsDone >= hitCount);
                }
            }
            base._PhysicsProcess(delta);
        }

        /// <summary>
        /// One storm sweep: an owner-centered hurtbox query over the authored
        /// footprint. Intermediate ticks carry no knockback (the spirits churn
        /// through the target); the final surge throws each caught enemy outward
        /// from the storm center with the authored KnockbackForce.
        /// </summary>
        private void DealStormTick(bool finalSurge) {
            if (Owner == null) return;
            var space = Owner.GetWorld2D()?.DirectSpaceState;
            if (space == null) return;

            Vector2 size = Data?.HitboxSize ?? new Vector2(480f, 300f);
            Vector2 offset = Data?.HitboxOffset ?? Vector2.Zero;
            if (!Owner.IsFacingRight) offset.X = -offset.X;
            uint targetHurtboxLayer = Owner.PlayerIndex == 0
                ? FTT.Core.CollisionLayers.EnemyHurtbox
                : FTT.Core.CollisionLayers.PlayerHurtbox;
            var query = new PhysicsShapeQueryParameters2D {
                Shape = new RectangleShape2D { Size = size },
                Transform = new Transform2D(0f, Owner.GlobalPosition + offset),
                CollideWithAreas = true,
                CollideWithBodies = false,
                CollisionMask = targetHurtboxLayer
            };

            foreach (Godot.Collections.Dictionary result in space.IntersectShape(query, MaxQueryResults)) {
                if (result["collider"].AsGodotObject() is not Hurtbox hurtbox) continue;
                if (hurtbox.OwnerPlayerIndex == Owner.PlayerIndex) continue;

                // The surge throws outward from the storm center, mirroring the
                // Fighter zone's hit-origin knockback resolution.
                bool outwardIsRight = hurtbox.GlobalPosition.X >= Owner.GlobalPosition.X;
                HitPayload hit = Stamp(new HitPayload {
                    AttackerIndex = Owner.PlayerIndex,
                    AttackID = Data?.AbilityID ?? "pocahontas_tidewater_tempest",
                    HitboxID = $"storm_tick_{_hitsDone}",
                    AttackClass = AttackClass.Ultimate,
                    Damage = (Data?.BaseDamage ?? 10f) * Owner.StorySpecialDamageMultiplier,
                    Knockback = finalSurge ? Data?.KnockbackForce ?? new Vector2(5f, -3f) : Vector2.Zero,
                    HitstunDuration = Data?.HitstunDuration ?? 0.2f,
                    HitOrigin = Owner.GlobalPosition,
                    AttackerFacingRight = finalSurge ? outwardIsRight : Owner.IsFacingRight,
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
