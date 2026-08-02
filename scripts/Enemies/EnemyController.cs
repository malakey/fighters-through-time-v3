using Godot;
using System;

namespace FTT.Enemies {

    public enum EnemyState { Idle, Patrol, Chase, Attacking, Stunned, Dead }

    public partial class EnemyController : CharacterBody2D {
        [Export] public EnemyData Data;

        public EnemyState CurrentState { get; private set; } = EnemyState.Patrol;
        public int CurrentHP;
        private float _attackCooldownTimer;
        private float _stunTimer;
        private float _patrolIdleTimer;
        private bool _patrolForward = true;

        private Marker2D _waypointA;
        private Marker2D _waypointB;
        private AnimatedSprite2D _sprite;
        private FTT.Characters.PlayerController _target;

        public override void _Ready() {
            if (Data != null) CurrentHP = Data.MaxHP;
            _sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
            _waypointA = GetNodeOrNull<Marker2D>("WaypointA");
            _waypointB = GetNodeOrNull<Marker2D>("WaypointB");

            CollisionLayer = 2;
            CollisionMask = 1 | 2;
        }

        public override void _PhysicsProcess(double delta) {
            float dt = (float)delta;
            if (_attackCooldownTimer > 0) _attackCooldownTimer -= dt;

            switch (CurrentState) {
                case EnemyState.Patrol: ProcessPatrol(dt); break;
                case EnemyState.Chase: ProcessChase(dt); break;
                case EnemyState.Attacking: ProcessAttacking(dt); break;
                case EnemyState.Stunned: ProcessStunned(dt); break;
                case EnemyState.Dead: break;
                default: break;
            }

            if (CurrentState != EnemyState.Dead) MoveAndSlide();
        }

        private void ProcessPatrol(float dt) {
            var player = FindNearestPlayer();
            if (player != null && GlobalPosition.DistanceTo(player.GlobalPosition) <= (Data?.AggroRadius ?? 8f)) {
                _target = player;
                CurrentState = EnemyState.Chase;
                return;
            }

            if (_patrolIdleTimer > 0) { _patrolIdleTimer -= dt; return; }

            var targetWP = _patrolForward ? _waypointB : _waypointA;
            if (targetWP == null) return;

            var dir = (targetWP.GlobalPosition - GlobalPosition).Normalized();
            Velocity = new Vector2(dir.X * (Data?.MoveSpeed ?? 3f) * 60f, Velocity.Y + 30f * dt * 60f);

            if (GlobalPosition.DistanceTo(targetWP.GlobalPosition) < 10f) {
                _patrolForward = !_patrolForward;
                _patrolIdleTimer = 1.0f;
            }
            _sprite?.Play("walk");
        }

        private void ProcessChase(float dt) {
            if (_target == null || _target.CurrentState == FTT.Characters.CharacterState.Dead) {
                CurrentState = EnemyState.Patrol;
                _target = null;
                return;
            }

            float dist = GlobalPosition.DistanceTo(_target.GlobalPosition);
            if (dist > (Data?.DeAggroRadius ?? 12f)) {
                CurrentState = EnemyState.Patrol;
                _target = null;
                return;
            }

            if (dist <= (Data?.AttackRange ?? 1.5f) * 60f && _attackCooldownTimer <= 0) {
                CurrentState = EnemyState.Attacking;
                return;
            }

            var dir = (_target.GlobalPosition - GlobalPosition).Normalized();
            Velocity = new Vector2(dir.X * (Data?.MoveSpeed ?? 3f) * 60f, Velocity.Y + 30f * dt * 60f);
            if (_sprite != null) _sprite.FlipH = dir.X < 0;
            _sprite?.Play("walk");
        }

        private void ProcessAttacking(float dt) {
            _attackCooldownTimer = Data?.AttackCooldown ?? 2f;
            _sprite?.Play("attack");
            CurrentState = EnemyState.Chase;
        }

        private void ProcessStunned(float dt) {
            _stunTimer -= dt;
            if (_stunTimer <= 0) CurrentState = EnemyState.Chase;
        }

        public void TakeDamage(int damage) {
            CurrentHP -= damage;
            if (CurrentHP <= 0) {
                CurrentHP = 0;
                CurrentState = EnemyState.Dead;
                FTT.Core.EventBus.Instance?.RaiseEnemyKilled(new FTT.Core.EnemyKilledPayload {
                    EnemyID = Data?.EnemyID ?? "",
                    Position = GlobalPosition,
                    ChronalDustDrop = Data?.ChronalDustDrop ?? 10,
                    IsElite = Data?.Tier == EnemyTier.Elite
                });
            }
        }

        public void ApplyStun(float duration) {
            float resistance = Data?.StunResistance ?? 0f;
            _stunTimer = duration * (1f - resistance);
            CurrentState = EnemyState.Stunned;
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
