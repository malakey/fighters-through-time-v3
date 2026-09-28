using Godot;
using System;
using System.Collections.Generic;
using FTT.Core;
using FTT.Environment;

namespace FTT.Enemies {

    /// <summary>
    /// APPEND-ONLY. <c>HistoricalRecovery</c> was appended by Package 11 A7b for
    /// T01b's suspended self-rewind beat; it is deliberately distinct from
    /// <c>PhaseTransitioning</c>, which zeroes timers the contract requires preserved.
    /// </summary>
    public enum BossState { Idle, Chase, Attacking, PhaseTransitioning, RestWindow, Dead, HistoricalRecovery }

    /// <summary>
    /// Story-side boss runtime. Executes authored <see cref="EnemyAbilityData"/>
    /// through the shared <see cref="EnemyAbilityExecutor"/> with weighted random
    /// selection, distance filtering, phase gating, telegraph interruption, and
    /// knockback immunity. Story-only; nothing here reaches scripts/FighterSim.
    /// </summary>
    public partial class BossController : CharacterBody2D, IPoolable, IStoryRewindable, IStoryRewindSimulation, IStoryTimeFreezable, FTT.Combat.IDamageable {
        public const float DeathAnimationSeconds = 1.0f;
        private const float GravityPixelsPerSecond = 980f;
        private const float PixelsPerUnit = 60f;
        private const float RestTrackingSpeedFactor = 0.3f;

        private static int _spawnCounter;

        [Export] public BossData Data;
        /// <summary>Non-zero pins the attack-selection RNG for tests and replays.</summary>
        [Export] public ulong SelectionSeed;
        /// <summary>
        /// T01b's third and last destination fallback: "a stable authored safe
        /// anchor". Arena authoring must guarantee it is valid. Levels that never
        /// use the historical recovery leave it unset.
        /// </summary>
        [Export] public Node2D HistoricalRecoveryAnchor;
        [Export] public StoryRewindPolicy RewindPolicy { get; set; } = StoryRewindPolicy.PreserveCurrentState;

        public BossState CurrentState { get; private set; } = BossState.Idle;
        public int CurrentHP;
        public int CurrentPhase;

        private float _restTimer;
        // V7.1 hitstop freeze (BasicComboRules numbers); max-assigned, never
        // shortened. Counted in physics frames so it cannot drift against the
        // 60 Hz clock the shared frame tables are authored in.
        private int _hitstopFramesRemaining;
        private float _transitionTimer;
        private float _deathTimer;
        private int _reactionFramesRemaining;
        private bool _attackCommitted;
        private bool _restStandOffEngaged;
        private int _scaledMaxHP;
        private bool _facingRight = true;
        private bool _rewindFrozen;
        private bool _nodesResolved;
        private bool _eventsBound;
        private bool _spawnAnnounced;

        private AnimatedSprite2D _sprite;
        private FTT.Combat.GlowPresentationController _glow;
        private FTT.Combat.PresentationVisibilitySuspender _presentationSuspender;
        private FTT.Characters.PlayerController _target;
        private FTT.Combat.CombatantPushbox _pushbox;
        private FTT.Combat.Hurtbox _hurtbox;
        private FTT.Combat.Hitbox _attackHitbox;
        private Node2D _abilityOrigin;

        private EnemyAbilityExecutor _executor;
        private Random _rng;
        private readonly List<int> _selectionBuffer = new();

        private float _controlStatusTimer;
        private float _controlStatusIntensity = 1f;
        private float _damageStatusTimer;
        private float _damageStatusIntensity = 1f;
        private float _venomTickTimer;

        private Vector2 _spawnPosition;
        private Vector2 _checkpointPosition;
        private int _checkpointHP;
        private bool _checkpointCaptured;

        // === Package 11 A7b — T01b capped historical recovery ===

        /// <summary>One authoritative 60 Hz sample: where the boss stood and how
        /// much HP it had at the start of that tick, with its encounter-local ID.</summary>
        private readonly struct HistorySample {
            public readonly int Tick;
            public readonly Vector2 Position;
            public readonly int HP;
            public HistorySample(int tick, Vector2 position, int hp) {
                Tick = tick;
                Position = position;
                HP = hp;
            }
        }

        /// <summary>Allocated only for a boss that actually authors T01b.</summary>
        private HistorySample[] _history;
        private int _historyCount;
        private int _historyHead = -1;
        private int _historyTick = -1;
        private bool _historicalRecoveryUsed;
        private int _historicalRecoverySuspendFrames;
        private int _historicalRecoveryResolvedHP;
        private Vector2 _historicalRecoveryDestination;

        public StatusType ControlStatusType { get; private set; } = StatusType.None;
        public StatusType DamageStatusType { get; private set; } = StatusType.None;
        /// <summary>Control slot first, then damage — the compat view for single-status readers.</summary>
        public StatusType ActiveStatusType =>
            ControlStatusType != StatusType.None ? ControlStatusType : DamageStatusType;
        public bool HasStatusEffect(StatusType type) =>
            ControlStatusType == type || DamageStatusType == type;
        public float StatusMoveMultiplier { get; private set; } = 1f;
        public float StatusDamageTakenMultiplier { get; private set; } = 1f;

        // === V7.6 StatusSlots contract (IStatusEffectTarget), Package 11 A1 ===
        // Bosses carry the same two slots as players and ordinary enemies. Their
        // flinch immunity is unchanged and orthogonal: this controller has no
        // Stunned state and its hit intake still ignores HitPayload.HitstunDuration
        // outright, so a status can land on a boss without ever staggering it.

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

        /// <summary>Both slots. Death, rewind restore and Restart Level call this.</summary>
        public void ClearAllStatusEffects() => ClearStatusEffect();

        // === V7.6 F07 Conductive mark (caster-owned; NOT a status) ===

        private int _conductiveFramesRemaining;
        private int _conductiveSourcePlayerID = -1;

        public int ConductiveFramesRemaining => _conductiveFramesRemaining;
        public int ConductiveSourcePlayerID =>
            _conductiveFramesRemaining > 0 ? _conductiveSourcePlayerID : -1;
        public bool HasConductiveMark => _conductiveFramesRemaining > 0;
        public bool HasConductiveMarkFrom(int sourcePlayerID) =>
            _conductiveFramesRemaining > 0 && _conductiveSourcePlayerID == sourcePlayerID;

