using Godot;
using FTT.Combat;
using FTT.Characters;
using FTT.Core;

namespace FTT.Characters.Abilities {

    /// <summary>
    /// Special 1 — The Emancipator: Lincoln slams his rail into the ground and a
    /// shockwave travels forward along the floor, knocking enemies upward. Blocked
    /// hits deplete exactly 2 block charges instead of the generic full-shatter
    /// special rule. Timing, damage, speed, and travel come from the authored
    /// AbilityData (travel distance = ProjectileSpeed x ProjectileLifetime).
    /// Story-only Resonance perk Executive Order: +50% travel distance and +20%
    /// damage.
    /// </summary>
    public partial class LincolnEmancipator : BaseSpecial {

        public const string ExecutiveOrderPerkKey = "executive_order";

        private const int BlockChargeDepletion = 2;
        private const float ExecutiveOrderTravelMultiplier = 1.5f;
        private const float ExecutiveOrderDamageMultiplier = 1.2f;
        private const int WaveVisualIntervalFrames = 6;
        private const float WaveFloorOffsetY = 10f;

        private bool _waveActive;
        private Vector2 _waveFront;
        private bool _waveMovingRight;
        private float _waveSpeed;
        private float _waveDistanceRemaining;
        private float _waveDamage;
        private int _waveVisualCountdown;
        private readonly System.Collections.Generic.HashSet<ulong> _struckHurtboxes = new();

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
            StartWave();
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

        private void StartWave() {
            if (Owner == null) return;
            bool executiveOrder = Owner.HasStoryPerk(ExecutiveOrderPerkKey);
            _waveMovingRight = Owner.IsFacingRight;
            _waveSpeed = Data?.ProjectileSpeed > 0f ? Data.ProjectileSpeed : 200f;
            _waveDistanceRemaining = _waveSpeed * (Data?.ProjectileLifetime > 0f ? Data.ProjectileLifetime : 1.5f)
                * (executiveOrder ? ExecutiveOrderTravelMultiplier : 1f);
            _waveDamage = (Data?.BaseDamage ?? 20f)
                * (executiveOrder ? ExecutiveOrderDamageMultiplier : 1f)
                * Owner.StorySpecialDamageMultiplier;
            _waveFront = Owner.GlobalPosition
                + new Vector2(_waveMovingRight ? 50f : -50f, WaveFloorOffsetY);
            _waveVisualCountdown = 0;
            _struckHurtboxes.Clear();
            _waveActive = true;
        }

        public override void _PhysicsProcess(double delta) {
            base._PhysicsProcess(delta);
            if (!_waveActive) return;
            if (Owner == null || !IsInstanceValid(Owner)) {
                _waveActive = false;
                return;
            }

            float step = _waveSpeed * (float)delta;
            _waveFront.X += _waveMovingRight ? step : -step;
            _waveDistanceRemaining -= step;
            if (_waveDistanceRemaining <= 0f) _waveActive = false;

            if (--_waveVisualCountdown <= 0) {
                _waveVisualCountdown = WaveVisualIntervalFrames;
                SpawnPlaceholderZone(
                    _waveFront, 0f, 0.2f, 1f, new Color(0.2f, 0.2f, 0.5f),
                    (Data?.HitboxSize.Y ?? 50f) / 2f);
            }

            StrikeWaveFront();
        }

