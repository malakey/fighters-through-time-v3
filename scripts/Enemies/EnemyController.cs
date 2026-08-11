using Godot;
using System;
using FTT.Core;
using FTT.Environment;

namespace FTT.Enemies {

    public enum EnemyState { Idle, Patrol, Chase, Attacking, Stunned, Returning, Dead }

    /// <summary>
    /// Story-side standard/elite enemy runtime. Owns patrol/chase/attack/stun/death,
    /// telegraphed attacks through the shared <see cref="EnemyAbilityExecutor"/>,
    /// difficulty-scaled reaction delays, elite ability cycling, and pooling.
    /// Nothing here participates in the deterministic Fighter simulation.
    /// </summary>
    public partial class EnemyController : CharacterBody2D, IPoolable, IStoryRewindable, IStoryRewindSimulation {
        /// <summary>Seconds the death animation plays before the pool reclaims the body.</summary>
        public const float DeathAnimationSeconds = 0.5f;
        private const float GravityPixelsPerSecond = 980f;
        private const float PixelsPerUnit = 60f;

        /// <summary>
        /// Stand-off engagement threshold as a fraction of <see cref="AttackRangePixels"/>:
        /// chase stops advancing once the target is inside 85% of striking distance,
        /// so an enemy waits out its attack cooldown at range instead of pressing
        /// into the target's pushbox. Shared with <see cref="BossController"/>.
        /// </summary>
        public const float StandOffEngageFraction = 0.85f;
        /// <summary>
        /// Stand-off release threshold as a fraction of <see cref="AttackRangePixels"/>:
        /// the approach only resumes once the target drifts beyond 110% of striking
        /// distance, so a small shuffle inside the band cannot restart the walk
        /// (hysteresis). Shared with <see cref="BossController"/>.
        /// </summary>
        public const float StandOffResumeFraction = 1.10f;
        /// <summary>
        /// Frames to decelerate from full chase speed to rest at the stand-off line,
        /// mirroring the universal eight-frame ground run ramp.
        /// </summary>
        public const float StandOffDecelerationFrames = 8f;

        private static int _spawnCounter;

        [Export] public EnemyData Data;
        [Export] public StoryRewindPolicy RewindPolicy { get; set; } = StoryRewindPolicy.PreserveCurrentState;

        public EnemyState CurrentState { get; private set; } = EnemyState.Patrol;
        public int CurrentHP;

        private float _attackCooldownTimer;
        private float _eliteCooldownTimer;
        private float _stunTimer;
        private float _patrolIdleTimer;
        private float _deathTimer;
        private bool _patrolForward = true;
        private int _reactionFramesRemaining;
        private bool _attackCommitted;
        private bool _standOffEngaged;
        private int _eliteAbilityIndex = -1;
        private bool _lastAttackWasElite;

        private Vector2 _spawnPosition;
        private Vector2 _patrolPointA;
        private Vector2 _patrolPointB;
        private bool _patrolPointsSet;
        private Vector2 _returnTarget;
        private AnimatedSprite2D _sprite;
        private FTT.Combat.GlowPresentationController _glow;
        private FTT.Combat.PresentationVisibilitySuspender _presentationSuspender;
        private FTT.Characters.PlayerController _target;
        private FTT.Combat.Hitbox _attackHitbox;
        private FTT.Combat.Hurtbox _hurtbox;
        private FTT.Combat.CombatantPushbox _pushbox;
        private Node2D _abilityOrigin;
        private ProgressBar _hpBar;
        private Label _nameLabel;
        /// <summary>
        /// Package 8 B1. Overhead bars are hidden until this body is damaged and
        /// fade out after the exchange goes quiet; bosses are unaffected (they use
        /// the HUD bar and have no overhead widgets). The state is reset on both
        /// halves of a pool cycle so a recycled body never inherits a visible bar.
        /// </summary>
        private readonly FTT.UI.OverheadBarVisibility _barVisibility = new();
        private bool _facingRight = true;
        private bool _rewindFrozen;
        private int _scaledMaxHP;
        private float _statusTimer;
        private float _statusIntensity = 1f;
        private float _venomTickTimer;
        private bool _nodesResolved;
        private bool _eventsBound;

        private EnemyAbilityExecutor _executor;
        private EnemyAbilityData _legacyPrimaryAttack;
        private Random _rng = new(unchecked(20260807 + System.Threading.Interlocked.Increment(ref _spawnCounter)));

        private Vector2 _checkpointPosition;
        private int _checkpointHP;
        private bool _checkpointCaptured;

        public bool IsStoryRewindFrozen => _rewindFrozen;

        /// <summary>Active status effect (newest replaces; no stacking), Story-only.</summary>
        public StatusType ActiveStatusType { get; private set; } = StatusType.None;
        public float StatusMoveMultiplier { get; private set; } = 1f;
        public float StatusDamageTakenMultiplier { get; private set; } = 1f;

        /// <summary>Story difficulty-scaled maximum HP; canonical base stays in EnemyData.</summary>
        public int ScaledMaxHP => _scaledMaxHP > 0 ? _scaledMaxHP : Data?.MaxHP ?? 1;

        /// <summary>Frames left before the committed attack starts its telegraph.</summary>
        public int ReactionFramesRemaining => _reactionFramesRemaining;

        /// <summary>Overhead HP bar / name label visibility state (Package 8 B1).</summary>
        public FTT.UI.OverheadBarVisibility BarVisibility => _barVisibility;