        public void ApplyConductiveMark(int sourcePlayerID, int frames) {
            if (frames <= 0 || CurrentState == BossState.Dead) return;
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

        /// <summary>Story difficulty-scaled maximum HP; canonical base stays in BossData.</summary>
        public int ScaledMaxHP => _scaledMaxHP > 0 ? _scaledMaxHP : Data?.MaxHP ?? 1;

        public bool IsStoryRewindFrozen => _rewindFrozen;
        public bool IsPhaseInvincible => CurrentState == BossState.PhaseTransitioning;
        public int SelectedAbilityIndex { get; private set; } = -1;
        public EnemyAbilityData SelectedAbility { get; private set; }
        public bool LastTelegraphInterrupted { get; private set; }
        public EnemyAbilityPhase AbilityPhase => Executor.Phase;
        public int ReactionFramesRemaining => _reactionFramesRemaining;

        /// <summary>V7 boss intro: the encounter controller reads these to cut
        /// the free intro telegraph off at its last wind-up frame.</summary>
        public bool IsTelegraphing => Executor.IsTelegraphing;
        public int AbilityFramesRemaining => Executor.FramesRemainingInPhase;
        public void CancelTelegraphIntoRecovery() => Executor.CancelIntoRecovery();

        private EnemyAbilityExecutor Executor => _executor ??= CreateExecutor();

        private EnemyAbilityExecutor CreateExecutor() {
            var executor = new EnemyAbilityExecutor(this) { Rng = ResolveRng() };
            executor.Bind(_sprite, _attackHitbox, _abilityOrigin);
            // Package 12 W9 (GAP-07): the two executor beats the phase mechanics key off.
            executor.MinionsSummoned += OnMinionsSummoned;
            executor.Teleported += OnTeleported;
            return executor;
        }

        private Random ResolveRng() =>
            _rng ??= new Random(SelectionSeed != 0
                ? unchecked((int)SelectionSeed)
                : unchecked(970417 + System.Threading.Interlocked.Increment(ref _spawnCounter)));

        public override void _Ready() {
            ResolveNodes();
            ApplyData(Data);
            _spawnPosition = GlobalPosition;
            CollisionLayer = CollisionLayers.Enemy;
            CollisionMask = CollisionLayers.EnemyBodyMask;
            BindEvents();
            FTT.Combat.VfxPresentationBinder.EnsureInstalled(this);
            AnnounceSpawn();
        }

        public override void _ExitTree() => UnbindEvents();

        private void ResolveNodes() {
            if (_nodesResolved) return;
            _nodesResolved = true;
            _sprite = GetNodeOrNull<AnimatedSprite2D>("Presentation/AnimatedSprite2D")
                ?? GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
            _pushbox = GetNodeOrNull<FTT.Combat.CombatantPushbox>("Pushbox");
            _hurtbox = GetNodeOrNull<FTT.Combat.Hurtbox>("Hurtbox");
            if (_hurtbox != null) _hurtbox.OwnerPlayerIndex = -1;
            _attackHitbox = GetNodeOrNull<FTT.Combat.Hitbox>("Hitbox")
                ?? GetNodeOrNull<FTT.Combat.Hitbox>("AttackHitbox");
            _abilityOrigin = GetNodeOrNull<Node2D>("AbilityOrigin");
            Executor.Bind(_sprite, _attackHitbox, _abilityOrigin);
            if (_sprite != null) {
                _glow = FTT.Combat.GlowPresentationController.AttachTo(
                    this, _sprite, ownerPlayerIndex: -1, subscribeToStoryEvents: false);
                Executor.Glow = _glow;
                _presentationSuspender = FTT.Combat.PresentationVisibilitySuspender.AttachTo(this, _sprite);
                FTT.Combat.RetroSpriteScaleNormalizer.Attach(
                    _sprite, FTT.Combat.RetroSpriteScaleNormalizer.FigureKind.Boss);
            }
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

        /// <summary>Assigns canonical data and rebuilds derived runtime values.</summary>
        public void ApplyData(BossData data) {
            Data = data;
            ResolveNodes();
            Difficulty difficulty = StoryDifficultyTuning.CurrentStoryDifficulty;
            // M19: a squad member stands on its even share of the authored pool.
            int authoredHP = Data == null ? 0 : IsSquadMember ? Data.MemberMaxHP : Data.MaxHP;
            _scaledMaxHP = Data != null ? StoryDifficultyTuning.ScaleEnemyHP(authoredHP, difficulty) : 0;
            CurrentHP = ScaledMaxHP;
            CurrentPhase = 0;
            Executor.SourceID = Data?.BossID ?? "";
            Executor.DamageMultiplier = StoryDifficultyTuning.GetEnemyDamageMultiplier(difficulty);
            // V7.5 Borrowed Legacies + T01b: the composite kit and the encounter's
            // history ring are both rebuilt from the (possibly new) BossData.
            BuildAbilityKit();
            ResetHistoricalRecoveryState();
            if (_sprite != null) {
                bool hasAuthoredFrames = Data?.SpriteFramesResource != null;
                if (hasAuthoredFrames) _sprite.SpriteFrames = Data.SpriteFramesResource;
                Color tint = hasAuthoredFrames ? Colors.White : Data?.PlaceholderTint ?? Colors.White;
                if (tint.A <= 0f) tint = Colors.White;
                Executor.SetBaseModulate(tint);
                if (_glow == null) _sprite.Modulate = tint;
                _glow?.ClearAllStates();
                PlayAnimation("idle");
            }
            if (_attackHitbox != null) {
                _attackHitbox.OwnerPlayerIndex = -1;
                _attackHitbox.Damage = StoryDifficultyTuning.ScaleEnemyDamage(Data?.AttackDamage ?? 0f, difficulty);
                _attackHitbox.KnockbackForce = new Vector2(Data?.AttackKnockback ?? 4f, -1.5f);
            }
        }

        private void AnnounceSpawn() {
            if (_spawnAnnounced) return;
            _spawnAnnounced = true;
            EventBus.Instance?.RaiseBossSpawned(new BossSpawnedPayload {
                BossID = Data?.BossID ?? "",
                DisplayNameKey = Data?.DisplayNameKey ?? "",
                CurrentHP = CurrentHP,
                MaxHP = ScaledMaxHP,
                Position = GlobalPosition
            });
            RaiseHPChanged();
        }

        public override void _PhysicsProcess(double delta) {
            if (_rewindFrozen) return;
            float dt = (float)delta;

            if (CurrentState == BossState.Dead) {
                ProcessDead(dt);
                return;
            }

            // T01b: the suspended rewind beat runs before every other clock on this
            // controller, so statuses, the Conductive mark, ability cooldowns, an
            // in-flight hitstop and the executor all hold their remaining values.
            if (CurrentState == BossState.HistoricalRecovery) {
                ProcessHistoricalRecovery();
                return;
            }

            // V7.1 hitstop: the boss freezes for the shared window like every
            // other combatant (its knockback stays unscaled — bosses do not
            // fly — but the hit-weight freeze is universal).
            if (_hitstopFramesRemaining > 0) {
                _hitstopFramesRemaining--;
                if (_sprite != null) _sprite.SpeedScale = 0f;
                if (_hitstopFramesRemaining <= 0 && _sprite != null) _sprite.SpeedScale = 1f;
                return;
            }

            // T01b: one history sample per authoritative simulation tick, taken at
            // tick start before movement, ability and damage resolution. A frozen or
            // hitstopped tick advances no gameplay clock and records nothing.
            RecordHistorySample();

            TickStatus(dt);
            // F07: the Conductive mark is not a status — it ticks on its own
            // frame counter and locks nothing.
            TickConductiveMark();
            TickAbilityCooldowns(dt);
            if (CurrentState == BossState.Dead) return;

            if (_target == null || !IsInstanceValid(_target)) _target = FindNearestPlayer();
            Executor.Tick(dt);
            // Package 12 W9: the guarded scene's clock and cast list, the
            // invulnerability presentation, and M18's decorative after-images.
            TickGuardedScene(dt);
            RefreshGuardPresentation();
            TickAfterImages();

            switch (CurrentState) {
                case BossState.Idle:
                    PlayAnimation("idle");
                    if (_target != null) CurrentState = BossState.Chase;
                    break;
                case BossState.Chase: ProcessChase(dt); break;
                case BossState.Attacking: ProcessAttacking(dt); break;
                case BossState.RestWindow: ProcessRest(dt); break;
                case BossState.PhaseTransitioning: ProcessPhaseTransition(dt); break;
            }

            ApplyGravity(dt);
            MoveAndSlide();
            _pushbox?.ResolveStoryOverlaps();
        }

        private void ApplyGravity(float dt) {
            if (!IsOnFloor()) Velocity = new Vector2(Velocity.X, Velocity.Y + GravityPixelsPerSecond * dt);
        }

        private float MoveSpeedPixels =>
            (Data?.MoveSpeed ?? 4f) * PixelsPerUnit * StatusMoveMultiplier
            * (Data?.GetPhaseSpeedMultiplier(CurrentPhase) ?? 1f);

        /// <summary>
        /// ChargeDash travel obeys the same status multiplier as ordinary movement:
        /// a Rooted boss stays pinned and TimeDilation slows the crossing, instead
        /// of the raw executor velocity covering the arena at full speed (audit Low).
        /// </summary>
        public float StatusScaledDashVelocityX => Executor.DashVelocity.X * StatusMoveMultiplier;

        private float EngagementRangePixels =>
            Mathf.Max(Data?.MeleeRangeThreshold ?? 3f, Data?.RangedRangeThreshold ?? 8f) * PixelsPerUnit;

        /// <summary>
        /// The authored melee band — the boss's shortest-range attack distance.
        /// Single source for both attack selection's melee filter and the
        /// rest-window tracking stand-off.
        /// </summary>
        private float MeleeBandPixels => (Data?.MeleeRangeThreshold ?? 3f) * PixelsPerUnit;

        /// <summary>
        /// Deceleration bringing the rest-window tracking speed to rest across
        /// <see cref="EnemyController.StandOffDecelerationFrames"/> frames, from the
        /// unscaled authored speed so a status slow can never stall the stop-out.
        /// </summary>
        private float RestStandOffDecelerationPerSecond =>
            (Data?.MoveSpeed ?? 4f) * PixelsPerUnit * RestTrackingSpeedFactor
            * 60f / EnemyController.StandOffDecelerationFrames;

        private void ProcessChase(float dt) {
            if (_target == null) {
                Velocity = new Vector2(0f, Velocity.Y);
                PlayAnimation("idle");
                return;
            }
            float dist = GlobalPosition.DistanceTo(_target.GlobalPosition);
            SetFacing(_target.GlobalPosition.X >= GlobalPosition.X);
            // GAP-07 Tragedy King: mid-soliloquy he performs, he does not fight —
            // the actors carry the scene until they bow.
            if (IsSoliloquy) {
                Velocity = new Vector2(0f, Velocity.Y);
                PlayAnimation("idle");
                return;
            }
            if (dist <= EngagementRangePixels) {
                EnterAttacking();
                return;
            }
            float sign = _facingRight ? 1f : -1f;
            Velocity = new Vector2(sign * MoveSpeedPixels, Velocity.Y);
            PlayAnimation("move");
        }

        private void EnterAttacking() {
            CurrentState = BossState.Attacking;
            Velocity = new Vector2(0f, Velocity.Y);
            _attackCommitted = false;
            LastTelegraphInterrupted = false;
            int min = Math.Max(0, Data?.ReactionDelayMinFrames ?? 4);
            int max = Math.Max(min, Data?.ReactionDelayMaxFrames ?? min);
            int rolled = min == max ? min : ResolveRng().Next(min, max + 1);
            _reactionFramesRemaining = StoryDifficultyTuning.ScaleReactionDelayFrames(
                rolled, StoryDifficultyTuning.CurrentStoryDifficulty);
        }

        private void ProcessAttacking(float dt) {
            Velocity = new Vector2(0f, Velocity.Y);
            if (_reactionFramesRemaining > 0) {
                _reactionFramesRemaining--;
                if (_target != null) SetFacing(_target.GlobalPosition.X >= GlobalPosition.X);
                return;
            }

            if (!_attackCommitted) {
                _attackCommitted = true;
                SelectAndExecuteAttack();
                return;
            }

            if (Executor.Phase == EnemyAbilityPhase.Active &&
                Executor.ActiveAbility?.Archetype == EnemyAbilityArchetype.ChargeDash) {
                Velocity = new Vector2(StatusScaledDashVelocityX, Velocity.Y);
            }

            if (!Executor.IsBusy) {
                _attackCommitted = false;
                _restTimer = Mathf.Max(0f, Data?.RestCooldown ?? 1.5f);
                CurrentState = BossState.RestWindow;
            }
        }

        private void ProcessRest(float dt) {
            _restTimer -= dt;
            TrackPlayer(dt);
            if (_restTimer <= 0f) CurrentState = BossState.Chase;
        }

        private void ProcessPhaseTransition(float dt) {
            Velocity = new Vector2(0f, Velocity.Y);
            _transitionTimer -= dt;
            if (_transitionTimer > 0f) return;
            _transitionTimer = 0f;
            // design Section 6: the rest cooldown is bypassed after a transition.
            _restTimer = 0f;
            CurrentState = BossState.Chase;
            // GAP-07: a phase whose entry opens a guarded scene casts it the moment
            // the transition window closes (the Tragedy King summons his actors).
            if (_phaseEntrySummonPending) {
                _phaseEntrySummonPending = false;
                BeginPhaseEntrySummon();
            }
        }

        private void ProcessDead(float dt) {
            Velocity = Vector2.Zero;
            if (_deathTimer <= 0f) return;
            _deathTimer = Mathf.Max(0f, _deathTimer - dt);
        }

        private void TrackPlayer(float dt) {
            if (_target == null) {
                Velocity = new Vector2(0f, Velocity.Y);
                return;
            }
            float sign = Mathf.Sign(_target.GlobalPosition.X - GlobalPosition.X);
            SetFacing(sign >= 0f);

            // Rest-window stand-off keyed to the boss's shortest-range attack (the
            // authored melee band): tracking repositions at range but must not
            // press into the player's pushbox while the boss waits out its rest.
            // Chase needs no equivalent — it already stops at EngagementRangePixels,
            // the full authored band. Same engage/release hysteresis as EnemyController.
            float dist = GlobalPosition.DistanceTo(_target.GlobalPosition);
            if (dist <= MeleeBandPixels * EnemyController.StandOffEngageFraction) {
                _restStandOffEngaged = true;
            } else if (dist > MeleeBandPixels * EnemyController.StandOffResumeFraction) {
                _restStandOffEngaged = false;
            }

            if (_restStandOffEngaged) {
                Velocity = new Vector2(
                    Mathf.MoveToward(Velocity.X, 0f, RestStandOffDecelerationPerSecond * dt),
                    Velocity.Y);
                PlayAnimation("idle");
                return;
            }

            Velocity = new Vector2(sign * MoveSpeedPixels * RestTrackingSpeedFactor, Velocity.Y);
            PlayAnimation("move");
        }

        // === Package 11 A7b — V7.5 Borrowed Legacies composite kit ===
        //
        // A boss kit is normally the static authored BossData.BossAbilities array.
        // A boss that authors BorrowsRosterLegacies gets a RUNTIME COMPOSITE: the
        // authored array, followed by one projected ability per roster character the
        // player did not pick (BorrowedLegacies, plan §2.5). Everything downstream —
        // selection, phase gating, cooldowns, execution — reads the composite through
        // ActiveAbilityArray / GetActiveAbilityMinPhase, so there is exactly one kit
        // concept rather than two parallel selection paths.
        //
        // The borrowed entries are gated to the boss's FINAL phase through the
        // existing AbilityMinPhase mechanism (phase 2 for the First Unbound's
        // [0.66, 0.33] thresholds = P3), and they all carry the default
        // SelectionWeight of 1, so the existing weighted roll is weighted-uniform
        // across the projected set exactly as §2.5 requires.

        private EnemyAbilityData[] _activeAbilities;
        private int[] _activeMinPhase;
        private int _borrowedStartIndex = -1;
        private string _borrowedForHeroID;
        private bool _borrowedKitBuilt;
        private EnemyAbilityData[] _borrowedCache = System.Array.Empty<EnemyAbilityData>();

        /// <summary>The kit actually in play: authored, or authored + borrowed.</summary>
        private EnemyAbilityData[] ActiveAbilityArray =>
            _activeAbilities ?? Data?.BossAbilities ?? System.Array.Empty<EnemyAbilityData>();

        /// <summary>Read-only view of the live kit for tests and encounter logic.</summary>
        public System.Collections.Generic.IReadOnlyList<EnemyAbilityData> ActiveAbilities =>
            ActiveAbilityArray;

        /// <summary>How many entries of <see cref="ActiveAbilities"/> are borrowed.</summary>
        public int BorrowedAbilityCount =>
            _borrowedStartIndex < 0 ? 0 : ActiveAbilityArray.Length - _borrowedStartIndex;

        /// <summary>Index of the first borrowed ability, or -1 when none were projected.</summary>
        public int BorrowedAbilityStartIndex => _borrowedStartIndex;

        /// <summary>Phase gate for a composite index; falls back to the authored table.</summary>
        public int GetActiveAbilityMinPhase(int index) {
            if (_activeMinPhase != null && index >= 0 && index < _activeMinPhase.Length) {
                return Math.Max(0, _activeMinPhase[index]);
            }
            return Data?.GetAbilityMinPhase(index) ?? 0;
        }

        /// <summary>
        /// Builds the composite kit for the session's locked hero. Cheap and
        /// idempotent: a rebuild for the same hero is skipped, so a pooled boss
        /// respawning does not re-project the whole roster every cycle.
        /// </summary>
        private void BuildAbilityKit() {
            // V7.5 Borrowed Legacies: the projection is cached per hero, so a pooled
            // boss respawning does not re-project the whole roster every cycle.
            EnemyAbilityData[] borrowed = System.Array.Empty<EnemyAbilityData>();
            if (Data?.BorrowsRosterLegacies == true) {
                string heroID = GameManager.Instance?.CurrentSession.SelectedCharacterID ?? "";
                if (!_borrowedKitBuilt || _borrowedForHeroID != heroID) {
                    _borrowedForHeroID = heroID;
                    _borrowedKitBuilt = true;
                    _borrowedCache = BorrowedLegacies.ProjectFor(heroID);
                }
                borrowed = _borrowedCache;
            } else {
                _borrowedForHeroID = null;
                _borrowedKitBuilt = false;
                _borrowedCache = System.Array.Empty<EnemyAbilityData>();
            }

            // M19: a squad member fights with its own abilities plus the shared
            // ones, and later with any fallen member's it has absorbed.
            bool squadFiltered = IsSquadMember;
            if (!squadFiltered && borrowed.Length == 0) {
                _activeAbilities = null;
                _activeMinPhase = null;
                _borrowedStartIndex = -1;
                _abilityCooldownTimers = null;
                return;
            }

            // Cooldowns follow the ability, not its composite index, so an absorb
            // mid-fight never refunds or re-arms anything.
            Dictionary<EnemyAbilityData, float> carriedCooldowns = CaptureCooldownsByAbility();

            EnemyAbilityData[] authored = Data?.BossAbilities ?? System.Array.Empty<EnemyAbilityData>();
            var abilities = new List<EnemyAbilityData>(authored.Length + borrowed.Length);
            var minPhase = new List<int>(authored.Length + borrowed.Length);
            for (int index = 0; index < authored.Length; index++) {
                if (squadFiltered && !MemberOwnsAbility(index)) continue;
                abilities.Add(authored[index]);
                minPhase.Add(Data.GetAbilityMinPhase(index));
            }
            if (borrowed.Length > 0) {
                // The final phase is derived from the authored thresholds, never a
                // second authored number: [0.66, 0.33] => PhaseCount 3 => phase index 2.
                int finalPhase = Math.Max(0, Data.PhaseCount - 1);
                _borrowedStartIndex = abilities.Count;
                foreach (EnemyAbilityData projected in borrowed) {
                    abilities.Add(projected);
                    minPhase.Add(finalPhase);
                }
            } else {
                _borrowedStartIndex = -1;
            }
            _activeAbilities = abilities.ToArray();
            _activeMinPhase = minPhase.ToArray();
            _abilityCooldownTimers = null;
            RestoreCooldownsByAbility(carriedCooldowns);
        }

        /// <summary>M19: does this squad member field the authored ability at <paramref name="authoredIndex"/>?</summary>
        private bool MemberOwnsAbility(int authoredIndex) {
            int owner = Data?.GetAbilityMember(authoredIndex) ?? -1;
            return owner < 0 || owner == SquadMemberIndex || _absorbedMembers.Contains(owner);
        }

        private Dictionary<EnemyAbilityData, float> CaptureCooldownsByAbility() {
            var carried = new Dictionary<EnemyAbilityData, float>();
            if (_abilityCooldownTimers == null) return carried;
            EnemyAbilityData[] current = ActiveAbilityArray;
            for (int index = 0; index < _abilityCooldownTimers.Length && index < current.Length; index++) {
                if (current[index] != null && _abilityCooldownTimers[index] > 0f) {
                    carried[current[index]] = _abilityCooldownTimers[index];
                }
            }
            return carried;
        }

        private void RestoreCooldownsByAbility(Dictionary<EnemyAbilityData, float> carried) {
            if (carried == null || carried.Count == 0) return;
            EnemyAbilityData[] current = ActiveAbilityArray;
            _abilityCooldownTimers = new float[current.Length];
            for (int index = 0; index < current.Length; index++) {
                if (current[index] != null && carried.TryGetValue(current[index], out float remaining)) {
                    _abilityCooldownTimers[index] = remaining;
                }
            }
        }

        // === Package 12 W9 (M19) — boss squads ===============================
        //
        // A BossData with SquadMemberCount > 1 is fought as several bodies that
        // share one boss: the BossEncounterController spawns one BossController
        // per member, each on MemberMaxHP (MaxHP split evenly), each fielding its
        // own AbilityMember-owned abilities plus the shared ones. A member does not
        // raise the global defeat payload on its own (the encounter raises one for
        // the whole squad, so the 25-dust award is paid once); when a member falls
        // the encounter hands its abilities to the survivors (AbsorbSquadMember)
        // and advances them into the next phase (EnterPhase) — the
        // BossPhaseTrigger.MemberDefeat rule.

        /// <summary>Member index inside a squad encounter; -1 for an ordinary boss.</summary>
        public int SquadMemberIndex { get; set; } = -1;

        /// <summary>True for one body of a <see cref="BossData.IsSquad"/> boss.</summary>
        public bool IsSquadMember => SquadMemberIndex >= 0 && Data?.IsSquad == true;

        private readonly HashSet<int> _absorbedMembers = new();

        /// <summary>Fallen members whose abilities this body now fields.</summary>
        public IReadOnlyCollection<int> AbsorbedSquadMembers => _absorbedMembers;

        /// <summary>
        /// M19: "the survivor absorbs the fallen's ability set". Rebuilds the live
        /// kit to include every ability the fallen member owned; cooldowns already
        /// running stay running.
        /// </summary>
        public void AbsorbSquadMember(int memberIndex) {
            if (memberIndex < 0 || memberIndex == SquadMemberIndex) return;
            if (!_absorbedMembers.Add(memberIndex)) return;
            BuildAbilityKit();
        }

        /// <summary>
        /// When false, <see cref="Die"/> raises only <see cref="Died"/>, not the
        /// EventBus defeat payload — a squad member, whose encounter raises one
        /// payload for the whole squad.
        /// </summary>
        public bool RaisesDefeatPayload { get; set; } = true;

        /// <summary>Raised once from <see cref="Die"/>, for every body.</summary>
        public event Action<BossController> Died;

        /// <summary>
        /// Raised the frame this body enters a new phase, whatever triggered it
        /// (an HP threshold, a squad member's defeat, or <see cref="EnterPhase"/>).
        /// Level arenas hang their phase mechanics off this (Package 12 W9).
        /// </summary>
        public event Action<BossController, int> PhaseEntered;

        // === Attack selection ===

        // Per-ability cooldown timers, indexed like ActiveAbilityArray. The
        // authored EnemyAbilityData.CooldownSeconds was previously never read
        // (design §6: "the data exists; the algorithm must read it") — an
        // ability on cooldown is excluded from the weighted roll, which is what
        // prevents a summon or a screen-wide ability from chaining back-to-back.
        private float[] _abilityCooldownTimers;

        private void TickAbilityCooldowns(float dt) {
            if (_abilityCooldownTimers == null) return;
            for (int index = 0; index < _abilityCooldownTimers.Length; index++) {
                if (_abilityCooldownTimers[index] > 0f) _abilityCooldownTimers[index] -= dt;
            }
        }

        /// <summary>Arms the authored cooldown for the ability at <paramref name="index"/>.</summary>
        private void ArmAbilityCooldown(int index) {
            EnemyAbilityData[] abilities = ActiveAbilityArray;
            if (abilities == null || index < 0 || index >= abilities.Length) return;
            _abilityCooldownTimers ??= new float[abilities.Length];
            if (_abilityCooldownTimers.Length < abilities.Length) {
                System.Array.Resize(ref _abilityCooldownTimers, abilities.Length);
            }
            _abilityCooldownTimers[index] = Mathf.Max(0f, abilities[index]?.CooldownSeconds ?? 0f);
        }

        private bool AbilityOnCooldown(int index) =>
            _abilityCooldownTimers != null
            && index < _abilityCooldownTimers.Length
            && _abilityCooldownTimers[index] > 0f;

        /// <summary>
        /// Weighted random over the phase-unlocked, off-cooldown,
        /// distance-appropriate abilities. If distance filtering leaves nothing
        /// selectable the full unlocked set is used instead: a boss must never
        /// deadlock with no valid attack. If every unlocked ability is cooling,
        /// selection returns -1 and the boss takes a rest window — a beat of
        /// downtime, never a repeat cast.
        /// </summary>
        public int SelectAbilityIndex(float distancePixels) {
            EnemyAbilityData[] abilities = ActiveAbilityArray;
            if (abilities == null || abilities.Length == 0) return -1;

            _selectionBuffer.Clear();
            var unlocked = new List<int>();
            bool inMelee = distancePixels <= MeleeBandPixels;
            for (int index = 0; index < abilities.Length; index++) {
                EnemyAbilityData ability = abilities[index];
                if (ability == null) continue;
                if (CurrentPhase < GetActiveAbilityMinPhase(index)) continue;
                if (AbilityOnCooldown(index)) continue;
                unlocked.Add(index);
                if (Data.AttackPattern != BossAttackPattern.DistanceBased) {
                    _selectionBuffer.Add(index);
                    continue;
                }
                bool matches = ability.RangeClass == EnemyAbilityRangeClass.Any
                    || (inMelee && ability.RangeClass == EnemyAbilityRangeClass.Melee)
                    || (!inMelee && ability.RangeClass == EnemyAbilityRangeClass.Ranged);
                if (matches) _selectionBuffer.Add(index);
            }

            // No-deadlock rule: an empty distance filter falls back to everything unlocked.
            List<int> candidates = _selectionBuffer.Count > 0 ? _selectionBuffer : unlocked;
            if (candidates.Count == 0) return -1;

            float totalWeight = 0f;
            foreach (int index in candidates) totalWeight += Mathf.Max(0f, abilities[index].SelectionWeight);
            if (totalWeight <= 0f) return candidates[ResolveRng().Next(candidates.Count)];

            float roll = (float)ResolveRng().NextDouble() * totalWeight;
            float cumulative = 0f;
            foreach (int index in candidates) {
                cumulative += Mathf.Max(0f, abilities[index].SelectionWeight);
                if (roll <= cumulative) return index;
            }
            return candidates[candidates.Count - 1];
        }

        /// <summary>Commits one authored ability immediately through the executor.</summary>
        public bool BeginAbility(EnemyAbilityData ability, Vector2 targetPosition) {
            SelectedAbility = ability;
            LastTelegraphInterrupted = false;
            // V7.2 classification: bosses forward their authored flags — the
            // only tier where unblockable (red telegraph) is honored.
            return Executor.Begin(ability, targetPosition, _facingRight,
                guardCrush: ability?.IsGuardCrushing ?? false,
                unblockable: ability?.IsUnblockable ?? false);
        }

        /// <summary>Advances the ability executor one 60 Hz frame (state machine and tests).</summary>
        public void TickAbility(float delta) => Executor.Tick(delta);

        private void SelectAndExecuteAttack() {
            float distance = _target != null ? GlobalPosition.DistanceTo(_target.GlobalPosition) : 0f;
            SelectedAbilityIndex = SelectAbilityIndex(distance);
            if (SelectedAbilityIndex < 0) {
                SelectedAbility = null;
                _attackCommitted = false;
                _restTimer = Mathf.Max(0.25f, Data?.RestCooldown ?? 1.5f);
                CurrentState = BossState.RestWindow;
                return;
            }

            SelectedAbility = ActiveAbilityArray[SelectedAbilityIndex];
            ArmAbilityCooldown(SelectedAbilityIndex);
            Vector2 targetPosition = _target?.GlobalPosition
                ?? GlobalPosition + new Vector2(_facingRight ? 200f : -200f, 0f);
            Executor.Begin(SelectedAbility, targetPosition, _facingRight,
                guardCrush: SelectedAbility?.IsGuardCrushing ?? false,
                unblockable: SelectedAbility?.IsUnblockable ?? false);
            PlayAnimation(SelectedAbility.SpawnsProjectiles ? "ranged_attack" : "melee_attack");
        }

        // === Damage, phases, death ===

        public void TakeDamage(int damage) => ApplyBossDamage(damage);

        /// <summary>
        /// M08 (Package 12 W3) <see cref="FTT.Combat.IDamageable"/>: the full hit
        /// pipeline the hurtbox feeds, reachable without a hurtbox in hand.
        /// </summary>
        public float TakeDamage(in FTT.Combat.HitPayload hit) => OnHurtboxHit(hit);

        /// <inheritdoc/>
        public bool IsAlive => CurrentState != BossState.Dead;

        /// <summary>
        /// V7.1 hitstop: freezes the boss's gameplay clock for the given frames
        /// (max-assign — an active freeze is never shortened). Dead bosses skip.
        /// </summary>
        public void ApplyHitstop(int frames) {
            if (frames <= 0 || CurrentState == BossState.Dead) return;
            if (frames > _hitstopFramesRemaining) _hitstopFramesRemaining = frames;
        }

        private int ApplyBossDamage(int damage) {
            // T01b: during the suspended rewind beat "player, boss, other actors,
            // projectiles, constructs, hazards and platforms cannot act or deal
            // damage", so the boss refuses incoming damage exactly as it does
            // during a phase-transition window.
            if (CurrentState == BossState.Dead
                || CurrentState == BossState.PhaseTransitioning
                || CurrentState == BossState.HistoricalRecovery) return 0;
            // Package 12 W9 (GAP-07): shielded by live arena guardians (the
            // Inventor's coils) or mid-soliloquy (the Tragedy King's actors).
            if (IsGuarded || IsSoliloquy) return 0;

            float incoming = Math.Max(0, damage) * StatusDamageTakenMultiplier;
            int applied = Math.Max(0, (int)MathF.Round(incoming));
            int previousHP = CurrentHP;
            CurrentHP = Math.Max(0, CurrentHP - applied);
            int damageApplied = previousHP - CurrentHP;
            if (damageApplied > 0) RaiseHPChanged();

            if (Data?.InterruptibleDuringTelegraph == true
                && Executor.IsTelegraphing
                && damageApplied >= Mathf.Max(0f, Data.InterruptDamageThreshold)) {
                Executor.CancelIntoRecovery();
                LastTelegraphInterrupted = true;
            }

            if (CurrentHP <= 0) {
                Die();
                return damageApplied;
            }
            if (damageApplied > 0) _glow?.FlashHit();
            CheckPhaseTransition();
            return damageApplied;
        }

        /// <summary>
        /// Advances through every threshold the hit crossed (a single big hit can
        /// skip a whole phase's worth of HP) while entering exactly one transition
        /// window and raising exactly one event per phase actually entered.
        /// </summary>
        private void CheckPhaseTransition() {
            // M19: a MemberDefeat squad never advances on HP — its thresholds only
            // size PhaseCount and notch the shared bar. The encounter calls
            // EnterPhase when a member falls.
            if (Data?.PhaseTrigger == BossPhaseTrigger.MemberDefeat) return;
            float[] thresholds = Data?.PhaseThresholds;
            if (thresholds == null || thresholds.Length == 0) return;
            float hpPercent = ScaledMaxHP > 0 ? (float)CurrentHP / ScaledMaxHP : 0f;

            int previousPhase = CurrentPhase;
            int crossed = 0;
            while (CurrentPhase < thresholds.Length && hpPercent <= thresholds[CurrentPhase]) {
                CurrentPhase++;
                crossed++;
                AnnouncePhaseEntered(CurrentPhase);
            }
            if (crossed == 0) return;

            // Shared "normal transition cleanup" — the current attack is cancelled
            // and its attached hitboxes removed. Deliberately NOT touching
            // _abilityCooldownTimers: T01b requires "do not reset spent resources or
            // remaining cooldowns", and the phase path never reset them either.
            CancelForTransition();

            // T01b: the boss's own capped historical recovery replaces the ordinary
            // invincibility window on the FIRST threshold crossing only. Phase
            // progression has already been latched above, including a Phase 3
            // crossing the same nonlethal hit produced — healing cannot erase it,
            // because the loop reads CurrentPhase, never the healed HP.
            if (previousPhase == 0 && CanBeginHistoricalRecovery()) {
                BeginHistoricalRecovery();
                return;
            }

            BeginTransitionWindow();
        }

        /// <summary>
        /// M19 <see cref="BossPhaseTrigger.MemberDefeat"/> (and any scripted phase
        /// change): enters <paramref name="phase"/> directly, with the ordinary
        /// transition window and cleanup. Never regresses; one call enters one
        /// phase. Returns false when refused (dead, not a later phase, or out of
        /// range).
        /// </summary>
        public bool EnterPhase(int phase) {
            if (Data == null || CurrentState == BossState.Dead) return false;
            if (phase <= CurrentPhase || phase >= Data.PhaseCount) return false;
            CurrentPhase = phase;
            AnnouncePhaseEntered(CurrentPhase);
            CancelForTransition();
            BeginTransitionWindow();
            return true;
        }

        private void CancelForTransition() {
            Executor.Cancel();
            _attackHitbox?.Deactivate();
            _attackCommitted = false;
            _reactionFramesRemaining = 0;
        }

        private void BeginTransitionWindow() {
            _transitionTimer = Mathf.Max(0f, Data.PhaseTransitionInvincibilityDuration);
            CurrentState = BossState.PhaseTransitioning;
            PlayAnimation("phase_transition");
        }

        /// <summary>One phase entered: the global event, the local event, and the
        /// boss-owned phase mechanics that arm on entry.</summary>
        private void AnnouncePhaseEntered(int phase) {
            EventBus.Instance?.RaiseBossPhaseChanged(phase);
            if (Data != null && Data.GuardedSummonMinPhase >= 0 && phase == Data.GuardedSummonMinPhase) {
                _phaseEntrySummonPending = true;
            }
            PhaseEntered?.Invoke(this, phase);
        }

        // === Package 11 A7b — T01b Option A: capped historical recovery ===
        //
        // The First Unbound's Phase 2 self-rewind
        // (docs/design-contracts/TEMPORAL_STATE_CONTRACT.md). Once per encounter, on
        // the first NONLETHAL crossing of the authored 66% threshold, the boss
        // rewinds its own position 180 ticks and recovers toward the HP it had then,
        // capped at 20% of its difficulty-scaled maximum. A lethal hit wins: Die()
        // runs before CheckPhaseTransition is ever reached, so this cannot resurrect
        // the boss.

        /// <summary>True for a boss whose <see cref="BossData"/> authors T01b.</summary>
        public bool HasHistoricalRecovery => Data?.HasHistoricalRecovery == true;

        /// <summary>Latched the moment the recovery commits; never re-armed.</summary>
        public bool HistoricalRecoveryUsed => _historicalRecoveryUsed;

        /// <summary>Combat is suspended for the recovery presentation.</summary>
        public bool IsHistoricalRecoverySuspended => CurrentState == BossState.HistoricalRecovery;

        public int HistoricalRecoverySuspendFramesRemaining => _historicalRecoverySuspendFrames;

        /// <summary>Consecutive authoritative samples currently retained (max 181).</summary>
        public int HistorySampleCount => _historyCount;

        /// <summary>The destination the fallback chain settled on.</summary>
        public Vector2 HistoricalRecoveryDestination => _historicalRecoveryDestination;

        /// <summary>True when the historical coordinate failed validation.</summary>
        public bool HistoricalRecoveryUsedFallbackDestination { get; private set; }

        /// <summary>
        /// True when none of the three tiers validated. The contract's answer is to
        /// "retain the pending transition and report the invalid arena configuration
        /// without spawning into danger" — the heal and the once-only event still
        /// resolve; only the relocation is abandoned.
        /// </summary>
        public bool HistoricalRecoveryDestinationInvalid { get; private set; }

        /// <summary>
        /// Destination-clearance predicate. The default is a real collision-shape
        /// query against the current arena; a level may install a stricter one
        /// (kill regions, arena bounds, authored locomotion support), and tests
        /// drive the three-tier fallback chain through it.
        /// </summary>
        public Func<Vector2, bool> HistoricalRecoveryDestinationValidator { get; set; }

        /// <summary>
        /// Encounter-local reset. A newly constructed or newly reconstructed
        /// encounter "starts new encounter-local history/phase flags"; a live player
        /// Death Rewind deliberately does <b>not</b> call this, so it can never
        /// re-arm a consumed recovery.
        /// </summary>
        private void ResetHistoricalRecoveryState() {
            _historicalRecoveryUsed = false;
            _historicalRecoverySuspendFrames = 0;
            _historicalRecoveryResolvedHP = 0;
            _historicalRecoveryDestination = GlobalPosition;
            HistoricalRecoveryUsedFallbackDestination = false;
            HistoricalRecoveryDestinationInvalid = false;
            ResetHistory();
        }

        private bool CanBeginHistoricalRecovery() =>
            HasHistoricalRecovery
            && !_historicalRecoveryUsed
            && CurrentState != BossState.Dead
            && CurrentHP > 0;

        /// <summary>
        /// Seeds one <b>actual</b> sample at combat start — never fabricated
        /// pre-fight history — and drops anything an earlier encounter left behind.
        /// </summary>
        private void ResetHistory() {
            _historyCount = 0;
            _historyHead = -1;
            _historyTick = -1;
            if (!HasHistoricalRecovery) {
                _history = null;
                return;
            }
            _history ??= new HistorySample[BossData.HistoricalRecoveryHistorySamples];
            RecordHistorySample();
        }

        /// <summary>One sample per authoritative 60 Hz tick, taken at tick start.</summary>
        private void RecordHistorySample() {
            if (!HasHistoricalRecovery) return;
            _history ??= new HistorySample[BossData.HistoricalRecoveryHistorySamples];
            _historyTick++;
            _historyHead = (_historyHead + 1) % _history.Length;
            _history[_historyHead] = new HistorySample(_historyTick, GlobalPosition, CurrentHP);
            if (_historyCount < _history.Length) _historyCount++;
        }

        /// <summary>
        /// The locked sample at <c>t - lookback</c>, or the oldest real sample from
        /// this encounter when the history is younger than the lookback. The same
        /// sample supplies both position and HP even when the spatial fallback fires.
        /// </summary>
        private bool TryResolveHistorySample(int lookbackFrames, out HistorySample sample) {
            sample = default;
            if (_history == null || _historyCount <= 0) return false;
            int oldestTick = _historyTick - (_historyCount - 1);
            int targetTick = Math.Max(oldestTick, _historyTick - Math.Max(0, lookbackFrames));
            int offset = _historyTick - targetTick;
            int index = ((_historyHead - offset) % _history.Length + _history.Length) % _history.Length;
            sample = _history[index];
            return true;
        }

        /// <summary>
        /// Commits the recovery: locks the sample, resolves the capped heal, walks
        /// the three-tier destination chain, and enters the suspended beat. Latched
        /// first, so repeated contacts or callbacks can never create a second event.
        /// </summary>
        private void BeginHistoricalRecovery() {
            _historicalRecoveryUsed = true;

            if (!TryResolveHistorySample(BossData.HistoricalRecoveryLookbackFrames,
                    out HistorySample sample)) {
                sample = new HistorySample(_historyTick, GlobalPosition, CurrentHP);
            }

            // heal = max(0, min(P - H, floor(0.20 * M), M - H)); resultHP = H + heal.
            // Never lowers HP from a lower sample, never a flat 20%, never over max.
            int currentHP = CurrentHP;
            int maximum = Math.Max(1, ScaledMaxHP);
            int cap = (int)MathF.Floor(BossData.HistoricalRecoveryHealCapFraction * maximum);
            int heal = Math.Max(0, Math.Min(Math.Min(sample.HP - currentHP, cap), maximum - currentHP));
            _historicalRecoveryResolvedHP = currentHP + heal;

            bool valid = TryResolveRecoveryDestination(
                sample.Position, out _historicalRecoveryDestination);
            HistoricalRecoveryDestinationInvalid = !valid;
            HistoricalRecoveryUsedFallbackDestination =
                !valid || _historicalRecoveryDestination != sample.Position;
            if (!valid) {
                GD.PushWarning(
                    $"Boss '{Data?.BossID}' found no valid historical-recovery destination; "
                    + "the arena must author a safe anchor. Staying in place.");
            }

            _historicalRecoverySuspendFrames = BossData.HistoricalRecoverySuspendFrames;
            CurrentState = BossState.HistoricalRecovery;
            Velocity = Vector2.Zero;
            PlayAnimation("phase_transition");
            EventBus.Instance?.RaiseBossHistoricalRecovery(new BossHistoricalRecoveryPayload {
                BossID = Data?.BossID ?? "",
                HistoricalPosition = sample.Position,
                ResolvedPosition = _historicalRecoveryDestination,
                UsedFallbackDestination = HistoricalRecoveryUsedFallbackDestination,
                HPBeforeHeal = currentHP,
                HPAfterHeal = _historicalRecoveryResolvedHP,
                SuspendSeconds = BossData.HistoricalRecoverySuspendFrames / 60f
            });
        }

        /// <summary>
        /// Historical coordinate, then the boss's current coordinate, then a stable
        /// authored anchor. Nothing searches nearby points, moves platforms or moves
        /// the player to make a destination fit.
        /// </summary>
        private bool TryResolveRecoveryDestination(Vector2 historical, out Vector2 destination) {
            if (IsRecoveryDestinationValid(historical)) {
                destination = historical;
                return true;
            }
            if (IsRecoveryDestinationValid(GlobalPosition)) {
                destination = GlobalPosition;
                return true;
            }
            Node2D anchor = HistoricalRecoveryAnchor;
            if (anchor != null && IsInstanceValid(anchor)
                && IsRecoveryDestinationValid(anchor.GlobalPosition)) {
                destination = anchor.GlobalPosition;
                return true;
            }
            destination = GlobalPosition;
            return false;
        }

        private bool IsRecoveryDestinationValid(Vector2 point) =>
            HistoricalRecoveryDestinationValidator?.Invoke(point) ?? IsRecoveryDestinationClear(point);

        /// <summary>
        /// Default clearance test: the boss's own collision shape must fit at the
        /// destination against the Environment layer. Headless and shape-less setups
        /// accept — there is no arena to collide with, and refusing every tier would
        /// turn a missing test fixture into a failed recovery.
        /// </summary>
        private bool IsRecoveryDestinationClear(Vector2 point) {
            if (!IsInsideTree()) return true;
            var shapeNode = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
            if (shapeNode?.Shape == null) return true;
            PhysicsDirectSpaceState2D space = GetWorld2D()?.DirectSpaceState;
            if (space == null) return true;
            using var query = new PhysicsShapeQueryParameters2D {
                Shape = shapeNode.Shape,
                Transform = new Transform2D(0f, point + shapeNode.Position),
                CollisionMask = CollisionLayers.Environment,
                CollideWithBodies = true,
                CollideWithAreas = false
            };
            Godot.Collections.Array<Godot.Collections.Dictionary> hits =
                space.IntersectShape(query, maxResults: 1);
            using var hitsLifetime = hits.AsDisposable();
            return hits.Count == 0;
        }

        /// <summary>
        /// The suspended beat. Nothing else on this controller ticks while it runs —
        /// statuses, the Conductive mark, ability cooldowns, the rest timer, the
        /// executor and gravity are all frozen at their remaining values, which is
        /// exactly the contract's "pause statuses, lifetimes, cooldowns, Rally drain
        /// and gameplay clocks".
        /// </summary>
        private void ProcessHistoricalRecovery() {
            Velocity = Vector2.Zero;
            if (_historicalRecoverySuspendFrames > 0) {
                _historicalRecoverySuspendFrames--;
                if (_historicalRecoverySuspendFrames > 0) return;
            }
            CompleteHistoricalRecovery();
        }

        /// <summary>
        /// Applies the resolved position and HP exactly once, zeroes the relocation
        /// velocity, retains facing, and hands the boss back to Chase so its next
        /// attack runs a full normal telegraph rather than resuming the cancelled
        /// one. The rest cooldown is bypassed (the existing transition rule) and no
        /// ability cooldown is refilled.
        /// </summary>
        private void CompleteHistoricalRecovery() {
            _historicalRecoverySuspendFrames = 0;
            if (!HistoricalRecoveryDestinationInvalid) {
                GlobalPosition = _historicalRecoveryDestination;
            }
            Velocity = Vector2.Zero;
            int previousHP = CurrentHP;
            CurrentHP = Math.Clamp(_historicalRecoveryResolvedHP, 1, Math.Max(1, ScaledMaxHP));
            if (CurrentHP != previousHP) RaiseHPChanged();
            _restTimer = 0f;
            _restStandOffEngaged = false;
            _attackCommitted = false;
            _reactionFramesRemaining = 0;
            CurrentState = BossState.Chase;
            PlayAnimation("idle");
        }

        private void Die() {
            CurrentState = BossState.Dead;
            // V7.6: death clears both status slots and the caster-owned mark.
            ClearAllStatusEffects();
            ClearConductiveMark();
            Executor.Cancel();
            _attackHitbox?.Deactivate();
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
            _deathTimer = DeathAnimationSeconds;
            PlayAnimation("death");
            _glow?.ClearAllStates();
            EndGuardedScene(bowSurvivors: true);
            _guardians.Clear();
            RaiseHPChanged();
            Died?.Invoke(this);
            if (!RaisesDefeatPayload) return;
            EventBus.Instance?.RaiseBossDefeated(new BossDefeatedPayload {
                BossID = Data?.BossID ?? "",
                Position = GlobalPosition,
                ChronalDustDrop = Data?.ChronalDustDrop ?? 25
            });
        }

        // === Package 12 W9 (GAP-07) — boss phase mechanics ====================

        /// <summary>
        /// Every authored phase rule this body can enforce, in one place:
        /// arena guardians (the Inventor's coils), guarded summon scenes (the
        /// Tragedy King's soliloquy), teleport decoys (the Jackal Priest), and the
        /// M18 after-image trail. Each is data-driven from <see cref="BossData"/> /
        /// <see cref="EnemyAbilityData"/> and inert for every boss that does not
        /// author it.
        /// </summary>
        private readonly List<FTT.Combat.IDamageable> _guardians = new();
        private readonly List<EnemyController> _sceneActors = new();
        private float _sceneSecondsRemaining;
        private bool _phaseEntrySummonPending;
        private bool _guardPresentationShown;
        private int _afterImageFrame;

        /// <summary>
        /// Registers an arena object that shields this boss while it stands
        /// (<see cref="ArenaGuardian"/>). The boss refuses all damage while any
        /// registered guardian is alive.
        /// </summary>
        public void RegisterGuardian(FTT.Combat.IDamageable guardian) {
            if (guardian == null || _guardians.Contains(guardian)) return;
            _guardians.Add(guardian);
            RefreshGuardPresentation();
        }

        /// <summary>Live registered guardians (dead or freed ones are pruned).</summary>
        public int LiveGuardianCount {
            get {
                PruneGuardians();
                return _guardians.Count;
            }
        }

        /// <summary>True while any registered arena guardian still stands.</summary>
        public bool IsGuarded => LiveGuardianCount > 0;

        private void PruneGuardians() {
            for (int index = _guardians.Count - 1; index >= 0; index--) {
                FTT.Combat.IDamageable guardian = _guardians[index];
                bool freed = guardian is GodotObject godotObject && !IsInstanceValid(godotObject);
                if (freed || !guardian.IsAlive) _guardians.RemoveAt(index);
            }
        }

        /// <summary>True while a guarded summon scene runs (the King's soliloquy).</summary>
        public bool IsSoliloquy => _sceneActors.Count > 0 && _sceneSecondsRemaining > 0f;

        /// <summary>Actors still performing in the current guarded scene.</summary>
        public int SceneActorCount => _sceneActors.Count;

        /// <summary>Seconds left before the surviving actors bow.</summary>
        public float SceneSecondsRemaining => _sceneSecondsRemaining;

        /// <summary>Any GAP-07 invulnerability in force, for presentation and tests.</summary>
        public bool IsMechanicInvulnerable => IsGuarded || IsSoliloquy;

        private void OnMinionsSummoned(EnemyAbilityData ability, IReadOnlyList<EnemyController> minions) {
            if (Data == null || Data.GuardedSummonMinPhase < 0 || CurrentPhase < Data.GuardedSummonMinPhase) return;
            if (minions == null || minions.Count == 0) return;
            // A new scene replaces an old one; its actors bow first.
            EndGuardedScene(bowSurvivors: true);
            foreach (EnemyController actor in minions) {
                if (actor != null && IsInstanceValid(actor)) _sceneActors.Add(actor);
            }
            _sceneSecondsRemaining = Mathf.Max(0.1f, Data.GuardedSummonSceneSeconds);
            RefreshGuardPresentation();
        }

        /// <summary>
        /// The scene ends when every actor has fallen, or when its time is up and
        /// the survivors "take their bow" — they leave the stage (released to the
        /// pool) rather than lingering as ordinary adds.
        /// </summary>
        private void TickGuardedScene(float dt) {
            if (_sceneActors.Count == 0) return;
            for (int index = _sceneActors.Count - 1; index >= 0; index--) {
                EnemyController actor = _sceneActors[index];
                if (actor == null || !IsInstanceValid(actor) || !actor.IsInsideTree()
                    || actor.CurrentState == EnemyState.Dead) {
                    _sceneActors.RemoveAt(index);
                }
            }
            if (_sceneActors.Count == 0) {
                _sceneSecondsRemaining = 0f;
                return;
            }
            _sceneSecondsRemaining -= dt;
            if (_sceneSecondsRemaining <= 0f) EndGuardedScene(bowSurvivors: true);
        }

        private void EndGuardedScene(bool bowSurvivors) {
            if (bowSurvivors) {
                foreach (EnemyController actor in _sceneActors) {
                    if (actor == null || !IsInstanceValid(actor) || actor.CurrentState == EnemyState.Dead) continue;
                    // Release falls back to QueueFree for an unpooled body.
                    if (PoolManager.Instance != null) PoolManager.Instance.Release(actor);
                    else actor.QueueFree();
                }
            }
            _sceneActors.Clear();
            _sceneSecondsRemaining = 0f;
        }

        /// <summary>
        /// Casts the first authored SummonMinions ability immediately — the phase
        /// entry that opens a guarded scene. Arms its cooldown like any cast.
        /// </summary>
        private void BeginPhaseEntrySummon() {
            EnemyAbilityData[] abilities = ActiveAbilityArray;
            for (int index = 0; index < abilities.Length; index++) {
                EnemyAbilityData ability = abilities[index];
                if (ability?.Archetype != EnemyAbilityArchetype.SummonMinions) continue;
                if (CurrentPhase < GetActiveAbilityMinPhase(index)) continue;
                SelectedAbilityIndex = index;
                SelectedAbility = ability;
                ArmAbilityCooldown(index);
                Vector2 targetPosition = _target?.GlobalPosition ?? GlobalPosition;
                Executor.Begin(ability, targetPosition, _facingRight,
                    guardCrush: ability.IsGuardCrushing, unblockable: ability.IsUnblockable);
                CurrentState = BossState.Attacking;
                _attackCommitted = true;
                _reactionFramesRemaining = 0;
                PlayAnimation("ranged_attack");
                return;
            }
        }

        /// <summary>Test seam: advances the guarded scene's clock and cast list.</summary>
        internal void AdvanceGuardedSceneForTest(float delta) => TickGuardedScene(delta);

        /// <summary>Test seam: fires the phase-entry summon without waiting out the window.</summary>
        internal void BeginPhaseEntrySummonForTest() {
            _phaseEntrySummonPending = false;
            BeginPhaseEntrySummon();
        }

        private void OnTeleported(EnemyAbilityData ability, Vector2 vacated) {
            if (Data == null || Data.TeleportDecoyMinPhase < 0 || CurrentPhase < Data.TeleportDecoyMinPhase) return;
            Node parent = GetParent();
            if (parent == null) return;
            Texture2D silhouette = null;
            if (_sprite?.SpriteFrames != null && _sprite.SpriteFrames.HasAnimation(_sprite.Animation)) {
                silhouette = _sprite.SpriteFrames.GetFrameTexture(_sprite.Animation, _sprite.Frame);
            }
            var decoy = new BossDecoy { Name = "SandDecoy" };
            decoy.Configure(Data, silhouette, _sprite?.FlipH ?? false);
            parent.AddChild(decoy);
            decoy.GlobalPosition = vacated;
            LastDecoy = decoy;
        }

        /// <summary>The most recently left decoy (test seam).</summary>
        public BossDecoy LastDecoy { get; private set; }

        /// <summary>
        /// The shared gold hyper-armor glow — the codebase's armor language — while
        /// a GAP-07 shield holds, so "hitting him does nothing" is readable.
        /// </summary>
        private void RefreshGuardPresentation() {
            bool shown = IsMechanicInvulnerable;
            if (shown == _guardPresentationShown) return;
            _guardPresentationShown = shown;
            _glow?.SetHyperArmor(shown);
        }

        /// <summary>M18: a fading placeholder ghost every fourth active frame.</summary>
        private void TickAfterImages() {
            if (Executor.Phase != EnemyAbilityPhase.Active || Executor.ActiveAbility?.LeavesAfterImages != true) {
                _afterImageFrame = 0;
                return;
            }
            if (_afterImageFrame++ % 4 != 0) return;
            if (!FTT.Core.ComfortSettings.GhostTrailsAllowed || _sprite == null) return;
            Node parent = GetParent();
            if (parent == null) return;
            AfterImageGhost ghost = AfterImageGhost.From(_sprite);
            parent.AddChild(ghost);
            ghost.GlobalPosition = _sprite.GlobalPosition;
            AfterImagesSpawned++;
        }

        /// <summary>After-images this body has shed (test seam).</summary>
        public int AfterImagesSpawned { get; private set; }

        private void RaiseHPChanged() {
            EventBus.Instance?.RaiseBossHPChanged(new BossHPPayload {
                BossID = Data?.BossID ?? "",
                CurrentHP = CurrentHP,
                MaxHP = ScaledMaxHP
            });
        }

        /// <summary>Knockback-immune bosses still take HP damage; only the shove is denied.</summary>
        public void ApplyKnockback(Vector2 knockback, bool attackerFacingRight) {
            if (CurrentState == BossState.Dead || Data?.IsKnockbackImmune == true) return;
            // Impulse-free hits (construct arcs/bites carry zero knockback).
            if (knockback == Vector2.Zero) return;
            Velocity += FTT.Combat.DamageCalculator.CalculateKnockback(knockback, 2f, attackerFacingRight) * 60f;
        }

        /// <summary>
        /// V7 two-slot status rule (mirrors StatusController), routed through the
        /// shared <see cref="FTT.Combat.StatusRouting"/> table: a damaging status
        /// and a control status coexist; a new application competes only with the
        /// occupant of its own slot, and V7.6's stronger-wins rule means a weaker
        /// same-type reapplication does nothing at all.
        /// </summary>
        public void ApplyStatusEffect(StatusType type, float duration, float intensity = 1f) {
            if (CurrentState == BossState.Dead || type == StatusType.None || duration <= 0f) return;
            // V7.6: Suppression is a player-side ability lock with no boss
            // meaning; refusing it keeps an inert status from evicting a live
            // control status.
            if (type == StatusType.Suppression) return;
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
                        // Movement denial is HP-unrelated, so knockback immunity does not block it.
                        StatusMoveMultiplier = 0f;
                        Velocity = new Vector2(0f, Velocity.Y);
                        break;
                    case StatusType.StaticCharge:
                        StatusMoveMultiplier = Mathf.Max(0.1f, 1f - 0.35f * potency);
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
                        ApplyBossDamage(Math.Max(1, (int)MathF.Round(2f * _damageStatusIntensity)));
                    }
                }
                if (_damageStatusTimer <= 0f) {
                    ClearDamageStatusSlot();
                    RefreshStatusGlow();
                }
            }
        }

