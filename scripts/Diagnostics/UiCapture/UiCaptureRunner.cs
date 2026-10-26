using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace FTT.Diagnostics {

    /// <summary>
    /// UR-THEME. Windowed screenshot harness for the pulp UI redesign. Renders each requested target into a
    /// 1920 x 1080 <see cref="SubViewport"/> (so the PNG is exact whatever the window size) and writes
    /// <c>&lt;out&gt;/&lt;name&gt;.png</c>.
    ///
    /// <code>
    /// Godot_..._console.exe --path . res://scenes/diagnostics/UiCaptureRunner.tscn -- --capture=theme_controls,main_menu --out=D:/Projects/ftt-scratch/ur-captures/theme
    /// </code>
    ///
    /// Options (after <c>--</c>): <c>--capture=a,b</c> or <c>all</c>; <c>--out=dir</c> (default
    /// <c>user://ui_captures</c>); <c>--list</c> prints the registered targets and quits. It needs a
    /// window: run the console build <b>without</b> <c>--headless</b>, briefly, holding the GdUnit lock
    /// (CLAUDE.md). Exit code 0 on success, 2 for a bad argument or an unknown target, 3 if any target failed.
    /// </summary>
    public partial class UiCaptureRunner : Node {

        public override void _Ready() => _ = RunAsync();

        private async Task RunAsync() {
            var options = ParseArguments(OS.GetCmdlineUserArgs());
            if (options.List) {
                foreach (var name in UiCaptureTargets.All.Keys) GD.Print(name);
                GetTree().Quit(0);
                return;
            }

            var names = new List<string>();
            foreach (string requested in options.Capture) {
                if (requested == "all") names.AddRange(UiCaptureTargets.All.Keys);
                else names.Add(requested);
            }
            foreach (string name in names) {
                if (!UiCaptureTargets.All.ContainsKey(name)) {
                    GD.PushError($"[ui-capture] unknown target '{name}'. Use --list.");
                    GetTree().Quit(2);
                    return;
                }
            }
            if (names.Count == 0) {
                GD.PushError("[ui-capture] nothing to capture: pass --capture=<name[,name]|all> or --list.");
                GetTree().Quit(2);
                return;
            }

            string outDir = options.Out;
            if (DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(outDir)) != Error.Ok
                && !DirAccess.DirExistsAbsolute(ProjectSettings.GlobalizePath(outDir))) {
                GD.PushError($"[ui-capture] cannot create '{outDir}'.");
                GetTree().Quit(2);
                return;
            }

            // UR-POLISH: the shared theme is not pinned by anything (ResourceLoader drops it once the last screen is
            // freed), so a scale applied to it between two targets was lost with the instance and the next screen
            // loaded the authored sizes: every 1.4 target of a batch came out as its 1.0 twin. Holding it for the
            // whole run keeps one instance, and the scale (and the saved UiScale setting) is reset around every target
            // so one target's scale never leaks into the next.
            _themeHold = FTT.UI.UIPalette.LoadTheme();
            float savedSetting = FTT.Core.SaveManager.Instance?.GlobalData?.UiScale ?? 1f;
            int failures = 0;
            foreach (string name in names) {
                try {
                    ResetScale(savedSetting);
                    await CaptureAsync(UiCaptureTargets.All[name], outDir);
                } catch (Exception exception) {
                    failures++;
                    GD.PushError($"[ui-capture] '{name}' failed: {exception}");
                } finally {
                    ResetScale(savedSetting);
                }
            }
            GetTree().Quit(failures == 0 ? 0 : 3);
        }

        private Theme _themeHold;

        internal static void ResetScale(float setting) {
            if (FTT.Core.SaveManager.Instance?.GlobalData != null) FTT.Core.SaveManager.Instance.GlobalData.UiScale = setting;
            FTT.UI.UIPalette.ApplyUiScale(1f);
        }

        private async Task CaptureAsync(UiCaptureTargets.Target target, string outDir) {
            var viewport = new SubViewport {
                Size = new Vector2I(1920, 1080),
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
                TransparentBg = false,
                HandleInputLocally = true,
                GuiEmbedSubwindows = true
            };
            AddChild(viewport);

            var stage = new Control { Name = "Stage" };
            stage.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            viewport.AddChild(stage);
            var backdrop = new ColorRect { Name = "Backdrop", Color = FTT.UI.UIPalette.Backdrop };
            backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            stage.AddChild(backdrop);

            var context = new UiCaptureContext { Stage = stage };
            Node shown = target.Build(context);
            stage.AddChild(shown);
            if (shown is Control control) {
                control.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            }

            await Frames(target.SettleFrames);
            if (context.AfterReady != null) {
                context.AfterReady(shown);
                await Frames(target.SettleFrames);
            }

            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            Image image = viewport.GetTexture().GetImage();
            string path = $"{outDir.TrimEnd('/', '\\')}/{target.Name}.png";
            Error error = image.SavePng(path);
            if (error != Error.Ok) throw new InvalidOperationException($"SavePng('{path}') returned {error}");
            GD.Print($"[ui-capture] {target.Name} -> {path} ({image.GetWidth()}x{image.GetHeight()})");

            shown.QueueFree();
            viewport.QueueFree();
            await Frames(2);
        }

        private async Task Frames(int count) {
            for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        /// <summary>The parsed command line.</summary>
        public readonly record struct Options(List<string> Capture, string Out, bool List);

        /// <summary>Parses <c>--capture=</c>, <c>--out=</c> and <c>--list</c>; unknown arguments are ignored.</summary>
        public static Options ParseArguments(string[] args) {
            var capture = new List<string>();
            string output = "user://ui_captures";
            bool list = false;
            foreach (string arg in args) {
                if (arg == "--list") list = true;
                else if (arg.StartsWith("--capture=")) {
                    foreach (string part in arg.Substring("--capture=".Length).Split(',', StringSplitOptions.RemoveEmptyEntries)) {
                        capture.Add(part.Trim());
                    }
                } else if (arg.StartsWith("--out=")) output = arg.Substring("--out=".Length);
            }
            return new Options(capture, output, list);
        }
    }
}