        public EnemyAbilityPhase AbilityPhase => Executor.Phase;
        public EnemyAbilityData ActiveAbility => Executor.ActiveAbility;
        public bool LastAttackWasElite => _lastAttackWasElite;
        public bool IsFacingRight => _facingRight;

        /// <summary>
        /// True while chase holds at the stand-off band: the target came inside
        /// <see cref="StandOffEngageFraction"/> of striking distance and has not
        /// yet drifted beyond <see cref="StandOffResumeFraction"/> of it.
        /// </summary>
        public bool IsStandOffEngaged => _standOffEngaged;

        private EnemyAbilityExecutor Executor => _executor ??= new EnemyAbilityExecutor(this) { Rng = _rng };

        public override void _Ready() {
            ResolveNodes();
            ApplyData(Data);
            _spawnPosition = GlobalPosition;
            _returnTarget = _spawnPosition;
            CaptureWaypointsFromScene();
            CollisionLayer = CollisionLayers.Enemy;
            CollisionMask = CollisionLayers.EnemyBodyMask;
            BindEvents();
            FTT.Combat.VfxPresentationBinder.EnsureInstalled(this);
        }

        public override void _ExitTree() {
            UnbindEvents();
        }

        private void ResolveNodes() {
            if (_nodesResolved) return;
            _nodesResolved = true;
            _sprite = GetNodeOrNull<AnimatedSprite2D>("Presentation/AnimatedSprite2D")
                ?? GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
            _attackHitbox = GetNodeOrNull<FTT.Combat.Hitbox>("Hitbox")
                ?? GetNodeOrNull<FTT.Combat.Hitbox>("AttackHitbox");
            _hurtbox = GetNodeOrNull<FTT.Combat.Hurtbox>("Hurtbox");
            if (_hurtbox != null) _hurtbox.OwnerPlayerIndex = -1;
            _pushbox = GetNodeOrNull<FTT.Combat.CombatantPushbox>("Pushbox");
            _abilityOrigin = GetNodeOrNull<Node2D>("AbilityOrigin");
            _hpBar = GetNodeOrNull<ProgressBar>("HPBar") ?? GetNodeOrNull<ProgressBar>("Presentation/HPBar");
            _nameLabel = GetNodeOrNull<Label>("NameLabel") ?? GetNodeOrNull<Label>("Presentation/NameLabel");
            Executor.Bind(_sprite, _attackHitbox, _abilityOrigin);
            if (_sprite != null) {
                // Roster bodies have no player slot and are driven directly rather
                // than through the EventBus (enemy status is not StatusController).
                _glow = FTT.Combat.GlowPresentationController.AttachTo(
                    this, _sprite, ownerPlayerIndex: -1, subscribeToStoryEvents: false);
                Executor.Glow = _glow;
                _presentationSuspender = FTT.Combat.PresentationVisibilitySuspender.AttachTo(this, _sprite);
            }
        }

        private void CaptureWaypointsFromScene() {
            Marker2D left = GetNodeOrNull<Marker2D>("Waypoints/Left") ?? GetNodeOrNull<Marker2D>("WaypointA");
            Marker2D right = GetNodeOrNull<Marker2D>("Waypoints/Right") ?? GetNodeOrNull<Marker2D>("WaypointB");
            if (left == null || right == null) return;
            _patrolPointA = GlobalPosition + left.Position;
            _patrolPointB = GlobalPosition + right.Position;
            _patrolPointsSet = true;
        }

        private void BindEvents() {
            if (_eventsBound) return;
            _eventsBound = true;
            if (_hurtbox != null) _hurtbox.OnHit += OnHurtboxHit;
            if (EventBus.Instance == null) return;
            EventBus.Instance.OnCheckpointReached += CaptureCheckpointState;
            EventBus.Instance.OnRewindTriggered += OnRewindTriggered;
        }

        private void UnbindEvents() {
            if (!_eventsBound) return;
            _eventsBound = false;
            if (_hurtbox != null) _hurtbox.OnHit -= OnHurtboxHit;
            if (EventBus.Instance == null) return;
            EventBus.Instance.OnCheckpointReached -= CaptureCheckpointState;
            EventBus.Instance.OnRewindTriggered -= OnRewindTriggered;
        }

        /// <summary>
        /// Assigns canonical data and rebuilds every derived runtime value. Pooled
        /// spawns call this so one shared scene serves the whole roster tier.
        /// </summary>
        public void ApplyData(EnemyData data) {
            Data = data;
            ResolveNodes();
            _legacyPrimaryAttack = null;
            ApplyDifficultyScaling();
            CurrentHP = ScaledMaxHP;
            Executor.SourceID = Data?.EnemyID ?? "";
            Executor.DamageMultiplier = StoryDifficultyTuning.GetEnemyDamageMultiplier(
                StoryDifficultyTuning.CurrentStoryDifficulty);
            ApplyPresentation();
            ApplyScaledHitboxDamage();
            UpdateHPBar();
        }

        private void ApplyPresentation() {
            if (_sprite != null) {
                if (Data?.SpriteFramesResource != null) _sprite.SpriteFrames = Data.SpriteFramesResource;
                Color tint = Data?.PlaceholderTint ?? Colors.White;
                if (tint.A <= 0f) tint = Colors.White;
                // SetBaseModulate routes through the arbiter when one is attached.
                Executor.SetBaseModulate(tint);
                if (_glow == null) _sprite.Modulate = tint;
                _glow?.ClearAllStates();
                PlayAnimation("idle");
            }
            if (_nameLabel != null) {
                string key = Data?.DisplayNameKey;
                _nameLabel.Text = string.IsNullOrWhiteSpace(key) ? Data?.DisplayName ?? "" : Tr(key);
            }
        }

