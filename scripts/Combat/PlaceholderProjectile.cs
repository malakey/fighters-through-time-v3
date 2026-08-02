using Godot;

namespace FTT.Combat {

    public partial class PlaceholderProjectile : Node2D {
        private float _speed;
        private bool _movingRight;
        private float _lifetime;
        private Hitbox _hitbox;

        public void Setup(float damage, Vector2 knockback, float speed, bool movingRight,
                          int ownerIndex, Color color, Vector2 size = default, float lifetime = 3f) {
            if (size == default) size = new Vector2(24, 12);
            _speed = speed;
            _movingRight = movingRight;
            _lifetime = lifetime;

            var visual = new ColorRect();
            visual.Size = size;
            visual.Position = -size / 2;
            visual.Color = color;
            AddChild(visual);

            var trail = new ColorRect();
            trail.Size = new Vector2(size.X * 0.6f, size.Y * 0.5f);
            trail.Position = new Vector2(movingRight ? -size.X * 0.6f : size.X * 0.5f, -size.Y * 0.25f);
            trail.Color = new Color(color.R, color.G, color.B, 0.4f);
            AddChild(trail);

            _hitbox = new Hitbox();
            _hitbox.Damage = damage;
            _hitbox.KnockbackForce = knockback;
            _hitbox.OwnerPlayerIndex = ownerIndex;
            _hitbox.CollisionLayer = 4;
            _hitbox.CollisionMask = 8;
            _hitbox.Monitorable = true;
            var shape = new CollisionShape2D();
            var rect = new RectangleShape2D();
            rect.Size = size;
            shape.Shape = rect;
            _hitbox.AddChild(shape);
            AddChild(_hitbox);
            _hitbox.Activate();
        }

        public override void _PhysicsProcess(double delta) {
            float dt = (float)delta;
            _lifetime -= dt;
            if (_lifetime <= 0) { QueueFree(); return; }

            var pos = GlobalPosition;
            pos.X += (_movingRight ? _speed : -_speed) * dt;
            GlobalPosition = pos;

            Modulate = new Color(1, 1, 1, Mathf.Min(1f, _lifetime * 2f));
        }
    }

    public partial class PlaceholderZone : Node2D {
        private float _lifetime;
        private float _tickTimer;
        private float _tickInterval;
        private float _damage;
        private int _ownerIndex;
        private Area2D _area;

        public void Setup(float damage, float lifetime, float tickInterval, int ownerIndex,
                          Color color, float radius = 60f) {
            _lifetime = lifetime;
            _tickInterval = tickInterval;
            _damage = damage;
            _ownerIndex = ownerIndex;

            var visual = new ColorRect();
            visual.Size = new Vector2(radius * 2, radius * 2);
            visual.Position = new Vector2(-radius, -radius);
            visual.Color = new Color(color.R, color.G, color.B, 0.3f);
            AddChild(visual);

            var border = new ColorRect();
            border.Size = new Vector2(radius * 2, 4);
            border.Position = new Vector2(-radius, -radius);
            border.Color = color;
            AddChild(border);

            _area = new Area2D();
            _area.CollisionLayer = 4;
            _area.CollisionMask = 8;
            _area.Monitoring = true;
            _area.Monitorable = true;
            var shape = new CollisionShape2D();
            var circle = new CircleShape2D();
            circle.Radius = radius;
            shape.Shape = circle;
            _area.AddChild(shape);
            AddChild(_area);
        }

        public override void _PhysicsProcess(double delta) {
            float dt = (float)delta;
            _lifetime -= dt;
            if (_lifetime <= 0) { QueueFree(); return; }

            _tickTimer -= dt;
            if (_tickTimer <= 0) {
                _tickTimer = _tickInterval;
                foreach (var body in _area.GetOverlappingBodies()) {
                    if (body is FTT.Characters.PlayerController pc && pc.PlayerIndex != _ownerIndex) {
                        pc.ApplyDamage((int)_damage);
                    } else if (body is FTT.Characters.TrainingDummy) {
                        var hurtbox = body.GetNodeOrNull<Hurtbox>("Hurtbox");
                        hurtbox?.TakeHit(_damage, new Vector2(1, -1), 0.1f, GlobalPosition);
                    }
                }
            }

            Modulate = new Color(1, 1, 1, 0.5f + 0.5f * Mathf.Sin((float)Time.GetTicksMsec() / 200f));
        }
    }
}
