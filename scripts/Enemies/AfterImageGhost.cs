using Godot;

namespace FTT.Enemies {

    /// <summary>
    /// Package 12 W9 (M18) — placeholder after-image left behind by an ability
    /// authored with <see cref="EnemyAbilityData.LeavesAfterImages"/> (the Borgia
    /// Inquisitor's Phase 2 after-image dash). A tinted snapshot of the caster's
    /// current frame that fades out over <see cref="LifetimeSeconds"/> and frees
    /// itself. Pure presentation: no collision, no gameplay state, and never
    /// spawned while Reduced Temporal Effects is on (the caster checks
    /// <c>ComfortSettings.GhostTrailsAllowed</c>). Package 10 replaces the look.
    /// </summary>
    public partial class AfterImageGhost : Sprite2D {
        public const string GroupName = "boss_after_image";
        public const float LifetimeSeconds = 0.3f;
        public static readonly Color GhostTint = new(0.95f, 0.35f, 0.4f, 0.55f);

        private float _remaining = LifetimeSeconds;

        public static AfterImageGhost From(AnimatedSprite2D source) {
            Texture2D frame = null;
            if (source?.SpriteFrames != null && source.SpriteFrames.HasAnimation(source.Animation)) {
                frame = source.SpriteFrames.GetFrameTexture(source.Animation, source.Frame);
            }
            var ghost = new AfterImageGhost {
                Name = "AfterImage",
                Texture = frame,
                FlipH = source?.FlipH ?? false,
                Offset = source?.Offset ?? Vector2.Zero,
                Centered = source?.Centered ?? true,
                Scale = source?.Scale ?? Vector2.One,
                Modulate = GhostTint,
                ZIndex = -1
            };
            return ghost;
        }

        public override void _Ready() => AddToGroup(GroupName);

        public override void _Process(double delta) {
            _remaining -= (float)delta;
            float alpha = Mathf.Clamp(_remaining / LifetimeSeconds, 0f, 1f) * GhostTint.A;
            Modulate = new Color(GhostTint.R, GhostTint.G, GhostTint.B, alpha);
            if (_remaining <= 0f) QueueFree();
        }
    }
}