        private void ApplyDifficultyScaling() {
            if (Data == null) return;
            Difficulty difficulty = StoryDifficultyTuning.CurrentStoryDifficulty;
            _scaledMaxHP = StoryDifficultyTuning.ScaleEnemyHP(Data.MaxHP, difficulty);
        }

        private void ApplyScaledHitboxDamage() {
            if (Data == null || _attackHitbox == null) return;
            Difficulty difficulty = StoryDifficultyTuning.CurrentStoryDifficulty;
            _attackHitbox.Damage = StoryDifficultyTuning.ScaleEnemyDamage(Data.AttackDamage, difficulty);
            _attackHitbox.OwnerPlayerIndex = -1;
        }

        public override void _PhysicsProcess(double delta) {
            if (_rewindFrozen) return;
            float dt = (float)delta;

            // Bars keep fading while the body plays out its death animation, so a
            // corpse does not hold a full-opacity bar for the pool's release delay.
            _barVisibility.Tick(dt);
            ApplyBarVisibility();

            if (CurrentState == EnemyState.Dead) {
                ProcessDead(dt);
                return;
            }

            TickStatus(dt);
            if (CurrentState == EnemyState.Dead) return;
            if (_attackCooldownTimer > 0) _attackCooldownTimer -= dt;
            if (_eliteCooldownTimer > 0) _eliteCooldownTimer -= dt;
            Executor.Tick(dt);

            switch (CurrentState) {
                case EnemyState.Patrol: ProcessPatrol(dt); break;
                case EnemyState.Chase: ProcessChase(dt); break;
                case EnemyState.Attacking: ProcessAttacking(dt); break;
                case EnemyState.Stunned: ProcessStunned(dt); break;
                case EnemyState.Returning: ProcessReturning(dt); break;
                default: break;
            }

            ApplyPhasingMask();
            ApplyGravity(dt);
            MoveAndSlide();
            _pushbox?.ResolveStoryOverlaps();
        }

        private bool IsFlying => Data?.Behavior == DefaultBehavior.Flying;

        /// <summary>StandGuard holds its post: no patrol pacing, normal aggro/chase/return.</summary>
        private bool IsStandGuard => Data?.Behavior == DefaultBehavior.StandGuard;

        /// <summary>
        /// ChargeDash travel obeys the same status multiplier as ordinary movement:
        /// Root pins the dasher in place and TimeDilation slows it, instead of the
        /// raw executor velocity crossing the room at full speed (audit Low).
        /// </summary>
        public float StatusScaledDashVelocityX => Executor.DashVelocity.X * StatusMoveMultiplier;

        private void ApplyGravity(float dt) {
            if (IsFlying) return;
            if (!IsOnFloor()) {
                Velocity = new Vector2(Velocity.X, Velocity.Y + GravityPixelsPerSecond * dt);
            }
        }

        /// <summary>Rift Phantom rule: ignore Environment collision while chasing.</summary>
        private void ApplyPhasingMask() {
            if (Data?.PhasesThroughWalls != true) return;
            CollisionMask = ResolveBodyMask(true, CurrentState);
        }

        /// <summary>
        /// Body mask a phasing/non-phasing enemy uses in a given state. Phasers drop
        /// the Environment bit while actively pursuing and restore it otherwise.
        /// </summary>
        public static uint ResolveBodyMask(bool phasesThroughWalls, EnemyState state) {
            bool phasing = phasesThroughWalls && state is EnemyState.Chase or EnemyState.Attacking;
            return phasing
                ? CollisionLayers.EnemyBodyMask & ~CollisionLayers.Environment
                : CollisionLayers.EnemyBodyMask;
        }

        private float MoveSpeedPixels => (Data?.MoveSpeed ?? 3f) * PixelsPerUnit * StatusMoveMultiplier;

        private void ProcessPatrol(float dt) {
            var player = FindNearestPlayer();
            if (player != null && GlobalPosition.DistanceTo(player.GlobalPosition) <= (Data?.AggroRadius ?? 400f)) {
                _target = player;
                CurrentState = EnemyState.Chase;
                return;
            }

            // StandGuard posts hold position (design-godot.md:1144): no waypoint
            // pacing, but the aggro check above still runs every frame.
            if (IsStandGuard || !_patrolPointsSet) {
                Velocity = new Vector2(0, IsFlying ? 0f : Velocity.Y);
                PlayAnimation("idle");
                return;
            }

            if (_patrolIdleTimer > 0) {
                _patrolIdleTimer -= dt;
                Velocity = new Vector2(0, IsFlying ? 0f : Velocity.Y);
                PlayAnimation("idle");
                return;
            }

            Vector2 targetPos = _patrolForward ? _patrolPointB : _patrolPointA;
            float dirX = targetPos.X - GlobalPosition.X;

            if (Mathf.Abs(dirX) < 10f) {
                _patrolForward = !_patrolForward;
                // design Section 6: one full second of idle at each waypoint.
                _patrolIdleTimer = 1.0f;
                Velocity = new Vector2(0, IsFlying ? 0f : Velocity.Y);
                return;
            }

            float sign = Mathf.Sign(dirX);
            SetFacing(sign > 0);
            Velocity = new Vector2(sign * MoveSpeedPixels, IsFlying ? 0f : Velocity.Y);
            PlayAnimation("patrol");
        }

