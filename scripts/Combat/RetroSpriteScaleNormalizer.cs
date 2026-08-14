using System.Collections.Generic;
using Godot;

namespace FTT.Combat {

    /// <summary>
    /// Evens out the per-animation figure-scale drift in the generated retro
    /// sprite sheets. The sheets share a cell grid and a 3 px feet baseline,
    /// but the drawn figure's height varies wildly between animations of the
    /// same sheet (an enemy's attack rows are often drawn at half the idle
    /// figure's scale). Baking the fix into the PNGs is impossible without
    /// clipping — corrected art would exceed the cell width — so this node
    /// hangs under a body AnimatedSprite2D, measures each animation's opaque
    /// art height once per SpriteFrames (cached process-wide), and rescales
    /// the sprite per animation so the figure reads at a consistent size,
    /// keeping the feet anchored where the scene author put them.
    ///
    /// A pose table per figure kind keeps legitimately compressed poses
    /// (crouch, rolls, lying death frames) from being inflated to standing
    /// height. Facing must use FlipH, never a negative Scale.X — this node
    /// owns Scale while attached.
    /// </summary>
    public partial class RetroSpriteScaleNormalizer : Node {
        public enum FigureKind { Character, Enemy, Boss }

        /// <summary>The generators leave the art's lowest opaque row this far above the cell bottom.</summary>
        public const float FeetGapPixels = 3f;

        /// <summary>Never boost an animation beyond this, however small its art measures.</summary>
        public const float MaxBoost = 2.0f;

        private static readonly StringName IdleAnimation = "idle";

        private static readonly Dictionary<string, float> CharacterPoseFractions = new() {
            ["idle"] = 1f, ["run"] = 0.93f, ["jump"] = 0.95f, ["fall"] = 0.8f,
            ["skid"] = 0.9f, ["crouch"] = 0.85f,
            ["roll_startup"] = 0.65f, ["roll"] = 0.55f, ["roll_recovery"] = 0.85f,
            ["ledge_hang"] = 1f, ["ledge_pull_up"] = 0.85f, ["ledge_drop"] = 0.9f,
            ["basic_attack_1"] = 0.95f, ["basic_attack_2"] = 0.95f, ["basic_attack_3"] = 1f,
            ["up_attack"] = 1f, ["down_attack"] = 0.9f,
            ["block"] = 1f, ["hitstun"] = 0.9f, ["dazed"] = 1f, ["death"] = 0.5f,
            ["respawn"] = 1f, ["victory"] = 1f, ["defeat"] = 1f,
            ["special_1"] = 0.95f, ["special_2"] = 0.95f,
            ["movement_ability"] = 0.95f, ["ultimate"] = 0.95f
        };

        private static readonly Dictionary<string, float> EnemyPoseFractions = new() {
            ["idle"] = 1f, ["patrol"] = 0.95f, ["attack"] = 0.95f,
            ["elite_attack"] = 0.95f, ["hitstun"] = 0.9f, ["death"] = 0.55f
        };

        private static readonly Dictionary<string, float> BossPoseFractions = new() {
            ["idle"] = 1f, ["move"] = 0.95f, ["melee_attack"] = 0.95f,
            ["ranged_attack"] = 0.95f, ["phase_transition"] = 1f, ["death"] = 0.6f
        };

        /// <summary>Per-SpriteFrames factor tables, measured once per process.</summary>
        private static readonly Dictionary<ulong, Dictionary<StringName, float>> FactorCache = new();

        [Export] public FigureKind Kind = FigureKind.Character;

        private AnimatedSprite2D _sprite;
        private Vector2 _baseScale = Vector2.One;
        private bool _baseCaptured;
        private ulong _appliedFramesId;
        private StringName _appliedAnimation;

        /// <summary>
        /// Adds a normalizer under <paramref name="sprite"/> (idempotent — a
        /// pooled scene keeps its instance across spawn cycles).
        /// </summary>
        public static RetroSpriteScaleNormalizer Attach(AnimatedSprite2D sprite, FigureKind kind) {
            if (sprite == null) return null;
            foreach (Node child in sprite.GetChildren()) {
                if (child is RetroSpriteScaleNormalizer existing) {
                    existing.Kind = kind;
                    return existing;
                }
            }
            var normalizer = new RetroSpriteScaleNormalizer { Name = "ScaleNormalizer", Kind = kind };
            sprite.AddChild(normalizer);
            return normalizer;
        }

