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
                Owner.StoryHyperArmorTimer = Mathf.Max(Owner.StoryHyperArmorTimer, activeSeconds);
            }
            Owner.SpecialOneCooldownTimer = Data?.CooldownDuration ?? 10f;
            FTT.Core.EventBus.Instance?.RaiseCooldownStarted(new FTT.Core.CooldownPayload {
                PlayerIndex = Owner.PlayerIndex,
                Slot = FTT.Core.AbilitySlot.Special1,
                Duration = Data?.CooldownDuration ?? 10f
            });
        }

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
            if (Owner.HasStoryPerk(UnstoppableCrusadePerkKey)) {
                Owner.StoryHyperArmorTimer = Mathf.Max(Owner.StoryHyperArmorTimer, PostCastHyperArmorSeconds);
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
    /// total BaseDamage * HitCount (12). Against a blocking opponent the flurry
    /// shreds exactly 2 block charges (design Section 5) instead of the
    /// special-class full shatter, so the block interaction is handled here
    /// rather than through the generic block path.
    /// </summary>
    public partial class JoanDivinePiercing : BaseSpecial {

        private const int BlockChargeDepletion = 2;
        private const int MaxQueryResults = 16;

        private int _thrustsDone;
        private int _activeFramesElapsed;
        private bool _blockChargesShredded;

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
            _thrustsDone = 0;
            _activeFramesElapsed = 0;
            _blockChargesShredded = false;
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
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
                CollisionMask = targetHurtboxLayer
            };

            bool finalThrust = _thrustsDone >= (Data?.HitCount ?? 1);
            foreach (Godot.Collections.Dictionary result in space.IntersectShape(query, MaxQueryResults)) {
                if (result["collider"].AsGodotObject() is not Hurtbox hurtbox) continue;
                if (hurtbox.OwnerPlayerIndex == Owner.PlayerIndex) continue;
                if (TryShredBlock(hurtbox)) continue;

                float dealt = hurtbox.TakeHit(new HitPayload {
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
                if (dealt > 0f) Owner.AddInfluenceFromDamageDealt(dealt);
            }
        }

        /// <summary>
        /// Blocked flurries shred exactly 2 charges once per cast. Handled before
        /// the generic hurtbox path because a special-class payload would shatter
        /// every remaining charge.
        /// </summary>
        private bool TryShredBlock(Hurtbox hurtbox) {
            if (hurtbox.GetParent() is not PlayerController target) return false;
            if (target.CurrentState != CharacterState.Blocking) return false;
            var blockSystem = target.GetNodeOrNull<BlockSystem>("BlockSystem");
            if (blockSystem == null || !blockSystem.IsBlocking || blockSystem.CurrentCharges <= 0) return false;
            if (!BlockRules.IsHitInFront(target.GlobalPosition, target.IsFacingRight, Owner.GlobalPosition)) {
                return false;
            }

            if (!_blockChargesShredded) {
                _blockChargesShredded = true;
                blockSystem.DepleteCharges(BlockChargeDepletion);
                target.CurrentBlockCharges = blockSystem.CurrentCharges;
                FTT.Core.CameraShake.Instance?.Shake(3f, 0.08f);
            }
            return true;
        }
    }

    /// <summary>
    /// Movement — Ascendant Wings: a rising vertical leap; holding Jump glides
    /// downward on ethereal wings for up to the authored duration (3 s). Usable
    /// in the air. Leap speed, glide duration, cooldown, and phase frames come
    /// from the authored MovementAbilityData.
    /// </summary>
    public partial class JoanAscendantWings : BaseSpecial {

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
            float cooldown = Data?.CooldownDuration ?? 5f;
            Owner.MovementAbilityCooldownTimer = cooldown;
            FTT.Core.EventBus.Instance?.RaiseMovementAbilityUsed(new FTT.Core.MovementAbilityPayload {
                PlayerIndex = Owner.PlayerIndex,
                AbilityName = Data?.AbilityName ?? "Ascendant Wings",
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
    /// Ultimate — The Grand Crusade. Structured sketch pending the dedicated
    /// ultimates pass (audit gap X7): directional spectral cavalry multi-hit.
    /// </summary>
    public partial class JoanGrandCrusade : BaseSpecial {
        private const float CinematicDuration = 2.5f;
        private const int HitCount = 6;
        private const float DamagePerHit = 12f;

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
            PhaseTimer = 0.5f;
            _hitsDone = 0;
            _hitTimer = 0;
            Owner.Velocity = Vector2.Zero;
            _meter?.Consume();
        }

        protected override void OnActive() {
            PhaseTimer = CinematicDuration;
        }

        protected override void OnRecovery() {
            PhaseTimer = 0.4f;
        }

        public override void _PhysicsProcess(double delta) {
            if (CurrentPhase == AbilityPhase.Active) {
                float dt = (float)delta;
                _hitTimer += dt;
                float hitInterval = CinematicDuration / HitCount;
                while (_hitTimer >= hitInterval && _hitsDone < HitCount) {
                    _hitTimer -= hitInterval;
                    _hitsDone++;
                    DealCavalryHit();
                }
            }
            base._PhysicsProcess(delta);
        }

        private void DealCavalryHit() {
            var chargeDir = Owner.IsFacingRight ? Vector2.Right : Vector2.Left;
            var hitbox = GetNodeOrNull<Hitbox>("CavalryHitbox");
            if (hitbox == null) return;

            hitbox.Damage = DamagePerHit;
            hitbox.KnockbackForce = chargeDir * 8f + new Vector2(0, -2f);
            hitbox.OwnerPlayerIndex = Owner.PlayerIndex;
            hitbox.GlobalPosition = Owner.GlobalPosition + chargeDir * (40f + _hitsDone * 60f);
            hitbox.Activate();
            GetTree().CreateTimer(0.08f).Timeout += () => hitbox.Deactivate();
        }
    }
}