        private void ProcessChase(float dt) {
            if (_target == null || _target.CurrentState == FTT.Characters.CharacterState.Dead) {
                StartReturning();
                return;
            }

            float dist = GlobalPosition.DistanceTo(_target.GlobalPosition);
            if (dist > (Data?.DeAggroRadius ?? 600f)) {
                StartReturning();
                return;
            }

            if (dist <= AttackRangePixels && _attackCooldownTimer <= 0) {
                EnterAttacking();
                return;
            }

            Vector2 toTarget = _target.GlobalPosition - GlobalPosition;
            float sign = Mathf.Sign(toTarget.X);
            if (sign != 0) SetFacing(sign > 0);

            // Stand-off band: once inside striking distance of the enemy's own
            // primary attack, stop approaching instead of pressing into the
            // target's pushbox while the attack cooldown runs. Engage and release
            // use different thresholds (hysteresis) so a target drifting at the
            // edge of the band cannot make the enemy oscillate between walking
            // and stopping. The CombatantPushbox stays as the overlap safety net;
            // chase simply no longer feeds it.
            if (dist <= AttackRangePixels * StandOffEngageFraction) _standOffEngaged = true;
            else if (dist > AttackRangePixels * StandOffResumeFraction) _standOffEngaged = false;

            if (_standOffEngaged) {
                float decel = StandOffDecelerationPerSecond * dt;
                Velocity = new Vector2(
                    Mathf.MoveToward(Velocity.X, 0f, decel),
                    IsFlying ? Mathf.MoveToward(Velocity.Y, 0f, decel) : Velocity.Y);
                PlayAnimation("idle");
                return;
            }

            float verticalVelocity = IsFlying
                ? Mathf.Clamp(toTarget.Y, -MoveSpeedPixels, MoveSpeedPixels)
                : Velocity.Y;
            Velocity = new Vector2(sign * MoveSpeedPixels, verticalVelocity);
            PlayAnimation("patrol");
        }

        /// <summary>
        /// Pixel striking distance of the enemy's own primary attack — the single
        /// source both the attack trigger and the stand-off band derive from.
        /// Projectile primaries extend it to most of the aggro radius.
        /// </summary>
        public float AttackRangePixels {
            get {
                float range = (Data?.AttackRange ?? 1.5f) * PixelsPerUnit;
                EnemyAbilityData primary = Data?.PrimaryAttack;
                if (primary != null && primary.SpawnsProjectiles) {
                    range = Mathf.Max(range, (Data?.AggroRadius ?? 400f) * 0.9f);
                }
                return range;
            }
        }

        /// <summary>
        /// Deceleration that brings the full base chase speed to rest across
        /// <see cref="StandOffDecelerationFrames"/> frames. Derived from the
        /// unscaled authored speed so a status slow can never stall the stop-out.
        /// </summary>
        private float StandOffDecelerationPerSecond =>
            (Data?.MoveSpeed ?? 3f) * PixelsPerUnit * 60f / StandOffDecelerationFrames;

        private void EnterAttacking() {
            CurrentState = EnemyState.Attacking;
            Velocity = new Vector2(0, IsFlying ? 0f : Velocity.Y);
            _attackCommitted = false;
            _reactionFramesRemaining = RollReactionDelayFrames();
        }

        /// <summary>Seeded per-instance roll, then scaled by the Story difficulty.</summary>
        private int RollReactionDelayFrames() {
            int min = Math.Max(0, Data?.ReactionDelayMinFrames ?? 30);
            int max = Math.Max(min, Data?.ReactionDelayMaxFrames ?? min);
            int rolled = min == max ? min : _rng.Next(min, max + 1);
            return StoryDifficultyTuning.ScaleReactionDelayFrames(
                rolled, StoryDifficultyTuning.CurrentStoryDifficulty);
        }

        private void ProcessAttacking(float dt) {
            Velocity = new Vector2(0, IsFlying ? 0f : Velocity.Y);

            if (_reactionFramesRemaining > 0) {
                _reactionFramesRemaining--;
                // Aim only during the pre-commit reaction delay. The facing locks
                // the moment the attack commits, because the executor resolves the
                // hitbox and dash direction on the facing captured at Begin — a
                // sprite that kept tracking would telegraph a hit that then lands
                // behind the enemy (audit M-19; BossController has the same rule).
                if (_target != null) SetFacing(_target.GlobalPosition.X >= GlobalPosition.X);
                return;
            }

            if (!_attackCommitted) {
                _attackCommitted = true;
                BeginAttack();
                return;
            }

            if (Executor.Phase == EnemyAbilityPhase.Active &&
                Executor.ActiveAbility?.Archetype == EnemyAbilityArchetype.ChargeDash) {
                Velocity = new Vector2(StatusScaledDashVelocityX, Velocity.Y);
            }

            if (!Executor.IsBusy) {
                _attackCommitted = false;
                _attackCooldownTimer = Data?.AttackCooldown ?? 2f;
                CurrentState = EnemyState.Chase;
            }
        }

