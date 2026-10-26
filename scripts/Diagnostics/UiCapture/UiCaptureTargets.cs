using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

namespace FTT.Diagnostics {

    /// <summary>
    /// UR-THEME. Marks a static method of <see cref="UiCaptureTargets"/> as a screen the capture tool can
    /// photograph. The method takes a <see cref="UiCaptureContext"/> and returns the node to show
    /// (a <see cref="Control"/> that fills the 1920 x 1080 frame, or a scene root).
    ///
    /// <para><b>Adding a target (Phase B).</b> Add your own <c>partial class UiCaptureTargets</c> file,
    /// <c>UiCaptureTargets.&lt;Workstream&gt;.cs</c>, with attributed static methods; no shared file is
    /// edited, so workstreams never conflict. See <c>docs/handoffs/UR_THEME.md</c>.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class UiCaptureTargetAttribute : Attribute {
        public string Name { get; }

        /// <summary>Process frames to wait after the node is added and before the grab (layout, tweens, focus).</summary>
        public int SettleFrames { get; set; } = 8;

        public UiCaptureTargetAttribute(string name) => Name = name;
    }

    /// <summary>What a target receives: the frame size and a place to run setup after the node is in the tree.</summary>
    public sealed class UiCaptureContext {
        /// <summary>The capture frame, 1920 x 1080 (the project's reference resolution).</summary>
        public Vector2I FrameSize { get; init; } = new(1920, 1080);

        /// <summary>The control the node is parented under (full rect, shared theme not applied: screens adopt their own).</summary>
        public Control Stage { get; init; }

        /// <summary>
        /// Optional setup that runs after the returned node has entered the tree and settled, then waits
        /// <c>SettleFrames</c> again (open a tab, grab focus, press a toggle). May be null.
        /// </summary>
        public Action<Node> AfterReady { get; set; }
    }

    /// <summary>
    /// The registry of capture targets. Partial: each workstream's targets live in its own file.
    /// Discovery is by reflection over <see cref="UiCaptureTargetAttribute"/>.
    /// </summary>
    public static partial class UiCaptureTargets {

        public delegate Node Builder(UiCaptureContext context);

        public readonly record struct Target(string Name, int SettleFrames, Builder Build);

        private static SortedDictionary<string, Target> _all;

        /// <summary>Every registered target by name.</summary>
        public static IReadOnlyDictionary<string, Target> All {
            get {
                if (_all != null) return _all;
                _all = new SortedDictionary<string, Target>(StringComparer.Ordinal);
                foreach (MethodInfo method in typeof(UiCaptureTargets).GetMethods(
                             BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)) {
                    foreach (UiCaptureTargetAttribute attribute in method.GetCustomAttributes<UiCaptureTargetAttribute>()) {
                        var build = (Builder)Delegate.CreateDelegate(typeof(Builder), method);
                        if (_all.ContainsKey(attribute.Name)) {
                            GD.PushError($"[ui-capture] duplicate target '{attribute.Name}'");
                            continue;
                        }
                        _all[attribute.Name] = new Target(attribute.Name, attribute.SettleFrames, build);
                    }
                }
                return _all;
            }
        }
    }
}
