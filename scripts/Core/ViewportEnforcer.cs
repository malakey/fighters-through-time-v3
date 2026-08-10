using Godot;

namespace FTT.Core {

    /// <summary>
    /// Owns the window/viewport contract: the fixed 1920x1080 reference canvas with
    /// `keep` aspect and black letterbox/pillarbox areas (AGENTS.md).
    ///
    /// <para>Package 8 A4 also made it the boot-time applier of the persisted
    /// display settings. It is the <b>last</b> autoload in project.godot, so
    /// <see cref="SaveManager"/>'s global payload is already loaded when
    /// <see cref="_Ready"/> runs; putting the apply here keeps every window-level
    /// concern in one place rather than splitting it across GameManager.</para>
    /// </summary>
    public partial class ViewportEnforcer : Node {
        public static readonly Vector2I ReferenceSize = new(1920, 1080);

        public override void _Ready() {
            Window window = GetTree().Root;
            window.ContentScaleSize = ReferenceSize;
            window.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
            window.ContentScaleAspect = Window.ContentScaleAspectEnum.Keep;
            RenderingServer.SetDefaultClearColor(Colors.Black);
            ApplyDisplaySettings(SaveManager.Instance?.GlobalData);
        }

        /// <summary>
        /// Applies persisted resolution / window mode / VSync. A headless run has no
        /// real window, so the DisplayServer calls are skipped there rather than
        /// logging errors through every test session.
        /// </summary>
        public static void ApplyDisplaySettings(GlobalSaveData data) {
            if (data == null) return;
            data.Normalize();
            if (DisplayServer.GetName() == "headless") return;

            DisplayServer.WindowSetVsyncMode(data.VSyncEnabled
                ? DisplayServer.VSyncMode.Enabled
                : DisplayServer.VSyncMode.Disabled);

            DisplayServer.WindowSetMode(ResolveWindowMode(data.WindowMode));

            // A window size only means anything in windowed mode; a fullscreen
            // window owns the display's size.
            if (data.WindowMode == WindowModeSetting.Windowed) {
                DisplayServer.WindowSetSize(new Vector2I(data.ResolutionWidth, data.ResolutionHeight));
            }
        }

        /// <summary>
        /// Maps the persisted setting onto the engine's window modes. Godot's
        /// naming is the trap the audit's window-mode Low found crossed here:
        /// <see cref="DisplayServer.WindowMode.Fullscreen"/> is the <i>borderless</i>
        /// fullscreen window, while <c>ExclusiveFullscreen</c> is true fullscreen.
        /// So the "Fullscreen" setting maps to ExclusiveFullscreen and the
        /// "Borderless" setting maps to Fullscreen.
        /// </summary>
        public static DisplayServer.WindowMode ResolveWindowMode(WindowModeSetting setting) => setting switch {
            WindowModeSetting.Fullscreen => DisplayServer.WindowMode.ExclusiveFullscreen,
            WindowModeSetting.BorderlessFullscreen => DisplayServer.WindowMode.Fullscreen,
            _ => DisplayServer.WindowMode.Windowed
        };
    }
}
