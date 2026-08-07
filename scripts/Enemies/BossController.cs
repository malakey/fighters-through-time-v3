using Godot;
using System;

namespace FTT.Enemies {

    public enum BossState { Idle, Chase, Attacking, PhaseTransitioning, RestWindow, Dead }

    public partial class BossController : CharacterBody2D {
        [Export] public BossData Data;

        public BossState CurrentState { get; private set; } = BossState.Idle;
        public int CurrentHP;
        public int CurrentPhase;
        private float _restTimer;
        private float _attackTimer;
        private float _attackActiveTimer;
        private int _currentAbilityIndex = -1;
        private int _scaledMaxHP;
        private AnimatedSprite2D _sprite;
        private FTT.Characters.PlayerController _target;
        private FTT.Combat.CombatantPushbox _pushbox;
        private FTT.Combat.Hurtbox _hurtbox;
        private FTT.Combat.Hitbox _attackHitbox;
        private Random _rng = new();

        /// <summary>Story difficulty-scaled maximum HP; canonical base stays in BossData.</summary>
        public int ScaledMaxHP => _scaledMaxHP > 0 ? _scaledMaxHP : Data?.MaxHP ?? 1;

        public override void _Ready() {
            FTT.Core.Difficulty difficulty = FTT.Core.StoryDifficultyTuning.CurrentStoryDifficulty;
            if (Data != null) {
                _scaledMaxHP = FTT.Core.StoryDifficultyTuning.ScaleEnemyHP(Data.MaxHP, difficulty);
                CurrentHP = _scaledMaxHP;
            }
            _sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
            _pushbox = GetNodeOrNull<FTT.Combat.CombatantPushbox>("Pushbox");
            _hurtbox = GetNodeOrNull<FTT.Combat.Hurtbox>("Hurtbox");
            if (_hurtbox != null) _hurtbox.OnHit += OnHurtboxHit;
            _attackHitbox = GetNodeOrNull<FTT.Combat.Hitbox>("AttackHitbox");
            if (_attackHitbox != null && Data != null) {
                _attackHitbox.Damage = FTT.Core.StoryDifficultyTuning.ScaleEnemyDamage(Data.AttackDamage, difficulty);
                _attackHitbox.KnockbackForce = new Vector2(Data.AttackKnockback, -1.5f);
            }

            CollisionLayer = FTT.Core.CollisionLayers.Enemy;
            CollisionMask = FTT.Core.CollisionLayers.EnemyBodyMask;
        }

        public override void _ExitTree() {
            if (_hurtbox != null) _hurtbox.OnHit -= OnHurtboxHit;
        }

        private float OnHurtboxHit(FTT.Combat.HitPayload hit) {
            int damageApplied = ApplyBossDamage(Mathf.Max(0, (int)Mathf.Round(hit.Damage)));
            return damageApplied;
        }

        public override void _PhysicsProcess(double delta) {
            float dt = (float)delta;
            _target ??= FindNearestPlayer();

            if (_attackActiveTimer > 0f) {
                _attackActiveTimer -= dt;
                if (_attackActiveTimer <= 0f) _attackHitbox?.Deactivate();
            }

            switch (CurrentState) {
                case BossState.Idle:
                    if (_target != null) CurrentState = BossState.Chase;
                    break;
                case BossState.Chase:
                    ProcessChase(dt);
                    break;
                case BossState.Attacking:
                    ProcessAttacking(dt);
                    break;
                case BossState.RestWindow:
                    _restTimer -= dt;
                    if (_restTimer <= 0) CurrentState = BossState.Chase;
                    TrackPlayer(dt);
                    break;
                case BossState.PhaseTransitioning:
                    _attackTimer -= dt;
                    if (_attackTimer <= 0) CurrentState = BossState.Chase;
                    break;
                case BossState.Dead: break;
            }

            if (CurrentState != BossState.Dead) {
                MoveAndSlide();
                _pushbox?.ResolveStoryOverlaps();
            }
        }

