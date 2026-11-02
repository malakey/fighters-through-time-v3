using Godot;

namespace FTT.Combat {

    /// <summary>
    /// Grab pass 2026-10-31 (step A): the grip flash shown when a grab connects,
    /// in both modes. A pooled, code-drawn effect in the <see cref="BlockSparkEffect"/>
    /// pattern (<c>_Draw()</c> primitives, no textures, no particles, no text).
    /// Shape, so it never reads as a block: a bright ring that CONTRACTS onto the
    /// catch point, four inward grip ticks (a clamp) and a few short sparks.
    /// Warm white and gold. The bright core flash follows
    /// <see cref="FTT.Core.ComfortSettings.FullScreenFlashAllowed"/>. Presentation
    /// only; nothing here reads or writes simulation state.
    /// </summary>
    public partial class GrabSparkEffect : FTT.Core.PooledNode, FTT.Core.IPoolable {
        private const string ScenePath = "res://scenes/vfx/GrabSparkEffect.tscn";
        private const int WarmUpCount = 4;
        private const int MaxCapacity = 8;

        public const float Seconds = 0.25f;
        /// <summary>Number of inward grip ticks.</summary>
        public const int GripTickCount = 4;

        public static readonly Color GripColor = new(1f, 0.86f, 0.42f);

        private static PackedScene _scene;

        private float _age;
        private float _facing = 1f;
        private float _scale = 1f;
        private bool _flashAllowed = true;

        public bool IsActive { get; private set; }
        public float Duration => Seconds;
        public float Age => _age;
        public float Facing => _facing;

        /// <summary>
        /// Spawns one effect at <paramref name="worldPosition"/> under
        /// <paramref name="parent"/>. Registers its own pool.
        /// </summary>
        public static GrabSparkEffect Show(Vector2 worldPosition, bool facingRight, Node parent, float scale = 1f) {
            if (FTT.Core.PoolManager.Instance == null || parent == null) return null;
            _scene ??= GD.Load<PackedScene>(ScenePath);
            if (_scene == null) return null;
            FTT.Core.PoolManager.Instance.RegisterPool(
                _scene, WarmUpCount, MaxCapacity, FTT.Core.PoolOverflowPolicy.RecycleOldest);
            var effect = FTT.Core.PoolManager.Instance.Spawn(_scene, worldPosition, parent) as GrabSparkEffect;
            effect?.Initialize(worldPosition, facingRight, scale);
            return effect;
        }

        public void Initialize(Vector2 worldPosition, bool facingRight, float scale = 1f) {
            GlobalPosition = worldPosition;
            _facing = facingRight ? 1f : -1f;
            _scale = scale > 0f ? scale : 1f;
            _age = 0f;
            _flashAllowed = FTT.Core.ComfortSettings.FullScreenFlashAllowed;
            IsActive = true;
            ZIndex = 20;
            QueueRedraw();
        }

        public void OnSpawn() {
            _age = 0f;
            _facing = 1f;
            _scale = 1f;
            _flashAllowed = true;
            IsActive = false;
            Modulate = Colors.White;
        }

        public void OnDespawn() {
            _age = 0f;
            IsActive = false;
            Modulate = Colors.White;
        }

        public override void _Process(double delta) {
            if (!IsActive) return;
            _age += (float)delta;
            if (_age >= Seconds) {
                IsActive = false;
                ReturnToPool();
                return;
            }
            QueueRedraw();
        }

        private static Color WithAlpha(Color c, float a) => new(c.R, c.G, c.B, Mathf.Clamp(a, 0f, 1f));

        public override void _Draw() {
            if (!IsActive) return;
            float t = Mathf.Clamp(_age / Seconds, 0f, 1f);
            float fade = 1f - t;
            float s = _scale;
            float ease = 1f - (1f - t) * (1f - t);

            // A bright core, the first ~0.06 s, only where flashes are allowed.
            const float coreSeconds = 0.06f;
            if (_flashAllowed && _age < coreSeconds) {
                DrawCircle(Vector2.Zero, 10f * s, WithAlpha(Colors.White, 1f - _age / coreSeconds));
            }

            // The contracting ring: 44 -> 10 px, thickening as it closes.
            float ringR = Mathf.Lerp(44f, 10f, ease) * s;
            DrawArc(Vector2.Zero, ringR, 0f, Mathf.Tau, 36,
                WithAlpha(GripColor, fade), Mathf.Max(1f, Mathf.Lerp(2f, 5f, ease) * s));
            DrawArc(Vector2.Zero, ringR * 0.8f, 0f, Mathf.Tau, 28,
                WithAlpha(Colors.White, fade * 0.7f), Mathf.Max(1f, 1.5f * s));

            // Grip ticks: short radial strokes pointing inward, on the diagonals.
            for (int i = 0; i < GripTickCount; i++) {
                float a = Mathf.Pi / 4f + i * Mathf.Tau / GripTickCount;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float outer = ringR + 14f * s;
                float inner = Mathf.Max(4f * s, ringR - 2f * s);
                DrawLine(dir * outer, dir * inner, WithAlpha(Colors.White, fade), Mathf.Max(1f, 3f * fade * s));
            }

            // Three short sparks thrown forward from the catch point.
            for (int i = 0; i < 3; i++) {
                float a = (_facing > 0f ? 0f : Mathf.Pi) + (i - 1) * 0.55f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float start = (6f + 30f * ease) * s;
                DrawLine(dir * start, dir * (start + 10f * fade * s), WithAlpha(GripColor, fade), Mathf.Max(1f, 2f * fade * s));
            }
        }
    }
}
