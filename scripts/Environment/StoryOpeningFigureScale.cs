using FTT.Combat;
using Godot;

namespace FTT.Environment {
    /// <summary>Opening-only figure scale; the normalizer keeps every pose anchored to the same feet.</summary>
    public sealed class StoryOpeningFigureScale {
        public const float MaximumMultiplier = 2.5f;
        private readonly RetroSpriteScaleNormalizer _normalizer;
        private readonly float _original;
        private readonly AnimatedSprite2D _sprite;
        private readonly Label _label;
        private readonly Vector2 _originalLabelPosition;
        public float Multiplier { get; }
        public float AchievedFraction { get; }

        public StoryOpeningFigureScale(AnimatedSprite2D sprite, float frameHeight, float requestedFraction) {
            _sprite = sprite;
            _label = sprite?.GetParent()?.GetNodeOrNull<Label>("NameLabel");
            _originalLabelPosition = _label?.Position ?? Vector2.Zero;
            _normalizer = RetroSpriteScaleNormalizer.Attach(sprite, RetroSpriteScaleNormalizer.FigureKind.Character);
            _original = _normalizer?.FigureScale ?? 1f;
            float idleHeight = MeasureHeight(sprite?.SpriteFrames, "idle");
            float currentHeight = MeasureHeight(sprite?.SpriteFrames, sprite?.Animation ?? "idle");
            float pose = RetroSpriteScaleNormalizer.CharacterPoseFraction(sprite?.Animation.ToString() ?? "idle");
            float correction = sprite != null && !RetroSpriteMetadata.IsBakedScale(sprite.SpriteFrames)
                && currentHeight > 0f && pose > 0f
                ? Mathf.Clamp(pose * idleHeight / currentHeight, 1f, RetroSpriteScaleNormalizer.MaxBoost) : 1f;
            float worldHeight = idleHeight * Mathf.Abs(sprite?.Scale.Y ?? 1f) / correction;
            Multiplier = worldHeight > 0f && frameHeight > 0f && requestedFraction > 0f
                ? Mathf.Clamp(frameHeight * requestedFraction / worldHeight, 0.5f, MaximumMultiplier) : 1f;
            AchievedFraction = frameHeight > 0f ? worldHeight * Multiplier / frameHeight : 0f;
            SetWeight(1f);
        }

        public void SetWeight(float weight) {
            weight = Mathf.Clamp(weight, 0f, 1f);
            if (_normalizer != null && GodotObject.IsInstanceValid(_normalizer))
                _normalizer.FigureScale = _original * Mathf.Lerp(1f, Multiplier, weight);
            if (_label == null || !GodotObject.IsInstanceValid(_label)) return;
            if (weight <= 0f) { _label.Position = _originalLabelPosition; return; }
            if (_sprite == null || !GodotObject.IsInstanceValid(_sprite)) return;
            Texture2D texture = _sprite.SpriteFrames?.GetFrameTexture(_sprite.Animation, _sprite.Frame);
            Image image = texture?.GetImage();
            if (image == null) return;
            Rect2I used = image.GetUsedRect();
            float localTop = used.Position.Y + _sprite.Offset.Y - (_sprite.Centered ? texture.GetHeight() / 2f : 0f);
            float top = _sprite.Position.Y + localTop * _sprite.Scale.Y;
            float labelHeight = Mathf.Max(_label.Size.Y, _label.GetCombinedMinimumSize().Y);
            float above = Mathf.Min(_originalLabelPosition.Y, top - labelHeight - 4f);
            _label.Position = new Vector2(_originalLabelPosition.X,
                Mathf.Lerp(_originalLabelPosition.Y, above, Mathf.Clamp(weight * 4f, 0f, 1f)));
        }

        public void Restore() => SetWeight(0f);

        private static float MeasureHeight(SpriteFrames frames, StringName animation) {
            if (frames == null || !frames.HasAnimation(animation)) return 0f;
            float height = 0f;
            for (int i = 0; i < frames.GetFrameCount(animation); i++) {
                Image image = frames.GetFrameTexture(animation, i)?.GetImage();
                if (image != null) height = Mathf.Max(height, image.GetUsedRect().Size.Y);
            }
            return height;
        }
    }
}