        private void ProcessChase(float dt) {
            if (_target == null) return;
            float dist = GlobalPosition.DistanceTo(_target.GlobalPosition);
            if (dist <= (Data?.AttackRange ?? 2f) * 60f) {
                SelectAndExecuteAttack();
                return;
            }

            var dir = (_target.GlobalPosition - GlobalPosition).Normalized();
            Velocity = new Vector2(dir.X * (Data?.MoveSpeed ?? 4f) * 60f, Velocity.Y + 30f * dt * 60f);
            if (_sprite != null) _sprite.FlipH = dir.X < 0;
        }

        private void ProcessAttacking(float dt) {
            _attackTimer -= dt;
            if (_attackTimer <= 0) {
                _restTimer = Data?.RestCooldown ?? 1.5f;
                CurrentState = BossState.RestWindow;
            }
        }

        private void SelectAndExecuteAttack() {
            if (Data?.BossAbilities == null || Data.BossAbilities.Length == 0) return;

            float totalWeight = 0;
            for (int i = 0; i < Data.AbilityWeights?.Length; i++) totalWeight += Data.AbilityWeights[i];
            if (totalWeight <= 0) { _currentAbilityIndex = 0; }
            else {
                float roll = (float)(_rng.NextDouble() * totalWeight);
                float cumulative = 0;
                for (int i = 0; i < Data.AbilityWeights.Length; i++) {
                    cumulative += Data.AbilityWeights[i];
                    if (roll <= cumulative) { _currentAbilityIndex = i; break; }
                }
            }

            _attackTimer = 1.0f;
            CurrentState = BossState.Attacking;
            if (_attackHitbox != null && _target != null) {
                bool facingRight = _target.GlobalPosition.X >= GlobalPosition.X;
                _attackHitbox.Position = new Vector2(facingRight ? 45f : -45f, -35f);
                _attackHitbox.KnockbackForce = new Vector2(
                    (facingRight ? 1f : -1f) * Mathf.Abs(_attackHitbox.KnockbackForce.X),
                    _attackHitbox.KnockbackForce.Y);
                _attackHitbox.Activate();
                _attackActiveTimer = 0.3f;
            }
            _sprite?.Play("attack");
        }

        private void TrackPlayer(float dt) {
            if (_target == null) return;
            var dir = (_target.GlobalPosition - GlobalPosition).Normalized();
            Velocity = new Vector2(dir.X * (Data?.MoveSpeed ?? 4f) * 0.3f * 60f, Velocity.Y + 30f * dt * 60f);
        }

        public void TakeDamage(int damage) => ApplyBossDamage(damage);

        private int ApplyBossDamage(int damage) {
            if (CurrentState == BossState.Dead || CurrentState == BossState.PhaseTransitioning) return 0;
            int previousHP = CurrentHP;
            CurrentHP = Math.Max(0, CurrentHP - Math.Max(0, damage));
            int damageApplied = previousHP - CurrentHP;
            CheckPhaseTransition();
            if (CurrentHP <= 0) {
                CurrentState = BossState.Dead;
                _attackHitbox?.Deactivate();
            }
            return damageApplied;
        }

        private void CheckPhaseTransition() {
            if (Data?.PhaseThresholds == null) return;
            float hpPercent = (float)CurrentHP / ScaledMaxHP;
            while (CurrentPhase < Data.PhaseThresholds.Length && hpPercent <= Data.PhaseThresholds[CurrentPhase]) {
                CurrentPhase++;
                CurrentState = BossState.PhaseTransitioning;
                _attackTimer = Data.PhaseTransitionInvincibilityDuration;
                _attackHitbox?.Deactivate();
                FTT.Core.EventBus.Instance?.RaiseBossPhaseChanged(CurrentPhase);
            }
        }

        private FTT.Characters.PlayerController FindNearestPlayer() {
            FTT.Characters.PlayerController nearest = null;
            float nearestDist = float.MaxValue;
            foreach (var node in GetTree().GetNodesInGroup("Players")) {
                if (node is FTT.Characters.PlayerController pc) {
                    float d = GlobalPosition.DistanceTo(pc.GlobalPosition);
                    if (d < nearestDist) { nearestDist = d; nearest = pc; }
                }
            }
            return nearest;
        }
    }
}
