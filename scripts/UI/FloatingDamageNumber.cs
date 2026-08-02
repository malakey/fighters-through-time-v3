using Godot;

namespace FTT.UI {
    public partial class FloatingDamageNumber : FTT.Core.PooledNode, FTT.Core.IPoolable {
        private Label _label;
        private float _lifetime;
        private const float MaxLifetime = 1.0f;
        private Vector2 _velocity;

        public override void _Ready() {
            _label = new Label();
            _label.HorizontalAlignment = HorizontalAlignment.Center;
            AddChild(_label);
        }

        public void Initialize(int damage, Vector2 position) {
            GlobalPosition = position;
            _label.Text = damage.ToString();
            _label.AddThemeColorOverride("font_color", damage >= 15 ? new Color(1, 0.2f, 0.2f) : Colors.White);
            _lifetime = MaxLifetime;
            _velocity = new Vector2(GD.Randf() * 40f - 20f, -120f);
        }

        public void OnSpawn() { _lifetime = MaxLifetime; Modulate = Colors.White; }
        public void OnDespawn() { }

        public override void _Process(double delta) {
            float dt = (float)delta;
            _lifetime -= dt;
            if (_lifetime <= 0) { ReturnToPool(); return; }

            GlobalPosition += _velocity * dt;
            _velocity.Y += 100f * dt;
            Modulate = new Color(1, 1, 1, _lifetime / MaxLifetime);
        }
    }
}