        /// <summary>
        /// Commits one attack through the shared executor. Passing null selects the
        /// next attack from the data (standard primary or the next elite ability).
        /// </summary>
        public void BeginAttack(EnemyAbilityData ability = null) {
            ability ??= SelectNextAttack();
            Vector2 targetPosition = _target?.GlobalPosition ?? GlobalPosition + new Vector2(_facingRight ? 100f : -100f, 0f);
            Executor.Begin(ability, targetPosition, _facingRight);
            PlayAnimation(_lastAttackWasElite ? "elite_attack" : "attack");
        }

        /// <summary>
        /// Elites alternate standard attack and the next entry of EliteAbilities
        /// (sequential cycle per design Section 6) whenever the elite cooldown is up.
        /// </summary>
        public EnemyAbilityData SelectNextAttack() {
            if (Data != null && Data.HasEliteAbilities && _eliteCooldownTimer <= 0f && !_lastAttackWasElite) {
                _eliteAbilityIndex = (_eliteAbilityIndex + 1) % Data.EliteAbilities.Length;
                EnemyAbilityData elite = Data.EliteAbilities[_eliteAbilityIndex];
                if (elite != null) {
                    _lastAttackWasElite = true;
                    _eliteCooldownTimer = Mathf.Max(0f, Data.EliteAbilityCooldown);
                    return elite;
                }
            }
            _lastAttackWasElite = false;
            return Data?.PrimaryAttack ?? GetLegacyPrimaryAttack();
        }

        /// <summary>
        /// Synthesized MeleeStrike for resources authored before PrimaryAttack
        /// existed, so the scalar AttackDamage/Knockback/frame fields stay live.
        /// </summary>
        private EnemyAbilityData GetLegacyPrimaryAttack() {
            if (_legacyPrimaryAttack != null) return _legacyPrimaryAttack;
            _legacyPrimaryAttack = new EnemyAbilityData {
                AbilityID = $"{Data?.EnemyID ?? "enemy"}.basic",
                Archetype = EnemyAbilityArchetype.MeleeStrike,
                RangeClass = EnemyAbilityRangeClass.Melee,
                TelegraphFrames = Math.Max(0, Data?.AttackTelegraphFrames ?? 14),
                ActiveFrames = Math.Max(1, Data?.AttackActiveFrames ?? 12),
                RecoveryFrames = Math.Max(0, Data?.AttackRecoveryFrames ?? 16),
                Damage = Data?.AttackDamage ?? 5f,
                KnockbackForce = new Vector2(Data?.AttackKnockback ?? 2f, -1.5f),
                HitstunDuration = 0.15f,
                HitboxSize = new Vector2(40f, 40f),
                HitboxOffset = new Vector2(30f, -25f),
                TelegraphTint = new Color(1f, 0.6f, 0.3f),
                PresentationEventID = $"{Data?.EnemyID ?? "enemy"}.basic"
            };
            return _legacyPrimaryAttack;
        }

        private void ProcessStunned(float dt) {
            Velocity = new Vector2(0, IsFlying ? 0f : Velocity.Y);
            _stunTimer -= dt;
            if (_stunTimer <= 0) {
                CurrentState = _target != null ? EnemyState.Chase : EnemyState.Patrol;
            }
        }

        private void ProcessReturning(float dt) {
            var player = FindNearestPlayer();
            if (player != null && GlobalPosition.DistanceTo(player.GlobalPosition) <= (Data?.AggroRadius ?? 400f)) {
                _target = player;
                CurrentState = EnemyState.Chase;
                return;
            }

            float dirX = _returnTarget.X - GlobalPosition.X;
            if (Mathf.Abs(dirX) < 15f) {
                CurrentState = EnemyState.Patrol;
                _target = null;
                Velocity = new Vector2(0, IsFlying ? 0f : Velocity.Y);
                return;
            }

            float sign = Mathf.Sign(dirX);
            SetFacing(sign > 0);
            Velocity = new Vector2(sign * MoveSpeedPixels, IsFlying ? 0f : Velocity.Y);
            PlayAnimation("patrol");
        }

        private void ProcessDead(float dt) {
            Velocity = Vector2.Zero;
            if (_deathTimer <= 0f) return;
            _deathTimer -= dt;
            if (_deathTimer > 0f) return;
            _deathTimer = 0f;
            if (PoolManager.Instance != null) PoolManager.Instance.Release(this);
            else QueueFree();
        }

        private void StartReturning() {
            _target = null;
            _standOffEngaged = false;
            CurrentState = EnemyState.Returning;
            _returnTarget = ResolveReturnTarget();
        }

        /// <summary>
        /// design-godot.md:2298: a de-aggroed mob "returns to its nearest waypoint
        /// and resumes patrol" — not to the spawn midpoint. Heading to a waypoint
        /// also seeds the patrol direction so the resumed pace continues toward the
        /// other waypoint instead of instantly reversing. StandGuard posts (and
        /// mobs without waypoints) return to the post itself.
        /// </summary>
        private Vector2 ResolveReturnTarget() {
            if (IsStandGuard || !_patrolPointsSet) return _spawnPosition;
            float toA = Mathf.Abs(_patrolPointA.X - GlobalPosition.X);
            float toB = Mathf.Abs(_patrolPointB.X - GlobalPosition.X);
            if (toA <= toB) {
                _patrolForward = true; // arriving at A, next leg heads to B
                return _patrolPointA;
            }
            _patrolForward = false; // arriving at B, next leg heads to A
            return _patrolPointB;
        }

        public int TakeDamage(int damage) => TakeDamage(damage, null);

