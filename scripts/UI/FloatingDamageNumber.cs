using Godot;

namespace FTT.UI {
    /// <summary>
    /// Pooled floating damage number, motion and fade per design :2630-2631
    /// (audit Low, 2026-08-08): white text that rises with a quick initial speed
    /// (<c>velocityY = 4.5</c> world units/second) decelerated by a linear air
    /// drag of <c>2.0</c>, fading linearly from 1.0 to 0.0 alpha starting at
    /// 0.5 s of the 1.0 s life. The previous ballistic arc (gravity), whole-life
    /// fade, and the undesigned "&gt;=15 damage renders red" rule are gone — the
    /// cyan chronal shimmer shader is Package 10 art.
    /// </summary>
    public partial class FloatingDamageNumber : FTT.Core.PooledNode, FTT.Core.IPoolable {
        private const string ScenePath = "res://scenes/ui/FloatingDamageNumber.tscn";
        private const int WarmUpCount = 20;
        private const int MaxCapacity = 64;

        /// <summary>Design: 1.0 s life; fade begins at 0.5 s.</summary>
        private const float MaxLifetime = 1.0f;
        private const float FadeStartSeconds = 0.5f;

        /// <summary>Design: 4.5 world units/s initial rise. Story renders in pixels;
        /// the repository's world scale is 62.5 px per unit (FighterStageConformance).</summary>
        private const float RiseUnitsPerSecond = 4.5f;
        private const float PixelsPerUnit = 62.5f;

        /// <summary>Design: linear air drag coefficient of 2.0 (per second).</summary>
        private const float LinearDragPerSecond = 2.0f;

        private static PackedScene _scene;
        private Label _label;
        private float _lifetime;
        private Vector2 _velocity;

        public override void _Ready() {
            _label = new Label();
            _label.HorizontalAlignment = HorizontalAlignment.Center;
            AddChild(_label);
        }

        public void Initialize(int damage, Vector2 position) {
            GlobalPosition = position;
            _label.Text = damage.ToString();
            // White for every hit; the shimmer treatment is Package 10 art.
            _label.AddThemeColorOverride("font_color", Colors.White);
            _lifetime = MaxLifetime;
            _velocity = new Vector2(0f, -RiseUnitsPerSecond * PixelsPerUnit);
        }

        public static FloatingDamageNumber Show(int damage, Vector2 position, Node parent = null) {
            if (FTT.Core.SaveManager.Instance?.GlobalData?.DamageNumbersVisible == false) return null;
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
            // Decelerating rise: linear drag proportional to current velocity.
            _velocity -= _velocity * Mathf.Min(1f, LinearDragPerSecond * dt);

            // Fully opaque until FadeStartSeconds have elapsed, then a linear fade
            // across the remaining life.
            float elapsed = MaxLifetime - _lifetime;
            float alpha = elapsed <= FadeStartSeconds
                ? 1f
                : Mathf.Clamp(_lifetime / (MaxLifetime - FadeStartSeconds), 0f, 1f);
            Modulate = new Color(1, 1, 1, alpha);
        }
    }
}
