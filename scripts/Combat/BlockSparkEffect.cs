using Godot;

namespace FTT.Combat {

    /// <summary>FU-BLOCK: which block outcome a <see cref="BlockSparkEffect"/> announces.</summary>
    public enum BlockSparkKind {
        /// <summary>A charge was spent and the hit absorbed: a cyan-white shield arc, a facet flash and sparks.</summary>
        Ordinary = 0,
        /// <summary>A perfect block: the same arc and sparks in gold-white, plus a double ring and a core flash.</summary>
        Perfect = 1,
        /// <summary>A guard break: red-orange shards and a broken ring, no arc.</summary>
        Shatter = 2
    }

    /// <summary>
    /// FU-BLOCK (feel and UI update 2026-10-25, user request "a clear indicator
    /// that the block was successful"; ledger <c>USER-BLOCK-SUCCESS-EFFECT</c>,
    /// look <c>PROVISIONAL-BLOCK-SPARK-LOOK</c>). A pooled, code-drawn effect
    /// (the <c>TelegraphGlyph</c> pattern: <c>_Draw()</c> primitives, no textures,
    /// no particles) shown at the blocker's front in both Story and Fighter.
    /// The three kinds differ in SHAPE as well as colour: an arc (ordinary), an
    /// arc plus rings (perfect), shards plus a broken ring (shatter). Presentation
    /// only; nothing here reads or writes simulation state. Nothing strobes: every
    /// element fades once and the bright core flash and fill alpha follow
    /// <see cref="FTT.Core.ComfortSettings.FullScreenFlashAllowed"/>.
    /// </summary>
    public partial class BlockSparkEffect : FTT.Core.PooledNode, FTT.Core.IPoolable {
        private const string ScenePath = "res://scenes/vfx/BlockSparkEffect.tscn";
        private const int WarmUpCount = 4;
        private const int MaxCapacity = 16;

        public const float OrdinarySeconds = 0.28f;
        public const float PerfectSeconds = 0.40f;
        public const float ShatterSeconds = 0.45f;

        public static readonly Color OrdinaryColor = new(0.65f, 0.95f, 1f);
        public static readonly Color PerfectColor = new(1f, 0.92f, 0.35f); // UIPalette.GoldBright family
        public static readonly Color ShatterColor = new(1f, 0.38f, 0.12f);

        private const float ArcHalfAngle = Mathf.Pi * 55f / 180f; // ~110 degrees across
        private static readonly float[] SparkJitter = { 0.00f, 0.55f, -0.40f, 0.30f, -0.75f, 0.80f, -0.15f, 0.65f, -0.60f, 0.15f };

        private static PackedScene _scene;

        private float _age;
        private float _duration;
        private float _facing = 1f;
        private float _scale = 1f;
        private bool _flashAllowed = true;

        /// <summary>The outcome this live effect announces.</summary>
        public BlockSparkKind Kind { get; private set; }
        /// <summary>True from <see cref="Initialize"/> until the effect returns to its pool.</summary>
        public bool IsActive { get; private set; }
        /// <summary>The effect's full duration in seconds.</summary>
        public float Duration => _duration;
        /// <summary>Seconds since it was shown.</summary>
        public float Age => _age;
        /// <summary>+1 facing right, -1 facing left.</summary>
        public float Facing => _facing;

        public static float DurationFor(BlockSparkKind kind) => kind switch {
            BlockSparkKind.Perfect => PerfectSeconds,
            BlockSparkKind.Shatter => ShatterSeconds,
            _ => OrdinarySeconds
        };

        /// <summary>
        /// Spawns one effect at <paramref name="worldPosition"/> under
        /// <paramref name="parent"/> (the level or arena, never the tree root).
        /// Registers its own pool, as <c>FloatingDamageNumber.Show</c> does.
        /// </summary>
        public static BlockSparkEffect Show(BlockSparkKind kind, Vector2 worldPosition, bool facingRight,
            Node parent, float scale = 1f) {
            if (FTT.Core.PoolManager.Instance == null || parent == null) return null;
            _scene ??= GD.Load<PackedScene>(ScenePath);
            if (_scene == null) return null;
            FTT.Core.PoolManager.Instance.RegisterPool(
                _scene, WarmUpCount, MaxCapacity, FTT.Core.PoolOverflowPolicy.RecycleOldest);
            var effect = FTT.Core.PoolManager.Instance.Spawn(_scene, worldPosition, parent) as BlockSparkEffect;
            effect?.Initialize(kind, worldPosition, facingRight, scale);
            return effect;
        }

        public void Initialize(BlockSparkKind kind, Vector2 worldPosition, bool facingRight, float scale = 1f) {
            GlobalPosition = worldPosition;
            Kind = kind;
            _facing = facingRight ? 1f : -1f;
            _scale = scale > 0f ? scale : 1f;
            _duration = DurationFor(kind);
            _age = 0f;
            _flashAllowed = FTT.Core.ComfortSettings.FullScreenFlashAllowed;
            IsActive = true;
            ZIndex = 20;
            QueueRedraw();
        }

        public void OnSpawn() {
            _age = 0f;
            _duration = OrdinarySeconds;
            _facing = 1f;
            _scale = 1f;
            _flashAllowed = true;
            Kind = BlockSparkKind.Ordinary;
            IsActive = false;
            Modulate = Colors.White;
        }

        public void OnDespawn() {
            _age = 0f;
            _duration = 0f;
            IsActive = false;
            Kind = BlockSparkKind.Ordinary;
            Modulate = Colors.White;
        }