        private int TakeDamage(int damage, Vector2? hitOrigin) {
            if (CurrentState == EnemyState.Dead) return 0;
            float incoming = Math.Max(0, damage) * StatusDamageTakenMultiplier;
            incoming *= 1f - Mathf.Clamp(ResolveFrontalReduction(hitOrigin), 0f, 0.95f);
            if (Executor.HasActiveShield) incoming *= 1f - Mathf.Clamp(Executor.ShieldDamageReduction, 0f, 1f);

            int applied = Math.Max(0, (int)MathF.Round(incoming));
            int previousHP = CurrentHP;
            CurrentHP = Math.Max(0, CurrentHP - applied);
            int damageApplied = previousHP - CurrentHP;
            // Any damage that lands reveals the bar and re-arms the quiet window,
            // including a Venom tick — the player caused that too.
            if (damageApplied > 0) _barVisibility.NotifyDamaged();
            UpdateHPBar();

            if (CurrentHP <= 0) Die();
            else if (damageApplied > 0) {
                PlayAnimation("hitstun");
                _glow?.FlashHit();
            }
            return damageApplied;
        }

        /// <summary>Shield-carrier reduction applies only to hits arriving from the facing side.</summary>
        private float ResolveFrontalReduction(Vector2? hitOrigin) {
            float reduction = Data?.FrontalDamageReduction ?? 0f;
            if (reduction <= 0f || !hitOrigin.HasValue) return 0f;
            bool fromRight = hitOrigin.Value.X >= GlobalPosition.X;
            return fromRight == _facingRight ? reduction : 0f;
        }

        private void Die() {
            CurrentState = EnemyState.Dead;
            Executor.Cancel();
            _attackHitbox?.Deactivate();
            _attackCommitted = false;
            _reactionFramesRemaining = 0;
            Velocity = Vector2.Zero;
            CollisionLayer = 0;
            CollisionMask = 0;
            if (_hurtbox != null) {
                _hurtbox.Monitoring = false;
                _hurtbox.Monitorable = false;
            }
            _pushbox?.SetPushEnabled(false);
            PlayAnimation("death");
            _glow?.ClearAllStates();
            _deathTimer = DeathAnimationSeconds;

            EventBus.Instance?.RaiseEnemyKilled(new EnemyKilledPayload {
                EnemyID = Data?.EnemyID ?? "",
                Position = GlobalPosition,
                ChronalDustDrop = Data?.ChronalDustDrop ?? 10,
                IsElite = Data?.Tier == EnemyTier.Elite,
                ItemDropChanceMultiplier = Data?.ItemDropChance ?? 1f
            });
            EventBus.Instance?.RaiseEnemyPresentation(new EnemyPresentationPayload {
                SourceID = Data?.EnemyID ?? "",
                AbilityID = "",
                PresentationEventID = $"{Data?.EnemyID ?? "enemy"}.death",
                Phase = EnemyPresentationPhase.Death,
                Position = GlobalPosition
            });
        }

        public void ApplyKnockback(Vector2 knockback, bool attackerFacingRight) {
            if (CurrentState == EnemyState.Dead) return;
            float weight = Data?.Weight ?? 1.0f;
            // Knockback replaces velocity, matching the player and the Fighter
            // sim — a hit imparts the same impulse regardless of prior motion.
            Velocity = FTT.Combat.DamageCalculator.CalculateKnockback(knockback, weight, attackerFacingRight) * 60f;
        }

        public void ApplyStun(float duration) => ApplyStun(duration, 0f);

        /// <summary>
        /// Applies hitstun scaled down by the authored <c>StunResistance</c>,
        /// floored at <paramref name="minimumSeconds"/>. Basic-class string
        /// hits pass <c>BasicComboRules.EnemyBasicStunFloorFrames</c> so no
        /// roster enemy — however resistant — can act between chain hits.
        /// </summary>
        public void ApplyStun(float duration, float minimumSeconds) {
            if (CurrentState == EnemyState.Dead) return;
            float resistance = Mathf.Clamp(Data?.StunResistance ?? 0f, 0f, 1f);
            float stun = Mathf.Max(duration * (1f - resistance), minimumSeconds);
            if (stun <= 0f) return;
            Executor.Cancel();
            _attackCommitted = false;
            _reactionFramesRemaining = 0;
            _stunTimer = stun;
            CurrentState = EnemyState.Stunned;
            PlayAnimation("hitstun");
        }

        /// <summary>
        /// Minimal Story status support mirroring StatusController semantics: one
        /// active status at a time, the newest completely replaces the previous.
        /// </summary>
        public void ApplyStatusEffect(StatusType type, float duration, float intensity = 1f) {
            if (CurrentState == EnemyState.Dead || type == StatusType.None || duration <= 0f) return;
            ClearStatusEffect();

            float potency = intensity <= 0f ? 1f : intensity;
            ActiveStatusType = type;
            _statusTimer = duration;
            _statusIntensity = potency;
            switch (type) {
                case StatusType.TimeDilation:
                    StatusMoveMultiplier = Mathf.Max(0.1f, 1f - 0.5f * potency);
                    break;
                case StatusType.RadiantBurn:
                    StatusDamageTakenMultiplier = 1f + 0.25f * potency;
                    break;
                case StatusType.Root:
                    StatusMoveMultiplier = 0f;
                    Velocity = new Vector2(0f, Velocity.Y);
                    break;
                case StatusType.StaticCharge:
                    ApplyStun(duration);
                    break;
                case StatusType.Venom:
                    _venomTickTimer = 1f;
                    break;
            }
            _glow?.SetStatus(type);
        }

