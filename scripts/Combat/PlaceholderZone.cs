using FTT.Core;
using Godot;

namespace FTT.Combat {

    public partial class PlaceholderZone : PooledNode, IPoolable {
        private float _lifetime;
        private float _tickTimer;
        private float _tickInterval;
        private float _damage;
        private int _ownerIndex;
        private Area2D _area;
        private ColorRect _visual;
        private ColorRect _border;
        private CollisionShape2D _shape;

        public override void _Ready() => AddToGroup("story_zone");

        public void Setup(float damage, float lifetime, float tickInterval, int ownerIndex,
                          Color color, float radius = 60f) {
            _lifetime = lifetime;
            _tickInterval = tickInterval;
            _damage = damage;
            _ownerIndex = ownerIndex;

            EnsureNodes();
            _visual.Size = new Vector2(radius * 2, radius * 2);
            _visual.Position = new Vector2(-radius, -radius);
            _visual.Color = new Color(color.R, color.G, color.B, 0.3f);
            _border.Size = new Vector2(radius * 2, 4);
            _border.Position = new Vector2(-radius, -radius);
            _border.Color = color;
            ((CircleShape2D)_shape.Shape).Radius = radius;
            _area.CollisionLayer = CollisionLayers.PlayerHitbox;
            _area.CollisionMask = CollisionLayers.PlayerHitboxMask;
            _area.Monitoring = true;
            _area.Monitorable = true;
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

            _tickTimer -= dt;
            if (_tickTimer <= 0) {
                _tickTimer = _tickInterval;
                foreach (Node2D body in _area.GetOverlappingBodies()) {
                    if (body is FTT.Characters.PlayerController pc && pc.PlayerIndex != _ownerIndex) {
                        pc.ApplyDamage((int)_damage);
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
                            StatusIntensity = 1f,
                            ScreenShakeIntensity = 0.1f,
                            ScreenShakeDuration = 0.08f
                        });
                    }
                }
            }

            Modulate = new Color(1, 1, 1, 0.5f + 0.5f * Mathf.Sin((float)Time.GetTicksMsec() / 200f));
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
            Modulate = Colors.White;
        }
    }
}
