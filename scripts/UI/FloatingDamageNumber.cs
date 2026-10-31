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
    ///
    /// <para>Three tints exist: a warded boss's refused "0" (A5, 2026-10-04);
    /// D8 (2026-10-11), <c>UIPalette.GoldBright</c> for a hit that landed on a
    /// mob's open Punish window, also drawn larger
    /// (<c>StoryEnemyCombatRules.PunishDamageNumberScale</c>), so the payoff
    /// never rests on colour alone; and CM-STORY 8.2 (2026-10-16),
    /// <c>UIPalette.DamageNumberReduced</c> for a hit the chain proration cut,
    /// drawn smaller (<c>StoryEnemyCombatRules.ProratedDamageNumberScale</c>).
    /// Every other number is white at scale 1.</para>
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

        public void Initialize(int damage, Vector2 position, Color? tint = null, float scale = 1f) {
            GlobalPosition = position;
            // D8: the non-colour channel of a punished hit (1 = the plain number).
            Scale = Vector2.One * (scale > 0f ? scale : 1f);
            _label.Text = damage.ToString();
            // White for every hit; the shimmer treatment is Package 10 art. A
            // tint marks a refused hit (a warded boss's "0", A5 2026-10-04) or a
            // punished one (GoldBright, D8 2026-10-11).
            _label.AddThemeColorOverride("font_color", tint ?? Colors.White);
            _lifetime = MaxLifetime;
            _velocity = new Vector2(0f, -RiseUnitsPerSecond * PixelsPerUnit);
        }

        public static FloatingDamageNumber Show(
            int damage, Vector2 position, Node parent = null, Color? tint = null, float scale = 1f) {
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
            number?.Initialize(damage, position, tint, scale);
            return number;
        }

        /// <summary>
        /// MM-FX (meter and Defy update, plan section 2.11): a word instead of a number, such
        /// as the Defy's "DEFIED". It is a mechanic cue, so it shows even when damage numbers
        /// are switched off. Same pool, motion and fade as a damage number.
        /// </summary>
        public static FloatingDamageNumber ShowText(
            string text, Vector2 position, Node parent = null, Color? tint = null, float scale = 1f) {
            if (string.IsNullOrEmpty(text) || FTT.Core.PoolManager.Instance == null) return null;
            _scene ??= GD.Load<PackedScene>(ScenePath);
            if (_scene == null) return null;
            FTT.Core.PoolManager.Instance.RegisterPool(
                _scene, WarmUpCount, MaxCapacity, FTT.Core.PoolOverflowPolicy.RecycleOldest);
            var number = FTT.Core.PoolManager.Instance.Spawn(_scene, position, parent) as FloatingDamageNumber;
            number?.Initialize(0, position, tint, scale);
            if (number?._label != null) number._label.Text = text;
            return number;
        }

        /// <summary>The text on screen right now. Test seam.</summary>
        public string DisplayedText => _label?.Text ?? "";

        public void OnSpawn() {
            _lifetime = MaxLifetime;
            _velocity = Vector2.Zero;
            Modulate = Colors.White;
            Scale = Vector2.One;
        }

        public void OnDespawn() {
            _lifetime = 0f;
            _velocity = Vector2.Zero;
            Modulate = Colors.White;
            Scale = Vector2.One;
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