        private float OnHurtboxHit(FTT.Combat.HitPayload hit) {
            int damageApplied = ApplyBossDamage(Mathf.Max(0, (int)Mathf.Round(hit.Damage)));
            if (damageApplied <= 0) return 0f;
            // V7.1 hitstop (victim side): a killing blow skips — the death
            // presentation owns that moment.
            if (CurrentState != BossState.Dead) {
                ApplyHitstop(FTT.Combat.BasicComboRules.HitstopFrames(damageApplied));
            }
            ApplyKnockback(hit.Knockback, hit.AttackerFacingRight);
            // V7.6 F07: the caster-owned combo mark rides the same hit but is
            // not a status — no slot, no action lock, zero stagger budget.
            if (hit.ComboMark == FTT.Combat.ComboMarkType.Conductive && hit.ComboMarkFrames > 0) {
                ApplyConductiveMark(hit.AttackerIndex, hit.ComboMarkFrames);
            }
            if (hit.AppliedStatus != StatusType.None && hit.StatusDuration > 0f) {
                ApplyStatusEffect(hit.AppliedStatus, hit.StatusDuration, hit.StatusIntensity);
            }
            return damageApplied;
        }

        // === Presentation and callbacks ===

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

        public void ActivateHitbox() => _attackHitbox?.Activate();

