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
        private int _currentAbilityIndex = -1;
        private AnimatedSprite2D _sprite;
        private FTT.Characters.PlayerController _target;
        private Random _rng = new();

        public override void _Ready() {
            if (Data != null) CurrentHP = Data.MaxHP;
            _sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");

            CollisionLayer = FTT.Core.CollisionLayers.Enemy;
            CollisionMask = FTT.Core.CollisionLayers.EnemyBodyMask;
        }

        public override void _PhysicsProcess(double delta) {
            float dt = (float)delta;
            _target ??= FindNearestPlayer();

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

            if (CurrentState != BossState.Dead) MoveAndSlide();
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
            _sprite?.Play("attack");
        }

        private void TrackPlayer(float dt) {
            if (_target == null) return;
            var dir = (_target.GlobalPosition - GlobalPosition).Normalized();
            Velocity = new Vector2(dir.X * (Data?.MoveSpeed ?? 4f) * 0.3f * 60f, Velocity.Y + 30f * dt * 60f);
        }

        public void TakeDamage(int damage) {
            CurrentHP -= damage;
            CheckPhaseTransition();
            if (CurrentHP <= 0) {
                CurrentHP = 0;
                CurrentState = BossState.Dead;
            }
        }

        private void CheckPhaseTransition() {
            if (Data?.PhaseThresholds == null) return;
            float hpPercent = (float)CurrentHP / (Data?.MaxHP ?? 1);
            while (CurrentPhase < Data.PhaseThresholds.Length && hpPercent <= Data.PhaseThresholds[CurrentPhase]) {
                CurrentPhase++;
                CurrentState = BossState.PhaseTransitioning;
                _attackTimer = 2.0f;
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