        private void StrikeWaveFront() {
            var space = Owner.GetWorld2D()?.DirectSpaceState;
            if (space == null) return;

            uint targetHurtboxLayer = Owner.PlayerIndex == 0
                ? FTT.Core.CollisionLayers.EnemyHurtbox
                : FTT.Core.CollisionLayers.PlayerHurtbox;
            var query = new PhysicsShapeQueryParameters2D {
                Shape = new RectangleShape2D { Size = Data?.HitboxSize ?? new Vector2(60, 50) },
                Transform = new Transform2D(0f, _waveFront),
                CollideWithAreas = true,
                CollideWithBodies = false,
                CollisionMask = targetHurtboxLayer
            };

            foreach (Godot.Collections.Dictionary result in space.IntersectShape(query, 16)) {
                if (result["collider"].AsGodotObject() is not Hurtbox hurtbox) continue;
                if (hurtbox.OwnerPlayerIndex == Owner.PlayerIndex) continue;
                if (!_struckHurtboxes.Add(hurtbox.GetInstanceId())) continue;

                // Design: the ground wave depletes exactly 2 block charges
                // instead of the generic special full-shatter rule.
                if (TryDepleteBlockCharges(hurtbox)) continue;

                float dealt = hurtbox.TakeHit(new HitPayload {
                    AttackerIndex = Owner.PlayerIndex,
                    AttackID = Data?.AbilityID ?? "lincoln_emancipator",
                    HitboxID = "ground_wave",
                    AttackClass = AttackClass.Special,
                    Damage = _waveDamage,
                    Knockback = Data?.KnockbackForce ?? new Vector2(2f, -6f),
                    HitstunDuration = Data?.HitstunDuration ?? 0.2f,
                    HitOrigin = _waveFront,
                    AttackerFacingRight = _waveMovingRight,
                    AppliedStatus = FTT.Core.StatusType.None,
                    StatusDuration = 0f,
                    StatusIntensity = 1f,
                    ScreenShakeIntensity = Data?.ScreenShakeIntensity ?? 0.2f,
                    ScreenShakeDuration = Data?.ScreenShakeDuration ?? 0.15f
                });
                if (dealt > 0f) Owner.AddInfluenceFromDamageDealt(dealt);
            }
        }

        private bool TryDepleteBlockCharges(Hurtbox hurtbox) {
            Node current = hurtbox.GetParent();
            while (current != null) {
                if (current is PlayerController target) {
                    if (target.CurrentState != CharacterState.Blocking) return false;
                    if (!BlockRules.IsHitInFront(target.GlobalPosition, target.IsFacingRight, _waveFront)) {
                        return false;
                    }
                    var blockSystem = target.GetNodeOrNull<BlockSystem>("BlockSystem");
                    if (blockSystem == null || !blockSystem.IsBlocking) return false;
                    blockSystem.DepleteCharges(BlockChargeDepletion);
                    FTT.Core.CameraShake.Instance?.Shake(3f, 0.08f);
                    return true;
                }
                current = current.GetParent();
            }
            return false;
        }
    }

    /// <summary>
    /// Special 2 — Splitting Strike: a massive overhead arc dealing the authored
    /// 18 damage through the shared hitbox contract. A blocking target loses every
    /// charge at once (the canonical special-versus-shield shatter rule); airborne
    /// targets caught in the arc are spiked straight down. Story-only Resonance
    /// perk Kinetic Splitting upgrades basic-combo hit 3 to the same shield
    /// shatter (wired in PlayerController.StartComboHit).
    /// </summary>
    public partial class LincolnSplittingStrike : BaseSpecial {

        public const string KineticSplittingPerkKey = "kinetic_splitting";

        private const float SpikeDownwardSpeed = 400f;