        private void ClearStatusEffect() {
            ActiveStatusType = StatusType.None;
            _statusTimer = 0f;
            _statusIntensity = 1f;
            _venomTickTimer = 0f;
            StatusMoveMultiplier = 1f;
            StatusDamageTakenMultiplier = 1f;
            _glow?.ClearState(FTT.Combat.GlowLayer.Status);
        }

        private void TickStatus(float dt) {
            if (ActiveStatusType == StatusType.None) return;
            _statusTimer -= dt;
            if (ActiveStatusType == StatusType.Venom) {
                _venomTickTimer -= dt;
                if (_venomTickTimer <= 0f) {
                    _venomTickTimer += 1f;
                    TakeDamage(Math.Max(1, (int)MathF.Round(2f * _statusIntensity)));
                }
            }
            if (_statusTimer <= 0f) ClearStatusEffect();
        }

        private void UpdateHPBar() {
            if (_hpBar != null) {
                _hpBar.MaxValue = ScaledMaxHP;
                _hpBar.Value = CurrentHP;
            }
            ApplyBarVisibility();
        }

        /// <summary>
        /// Writes the current bar alpha onto the two overhead widgets. The
        /// HudOpacity accessibility setting multiplies in here rather than being
        /// read once at spawn, so moving the slider mid-level is visible on mobs
        /// already in the room.
        /// </summary>
        private void ApplyBarVisibility() {
            float alpha = _barVisibility.Alpha * Mathf.Clamp(FTT.UI.HudOpacityBinder.CurrentSetting, 0f, 1f);
            bool visible = alpha > 0f;
            if (_hpBar != null) {
                _hpBar.Visible = visible;
                Color barModulate = _hpBar.Modulate;
                _hpBar.Modulate = new Color(barModulate.R, barModulate.G, barModulate.B, alpha);
            }
            if (_nameLabel != null) {
                _nameLabel.Visible = visible;
                Color labelModulate = _nameLabel.Modulate;
                _nameLabel.Modulate = new Color(labelModulate.R, labelModulate.G, labelModulate.B, alpha);
            }
        }

        private void SetFacing(bool facingRight) {
            _facingRight = facingRight;
            if (_sprite != null) _sprite.FlipH = !facingRight;
        }

        private void PlayAnimation(string animationName) {
            if (_sprite?.SpriteFrames == null || string.IsNullOrEmpty(animationName)) return;
            if (!_sprite.SpriteFrames.HasAnimation(animationName)) return;
            if (_sprite.Animation == animationName && _sprite.IsPlaying()) return;
            _sprite.Play(animationName);
        }

        private FTT.Characters.PlayerController FindNearestPlayer() {
            if (!IsInsideTree()) return null;
            FTT.Characters.PlayerController nearest = null;
            float nearestDist = float.MaxValue;
            Godot.Collections.Array<Node> players = GetTree().GetNodesInGroup("Players");
            using var playersLifetime = players.AsDisposable();
            foreach (var node in players) {
                if (node is FTT.Characters.PlayerController pc && pc.CurrentState != FTT.Characters.CharacterState.Dead) {
                    float d = GlobalPosition.DistanceTo(pc.GlobalPosition);
                    if (d < nearestDist) { nearestDist = d; nearest = pc; }
                }
            }
            return nearest;
        }

        public void ConfigureSpawn(Vector2 position, Vector2? waypointA = null, Vector2? waypointB = null) {
            GlobalPosition = position;
            _spawnPosition = position;
            Marker2D left = GetNodeOrNull<Marker2D>("Waypoints/Left") ?? GetNodeOrNull<Marker2D>("WaypointA");
            Marker2D right = GetNodeOrNull<Marker2D>("Waypoints/Right") ?? GetNodeOrNull<Marker2D>("WaypointB");
            if (left == null || right == null) return;
            left.Position = waypointA.HasValue ? waypointA.Value - position : new Vector2(-150f, 0f);
            right.Position = waypointB.HasValue ? waypointB.Value - position : new Vector2(150f, 0f);
            _patrolPointA = position + left.Position;
            _patrolPointB = position + right.Position;
            _patrolPointsSet = true;
        }

        // === Animation event callbacks (template contract) ===

        public void ActivateHitbox() => _attackHitbox?.Activate();

        public void DeactivateHitbox() => _attackHitbox?.Deactivate();

        public void EmitPresentationEvent() {
            EventBus.Instance?.RaiseEnemyPresentation(new EnemyPresentationPayload {
                SourceID = Data?.EnemyID ?? "",
                AbilityID = Executor.ActiveAbility?.AbilityID ?? "",
                PresentationEventID = Executor.ActiveAbility?.PresentationEventID ?? "",
                Phase = EnemyPresentationPhase.Active,
                Position = GlobalPosition
            });
        }

        // === Pooling ===