        public void DeactivateHitbox() => _attackHitbox?.Deactivate();

        public void EmitPresentationEvent() {
            EventBus.Instance?.RaiseEnemyPresentation(new EnemyPresentationPayload {
                SourceID = Data?.BossID ?? "",
                AbilityID = Executor.ActiveAbility?.AbilityID ?? "",
                PresentationEventID = Executor.ActiveAbility?.PresentationEventID ?? "",
                Phase = EnemyPresentationPhase.Active,
                Position = GlobalPosition
            });
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

        // === Pooling and rewind ===

        public void OnSpawn() {
            ResolveNodes();
            // Package 12 W9: encounter-local phase-mechanic state never survives a
            // pool cycle (the membership index itself is the encounter's to assign).
            _absorbedMembers.Clear();
            _guardians.Clear();
            EndGuardedScene(bowSurvivors: false);
            _phaseEntrySummonPending = false;
            _guardPresentationShown = false;
            _afterImageFrame = 0;
            LastDecoy = null;
            ApplyData(Data);
            CurrentState = BossState.Idle;
            _restTimer = 0f;
            _transitionTimer = 0f;
            _deathTimer = 0f;
            _reactionFramesRemaining = 0;
            _attackCommitted = false;
            _restStandOffEngaged = false;
            SelectedAbilityIndex = -1;
            SelectedAbility = null;
            LastTelegraphInterrupted = false;
            _target = null;
            _rewindFrozen = false;
            _checkpointCaptured = false;
            _spawnAnnounced = false;
            _spawnPosition = GlobalPosition;
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
            BindEvents();
            AnnounceSpawn();
        }

        public void OnDespawn() {
            UnbindEvents();
            Executor.Reset();
            _attackHitbox?.Deactivate();
            if (_hurtbox != null) {
                _hurtbox.Monitoring = false;
                _hurtbox.Monitorable = false;
            }
            _target = null;
            _rewindFrozen = false;
            _restStandOffEngaged = false;
            _spawnAnnounced = false;
            Velocity = Vector2.Zero;
            CollisionLayer = 0;
            CollisionMask = 0;
        }

        public void SetStoryRewindFrozen(bool frozen) {
            _rewindFrozen = frozen;
            if (!frozen) return;
            Velocity = Vector2.Zero;
            Executor.Cancel();
            _attackHitbox?.Deactivate();
        }

        public void CaptureCheckpointState(string checkpointID) {
            if (CurrentState == BossState.Dead) return;
            _checkpointPosition = GlobalPosition;
            _checkpointHP = CurrentHP;
            _checkpointCaptured = true;
        }

        public void ApplyStoryRewind() {
            // Dead bosses stay dead (audit M-6, mirroring CaptureCheckpointState):
            // Die() zeroed collision/hurtbox/pushbox and already raised the defeat
            // payload with its dust, so a restore would stand up an invulnerable
            // ghost whose second defeat double-pays. Skip the restore entirely.
            if (CurrentState == BossState.Dead) return;
            if (RewindPolicy == StoryRewindPolicy.PreserveCurrentState) return;
            bool useCheckpoint = RewindPolicy == StoryRewindPolicy.RestoreCheckpointState && _checkpointCaptured;
            GlobalPosition = useCheckpoint ? _checkpointPosition : _spawnPosition;
            CurrentHP = useCheckpoint && _checkpointHP > 0 ? _checkpointHP : ScaledMaxHP;
            if (!useCheckpoint) CurrentPhase = 0;
            CurrentState = BossState.Idle;
            _target = null;
            _restTimer = 0f;
            _hitstopFramesRemaining = 0;
            if (_sprite != null) _sprite.SpeedScale = 1f;
            _transitionTimer = 0f;
            _attackCommitted = false;
            _restStandOffEngaged = false;
            _reactionFramesRemaining = 0;
            Velocity = Vector2.Zero;
            ClearStatusEffect();
            Executor.Reset();
            _attackHitbox?.Deactivate();
            PlayAnimation("idle");
            RaiseHPChanged();
        }

        private void OnRewindTriggered(Vector2 targetPosition) => ApplyStoryRewind();

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
