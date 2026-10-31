using Godot;

namespace FTT.Combat {

    /// <summary>
    /// MM-FX (meter and Defy update 2026-10-31, plan section 2.11; ledger
    /// <c>USER-DEFY-TIME-BREAK-PRESENTATION</c>, look <c>PROVISIONAL-DEFY-PRESENTATION</c>).
    /// The Defy's "full time-break" ring: a pooled, code-drawn gold ring that expands from
    /// the Defier to the blast radius over <see cref="Seconds"/>. The
    /// <see cref="BlockSparkEffect"/> pattern: <c>_Draw()</c> primitives, no textures, no
    /// particles, the pool registered by <see cref="Show"/>. Presentation only. The ring
    /// itself never flashes; the bright inner fill and the white core follow
    /// <see cref="FTT.Core.ComfortSettings.FullScreenFlashAllowed"/>.
    /// </summary>
    public partial class DefyBurstEffect : FTT.Core.PooledNode, FTT.Core.IPoolable {
        private const string ScenePath = "res://scenes/vfx/DefyBurstEffect.tscn";
        private const int WarmUpCount = 2;
        private const int MaxCapacity = 6;

        /// <summary>The ring's full life.</summary>
        public const float Seconds = 0.4f;
        /// <summary>Story's blast radius in pixels (2.5 units at 60 px per unit).</summary>
        public const float StoryRadiusPixels = 150f;
        /// <summary>Fighter's blast radius in pixels (2.5 units at 62.5 px per unit).</summary>
        public const float FighterRadiusPixels = 156.25f;

        public static readonly Color RingColor = FTT.UI.UIPalette.GoldBright;

        private static PackedScene _scene;

        private float _age;
        private float _radius = StoryRadiusPixels;
        private bool _flashAllowed = true;

        /// <summary>True from <see cref="Initialize"/> until the effect returns to its pool.</summary>
        public bool IsActive { get; private set; }
        /// <summary>The radius the ring reaches, in pixels.</summary>
        public float Radius => _radius;
        /// <summary>Seconds since it was shown.</summary>
        public float Age => _age;
        /// <summary>Whether the bright fills were allowed when it was shown.</summary>
        public bool FillsAllowed => _flashAllowed;

        /// <summary>
        /// Spawns one ring at <paramref name="worldPosition"/> under <paramref name="parent"/>
        /// (the level or arena, never the tree root). Registers its own pool.
        /// </summary>
        public static DefyBurstEffect Show(Vector2 worldPosition, Node parent, float radiusPixels = StoryRadiusPixels) {
            if (FTT.Core.PoolManager.Instance == null || parent == null) return null;
            _scene ??= GD.Load<PackedScene>(ScenePath);
            if (_scene == null) return null;
            FTT.Core.PoolManager.Instance.RegisterPool(
                _scene, WarmUpCount, MaxCapacity, FTT.Core.PoolOverflowPolicy.RecycleOldest);
            var effect = FTT.Core.PoolManager.Instance.Spawn(_scene, worldPosition, parent) as DefyBurstEffect;
            effect?.Initialize(worldPosition, radiusPixels);
            return effect;
        }

        public void Initialize(Vector2 worldPosition, float radiusPixels) {
            GlobalPosition = worldPosition;
            _radius = radiusPixels > 0f ? radiusPixels : StoryRadiusPixels;
            _age = 0f;
            _flashAllowed = FTT.Core.ComfortSettings.FullScreenFlashAllowed;
            IsActive = true;
            ZIndex = 20;
            QueueRedraw();
        }

        public void OnSpawn() {
            _age = 0f;
            _radius = StoryRadiusPixels;
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

        public override void _Draw() {
            if (!IsActive) return;
            float t = Mathf.Clamp(_age / Seconds, 0f, 1f);
            float fade = 1f - t;
            float ease = 1f - (1f - t) * (1f - t);

            // The soft inner fill, only where flashes are allowed.
            if (_flashAllowed) {
                DrawCircle(Vector2.Zero, _radius * ease, WithAlpha(RingColor, 0.18f * fade));
            }
            // The leading ring and a thinner white one just behind it.
            DrawArc(Vector2.Zero, Mathf.Max(2f, _radius * ease), 0f, Mathf.Tau, 48,
                WithAlpha(RingColor, fade), Mathf.Max(1f, 8f * fade));
            DrawArc(Vector2.Zero, Mathf.Max(2f, _radius * ease * 0.82f), 0f, Mathf.Tau, 40,
                WithAlpha(Colors.White, fade * 0.8f), Mathf.Max(1f, 3f * fade));
            // A brief bright core, the first ~0.06 s.
            const float coreSeconds = 0.06f;
            if (_flashAllowed && _age < coreSeconds) {
                DrawCircle(Vector2.Zero, 18f, WithAlpha(Colors.White, 1f - _age / coreSeconds));
            }
        }

        private static Color WithAlpha(Color c, float a) => new(c.R, c.G, c.B, Mathf.Clamp(a, 0f, 1f));
    }
}