        public override void _Process(double delta) {
            if (!IsActive) return;
            _age += (float)delta;
            if (_age >= _duration) {
                IsActive = false;
                ReturnToPool();
                return;
            }
            QueueRedraw();
        }

        public override void _Draw() {
            if (!IsActive || _duration <= 0f) return;
            float t = Mathf.Clamp(_age / _duration, 0f, 1f);
            switch (Kind) {
                case BlockSparkKind.Shatter: DrawShatter(t); break;
                case BlockSparkKind.Perfect: DrawShield(t, PerfectColor, perfect: true); break;
                default: DrawShield(t, OrdinaryColor, perfect: false); break;
            }
        }

        private static Color WithAlpha(Color c, float a) => new(c.R, c.G, c.B, Mathf.Clamp(a, 0f, 1f));

        private void DrawShield(float t, Color color, bool perfect) {
            float fade = 1f - t;
            float baseAngle = _facing > 0f ? 0f : Mathf.Pi;
            float ease = 1f - (1f - t) * (1f - t);
            float s = _scale;

            // Facet flash: a translucent hexagon inside the arc, the first ~0.08 s.
            float facetSeconds = 0.08f;
            if (_age < facetSeconds) {
                float fa = 1f - _age / facetSeconds;
                float fillAlpha = (_flashAllowed ? 0.55f : 0.27f) * fa;
                var hex = new Vector2[6];
                for (int i = 0; i < 6; i++) {
                    float a = Mathf.Pi / 6f + i * Mathf.Tau / 6f;
                    hex[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 16f * s;
                }
                DrawColoredPolygon(hex, WithAlpha(Colors.White, fillAlpha));
            }

            // Shield arc: radius 22 -> 38, the line thinning 6 -> 1 while it fades.
            float radius = Mathf.Lerp(22f, 38f, ease) * s;
            float width = Mathf.Lerp(6f, 1f, t) * s;
            DrawArc(Vector2.Zero, radius, baseAngle - ArcHalfAngle, baseAngle + ArcHalfAngle, 20,
                WithAlpha(color, fade), Mathf.Max(1f, width));
            DrawArc(Vector2.Zero, radius - 4f * s, baseAngle - ArcHalfAngle * 0.8f, baseAngle + ArcHalfAngle * 0.8f, 16,
                WithAlpha(Colors.White, fade * 0.7f), Mathf.Max(1f, width * 0.4f));

            if (perfect) {
                // A full double ring, expanding to ~70 px.
                float r1 = Mathf.Lerp(12f, 70f, ease) * s;
                float r2 = Mathf.Lerp(6f, 52f, ease) * s;
                DrawArc(Vector2.Zero, r1, 0f, Mathf.Tau, 40, WithAlpha(color, fade * 0.9f), Mathf.Max(1f, 3f * fade * s));
                DrawArc(Vector2.Zero, r2, 0f, Mathf.Tau, 36, WithAlpha(Colors.White, fade * 0.8f), Mathf.Max(1f, 2f * fade * s));
                // A brief bright core, only where flashes are allowed.
                float coreSeconds = 0.06f;
                if (_flashAllowed && _age < coreSeconds) {
                    DrawCircle(Vector2.Zero, 14f * s, WithAlpha(Colors.White, 1f - _age / coreSeconds));
                }
            }

            // Sparks flying out from the contact point and shrinking.
            int count = perfect ? 10 : 6;
            for (int i = 0; i < count; i++) {
                float a = baseAngle + SparkJitter[i % SparkJitter.Length] * (Mathf.Pi * 0.5f);
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float start = (8f + 46f * ease * (0.75f + 0.05f * (i % 5))) * s;
                float len = 14f * (1f - t) * s;
                DrawLine(dir * start, dir * (start + len), WithAlpha(i % 2 == 0 ? Colors.White : color, fade),
                    Mathf.Max(1f, 2.5f * (1f - t) * s));
            }
        }

        private void DrawShatter(float t) {
            float fade = 1f - t;
            float s = _scale;
            float ease = 1f - (1f - t) * (1f - t);

            // A broken (dashed) ring expanding outward.
            float ringR = Mathf.Lerp(12f, 46f, ease) * s;
            for (int i = 0; i < 8; i++) {
                float a0 = i * Mathf.Tau / 8f + 0.1f;
                DrawArc(Vector2.Zero, ringR, a0, a0 + Mathf.Tau / 8f * 0.55f, 6,
                    WithAlpha(ShatterColor, fade), Mathf.Max(1f, 3f * fade * s));
            }

            // Triangular shards flying out under gravity, spinning.
            const int shards = 7;
            for (int i = 0; i < shards; i++) {
                float a = -Mathf.Pi * 0.5f + (i - (shards - 1) * 0.5f) * (Mathf.Tau / shards) * 0.95f
                    + (_facing > 0f ? 0.25f : -0.25f);
                float speed = (90f + 14f * (i % 3)) * s;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 pos = dir * speed * _age + new Vector2(0f, 0.5f * 420f * s * _age * _age);
                float rot = (i % 2 == 0 ? 1f : -1f) * (7f + i) * _age;
                float size = (7f - 0.5f * (i % 3)) * s;
                var tri = new Vector2[3];
                for (int v = 0; v < 3; v++) {
                    float va = rot + v * Mathf.Tau / 3f;
                    float vr = v == 0 ? size * 1.5f : size;
                    tri[v] = pos + new Vector2(Mathf.Cos(va), Mathf.Sin(va)) * vr;
                }
                DrawColoredPolygon(tri, WithAlpha(i % 2 == 0 ? ShatterColor : new Color(1f, 0.65f, 0.25f), fade));
            }
        }
    }
}
