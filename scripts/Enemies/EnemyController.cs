using Godot;
using System;

namespace FTT.Enemies {

    public enum EnemyState { Idle, Patrol, Chase, Attacking, Stunned, Returning, Dead }

    public partial class EnemyController : CharacterBody2D {
        [Export] public EnemyData Data;

        public EnemyState CurrentState { get; private set; } = EnemyState.Patrol;
        public int CurrentHP;
        private float _attackCooldownTimer;
        private float _stunTimer;
        private float _patrolIdleTimer;
        private float _attackActiveTimer;
        private bool _patrolForward = true;

        private Vector2 _spawnPosition;
        private Vector2 _patrolPointA;
        private Vector2 _patrolPointB;
        private bool _patrolPointsSet;
        private AnimatedSprite2D _sprite;
        private FTT.Characters.PlayerController _target;
        private FTT.Combat.Hitbox _attackHitbox;
        private ProgressBar _hpBar;
        private bool _facingRight = true;

        public override void _Ready() {
            if (Data != null) CurrentHP = Data.MaxHP;
            _sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
            _attackHitbox = GetNodeOrNull<FTT.Combat.Hitbox>("AttackHitbox");
            _hpBar = GetNodeOrNull<ProgressBar>("HPBar");

            _spawnPosition = GlobalPosition;

            var wpA = GetNodeOrNull<Marker2D>("WaypointA");
            var wpB = GetNodeOrNull<Marker2D>("WaypointB");
            if (wpA != null && wpB != null) {
                _patrolPointA = GlobalPosition + wpA.Position;
                _patrolPointB = GlobalPosition + wpB.Position;
                _patrolPointsSet = true;
            }

            CollisionLayer = FTT.Core.CollisionLayers.Enemy;
            CollisionMask = FTT.Core.CollisionLayers.EnemyBodyMask;

            UpdateHPBar();
        }

        public override void _PhysicsProcess(double delta) {
            float dt = (float)delta;
            if (_attackCooldownTimer > 0) _attackCooldownTimer -= dt;

            if (_attackActiveTimer > 0) {
                _attackActiveTimer -= dt;
                if (_attackActiveTimer <= 0) {
                    _attackHitbox?.Deactivate();
                }
            }

            switch (CurrentState) {
                case EnemyState.Patrol: ProcessPatrol(dt); break;
                case EnemyState.Chase: ProcessChase(dt); break;
                case EnemyState.Attacking: ProcessAttacking(dt); break;
                case EnemyState.Stunned: ProcessStunned(dt); break;
                case EnemyState.Returning: ProcessReturning(dt); break;
                case EnemyState.Dead: break;
                default: break;
            }

            if (CurrentState != EnemyState.Dead) {
                ApplyGravity(dt);
                MoveAndSlide();
            }
        }

        private void ApplyGravity(float dt) {
            if (!IsOnFloor()) {
                Velocity = new Vector2(Velocity.X, Velocity.Y + 980f * dt);
            }
        }

        private void ProcessPatrol(float dt) {
            var player = FindNearestPlayer();
            if (player != null && GlobalPosition.DistanceTo(player.GlobalPosition) <= (Data?.AggroRadius ?? 400f)) {
                _target = player;
                CurrentState = EnemyState.Chase;
                return;
            }

            if (!_patrolPointsSet) {
                Velocity = new Vector2(0, Velocity.Y);
                return;
            }

            if (_patrolIdleTimer > 0) {
                _patrolIdleTimer -= dt;
                Velocity = new Vector2(0, Velocity.Y);
                return;
            }

            var targetPos = _patrolForward ? _patrolPointB : _patrolPointA;
            float dirX = targetPos.X - GlobalPosition.X;

            if (Mathf.Abs(dirX) < 10f) {
                _patrolForward = !_patrolForward;
                _patrolIdleTimer = 1.0f;
                Velocity = new Vector2(0, Velocity.Y);
                return;
            }

            float sign = Mathf.Sign(dirX);
            _facingRight = sign > 0;
            Velocity = new Vector2(sign * (Data?.MoveSpeed ?? 3f) * 60f, Velocity.Y);
            if (_sprite != null) _sprite.FlipH = !_facingRight;
            _sprite?.Play("walk");
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

            float attackRange = (Data?.AttackRange ?? 1.5f) * 60f;
            if (dist <= attackRange && _attackCooldownTimer <= 0) {
                CurrentState = EnemyState.Attacking;
                Velocity = new Vector2(0, Velocity.Y);
                return;
            }

            float dirX = _target.GlobalPosition.X - GlobalPosition.X;
            float sign = Mathf.Sign(dirX);
            _facingRight = sign > 0;
            Velocity = new Vector2(sign * (Data?.MoveSpeed ?? 3f) * 60f, Velocity.Y);
            if (_sprite != null) _sprite.FlipH = !_facingRight;
            _sprite?.Play("walk");
        }

