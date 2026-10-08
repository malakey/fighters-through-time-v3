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
    public partial class EnemyController : CharacterBody2D, IPoolable, IStoryRewindable, IStoryRewindSimulation, IStoryTimeFreezable, FTT.Combat.IDamageable {
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

        // === V7.4 Enemy Stagger Discipline (FTT.Combat.EnemyStaggerRules; PvE only) ===
        // Getup armor after a naturally-expiring stun (all tiers), the elite
        // stagger budget behind Armored Recovery, and the per-target
        // diminishing-special-stun window. Bosses never flinch (BossController
        // ignores HitPayload.HitstunDuration), so their budget constant has no
        // consumer — recorded compliance, not an omission.
        private int _getupArmorFramesRemaining;
        private float _armoredRecoveryTimer;
        private float _staggerBudget;
        private float _specialStunWindowTimer;
        // V7.1 hitstop freeze (BasicComboRules numbers); max-assigned, never
        // shortened. Counted in physics frames so it cannot drift against the
        // 60 Hz clock the shared frame tables are authored in.
        private int _hitstopFramesRemaining;
        /// <summary>Read-only view of the shared hitstop freeze (diagnostics / feel telemetry).</summary>
        public int HitstopFramesRemaining => _hitstopFramesRemaining;

        // === V7.2 Grabs & Throws (Story: the beat-em-up payoff) ===
        // A held mob is pinned by the player each frame; a thrown mob is a
        // projectile — enemies it collides with in flight take the bowling
        // fraction and are knocked down. Elites and bosses never enter these.
        private bool _isHeldByPlayer;
        private bool _thrownFlight;
        private int _thrownBowlingDamage;
        private readonly System.Collections.Generic.HashSet<ulong> _bowledVictims = new();

        // === 2026-10-04 playtest-feel pass (StoryEnemyCombatRules; Story only) ===
        // _hitstopFramesRemaining doubles as the corpse's kill freeze: a lethal
        // direct hit holds the victim hitstop before the death animation plays.
        // S2: hits landed in the current uninterrupted stun chain (1-based once a
        // chain opens). The chain survives the hits of an Armored Recovery the
        // chain itself tripped (S3) and resets on natural stun expiry, the end of
        // that armored window, death, spawn/pool reset and rewind.
        private int _confirmChainHits;
        // S2: per stun chain, whether each Special (keyed by AttackID) was a
        // confirm — its first hit in the chain landed on hitstun another attack
        // opened. Every later hit of that Special in the chain keeps the same
        // answer, so a multi-hit Special's own follow-up hits (Divine Piercing's
        // 3 x 8, the Lorentz chain arc) are never prorated against the hitstun
        // their own first hit applied. Cleared with the chain.
        private readonly System.Collections.Generic.Dictionary<string, bool> _chainSpecialConfirms = new();
        // P4: the room x-bounds this mob is leashed to, resolved once per spawn
        // from the owning StoryLevelControllerBase (or set explicitly).
        private bool _leashRoomResolved;
        private bool _hasLeashRoom;
        private float _leashRoomMinX;
        private float _leashRoomMaxX;
        // P4: body extents read once from the authored collision shape, for the
        // ledge and wall probes and the floating damage number's height.
        private float _bodyHalfWidth = 23f;
        private float _bodyHeight = 72f;
        private float _bodyCenterY = -36f;
        private PhysicsRayQueryParameters2D _probeQuery;
        // P3: the authored |x| of the AbilityOrigin marker, mirrored by SetFacing.
        private float _abilityOriginBaseX;

        /// <summary>Standard mobs only; elites and bosses are grab-immune.</summary>
        public bool IsGrabbable =>
            Data?.Tier == EnemyTier.Standard
            && CurrentState != EnemyState.Dead
            && !_isHeldByPlayer
            && !_thrownFlight;

        /// <summary>True while pinned in the player's grab. Test seam.</summary>
        public bool IsHeldByPlayer => _isHeldByPlayer;

        /// <summary>True while flying as a thrown projectile. Test seam.</summary>
        public bool IsThrownFlight => _thrownFlight;

        public void BeginHeld() {
            _isHeldByPlayer = true;
            Velocity = Vector2.Zero;
            Executor.Cancel();
            _attackHitbox?.Deactivate();
        }

        public void PinHeldAt(Vector2 position) {
            GlobalPosition = position;
            Velocity = Vector2.Zero;
        }

        public void ReleaseHeld() => _isHeldByPlayer = false;

        /// <summary>
        /// The throw launch: the mob becomes a projectile. Enemies it hits in
        /// flight take <paramref name="bowlingDamage"/> and are knocked down;
        /// the flight ends on ground contact as a knockdown.
        /// </summary>
        public void LaunchThrown(Vector2 velocity, int bowlingDamage) {
            _isHeldByPlayer = false;
            // A throw whose own damage killed the mob launches nothing
            // (PlayerController resolves the throw damage, then launches): the
            // Dead branch never runs a flight, so the latch would only ride the
            // corpse through the pool into the next spawn (2026-10-04 review).
            if (CurrentState == EnemyState.Dead) return;
            _thrownFlight = true;
            _thrownBowlingDamage = Mathf.Max(0, bowlingDamage);
            _bowledVictims.Clear();
            Velocity = velocity;
            // A throw is a knockdown, not hitstun: it spends no poise budget (S3).
            // The throw velocity just written is the flight, so the stun keeps it.
            ApplyStun(1.2f, 0f, fromSpecial: false, chargesStaggerBudget: false, eliteBudgetMinimumSeconds: 0f,
                chargesStandardPoise: false, carriesKnockback: true);
        }

        /// <summary>
        /// Thrown-projectile flight: gravity and motion only (no AI), sweeping
        /// nearby enemies — standards and elites alike — for the crowd-bowling
        /// hit. Ends as a knockdown when the mob returns to the floor.
        /// </summary>
        private void ProcessThrownFlight(float dt) {
            ApplyGravity(dt);
            MoveAndSlide();
            Godot.Collections.Array<Node> enemies = GetTree().GetNodesInGroup("Enemies");
            using var enemiesLifetime = enemies.AsDisposable();
            foreach (Node node in enemies) {
                if (node is not EnemyController other || other == this) continue;
                if (other.CurrentState == EnemyState.Dead || other._thrownFlight || other._isHeldByPlayer) continue;
                if (!_bowledVictims.Add(other.GetInstanceId())) continue;
                if (other.GlobalPosition.DistanceTo(GlobalPosition) > 55f) {
                    _bowledVictims.Remove(other.GetInstanceId());
                    continue;
                }
                // Crowd bowling: 0.5x BasicAttackDamage and a knockdown.
                other.TakeDamage(_thrownBowlingDamage, GlobalPosition);
                bool bowledKnockback = other.TryApplyKnockback(new Vector2(2.5f, -1.5f), Velocity.X >= 0f);
                // A bowled STANDARD's knockdown is not hitstun and spends no S3
                // poise; a bowled ELITE keeps the V7.4 budget charge it took
                // before the 2026-10-04 pass (the applied stun, no floor).
                other.ApplyStun(1.0f, 0f, fromSpecial: false,
                    chargesStaggerBudget: other.Data?.Tier == EnemyTier.Elite,
                    eliteBudgetMinimumSeconds: 0f,
                    chargesStandardPoise: false,
                    carriesKnockback: bowledKnockback);
            }
            if (IsOnFloor()) {
                _thrownFlight = false;
                Velocity = new Vector2(0f, Velocity.Y);
            }
        }
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
        private float _controlStatusTimer;
        private float _controlStatusIntensity = 1f;
        private float _damageStatusTimer;
        private float _damageStatusIntensity = 1f;
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

        /// <summary>
        /// Active status effect, Story-only. V7.6: routing is
        /// <see cref="FTT.Combat.StatusRouting.SlotOf"/> and replacement within a
        /// slot is stronger-wins, not newest-wins.
        /// </summary>
        public StatusType ControlStatusType { get; private set; } = StatusType.None;
        public StatusType DamageStatusType { get; private set; } = StatusType.None;
        /// <summary>Control slot first, then damage — the compat view for single-status readers.</summary>
        public StatusType ActiveStatusType =>
            ControlStatusType != StatusType.None ? ControlStatusType : DamageStatusType;
        public bool HasStatusEffect(StatusType type) =>
            ControlStatusType == type || DamageStatusType == type;
        public float StatusMoveMultiplier { get; private set; } = 1f;
        public float StatusDamageTakenMultiplier { get; private set; } = 1f;

        /// <summary>The V7.6 contracted two-slot view (IStatusEffectTarget).</summary>
        public FTT.Combat.StatusSlots ActiveStatuses {
            get => new() {
                Damage = new FTT.Combat.StatusEffectData {
                    Type = DamageStatusType,
                    RemainingSeconds = _damageStatusTimer,
                    Intensity = _damageStatusIntensity
                },
                Control = new FTT.Combat.StatusEffectData {
                    Type = ControlStatusType,
                    RemainingSeconds = _controlStatusTimer,
                    Intensity = _controlStatusIntensity
                }
            };
            set {
                ClearAllStatusEffects();
                if (value.Damage.IsActive) {
                    ApplyStatusEffect(value.Damage.Type, value.Damage.RemainingSeconds, value.Damage.Intensity);
                }
                if (value.Control.IsActive) {
                    ApplyStatusEffect(value.Control.Type, value.Control.RemainingSeconds, value.Control.Intensity);
                }
            }
        }

        public Node2D TargetNode => this;

        /// <summary>Clears one slot by identity, whatever occupies it.</summary>
        public void ClearStatusEffect(FTT.Combat.StatusSlot slot) {
            if (slot == FTT.Combat.StatusSlot.Damage) ClearDamageStatusSlot();
            else ClearControlStatusSlot();
            RefreshStatusGlow();
        }

        /// <summary>Both slots. Death, respawn, rewind restore and Restart Level call this.</summary>
        public void ClearAllStatusEffects() => ClearStatusEffect();

        // === V7.6 F07 Conductive mark (caster-owned; NOT a status) ===
        // No slot, no action lock, zero stagger budget, and it may remain while
        // the enemy acts inside a V7.4 armor window.

        private int _conductiveFramesRemaining;
        private int _conductiveSourcePlayerID = -1;

        public int ConductiveFramesRemaining => _conductiveFramesRemaining;
        public int ConductiveSourcePlayerID =>
            _conductiveFramesRemaining > 0 ? _conductiveSourcePlayerID : -1;
        public bool HasConductiveMark => _conductiveFramesRemaining > 0;
        public bool HasConductiveMarkFrom(int sourcePlayerID) =>
            _conductiveFramesRemaining > 0 && _conductiveSourcePlayerID == sourcePlayerID;

        /// <summary>
        /// One mark per target: a new source replaces the old one, the same
        /// source takes the longer remaining time — never additive, and never
        /// blocked by armor (a mark is not a stun).
        /// </summary>
        public void ApplyConductiveMark(int sourcePlayerID, int frames) {
            if (frames <= 0 || CurrentState == EnemyState.Dead) return;
            if (_conductiveSourcePlayerID != sourcePlayerID || frames > _conductiveFramesRemaining) {
                _conductiveSourcePlayerID = sourcePlayerID;
                _conductiveFramesRemaining = frames;
            }
        }

        public void ClearConductiveMark() {
            _conductiveFramesRemaining = 0;
            _conductiveSourcePlayerID = -1;
        }

        private void TickConductiveMark() {
            if (_conductiveFramesRemaining <= 0) return;
            _conductiveFramesRemaining--;
            if (_conductiveFramesRemaining <= 0) _conductiveSourcePlayerID = -1;
        }

        /// <summary>Story difficulty-scaled maximum HP; canonical base stays in EnemyData.</summary>
        public int ScaledMaxHP => _scaledMaxHP > 0 ? _scaledMaxHP : Data?.MaxHP ?? 1;

        /// <summary>Frames left before the committed attack starts its telegraph.</summary>
        public int ReactionFramesRemaining => _reactionFramesRemaining;

        /// <summary>Overhead HP bar / name label visibility state (Package 8 B1).</summary>
        public FTT.UI.OverheadBarVisibility BarVisibility => _barVisibility;

        public EnemyAbilityPhase AbilityPhase => Executor.Phase;
        public EnemyAbilityData ActiveAbility => Executor.ActiveAbility;

        /// <summary>
        /// V7.6 F14 (Package 11 A7a): true while this enemy is maintaining a
        /// Siphon tether — stationary, and unable to select or fire anything else.
        /// </summary>
        public bool IsChannelling => Executor.IsChannelling;

        /// <summary>The live Siphon tether this enemy owns, or null.</summary>
        public SiphonTetherChannel ActiveSiphonTether => Executor.ActiveTether;

        /// <summary>The outcome of this enemy's most recent Siphon attachment check.</summary>
        public SiphonAttachResult LastSiphonResult => Executor.LastSiphonResult;

        /// <summary>
        /// Advances only the ability executor, without the movement, gravity and
        /// MoveAndSlide the full physics step carries. Test seam: it lets a
        /// telegraph be walked frame by frame without a floor under the enemy.
        /// </summary>
        internal void TickAbilityExecutor(float delta) => Executor.Tick(delta);

        /// <summary>
        /// Runs one Attacking-state step in isolation. Test seam for the V7.6
        /// channelling gate: it exercises the real <see cref="ProcessAttacking"/>
        /// without the gravity and MoveAndSlide a full physics step would need a
        /// floor for.
        /// </summary>
        internal void PumpAttackState(float delta) {
            CurrentState = EnemyState.Attacking;
            ProcessAttacking(delta);
        }
        public bool LastAttackWasElite => _lastAttackWasElite;
        public bool IsFacingRight => _facingRight;

        // === V7.4 stagger-discipline seams ===

        /// <summary>True during the armored getup window after a naturally-expiring stun.</summary>
        public bool IsGetupArmored => _getupArmorFramesRemaining > 0;

        /// <summary>True during the elite budget-triggered Armored Recovery window.</summary>
        public bool IsArmoredRecovery => _armoredRecoveryTimer > 0f;

        /// <summary>
        /// True while any V7.4 armor is up: incoming hits deal full damage but
        /// apply no hitstun and no knockback, and the enemy acts freely. Armor,
        /// not invulnerability — death is unaffected.
        /// </summary>
        public bool IsStaggerArmored => IsGetupArmored || IsArmoredRecovery;

        /// <summary>
        /// Accumulated post-resistance stun credit (elites since V7.4; standards
        /// since the 2026-10-04 S3 poise, Special-sourced stuns at a premium).
        /// Test seam.
        /// </summary>
        public float StaggerBudgetSeconds => _staggerBudget;

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
            // P3: the scenes author the marker for a right-facing body; SetFacing
            // mirrors it so a left-facing caster fires from in front, not behind.
            if (_abilityOrigin != null) _abilityOriginBaseX = Mathf.Abs(_abilityOrigin.Position.X);
            if (GetNodeOrNull<CollisionShape2D>("CollisionShape2D") is { Shape: RectangleShape2D bodyRect } bodyShape) {
                _bodyHalfWidth = bodyRect.Size.X * 0.5f;
                _bodyHeight = bodyRect.Size.Y;
                _bodyCenterY = bodyShape.Position.Y;
            }
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
                FTT.Combat.RetroSpriteScaleNormalizer.Attach(
                    _sprite, FTT.Combat.RetroSpriteScaleNormalizer.FigureKind.Enemy);
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
                bool hasAuthoredFrames = Data?.SpriteFramesResource != null;
                if (hasAuthoredFrames) _sprite.SpriteFrames = Data.SpriteFramesResource;
                Color tint = hasAuthoredFrames ? Colors.White : Data?.PlaceholderTint ?? Colors.White;
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
            _attackHitbox.SourceIsStandardMob = Data.Tier == EnemyTier.Standard; // F7 (FEEL): standard-mob hits never launch the player
        }

        public override void _PhysicsProcess(double delta) {
            if (_rewindFrozen) return;
            float dt = (float)delta;

            // Bars keep fading while the body plays out its death animation, so a
            // corpse does not hold a full-opacity bar for the pool's release delay.
            _barVisibility.Tick(dt);
            ApplyBarVisibility();

            if (CurrentState == EnemyState.Dead) {
                // 2026-10-04 kill weight (victim half): a lethal direct hit holds
                // the corpse in its flashed hitstun pose for the hit's own victim
                // hitstop before the death animation and the release clock start.
                // Gameplay (the kill payload, dust, collision) already resolved
                // in Die(); this is presentation timing only.
                if (_hitstopFramesRemaining > 0) {
                    _hitstopFramesRemaining--;
                    Velocity = Vector2.Zero;
                    if (_hitstopFramesRemaining <= 0) {
                        if (_sprite != null) _sprite.SpeedScale = 1f;
                        PlayAnimation("death");
                    }
                    return;
                }
                ProcessDead(dt);
                return;
            }

            // V7.1 hitstop: a landed hit freezes this enemy's gameplay clock
            // (state timers, velocity, position, animation) for the shared
            // window; only presentation fades above keep running.
            if (_hitstopFramesRemaining > 0) {
                _hitstopFramesRemaining--;
                if (_sprite != null) _sprite.SpeedScale = 0f;
                if (_hitstopFramesRemaining <= 0 && _sprite != null) _sprite.SpeedScale = 1f;
                return;
            }

            // V7.2 grabs: a held mob is fully owned by the player's grab (the
            // player pins the position each frame); a thrown mob is a
            // projectile until it lands.
            if (_isHeldByPlayer) return;
            if (_thrownFlight) {
                ProcessThrownFlight(dt);
                return;
            }

            TickStatus(dt);
            if (CurrentState == EnemyState.Dead) return;
            // F07: the Conductive mark is not a status — it ticks on its own
            // frame counter and locks nothing.
            TickConductiveMark();
            if (_attackCooldownTimer > 0) _attackCooldownTimer -= dt;
            if (_eliteCooldownTimer > 0) _eliteCooldownTimer -= dt;
            TickStaggerDiscipline(dt);
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
            ResolveSlamBounce();
            MoveAndSlide();
            _pushbox?.ResolveStoryOverlaps();
        }

        // === A12 (Package 13 W1): the Down-Air slam's single ground bounce ===
        // A mob struck by the player's Down-Air is spiked straight down; on the
        // ground, or on reaching the floor or a one-way surface, it bounces back
        // up ONCE at the slam's own scaled vertical speed. Story mobs have no
        // tumble or tech (VERIFY-M05-KNOCKDOWN-SCOPE), so the bounce is the whole
        // rule here. Any later knockback replaces the owed bounce.
        private bool _slamBouncePending;
        private float _slamBounceSpeed;

        /// <summary>A12 test seam: this mob still owes its slam bounce.</summary>
        public bool SlamBouncePending => _slamBouncePending;

        private void ResolveSlamBounce() {
            if (!_slamBouncePending || !IsOnFloor() || CurrentState == EnemyState.Dead) return;
            _slamBouncePending = false;
            Velocity = new Vector2(Velocity.X, -_slamBounceSpeed);
            _slamBounceSpeed = 0f;
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

        /// <summary>
        /// Package 12 W4 (GAP-10c): where this enemy aims — the target's live
        /// Cleopatra sand decoy while one stands (design: "enemies briefly target
        /// it, 1 s"), otherwise the target itself.
        /// </summary>
        public Vector2 TargetAimPosition =>
            _target == null
                ? GlobalPosition
                : FTT.Characters.Abilities.SandDecoyNode.TryGetLure(_target, out Vector2 lure)
                    ? lure
                    : _target.GlobalPosition;

        /// <summary>Package 12 W4 test seam: pins the chase target without an aggro scan.</summary>
        internal void SetChaseTargetForTest(FTT.Characters.PlayerController target) => _target = target;

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

            // Package 12 W4 (GAP-10c): a live sand decoy of the target's is what
            // the enemy approaches and strikes for its one-second life.
            Vector2 aim = TargetAimPosition;
            float dist = GlobalPosition.DistanceTo(aim);
            // P4 (2026-10-04, provisional): crossing DeAggroRadius no longer drops
            // the chase on its own — an aggroed mob keeps after a target still in
            // its room, or within the leash multiple of the radius (the only test
            // when the mob has no room). Aggro acquisition is unchanged.
            if (dist > (Data?.DeAggroRadius ?? 600f) && !KeepsChasingTarget()) {
                StartReturning();
                return;
            }

            // V7.3: a Teleport elite is reactive — the target closing to point
            // blank triggers it ahead of the ordinary attack commit.
            if (TryReactivePhaseSkip()) return;
            // M20: a standard's Teleport is the opposite — a gap-closer from range.
            if (TryEngageBlink()) return;

            if (dist <= AttackRangePixels && _attackCooldownTimer <= 0) {
                EnterAttacking();
                return;
            }

            Vector2 toTarget = aim - GlobalPosition;
            float sign = Mathf.Sign(toTarget.X);

            // P4: a ground mob whose target sits (nearly) straight above holds
            // its post and its facing rather than flipping under the player.
            if (!IsFlying && FTT.Combat.StoryEnemyCombatRules.HoldsUnderTarget(toTarget.X, toTarget.Y)) {
                HoldChasePosition(dt);
                return;
            }
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

            // P4: ground mobs do not jump (design-godot.md:2984), so they stop at
            // a drop-off or a wall instead of walking into the void or pushing
            // forever. Phasers pursue through walls by design and skip the wall
            // half; flying mobs skip both.
            if (!IsFlying && IsChaseStepBlocked(sign)) {
                // An edge or a wall stops the walk on the spot: decelerating
                // past the probe would carry the body over the drop.
                Velocity = new Vector2(0f, Velocity.Y);
                PlayAnimation("idle");
                return;
            }

            float verticalVelocity = IsFlying
                ? Mathf.Clamp(toTarget.Y, -MoveSpeedPixels, MoveSpeedPixels)
                : Velocity.Y;
            Velocity = new Vector2(sign * MoveSpeedPixels, verticalVelocity);
            PlayAnimation("patrol");
        }

        // === P4 (2026-10-04, provisional): chase leash and ledge/wall awareness ===

        /// <summary>
        /// Leashes this mob's chase to an explicit room x-range (level authoring
        /// and test seam). Without a call, the range is resolved once per spawn
        /// from the owning <see cref="StoryLevelControllerBase"/>'s room triggers.
        /// </summary>
        public void SetChaseLeashRoom(float minX, float maxX) {
            _leashRoomResolved = true;
            _hasLeashRoom = maxX >= minX;
            _leashRoomMinX = Mathf.Min(minX, maxX);
            _leashRoomMaxX = Mathf.Max(minX, maxX);
        }

        /// <summary>True when this mob is leashed to a room x-range. Test seam.</summary>
        public bool HasChaseLeashRoom {
            get {
                EnsureLeashRoomResolved();
                return _hasLeashRoom;
            }
        }

        private bool KeepsChasingTarget() {
            if (_target == null || !IsInstanceValid(_target)) return false;
            EnsureLeashRoomResolved();
            float targetX = _target.GlobalPosition.X;
            bool inRoom = _hasLeashRoom && targetX >= _leashRoomMinX && targetX <= _leashRoomMaxX;
            return FTT.Combat.StoryEnemyCombatRules.KeepsChasing(
                GlobalPosition.DistanceTo(_target.GlobalPosition),
                Data?.DeAggroRadius ?? 600f,
                inRoom);
        }

        /// <summary>
        /// The mob's room is the authored room trigger whose camera bounds span
        /// its spawn x (the first match, as <c>FindRoomTriggerContaining</c>
        /// resolves overlaps). Florence and the Tutorial, which do not extend the
        /// level base, leave the mob on the radius leash alone.
        /// </summary>
        private void EnsureLeashRoomResolved() {
            if (_leashRoomResolved) return;
            _leashRoomResolved = true;
            _hasLeashRoom = false;
            for (Node node = GetParent(); node != null; node = node.GetParent()) {
                if (node is not StoryLevelControllerBase level) continue;
                foreach (RoomTransitionTrigger trigger in level.RoomTriggers) {
                    if (trigger == null || !IsInstanceValid(trigger)) continue;
                    Rect2 bounds = trigger.CameraBounds;
                    if (_spawnPosition.X < bounds.Position.X || _spawnPosition.X > bounds.End.X) continue;
                    _hasLeashRoom = true;
                    _leashRoomMinX = bounds.Position.X;
                    _leashRoomMaxX = bounds.End.X;
                    return;
                }
                return;
            }
        }

        /// <summary>Decelerates to rest in place, keeping the current facing.</summary>
        private void HoldChasePosition(float dt) {
            float decel = StandOffDecelerationPerSecond * dt;
            Velocity = new Vector2(Mathf.MoveToward(Velocity.X, 0f, decel), Velocity.Y);
            PlayAnimation("idle");
        }

        /// <summary>
        /// True when a grounded walker's next step in <paramref name="direction"/>
        /// is a drop deeper than <see cref="FTT.Combat.StoryEnemyCombatRules.LedgeProbeDepthPixels"/>
        /// or a wall. Airborne mobs (a launch, a fall) are never blocked here.
        /// </summary>
        private bool IsChaseStepBlocked(float direction) {
            if (direction == 0f || !IsOnFloor()) return false;
            if (!HasFloorAhead(direction, FTT.Combat.StoryEnemyCombatRules.LedgeProbeDepthPixels)) return true;
            if (Data?.PhasesThroughWalls == true) return false;
            if (IsOnWall() && GetWallNormal().X * direction < -0.5f) return true;
            return HasWallAhead(direction);
        }

        /// <summary>The chase ledge/wall check, without any AI. Test seam.</summary>
        internal bool IsChaseStepBlockedForTest(float direction) => IsChaseStepBlocked(direction);

        /// <summary>
        /// Floor probe: a vertical ray just past the front edge, from
        /// <see cref="FTT.Combat.StoryEnemyCombatRules.LedgeProbeRisePixels"/> above the
        /// feet to <paramref name="depth"/> below them, against Environment and
        /// one-way platforms. Starting inside a rising slope counts as floor.
        /// </summary>
        private bool HasFloorAhead(float direction, float depth) {
            float x = GlobalPosition.X + Mathf.Sign(direction)
                * (_bodyHalfWidth + FTT.Combat.StoryEnemyCombatRules.LedgeProbeAheadMarginPixels);
            return ProbeHits(
                new Vector2(x, GlobalPosition.Y - FTT.Combat.StoryEnemyCombatRules.LedgeProbeRisePixels),
                new Vector2(x, GlobalPosition.Y + depth),
                CollisionLayers.EnemyBodyMask,
                hitFromInside: true,
                fallback: true);
        }

        /// <summary>Wall probe: a horizontal ray at body centre height, Environment only.</summary>
        private bool HasWallAhead(float direction) {
            Vector2 centre = GlobalPosition + new Vector2(0f, _bodyCenterY);
            float reach = _bodyHalfWidth + FTT.Combat.StoryEnemyCombatRules.WallProbeMarginPixels;
            return ProbeHits(centre, centre + new Vector2(Mathf.Sign(direction) * reach, 0f),
                CollisionLayers.Environment, hitFromInside: false, fallback: false);
        }

        /// <summary>
        /// One ray against the physics space, reusing a single query object (never
        /// disposed — it is RefCounted) and disposing the engine-returned result.
        /// The masks never include the Enemy layer, so the body needs no exclusion.
        /// Outside the tree — or inside a physics in/out callback flush, where the
        /// direct space state is inaccessible (<see cref="FTT.Core.PhysicsCallbackGuard"/>)
        /// — the probe answers <paramref name="fallback"/> without querying.
        /// </summary>
        private bool ProbeHits(Vector2 from, Vector2 to, uint mask, bool hitFromInside, bool fallback) {
            if (!IsInsideTree() || FTT.Core.PhysicsCallbackGuard.IsInPhysicsCallback) return fallback;
            PhysicsDirectSpaceState2D space = GetWorld2D()?.DirectSpaceState;
            if (space == null) return fallback;
            _probeQuery ??= new PhysicsRayQueryParameters2D { CollideWithAreas = false, CollideWithBodies = true };
            _probeQuery.From = from;
            _probeQuery.To = to;
            _probeQuery.CollisionMask = mask;
            _probeQuery.HitFromInside = hitFromInside;
            using Godot.Collections.Dictionary hit = space.IntersectRay(_probeQuery);
            return hit.Count > 0;
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

            // V7.6 F14 (Package 11 A7a) channelling gate: while a Siphon Snare is
            // maintaining, the caster is stationary and mute. It selects no new
            // attack, fires no reactive Teleport and applies no dash velocity —
            // the whole contract is "the Eraser is stationary and cannot attack or
            // use the Null Lance while maintaining". The velocity zero above
            // already holds it in place; this return is what stops everything else.
            if (Executor.IsChannelling) return;

            // V7.3: the target closing to point blank during the pre-commit
            // reaction delay fires the reactive Teleport instead of letting the
            // committed attack whiff through a body already inside its arc.
            if (!_attackCommitted && TryReactivePhaseSkip()) return;

            if (_reactionFramesRemaining > 0) {
                _reactionFramesRemaining--;
                // Aim only during the pre-commit reaction delay. The facing locks
                // the moment the attack commits, because the executor resolves the
                // hitbox and dash direction on the facing captured at Begin — a
                // sprite that kept tracking would telegraph a hit that then lands
                // behind the enemy (audit M-19; BossController has the same rule).
                if (_target != null) SetFacing(TargetAimPosition.X >= GlobalPosition.X);
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
                // M20: a finished engage blink hands straight back to Chase without
                // arming the primary's cooldown — the ambush swing follows.
                if (_secondaryBlinkInFlight) {
                    _secondaryBlinkInFlight = false;
                } else {
                    _attackCooldownTimer = Data?.AttackCooldown ?? 2f;
                }
                CurrentState = EnemyState.Chase;
            }
        }

        /// <summary>
        /// Commits one attack through the shared executor. Passing null selects the
        /// next attack from the data (standard primary or the next elite ability).
        /// </summary>
        public void BeginAttack(EnemyAbilityData ability = null) {
            ability ??= SelectNextAttack();
            Vector2 targetPosition = _target != null
                ? TargetAimPosition
                : GlobalPosition + new Vector2(_facingRight ? 100f : -100f, 0f);
            // V7.2 classification: elite signature abilities are Guard-Crush
            // (2 charges, orange telegraph); mobs never carry unblockables.
            // V7.6 (A7a): an ability may opt OUT of that implicit elite flag with
            // ForcesBasicBlockClass — the Eraser's Null Lance is Basic-class
            // because "blocking is never a trap", and the override also flips the
            // telegraph tint and glyph back to white-yellow/circle.
            Executor.Begin(ability, targetPosition, _facingRight,
                guardCrush: ResolveGuardCrush(ability),
                unblockable: false);
            PlayAnimation(_lastAttackWasElite ? "elite_attack" : "attack");
        }

        /// <summary>
        /// The single Guard-Crush rule for mob attacks: the elite implicit, the
        /// authored per-ability flag, and V7.6's explicit Basic-class override that
        /// beats both. Public so the telegraph contract is directly pinnable.
        /// </summary>
        public bool ResolveGuardCrush(EnemyAbilityData ability) {
            if (ability?.ForcesBasicBlockClass == true) return false;
            // M20: the implicit is an ELITE rule — a standard mob's opt-in
            // secondary ability is never Guard-Crush by tier alone.
            bool eliteImplicit = _lastAttackWasElite && Data?.Tier == EnemyTier.Elite;
            return eliteImplicit || (ability?.IsGuardCrushing ?? false);
        }

        /// <summary>
        /// Elites alternate standard attack and the next entry of EliteAbilities
        /// (sequential cycle per design Section 6) whenever the elite cooldown is up.
        /// V7.3 (Chrono-Warden rework): Teleport-archetype elites are REACTIVE —
        /// fired by <see cref="TryReactivePhaseSkip"/> when the target closes in —
        /// and are excluded from this sequential cycle, which would otherwise
        /// burn Phase Skip on schedule with nobody to skip away from.
        /// </summary>
        public EnemyAbilityData SelectNextAttack() {
            // M20: the same alternation serves an elite's EliteAbilities and a
            // standard's opt-in SecondaryAbilities (ActiveSecondaryAbilities).
            EnemyAbilityData[] secondaries = Data?.ActiveSecondaryAbilities;
            if (secondaries != null && _eliteCooldownTimer <= 0f && !_lastAttackWasElite) {
                int count = secondaries.Length;
                for (int step = 1; step <= count; step++) {
                    int index = (_eliteAbilityIndex + step) % count;
                    EnemyAbilityData elite = secondaries[index];
                    if (elite == null || elite.Archetype == EnemyAbilityArchetype.Teleport) continue;
                    _eliteAbilityIndex = index;
                    _lastAttackWasElite = true;
                    _eliteCooldownTimer = Mathf.Max(0f, Data.ActiveSecondaryCooldown);
                    return elite;
                }
            }
            _lastAttackWasElite = false;
            return Data?.PrimaryAttack ?? GetLegacyPrimaryAttack();
        }

        // === M20 (Package 12 W9): the standard-tier engage blink ================

        /// <summary>
        /// A standard with a Teleport secondary blinks only when the target is at
        /// least this multiple of its striking distance away — a gap-closer, never a
        /// point-blank dodge (that is the elite's reactive Phase Skip).
        /// </summary>
        public const float EngageBlinkMinRangeFactor = 1.5f;

        private bool _secondaryBlinkInFlight;

        /// <summary>True while a standard's engage blink is executing. Test seam.</summary>
        public bool IsEngageBlinking => _secondaryBlinkInFlight && Executor.IsBusy;

        private EnemyAbilityData FindStandardTeleport() {
            if (Data?.HasSecondaryAbilities != true) return null;
            foreach (EnemyAbilityData ability in Data.SecondaryAbilities) {
                if (ability != null && ability.Archetype == EnemyAbilityArchetype.Teleport) return ability;
            }
            return null;
        }

        /// <summary>
        /// The Rift Phantom's Teleport line (design §6: "the roster's Teleport line",
        /// M20): a standard whose target sits beyond
        /// <see cref="EngageBlinkMinRangeFactor"/> × its attack range, but inside
        /// its aggro radius, blinks through rift-space to reappear just past the
        /// target (the executor's far-side rule), telegraphed like any cast. It
        /// does not arm the primary attack's cooldown, so the ambush can follow.
        /// Public so tests can pin the trigger geometry.
        /// </summary>
        public bool TryEngageBlink() {
            if (_target == null || !IsInstanceValid(_target)) return false;
            if (Executor.IsBusy || _eliteCooldownTimer > 0f) return false;
            EnemyAbilityData teleport = FindStandardTeleport();
            if (teleport == null) return false;
            float distance = GlobalPosition.DistanceTo(_target.GlobalPosition);
            if (distance <= AttackRangePixels * EngageBlinkMinRangeFactor) return false;
            if (distance > (Data?.AggroRadius ?? 400f)) return false;

            _eliteCooldownTimer = Mathf.Max(0f, Data.ActiveSecondaryCooldown);
            _secondaryBlinkInFlight = true;
            CurrentState = EnemyState.Attacking;
            _attackCommitted = true;
            _reactionFramesRemaining = 0;
            Executor.Begin(teleport, _target.GlobalPosition, _facingRight,
                guardCrush: ResolveGuardCrush(teleport), unblockable: false);
            PlayAnimation("attack");
            return true;
        }

        // === V7.3 reactive Phase Skip (Chrono-Warden rework) ================

        /// <summary>Distance at which a Teleport elite fires reactively.</summary>
        public const float ReactiveTeleportRangePixels = 125f;

        /// <summary>The elite roster's Teleport ability, or null.</summary>
        private EnemyAbilityData FindReactiveTeleport() {
            if (Data?.HasEliteAbilities != true) return null;
            foreach (EnemyAbilityData elite in Data.EliteAbilities) {
                if (elite != null && elite.Archetype == EnemyAbilityArchetype.Teleport) return elite;
            }
            return null;
        }

        /// <summary>
        /// Fires the elite Teleport immediately — no reaction delay — when the
        /// target closes within <see cref="ReactiveTeleportRangePixels"/> while
        /// the elite cooldown is up and no attack is mid-execution, marking the
        /// elite cooldown. Public so tests can pin the trigger geometry.
        /// </summary>
        public bool TryReactivePhaseSkip() {
            if (_target == null || !IsInstanceValid(_target)) return false;
            if (Executor.IsBusy || _eliteCooldownTimer > 0f) return false;
            EnemyAbilityData teleport = FindReactiveTeleport();
            if (teleport == null) return false;
            if (GlobalPosition.DistanceTo(_target.GlobalPosition) > ReactiveTeleportRangePixels) return false;

            _eliteCooldownTimer = Mathf.Max(0f, Data.EliteAbilityCooldown);
            _lastAttackWasElite = true;
            CurrentState = EnemyState.Attacking;
            _attackCommitted = true;
            _reactionFramesRemaining = 0;
            // V7.6 (A7a): the reactive path honours the same Basic-class override
            // as BeginAttack — a hardcoded `true` here would have quietly made an
            // opted-out elite ability Guard-Crush on this one route.
            Executor.Begin(teleport, _target.GlobalPosition, _facingRight,
                guardCrush: ResolveGuardCrush(teleport), unblockable: false);
            PlayAnimation("elite_attack");
            return true;
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
                // V7.3 dual-channel telegraph: legacy mob melee is Basic-class,
                // so its tint joins the white/yellow family (the old orange
                // 1/0.6/0.3 read as Guard-Crush, breaking the colour promise).
                TelegraphTint = EnemyAbilityExecutor.BasicTelegraph,
                PresentationEventID = $"{Data?.EnemyID ?? "enemy"}.basic"
            };
            return _legacyPrimaryAttack;
        }

        private void ProcessStunned(float dt) {
            ApplyStunnedKnockbackFriction(dt);
            _stunTimer -= dt;
            if (_stunTimer <= 0) {
                // S2: natural expiry ends the uninterrupted stun chain.
                ResetConfirmChain();
                // V7.4 armored getup recovery: a NATURALLY expiring hitstun (a
                // refresh mid-stun never reaches here) arms the armor window —
                // damage still lands, flinch and knockback do not, and the
                // enemy acts freely (EnemyStaggerRules.GetupArmorFrames).
                _getupArmorFramesRemaining = FTT.Combat.EnemyStaggerRules.GetupArmorFrames;
                RefreshArmorPresentation();
                // V7.4 pressure-exit rule: leaving stun with the target inside
                // attack range prefers an immediate attack (through the normal
                // attack-selection path, so cadence and cooldowns hold) over
                // resuming Chase.
                if (!TryPressureExitAttack()) {
                    CurrentState = _target != null ? EnemyState.Chase : EnemyState.Patrol;
                }
            }
        }

        /// <summary>
        /// S1 (2026-10-04): the knockback a hit wrote decays under friction while
        /// the mob is stunned instead of being zeroed on the next frame, so the
        /// string nudges a mob and the 4.5× finisher actually carries it. Ground
        /// friction while grounded, a lighter drag while launched or flying;
        /// flying mobs keep their stunned hover (vertical zeroed). A ground mob
        /// never slides or sails off a drop: knockback heading over a ledge (or,
        /// airborne, over a void) is cancelled at the edge.
        /// </summary>
        private void ApplyStunnedKnockbackFriction(float dt) {
            bool grounded = !IsFlying && IsOnFloor();
            float decel = grounded
                ? FTT.Combat.StoryEnemyCombatRules.StunnedGroundFrictionPixelsPerSecondSquared
                : FTT.Combat.StoryEnemyCombatRules.StunnedAirDragPixelsPerSecondSquared;
            float vx = Mathf.MoveToward(Velocity.X, 0f, decel * dt);
            if (vx != 0f && !IsFlying) {
                float depth = grounded
                    ? FTT.Combat.StoryEnemyCombatRules.LedgeProbeDepthPixels
                    : FTT.Combat.StoryEnemyCombatRules.KnockbackAirborneLedgeProbeDepthPixels;
                if (!HasFloorAhead(vx, depth)) vx = 0f;
            }
            Velocity = new Vector2(vx, IsFlying ? 0f : Velocity.Y);
        }

        // === V7.4 Enemy Stagger Discipline (EnemyStaggerRules; PvE only) ===

        /// <summary>
        /// Per-frame stagger-discipline bookkeeping: the special-stun diminish
        /// window, the elite stagger budget's decay (only while not stunned),
        /// and both armor windows' expiry (with the pressure-exit answer when
        /// Armored Recovery ends with the target still in reach).
        /// </summary>
        private void TickStaggerDiscipline(float dt) {
            if (_specialStunWindowTimer > 0f) _specialStunWindowTimer -= dt;
            if (_staggerBudget > 0f && CurrentState != EnemyState.Stunned) {
                // S3 (2026-10-04, provisional): a standard's poise drains faster,
                // so it is spent inside one stun chain; elites keep V7.4's rate.
                float decayPerSecond = Data?.Tier == EnemyTier.Standard
                    ? FTT.Combat.StoryEnemyCombatRules.StandardStaggerDecayPerSecond
                    : FTT.Combat.EnemyStaggerRules.StaggerDecayPerSecond;
                _staggerBudget = Mathf.Max(0f, _staggerBudget - decayPerSecond * dt);
            }
            if (_getupArmorFramesRemaining > 0) {
                _getupArmorFramesRemaining--;
                if (_getupArmorFramesRemaining <= 0) RefreshArmorPresentation();
            }
            if (_armoredRecoveryTimer > 0f) {
                _armoredRecoveryTimer -= dt;
                if (_armoredRecoveryTimer <= 0f) {
                    _armoredRecoveryTimer = 0f;
                    // S2: the trip's exchange is over with its armored window.
                    ResetConfirmChain();
                    RefreshArmorPresentation();
                    TryPressureExitAttack();
                }
            }
        }

        /// <summary>
        /// The armor flash: the shared gold hyper-armor glow — the codebase's
        /// existing armor language, distinct from the red hit flash — pulses
        /// while any V7.4 armor window is up.
        /// </summary>
        private void RefreshArmorPresentation() => _glow?.SetHyperArmor(IsStaggerArmored);

        /// <summary>
        /// V7.4 pressure-exit: attacks immediately when the target sits inside
        /// attack range and the normal cadence permits (cooldown up, executor
        /// idle). Routed through <see cref="EnterAttacking"/> so the ordinary
        /// reaction delay and telegraph play. Returns false to let the caller
        /// fall back to Chase/Patrol.
        /// </summary>
        private bool TryPressureExitAttack() {
            if (_target == null || !IsInstanceValid(_target)) return false;
            if (_target.CurrentState == FTT.Characters.CharacterState.Dead) return false;
            if (CurrentState is EnemyState.Dead or EnemyState.Attacking) return false;
            if (Executor.IsBusy || _attackCooldownTimer > 0f) return false;
            if (GlobalPosition.DistanceTo(_target.GlobalPosition) > AttackRangePixels) return false;
            EnterAttacking();
            return true;
        }

        /// <summary>
        /// Elite budget trip: flinch- and knockback-proof for
        /// <see cref="FTT.Combat.EnemyStaggerRules.EliteArmoredRecoverySeconds"/>
        /// (damage still lands), armor flash up, and the AI immediately commits
        /// its signature telegraphed attack — the elite ability when its
        /// cooldown is up, else the primary — through the normal
        /// <see cref="BeginAttack"/> path so the class colour + glyph telegraph
        /// plays. The budget resets on trigger.
        /// </summary>
        private void BeginArmoredRecovery() {
            _staggerBudget = 0f;
            // S3 (2026-10-04, provisional): a standard's poise trip is the short
            // window; elites keep the V7.4 1.5 s.
            _armoredRecoveryTimer = Data?.Tier == EnemyTier.Elite
                ? FTT.Combat.EnemyStaggerRules.EliteArmoredRecoverySeconds
                : FTT.Combat.StoryEnemyCombatRules.StandardArmoredRecoverySeconds;
            _stunTimer = 0f;
            // S2 (2026-10-04 review): the confirm chain deliberately survives the
            // trip. The hits that land on the armored window are still the same
            // exchange (a multi-hit confirm's later hits, a second Special), so
            // they keep prorating; TickStaggerDiscipline ends the chain when the
            // window expires.
            // F07: Static Charge cannot persist as a separate input lock once
            // stagger protection has ended the effective stun. Entering armored
            // recovery drops the lock (and ApplyStatusEffect rejects its
            // reapplication for the duration of the protection).
            if (ControlStatusType == StatusType.StaticCharge) {
                ClearControlStatusSlot();
                RefreshStatusGlow();
            }
            Executor.Cancel();
            // Make the elite branch of SelectNextAttack eligible: the armored
            // answer is the signature ability whenever its cooldown allows.
            _lastAttackWasElite = false;
            CurrentState = EnemyState.Attacking;
            _attackCommitted = true;
            _reactionFramesRemaining = 0;
            RefreshArmorPresentation();
            BeginAttack();
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

        /// <summary>
        /// M08 (Package 12 W3) <see cref="FTT.Combat.IDamageable"/>: the full hit
        /// pipeline the hurtbox feeds (stun, knockback, stagger discipline,
        /// status, marks), reachable without a hurtbox in hand.
        /// </summary>
        public float TakeDamage(in FTT.Combat.HitPayload hit) => OnHurtboxHit(hit);

        /// <inheritdoc/>
        public bool IsAlive => CurrentState != EnemyState.Dead;

        /// <summary>
        /// V7.6 Level 0 Time Freeze drill: the blocker the player must escape
        /// past is unkillable, so the lesson cannot be solved by fighting. Set
        /// only by <c>Level00Controller</c> for the duration of the drill; no
        /// campaign encounter ever sets it.
        /// </summary>
        public bool DrillInvulnerable { get; set; }

        private int TakeDamage(int damage, Vector2? hitOrigin, bool ignoreDefenses = false) {
            if (CurrentState == EnemyState.Dead || DrillInvulnerable) return 0;
            float incoming = Math.Max(0, damage) * StatusDamageTakenMultiplier;
            // V7.2 companion ruling: player Ultimate-class damage ignores enemy
            // damage-reduction defenses (frontal shields, the bubble) — the
            // ultimate is the authored answer to a shelled target.
            if (!ignoreDefenses) {
                incoming *= 1f - Mathf.Clamp(ResolveFrontalReduction(hitOrigin), 0f, 0.95f);
                if (Executor.HasActiveShield) incoming *= 1f - Mathf.Clamp(Executor.ShieldDamageReduction, 0f, 1f);
            }

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
            ResetConfirmChain();
            // V7.6: death clears both status slots and the caster-owned mark.
            ClearAllStatusEffects();
            ClearConductiveMark();
            Executor.Cancel();
            _attackHitbox?.Deactivate();
            _attackCommitted = false;
            _reactionFramesRemaining = 0;
            Velocity = Vector2.Zero;
            CollisionLayer = 0;
            CollisionMask = 0;
            if (_hurtbox != null) {
                // Safe setters: a killing blow arrives inside the hit signal's
                // physics flush, where the direct writes are engine-blocked.
                _hurtbox.SetMonitoringSafe(false);
                _hurtbox.SetMonitorableSafe(false);
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
                ItemDropChanceMultiplier = Data?.ItemDropChance ?? 1f,
                IsSummoned = IsSummoned
            });
            EventBus.Instance?.RaiseEnemyPresentation(new EnemyPresentationPayload {
                SourceID = Data?.EnemyID ?? "",
                AbilityID = "",
                PresentationEventID = $"{Data?.EnemyID ?? "enemy"}.death",
                Phase = EnemyPresentationPhase.Death,
                Position = GlobalPosition
            });
        }

        public void ApplyKnockback(Vector2 knockback, bool attackerFacingRight) =>
            TryApplyKnockback(knockback, attackerFacingRight);

        /// <summary>
        /// <see cref="ApplyKnockback"/>, reporting whether it actually wrote the
        /// knockback velocity. The stun that follows a hit needs the answer: a
        /// stun opened by a hit that wrote no knockback stops the mob instead of
        /// letting S1's friction decay its chase or dash speed, while a refresh
        /// of a running stun keeps the slide (2026-10-04 review).
        /// </summary>
        private bool TryApplyKnockback(Vector2 knockback, bool attackerFacingRight) {
            if (CurrentState == EnemyState.Dead) return false;
            // V7.4: both armor windows are knockback-proof (damage still lands).
            if (IsStaggerArmored) return false;
            // Impulse-free hits (construct arcs/bites carry zero knockback)
            // must not replace the velocity with a zero vector.
            if (knockback == Vector2.Zero) return false;
            float weight = Data?.Weight ?? 1.0f;
            // Knockback replaces velocity, matching the player and the Fighter
            // sim — a hit imparts the same impulse regardless of prior motion —
            // and scales with the victim's missing HP after the hit
            // (gameplay-feel plan §2.5), the same rule both other combatants use.
            Velocity = FTT.Combat.DamageCalculator.CalculateKnockback(
                knockback,
                weight,
                attackerFacingRight,
                CurrentHP,
                ScaledMaxHP) * 60f;
            return true;
        }

        /// <summary>
        /// V7.1 hitstop: freezes this enemy's gameplay clock for the given
        /// frames (max-assign — an active freeze is never shortened). Dead
        /// enemies skip; the death animation owns that moment.
        /// </summary>
        public void ApplyHitstop(int frames) {
            if (frames <= 0 || CurrentState == EnemyState.Dead) return;
            if (frames > _hitstopFramesRemaining) _hitstopFramesRemaining = frames;
        }

        public void ApplyStun(float duration) => ApplyStun(duration, 0f);

        public void ApplyStun(float duration, float minimumSeconds) =>
            ApplyStun(duration, minimumSeconds, fromSpecial: false);

        /// <summary>
        /// Applies hitstun scaled down by the authored <c>StunResistance</c>,
        /// floored at <paramref name="minimumSeconds"/>. Since S5 (2026-10-04)
        /// <c>OnHurtboxHit</c> does not come through here: it passes the
        /// hit-indexed <c>StoryEnemyCombatRules.BasicStringStunFloorFrames</c>
        /// (34/44/24) as the stun floor, so no roster enemy — however
        /// resistant — can act between chain hits, and the pre-S5
        /// <c>BasicComboRules.EnemyBasicStunFloorFrames</c> only as the separate
        /// elite-budget floor of the private overload. A direct caller of this
        /// overload charges the elite budget exactly the stun it applies.
        /// V7.4 (Enemy Stagger Discipline): both armor windows deny the stun
        /// outright (damage already landed in TakeDamage); a special-sourced
        /// stun inside the diminish window applies at half strength; and on
        /// elites the applied stun feeds the stagger budget, tripping Armored
        /// Recovery when it exceeds <c>EliteStaggerBudgetSeconds</c>. A direct
        /// caller writes no knockback, so a stun it <i>opens</i> stops the mob's
        /// horizontal motion on the spot, while a <i>refresh</i> of a running
        /// stun keeps it (see the private overload's <c>carriesKnockback</c>).
        /// For Lincoln's Kinetic Splitting bounce window that depends on how long
        /// the spiked body fell: one that lands inside the spike's own stun gets
        /// a refresh and keeps its slide; one that lands inside the getup armor
        /// that stun's natural expiry arms has the window refused; and only a
        /// fall outlasting both opens a fresh stun, which zeroes the horizontal
        /// speed and keeps the bounce's upward speed.
        /// </summary>
        public void ApplyStun(float duration, float minimumSeconds, bool fromSpecial) =>
            ApplyStun(duration, minimumSeconds, fromSpecial, chargesStaggerBudget: true,
                eliteBudgetMinimumSeconds: minimumSeconds, chargesStandardPoise: true, carriesKnockback: false);

        /// <summary>
        /// <paramref name="chargesStaggerBudget"/> false is the grab system's
        /// knockdown on a standard (the thrown flight and a bowled standard): a
        /// knockdown is not hitstun, so it does not spend the poise budget a
        /// follow-up string needs (S3, 2026-10-04). A bowled <i>elite</i> passes
        /// true — elites are grab-immune but not bowling-immune, and the
        /// knockdown keeps charging their V7.4 budget as it always did.
        /// <paramref name="eliteBudgetMinimumSeconds"/> is the floor the V7.4
        /// elite budget is charged at — the pre-S5 shared floor for a basic-set
        /// hit, so the longer S5 stun floor never raises an elite's poise cost
        /// (2026-10-04 review). Direct callers pass their own floor, which keeps
        /// the charge equal to the applied stun exactly as before.
        /// <paramref name="chargesStandardPoise"/> false exempts the stun from the
        /// S3 <i>standard</i> budget only — an Ultimate-sourced hit
        /// (<see cref="FTT.Combat.StoryEnemyCombatRules.ChargesStandardPoise"/>);
        /// the elite budget ignores it.
        /// <paramref name="carriesKnockback"/> says the caller just wrote this
        /// body's velocity as knockback (or a throw). Otherwise a stun that
        /// <i>opens</i> here stops the mob — whatever chase, patrol or charge
        /// speed it carried is not knockback, and S1's stunned friction must not
        /// slide it 100+ px (2026-10-04 review). A refresh of a running stun
        /// keeps the knockback slide the earlier hit wrote.
        /// No poise accrues and no Armored Recovery trips while the body is
        /// frozen (<see cref="SetStoryRewindFrozen"/> / Time Freeze) or
        /// <see cref="DrillInvulnerable"/>: a frozen trip would commit an attack
        /// on a body that cannot act, and the budget never drains while frozen.
        /// </summary>
        private void ApplyStun(float duration, float minimumSeconds, bool fromSpecial, bool chargesStaggerBudget,
            float eliteBudgetMinimumSeconds, bool chargesStandardPoise, bool carriesKnockback) {
            if (CurrentState == EnemyState.Dead) return;
            // V7.4 armor: flinch-proof. Armor only arms on natural stun expiry
            // or a budget trip, so a hit landing DURING stun still refreshes
            // the stun normally through this path.
            if (IsStaggerArmored) return;
            float resistance = Mathf.Clamp(Data?.StunResistance ?? 0f, 0f, 1f);
            float resisted = duration * (1f - resistance);
            float stun = Mathf.Max(resisted, minimumSeconds);
            if (stun <= 0f) return;
            float eliteBudgetCharge = Mathf.Max(resisted, eliteBudgetMinimumSeconds);
            // V7.4 diminishing special stun: a special-sourced stun landing
            // within the window of the previous one applies full damage but
            // half stun; the window refreshes on every special-sourced stun.
            // Basics are untouched (the getup armor already bounds them).
            if (fromSpecial) {
                if (_specialStunWindowTimer > 0f) {
                    stun *= FTT.Combat.EnemyStaggerRules.SpecialStunDiminishFactor;
                    eliteBudgetCharge *= FTT.Combat.EnemyStaggerRules.SpecialStunDiminishFactor;
                }
                _specialStunWindowTimer = FTT.Combat.EnemyStaggerRules.SpecialStunDiminishWindowSeconds;
            }
            bool accruesPoise = chargesStaggerBudget && !_rewindFrozen && !DrillInvulnerable;
            // V7.6 F14 (A7a): a stun that gets this far is a SUCCESSFUL stagger
            // interrupt — the two armor windows returned above and a zero stun
            // returned above it — so it severs a maintained Siphon tether. An
            // armor-rejected hit deliberately never reaches here, which is the
            // contract's one explicit exclusion. A budget trip counts too: the
            // Eraser breaks out swinging, and either way the channel is over.
            Executor.InterruptSiphonTether();
            // V7.4 stagger budget (elites; bosses never flinch — see
            // BossController, whose hit intake ignores hitstun entirely): each
            // applied stun adds its post-resistance duration, and exceeding
            // the budget answers with Armored Recovery instead of the stun.
            if (accruesPoise && Data?.Tier == EnemyTier.Elite) {
                _staggerBudget += eliteBudgetCharge;
                if (_staggerBudget > FTT.Combat.EnemyStaggerRules.EliteStaggerBudgetSeconds) {
                    BeginArmoredRecovery();
                    return;
                }
            } else if (accruesPoise && chargesStandardPoise && Data?.Tier == EnemyTier.Standard) {
                // S3 standard poise (2026-10-04, provisional): the same rule a
                // tier down, with a budget one full string fits inside and a
                // Special-sourced stun charging extra, so string + Special trips
                // the short Armored Recovery and its committed counterattack.
                // Ultimate-sourced stuns charge nothing (review fix).
                _staggerBudget += FTT.Combat.StoryEnemyCombatRules.StandardBudgetCost(stun, fromSpecial);
                if (_staggerBudget > FTT.Combat.StoryEnemyCombatRules.StandardStaggerBudgetSeconds) {
                    BeginArmoredRecovery();
                    return;
                }
            }
            bool opensStun = CurrentState != EnemyState.Stunned;
            Executor.Cancel();
            _attackCommitted = false;
            _secondaryBlinkInFlight = false;
            _reactionFramesRemaining = 0;
            _stunTimer = stun;
            CurrentState = EnemyState.Stunned;
            // S1 review fix: the stunned friction decays knockback, not the
            // chase / patrol / ChargeDash speed the mob happened to be carrying.
            if (opensStun && !carriesKnockback) Velocity = new Vector2(0f, IsFlying ? 0f : Velocity.Y);
            PlayAnimation("hitstun");
        }

        /// <summary>
        /// Minimal Story status support mirroring StatusController semantics: the
        /// V7 two-slot rule routed through the shared
        /// <see cref="FTT.Combat.StatusRouting"/> table. A damaging status
        /// (Venom, RadiantBurn) and a control status (TimeDilation, StaticCharge,
        /// Root, Suppression) coexist; a new application competes only with the
        /// occupant of its own slot, and V7.6's stronger-wins rule means a weaker
        /// same-type reapplication does nothing at all.
        /// </summary>
        public void ApplyStatusEffect(StatusType type, float duration, float intensity = 1f) =>
            ApplyStatusEffect(type, duration, intensity, fromHitPayload: false);

        /// <summary>
        /// F07: <paramref name="fromHitPayload"/> marks the application that
        /// already had its stun folded into <see cref="TakeHit"/>'s single
        /// <see cref="ApplyStun"/> call, so a Static Charge riding a hit sets only
        /// the slot/visual state instead of charging the stagger budget a second
        /// time. Direct callers (a scripted status, a debug apply) keep the old
        /// behaviour and route their own stun.
        /// </summary>
        public void ApplyStatusEffect(StatusType type, float duration, float intensity, bool fromHitPayload) {
            if (CurrentState == EnemyState.Dead || type == StatusType.None || duration <= 0f) return;
            // V7.6: Suppression has no enemy-side effect (the ability lock is a
            // player verb). Occupying the control slot with an inert status would
            // silently evict a live Root or slow, so it is refused outright.
            if (type == StatusType.Suppression) return;
            // F07: Static Charge's action lock IS stun in PvE, so an armor window
            // rejects its reapplication the same way it rejects hitstun — no
            // lingering lock with no stun behind it.
            if (type == StatusType.StaticCharge && IsStaggerArmored) return;

            float potency = intensity <= 0f ? 1f : intensity;
            if (FTT.Combat.StatusRouting.SlotOf(type) == FTT.Combat.StatusSlot.Damage) {
                if (!FTT.Combat.StatusRouting.ShouldReplace(
                        DamageStatusType, _damageStatusIntensity, _damageStatusTimer,
                        type, potency, duration)) return;
                ClearDamageStatusSlot();
                DamageStatusType = type;
                _damageStatusTimer = duration;
                _damageStatusIntensity = potency;
                switch (type) {
                    case StatusType.RadiantBurn:
                        StatusDamageTakenMultiplier = 1f + 0.25f * potency;
                        break;
                    case StatusType.Venom:
                        _venomTickTimer = 1f;
                        break;
                }
            } else {
                if (!FTT.Combat.StatusRouting.ShouldReplace(
                        ControlStatusType, _controlStatusIntensity, _controlStatusTimer,
                        type, potency, duration)) return;
                ClearControlStatusSlot();
                ControlStatusType = type;
                _controlStatusTimer = duration;
                _controlStatusIntensity = potency;
                switch (type) {
                    case StatusType.TimeDilation:
                        StatusMoveMultiplier = Mathf.Max(0.1f, 1f - 0.5f * potency);
                        break;
                    case StatusType.Root:
                        StatusMoveMultiplier = 0f;
                        Velocity = new Vector2(0f, Velocity.Y);
                        break;
                    case StatusType.StaticCharge:
                        // F07: one hit is ONE stun event. When the charge rode a
                        // hit payload, TakeHit already applied
                        // max(hitstun, staticCharge) through a single ApplyStun
                        // with the correct fromSpecial classification — charging
                        // the budget again here is the double-stun defect.
                        if (!fromHitPayload) ApplyStun(duration);
                        break;
                }
            }
            _glow?.SetStatus(type);
        }

        private void ClearControlStatusSlot() {
            ControlStatusType = StatusType.None;
            _controlStatusTimer = 0f;
            _controlStatusIntensity = 1f;
            StatusMoveMultiplier = 1f;
        }

        private void ClearDamageStatusSlot() {
            DamageStatusType = StatusType.None;
            _damageStatusTimer = 0f;
            _damageStatusIntensity = 1f;
            _venomTickTimer = 0f;
            StatusDamageTakenMultiplier = 1f;
        }

        // Single glow layer: the newest status paints it; when a slot falls the
        // survivor repaints, and only an empty pair clears it.
        private void RefreshStatusGlow() {
            if (ActiveStatusType == StatusType.None) {
                _glow?.ClearState(FTT.Combat.GlowLayer.Status);
            } else {
                _glow?.SetStatus(ActiveStatusType);
            }
        }

        private void ClearStatusEffect() {
            ClearControlStatusSlot();
            ClearDamageStatusSlot();
            _glow?.ClearState(FTT.Combat.GlowLayer.Status);
        }

        private void TickStatus(float dt) {
            if (ControlStatusType != StatusType.None) {
                _controlStatusTimer -= dt;
                if (_controlStatusTimer <= 0f) {
                    ClearControlStatusSlot();
                    RefreshStatusGlow();
                }
            }
            if (DamageStatusType != StatusType.None) {
                _damageStatusTimer -= dt;
                if (DamageStatusType == StatusType.Venom) {
                    _venomTickTimer -= dt;
                    if (_venomTickTimer <= 0f) {
                        _venomTickTimer += 1f;
                        TakeDamage(Math.Max(1, (int)MathF.Round(2f * _damageStatusIntensity)));
                    }
                }
                if (_damageStatusTimer <= 0f) {
                    ClearDamageStatusSlot();
                    RefreshStatusGlow();
                }
            }
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
            // P3 (2026-10-04): the AbilityOrigin marker (EliteEnemy.tscn authors
            // it at (44, -58)) is mirrored with the body, so a left-facing caster
            // spawns its projectiles in front of it rather than behind its back.
            if (_abilityOrigin != null) {
                _abilityOrigin.Position = new Vector2(
                    facingRight ? _abilityOriginBaseX : -_abilityOriginBaseX,
                    _abilityOrigin.Position.Y);
            }
        }

        /// <summary>The AbilityOrigin marker's current local position, or null when the scene has none. Test seam.</summary>
        public Vector2? AbilityOriginLocalPosition => _abilityOrigin?.Position;

        /// <summary>Turns the body (and its AbilityOrigin) without any AI. Test seam.</summary>
        internal void SetFacingForTest(bool facingRight) => SetFacing(facingRight);

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
            // P4: the leash room follows the (new) spawn point.
            _leashRoomResolved = false;
            _hasLeashRoom = false;
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

        /// <summary>
        /// Package 12 W2 (GAP-04): reward provenance. Set by
        /// <see cref="EnemyFactory.Spawn"/> for a <c>SummonMinions</c> spawn and
        /// carried to <see cref="EnemyKilledPayload.IsSummoned"/>; a summon pays no
        /// F05 award. Reset on every pool cycle so a recycled body never inherits
        /// its previous occupant's provenance.
        /// </summary>
        public bool IsSummoned { get; set; }

        public void OnSpawn() {
            IsSummoned = false;
            ResolveNodes();
            ApplyDifficultyScaling();
            ApplyScaledHitboxDamage();
            CurrentHP = ScaledMaxHP;
            CurrentState = EnemyState.Patrol;
            _attackCooldownTimer = 0f;
            _eliteCooldownTimer = 0f;
            _stunTimer = 0f;
            _getupArmorFramesRemaining = 0;
            _armoredRecoveryTimer = 0f;
            _staggerBudget = 0f;
            _specialStunWindowTimer = 0f;
            // 2026-10-04 pass: a recycled body starts with no stun chain, no
            // kill freeze and an unresolved room leash.
            ResetConfirmChain();
            _hitstopFramesRemaining = 0;
            if (_sprite != null) _sprite.SpeedScale = 1f;
            _leashRoomResolved = false;
            _hasLeashRoom = false;
            ResetGrabAndSlamState();
            _patrolIdleTimer = 0f;
            _deathTimer = 0f;
            _patrolForward = true;
            _reactionFramesRemaining = 0;
            _attackCommitted = false;
            _standOffEngaged = false;
            _eliteAbilityIndex = -1;
            _lastAttackWasElite = false;
            _secondaryBlinkInFlight = false;
            _target = null;
            _rewindFrozen = false;
            _checkpointCaptured = false;
            _spawnPosition = GlobalPosition;
            _returnTarget = _spawnPosition;
            ClearStatusEffect();
            ClearConductiveMark();
            RefreshArmorPresentation();
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
            IsSummoned = false;
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
            _getupArmorFramesRemaining = 0;
            _armoredRecoveryTimer = 0f;
            _staggerBudget = 0f;
            _specialStunWindowTimer = 0f;
            ResetConfirmChain();
            _hitstopFramesRemaining = 0;
            if (_sprite != null) _sprite.SpeedScale = 1f;
            _leashRoomResolved = false;
            _hasLeashRoom = false;
            ResetGrabAndSlamState();
            Velocity = Vector2.Zero;
            CollisionLayer = 0;
            CollisionMask = 0;
        }

        /// <summary>
        /// Pool and rewind hygiene for the V7.2 grab/throw latches and the A12
        /// slam bounce: a body released mid-flight, while held, or still owing a
        /// bounce (the level unloads, or a later hit kills it first) must not
        /// carry any of it into its next spawn, where a stale thrown flight bowls
        /// the neighbours it spawned beside and a stale bounce launches it off its
        /// first floor contact; a rewind restore drops them the same way
        /// (2026-10-04 review).
        /// </summary>
        private void ResetGrabAndSlamState() {
            _isHeldByPlayer = false;
            _thrownFlight = false;
            _thrownBowlingDamage = 0;
            _bowledVictims.Clear();
            _slamBouncePending = false;
            _slamBounceSpeed = 0f;
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
            _getupArmorFramesRemaining = 0;
            _armoredRecoveryTimer = 0f;
            _staggerBudget = 0f;
            _specialStunWindowTimer = 0f;
            ResetConfirmChain();
            RefreshArmorPresentation();
            _hitstopFramesRemaining = 0;
            // The restored body is standing at its spawn/checkpoint: no grab,
            // no flight and no slam bounce owed from the abandoned timeline.
            ResetGrabAndSlamState();
            if (_sprite != null) _sprite.SpeedScale = 1f;
            _deathTimer = 0f;
            _attackCommitted = false;
            _standOffEngaged = false;
            _reactionFramesRemaining = 0;
            _lastAttackWasElite = false;
            _secondaryBlinkInFlight = false;
            Velocity = Vector2.Zero;
            ClearStatusEffect();
            ClearConductiveMark();
            Executor.Reset();
            _attackHitbox?.Deactivate();
            PlayAnimation("idle");
            // A rewound encounter has not been engaged yet in the restored timeline.
            _barVisibility.Reset();
            UpdateHPBar();
        }

        private void OnRewindTriggered(Vector2 targetPosition) => ApplyStoryRewind();

        private float OnHurtboxHit(FTT.Combat.HitPayload hit) {
            // S2 + S4 (2026-10-04, provisional): the Story intake scale — confirm
            // proration over the current stun chain, then basics pacing against
            // standards — is resolved before the hit can change the stun state.
            int baseDamage = Mathf.Max(0, (int)Mathf.Round(hit.Damage));
            int damageApplied = TakeDamage(
                FTT.Combat.StoryEnemyCombatRules.ScaleIntakeDamage(baseDamage, ResolveIntakeScale(in hit)),
                hit.HitOrigin,
                ignoreDefenses: hit.AttackClass == FTT.Combat.AttackClass.Ultimate);
            bool skipsHitstop = FTT.Combat.StoryEnemyCombatRules.VictimSkipsHitstop(
                hit.Delivery, hit.ExemptFromHitstop);
            if (damageApplied > 0 && !skipsHitstop) {
                // One rule for both halves (StoryEnemyCombatRules.VictimHitstopFrames).
                int victimHitstop = FTT.Combat.StoryEnemyCombatRules.VictimHitstopFrames(damageApplied, hit.Launches);
                if (CurrentState != EnemyState.Dead) {
                    // V7.1 hitstop (victim side), scaled by the damage that
                    // landed. F5 (2026-10-04): tick, construct and hazard
                    // deliveries and ExemptFromHitstop payloads never freeze.
                    ApplyHitstop(victimHitstop);
                } else {
                    // Kill weight (2026-10-04, victim half): a lethal direct hit
                    // no longer skips — the corpse holds its flashed hit pose for
                    // the same victim hitstop before the death animation.
                    BeginKillFreeze(victimHitstop);
                }
            }
            bool knockbackWritten = TryApplyKnockback(hit.Knockback, hit.AttackerFacingRight);
            // A12 (Package 13 W1): a Down-Air slam that actually moved this mob
            // (armor windows and flying mobs refuse it) owes one ground bounce;
            // any other knockback-carrying hit clears a stale one.
            if (hit.Knockback != Vector2.Zero) {
                _slamBouncePending = hit.HitboxID == FTT.Combat.BasicComboRules.DownAirHitboxID
                    && !IsFlying && !IsStaggerArmored && Velocity.Y > 0f;
                _slamBounceSpeed = _slamBouncePending ? Mathf.Abs(Velocity.Y) : 0f;
            }
            // F07 (V7.6): Static Charge's action lock counts as stun in PvE, so a
            // hit that carries BOTH hitstun and a Static Charge runs them
            // CONCURRENTLY and counts the GREATER duration ONCE — Tesla's finisher
            // must not turn 0.4 s into 0.8 s of elite stagger budget. The charge
            // is folded into the single ApplyStun below and its slot application
            // is then flagged fromHitPayload so it cannot re-enter ApplyStun.
            bool staticChargeRidesThisHit =
                hit.AppliedStatus == StatusType.StaticCharge && hit.StatusDuration > 0f;
            float concurrentStun = staticChargeRidesThisHit
                ? Mathf.Max(hit.HitstunDuration, hit.StatusDuration)
                : hit.HitstunDuration;
            if (concurrentStun > 0f) {
                // Basic-class STRING hits (the melee combo's "combo_N" hitboxes
                // plus the §2.8 directional strikes "up_attack" / "down_air";
                // the idiom Joan's Zealous Vigor also keys on) floor the
                // post-resistance stun so the universal basic set holds every
                // roster enemy (max StunResistance 0.65) through its gaps. Other
                // Basic-class sources — Leonardo's turret, Tesla's coil arcs —
                // keep their authored short stuns. S5 (2026-10-04): the floor is
                // hit-indexed (Story-only rulebook) so it actually spans the
                // buffered string's connect gaps — 34 frames after hit 1, 44
                // after hit 2 — instead of the shared 24 that released a
                // resistant mob between hits.
                bool basicStringHit = hit.AttackClass == FTT.Combat.AttackClass.Basic
                    && FTT.Combat.BasicComboRules.IsBasicStringHitbox(hit.HitboxID);
                float minimumSeconds = basicStringHit
                    ? FTT.Combat.StoryEnemyCombatRules.BasicStringStunFloorFrames(hit.HitboxID) / 60f
                    : 0f;
                // The V7.4 elite stagger budget keeps charging the pre-S5 floor:
                // the S5 floor lengthens the stun, not the elite's poise cost.
                float eliteBudgetMinimumSeconds = basicStringHit
                    ? FTT.Combat.StoryEnemyCombatRules.EliteBudgetStunFloorFrames(hit.HitboxID) / 60f
                    : 0f;
                // V7.4: Special-class hits are marked so ApplyStun can run the
                // diminishing-special-stun window (ultimates and basics are
                // exempt — the loop being closed is the special-ability chain).
                // S3 review fix: an Ultimate-sourced stun (class or origin)
                // charges no standard poise. S1 review fix: a stun this hit
                // opens without writing knockback stops the mob.
                ApplyStun(concurrentStun, minimumSeconds,
                    fromSpecial: hit.AttackClass == FTT.Combat.AttackClass.Special,
                    chargesStaggerBudget: true,
                    eliteBudgetMinimumSeconds: eliteBudgetMinimumSeconds,
                    chargesStandardPoise: FTT.Combat.StoryEnemyCombatRules.ChargesStandardPoise(
                        hit.AttackClass, hit.Origin),
                    carriesKnockback: knockbackWritten);
            }
            if (hit.AppliedStatus != StatusType.None && hit.StatusDuration > 0f) {
                ApplyStatusEffect(hit.AppliedStatus, hit.StatusDuration, hit.StatusIntensity,
                    fromHitPayload: true);
            }
            // V7.6 F07: the caster-owned combo mark rides the same hit but is not
            // a status — no slot, no action lock, zero stagger budget, and armor
            // never refuses it.
            if (hit.ComboMark == FTT.Combat.ComboMarkType.Conductive && hit.ComboMarkFrames > 0) {
                ApplyConductiveMark(hit.AttackerIndex, hit.ComboMarkFrames);
            }
            PresentDealtHit(in hit, damageApplied);
            return damageApplied;
        }

        // === 2026-10-04 playtest-feel pass: intake, kill weight, feedback ===

        /// <summary>Hits counted in the current uninterrupted stun chain (S2). Test seam.</summary>
        public int ConfirmChainHits => _confirmChainHits;

        /// <summary>
        /// The Story intake scale for one payload hit. S2 confirm proration: a
        /// non-exempt hit opens a chain (hit 1) on a mob that is not in hitstun and
        /// extends it on a mob that is — or that is in the Armored Recovery the
        /// chain's own poise trip opened; the chain's 4th and later hits decay, and a
        /// Special that is a confirm (see <see cref="IsSpecialConfirm"/>) is cut
        /// further. Ultimates, ticks and hazards are exempt and do not advance the
        /// chain. S4 basics pacing then scales the universal basic set against
        /// Standard-tier mobs. Advances the chain, so it is called exactly once
        /// per hit.
        /// </summary>
        private float ResolveIntakeScale(in FTT.Combat.HitPayload hit) {
            if (CurrentState == EnemyState.Dead) return 1f;
            float scale = 1f;
            if (!FTT.Combat.StoryEnemyCombatRules.IsProrationExempt(hit.AttackClass, hit.Origin, hit.Delivery)) {
                // In the chain: in hitstun, or in the Armored Recovery the chain's
                // own poise trip opened (S3) — that window is the same exchange,
                // so its hits keep prorating instead of landing at full value.
                bool inChain = CurrentState == EnemyState.Stunned || IsArmoredRecovery;
                // A hit on a mob outside the chain opens a fresh one.
                if (!inChain) ResetConfirmChain();
                _confirmChainHits++;
                bool specialConfirm = hit.AttackClass == FTT.Combat.AttackClass.Special
                    && IsSpecialConfirm(hit.AttackID, inChain);
                scale *= FTT.Combat.StoryEnemyCombatRules.ConfirmProrationScale(_confirmChainHits, specialConfirm);
            }
            if (Data?.Tier == EnemyTier.Standard
                && FTT.Combat.StoryEnemyCombatRules.IsBasicsPacingHit(hit.AttackClass, hit.HitboxID)) {
                scale *= FTT.Combat.StoryEnemyCombatRules.StandardBasicDamageScale;
            }
            return scale;
        }

        /// <summary>
        /// S2: a Special is a confirm when its first hit in the current chain
        /// landed on hitstun (opened by another attack); every later hit of the
        /// same Special (same <c>AttackID</c>) in the chain keeps that answer. A
        /// multi-hit Special's own follow-ups therefore never prorate against the
        /// hitstun its own first hit applied, while a whole Special fired into a
        /// string is prorated hit for hit. An unidentified Special (no AttackID)
        /// is judged per hit. "Hitstun" here is the chain (<paramref name="inChain"/>):
        /// a Special whose first hit lands on the chain's Armored Recovery is a
        /// confirm too.
        /// </summary>
        private bool IsSpecialConfirm(string attackID, bool inChain) {
            if (string.IsNullOrEmpty(attackID)) return inChain;
            if (_chainSpecialConfirms.TryGetValue(attackID, out bool confirm)) return confirm;
            _chainSpecialConfirms[attackID] = inChain;
            return inChain;
        }

        /// <summary>S2: ends the uninterrupted stun chain (hit count and per-Special confirm record).</summary>
        private void ResetConfirmChain() {
            _confirmChainHits = 0;
            _chainSpecialConfirms.Clear();
        }

        /// <summary>
        /// The corpse's kill freeze: hit pose, red hit flash, frozen animation, and
        /// the release clock held until it ends (see the Dead branch of
        /// <see cref="_PhysicsProcess"/>). Die() has already resolved every gameplay
        /// consequence of the kill.
        /// </summary>
        private void BeginKillFreeze(int frames) {
            if (frames <= 0) return;
            _hitstopFramesRemaining = Math.Max(_hitstopFramesRemaining, frames);
            PlayAnimation("hitstun");
            if (_sprite != null) _sprite.SpeedScale = 0f;
            _glow?.FlashHit();
        }

        /// <summary>True while a killed mob holds its kill freeze. Test seam.</summary>
        public bool IsInKillFreeze => CurrentState == EnemyState.Dead && _hitstopFramesRemaining > 0;

        /// <summary>
        /// F9 (2026-10-04): a player-dealt hit that landed damage floats a damage
        /// number over the mob (FloatingDamageNumber already honours the
        /// damage-number visibility setting) and, for a direct hit, shakes the
        /// camera through the CameraShake autoload (which applies the
        /// accessibility screen-shake scale): an ability's authored
        /// <c>ScreenShakeIntensity</c>/<c>Duration</c> when the payload carries
        /// one, else the basics' damage-and-launch curve. Presentation only.
        /// </summary>
        private void PresentDealtHit(in FTT.Combat.HitPayload hit, int damageApplied) {
            if (damageApplied <= 0) return;
            if (!FTT.Combat.StoryEnemyCombatRules.IsPlayerDealt(hit.AttackerIndex, hit.AttackClass, hit.Delivery)) return;
            if (FTT.Combat.StoryEnemyCombatRules.DealtHitShakes(hit.Delivery, hit.ExemptFromHitstop)) {
                FTT.Core.CameraShake.Instance?.Shake(
                    FTT.Combat.StoryEnemyCombatRules.DealtHitShakeIntensity(
                        hit.AttackClass, hit.ScreenShakeIntensity, damageApplied, hit.Launches),
                    FTT.Combat.StoryEnemyCombatRules.DealtHitShakeDuration(
                        hit.AttackClass, hit.ScreenShakeIntensity, hit.ScreenShakeDuration, hit.Launches));
            }
            if (!IsInsideTree()) return;
            Vector2 numberPosition = GlobalPosition + new Vector2(
                0f, _bodyCenterY - _bodyHeight * 0.5f - FTT.Combat.StoryEnemyCombatRules.DamageNumberHeadClearancePixels);
            FTT.UI.FloatingDamageNumber.Show(damageApplied, numberPosition, GetParent());
        }

        /// <summary>
        /// V7.6 Time Freeze. Deliberately NOT
        /// <see cref="SetStoryRewindFrozen"/>: that path cancels the executor,
        /// zeroes velocity and deactivates the hitbox, which is correct when the
        /// world is about to be restored to a past state and catastrophic for a
        /// freeze, whose whole contract is "resume preserved positions,
        /// velocities, attack phases and remaining timers". This latches the same
        /// early-return flag and touches nothing else.
        /// </summary>
        public void SetTimeFrozen(bool frozen) => _rewindFrozen = frozen;

    }
}
