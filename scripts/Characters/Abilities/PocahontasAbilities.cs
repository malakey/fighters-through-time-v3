using Godot;
using FTT.Combat;
using FTT.Characters;

namespace FTT.Characters.Abilities {

    /// <summary>
    /// Special 1 — Spirit Strike: a spectral eagle swoops down in a diagonal arc,
    /// dealing the authored 14 damage and staggering the target. Damage,
    /// knockback, hitstun, and phase frames come from the authored AbilityData
    /// resource via the factory-built EagleHitbox; only the swoop travel speed is
    /// presentation tuning.
    /// </summary>
    public partial class PocahontasSpiritStrike : BaseSpecial {
        private const float SwoopSpeed = 420f;

        private Vector2 _swoopDirection;

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
            float hDir = Owner.IsFacingRight ? 1f : -1f;
            _swoopDirection = new Vector2(hDir, -0.6f).Normalized();
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
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

        public override void _PhysicsProcess(double delta) {
            if (CurrentPhase == AbilityPhase.Active) {
                Owner.Velocity = _swoopDirection * SwoopSpeed;

                // The factory-built hitbox already carries the authored damage
                // (scaled by the Story special multiplier), knockback, and
                // hitstun; the swoop only drags it along the dive path.
                var hitbox = GetNodeOrNull<Hitbox>("EagleHitbox");
                if (hitbox != null) {
                    hitbox.GlobalPosition = Owner.GlobalPosition;
                    hitbox.Activate();
                }
            } else {
                GetNodeOrNull<Hitbox>("EagleHitbox")?.Deactivate();
            }
            base._PhysicsProcess(delta);
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
            _maxGlideDuration = MovementData?.MovementDuration > 0f ? MovementData.MovementDuration : 3f;

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
            float cooldown = Data?.CooldownDuration ?? 5f;
            Owner.MovementAbilityCooldownTimer = cooldown;
            FTT.Core.EventBus.Instance?.RaiseMovementAbilityUsed(new FTT.Core.MovementAbilityPayload {
                PlayerIndex = Owner.PlayerIndex,
                AbilityName = Data?.AbilityName ?? "Breeze Glide",
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

            if (_isGliding) {
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
                hurtbox.TakeHit(new HitPayload {
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
                });
            }
        }

        /// <summary>
        /// Leaf Barrier (Story-only): entering a Breeze Glide grants a shield
        /// that absorbs 10% of maximum health before HP is touched.
        /// </summary>
        private void ApplyLeafBarrier() {
            if (Owner == null || !Owner.HasStoryPerk(LeafBarrierPerkKey)) return;
            float capacity = 0.10f * Owner.MaximumHP;
            Owner.ConfigureStoryShield(capacity);
            Owner.RechargeStoryShield(capacity);
        }
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
                float dealt = hurtbox.TakeHit(new HitPayload {
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
                if (dealt > 0f) Owner.AddInfluenceFromDamageDealt(dealt);
            }
        }
    }
}