        private Hitbox _overheadHitbox;

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
            ActivateOverheadHitbox();
            Owner.SpecialTwoCooldownTimer = Data?.CooldownDuration ?? 10f;
            FTT.Core.EventBus.Instance?.RaiseCooldownStarted(new FTT.Core.CooldownPayload {
                PlayerIndex = Owner.PlayerIndex,
                Slot = FTT.Core.AbilitySlot.Special2,
                Duration = Data?.CooldownDuration ?? 10f
            });
        }

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
            _overheadHitbox?.Deactivate();
        }

        private void ActivateOverheadHitbox() {
            _overheadHitbox = GetOrCreateChildHitbox("OverheadHitbox");
            if (_overheadHitbox == null || Owner == null) return;

            _overheadHitbox.Damage = (Data?.BaseDamage ?? 18f) * Owner.StorySpecialDamageMultiplier;
            _overheadHitbox.KnockbackForce = Data?.KnockbackForce ?? new Vector2(4f, 5f);
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
        }

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
                        }
                        break;
                    }
                    current = current.GetParent();
                }
            }
        }
    }

    /// <summary>
    /// Movement — Rail Charge: a shoulder charge behind the wooden rail, usable in
    /// the air for horizontal recovery. Duration (capped at the design's 3 s),
    /// speed, and cooldown come from the authored MovementAbilityData. Lincoln has
    /// hyper-armor for the whole charge (damage yes, hitstun no via
    /// StoryCombatRules), and contact deals the authored damage without applying
    /// hitstun to the target. Story-only Resonance perk Homestead Bulwark: landing
    /// a charge hit grants 3 s of hyper-armor.
    /// </summary>
    public partial class LincolnRailCharge : BaseSpecial {

        public const string HomesteadBulwarkPerkKey = "homestead_bulwark";

        private const float MaxChargeDuration = 3.0f;
        private const float HomesteadBulwarkArmorSeconds = 3f;

        private Vector2 _chargeDirection;
        private Vector2 _startPosition;
        private float _chargeDuration = MaxChargeDuration;
        private float _chargeSpeed = 200f;
        private readonly System.Collections.Generic.HashSet<ulong> _struckHurtboxes = new();

        private MovementAbilityData MovementData => Data as MovementAbilityData;

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
            _chargeDuration = Mathf.Min(
                MovementData?.MovementDuration > 0f ? MovementData.MovementDuration : MaxChargeDuration,
                MaxChargeDuration);
            _chargeSpeed = MovementData?.MovementSpeed > 0f ? MovementData.MovementSpeed : 200f;
            _chargeDirection = Owner.IsFacingRight ? Vector2.Right : Vector2.Left;
            _startPosition = Owner.GlobalPosition;
            _struckHurtboxes.Clear();
        }

        protected override void OnActive() {
            PhaseTimer = _chargeDuration;
        }

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
            float cooldown = Data?.CooldownDuration ?? 5f;
            Owner.MovementAbilityCooldownTimer = cooldown;
            FTT.Core.EventBus.Instance?.RaiseMovementAbilityUsed(new FTT.Core.MovementAbilityPayload {
                PlayerIndex = Owner.PlayerIndex,
                AbilityName = Data?.AbilityName ?? "Rail Charge",
                StartPosition = _startPosition,
                EndPosition = Owner.GlobalPosition
            });
            FTT.Core.EventBus.Instance?.RaiseCooldownStarted(new FTT.Core.CooldownPayload {
                PlayerIndex = Owner.PlayerIndex,
                Slot = FTT.Core.AbilitySlot.MovementAbility,
                Duration = cooldown
            });
        }

        public override void _PhysicsProcess(double delta) {
            if (CurrentPhase == AbilityPhase.Active) {
                Owner.Velocity = _chargeDirection * _chargeSpeed;
                StrikeContacts();
            }
            base._PhysicsProcess(delta);
        }

        /// <summary>
        /// Contact damage along the charge path. Each target is struck once per
        /// charge and takes damage without hitstun (design: "damage yes, hitstun
        /// no" so the ram never stun-locks along its 3 s travel).
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
                CollisionMask = targetHurtboxLayer
            };

            foreach (Godot.Collections.Dictionary result in space.IntersectShape(query, 16)) {
                if (result["collider"].AsGodotObject() is not Hurtbox hurtbox) continue;
                if (hurtbox.OwnerPlayerIndex == Owner.PlayerIndex) continue;
                if (!_struckHurtboxes.Add(hurtbox.GetInstanceId())) continue;

                float dealt = hurtbox.TakeHit(new HitPayload {
                    AttackerIndex = Owner.PlayerIndex,
                    AttackID = Data?.AbilityID ?? "lincoln_rail_charge",
                    HitboxID = "ram",
                    AttackClass = AttackClass.Special,
                    Damage = (Data?.BaseDamage ?? 5f) * Owner.StorySpecialDamageMultiplier,
                    Knockback = Data?.KnockbackForce ?? new Vector2(3f, -1f),
                    HitstunDuration = 0f,
                    HitOrigin = contactCenter,
                    AttackerFacingRight = Owner.IsFacingRight,
                    AppliedStatus = FTT.Core.StatusType.None,
                    StatusDuration = 0f,
                    StatusIntensity = 1f,
                    ScreenShakeIntensity = Data?.ScreenShakeIntensity ?? 0.2f,
                    ScreenShakeDuration = Data?.ScreenShakeDuration ?? 0.15f
                });
                if (dealt > 0f) {
                    Owner.AddInfluenceFromDamageDealt(dealt);
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
                float dealt = hurtbox.TakeHit(new HitPayload {
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
                if (dealt > 0f) Owner.AddInfluenceFromDamageDealt(dealt);
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
                CollisionMask = targetHurtboxLayer
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
