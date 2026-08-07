using FTT.Core;
using Godot;

namespace FTT.Combat {

    public partial class PlaceholderZone : PooledNode, IPoolable {
        private float _lifetime;
        private float _tickTimer;
        private float _tickInterval;
        private float _damage;
        private int _ownerIndex;
        private StatusType _appliedStatus = StatusType.None;
        private float _statusDuration;
        private float _statusIntensity = 1f;
        private float _ownerSpeedMultiplier = 1f;
        private FTT.Characters.PlayerController _ownerPlayer;
        private Area2D _area;
        private ColorRect _visual;
        private ColorRect _border;
        private CollisionShape2D _shape;

        // Refresh window slightly longer than one frame so the buff persists while
        // the owner remains inside and lapses right after leaving.
        private const float OwnerBuffRefreshSeconds = 0.05f;

        public override void _Ready() => AddToGroup("story_zone");

        public void Setup(float damage, float lifetime, float tickInterval, int ownerIndex,
                          Color color, float radius = 60f,
                          StatusType appliedStatus = StatusType.None,
                          float statusDuration = 0f,
                          float statusIntensity = 1f,
                          FTT.Characters.PlayerController ownerPlayer = null,
                          float ownerSpeedMultiplier = 1f) {
            _lifetime = lifetime;
            _tickInterval = tickInterval;
            _damage = damage;
            _ownerIndex = ownerIndex;
            _appliedStatus = appliedStatus;
            _statusDuration = statusDuration;
            _statusIntensity = statusIntensity <= 0f ? 1f : statusIntensity;
            _ownerPlayer = ownerPlayer;
            _ownerSpeedMultiplier = ownerSpeedMultiplier;

            EnsureNodes();
            _visual.Size = new Vector2(radius * 2, radius * 2);
            _visual.Position = new Vector2(-radius, -radius);
            _visual.Color = new Color(color.R, color.G, color.B, 0.3f);
            _border.Size = new Vector2(radius * 2, 4);
            _border.Position = new Vector2(-radius, -radius);
            _border.Color = color;
            ((CircleShape2D)_shape.Shape).Radius = radius;
            // The zone applies effects directly to overlapping combatant bodies,
            // so it monitors body layers rather than hitbox/hurtbox area layers.
            _area.CollisionLayer = 0;
            _area.CollisionMask = CollisionLayers.Player | CollisionLayers.Enemy;
            _area.Monitoring = true;
            _area.Monitorable = false;
        }

        private void EnsureNodes() {
            if (_area != null) return;
            _visual = new ColorRect { Name = "Visual", MouseFilter = Control.MouseFilterEnum.Ignore };
            AddChild(_visual);
            _border = new ColorRect { Name = "Border", MouseFilter = Control.MouseFilterEnum.Ignore };
            AddChild(_border);
            _area = new Area2D { Name = "Area" };
            _shape = new CollisionShape2D {
                Name = "CollisionShape2D",
                Shape = new CircleShape2D()
            };
            _area.AddChild(_shape);
            AddChild(_area);
        }

        public override void _PhysicsProcess(double delta) {
            float dt = (float)delta;
            _lifetime -= dt;
            if (_lifetime <= 0) { ReturnToPool(); return; }

            RefreshOwnerBuff();

            _tickTimer -= dt;
            if (_tickTimer <= 0) {
                _tickTimer = _tickInterval;
                foreach (Node2D body in _area.GetOverlappingBodies()) {
                    ApplyTickTo(body);
                }
            }

            Modulate = new Color(1, 1, 1, 0.5f + 0.5f * Mathf.Sin((float)Time.GetTicksMsec() / 200f));
        }

        private void ApplyTickTo(Node2D body) {
            if (body is FTT.Characters.PlayerController pc) {
                if (pc.PlayerIndex == _ownerIndex) return;
                if (_damage > 0f) pc.ApplyDamage((int)_damage);
                if (_appliedStatus != StatusType.None && _statusDuration > 0f) {
                    pc.GetNodeOrNull<StatusController>("StatusController")
                        ?.ApplyStatus(_appliedStatus, _statusDuration, _statusIntensity);
                }
            } else if (body is FTT.Enemies.EnemyController enemy) {
                if (_damage > 0f) enemy.TakeDamage((int)_damage);
                if (_appliedStatus != StatusType.None && _statusDuration > 0f) {
                    enemy.ApplyStatusEffect(_appliedStatus, _statusDuration, _statusIntensity);
                }
            } else if (body is FTT.Characters.TrainingDummy) {
                Hurtbox hurtbox = body.GetNodeOrNull<Hurtbox>("Hurtbox");
                hurtbox?.TakeHit(new HitPayload {
                    AttackerIndex = _ownerIndex,
                    AttackID = "placeholder_zone",
                    HitboxID = "tick",
                    AttackClass = AttackClass.Special,
                    Damage = _damage,
                    Knockback = new Vector2(1, -1),
                    HitstunDuration = 0.1f,
                    HitOrigin = GlobalPosition,
                    AttackerFacingRight = true,
                    AppliedStatus = _appliedStatus,
                    StatusDuration = _statusDuration,
                    StatusIntensity = _statusIntensity,
                    ScreenShakeIntensity = 0.1f,
                    ScreenShakeDuration = 0.08f
                });
            }
        }

        private void RefreshOwnerBuff() {
            if (_ownerSpeedMultiplier <= 1f || _ownerPlayer == null || !IsInstanceValid(_ownerPlayer)) return;
            foreach (Node2D body in _area.GetOverlappingBodies()) {
                if (body == _ownerPlayer) {
                    _ownerPlayer.ApplyStorySpeedBuff(_ownerSpeedMultiplier, OwnerBuffRefreshSeconds);
                    return;
                }
            }
        }

        public void OnSpawn() {
            _lifetime = 0f;
            _tickTimer = 0f;
            Modulate = Colors.White;
        }

        public void OnDespawn() {
            if (_area != null) {
                _area.Monitoring = false;
                _area.Monitorable = false;
            }
            _lifetime = 0f;
            _tickTimer = 0f;
            _damage = 0f;
            _ownerIndex = -1;
            _appliedStatus = StatusType.None;
            _statusDuration = 0f;
            _statusIntensity = 1f;
            _ownerSpeedMultiplier = 1f;
            _ownerPlayer = null;
            Modulate = Colors.White;
        }
    }
}
