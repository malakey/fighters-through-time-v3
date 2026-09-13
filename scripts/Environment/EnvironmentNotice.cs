using Godot;

namespace FTT.Environment {

    /// <summary>
    /// Package 11 A3 (E01 drain feedback): a brief, non-blocking world-space
    /// notice posted above a world object.
    ///
    /// Deliberately minimal and deliberately NOT a HUD element — A8 owns the
    /// HUD, and this must not become a second Integrity readout. E01 is
    /// explicit about what it may say: "the drain slowed", and nothing else.
    /// No time-gained figure, no percentage, no seconds-saved estimate, no
    /// numeric drain readout. Dust keeps its own separate <c>+N</c> feedback.
    ///
    /// Not pooled: a level posts at most a handful of these, on discrete
    /// one-shot events, so the pooling contract would cost more than it saves.
    /// The label frees itself and runs <see cref="Node.ProcessModeEnum.Pausable"/>
    /// so a pause or a frozen world holds it in place rather than expiring it
    /// behind a menu.
    /// </summary>
    public static class EnvironmentNotice {

        /// <summary>Seconds the notice stays up before fading out.</summary>
        public const float DefaultSeconds = 2.5f;

        /// <summary>The cool cyan the Integrity family reads in.</summary>
        public static readonly Color DefaultColor = new(0.55f, 0.9f, 1f);

        /// <summary>
        /// Posts <paramref name="translationKey"/> above <paramref name="anchor"/>.
        /// Null-safe and headless-safe: with no anchor, no parent or no tree,
        /// nothing happens and nothing throws.
        /// </summary>
        public static Label Post(
            string translationKey,
            Node2D anchor,
            float seconds = DefaultSeconds,
            Color? color = null) {
            if (string.IsNullOrWhiteSpace(translationKey) || anchor == null
                || !GodotObject.IsInstanceValid(anchor) || !anchor.IsInsideTree()) {
                return null;
            }
            Node parent = anchor.GetParent();
            if (parent == null) return null;

            var label = new Label {
                Name = "EnvironmentNotice",
                Text = TranslationServer.Translate(translationKey),
                Position = anchor.Position + new Vector2(-140f, -190f),
                CustomMinimumSize = new Vector2(280f, 20f),
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ProcessMode = Node.ProcessModeEnum.Pausable
            };
            label.AddThemeFontSizeOverride("font_size", 14);
            label.AddThemeColorOverride("font_color", color ?? DefaultColor);
            parent.AddChild(label);

            var timer = label.GetTree()?.CreateTimer(Mathf.Max(0.1f, seconds), processAlways: false);
            timer?.Connect(
                SceneTreeTimer.SignalName.Timeout,
                Callable.From(() => {
                    if (GodotObject.IsInstanceValid(label)) label.QueueFree();
                }),
                (uint)GodotObject.ConnectFlags.OneShot);
            return label;
        }
    }
}
