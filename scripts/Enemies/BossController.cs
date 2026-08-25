using Godot;
using System;
using System.Collections.Generic;
using FTT.Core;
using FTT.Environment;

namespace FTT.Enemies {

    public enum BossState { Idle, Chase, Attacking, PhaseTransitioning, RestWindow, Dead }

    /// <summary>
    /// Story-side boss runtime. Executes authored <see cref="EnemyAbilityData"/>
    /// through the shared <see cref="EnemyAbilityExecutor"/> with weighted random
    /// selection, distance filtering, phase gating, telegraph interruption, and
    /// knockback immunity. Story-only; nothing here reaches scripts/FighterSim.
    /// </summary>
    public partial class BossController : CharacterBody2D, IPoolable, IStoryRewindable, IStoryRewindSimulation {
        public const float DeathAnimationSeconds = 1.0f;
        private const float GravityPixelsPerSecond = 980f;
        private const float PixelsPerUnit = 60f;
        private const float RestTrackingSpeedFactor = 0.3f;

        private static int _spawnCounter;

        [Export] public BossData Data;
        /// <summary>Non-zero pins the attack-selection RNG for tests and replays.</summary>
        [Export] public ulong SelectionSeed;
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

        public StatusType ControlStatusType { get; private set; } = StatusType.None;
        public StatusType DamageStatusType { get; private set; } = StatusType.None;
        /// <summary>Control slot first, then damage — the compat view for single-status readers.</summary>
        public StatusType ActiveStatusType =>
            ControlStatusType != StatusType.None ? ControlStatusType : DamageStatusType;
        public bool HasStatusEffect(StatusType type) =>
            ControlStatusType == type || DamageStatusType == type;
        public float StatusMoveMultiplier { get; private set; } = 1f;
        public float StatusDamageTakenMultiplier { get; private set; } = 1f;

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
            _scaledMaxHP = Data != null ? StoryDifficultyTuning.ScaleEnemyHP(Data.MaxHP, difficulty) : 0;
            CurrentHP = ScaledMaxHP;
            CurrentPhase = 0;
            Executor.SourceID = Data?.BossID ?? "";
            Executor.DamageMultiplier = StoryDifficultyTuning.GetEnemyDamageMultiplier(difficulty);
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

            // V7.1 hitstop: the boss freezes for the shared window like every
            // other combatant (its knockback stays unscaled — bosses do not
            // fly — but the hit-weight freeze is universal).
            if (_hitstopFramesRemaining > 0) {
                _hitstopFramesRemaining--;
                if (_sprite != null) _sprite.SpeedScale = 0f;
                if (_hitstopFramesRemaining <= 0 && _sprite != null) _sprite.SpeedScale = 1f;
                return;
            }

            TickStatus(dt);
            TickAbilityCooldowns(dt);
            if (CurrentState == BossState.Dead) return;

            if (_target == null || !IsInstanceValid(_target)) _target = FindNearestPlayer();
            Executor.Tick(dt);

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

        // === Attack selection ===

        // Per-ability cooldown timers, indexed like Data.BossAbilities. The
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
            EnemyAbilityData[] abilities = Data?.BossAbilities;
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
            EnemyAbilityData[] abilities = Data?.BossAbilities;
            if (abilities == null || abilities.Length == 0) return -1;

            _selectionBuffer.Clear();
            var unlocked = new List<int>();
            bool inMelee = distancePixels <= MeleeBandPixels;
            for (int index = 0; index < abilities.Length; index++) {
                EnemyAbilityData ability = abilities[index];
                if (ability == null) continue;
                if (CurrentPhase < Data.GetAbilityMinPhase(index)) continue;
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

            SelectedAbility = Data.BossAbilities[SelectedAbilityIndex];
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
        /// V7.1 hitstop: freezes the boss's gameplay clock for the given frames
        /// (max-assign — an active freeze is never shortened). Dead bosses skip.
        /// </summary>
        public void ApplyHitstop(int frames) {
            if (frames <= 0 || CurrentState == BossState.Dead) return;
            if (frames > _hitstopFramesRemaining) _hitstopFramesRemaining = frames;
        }

        private int ApplyBossDamage(int damage) {
            if (CurrentState == BossState.Dead || CurrentState == BossState.PhaseTransitioning) return 0;

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
            float[] thresholds = Data?.PhaseThresholds;
            if (thresholds == null || thresholds.Length == 0) return;
            float hpPercent = ScaledMaxHP > 0 ? (float)CurrentHP / ScaledMaxHP : 0f;

            int crossed = 0;
            while (CurrentPhase < thresholds.Length && hpPercent <= thresholds[CurrentPhase]) {
                CurrentPhase++;
                crossed++;
                EventBus.Instance?.RaiseBossPhaseChanged(CurrentPhase);
            }
            if (crossed == 0) return;

            Executor.Cancel();
            _attackHitbox?.Deactivate();
            _attackCommitted = false;
            _reactionFramesRemaining = 0;
            _transitionTimer = Mathf.Max(0f, Data.PhaseTransitionInvincibilityDuration);
            CurrentState = BossState.PhaseTransitioning;
            PlayAnimation("phase_transition");
        }

        private void Die() {
            CurrentState = BossState.Dead;
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
            RaiseHPChanged();
            EventBus.Instance?.RaiseBossDefeated(new BossDefeatedPayload {
                BossID = Data?.BossID ?? "",
                Position = GlobalPosition,
                ChronalDustDrop = Data?.ChronalDustDrop ?? 50
            });
        }

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
        /// V7 two-slot status rule (mirrors StatusController): a damaging status
        /// and a control status coexist; a new application replaces only the
        /// occupant of its own slot.
        /// </summary>
        public void ApplyStatusEffect(StatusType type, float duration, float intensity = 1f) {
            if (CurrentState == BossState.Dead || type == StatusType.None || duration <= 0f) return;
            float potency = intensity <= 0f ? 1f : intensity;
            if (FTT.Combat.StatusController.IsDamageStatus(type)) {
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
    }
}