        public override void _EnterTree() {
            _sprite = GetParentOrNull<AnimatedSprite2D>();
            if (_sprite == null) return;
            _sprite.AnimationChanged += Apply;
            // FrameChanged also covers a SpriteFrames swap that keeps the same
            // animation name (pooled enemies reused as a different roster ID).
            _sprite.FrameChanged += Apply;
        }

        public override void _ExitTree() {
            if (_sprite == null) return;
            _sprite.AnimationChanged -= Apply;
            _sprite.FrameChanged -= Apply;
            _sprite = null;
        }

        public override void _Ready() {
            CaptureBase();
            Apply();
        }

        /// <summary>
        /// Records the author's intended scale. The feet line is not captured:
        /// every body sprite in this project follows the feet-on-origin
        /// convention (the sprite's parent sits at the floor-contact point),
        /// so the normalizer anchors the art's bottom row at parent y = 0 —
        /// which also self-corrects when a pooled boss swaps from the
        /// placeholder sheet's cell height to an authored one.
        /// </summary>
        private void CaptureBase() {
            if (_baseCaptured || _sprite == null) return;
            _baseScale = _sprite.Scale;
            _baseCaptured = true;
        }

        public void Apply() {
            if (_sprite == null || !_baseCaptured) return;
            SpriteFrames frames = _sprite.SpriteFrames;
            if (frames == null) return;
            StringName animation = _sprite.Animation;
            ulong framesId = frames.GetInstanceId();
            if (framesId == _appliedFramesId && animation == _appliedAnimation) return;

            float factor = ResolveFactor(frames, animation);
            Vector2 scale = _baseScale * factor;
            float cellH = CellHeight(frames, animation);
            _sprite.Scale = scale;
            _sprite.Position = new Vector2(
                _sprite.Position.X,
                -(cellH * 0.5f - FeetGapPixels) * scale.Y);
            _appliedFramesId = framesId;
            _appliedAnimation = animation;
        }

        private Dictionary<string, float> PoseFractions => Kind switch {
            FigureKind.Enemy => EnemyPoseFractions,
            FigureKind.Boss => BossPoseFractions,
            _ => CharacterPoseFractions
        };

        private float ResolveFactor(SpriteFrames frames, StringName animation) {
            Dictionary<StringName, float> table = FactorTable(frames);
            return table.TryGetValue(animation, out float factor) ? factor : 1f;
        }

        private Dictionary<StringName, float> FactorTable(SpriteFrames frames) {
            ulong id = frames.GetInstanceId();
            if (FactorCache.TryGetValue(id, out Dictionary<StringName, float> cached)) return cached;

            var table = new Dictionary<StringName, float>();
            float reference = frames.HasAnimation(IdleAnimation)
                ? MeasureArtHeight(frames, IdleAnimation)
                : 0f;
            if (reference > 0f) {
                Dictionary<string, float> fractions = PoseFractions;
                foreach (string name in frames.GetAnimationNames()) {
                    if (!fractions.TryGetValue(name, out float fraction)) continue;
                    float measured = MeasureArtHeight(frames, name);
                    if (measured <= 0f) continue;
                    table[name] = Mathf.Clamp(fraction * reference / measured, 1f, MaxBoost);
                }
            }
            FactorCache[id] = table;
            return table;
        }

        /// <summary>Tallest opaque art across the animation's frames, in cell pixels.</summary>
        private static float MeasureArtHeight(SpriteFrames frames, StringName animation) {
            if (!frames.HasAnimation(animation)) return 0f;
            float best = 0f;
            for (int frame = 0; frame < frames.GetFrameCount(animation); frame++) {
                // No using/Dispose: Image is RefCounted and reference counting
                // owns its lifetime (repo rule; disposing risks the
                // double-dispose CRASH_COND).
                Texture2D texture = frames.GetFrameTexture(animation, frame);
                Image image = texture?.GetImage();
                if (image == null) continue;
                best = Mathf.Max(best, image.GetUsedRect().Size.Y);
            }
            return best;
        }

        private static float CellHeight(SpriteFrames frames, StringName animation) {
            if (frames != null && frames.HasAnimation(animation) && frames.GetFrameCount(animation) > 0) {
                Texture2D texture = frames.GetFrameTexture(animation, 0);
                if (texture != null) return texture.GetHeight();
            }
            return 128f;
        }
    }
}
