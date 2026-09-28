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
    /// <summary>
    /// G04 display timing (design Section 12): a fixed 60 Hz simulation, rendering
    /// capped at 60 FPS with V-Sync on or off, and no physics interpolation — every
    /// presented frame is an exact simulation state.
    /// </summary>
    public static class DisplayTimingRules {
        public const int MaxFramesPerSecond = 60;
        public const int PhysicsTicksPerSecond = 60;
        public const string MaxFpsSetting = "application/run/max_fps";
        public const string PhysicsTicksSetting = "physics/common/physics_ticks_per_second";
        public const string PhysicsInterpolationSetting = "physics/common/physics_interpolation";

        /// <summary>The render cap to apply: 60 on any real display, 0 (uncapped) headless.</summary>
        public static int EffectiveMaxFps(bool headless) => headless ? 0 : MaxFramesPerSecond;
    }

    public partial class ViewportEnforcer : Node {
        public static readonly Vector2I ReferenceSize = new(1920, 1080);

        public override void _Ready() {
            Window window = GetTree().Root;
            window.ContentScaleSize = ReferenceSize;
            window.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
            window.ContentScaleAspect = Window.ContentScaleAspectEnum.Keep;
            RenderingServer.SetDefaultClearColor(Colors.Black);
            ApplyDisplaySettings(SaveManager.Instance?.GlobalData);
            // Accessibility UI scale: rescale the shared theme before the first
            // screen adopts it. This is the last autoload, so the saved value
            // is already loaded.
            FTT.UI.UIPalette.ApplySavedUiScale();
            // Package 12 W6. G15d: the build version goes into every log. G09:
            // this is the last autoload, so the saved crash-report choice is
            // loaded — arm the session marker and act on a previous crash.
            BuildInfo.LogBootBanner();
            CrashReportService.OnBoot(SaveManager.Instance?.GlobalData?.CrashReports ?? CrashReportMode.Ask);
        }

        /// <summary>
        /// Clean shutdown: disarms the G09 session marker so the next boot does not
        /// read this exit as a crash.
        /// </summary>
        public override void _ExitTree() => CrashReportService.OnCleanExit();

        /// <summary>
        /// Applies persisted resolution / window mode / VSync. A headless run has no
        /// real window, so the DisplayServer calls are skipped there rather than
        /// logging errors through every test session.
        /// </summary>
        public static void ApplyDisplaySettings(GlobalSaveData data) {
            if (data == null) return;
            data.Normalize();
            // G04 (design Section 12 "Display timing"): rendering is capped at the
            // simulation rate whether V-Sync is on or off. The cap is authored in
            // project.godot (application/run/max_fps) and re-asserted here so a
            // V-Sync toggle can never leave an uncapped renderer behind.
            // A headless run presents nothing, so it has no render rate to cap;
            // leaving it uncapped keeps headless test and smoke runs as fast as
            // they were before the cap existed.
            bool headless = DisplayServer.GetName() == "headless";
            Engine.MaxFps = DisplayTimingRules.EffectiveMaxFps(headless);
            if (headless) return;

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