        public void OnSpawn() {
            ResolveNodes();
            ApplyDifficultyScaling();
            ApplyScaledHitboxDamage();
            CurrentHP = ScaledMaxHP;
            CurrentState = EnemyState.Patrol;
            _attackCooldownTimer = 0f;
            _eliteCooldownTimer = 0f;
            _stunTimer = 0f;
            _patrolIdleTimer = 0f;
            _deathTimer = 0f;
            _patrolForward = true;
            _reactionFramesRemaining = 0;
            _attackCommitted = false;
            _standOffEngaged = false;
            _eliteAbilityIndex = -1;
            _lastAttackWasElite = false;
            _target = null;
            _rewindFrozen = false;
            _checkpointCaptured = false;
            _spawnPosition = GlobalPosition;
            _returnTarget = _spawnPosition;
            ClearStatusEffect();
            Executor.Reset();
            Velocity = Vector2.Zero;
            CollisionLayer = CollisionLayers.Enemy;
            CollisionMask = CollisionLayers.EnemyBodyMask;
            if (_hurtbox != null) {
                _hurtbox.Monitoring = true;
                _hurtbox.Monitorable = true;
            }
            _pushbox?.SetPushEnabled(true);
            _attackHitbox?.Deactivate();
            PlayAnimation("idle");
            // Reset before the bar write: a recycled body must start hidden even if
            // its previous occupant died with a full bar showing.
            _barVisibility.Reset();
            UpdateHPBar();
            BindEvents();
        }

        public void OnDespawn() {
            UnbindEvents();
            _barVisibility.Reset();
            ApplyBarVisibility();
            Executor.Reset();
            _attackHitbox?.Deactivate();
            if (_hurtbox != null) {
                _hurtbox.Monitoring = false;
                _hurtbox.Monitorable = false;
            }
            _target = null;
            _rewindFrozen = false;
            _deathTimer = 0f;
            _attackCommitted = false;
            _standOffEngaged = false;
            _reactionFramesRemaining = 0;
            Velocity = Vector2.Zero;
            CollisionLayer = 0;
            CollisionMask = 0;
        }

        // === Rewind ===

        public void SetStoryRewindFrozen(bool frozen) {
            _rewindFrozen = frozen;
            if (frozen) {
                Velocity = Vector2.Zero;
                Executor.Cancel();
                _attackHitbox?.Deactivate();
            }
        }

        public void CaptureCheckpointState(string checkpointID) {
            if (CurrentState == EnemyState.Dead) return;
            _checkpointPosition = GlobalPosition;
            _checkpointHP = CurrentHP;
            _checkpointCaptured = true;
        }

        /// <summary>
        /// RestoreCheckpointState/ResetToInitialState rebuild the encounter after a
        /// Chronal Rewind; PreserveCurrentState (the default) leaves mobs mid-fight.
        /// </summary>
        public void ApplyStoryRewind() {
            // Dead actors stay dead (audit M-6, mirroring CaptureCheckpointState):
            // Die() zeroed collision/hurtbox/pushbox and already paid the kill's
            // dust, so a restore here would resurrect an invulnerable ghost whose
            // second death double-pays. The safest policy is to skip the restore.
            if (CurrentState == EnemyState.Dead) return;
            if (RewindPolicy == StoryRewindPolicy.PreserveCurrentState) return;
            bool useCheckpoint = RewindPolicy == StoryRewindPolicy.RestoreCheckpointState && _checkpointCaptured;
            GlobalPosition = useCheckpoint ? _checkpointPosition : _spawnPosition;
            CurrentHP = useCheckpoint && _checkpointHP > 0 ? _checkpointHP : ScaledMaxHP;
            CurrentState = EnemyState.Patrol;
            _target = null;
            _attackCooldownTimer = 0f;
            _eliteCooldownTimer = 0f;
            _stunTimer = 0f;
            _deathTimer = 0f;
            _attackCommitted = false;
            _standOffEngaged = false;
            _reactionFramesRemaining = 0;
            _lastAttackWasElite = false;
            Velocity = Vector2.Zero;
            ClearStatusEffect();
            Executor.Reset();
            _attackHitbox?.Deactivate();
            PlayAnimation("idle");
            // A rewound encounter has not been engaged yet in the restored timeline.
            _barVisibility.Reset();
            UpdateHPBar();
        }

        private void OnRewindTriggered(Vector2 targetPosition) => ApplyStoryRewind();

        private float OnHurtboxHit(FTT.Combat.HitPayload hit) {
            int damageApplied = TakeDamage(Mathf.Max(0, (int)Mathf.Round(hit.Damage)), hit.HitOrigin);
            ApplyKnockback(hit.Knockback, hit.AttackerFacingRight);
            if (hit.HitstunDuration > 0f) {
                // Basic-class STRING hits (the melee combo's "combo_N" hitboxes
                // plus the §2.8 directional strikes "up_attack" / "down_air";
                // the idiom Joan's Zealous Vigor also keys on) floor the
                // post-resistance stun so the universal basic set holds every
                // roster enemy (max StunResistance 0.65) through its gaps. Other
                // Basic-class sources — Leonardo's turret, Tesla's coil arcs —
                // keep their authored short stuns.
                bool basicStringHit = hit.AttackClass == FTT.Combat.AttackClass.Basic
                    && FTT.Combat.BasicComboRules.IsBasicStringHitbox(hit.HitboxID);
                float minimumSeconds = basicStringHit
                    ? FTT.Combat.BasicComboRules.EnemyBasicStunFloorFrames / 60f
                    : 0f;
                ApplyStun(hit.HitstunDuration, minimumSeconds);
            }
            if (hit.AppliedStatus != StatusType.None && hit.StatusDuration > 0f) {
                ApplyStatusEffect(hit.AppliedStatus, hit.StatusDuration, hit.StatusIntensity);
            }
            return damageApplied;
        }
    }
}