        private void ProcessAttacking(float dt) {
            PerformAttack();
            _attackCooldownTimer = Data?.AttackCooldown ?? 2f;
            CurrentState = EnemyState.Chase;
        }

        private void PerformAttack() {
            if (_attackHitbox != null) {
                float hitboxX = _facingRight ? 30f : -30f;
                _attackHitbox.Position = new Vector2(hitboxX, -25f);
                _attackHitbox.KnockbackForce = new Vector2(
                    (_facingRight ? 1f : -1f) * Mathf.Abs(_attackHitbox.KnockbackForce.X),
                    _attackHitbox.KnockbackForce.Y);
                _attackHitbox.Activate();
                _attackActiveTimer = 0.2f;
            }
            _sprite?.Play("attack");
        }

        private void ProcessStunned(float dt) {
            Velocity = new Vector2(0, Velocity.Y);
            _stunTimer -= dt;
            if (_stunTimer <= 0) CurrentState = EnemyState.Chase;
        }

        private void ProcessReturning(float dt) {
            var player = FindNearestPlayer();
            if (player != null && GlobalPosition.DistanceTo(player.GlobalPosition) <= (Data?.AggroRadius ?? 400f)) {
                _target = player;
                CurrentState = EnemyState.Chase;
                return;
            }

            float dirX = _spawnPosition.X - GlobalPosition.X;
            if (Mathf.Abs(dirX) < 15f) {
                CurrentState = EnemyState.Patrol;
                _target = null;
                Velocity = new Vector2(0, Velocity.Y);
                return;
            }

            float sign = Mathf.Sign(dirX);
            _facingRight = sign > 0;
            Velocity = new Vector2(sign * (Data?.MoveSpeed ?? 3f) * 60f, Velocity.Y);
            if (_sprite != null) _sprite.FlipH = !_facingRight;
        }

        private void StartReturning() {
            _target = null;
            CurrentState = EnemyState.Returning;
        }

        public int TakeDamage(int damage) {
            if (CurrentState == EnemyState.Dead) return 0;
            int previousHP = CurrentHP;
            CurrentHP = Math.Max(0, CurrentHP - Math.Max(0, damage));
            int damageApplied = previousHP - CurrentHP;
            UpdateHPBar();

            if (CurrentHP <= 0) {
                CurrentState = EnemyState.Dead;
                _attackHitbox?.Deactivate();
                FTT.Core.EventBus.Instance?.RaiseEnemyKilled(new FTT.Core.EnemyKilledPayload {
                    EnemyID = Data?.EnemyID ?? "",
                    Position = GlobalPosition,
                    ChronalDustDrop = Data?.ChronalDustDrop ?? 10,
                    IsElite = Data?.Tier == EnemyTier.Elite
                });
                QueueFree();
            }
            return damageApplied;
        }

        public void ApplyKnockback(Vector2 knockback, bool attackerFacingRight) {
            if (CurrentState == EnemyState.Dead) return;
            float weight = Data?.Weight ?? 1.0f;
            Velocity += FTT.Combat.DamageCalculator.CalculateKnockback(knockback, weight, attackerFacingRight) * 60f;
        }

        public void ApplyStun(float duration) {
            if (CurrentState == EnemyState.Dead) return;
            float resistance = Data?.StunResistance ?? 0f;
            _stunTimer = duration * (1f - resistance);
            CurrentState = EnemyState.Stunned;
        }

        private void UpdateHPBar() {
            if (_hpBar != null) {
                _hpBar.MaxValue = Data?.MaxHP ?? 50;
                _hpBar.Value = CurrentHP;
            }
        }

        private FTT.Characters.PlayerController FindNearestPlayer() {
            FTT.Characters.PlayerController nearest = null;
            float nearestDist = float.MaxValue;
            foreach (var node in GetTree().GetNodesInGroup("Players")) {
                if (node is FTT.Characters.PlayerController pc && pc.CurrentState != FTT.Characters.CharacterState.Dead) {
                    float d = GlobalPosition.DistanceTo(pc.GlobalPosition);
                    if (d < nearestDist) { nearestDist = d; nearest = pc; }
                }
            }
            return nearest;
        }
    }
}
