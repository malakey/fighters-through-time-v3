using Godot;

namespace FTT.UI {
    public partial class FloatingDamageNumber : FTT.Core.PooledNode, FTT.Core.IPoolable {
        private const string ScenePath = "res://scenes/ui/FloatingDamageNumber.tscn";
        private const int WarmUpCount = 20;
        private const int MaxCapacity = 64;
        private static PackedScene _scene;
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

        public static FloatingDamageNumber Show(int damage, Vector2 position, Node parent = null) {
            if (FTT.Core.PoolManager.Instance == null) return null;
            _scene ??= GD.Load<PackedScene>(ScenePath);
            if (_scene == null) return null;

            FTT.Core.PoolManager.Instance.RegisterPool(
                _scene,
                WarmUpCount,
                MaxCapacity,
                FTT.Core.PoolOverflowPolicy.RecycleOldest);
            var number = FTT.Core.PoolManager.Instance.Spawn(_scene, position, parent) as FloatingDamageNumber;
            number?.Initialize(damage, position);
            return number;
        }

        public void OnSpawn() {
            _lifetime = MaxLifetime;
            _velocity = Vector2.Zero;
            Modulate = Colors.White;
        }

        public void OnDespawn() {
            _lifetime = 0f;
            _velocity = Vector2.Zero;
            Modulate = Colors.White;
            if (_label != null) {
                _label.Text = "";
                _label.RemoveThemeColorOverride("font_color");
            }
        }

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
