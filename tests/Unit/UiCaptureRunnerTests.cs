using FTT.Diagnostics;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// UR-THEME. The UI capture tool's argument parsing and its target registry. A capture itself needs a
/// window, so these only cover what a headless run can: the arguments, the discovery and that each
/// theme target builds a control that frees cleanly.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class UiCaptureRunnerTests {

    [TestCase]
    public void TheArgumentsParseTargetsOutputAndList() {
        var options = UiCaptureRunner.ParseArguments(new[] {
            "--capture=theme_controls,main_menu_root", "--out=D:/shots", "--unknown=1" });
        AssertThat(options.Capture.Count).IsEqual(2);
        AssertThat(options.Capture[0]).IsEqual("theme_controls");
        AssertThat(options.Capture[1]).IsEqual("main_menu_root");
        AssertThat(options.Out).IsEqual("D:/shots");
        AssertThat(options.List).IsFalse();

        var defaults = UiCaptureRunner.ParseArguments(new[] { "--list" });
        AssertThat(defaults.List).IsTrue();
        AssertThat(defaults.Capture.Count).IsEqual(0);
        AssertThat(defaults.Out).IsEqual("user://ui_captures");
    }

    [TestCase]
    public void TheRegistryDiscoversAttributedTargetsFromEveryPartialFile() {
        var all = UiCaptureTargets.All;
        foreach (string name in new[] {
            "theme_labels_small", "theme_labels_large", "theme_controls", "theme_palette", "main_menu_root" }) {
            AssertThat(all.ContainsKey(name))
                .OverrideFailureMessage($"capture target '{name}' is not registered").IsTrue();
        }
    }

    [TestCase]
    public void EveryThemeSpecimenTargetBuildsAControlThatFreesCleanly() {
        foreach (var pair in UiCaptureTargets.All) {
            if (!pair.Key.StartsWith("theme_")) continue;
            Node built = pair.Value.Build(new UiCaptureContext());
            try {
                AssertObject(built).IsNotNull();
                AssertThat(built is Control).IsTrue();
                AssertThat(built.GetChildCount() > 0).IsTrue();
            } finally {
                built.Free();
            }
        }
    }
}
